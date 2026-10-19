namespace AVADefenderOfTheCrown.Engine;

/// <summary>What marching into a shire comes to: walking in, a battle in the field, or a siege.</summary>
public enum Venture { None, March, Battle, Siege, NeedCatapult }

/// <summary>Who stands in a shire's way: their troops, their lord (or -1 for its vassals), and the walls round them.</summary>
public readonly record struct Defence(Troops Troops, int Lord, double Walls, bool Castle, bool Army);

/// <summary>
/// The rules of the campaign: setting up the realm, money and armies, marching and conquering, castles falling,
/// tournaments, raids, the outlaw's help, the lady, the other lords' turns, the seasons, the end and the score.
/// </summary>
public static class Sim
{
    /// <summary>How much an unbroken castle wall multiplies its garrison's worth in a fight.</summary>
    public const double CastleWalls = 3.0;
    public const int GilRaidShare = 2;
    /// <summary>The silver plate a successful raid carries off from the strongroom, besides half the lord's gold.</summary>
    public const int RaidPlate = 5;
    /// <summary>No lord storms a castle before this season: the realm is still taking in the king's death.</summary>
    public const int CastleGrace = 4;
    /// <summary>What the computer lords reckon a castle's walls are worth after their catapults have been at them.</summary>
    public const double ExpectedWalls = 2.2;
    /// <summary>The knights who land from Normandy for each Norman lord every season.</summary>
    public const int NormanReinforcements = 0;

    // ───────────────────────────── the realm at the king's death ─────────────────────────────

    public static void Setup(World w, int player, int seed)
    {
        w.Player = player;
        w.Dice = new Dice(seed);
        w.Turn = 1;
        Array.Fill(w.Owner, Realm.Nobody);
        w.Owner[Realm.Sherwood] = Realm.Outlaws;
        w.Lords = new Lord[Realm.LordCount];
        for (int i = 0; i < Realm.LordCount; i++)
        {
            var info = Realm.Lords[i];
            bool norman = info.Side == Side.Norman;
            w.Lords[i] = new Lord
            {
                Id = i, Castle = info.Home, At = info.Home,
                Gold = norman ? 30 : 20,
                Army = norman ? new Troops(24, 5, 1) : i == player ? new Troops(16, 3) : new Troops(12, 2),
                Garrison = norman ? new Troops(18, 4) : i == player ? new Troops(16, 3) : new Troops(14, 2),
            };
            w.Owner[info.Home] = i;
        }
        // The Normans came with more than their castles.
        w.Owner[Realm.Sussex] = Realm.Hugh;
        w.Owner[Realm.Derby] = Realm.Raoul;
        w.Owner[Realm.Hereford] = Realm.Guy;
        w.LadyTurn = 3 + w.Dice.Next(4);
        w.Cards.Add(new Card("THE KING IS DEAD",
            "The king has died with no heir, and the Norman lords move to seize his crown. " +
            $"{Realm.Lords[player].Name}, the Saxon cause looks to you: win England shire by shire and take the three Norman castles.",
            player, CardKind.Story));
    }

    public static int Income(World w, int lord) => w.Lands(lord).Sum(s => Realm.Shires[s].Income);

    /// <summary>The vassals who hold a shire nobody has claimed (and who raise half as many for a lord with no army there).</summary>
    public static Troops Levy(int shire) => new(2 + 2 * Realm.Shires[shire].Income, Realm.Shires[shire].Income / 3);

    // ───────────────────────────── money and men ─────────────────────────────

    public static int Price(int soldiers, int knights, int catapults) =>
        soldiers * Realm.SoldierCost + knights * Realm.KnightCost + catapults * Realm.CatapultCost;

    /// <summary>Buys troops for a lord: they join his campaign army (wherever it is) or his castle's garrison. Catapults always go with the army.</summary>
    public static bool Buy(World w, int lord, int soldiers, int knights, int catapults, bool toGarrison)
    {
        var l = w.Lords[lord];
        int cost = Price(soldiers, knights, catapults);
        if (!l.Alive || soldiers < 0 || knights < 0 || catapults < 0 || cost > l.Gold || cost == 0) return false;
        l.Gold -= cost;
        if (toGarrison)
        {
            l.Garrison += new Troops(soldiers, knights);
            if (catapults > 0) Join(l, new Troops(0, 0, catapults));
        }
        else Join(l, new Troops(soldiers, knights, catapults));
        if (lord == w.Player) w.Heard.Add(Sfx.Coins);
        return true;
    }

