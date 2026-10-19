namespace AVADefenderOfTheCrown.Engine;

/// <summary>
/// A season's plan: the order to give (and its shire or lord), what to buy for the army and for the garrison, the men
/// to send from the army to the walls, and whether to call on the outlaws' archers first.
/// </summary>
public sealed record Plan(Order Order, int Target = -1, bool ForLand = false, Troops Buy = default, Troops Garrison = default,
    Troops Muster = default, bool AskGilAid = false);

/// <summary>
/// The computer player, for the demonstration and the tests. On the map it keeps the castle garrisoned, puts the rest
/// of its gold into its army (and a catapult once there's a castle worth taking), and marches on the richest shire
/// next to the army that it can beat, calling on the outlaws' archers before storming a castle; with nothing to
/// attack it jousts, raids or moves up. In the scenes it plays like a fair hand: its lance point wanders a little
/// off the shield, its catapult a little off the mark, and it parries late or strikes into a guard now and then.
/// </summary>
public sealed class Pilot
{
    private double _wait;
    private Plan? _plan;
    private int _step;
    private bool _held;
    private Dice _dice = new(77);
    private (double X, double Y) _err;
    private int _errPass = -1;
    private bool _winding;
    private double _late;

    public void Reset()
    {
        _wait = 0;
        _plan = null;
        _step = 0;
        _held = false;
        _errPass = -1;
    }

    /// <summary>The garrison it keeps: enough to hold the walls against the Norman armies near the castle.</summary>
    public static int GarrisonWanted(World w)
    {
        int castle = w.Me.Castle;
        double threat = w.Lords.Where(l => l.Alive && Realm.IsNorman(l.Id) && l.At >= 0 && Distance(l.At, castle) <= 3)
            .Select(l => l.Army.Strength * Realm.Lead(l.Info.Leadership)).DefaultIfEmpty(0).Max();
        return (int)Math.Max(18 + w.Turn, threat * 0.62 / Realm.Lead(Realm.Lords[w.Player].Leadership));
    }

