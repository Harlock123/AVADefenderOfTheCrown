using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVADefenderOfTheCrown.Engine;
using AVADefenderOfTheCrown.Views;

namespace AVADefenderOfTheCrown.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set CROWN_SHOTS to a folder to save them
/// as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "avadefenderofthecrown-ui-" + Guid.NewGuid().ToString("N"));

    private static (Window W, GameCanvas Canvas, GameEngine G) Open()
    {
        string folder = TempFolder();
        var g = new GameEngine(HighScoreTable.Load(folder), folder, seed: 11) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("CROWN_SHOTS");
        if (string.IsNullOrEmpty(dir) || name == "-") return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVADefenderOfTheCrown-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Runs the game with the computer at the controls until <paramref name="done"/>, moving the drawing clock along.</summary>
    private static bool PlayUntil(GameEngine g, GameCanvas c, Func<GameEngine, bool> done, int maxSeconds = 120)
    {
        for (int t = 0; t < 60 * maxSeconds; t++)
        {
            if (done(g)) return true;
            if (g.Scene == Scene.Card && g.SceneTime > 0.5) g.DismissCard();
            g.Tick(g.Pilot.Drive(g, Field.Step));
            if (t % 4 == 0) c.Advance(4 / 60.0);
        }
        return false;
    }

    [AvaloniaFact]
    public void The_title()
    {
        var (w, c, g) = Open();
        g.Idle(30);
        c.Advance(2.1);
        Assert.Equal(GameState.Title, g.State);
        Shot(w, c, "title");
    }

    [AvaloniaFact]
    public void Choosing_a_knight()
    {
        var (w, c, g) = Open();
        g.NewGame();
        g.ChooseIndex = 2;
        g.Idle(5);
        Assert.Equal(GameState.Choose, g.State);
        Shot(w, c, "knights");
    }

    [AvaloniaFact]
    public void The_map_of_England()
    {
        var (w, c, g) = Open();
        g.Go(0, seed: 5);
        // A few seasons of the computer's campaign, then the orders.
        Assert.True(PlayUntil(g, c, g => g.World.Turn >= 7 && g.Scene == Scene.Map && g.Mode == MapMode.Orders, 900));
        g.Selected = Realm.York;
        c.Advance(0.5);
        Shot(w, c, "map");
    }

    [AvaloniaFact]
    public void A_joust()
    {
        var (w, c, g) = Open();
        g.Go(1, seed: 3);
        g.HoldTournament(forLand: true);
        Assert.Equal(Scene.Joust, g.Scene);
        Assert.True(PlayUntil(g, c, g => g.Joust is { Phase: JoustPhase.Charge } j && j.T > Joust.ChargeSeconds * 0.96));
        Shot(w, c, "joust");
    }

    [AvaloniaFact]
    public void A_siege()
    {
        var (w, c, g) = Open();
        g.Go(0, seed: 4);
        var me = g.World.Me;
        me.Army = new Troops(60, 12, 2);
        me.At = Realm.Wessex;
        g.Conquer(Realm.Gloucester);
        Assert.Equal(Scene.Siege, g.Scene);
        Assert.True(PlayUntil(g, c, g => g.Siege is { Hits: >= 3, Phase: SiegePhase.Flight } s && s.T > 0.55));
        Shot(w, c, "siege");
    }

    [AvaloniaFact]
    public void A_raid()
    {
        var (w, c, g) = Open();
        g.Go(2, seed: 6);
        g.StartRaid(Realm.Hugh);
        Assert.Equal(Scene.Raid, g.Scene);
        Assert.True(PlayUntil(g, c, g => g.Raid is { Phase: RaidPhase.Fight, Current: 1 } r && r.Foe is { Act: Act.Wind } f && f.T > 0.4));
        Shot(w, c, "raid");
    }

    [AvaloniaFact]
    public void A_battle()
    {
        var (w, c, g) = Open();
        g.Go(0, seed: 8);
        g.World.Me.Army = new Troops(44, 8, 1);
        var hugh = g.World.Lords[Realm.Hugh];
        hugh.At = Realm.Sussex;
        hugh.Army = new Troops(40, 7, 1);
        g.Conquer(Realm.Sussex);
        Assert.Equal(Scene.Battle, g.Scene);
        g.Command(Tactic.FierceAttack);
        Assert.True(PlayUntil(g, c, g => g.Battle is { Phase: BattlePhase.Clash } b && b.T > Battle.ClashSeconds * 0.45));
        Shot(w, c, "battle");
    }

    [AvaloniaFact]
    public void An_event_card()
    {
        var (w, c, g) = Open();
        g.Go(3, seed: 9);
        g.Tell(new Card("A LADY CARRIED OFF",
            $"{Realm.Lords[Realm.Raoul].Name} has carried off {Realm.LadyName}, daughter of the late Saxon earl of Warwick, and holds her in Mortaine Tower. A daring raid might free her.",
            Realm.Raoul, CardKind.Story, Realm.Lincoln));
        c.Advance(0.3);
        Assert.Equal(Scene.Card, g.Scene);
        Shot(w, c, "card");
    }

    [AvaloniaFact]
    public void The_high_scores()
    {
        var (w, c, g) = Open();
        g.HighScores.Insert("AVA", 9120, 34);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + GameEngine.AttractSeconds)) + 30);
        for (int i = 0; i < 60 * 300 && g.State != GameState.HighScores; i++) g.Idle(1);
        Assert.Equal(GameState.HighScores, g.State);
        c.Advance(0.2);
        Shot(w, c, "high-scores");
    }

    // ───────────────────────────── the mouse ─────────────────────────────

    private static Avalonia.Point Middle(int shire) => new(MapArt.Shared.Middle[shire].X, MapArt.Shared.Middle[shire].Y);

    [AvaloniaFact]
    public void The_mouse_chooses_a_knight_gives_orders_and_picks_a_shire_on_the_map()
    {
        var (w, c, g) = Open();
        Shot(w, c, "-");
        c.Click(new Avalonia.Point(512, 420));
        Assert.Equal(GameState.Choose, g.State);
        Shot(w, c, "-");
        c.Click(GameCanvas.KnightCard(3).Center);
        Assert.Equal(Realm.Leofric, g.World.Player);
        Shot(w, c, "-");
        c.Click(new Avalonia.Point(300, 300));
        Assert.Equal(Scene.Map, g.Scene);
        Shot(w, c, "-");
        // The shire under the mouse is the one clicked.
        Assert.Equal(Realm.York, c.ShireAt(Middle(Realm.York)));
        Assert.Equal(-1, c.ShireAt(new Avalonia.Point(40, 600)));
        c.Click(Middle(Realm.York));
        Assert.Equal(Realm.York, g.Selected);
        c.Click(GameCanvas.OrderRect((int)Order.Conquer).Center);
        Assert.Equal(MapMode.Conquer, g.Mode);
        Shot(w, c, "-");
        c.Click(Middle(Realm.Essex), right: true);
        Assert.Equal(MapMode.Orders, g.Mode);
        Shot(w, c, "-");
        c.Click(GameCanvas.OrderRect((int)Order.Conquer).Center);
        Assert.Equal(MapMode.Conquer, g.Mode);
        Shot(w, c, "-");
        c.Click(Middle(Realm.Lincoln));
        Assert.Equal(Scene.Map, g.Scene);
        Assert.Equal("YOU NEED A CATAPULT TO BESIEGE A CASTLE", g.Message);
        c.Click(Middle(Realm.Warwick));
        Assert.Equal(Scene.Battle, g.Scene);
        Shot(w, c, "-");
        c.Click(GameCanvas.TacticRect((int)Tactic.FierceAttack).Center);
        Assert.Equal(Tactic.FierceAttack, g.Battle!.Last[0]);
        Assert.Equal(BattlePhase.Clash, g.Battle.Phase);
    }

    [AvaloniaFact]
    public void The_mouse_buys_an_army_and_fights_with_the_sword_and_the_catapult()
    {
        var (w, c, g) = Open();
        g.Go(Realm.Godwin, seed: 2);
        g.World.Me.Gold = 30;
        Shot(w, c, "-");
        c.Click(GameCanvas.OrderRect((int)Order.Buy).Center);
        Assert.Equal(MapMode.Buy, g.Mode);
        Shot(w, c, "-");
        c.Click(new Avalonia.Point(GameCanvas.PanelX + 341, 356));
        c.Click(new Avalonia.Point(GameCanvas.PanelX + 341, 356));
        Assert.Equal(2, g.BuySoldiers);
        Shot(w, c, "-");
        c.Click(new Avalonia.Point(GameCanvas.PanelX + 120, 553));
        Assert.Equal(28, g.World.Me.Gold);
        Assert.Equal(MapMode.Orders, g.Mode);
        g.StartRaid(Realm.Hugh);
        for (int i = 0; i < 60 * 6 && !(g.Raid!.Phase == RaidPhase.Fight && g.Raid.Hero.Act == Act.Guard && g.Raid.Foe!.Act == Act.Guard); i++)
        {
            g.Tick(default);
            if (g.Raid.Foe is { } f) f.NextAttack = 10;
        }
        Shot(w, c, "-");
        c.Click(GameCanvas.SwordRect(0).Center);
        Assert.Equal((Act.Cut, Line.High), (g.Raid!.Hero.Act, g.Raid.Hero.Line));
        for (int i = 0; i < 40; i++) g.Tick(default);
        c.Click(new Avalonia.Point(600, 400), right: true);
        Assert.Equal(Act.Parry, g.Raid.Hero.Act);

        var (w2, c2, h) = Open();
        h.Go(Realm.Edmund, seed: 3);
        h.World.Me.Army = new Troops(40, 4, 1);
        h.Conquer(Realm.Gloucester);
        Shot(w2, c2, "-");
        c2.Click(GameCanvas.AmmoRect(2).Center);
        Assert.Equal(Ammo.Disease, h.Siege!.Ammo);
        // A click on the field aims there and looses.
        c2.Click(new Avalonia.Point(Siege.LaunchX + 300, Siege.LaunchY - 300));
        h.Tick(c2.Input.Player);
        Assert.Equal(SiegePhase.Flight, h.Siege.Phase);
        Assert.Equal(45, h.Siege.FlightAngle, 1);
        c2.Input.MouseLeft = false;
        for (int i = 0; i < 600 && h.Siege.Phase != SiegePhase.Aim; i++) h.Tick(default);
        Shot(w2, c2, "-");
        c2.Click(GameCanvas.StormRect.Center);
        Assert.True(h.Siege.Over);
    }

    [AvaloniaFact]
    public void P_pauses_M_mutes_and_C_on_the_title_continues()
    {
        var (w, c, g) = Open();
        g.Go(Realm.Edmund, seed: 2);
        g.DoOrder(Order.Pass);
        while (g.Scene == Scene.Card) g.DismissCard();
        c.PressKey(Avalonia.Input.Key.P);
        Assert.Equal(GameState.Paused, g.State);
        Shot(w, c, "-");
        c.PressKey(Avalonia.Input.Key.Escape);
        Assert.Equal(GameState.Playing, g.State);
        c.PressKey(Avalonia.Input.Key.M);
        Assert.True(c.Sound.IsMuted);
        g.ReturnToTitle();
        Shot(w, c, "-");
        c.PressKey(Avalonia.Input.Key.C);
        Assert.Equal(GameState.Playing, g.State);
        Assert.Equal(2, g.World.Turn);
    }
}