    /// <summary>Troops join the campaign army; a lord with no army left raises a new one at his castle.</summary>
    private static void Join(Lord l, Troops t)
    {
        if (l.Army.Empty && l.Army.Catapults == 0) l.At = l.Castle;
        l.Army += t;
    }

    /// <summary>Moves men between the campaign army and the garrison: only with the army at the castle.</summary>
    public static bool Transfer(World w, int soldiers, int knights, bool toGarrison)
    {
        var me = w.Me;
        if (!me.Alive || soldiers < 0 || knights < 0 || soldiers + knights == 0) return false;
        if (!(me.At == me.Castle || me.Army.Empty)) return false;
        var from = toGarrison ? me.Army : me.Garrison;
        if (soldiers > from.Soldiers || knights > from.Knights) return false;
        var t = new Troops(soldiers, knights);
        if (toGarrison)
        {
            me.Army -= t;
            me.Garrison += t;
        }
        else
        {
            me.Garrison -= t;
            Join(me, t);
        }
        return true;
    }

    // ───────────────────────────── marching and conquering ─────────────────────────────

    /// <summary>The shires a lord's campaign army can march into from where it stands.</summary>
    public static IEnumerable<int> Marches(World w, int lord)
    {
        var l = w.Lords[lord];
        if (!l.Alive || l.Army.Empty) return [];
        return Realm.Neighbours(l.At).Where(s => s != Realm.Sherwood);
    }

    /// <summary>Who would stand against <paramref name="attacker"/> marching into <paramref name="shire"/>.</summary>
    public static Defence Defenders(World w, int shire, int attacker)
    {
        var castle = w.CastleOf(shire);
        if (castle != null && castle.Id != attacker)
        {
            var t = castle.Garrison;
            if (castle.At == shire) t += new Troops(castle.Army.Soldiers, castle.Army.Knights);
            return new Defence(t, castle.Id, CastleWalls, true, castle.At == shire && !castle.Army.Empty);
        }
        if (w.ArmyIn(shire, attacker) is { } army) return new Defence(army.Army, army.Id, 1, false, true);
        int owner = w.Owner[shire];
        if (owner == attacker) return new Defence(default, owner, 1, false, false);
        if (owner == Realm.Nobody) return new Defence(Levy(shire), Realm.Nobody, 1, false, false);
        var levy = Levy(shire);
        return new Defence(new Troops(levy.Soldiers / 2, levy.Knights / 2), owner, 1, false, false);
    }

    /// <summary>What marching into a shire would come to for a lord.</summary>
    public static Venture Plan(World w, int lord, int shire)
    {
        var l = w.Lords[lord];
        if (!Marches(w, lord).Contains(shire)) return Venture.None;
        var d = Defenders(w, shire, lord);
        if (d.Castle) return l.Army.Catapults > 0 ? Venture.Siege : Venture.NeedCatapult;
        return d.Troops.Empty ? Venture.March : Venture.Battle;
    }

    /// <summary>A lord's army has won a shire: it moves in with what's left of it, and a castle there falls.</summary>
    public static void Conquer(World w, int lord, int shire, Troops survivors)
    {
        var l = w.Lords[lord];
        var d = w.CastleOf(shire);
        foreach (var other in w.Lords)
            if (other.Id != lord && other.At == shire && other.Alive && other != d)
            {
                // A beaten army there is scattered home.
                other.Army = default;
                other.At = other.Castle;
            }
        l.Army = survivors;
        l.At = shire;
        if (d != null && d.Id != lord) Fall(w, lord, d);
        else w.Owner[shire] = lord;
    }