    /// <summary>The season's plan for the player's side.</summary>
    public static Plan Decide(World w, Dice? dice = null)
    {
        var d = dice ?? new Dice(w.Turn * 31 + 7);
        var me = w.Me;
        var info = Realm.Lords[w.Player];
        int gold = me.Gold;
        // The garrison, if it's short and the army's at home to give it men (or there's gold to buy them).
        Troops muster = default, buyG = default, buyA = default;
        int short_ = GarrisonWanted(w) - me.Garrison.Strength;
        bool home = me.At == me.Castle || me.Army.Empty;
        if (short_ > 0 && home && me.Army.Soldiers > 20)
        {
            int s = Math.Min(short_, me.Army.Soldiers - 20);
            muster = new Troops(s, 0);
            short_ -= s;
        }
        if (short_ > 0)
        {
            int s = Math.Min(short_, gold);
            buyG = new Troops(s, 0);
            gold -= s;
        }
        // A catapult once the army is big enough to storm a castle.
        bool wantCatapult = me.Army.Catapults == 0 && me.Army.Strength + gold >= 40 && gold >= Realm.CatapultCost + 6;
        int cats = wantCatapult ? 1 : 0;
        gold -= cats * Realm.CatapultCost;
        int knights = gold / 4 / Realm.KnightCost;
        int soldiers = Math.Max(0, gold - knights * Realm.KnightCost);
        buyA = new Troops(soldiers, knights, cats);
        var army = me.Army + buyA;
        double lead = Realm.Lead(info.Leadership);

        // The richest shire it can take.
        int best = -1;
        double bestValue = 0;
        bool bestCastle = false;
        foreach (int s in Sim.Marches(w, w.Player))
        {
            var def = Sim.Defenders(w, s, w.Player);
            if (w.Owner[s] == w.Player && !def.Army) continue;
            if (def.Castle && army.Catapults == 0) continue;
            double theirs = def.Troops.Strength * (def.Lord >= 0 ? Realm.Lead(Realm.Lords[def.Lord].Leadership) : 0.9) * (def.Castle ? 1.45 : 1);
            double mine = army.Strength * lead * (w.GilAid || w.GilCalls > 0 && def.Castle ? 1.15 : 1);
            if (mine < theirs * 1.35 + 3) continue;
            double value = Realm.Shires[s].Income + (def.Castle && Realm.IsNorman(def.Lord) ? 30 : 0) - def.Troops.Strength * 0.05;
            if (value > bestValue) { bestValue = value; best = s; bestCastle = def.Castle; }
        }
        var plan = new Plan(Order.Pass, Buy: buyA, Garrison: buyG, Muster: muster);
        if (best >= 0) return plan with { Order = Order.Conquer, Target = best, AskGilAid = bestCastle && w.GilCalls > 0 && !w.GilAid };

        // Nothing to take: rescue the lady, joust, raid, or move up towards the Normans.
        var normans = w.Lords.Where(l => l.Alive && Realm.IsNorman(l.Id)).ToList();
        if (w.Lady == LadyState.Held && info.Swordplay >= Rating.Average && d.Chance(0.6))
            return plan with { Order = Order.Raid, Target = w.LadyHeldBy };
        if (me.Gold < 10 && w.GilCalls > 1 && normans.Count > 0 && d.Chance(0.5))
            return plan with { Order = Order.Gil, Target = normans.MaxBy(n => n.Gold)!.Id };
        if (Sim.Marches(w, w.Player).Where(s => w.Owner[s] == w.Player && w.ArmyIn(s, w.Player) == null).ToList() is { Count: > 0 } own && !me.Army.Empty && d.Chance(0.5))
        {
            // Towards the weakest Norman castle.
            var target = normans.MinBy(n => n.Garrison.Strength);
            int step = target == null ? own[0] : own.MinBy(s => Distance(s, target.Castle));
            if (target == null || Distance(step, target.Castle) < Distance(me.At, target.Castle))
                return plan with { Order = Order.Conquer, Target = step };
        }
        if (Sim.TournamentFoes(w, true).Any() && info.Jousting >= Rating.Average && d.Chance(0.55))
            return plan with { Order = Order.Tournament, ForLand = true };
        if (normans.Count > 0 && normans.Max(n => n.Gold) > 25 && d.Chance(0.5))
            return plan with { Order = Order.Raid, Target = normans.MaxBy(n => n.Gold)!.Id };
        return d.Chance(0.4) ? plan with { Order = Order.Tournament } : plan;
    }

    /// <summary>Marches between two shires.</summary>
    public static int Distance(int a, int b)
    {
        var seen = new Dictionary<int, int> { [a] = 0 };
        var q = new Queue<int>([a]);
        while (q.Count > 0)
        {
            int s = q.Dequeue();
            if (s == b) return seen[s];
            foreach (int n in Realm.Neighbours(s))
                if (seen.TryAdd(n, seen[s] + 1)) q.Enqueue(n);
        }
        return 99;
    }

    // ───────────────────────────── at the controls ─────────────────────────────

    /// <summary>The demonstration's controls for a step: menus worked through the engine, and the scenes played.</summary>
    public Controls Drive(GameEngine g, double dt)
    {
        var w = g.World;
        switch (g.Scene)
        {
            case Scene.Card:
                return default;
            case Scene.Map:
                _wait += dt;
                if (_wait < 0.9) return default;
                _wait = 0;
                Command(g);
                return default;
            case Scene.Joust:
                return Lance(g.Joust!);
            case Scene.Siege:
                return Catapult(g.Siege!);
            case Scene.Raid:
                return Sword(g.Raid!);
            case Scene.Battle:
                if (g.Battle is { Phase: BattlePhase.Choose } b) g.Command(Tactic(b, w));
                return default;
        }
        return default;
    }

