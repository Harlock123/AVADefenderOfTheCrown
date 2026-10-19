using Avalonia.Input;
using AVADefenderOfTheCrown.Engine;

namespace AVADefenderOfTheCrown.Tests;

public class GameplayTests
{
    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "avadefenderofthecrown-tests-" + Guid.NewGuid().ToString("N"));

    private static GameEngine Engine(int seed = 5)
    {
        string folder = TempFolder();
        return new GameEngine(HighScoreTable.Load(folder), folder, seed) { Silent = true };
    }

    /// <summary>England at the king's death, with the player as one of the Saxon knights and the opening card read.</summary>
    private static World Realm1149(int player = Realm.Edmund, int seed = 1)
    {
        var w = new World();
        Sim.Setup(w, player, seed);
        w.Cards.Clear();
        return w;
    }

    /// <summary>Steps a battle's clash (and the end of it) through.</summary>
    private static void Finish(Battle b)
    {
        for (int i = 0; i < 60 * 30 && !b.Finished; i++) b.Step(Field.Step);
    }

    // ───────────────────────────── the realm ─────────────────────────────

    [Fact]
    public void England_has_eighteen_shires_whose_borders_run_both_ways_and_join_them_all_up()
    {
        Assert.Equal(18, Realm.Count);
        for (int a = 0; a < Realm.Count; a++)
        {
            Assert.True(Realm.Shires[a].Income > 0);
            Assert.DoesNotContain(a, Realm.Neighbours(a));
            foreach (int b in Realm.Neighbours(a)) Assert.Contains(a, Realm.Neighbours(b));
        }
        var seen = new HashSet<int> { 0 };
        var q = new Queue<int>([0]);
        while (q.Count > 0)
            foreach (int n in Realm.Neighbours(q.Dequeue()))
                if (seen.Add(n)) q.Enqueue(n);
        Assert.Equal(Realm.Count, seen.Count);
        Assert.Equal(Realm.Count, Realm.Shires.Select(s => s.Name).Distinct().Count());
    }

    [Fact]
    public void Seven_lords_four_Saxon_and_three_Norman_each_with_a_castle_of_his_own_and_his_own_arms()
    {
        Assert.Equal(7, Realm.LordCount);
        Assert.Equal(4, Realm.Lords.Count(l => l.Side == Side.Saxon));
        Assert.Equal(3, Realm.Lords.Count(l => l.Side == Side.Norman));
        Assert.Equal(7, Realm.Lords.Select(l => l.Home).Distinct().Count());
        Assert.Equal(7, Realm.Lords.Select(l => (l.Field, l.Charge)).Distinct().Count());
        Assert.DoesNotContain(Realm.Lords, l => l.Home == Realm.Sherwood);
        // The four knights differ: each is strongest at something different, or average at all three.
        Assert.Equal(4, Realm.Lords.Take(4).Select(l => (l.Leadership, l.Jousting, l.Swordplay)).Distinct().Count());
        Assert.True(Realm.Lead(Rating.Strong) > Realm.Lead(Rating.Average) && Realm.Lead(Rating.Average) > Realm.Lead(Rating.Weak));
    }

    [Fact]
    public void At_the_start_each_lord_holds_his_castle_the_Normans_a_shire_more_Sherwood_is_the_outlaws_and_the_rest_nobodys()
    {
        var w = Realm1149(Realm.Osric);
        Assert.Equal(Realm.Osric, w.Player);
        Assert.All(w.Lords, l => Assert.Equal(l.Id, w.Owner[l.Castle]));
        Assert.Equal(Realm.Outlaws, w.Owner[Realm.Sherwood]);
        Assert.All(w.Lords.Where(l => Realm.IsNorman(l.Id)), l => Assert.Equal(2, w.LandCount(l.Id)));
        Assert.All(w.Lords.Where(l => Realm.IsSaxon(l.Id)), l => Assert.Equal(1, w.LandCount(l.Id)));
        Assert.Equal(7, w.Owner.Count(o => o == Realm.Nobody));
        Assert.Equal(Realm.Shires[Realm.Kent].Income + Realm.Shires[Realm.Sussex].Income, Sim.Income(w, Realm.Hugh));
        Assert.Equal("SPRING 1149", w.Date);
        // The opening card tells the story.
        var fresh = new World();
        Sim.Setup(fresh, Realm.Osric, 1);
        Assert.Equal("THE KING IS DEAD", fresh.Cards.Single().Title);
    }

    // ───────────────────────────── money, men and the seasons ─────────────────────────────

    [Fact]
    public void Each_season_brings_its_taxes_and_the_seasons_turn_into_years()
    {
        var w = Realm1149();
        int gold = w.Me.Gold;
        Sim.EndTurn(w);
        Assert.Equal(gold + Sim.Income(w, w.Player) - Sim.Upkeep(w.Me.Army), w.Me.Gold);
        Assert.Equal(2, w.Turn);
        Assert.Equal("SUMMER 1149", w.Date);
        for (int i = 0; i < 3; i++) Sim.EndTurn(w);
        Assert.Equal("SPRING 1150", w.Date);
    }

    [Fact]
    public void An_army_in_the_field_is_paid_and_unpaid_men_desert()
    {
        Assert.Equal(0, Sim.Upkeep(new Troops(20, 0)));
        Assert.Equal(2, Sim.Upkeep(new Troops(50, 0)));
        Assert.Equal(2, Sim.Upkeep(new Troops(20, 10)));
        var w = Realm1149();
        w.Me.Army = new Troops(80, 0);
        w.Me.Gold = 10;
        Sim.PayArmy(w, w.Me);
        Assert.Equal(6, w.Me.Gold);
        w.Me.Gold = 1;
        Sim.PayArmy(w, w.Me);
        Assert.Equal(0, w.Me.Gold);
        Assert.Equal(80 - 15, w.Me.Army.Soldiers);
        Assert.Equal("UNPAID MEN DESERT", w.Cards.Last().Title);
    }

    [Fact]
    public void Soldiers_knights_and_catapults_cost_their_price_and_join_the_army_or_the_garrison()
    {
        var w = Realm1149();
        var me = w.Me;
        me.Gold = 40;
        var army = me.Army;
        Assert.True(Sim.Buy(w, w.Player, 5, 2, 0, toGarrison: false));
        Assert.Equal(40 - 5 - 8, me.Gold);
        Assert.Equal(army + new Troops(5, 2), me.Army);
        var garrison = me.Garrison;
        Assert.True(Sim.Buy(w, w.Player, 3, 0, 1, toGarrison: true));
        Assert.Equal(garrison + new Troops(3, 0), me.Garrison);
        Assert.Equal(1, me.Army.Catapults);
        Assert.Equal(27 - 3 - 12, me.Gold);
        Assert.False(Sim.Buy(w, w.Player, 13, 0, 0, toGarrison: false));
        Assert.False(Sim.Buy(w, w.Player, 0, 0, 0, toGarrison: false));
        Assert.Equal(12, me.Gold);
        // With no army left, the men bought muster at the castle.
        me.Army = default;
        me.At = Realm.Oxford;
        Assert.True(Sim.Buy(w, w.Player, 4, 0, 0, toGarrison: false));
        Assert.Equal(me.Castle, me.At);
    }

    [Fact]
    public void Men_pass_between_the_army_and_the_garrison_only_with_the_army_at_home()
    {
        var w = Realm1149();
        var me = w.Me;
        var (army, garrison) = (me.Army, me.Garrison);
        Assert.True(Sim.Transfer(w, 5, 1, toGarrison: true));
        Assert.Equal(army - new Troops(5, 1), me.Army);
        Assert.Equal(garrison + new Troops(5, 1), me.Garrison);
        Assert.False(Sim.Transfer(w, 500, 0, toGarrison: false));
        Assert.True(Sim.Transfer(w, 2, 0, toGarrison: false));
        me.At = Realm.Oxford;
        Assert.False(Sim.Transfer(w, 1, 0, toGarrison: true));
    }

    // ───────────────────────────── marching and conquering ─────────────────────────────

    [Fact]
    public void Marching_comes_to_a_walk_a_battle_or_a_siege_by_who_holds_the_shire()
    {
        var w = Realm1149(Realm.Edmund);
        var me = w.Me;
        Assert.Equal(Realm.Wessex, me.At);
        Assert.Equal([Realm.Gloucester, Realm.Oxford, Realm.Sussex, Realm.Cornwall], Sim.Marches(w, w.Player).Order());
        Assert.Equal(Venture.Battle, Sim.Plan(w, w.Player, Realm.Oxford));
        Assert.Equal(Venture.Battle, Sim.Plan(w, w.Player, Realm.Sussex));
        Assert.Equal(Venture.NeedCatapult, Sim.Plan(w, w.Player, Realm.Gloucester));
        Assert.Equal(Venture.None, Sim.Plan(w, w.Player, Realm.Kent));
        me.Army += new Troops(0, 0, 1);
        Assert.Equal(Venture.Siege, Sim.Plan(w, w.Player, Realm.Gloucester));
        w.Owner[Realm.Oxford] = w.Player;
        Assert.Equal(Venture.March, Sim.Plan(w, w.Player, Realm.Oxford));
        // An unclaimed shire's vassals defend it; a lord's shire with no army in it raises half as many.
        var levy = Sim.Defenders(w, Realm.Cornwall, w.Player);
        Assert.Equal(Sim.Levy(Realm.Cornwall), levy.Troops);
        Assert.Equal(Realm.Nobody, levy.Lord);
        var sussex = Sim.Defenders(w, Realm.Sussex, w.Player);
        Assert.Equal(Realm.Hugh, sussex.Lord);
        Assert.True(sussex.Troops.Strength < Sim.Levy(Realm.Sussex).Strength);
        var castle = Sim.Defenders(w, Realm.Gloucester, w.Player);
        Assert.True(castle.Castle);
        Assert.Equal(Sim.CastleWalls, castle.Walls);
        // Sherwood is the outlaws': no army marches into it.
        me.At = Realm.Derby;
        Assert.DoesNotContain(Realm.Sherwood, Sim.Marches(w, w.Player));
    }

    [Fact]
    public void A_shire_won_is_the_conquerors_and_his_army_moves_into_it()
    {
        var w = Realm1149();
        Sim.Conquer(w, w.Player, Realm.Oxford, new Troops(10, 2));
        Assert.Equal(w.Player, w.Owner[Realm.Oxford]);
        Assert.Equal(Realm.Oxford, w.Me.At);
        Assert.Equal(new Troops(10, 2), w.Me.Army);
    }

    [Fact]
    public void A_fallen_castle_finishes_its_lord_and_his_lands_and_gold_go_to_whoever_took_it()
    {
        var w = Realm1149();
        int gold = w.Me.Gold, theirs = w.Lords[Realm.Guy].Gold;
        Sim.Conquer(w, w.Player, Realm.Gloucester, new Troops(20, 2, 1));
        var guy = w.Lords[Realm.Guy];
        Assert.False(guy.Alive);
        Assert.Equal(w.Player, w.Owner[Realm.Gloucester]);
        Assert.Equal(w.Player, w.Owner[Realm.Hereford]);
        Assert.Equal(gold + theirs, w.Me.Gold);
        Assert.Equal(1, w.NormansBeaten);
        Assert.Equal("BRECY CASTLE HAS FALLEN", w.Cards.Last().Title);
        Assert.False(w.IsCastle(Realm.Gloucester));
        Assert.False(w.Won);
    }

    [Fact]
    public void Lose_your_castle_and_the_game_is_lost()
    {
        var w = Realm1149();
        Sim.Conquer(w, Realm.Hugh, Realm.Wessex, new Troops(40, 5));
        Assert.True(w.Lost);
        Assert.Equal(0, w.LandCount(w.Player));
        Assert.Equal(CardKind.Defeat, w.Cards.Last().Kind);
    }

    [Fact]
    public void Take_all_three_Norman_castles_and_you_are_crowned()
    {
        var w = Realm1149();
        foreach (int castle in new[] { Realm.Kent, Realm.Lincoln, Realm.Gloucester })
        {
            Assert.False(w.Won);
            Sim.Conquer(w, w.Player, castle, new Troops(30, 5, 1));
        }
        Assert.True(w.Won);
        Assert.Equal(3, w.NormansBeaten);
        Assert.Equal("LONG LIVE THE KING", w.Cards.Last().Title);
    }

    [Fact]
    public void The_score_counts_land_gold_renown_Normans_beaten_a_wedding_and_a_quick_crown()
    {
        var w = Realm1149();
        w.Me.Gold = 37;
        Assert.Equal(100 + 37, Sim.Score(w));
        w.Owner[Realm.Oxford] = w.Player;
        w.Renown = 2;
        w.NormansBeaten = 1;
        w.Lady = LadyState.Married;
        Assert.Equal(200 + 37 + 300 + 1000 + 500, Sim.Score(w));
        w.Won = true;
        w.Turn = 20;
        Assert.Equal(200 + 37 + 300 + 1000 + 500 + 5000 + 100 * 40, Sim.Score(w));
    }

    // ───────────────────────────── battles ─────────────────────────────

    [Fact]
    public void Each_tactic_beats_one_other_fierce_beats_bombard_bombard_beats_stand_fast_stand_fast_beats_fierce()
    {
        Assert.True(Battle.Meet(Tactic.FierceAttack, Tactic.Bombard) > Battle.Meet(Tactic.Bombard, Tactic.FierceAttack));
        Assert.True(Battle.Meet(Tactic.Bombard, Tactic.StandFast) > Battle.Meet(Tactic.StandFast, Tactic.Bombard));
        Assert.True(Battle.Meet(Tactic.StandFast, Tactic.FierceAttack) > Battle.Meet(Tactic.FierceAttack, Tactic.StandFast));
        Assert.Equal(0, Battle.Meet(Tactic.Retreat, Tactic.FierceAttack));
    }

    [Fact]
    public void A_much_bigger_army_wins_and_the_losses_come_off_soldiers_first()
    {
        for (int seed = 1; seed <= 10; seed++)
        {
            var b = new Battle(new Troops(60, 10), Realm.Hugh, new Troops(12, 1), Realm.Nobody, 1, -1, seed);
            Assert.Equal(0, b.Resolve());
            Assert.True(b.Left(1).Empty || b.Report.Contains("FLEE"));
        }
        var c = new Battle(new Troops(30, 10), Realm.Leofric, new Troops(30, 10), Realm.Leofric, 1, -1, 3);
        c.Fight(Tactic.StandFast, Tactic.StandFast);
        Assert.True(30 - c.Soldiers[0] > 10 - c.Knights[0]);
    }

    [Fact]
    public void Castle_walls_turn_a_winning_army_into_a_beaten_one()
    {
        for (int seed = 1; seed <= 10; seed++)
        {
            var open = new Battle(new Troops(40, 0), Realm.Leofric, new Troops(24, 0), Realm.Leofric, 1, -1, seed);
            Assert.Equal(0, open.Resolve());
            var walled = new Battle(new Troops(40, 0), Realm.Leofric, new Troops(24, 0), Realm.Leofric, Sim.CastleWalls, -1, seed);
            Assert.Equal(1, walled.Resolve());
        }
    }

    [Fact]
    public void A_better_leader_wins_an_even_fight()
    {
        for (int seed = 1; seed <= 10; seed++)
        {
            var b = new Battle(new Troops(30, 4), Realm.Edmund, new Troops(30, 4), Realm.Osric, 1, -1, seed);
            for (int r = 0; r < Battle.MaxRounds && b.Winner < 0; r++) b.Fight(Tactic.FierceAttack, Tactic.FierceAttack);
            Assert.Equal(0, b.Winner);
        }
    }

    [Fact]
    public void Retreat_ends_the_battle_with_a_parting_blow_and_after_eight_rounds_the_attackers_give_up()
    {
        var b = new Battle(new Troops(30, 0), Realm.Leofric, new Troops(30, 0), Realm.Leofric, 1, 0, 1);
        b.Fight(Tactic.Retreat, Tactic.FierceAttack);
        Assert.Equal(1, b.Winner);
        Assert.True(b.Fell[0] > 0);
        Assert.Equal(0, b.Fell[1]);
        var long_ = new Battle(new Troops(1000, 0), Realm.Leofric, new Troops(1000, 0), Realm.Leofric, 1, 0, 1);
        for (int r = 0; r < Battle.MaxRounds; r++)
        {
            Assert.Equal(-1, long_.Winner);
            long_.Fight(Tactic.StandFast, Tactic.StandFast);
        }
        Assert.Equal(1, long_.Winner);
        Assert.Equal("THE ATTACKERS GIVE UP THE FIELD", long_.Report);
    }

    [Fact]
    public void Only_an_army_with_catapults_can_bombard_and_the_outlaws_archers_add_their_arrows()
    {
        var b = new Battle(new Troops(20, 2), Realm.Edmund, new Troops(20, 2), Realm.Nobody, 1, 0, 1);
        Assert.False(b.Order(Tactic.Bombard));
        Assert.Equal(BattlePhase.Choose, b.Phase);
        var c = new Battle(new Troops(20, 2, 1), Realm.Edmund, new Troops(20, 2), Realm.Nobody, 1, 0, 1);
        Assert.True(c.Order(Tactic.Bombard));
        Assert.Equal(Tactic.Bombard, c.Last[0]);
        Assert.Equal(BattlePhase.Clash, c.Phase);
        var without = new Battle(new Troops(20, 2), Realm.Edmund, new Troops(40, 2), Realm.Nobody, 1, 0, 7);
        var with = new Battle(new Troops(20, 2), Realm.Edmund, new Troops(40, 2), Realm.Nobody, 1, 0, 7) { Archers = true };
        without.Fight(Tactic.StandFast, Tactic.StandFast);
        with.Fight(Tactic.StandFast, Tactic.StandFast);
        Assert.True(with.Fell[1] > without.Fell[1]);
    }

    [Fact]
    public void An_order_shows_the_clash_then_asks_again_or_ends_with_the_winner()
    {
        var b = new Battle(new Troops(40, 5), Realm.Edmund, new Troops(6, 0), Realm.Nobody, 1, 0, 2);
        Assert.True(b.Order(Tactic.FierceAttack));
        Assert.False(b.Order(Tactic.FierceAttack));
        for (int i = 0; i < 60 * 30 && b.Phase != BattlePhase.Done; i++)
        {
            b.Step(Field.Step);
            if (b.Phase == BattlePhase.Choose) b.Order(Tactic.FierceAttack);
        }
        Finish(b);
        Assert.True(b.Finished);
        Assert.Equal(0, b.Winner);
        Assert.Contains(Sfx.Fanfare, b.Heard);
        var copy = b.Copy();
        copy.Soldiers[0] = 0;
        Assert.NotEqual(0, b.Soldiers[0]);
        Assert.Equal(new Troops(3, 1), new Battle(new Troops(3, 1), 0, default, -1, 1, 0, 1) { Soldiers = [2.6, 0], Knights = [0.8, 0] }.Left(0));
    }

    [Fact]
    public void The_result_of_a_battle_puts_the_winners_in_the_shire_and_sends_the_losers_home()
    {
        var w = Realm1149();
        var hugh = w.Lords[Realm.Hugh];
        hugh.At = Realm.Sussex;
        hugh.Army = new Troops(10, 0);
        var d = Sim.Defenders(w, Realm.Sussex, w.Player);
        Assert.True(d.Army);
        var b = new Battle(new Troops(80, 10), w.Player, d.Troops, d.Lord, 1, -1, 4);
        b.Resolve();
        Sim.Settle(w, w.Player, Realm.Sussex, d, b);
        Assert.Equal(w.Player, w.Owner[Realm.Sussex]);
        Assert.Equal(Realm.Kent, hugh.At);
        Assert.Equal(b.Left(0), w.Me.Army);
        // Thrown back from a castle, the attackers stay where they were with what's left.
        var w2 = Realm1149();
        var d2 = Sim.Defenders(w2, Realm.Gloucester, w2.Player);
        var b2 = new Battle(new Troops(10, 0, 1), w2.Player, d2.Troops, d2.Lord, Sim.CastleWalls, -1, 4);
        b2.Resolve();
        Sim.Settle(w2, w2.Player, Realm.Gloucester, d2, b2);
        Assert.Equal(Realm.Guy, w2.Owner[Realm.Gloucester]);
        var guy = w2.Lords[Realm.Guy];
        Assert.Equal(b2.Left(1), guy.Garrison + new Troops(guy.Army.Soldiers, guy.Army.Knights));
        Assert.Equal(b2.Left(0) with { Catapults = 1 }, w2.Me.Army with { Catapults = 1 });
        Assert.Equal(w2.Me.Army.Empty ? w2.Me.Castle : Realm.Wessex, w2.Me.At);
    }

    // ───────────────────────────── the joust ─────────────────────────────

    [Fact]
    public void Where_the_lance_lands_square_on_the_boss_unhorses_on_the_shield_shatters_off_it_misses()
    {
        Assert.Equal(Strike.Unhorse, Joust.Classify(0, 0));
        Assert.Equal(Strike.Unhorse, Joust.Classify(Joust.BossRadius, 0));
        Assert.Equal(Strike.Shatter, Joust.Classify(Joust.BossRadius + 1, 0));
        Assert.Equal(Strike.Shatter, Joust.Classify(30, 30));
        Assert.Equal(Strike.Shatter, Joust.Classify(0, -Joust.ShieldRadius));
        Assert.Equal(Strike.Miss, Joust.Classify(Joust.ShieldRadius + 1, 0));
        Assert.Equal(Strike.Miss, Joust.Classify(-60, 60));
    }

    /// <summary>Rides a pass with the lance aimed (allowing for the bob) at an offset from where the shield will be.</summary>
    private static Joust Ride(Rating skill, double offX, double offY, int seed = 3)
    {
        var j = new Joust(skill, Realm.Hugh, seed);
        for (int i = 0; i < 400 && j.Phase != JoustPhase.Strike; i++)
        {
            var (sx, sy) = j.ShieldAt(Joust.ChargeSeconds);
            var (tx, ty) = j.TipAt(Joust.ChargeSeconds);
            j.AimX += sx + offX - tx;
            j.AimY += sy + offY - ty;
            j.Step(Field.Step, 0, 0);
        }
        return j;
    }

    [Fact]
    public void A_lance_held_on_the_shield_as_the_horses_meet_strikes_where_it_was_aimed()
    {
        Assert.Equal(Strike.Unhorse, Ride(Rating.Weak, 0, 0).Mine);
        Assert.Equal(Strike.Unhorse, Ride(Rating.Strong, 10, -10).Mine);
        Assert.Equal(Strike.Shatter, Ride(Rating.Average, 0, 40).Mine);
        Assert.Equal(Strike.Miss, Ride(Rating.Average, 80, 0).Mine);
        Assert.Equal(JoustPhase.Strike, Ride(Rating.Average, 0, 0).Phase);
    }

    [Fact]
    public void A_better_jouster_holds_his_lance_steadier_and_the_shield_sways_as_the_knight_rides()
    {
        Assert.True(new Joust(Rating.Strong, Realm.Hugh, 1).BobSize < new Joust(Rating.Weak, Realm.Hugh, 1).BobSize);
        var j = new Joust(Rating.Average, Realm.Raoul, 1);
        Assert.NotEqual(j.ShieldAt(0.5), j.ShieldAt(2.0));
        Assert.Equal(0, j.Progress);
        for (int i = 0; i < 60 * 3; i++) j.Step(Field.Step, 0, 0);
        Assert.Equal(JoustPhase.Charge, j.Phase);
        Assert.InRange(j.Progress, 0.3, 0.7);
        Assert.Contains(Sfx.Hooves, j.Heard);
    }

    [Fact]
    public void The_keys_steer_the_lance_and_the_mouse_draws_it_along_no_faster_than_an_arm_can()
    {
        var j = new Joust(Rating.Average, Realm.Hugh, 1);
        double x = j.AimX, y = j.AimY;
        j.Step(0.1, 0, -1);
        Assert.Equal(y - Joust.KeySpeed * 0.1, j.AimY, 6);
        j.Step(0.1, 1, 0);
        Assert.Equal(x + Joust.KeySpeed * 0.1, j.AimX, 6);
        for (int i = 0; i < 300; i++) j.Step(0.01, -1, -1);
        Assert.Equal(Joust.CentreX - 280, j.AimX);
        Assert.Equal(Joust.CentreY - 220, j.AimY);
        double before = j.AimX;
        j.Step(0.01, 0, 0, (Joust.CentreX + 280, Joust.CentreY - 220));
        Assert.Equal(before + Joust.MouseSpeed * 0.01, j.AimX, 6);
    }

    [Fact]
    public void Unhorse_him_and_you_win_the_joust_at_once_be_unhorsed_and_you_lose_it()
    {
        var j = new Joust(Rating.Average, Realm.Hugh, 1);
        j.Settle(Strike.Unhorse, Strike.Shatter);
        Assert.Contains(Sfx.Cheer, j.Heard);
        j.Step(Joust.StrikeSeconds + 0.01, 0, 0);
        Assert.True(j.Over);
        Assert.Equal(1, j.Result);
        var k = new Joust(Rating.Average, Realm.Hugh, 1);
        k.Settle(Strike.Shatter, Strike.Unhorse);
        k.Step(Joust.StrikeSeconds + 0.01, 0, 0);
        Assert.Equal(-1, k.Result);
    }

    [Fact]
    public void Three_passes_without_a_fall_go_to_the_points_and_both_knights_down_ride_the_pass_again()
    {
        var j = new Joust(Rating.Average, Realm.Hugh, 1);
        foreach (var (mine, his) in new[] { (Strike.Shatter, Strike.Miss), (Strike.Shatter, Strike.Shatter), (Strike.Miss, Strike.Miss) })
        {
            Assert.False(j.Over);
            j.Settle(mine, his);
            j.Step(Joust.StrikeSeconds + 0.01, 0, 0);
        }
        Assert.True(j.Over);
        Assert.Equal((2, 1), (j.Points, j.FoePoints));
        Assert.Equal(1, j.Result);
        var both = new Joust(Rating.Average, Realm.Hugh, 1);
        both.Settle(Strike.Unhorse, Strike.Unhorse);
        Assert.True(both.BothDown);
        both.Step(Joust.StrikeSeconds + 0.01, 0, 0);
        Assert.Equal((1, JoustPhase.Ready), (both.Pass, both.Phase));
        var draw = new Joust(Rating.Average, Realm.Hugh, 1);
        for (int p = 0; p < 3; p++) { draw.Settle(Strike.Miss, Strike.Miss); draw.Step(Joust.StrikeSeconds + 0.01, 0, 0); }
        Assert.Equal(0, draw.Result);
    }

    [Fact]
    public void A_better_jouster_unhorses_you_more_often_and_a_better_rider_keeps_his_seat()
    {
        var weak = new Joust(Rating.Average, Realm.Leofric, 1);
        var strong = new Joust(Rating.Average, Realm.Hugh, 1);
        Assert.Equal(Strike.Unhorse, strong.HisStrike(0));
        Assert.Equal(Strike.Miss, strong.HisStrike(0.999));
        double Rate(Joust j) => Enumerable.Range(0, 1000).Count(i => j.HisStrike(i / 1000.0) == Strike.Unhorse);
        Assert.True(Rate(strong) > Rate(weak));
        Assert.True(Rate(new Joust(Rating.Strong, Realm.Hugh, 1)) < Rate(new Joust(Rating.Weak, Realm.Hugh, 1)));
    }

    // ───────────────────────────── the siege ─────────────────────────────

    [Fact]
    public void A_shot_flies_in_an_arc_under_gravity()
    {
        Assert.Equal((Siege.LaunchX, Siege.LaunchY), Siege.Arc(45, 600, 0));
        double a = 45 * Math.PI / 180, top = 600 * Math.Sin(a) / Siege.Gravity;
        var (x1, y1) = Siege.Arc(45, 600, top - 0.3);
        var (x2, y2) = Siege.Arc(45, 600, top + 0.3);
        Assert.Equal(y1, y2, 6);
        Assert.True(x2 > x1);
        Assert.True(Siege.Arc(45, 600, top).Y < y1);
        Assert.Null(Siege.HeightAtWall(15, Siege.MinPower));
        Assert.Equal(Landing.Short, Siege.Predict(15, Siege.MinPower));
    }

    [Fact]
    public void Where_a_shot_comes_down_short_on_the_wall_on_the_battlements_inside_or_over()
    {
        var cases = new (double Angle, double Power, Landing Want)[]
        {
            (20, Siege.MinPower, Landing.Short),
            (38, Pilot.PowerFor(38, 0.3), Landing.Wall),
            (38, Pilot.PowerFor(38, 0.96), Landing.Battlements),
            (70, Pilot.PowerOver(70), Landing.Inside),
            (35, Siege.MaxPower, Landing.Over),
        };
        foreach (var (angle, power, want) in cases)
        {
            Assert.Equal(want, Siege.Predict(angle, power));
            var s = new Siege(1, new Troops(30, 4), Realm.Guy, 1) { Angle = angle, Power = power };
            Assert.True(s.Loose());
            for (int i = 0; i < 600 && s.Phase == SiegePhase.Flight; i++) s.Step(Field.Step, 0, 0, false);
            Assert.Equal(want, s.Last);
        }
        Assert.Equal(0.3, Siege.HeightAtWall(38, Pilot.PowerFor(38, 0.3))!.Value, 3);
    }

    [Fact]
    public void Boulders_low_and_hard_on_the_wall_break_it_and_a_breached_wall_ends_the_siege()
    {
        Assert.True(Siege.WallDamage(0.3, 800) > Siege.WallDamage(0.9, 800));
        Assert.True(Siege.WallDamage(0.3, 800) > Siege.WallDamage(0.3, 400));
        var s = new Siege(3, new Troops(30, 4), Realm.Guy, 1);
        Assert.Equal(10, s.Shots);
        s.Angle = 38;
        s.Power = Pilot.PowerFor(38, 0.3);
        int shots = 0;
        while (!s.Breached && s.Shots > 0)
        {
            Assert.True(s.Loose());
            shots++;
            for (int i = 0; i < 600 && s.Phase == SiegePhase.Flight; i++) s.Step(Field.Step, 0, 0, false);
            Assert.Equal(Landing.Wall, s.Last);
            Assert.Contains(Sfx.Crash, s.Heard);
            for (int i = 0; i < 600 && s.Phase == SiegePhase.Result; i++) s.Step(Field.Step, 0, 0, false);
        }
        Assert.True(s.Breached);
        Assert.InRange(shots, 2, 4);
        Assert.Equal(SiegePhase.Done, s.Phase);
        Assert.Equal(1 + (Sim.CastleWalls - 1) * 0.2, s.Walls, 6);
        Assert.Equal(Sim.CastleWalls, new Siege(1, default, 0, 1).Walls);
    }

    [Fact]
    public void Greek_fire_and_disease_over_the_wall_kill_more_of_the_garrison_than_a_boulder()
    {
        int Killed(Ammo ammo)
        {
            var s = new Siege(1, new Troops(50, 10), Realm.Guy, 1);
            s.Select(ammo);
            s.Angle = 70;
            s.Power = Pilot.PowerOver(70);
            s.Loose();
            for (int i = 0; i < 600 && s.Phase == SiegePhase.Flight; i++) s.Step(Field.Step, 0, 0, false);
            Assert.Equal(Landing.Inside, s.Last);
            return s.KilledSoldiers + 3 * s.KilledKnights;
        }
        int boulder = Killed(Ammo.Boulder), fire = Killed(Ammo.GreekFire), disease = Killed(Ammo.Disease);
        Assert.True(fire > boulder);
        Assert.True(disease > boulder);
        Assert.Equal(1 + 10 + 3 * 2, disease);
    }

    [Fact]
    public void Greek_fire_and_disease_are_few_and_the_shots_are_counted()
    {
        Assert.Equal(6, new Siege(1, default, 0, 1).Shots);
        Assert.Equal(12, new Siege(9, default, 0, 1).Shots);
        var s = new Siege(1, new Troops(20, 0), Realm.Guy, 1);
        Assert.Equal(2, s.AmmoLeft(Ammo.GreekFire));
        Assert.Equal(1, s.AmmoLeft(Ammo.Disease));
        for (int k = 0; k < 2; k++)
        {
            s.Select(Ammo.GreekFire);
            Assert.True(s.Loose());
            for (int i = 0; i < 2000 && s.Phase != SiegePhase.Aim; i++) s.Step(Field.Step, 0, 0, false);
        }
        Assert.Equal(0, s.AmmoLeft(Ammo.GreekFire));
        s.Select(Ammo.GreekFire);
        Assert.Equal(Ammo.Boulder, s.Ammo);
        Assert.Equal(4, s.Shots);
        s.NextAmmo();
        Assert.Equal(Ammo.Disease, s.Ammo);
        s.Storm();
        Assert.True(s.Over);
    }

    [Fact]
    public void Up_raises_the_catapults_aim_right_adds_power_and_the_mouse_aims_along_the_line_to_it()
    {
        var s = new Siege(1, default, 0, 1);
        double angle = s.Angle, power = s.Power;
        s.Step(0.1, 0, -1, false);
        Assert.Equal(angle + Siege.AngleSpeed * 0.1, s.Angle, 6);
        s.Step(0.1, 1, 0, false);
        Assert.Equal(power + Siege.PowerSpeed * 0.1, s.Power, 6);
        s.Step(0.01, 0, 0, false, (Siege.LaunchX + 400, Siege.LaunchY - 400));
        Assert.Equal(45, s.Angle, 6);
        Assert.Equal(Math.Sqrt(2) * 400 * 1.25, s.Power, 6);
        s.Step(0.01, 0, 0, true);
        Assert.Equal(SiegePhase.Flight, s.Phase);
        Assert.Contains(Sfx.Launch, s.Heard);
    }

    // ───────────────────────────── the swordfight ─────────────────────────────

    /// <summary>A raid with the first guard come up to the hero and standing on guard, his next blow put off.</summary>
    private static Raid Engaged(Rating skill = Rating.Average, bool lady = false)
    {
        var r = new Raid(skill, Realm.Hugh, lady, 1);
        for (int i = 0; i < 60 * 6 && !(r.Phase == RaidPhase.Fight && r.Foe!.Act == Act.Guard); i++)
        {
            r.Step(Field.Step);
            if (r.Foe is { } f) f.NextAttack = 5;
        }
        var g = r.Foe!;
        g.NextAttack = 5;
        g.NextShift = 5;
        r.Heard.Clear();
        return r;
    }

    private static void Steps(Raid r, double seconds)
    {
        for (int i = 0; i < seconds * 60; i++) r.Step(Field.Step);
    }

    [Fact]
    public void A_stroke_where_his_blade_is_not_wounds_him_and_one_into_his_guard_is_turned()
    {
        var r = Engaged();
        var g = r.Foe!;
        g.Guard = Line.High;
        Assert.True(r.Attack(Line.Low));
        Steps(r, Raid.CutSeconds + 0.05);
        Assert.Equal(g.MaxHp - 1, g.Hp);
        Assert.Contains(Sfx.Slash, r.Heard);
        g.NextAttack = 5;
        Steps(r, 1.5);
        g.Guard = Line.Middle;
        g.NextShift = 5;
        g.NextAttack = 5;
        int hp = g.Hp;
        Assert.True(r.Attack(Line.Middle));
        Steps(r, Raid.CutSeconds + 0.05);
        Assert.Equal(hp, g.Hp);
        Assert.Contains(Sfx.Clang, r.Heard);
    }

    [Fact]
    public void A_parry_as_his_blow_falls_turns_it_and_leaves_him_off_balance_and_open()
    {
        var r = Engaged();
        var g = r.Foe!;
        g.NextAttack = 0;
        r.Step(Field.Step);
        Assert.Equal(Act.Wind, g.Act);
        while (g.T < Raid.WindSeconds - r.ParryWindow * 0.5) r.Step(Field.Step);
        Assert.True(r.Parry());
        Steps(r, 0.4);
        Assert.Equal(Act.Stagger, g.Act);
        Assert.Equal(r.Hero.MaxHp, r.Hero.Hp);
        g.Guard = Line.Middle;
        int hp = g.Hp;
        Steps(r, 0.15);
        Assert.True(r.Attack(Line.Middle));
        Steps(r, Raid.CutLands + 0.02);
        Assert.Equal(hp - 1, g.Hp);
    }

    [Fact]
    public void A_parry_made_too_soon_is_over_before_the_blow_falls()
    {
        var r = Engaged();
        var g = r.Foe!;
        g.NextAttack = 0;
        r.Step(Field.Step);
        Assert.True(r.Parry());
        Steps(r, Raid.WindSeconds + 0.1);
        Assert.Equal(r.Hero.MaxHp - 1, r.Hero.Hp);
        Assert.Contains(Sfx.Hurt, r.Heard);
    }

    [Fact]
    public void A_better_swordsman_has_longer_to_parry_and_more_strength()
    {
        var weak = new Raid(Rating.Weak, Realm.Hugh, false, 1);
        var strong = new Raid(Rating.Strong, Realm.Hugh, false, 1);
        Assert.True(strong.ParryWindow > weak.ParryWindow);
        Assert.True(strong.Hero.MaxHp > weak.Hero.MaxHp);
        Assert.True(Raid.WindSeconds > weak.ParryWindow);
    }

    [Fact]
    public void Struck_as_he_winds_up_he_is_wounded_but_his_blow_still_falls()
    {
        var r = Engaged();
        var g = r.Foe!;
        g.Guard = Line.High;
        g.NextAttack = 0;
        r.Step(Field.Step);
        Assert.True(r.Attack(Line.Low));
        Steps(r, Raid.WindSeconds + 0.05);
        Assert.Equal(g.MaxHp - 1, g.Hp);
        Assert.Equal(r.Hero.MaxHp - 1, r.Hero.Hp);
    }

    [Fact]
    public void Beat_every_guard_and_the_raid_succeeds_fall_and_you_are_taken()
    {
        var r = Engaged();
        for (int i = 0; i < 60 * 120 && !r.Over; i++)
        {
            if (r.Foe is { } g) { g.NextAttack = 5; g.Guard = Line.High; g.NextShift = 5; }
            r.Step(Field.Step, Line.Low);
        }
        Assert.True(r.Over);
        Assert.True(r.Success);
        Assert.Equal(3, r.Current);
        Assert.Equal(4, new Raid(Rating.Average, Realm.Hugh, true, 1).Guards.Count);
        var lost = Engaged(Rating.Weak);
        for (int i = 0; i < 60 * 120 && !lost.Over; i++) lost.Step(Field.Step);
        Assert.True(lost.Over);
        Assert.False(lost.Success);
        Assert.Equal(0, lost.Hero.Hp);
    }

    // ───────────────────────────── tournaments, raids, the outlaw and the lady ─────────────────────────────

    [Fact]
    public void A_tournament_for_land_wins_a_shire_or_loses_one_and_one_for_fame_wins_a_purse_renown_and_knights()
    {
        var w = Realm1149();
        Assert.Equal(Realm.Sussex, Sim.PrizeLand(w, Realm.Hugh));
        Assert.Equal(-1, Sim.PrizeLand(w, w.Player));
        Assert.DoesNotContain(Realm.Osric, Sim.TournamentFoes(w, forLand: true));
        Assert.Contains(Realm.Osric, Sim.TournamentFoes(w, forLand: false));
        Sim.Tournament(w, Realm.Hugh, forLand: true, result: 1);
        Assert.Equal(w.Player, w.Owner[Realm.Sussex]);
        Assert.Equal(1, w.Renown);
        Sim.Tournament(w, Realm.Raoul, forLand: true, result: -1);
        Assert.Equal(Realm.Raoul, w.Owner[Realm.Sussex]);
        int gold = w.Me.Gold;
        var army = w.Me.Army;
        Sim.Tournament(w, Realm.Osric, forLand: false, result: 1);
        Assert.Equal(gold + 10, w.Me.Gold);
        Assert.Equal(army + new Troops(0, 2), w.Me.Army);
        Assert.Equal(2, w.Renown);
        var owners = (int[])w.Owner.Clone();
        Sim.Tournament(w, Realm.Osric, forLand: true, result: 0);
        Assert.Equal(owners, w.Owner);
        Assert.Equal("NEITHER YIELDS", w.Cards.Last().Title);
        // Losing for land with no land to lose costs gold instead.
        Sim.Tournament(w, Realm.Hugh, forLand: true, result: -1);
        Assert.Equal(gold + 10 - 10, w.Me.Gold);
    }

    [Fact]
    public void A_raid_takes_half_a_lords_gold_and_a_failed_one_costs_half_your_own_in_ransom()
    {
        var w = Realm1149();
        var hugh = w.Lords[Realm.Hugh];
        hugh.Gold = 40;
        w.Me.Gold = 10;
        Sim.Raided(w, Realm.Hugh, success: true, forLady: false);
        Assert.Equal(20, hugh.Gold);
        Assert.Equal(10 + 20 + Sim.RaidPlate, w.Me.Gold);
        Sim.Raided(w, Realm.Hugh, success: false, forLady: false);
        Assert.Equal(35 - 18, w.Me.Gold);
        Assert.Equal("TAKEN IN THE NIGHT", w.Cards.Last().Title);
        Assert.Equal(2, w.Raids);
        Assert.Equal([Realm.Osric, Realm.Godwin, Realm.Leofric, Realm.Hugh, Realm.Raoul, Realm.Guy], Sim.RaidTargets(w).ToArray());
    }

    [Fact]
    public void A_Norman_carries_off_the_lady_and_freeing_her_brings_a_wedding_her_fathers_shire_and_a_dowry()
    {
        var w = Realm1149();
        w.LadyTurn = w.Turn + 1;
        Sim.EndTurn(w);
        Assert.Equal(LadyState.Held, w.Lady);
        Assert.True(Realm.IsNorman(w.LadyHeldBy));
        Assert.Contains(w.Cards, c => c.Title == "A LADY CARRIED OFF");
        Assert.True(Sim.HoldsLady(w, w.LadyHeldBy));
        int gold = w.Me.Gold;
        w.Owner[Realm.LadyLand] = Realm.Nobody;
        Sim.Raided(w, w.LadyHeldBy, success: true, forLady: true);
        Assert.Equal(LadyState.Married, w.Lady);
        Assert.Equal(w.Player, w.Owner[Realm.LadyLand]);
        Assert.Equal(gold + 20, w.Me.Gold);
        Assert.Equal("A WEDDING", w.Cards.Last().Title);
        Assert.Contains(Sfx.Wedding, w.Heard);
    }

    [Fact]
    public void Taking_the_castle_where_the_lady_is_held_frees_her_too()
    {
        var w = Realm1149();
        w.Lady = LadyState.Held;
        w.LadyHeldBy = Realm.Raoul;
        Sim.Conquer(w, w.Player, Realm.Lincoln, new Troops(20, 0, 1));
        Assert.Equal(LadyState.Married, w.Lady);
    }

    [Fact]
    public void The_outlaw_raids_for_you_or_lends_his_archers_three_times_in_all()
    {
        var w = Realm1149();
        var raoul = w.Lords[Realm.Raoul];
        raoul.Gold = 30;
        int gold = w.Me.Gold;
        Assert.True(Sim.GilRaid(w, Realm.Raoul));
        Assert.Equal(15, raoul.Gold);
        Assert.Equal(gold + 15, w.Me.Gold);
        Assert.False(Sim.GilRaid(w, w.Player));
        Assert.True(Sim.GilAid(w));
        Assert.True(w.GilAid);
        Assert.False(Sim.GilAid(w));
        w.GilAid = false;
        Assert.True(Sim.GilAid(w));
        Assert.Equal(0, w.GilCalls);
        w.GilAid = false;
        Assert.False(Sim.GilAid(w));
        Assert.False(Sim.GilRaid(w, Realm.Hugh));
    }

    [Fact]
    public void The_outlaws_archers_march_with_the_army_into_its_next_battle_only()
    {
        var g = Engine();
        g.Go(Realm.Edmund, seed: 2);
        g.DoOrder(Order.Gil);
        Assert.Equal(MapMode.Gil, g.Mode);
        g.CallGil(raid: false);
        Assert.True(g.World.GilAid);
        Assert.Equal(Scene.Card, g.Scene);
        g.DismissCard();
        g.Conquer(Realm.Cornwall);
        Assert.Equal(Scene.Battle, g.Scene);
        Assert.True(g.Battle!.Archers);
        Assert.False(g.World.GilAid);
        Assert.Equal(2, g.World.GilCalls);
    }

    // ───────────────────────────── the other lords ─────────────────────────────

    [Fact]
    public void Normans_never_march_on_Normans_nor_Saxon_lords_on_Saxons()
    {
        Assert.True(Sim.Allies(Realm.Hugh, Realm.Guy));
        Assert.True(Sim.Allies(Realm.Osric, Realm.Edmund));
        Assert.False(Sim.Allies(Realm.Hugh, Realm.Edmund));
        Assert.False(Sim.Allies(Realm.Hugh, Realm.Nobody));
        for (int seed = 1; seed <= 20; seed++)
        {
            var w = Realm1149(Realm.Edmund, seed);
            w.Turn = 10;
            var hugh = w.Lords[Realm.Hugh];
            // Hugh's great army in Essex, next to nothing but Norman land.
            foreach (int s in new[] { Realm.Essex, Realm.Norfolk, Realm.Oxford }) w.Owner[s] = s == Realm.Essex ? Realm.Hugh : Realm.Raoul;
            hugh.At = Realm.Essex;
            hugh.Army = new Troops(300, 40, 2);
            var owners = (int[])w.Owner.Clone();
            Sim.LordTurn(w, hugh);
            Assert.Equal(owners, w.Owner);
            // Osric's great army next to Godwin's castle leaves it alone.
            var osric = w.Lords[Realm.Osric];
            osric.Army = new Troops(300, 40, 2);
            osric.At = Realm.York;
            Sim.LordTurn(w, osric);
            Assert.Equal(Realm.Godwin, w.Owner[Realm.Northumbria]);
        }
    }

    [Fact]
    public void A_lord_marches_on_a_weak_shire_next_to_his_army_and_keeps_his_garrison_up()
    {
        bool took = false;
        for (int seed = 1; seed <= 20 && !took; seed++)
        {
            var w = Realm1149(Realm.Edmund, seed);
            var hugh = w.Lords[Realm.Hugh];
            hugh.Army = new Troops(80, 10, 1);
            hugh.Gold = 100;
            Sim.LordTurn(w, hugh);
            Assert.True(hugh.Garrison.Strength >= 40 + 2 * w.Turn);
            took = w.Owner[Realm.Essex] == Realm.Hugh || w.Owner[Realm.Oxford] == Realm.Hugh;
        }
        Assert.True(took);
    }

    [Fact]
    public void No_castle_is_stormed_in_the_first_seasons_and_then_a_Norman_army_at_your_gate_must_be_fought()
    {
        var w = Realm1149(Realm.Edmund);
        var hugh = w.Lords[Realm.Hugh];
        hugh.At = Realm.Sussex;
        hugh.Army = new Troops(200, 30, 2);
        w.Me.Garrison = new Troops(10, 0);
        w.Turn = 1;
        for (int i = 0; i < 10; i++) Sim.LordTurn(w, hugh);
        Assert.Null(w.Assault);
        w.Turn = Sim.CastleGrace;
        for (int i = 0; i < 20 && w.Assault == null; i++) Sim.LordTurn(w, hugh);
        Assert.NotNull(w.Assault);
        Assert.Equal(Realm.Wessex, w.Assault!.Shire);
        Assert.Equal("YOUR CASTLE IS BESIEGED", w.Cards.Last().Title);
        Assert.Equal(w.Player, w.Owner[Realm.Wessex]);
    }

    [Fact]
    public void Beat_off_an_attack_on_your_castle_and_it_stands_lose_the_fight_and_the_game_is_lost()
    {
        foreach (bool hold in new[] { true, false })
        {
            var g = Engine();
            g.Go(Realm.Edmund, seed: 4);
            var w = g.World;
            w.Turn = Sim.CastleGrace + 2;
            var hugh = w.Lords[Realm.Hugh];
            hugh.At = Realm.Sussex;
            hugh.Army = hold ? new Troops(60, 0, 1) : new Troops(400, 60, 1);
            w.Me.Garrison = hold ? new Troops(200, 20) : new Troops(10, 0);
            w.Assault = new Assault(Realm.Hugh, hugh.Army, Realm.Wessex, 0.5);
            g.Tell(new Card("YOUR CASTLE IS BESIEGED", "Defend it!", Realm.Hugh, CardKind.War, Realm.Wessex));
            g.DismissCard();
            Assert.Equal(Scene.Battle, g.Scene);
            Assert.Equal(1, g.Battle!.Mine);
            Assert.True(g.Battle.Walls > 1);
            for (int i = 0; i < 60 * 60 && g.Scene == Scene.Battle; i++)
            {
                if (g.Battle is { Phase: BattlePhase.Choose }) g.Command(Tactic.StandFast);
                g.Tick(default);
            }
            Assert.Null(w.Assault);
            if (hold)
            {
                Assert.Equal(w.Player, w.Owner[Realm.Wessex]);
                Assert.Equal("THE ATTACK IS BEATEN OFF", g.Card?.Title);
            }
            else
            {
                Assert.True(w.Lost);
                for (int i = 0; i < 20 && g.Scene == Scene.Card && g.State == GameState.Playing; i++) g.DismissCard();
                Assert.Equal(GameState.GameOver, g.State);
            }
        }
    }

    [Fact]
    public void The_other_lords_spend_their_taxes_and_the_season_ends_with_their_news()
    {
        var w = Realm1149(Realm.Leofric, 3);
        for (int i = 0; i < 6; i++) Sim.EndTurn(w);
        Assert.Equal(7, w.Turn);
        Assert.Contains(w.Cards, c => c.Kind is CardKind.War or CardKind.News);
        Assert.True(w.Lords.Where(l => Realm.IsNorman(l.Id)).Sum(l => w.LandCount(l.Id)) > 6);
        var copy = w.Copy();
        Sim.EndTurn(copy);
        Assert.Equal(7, w.Turn);
        Assert.NotSame(w.Lords[0], copy.Lords[0]);
    }

    // ───────────────────────────── the game ─────────────────────────────

    [Fact]
    public void Enter_on_the_title_goes_to_the_knights_and_the_one_chosen_rides_out()
    {
        var g = Engine();
        Assert.Equal(GameState.Title, g.State);
        g.Tick(default, start: true);
        Assert.Equal(GameState.Choose, g.State);
        g.Tick(default);
        g.Press(new Controls(Dx: 1));
        Assert.Equal(1, g.ChooseIndex);
        g.Press(new Controls(Fire: true));
        Assert.Equal(GameState.Playing, g.State);
        Assert.Equal(Realm.Osric, g.World.Player);
        Assert.Equal(Scene.Card, g.Scene);
        Assert.Equal("THE KING IS DEAD", g.Card!.Title);
        g.Press(new Controls(Fire: true));
        Assert.Equal(Scene.Map, g.Scene);
        Assert.Equal(MapMode.Orders, g.Mode);
        var h = Engine();
        h.NewGame();
        h.Press(new Controls(Pick: 3));
        Assert.Equal(Realm.Godwin, h.World.Player);
    }

    [Fact]
    public void The_orders_are_chosen_with_the_keys_and_passing_ends_the_season()
    {
        var g = Engine();
        g.Go(Realm.Edmund);
        Assert.Equal(0, g.MenuIndex);
        g.Press(new Controls(Dy: 1));
        g.Press(new Controls(Dy: 1));
        Assert.Equal(2, g.MenuIndex);
        g.Press(new Controls(Fire: true));
        Assert.Equal(MapMode.Raid, g.Mode);
        g.Press(new Controls(Back: true));
        Assert.Equal(MapMode.Orders, g.Mode);
        g.Press(new Controls(Pick: 7));
        Assert.Equal(2, g.World.Turn);
        while (g.Scene == Scene.Card) g.DismissCard();
        Assert.Equal(Scene.Map, g.Scene);
        // Muster is refused away from home.
        g.World.Me.At = Realm.Oxford;
        g.DoOrder(Order.Garrison);
        Assert.Equal(MapMode.Orders, g.Mode);
        Assert.NotNull(g.Message);
    }

    [Fact]
    public void Going_conquering_with_the_keys_picks_a_shire_next_to_the_army_and_fights_for_it()
    {
        var g = Engine();
        g.Go(Realm.Edmund);
        g.Press(new Controls(Pick: 2));
        Assert.Equal(MapMode.Conquer, g.Mode);
        var list = g.Candidates;
        Assert.Equal(4, list.Count);
        g.Press(new Controls(Dx: 1));
        Assert.Equal(list[1], g.Selected);
        while (g.Selected != Realm.Cornwall) g.Press(new Controls(Dx: 1));
        g.Press(new Controls(Fire: true));
        Assert.Equal(Scene.Battle, g.Scene);
        Assert.Equal(Realm.Nobody, g.Battle!.Lord[1]);
        g.Press(new Controls(Pick: 3));
        Assert.Equal(Tactic.FierceAttack, g.Battle.Last[0]);
        g.World.Me.Army = new Troops(500, 50);
        for (int i = 0; i < 60 * 60 && g.Scene == Scene.Battle; i++)
        {
            g.Tick(default);
            if (g.Battle is { Phase: BattlePhase.Choose }) g.Command(Tactic.FierceAttack);
        }
        Assert.Equal(Scene.Card, g.Scene);
        Assert.Equal(g.World.Player, g.World.Owner[Realm.Cornwall]);
        Assert.Equal("CORNWALL IS YOURS", g.Card!.Title);
        Assert.Equal(2, g.World.Turn);
    }

    [Fact]
    public void A_siege_with_a_catapult_comes_before_the_storming_of_a_castle()
    {
        var g = Engine();
        g.Go(Realm.Edmund);
        var me = g.World.Me;
        me.Army = new Troops(400, 60, 2);
        g.Conquer(Realm.Gloucester);
        Assert.Equal(Scene.Siege, g.Scene);
        var s = g.Siege!;
        Assert.Equal(8, s.Shots);
        g.Press(new Controls(Storm: true));
        for (int i = 0; i < 300 && g.Scene == Scene.Siege; i++) g.Tick(default);
        Assert.Equal(Scene.Battle, g.Scene);
        Assert.Equal(Sim.CastleWalls, g.Battle!.Walls);
        for (int i = 0; i < 60 * 60 && g.Scene == Scene.Battle; i++)
        {
            g.Tick(default);
            if (g.Battle is { Phase: BattlePhase.Choose }) g.Command(Tactic.FierceAttack);
        }
        Assert.False(g.World.Lords[Realm.Guy].Alive);
        Assert.Equal(1, g.World.NormansBeaten);
    }

    [Fact]
    public void The_buy_panel_works_from_the_keys()
    {
        var g = Engine();
        g.Go(Realm.Edmund);
        g.World.Me.Gold = 30;
        var army = g.World.Me.Army;
        g.Press(new Controls(Pick: 4));
        Assert.Equal(MapMode.Buy, g.Mode);
        g.Press(new Controls(Dx: 1));
        g.Press(new Controls(Dx: 1));
        g.Press(new Controls(Dy: 1));
        g.Press(new Controls(Dx: 1));
        Assert.Equal((2, 1, 0), (g.BuySoldiers, g.BuyKnights, g.BuyCatapults));
        Assert.Equal(6, g.BuyCost);
        g.Press(new Controls(Fire: true));
        Assert.Equal(MapMode.Orders, g.Mode);
        Assert.Equal(24, g.World.Me.Gold);
        Assert.Equal(army + new Troops(2, 1), g.World.Me.Army);
        // More than can be paid for is held back.
        g.World.Me.Gold = 20;
        g.DoOrder(Order.Buy);
        g.AdjustBuy(2, 1);
        g.AdjustBuy(2, 1);
        Assert.Equal(1, g.BuyCatapults);
    }

    [Fact]
    public void A_tournament_rides_a_joust_and_its_result_decides_the_prize()
    {
        var g = Engine();
        g.Go(Realm.Osric);
        g.DoOrder(Order.Tournament);
        Assert.Equal(MapMode.Tournament, g.Mode);
        g.Press(new Controls(Fire: true));
        Assert.Equal(Scene.Joust, g.Scene);
        Assert.True(g.ForLand);
        var j = g.Joust!;
        int foe = j.Foe;
        int prize = Sim.PrizeLand(g.World, foe);
        j.Settle(Strike.Unhorse, Strike.Miss);
        for (int i = 0; i < 60 * 5 && g.Scene == Scene.Joust; i++) g.Tick(default);
        Assert.Equal("VICTORY IN THE LISTS", g.Card!.Title);
        Assert.Equal(prize, g.Card.Shire);
        Assert.Equal(1, g.World.Tournaments);
    }

    [Fact]
    public void A_raid_by_night_ends_in_gold_or_ransom()
    {
        var g = Engine();
        g.Go(Realm.Godwin);
        g.World.Lords[Realm.Hugh].Gold = 50;
        g.DoOrder(Order.Raid);
        g.ClickShire(Realm.Kent);
        Assert.Equal(Scene.Raid, g.Scene);
        Assert.False(g.Raid!.ForLady);
        int gold = g.World.Me.Gold;
        for (int i = 0; i < 60 * 120 && g.Scene == Scene.Raid; i++)
        {
            if (g.Raid?.Foe is { } f) { f.NextAttack = 5; f.Guard = Line.High; f.NextShift = 5; }
            g.Tick(i % 2 == 0 ? new Controls(Dy: 1) : default);
        }
        Assert.Equal("THE RAID SUCCEEDS", g.Card!.Title);
        Assert.Contains($"{25 + Sim.RaidPlate} gold", g.Card.Text);
        Assert.True(g.World.Me.Gold >= gold + 25 + Sim.RaidPlate);
    }

    [Fact]
    public void The_game_is_saved_each_season_and_can_be_continued_and_a_finished_one_is_gone()
    {
        string folder = TempFolder();
        var g = new GameEngine(HighScoreTable.Load(folder), folder, 3) { Silent = true };
        Assert.False(g.HasSave);
        g.Go(Realm.Leofric);
        g.DoOrder(Order.Pass);
        while (g.Scene == Scene.Card) g.DismissCard();
        Assert.True(g.HasSave);
        var saved = SaveFile.Write(g.World);
        var h = new GameEngine(HighScoreTable.Load(folder), folder, 3) { Silent = true };
        Assert.True(h.HasSave);
        Assert.True(h.Continue());
        Assert.Equal(GameState.Playing, h.State);
        Assert.Equal(saved, SaveFile.Write(h.World));
        Assert.Equal(2, h.World.Turn);
        Assert.Equal(Realm.Leofric, h.World.Player);
        // The continued game goes on just as the saved one would.
        var a = g.World.Copy();
        var b = h.World.Copy();
        Sim.EndTurn(a);
        Sim.EndTurn(b);
        Assert.Equal(SaveFile.Write(a), SaveFile.Write(b));
        Sim.Fall(h.World, Realm.Hugh, h.World.Me);
        h.DoOrder(Order.Pass);
        for (int i = 0; i < 30 && h.Scene == Scene.Card && h.State == GameState.Playing; i++) h.DismissCard();
        Assert.Equal(GameState.GameOver, h.State);
        Assert.False(h.HasSave);
        Assert.Null(SaveFile.Read("NOT A SAVE"));
        File.WriteAllText(SaveFile.PathIn(folder), "AVADEFENDEROFTHECROWN 1\ngame x\n");
        Assert.False(h.Continue());
    }

    [Fact]
    public void The_crown_won_the_score_is_entered_in_the_roll_of_honour()
    {
        var g = Engine();
        g.Go(Realm.Edmund);
        var w = g.World;
        Sim.Conquer(w, w.Player, Realm.Kent, new Troops(30, 5, 1));
        Sim.Conquer(w, w.Player, Realm.Lincoln, new Troops(30, 5, 1));
        w.Me.At = Realm.Wessex;
        w.Me.Army = new Troops(500, 50, 1);
        g.Conquer(Realm.Gloucester);
        g.Siege!.Storm();
        for (int i = 0; i < 60 * 60 && g.Scene != Scene.Card; i++)
        {
            g.Tick(default);
            if (g.Battle is { Phase: BattlePhase.Choose }) g.Command(Tactic.FierceAttack);
        }
        Assert.True(w.Won);
        for (int i = 0; i < 30 && g.Scene == Scene.Card && g.State == GameState.Playing; i++) g.DismissCard();
        Assert.Equal(GameState.GameOver, g.State);
        int score = g.Score;
        Assert.True(score > 5000);
        g.Run((int)(60 * GameEngine.OverSeconds) + 2);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Type('A');
        g.Initials.Type('V');
        g.Initials.Type('A');
        g.Run(2);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(0, g.NewEntryIndex);
        Assert.Equal(("AVA", score), (g.HighScores.Entries[0].Initials, g.HighScores.Entries[0].Score));
        Assert.Equal(HighScoreTable.Load(g.HighScores.Folder).Entries[0], g.HighScores.Entries[0]);
    }

    [Fact]
    public void The_title_runs_a_demonstration_of_the_computer_playing_then_the_roll_of_honour_then_the_title()
    {
        var g = Engine();
        g.Run((int)(60 * GameEngine.TitleIdleSeconds) + 5);
        Assert.Equal(GameState.Attract, g.State);
        int seen = 0;
        var scenes = new HashSet<Scene>();
        for (int i = 0; i < (int)(60 * GameEngine.AttractSeconds) + 5 && g.State == GameState.Attract; i++)
        {
            g.Tick(default);
            scenes.Add(g.Scene);
            seen = Math.Max(seen, g.World.Turn);
        }
        Assert.True(seen >= 3, $"the demonstration got to season {seen}");
        Assert.Contains(Scene.Battle, scenes);
        Assert.Equal(GameState.HighScores, g.State);
        g.Run((int)(60 * GameEngine.HighScoreSeconds) + 5);
        Assert.Equal(GameState.Title, g.State);
        // Enter during the demonstration goes back to the title.
        g.Run((int)(60 * GameEngine.TitleIdleSeconds) + 5);
        Assert.Equal(GameState.Attract, g.State);
        g.Tick(default, start: true);
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Paused_the_game_stands_still()
    {
        var g = Engine();
        g.Go(Realm.Edmund);
        g.Conquer(Realm.Cornwall);
        g.Command(Tactic.StandFast);
        g.Pause();
        Assert.Equal(GameState.Paused, g.State);
        double t = g.Battle!.T;
        g.Run(60);
        Assert.Equal(t, g.Battle.T);
        g.Resume();
        g.Run(1);
        Assert.True(g.Battle.T > t);
    }

    [Fact]
    public void The_computer_player_marches_on_a_weak_neighbour_and_buys_a_catapult_before_a_castle()
    {
        var w = Realm1149(Realm.Edmund);
        w.Me.Army = new Troops(60, 8);
        w.Me.Gold = 0;
        var plan = Pilot.Decide(w);
        Assert.Equal(Order.Conquer, plan.Order);
        Assert.Contains(plan.Target, Sim.Marches(w, w.Player));
        Assert.NotEqual(Realm.Gloucester, plan.Target);
        w.Me.Gold = 40;
        Assert.Equal(1, Pilot.Decide(w).Buy.Catapults);
        Assert.True(Pilot.GarrisonWanted(w) >= 18);
        Assert.Equal(2, Pilot.Distance(Realm.Wessex, Realm.Hereford));
    }

    [Fact]
    public void The_computer_player_wins_a_whole_campaign_scenes_and_all()
    {
        // The same campaign as the probe's fourth (CROWN_PROBE): Leofric, crowned in his fourteenth season.
        var g = Engine(103);
        g.Go(Realm.Leofric, seed: 1000 + 3 * 37);
        for (int t = 0; t < 60 * 60 * 60 && g.State == GameState.Playing; t++)
        {
            if (g.Scene == Scene.Card) g.DismissCard();
            else g.Tick(g.Pilot.Drive(g, Field.Step));
        }
        Assert.True(g.World.Won, $"season {g.World.Turn}: {g.World.LandCount(g.World.Player)} shires");
        Assert.Equal(GameState.GameOver, g.State);
    }

    // ───────────────────────────── keys and sounds ─────────────────────────────

    [Fact]
    public void The_keys_up_is_minus_one_as_on_the_screen_and_the_newest_key_wins()
    {
        var input = new InputHandler(gamepad: false);
        input.OnKeyDown(Key.Up);
        Assert.Equal(-1, input.Player.Dy);
        input.OnKeyDown(Key.Down);
        Assert.Equal(1, input.Player.Dy);
        input.OnKeyUp(Key.Down);
        Assert.Equal(-1, input.Player.Dy);
        input.OnKeyUp(Key.Up);
        input.OnKeyDown(Key.A);
        Assert.Equal(-1, input.Player.Dx);
        input.OnKeyUp(Key.A);
        input.OnKeyDown(Key.D3);
        Assert.Equal(3, input.Player.Pick);
        input.OnKeyUp(Key.D3);
        input.OnKeyDown(Key.G);
        Assert.True(input.Player.Storm);
        input.OnKeyUp(Key.G);
        input.OnKeyDown(Key.Back);
        Assert.True(input.Player.Back);
        input.OnKeyUp(Key.Back);
        input.OnKeyDown(Key.Space);
        Assert.True(input.Player.Fire);
        input.OnKeyUp(Key.Space);
        input.OnKeyDown(Key.Enter);
        Assert.True(input.Start && input.Player.Fire);
        input.Dispose();
    }

    [Fact]
    public void A_shift_let_go_as_caps_lock_is_let_go()
    {
        var input = new InputHandler(gamepad: false);
        input.OnKeyDown(Key.LeftShift, PhysicalKey.ShiftLeft);
        Assert.True(input.Player.Alt);
        input.OnKeyUp(Key.CapsLock, PhysicalKey.ShiftLeft);
        Assert.False(input.Has(Key.LeftShift));
        Assert.False(input.Player.Alt);
        input.Dispose();
    }

    [Fact]
    public void The_mouse_points_until_a_direction_key_is_pressed_and_its_buttons_fire()
    {
        var input = new InputHandler(gamepad: false);
        input.PointAt((300, 200));
        Assert.Equal((300.0, 200.0), input.Player.Point);
        input.OnKeyDown(Key.Left);
        Assert.Null(input.Player.Point);
        input.OnKeyUp(Key.Left);
        Assert.Null(input.Player.Point);
        input.MouseLeft = true;
        Assert.True(input.Player.Fire);
        input.MouseLeft = false;
        input.MouseRight = true;
        Assert.True(input.Player.Alt);
        input.Clear();
        Assert.False(input.Player.Alt);
        input.Dispose();
    }

    [Fact]
    public void Through_the_keys_up_lifts_the_lance_the_catapult_and_the_sword()
    {
        var input = new InputHandler(gamepad: false);
        var g = Engine();
        g.Go(Realm.Osric);
        g.HoldTournament(forLand: false);
        var j = g.Joust!;
        double y = j.AimY;
        input.OnKeyDown(Key.Up);
        g.Tick(input.Player);
        Assert.True(j.AimY < y);
        input.OnKeyUp(Key.Up);

        var h = Engine();
        h.Go(Realm.Edmund);
        h.World.Me.Army = new Troops(50, 5, 1);
        h.Conquer(Realm.Gloucester);
        var s = h.Siege!;
        double angle = s.Angle, power = s.Power;
        input.OnKeyDown(Key.W);
        h.Tick(input.Player);
        Assert.True(s.Angle > angle);
        input.OnKeyUp(Key.W);
        input.OnKeyDown(Key.Right);
        h.Tick(input.Player);
        Assert.True(s.Power > power);
        input.OnKeyUp(Key.Right);
        input.OnKeyDown(Key.D2);
        h.Tick(input.Player);
        Assert.Equal(Ammo.GreekFire, s.Ammo);
        input.OnKeyUp(Key.D2);
        input.OnKeyDown(Key.Space);
        h.Tick(input.Player);
        Assert.Equal(SiegePhase.Flight, s.Phase);
        input.OnKeyUp(Key.Space);

        var k = Engine();
        k.Go(Realm.Godwin);
        k.StartRaid(Realm.Raoul);
        var r = k.Raid!;
        for (int i = 0; i < 60 * 6 && !(r.Phase == RaidPhase.Fight && r.Hero.Act == Act.Guard); i++) k.Tick(default);
        input.OnKeyDown(Key.Up);
        k.Tick(input.Player);
        Assert.Equal((Act.Cut, Line.High), (r.Hero.Act, r.Hero.Line));
        input.OnKeyUp(Key.Up);
        for (int i = 0; i < 60; i++) k.Tick(input.Player);
        if (r.Hero.Act == Act.Guard)
        {
            input.OnKeyDown(Key.X);
            k.Tick(input.Player);
            Assert.Equal(Act.Parry, r.Hero.Act);
        }
        input.Dispose();
    }

    [Fact]
    public void Every_sound_has_its_file()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        foreach (var s in Enum.GetValues<Sfx>())
            if (s != Sfx.MusicStop) Assert.True(File.Exists(Path.Combine(folder, s + ".wav")), $"no sound for {s}");
    }
}