    /// <summary>A castle falls: its lord is finished, and his lands and treasury go to the lord who took it.</summary>
    public static void Fall(World w, int captor, Lord fallen)
    {
        int shire = fallen.Castle;
        for (int i = 0; i < w.Owner.Length; i++)
            if (w.Owner[i] == fallen.Id) w.Owner[i] = captor;
        w.Owner[shire] = captor;
        w.Lords[captor].Gold += fallen.Gold;
        fallen.Gold = 0;
        fallen.Army = default;
        fallen.Garrison = default;
        fallen.Castle = -1;
        fallen.At = -1;
        var c = Realm.Lords[captor];
        if (fallen.Id == w.Player)
        {
            w.Lost = true;
            w.Cards.Add(new Card($"{Realm.Lords[fallen.Id].CastleName.ToUpperInvariant()} HAS FALLEN",
                $"{c.Name} has stormed your castle. The Saxon cause is lost, and you with it.", captor, CardKind.Defeat, shire));
            return;
        }
        if (captor == w.Player && Realm.IsNorman(fallen.Id)) w.NormansBeaten++;
        w.Cards.Add(new Card($"{Realm.Lords[fallen.Id].CastleName.ToUpperInvariant()} HAS FALLEN",
            $"{(captor == w.Player ? "You have" : c.Name + " has")} taken the castle of {Realm.Lords[fallen.Id].Name}. All his lands now answer to {(captor == w.Player ? "you" : c.Short)}.",
            captor, captor == w.Player ? CardKind.Victory : CardKind.War, shire));
        if (w.Lady == LadyState.Held && w.LadyHeldBy == fallen.Id && captor == w.Player) Marry(w);
        else if (w.Lady == LadyState.Held && w.LadyHeldBy == fallen.Id) { w.LadyHeldBy = captor; }
        CheckEnd(w);
    }

    /// <summary>An attack beaten off: the attackers stay where they marched from with what's left of them.</summary>
    public static void Repulsed(World w, int lord, Troops survivors)
    {
        var l = w.Lords[lord];
        l.Army = survivors;
        if (l.Army.Empty) l.At = l.Castle;
    }

    /// <summary>Fights a battle out between two lords (or a lord and some vassals) with no one at the reins.</summary>
    public static Battle AutoBattle(World w, int lord, int shire, Defence d)
    {
        var a = w.Lords[lord];
        double walls = d.Castle ? 1 + (CastleWalls - 1) * (1 - w.Dice.Range(0.2, 0.6)) : 1;
        var b = new Battle(a.Army, lord, d.Troops, d.Lord, walls, -1, w.Dice.Next(1 << 30), shire);
        b.Resolve();
        return b;
    }

    /// <summary>The result of a battle for a shire, whoever fought it: the winners and losers take their places.</summary>
    public static void Settle(World w, int lord, int shire, Defence d, Battle b)
    {
        var attackers = b.Left(0);
        var defenders = b.Left(1);
        if (b.Winner == 0)
        {
            if (d.Castle && w.CastleOf(shire) is { } c) { c.Garrison = default; c.Army = default; c.At = c.Castle; }
            else if (d.Army && d.Lord >= 0)
            {
                var beaten = w.Lords[d.Lord];
                beaten.Army = defenders;
                beaten.At = beaten.Castle;
            }
            Conquer(w, lord, shire, attackers);
        }
        else
        {
            if (d.Castle && w.CastleOf(shire) is { } c)
            {
                // What's left of the garrison (and of the lord's army, if it was there) holds the castle.
                int s = defenders.Soldiers, k = defenders.Knights;
                var army = c.At == shire ? c.Army : default;
                int armyS = Math.Min(army.Soldiers, s / 2), armyK = Math.Min(army.Knights, k / 2);
                if (c.At == shire) c.Army = new Troops(armyS, armyK, army.Catapults);
                c.Garrison = new Troops(s - armyS, k - armyK);
            }
            else if (d.Army && d.Lord >= 0) w.Lords[d.Lord].Army = defenders with { Catapults = w.Lords[d.Lord].Army.Catapults };
            Repulsed(w, lord, attackers);
        }
    }

    // ───────────────────────────── tournaments, raids, the outlaw and the lady ─────────────────────────────

    /// <summary>The lords who'd ride against the player in a tournament (for land, only those with land to lose).</summary>
    public static IEnumerable<int> TournamentFoes(World w, bool forLand) =>
        w.Lords.Where(l => l.Alive && l.Id != w.Player && (!forLand || PrizeLand(w, l.Id) >= 0)).Select(l => l.Id);

    /// <summary>The best shire a lord would put up as a tournament prize: his richest one that isn't his castle's.</summary>
    public static int PrizeLand(World w, int lord) =>
        w.Lands(lord).Where(s => s != w.Lords[lord].Castle).OrderByDescending(s => Realm.Shires[s].Income).ThenBy(s => s).DefaultIfEmpty(-1).First();