    /// <summary>Works the season's plan through the engine, a step at a time so it can be watched.</summary>
    private void Command(GameEngine g)
    {
        var w = g.World;
        _plan ??= Decide(w, _dice);
        var p = _plan;
        switch (_step++)
        {
            case 0 when !p.Garrison.Empty:
                g.DoOrder(Order.Buy);
                g.BuyToGarrison = true;
                g.BuySoldiers = p.Garrison.Soldiers;
                g.ConfirmBuy();
                return;
            case 1 when !p.Buy.Empty || p.Buy.Catapults > 0:
                g.DoOrder(Order.Buy);
                g.BuyToGarrison = false;
                (g.BuySoldiers, g.BuyKnights, g.BuyCatapults) = (p.Buy.Soldiers, p.Buy.Knights, p.Buy.Catapults);
                if (g.BuyCost > w.Me.Gold) (g.BuySoldiers, g.BuyKnights) = (Math.Max(0, w.Me.Gold - g.BuyCatapults * Realm.CatapultCost), 0);
                g.ConfirmBuy();
                return;
            case 2 when !p.Muster.Empty && g.CanOrder(Order.Garrison):
                g.DoOrder(Order.Garrison);
                g.MoveToGarrison = true;
                g.MoveSoldiers = Math.Min(p.Muster.Soldiers, w.Me.Army.Soldiers);
                g.ConfirmMove();
                return;
            case 3 when p.AskGilAid && w.GilCalls > 0 && !w.GilAid:
                g.DoOrder(Order.Gil);
                g.CallGil(false);
                return;
            case 0 or 1 or 2 or 3:
                Command(g);
                return;
        }
        _plan = null;
        _step = 0;
        switch (p.Order)
        {
            case Order.Conquer:
                g.DoOrder(Order.Conquer);
                if (g.Mode == MapMode.Conquer) g.ClickShire(p.Target);
                if (g.Scene == Scene.Map && g.Mode == MapMode.Conquer) { g.Cancel(); g.DoOrder(Order.Pass); }
                break;
            case Order.Raid:
                g.DoOrder(Order.Raid);
                g.ClickShire(w.Lords[p.Target].Castle);
                break;
            case Order.Gil:
                g.DoOrder(Order.Gil);
                if (g.Mode == MapMode.Gil) { g.CallGil(true); g.ClickShire(w.Lords[p.Target].Castle); }
                else g.DoOrder(Order.Pass);
                break;
            case Order.Tournament:
                g.DoOrder(Order.Tournament);
                g.HoldTournament(p.ForLand);
                if (g.Scene == Scene.Map) { g.Cancel(); g.DoOrder(Order.Pass); }
                break;
            default:
                g.DoOrder(Order.Pass);
                break;
        }
    }

    /// <summary>The lance steered at where the shield will be when the horses meet, give or take.</summary>
    public Controls Lance(Joust j)
    {
        if (_errPass != j.Pass * 10 + (int)j.Phase / 2)
        {
            _errPass = j.Pass * 10 + (int)j.Phase / 2;
            double r = _dice.Range(0, 34), a = _dice.Range(0, Math.Tau);
            _err = (r * Math.Cos(a), r * Math.Sin(a));
        }
        var (sx, sy) = j.ShieldAt(Joust.ChargeSeconds);
        // Allowing for where the bob will carry the point.
        double bobX = j.TipAt(Joust.ChargeSeconds).X - j.AimX, bobY = j.TipAt(Joust.ChargeSeconds).Y - j.AimY;
        return new Controls(Point: (sx - bobX + _err.X, sy - bobY + _err.Y));
    }

