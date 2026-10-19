using AVADefenderOfTheCrown.Engine;

namespace AVADefenderOfTheCrown.Tests;

/// <summary>
/// How the computer player gets on (set CROWN_PROBE to run it): whole campaigns played through the engine, scenes and
/// all, as the demonstration plays them. CROWN_GAMES sets how many (8), CROWN_SEASONS the longest one (80).
/// </summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_player_does()
    {
        if (Environment.GetEnvironmentVariable("CROWN_PROBE") is null) return;
        int games = int.TryParse(Environment.GetEnvironmentVariable("CROWN_GAMES"), out int n) ? n : 8;
        int seasons = int.TryParse(Environment.GetEnvironmentVariable("CROWN_SEASONS"), out int s) ? s : 80;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int won = 0, lost = 0;
        for (int game = 0; game < games; game++)
        {
            string folder = Path.Combine(Path.GetTempPath(), "avadefenderofthecrown-probe-" + Guid.NewGuid().ToString("N"));
            var g = new GameEngine(HighScoreTable.Load(folder), folder, seed: 100 + game) { Silent = true };
            g.Go(game % Realm.SaxonCount, seed: 1000 + game * 37);
            var scenes = new Dictionary<string, int>();
            var last = Scene.Map;
            string trace = "";
            int lastTurn = 0;
            for (int t = 0; t < 60 * 60 * 120 && g.State == GameState.Playing && g.World.Turn <= seasons; t++)
            {
                if (g.Scene == Scene.Card) g.DismissCard();
                else g.Tick(g.Pilot.Drive(g, Field.Step));
                if (g.Scene != last && g.Scene is not (Scene.Map or Scene.Card)) scenes[g.Scene.ToString()] = scenes.GetValueOrDefault(g.Scene.ToString()) + 1;
                last = g.Scene;
                var w = g.World;
                if (w.Turn != lastTurn && w.Turn % 8 == 0)
                {
                    lastTurn = w.Turn;
                    trace += $" T{w.Turn}:{w.LandCount(w.Player)}sh/{w.Me.Gold}g/{w.Me.Army.Strength}a/{w.Me.Garrison.Strength}g N[{string.Join(",", w.Lords.Where(l => Realm.IsNorman(l.Id)).Select(l => l.Alive ? $"{w.LandCount(l.Id)}:{l.Army.Strength}/{l.Garrison.Strength}" : "x"))}]";
                }
            }
            var wd = g.World;
            if (wd.Won) won++;
            if (wd.Lost) lost++;
            output.WriteLine($"game {game} as {Realm.Lords[wd.Player].Short}: {(wd.Won ? "WON" : wd.Lost ? "LOST" : "undecided")} season {wd.Turn}, {wd.LandCount(wd.Player)} shires, {wd.NormansBeaten} normans, score {Sim.Score(wd)}, lady {wd.Lady}; scenes {string.Join(",", scenes.Select(kv => kv.Key + "=" + kv.Value))}");
            output.WriteLine("   " + trace);
        }
        output.WriteLine($"won {won}, lost {lost} of {games}; {sw.ElapsedMilliseconds} ms");
    }
}