    /// <summary>A tournament's outcome: land changes hands (or fame and a purse are won) by the joust.</summary>
    public static void Tournament(World w, int foe, bool forLand, int result)
    {
        w.Tournaments++;
        var me = w.Me;
        var f = Realm.Lords[foe];
        if (result > 0)
        {
            w.Renown++;
            w.Heard.Add(Sfx.Fanfare);
            if (forLand && PrizeLand(w, foe) is var s and >= 0)
            {
                TakeLand(w, s, w.Player);
                w.Cards.Add(new Card("VICTORY IN THE LISTS", $"You unseat {f.Name} before the crowd, and {Realm.Shires[s].Name} is yours.", w.Player, CardKind.Victory, s));
            }
            else
            {
                me.Gold += 10;
                Join(me, new Troops(0, 2));
                w.Cards.Add(new Card("THE CHAMPION OF THE LISTS", $"You best {f.Name}. Your fame spreads: a purse of 10 gold, and two knights ride to your banner.", w.Player, CardKind.Victory));
            }
        }
        else if (result < 0)
        {
            if (forLand && PrizeLand(w, w.Player) is var s and >= 0)
            {
                TakeLand(w, s, foe);
                w.Cards.Add(new Card("DEFEAT IN THE LISTS", $"{f.Name} unseats you, and {Realm.Shires[s].Name} passes to him.", foe, CardKind.Loss, s));
            }
            else
            {
                int fine = forLand ? Math.Min(me.Gold, 10) : Math.Min(me.Gold, 4);
                me.Gold -= fine;
                w.Cards.Add(new Card("DEFEAT IN THE LISTS", $"{f.Name} has the better of you, and you pay {fine} gold for your armour and your pride.", foe, CardKind.Loss));
            }
        }
        else w.Cards.Add(new Card("NEITHER YIELDS", $"Three passes, and neither you nor {f.Name} has the better of it. The crowd goes home.", foe));
    }

    /// <summary>A shire changes hands without a fight; an army of the old owner's there goes home.</summary>
    private static void TakeLand(World w, int shire, int lord)
    {
        int old = w.Owner[shire];
        w.Owner[shire] = lord;
        if (old >= 0 && w.Lords[old].At == shire) w.Lords[old].At = w.Lords[old].Castle;
    }

    /// <summary>The castles the player could raid: every other lord's still standing.</summary>
    public static IEnumerable<int> RaidTargets(World w) => w.Lords.Where(l => l.Alive && l.Id != w.Player).Select(l => l.Id);

    /// <summary>A raid's end: gold taken (or the lady freed), or a ransom paid.</summary>
    public static void Raided(World w, int lord, bool success, bool forLady)
    {
        w.Raids++;
        var me = w.Me;
        var l = w.Lords[lord];
        if (success && forLady)
        {
            Marry(w);
            return;
        }
        if (success)
        {
            // Half his treasury, and the silver plate from the strongroom besides.
            int half = l.Gold / 2, loot = half + RaidPlate;
            l.Gold -= half;
            me.Gold += loot;
            w.Heard.Add(Sfx.Coins);
            w.Cards.Add(new Card("THE RAID SUCCEEDS", $"You slip out of {l.Info.CastleName} by night with {loot} gold from {l.Info.Short}'s strongroom.", w.Player, CardKind.Victory, l.Castle));
        }
        else
        {
            int ransom = (me.Gold + 1) / 2;
            me.Gold -= ransom;
            w.Renown = Math.Max(0, w.Renown - 1);
            w.Cards.Add(new Card("TAKEN IN THE NIGHT", $"The guards of {l.Info.CastleName} overpower you. You are ransomed for {ransom} gold.", lord, CardKind.Loss, l.Castle));
        }
    }

    /// <summary>The lady is held in this lord's castle (a raid there is to free her).</summary>
    public static bool HoldsLady(World w, int lord) => w.Lady == LadyState.Held && w.LadyHeldBy == lord;