    /// <summary>The catapult: boulders low on the wall until it's breached, the fire and the sickness over it at the end.</summary>
    public Controls Catapult(Siege s)
    {
        if (s.Phase != SiegePhase.Aim) { _held = false; return default; }
        var ammo = s.Shots <= 2 && s.Breach < 80 && s.AmmoLeft(Ammo.GreekFire) > 0 ? Ammo.GreekFire
            : s.Shots <= 1 && s.AmmoLeft(Ammo.Disease) > 0 ? Ammo.Disease : Ammo.Boulder;
        if (s.Ammo != ammo) { s.Select(ammo); return default; }
        double angle = ammo == Ammo.Boulder ? 38 : 70;
        double power = ammo == Ammo.Boulder ? PowerFor(angle, 0.3) : PowerOver(angle);
        power *= 1 + _dice.Range(-0.06, 0.06);
        double a = angle * Math.PI / 180, r = power / 1.25;
        var point = (Siege.LaunchX + r * Math.Cos(a), Siege.LaunchY - r * Math.Sin(a));
        bool aimed = Math.Abs(s.Angle - angle) < 0.5 && Math.Abs(s.Power - power) < 8;
        bool fire = aimed && !_held;
        _held = fire;
        return new Controls(Point: point, Fire: fire);
    }

    /// <summary>The power that puts a shot at this angle at height <paramref name="h"/> up the wall's face.</summary>
    public static double PowerFor(double angle, double h)
    {
        double lo = Siege.MinPower, hi = Siege.MaxPower;
        for (int i = 0; i < 40; i++)
        {
            double mid = (lo + hi) / 2;
            double? at = Siege.HeightAtWall(angle, mid);
            if (at == null || at < h) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }

    /// <summary>The power that drops a shot at this angle into the middle of the bailey.</summary>
    public static double PowerOver(double angle)
    {
        double lo = Siege.MinPower, hi = Siege.MaxPower, want = (Siege.WallX + Siege.WallThick + Siege.KeepX) / 2;
        for (int i = 0; i < 40; i++)
        {
            double mid = (lo + hi) / 2, a = angle * Math.PI / 180, vy = mid * Math.Sin(a);
            double t = (vy + Math.Sqrt(vy * vy + 2 * Siege.Gravity * (Siege.GroundY - Siege.LaunchY))) / Siege.Gravity;
            if (Siege.Arc(angle, mid, t).X < want) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }

    /// <summary>The sword: parry the falling blow (a little late now and then), strike where the guard isn't covering.</summary>
    public Controls Sword(Raid r)
    {
        var foe = r.Foe;
        var h = r.Hero;
        if (_held || foe == null || r.Phase != RaidPhase.Fight || h.Act != Act.Guard) { _held = false; return default; }
        if (foe.Act == Act.Wind)
        {
            // How late it'll be to this blow is settled once, as the guard winds up.
            if (!_winding) { _winding = true; _late = _dice.Chance(0.22) ? 0.35 : 0; }
            if (foe.T >= Raid.WindSeconds - r.ParryWindow * 0.6 + _late) { _held = true; return new Controls(Alt: true); }
            return default;
        }
        _winding = false;
        if (Math.Abs(foe.X - h.X) > Raid.Reach + 10 || foe.Act is Act.Walk or Act.Down) return default;
        if (_dice.Next() > 0.06) return default;
        var line = foe.Act == Act.Stagger ? Line.Middle : (Line)(((int)foe.Guard + 1 + _dice.Next(2)) % 3);
        if (_dice.Chance(0.12)) line = foe.Guard;
        _held = true;
        return line switch { Line.High => new Controls(Dy: -1), Line.Low => new Controls(Dy: 1), _ => new Controls(Dx: 1) };
    }

    /// <summary>The battle: fierce when it's stronger, the catapults now and then, steady otherwise, and away when it's beaten.</summary>
    public Tactic Tactic(Battle b, World w)
    {
        int me = b.Mine, them = 1 - me;
        double mine = b.Strength(me) * b.Lead[me] * (me == 1 ? b.Walls : 1);
        double theirs = b.Strength(them) * b.Lead[them] * (them == 1 ? b.Walls : 1);
        if (me == 0 && mine < theirs * 0.4 && b.Round >= 3) return Engine.Tactic.Retreat;
        if (b.CanBombard(me) && _dice.Chance(0.3)) return Engine.Tactic.Bombard;
        return mine > theirs * 1.1 ? Engine.Tactic.FierceAttack : _dice.Chance(0.5) ? Engine.Tactic.StandFast : Engine.Tactic.FierceAttack;
    }
}