    /// <summary>The lady freed: she weds her rescuer, and brings her father's shire and a dowry.</summary>
    public static void Marry(World w)
    {
        w.Lady = LadyState.Married;
        w.LadyHeldBy = -1;
        w.Renown += 2;
        w.Me.Gold += 20;
        string land = "";
        if (w.Owner[Realm.LadyLand] == Realm.Nobody)
        {
            w.Owner[Realm.LadyLand] = w.Player;
            land = $" {Realm.Shires[Realm.LadyLand].Name}, her father's shire, comes with her.";
        }
        w.Heard.Add(Sfx.Wedding);
        w.Cards.Add(new Card("A WEDDING", $"{Realm.LadyName} is free. Love follows gratitude, and before the season is out you are wed, with a dowry of 20 gold.{land}",
            w.Player, CardKind.Story, Realm.LadyLand));
    }

    /// <summary>Gil and his outlaws raid a lord's castle on the player's behalf: half his gold.</summary>
    public static bool GilRaid(World w, int lord)
    {
        if (w.GilCalls <= 0 || lord == w.Player || !w.Lords[lord].Alive) return false;
        w.GilCalls--;
        var l = w.Lords[lord];
        int loot = l.Gold / GilRaidShare;
        l.Gold -= loot;
        w.Me.Gold += loot;
        w.Heard.Add(Sfx.Coins);
        w.Cards.Add(new Card("THE OUTLAWS RIDE", $"{Realm.OutlawName}'s men fall on {l.Info.CastleName}'s tax wagons and bring you {loot} gold.", -2, CardKind.Story, Realm.Sherwood));
        return true;
    }

    /// <summary>Gil's archers will ride with the army in its next battle.</summary>
    public static bool GilAid(World w)
    {
        if (w.GilCalls <= 0 || w.GilAid) return false;
        w.GilCalls--;
        w.GilAid = true;
        w.Cards.Add(new Card("THE GREENWOOD ANSWERS", $"{Realm.OutlawName} sends his archers to march with your army. They will loose their arrows in your next battle.", -2, CardKind.Story, Realm.Sherwood));
        return true;
    }

    // ───────────────────────────── the other lords' turns and the seasons ─────────────────────────────

    /// <summary>The end of the player's turn: every other lord takes his, the lady's fate, the season's taxes, and the next season.</summary>
    public static void EndTurn(World w)
    {
        if (w.Won || w.Lost) return;
        var conquests = new List<string>();
        foreach (var l in w.Lords)
        {
            if (l.Id == w.Player || !l.Alive) continue;
            LordTurn(w, l, conquests);
            if (w.Lost) return;
        }
        if (conquests.Count > 0)
            w.Cards.Add(new Card("THE SAXON LORDS RIDE", string.Join(" ", conquests), -1));
        if (w.Turn + 1 == w.LadyTurn && w.Lady == LadyState.Free && w.Lords.Where(l => l.Alive && Realm.IsNorman(l.Id)).ToList() is { Count: > 0 } normans)
        {
            var n = normans[w.Dice.Next(normans.Count)];
            w.Lady = LadyState.Held;
            w.LadyHeldBy = n.Id;
            w.Cards.Add(new Card("A LADY CARRIED OFF",
                $"{n.Info.Name} has carried off {Realm.LadyName}, daughter of the late Saxon earl of {Realm.Shires[Realm.LadyLand].Name}, and holds her in {n.Info.CastleName}. A daring raid might free her.",
                n.Id, CardKind.Story, n.Castle));
        }
        foreach (var l in w.Lords)
        {
            if (!l.Alive) continue;
            l.Gold += Income(w, l.Id);
            PayArmy(w, l);
            // Ships from Normandy bring the Normans knights every season.
            if (Realm.IsNorman(l.Id)) l.Garrison += new Troops(0, NormanReinforcements);
        }
        w.Turn++;
        CheckEnd(w);
    }

    /// <summary>
    /// A computer lord's turn: he spends his taxes on his garrison, on a catapult (the Normans) and on his army, then marches
    /// on the most tempting shire next to his army that it can beat, or towards one.
    /// </summary>
    public static void LordTurn(World w, Lord l, List<string>? news = null)
    {
        bool norman = Realm.IsNorman(l.Id);
        // The garrison first: more as the war goes on.
        int want = norman ? 40 + 2 * w.Turn : 20 + w.Turn;
        int add = Math.Min(l.Gold, Math.Max(0, want - l.Garrison.Strength));
        if (add > 0) { l.Gold -= add; l.Garrison += new Troops(add, 0); }
        if (norman && l.Army.Catapults == 0 && l.Gold >= Realm.CatapultCost + 8) { l.Gold -= Realm.CatapultCost; Join(l, new Troops(0, 0, 1)); }
        // Then the army, up to what he can keep in the field: it grows as the war goes on.
        int cap = (norman ? 40 : 30) + w.Turn * (norman ? 4 : 3);
        int spend = Math.Min(l.Gold * 3 / 4, Math.Max(0, cap - l.Army.Strength));
        int knights = spend / 3 / Realm.KnightCost;
        int soldiers = spend - knights * Realm.KnightCost;
        if (knights + soldiers > 0)
        {
            l.Gold -= knights * Realm.KnightCost + soldiers;
            Join(l, new Troops(soldiers, knights));
        }
        if (l.Army.Empty) return;
        // An enemy army at the gates: the lord's own army hurries home to hold the castle.
        if (l.At != l.Castle && w.Lords.Any(o => o.Alive && !o.Army.Empty && !Allies(o.Id, l.Id) && o.Army.Catapults > 0 && Realm.Adjacent(o.At, l.Castle))
            && Realm.Neighbours(l.At).Contains(l.Castle) || l.At != l.Castle && Realm.Adjacent(l.At, l.Castle) && w.Lords[w.Player].Army.Catapults > 0 && w.Lords[w.Player].At >= 0 && Realm.Adjacent(w.Lords[w.Player].At, l.Castle))
        {
            l.At = l.Castle;
            return;
        }
        if (!w.Dice.Chance(norman ? 0.7 : 0.55)) return;
        // The most tempting shire he can take.
        int best = -1;
        double bestValue = 0;
        foreach (int s in Marches(w, l.Id))
        {
            if (w.Owner[s] == l.Id && w.ArmyIn(s, l.Id) == null) continue;
            // Normans don't fight Normans, nor Saxons Saxons; and only the player can take a Norman castle.
            if (Allies(l.Id, w.Owner[s]) || w.ArmyIn(s, l.Id) is { } there && Allies(l.Id, there.Id)) continue;
            if (!norman && w.CastleOf(s) is { } c && Realm.IsNorman(c.Id)) continue;
            var d = Defenders(w, s, l.Id);
            if (d.Castle && (l.Army.Catapults == 0 || w.Turn < CastleGrace)) continue;
            // One march on the player a season is enough.
            if (d.Lord == w.Player && (d.Castle || d.Army) && w.Assault != null) continue;
            double defence = d.Troops.Strength * (d.Castle ? ExpectedWalls : 1) * (d.Lord >= 0 ? Realm.Lead(Realm.Lords[d.Lord].Leadership) : 0.9);
            double mine = l.Army.Strength * Realm.Lead(l.Info.Leadership);
            if (mine < defence * 1.3 + 4) continue;
            double value = Realm.Shires[s].Income + (d.Castle ? 20 : 0) + (norman && (w.Owner[s] == w.Player || d.Lord == w.Player) ? 8 : 0) + w.Dice.Next();
            if (value > bestValue) { bestValue = value; best = s; }
        }
        if (best < 0)
        {
            Advance(w, l);
            return;
        }
        var def = Defenders(w, best, l.Id);
        bool player = def.Lord == w.Player && (def.Castle || def.Army);
        if (player)
        {
            // The player fights this one out on the field.
            w.Assault = new Assault(l.Id, l.Army, best, def.Castle ? w.Dice.Range(0.2, 0.6) : 0);
            w.Cards.Add(new Card(def.Castle ? "YOUR CASTLE IS BESIEGED" : "THE NORMANS ARE UPON YOU",
                def.Castle
                    ? $"{l.Info.Name}'s catapults have battered the walls of {Realm.Lords[w.Player].CastleName}, and his army is at the gate. Defend it!"
                    : $"{l.Info.Name} marches on your army in {Realm.Shires[best].Name}. Stand and fight!",
                l.Id, CardKind.War, best));
            w.Heard.Add(Sfx.Drum);
            return;
        }
        int was = w.Owner[best];
        if (def.Troops.Empty)
        {
            Conquer(w, l.Id, best, l.Army);
        }
        else
        {
            var b = AutoBattle(w, l.Id, best, def);
            Settle(w, l.Id, best, def, b);
            if (b.Winner != 0)
            {
                if (was == w.Player || norman)
                    w.Cards.Add(new Card("AN ATTACK BEATEN OFF", $"{l.Info.Name} marched on {Realm.Shires[best].Name} but was thrown back.", l.Id, CardKind.News, best));
                return;
            }
        }
        if (w.Owner[best] != l.Id) return;
        if (was == w.Player)
        {
            w.Cards.Add(new Card($"{Realm.Shires[best].Name.ToUpperInvariant()} IS LOST", $"{l.Info.Name} has seized {Realm.Shires[best].Name} from you.", l.Id, CardKind.Loss, best));
            w.Heard.Add(Sfx.Drum);
        }
        else if (def.Castle) { }
        else if (norman)
            w.Cards.Add(new Card("THE NORMANS ADVANCE", $"The Norman lord {l.Info.Name} has seized {Realm.Shires[best].Name}" + (was >= 0 ? $" from {Realm.Lords[was].Short}." : "."), l.Id, CardKind.War, best));
        else news?.Add($"{l.Info.Short} takes {Realm.Shires[best].Name}.");
    }

    /// <summary>A lord with nothing to attack marches his army a shire nearer the nearest thing he could.</summary>
    private static void Advance(World w, Lord l)
    {
        bool norman = Realm.IsNorman(l.Id);
        var prev = new Dictionary<int, int> { [l.At] = l.At };
        var q = new Queue<int>([l.At]);
        int found = -1;
        while (q.Count > 0 && found < 0)
        {
            int s = q.Dequeue();
            foreach (int n in Realm.Neighbours(s))
            {
                if (n == Realm.Sherwood || prev.ContainsKey(n)) continue;
                prev[n] = s;
                bool target = w.Owner[n] != l.Id && !Allies(l.Id, w.Owner[n]);
                if (target) { found = n; break; }
                if (w.Owner[n] == l.Id) q.Enqueue(n);
            }
        }
        if (found < 0) return;
        int step = found;
        while (prev[step] != l.At) step = prev[step];
        if (w.Owner[step] == l.Id && w.ArmyIn(step, l.Id) == null) l.At = step;
    }

    /// <summary>Two lords on the same side (a lord and himself too).</summary>
    public static bool Allies(int a, int b) => a >= 0 && b >= 0 && Realm.Lords[a].Side == Realm.Lords[b].Side;

    /// <summary>What an army in the field costs a season: its first 20 strength live off the land; then a gold piece for every 15 more.</summary>
    public static int Upkeep(Troops army) => Math.Max(0, army.Strength - FreeStrength) / 15;

    public const int FreeStrength = 20;

    /// <summary>The army is paid; what can't be paid for deserts (five soldiers for each gold piece short).</summary>
    public static void PayArmy(World w, Lord l)
    {
        int due = Upkeep(l.Army);
        if (due <= l.Gold) { l.Gold -= due; return; }
        int shortBy = due - l.Gold;
        l.Gold = 0;
        int gone = Math.Min(l.Army.Soldiers, shortBy * 5);
        l.Army -= new Troops(gone, 0);
        if (gone > 0 && l.Id == w.Player)
            w.Cards.Add(new Card("UNPAID MEN DESERT", $"Your treasury is empty, and {gone} unpaid soldiers slip away from the army in the night.", w.Player, CardKind.Loss));
    }

    // ───────────────────────────── the end and the score ─────────────────────────────

    public static void CheckEnd(World w)
    {
        if (w.Lost || w.Won) return;
        if (!w.Me.Alive) { w.Lost = true; return; }
        if (w.Lords.Where(l => Realm.IsNorman(l.Id)).All(l => !l.Alive))
        {
            w.Won = true;
            w.Cards.Add(new Card("LONG LIVE THE KING",
                $"The last Norman castle has fallen. The Saxon lords kneel, and in the abbey at Westminster {Realm.Lords[w.Player].Name} is crowned King of England.",
                w.Player, CardKind.Victory));
        }
    }

    /// <summary>The score: land, gold, renown, the Normans beaten, a wedding, and, for a crown, the sooner the better.</summary>
    public static int Score(World w)
    {
        int s = 100 * w.LandCount(w.Player) + w.Me.Gold + 1000 * w.NormansBeaten + 150 * w.Renown + (w.Lady == LadyState.Married ? 500 : 0);
        if (w.Won) s += 5000 + 100 * Math.Max(0, 60 - w.Turn);
        return s;
    }
}
