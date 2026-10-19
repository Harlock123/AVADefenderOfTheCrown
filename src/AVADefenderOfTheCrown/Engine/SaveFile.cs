using System.Globalization;
using System.Text;

namespace AVADefenderOfTheCrown.Engine;

/// <summary>
/// The campaign saved at the start of each of the player's turns, so a game can be continued: a small text file of
/// everything in the <see cref="World"/>, kept next to the high-score table.
/// </summary>
public static class SaveFile
{
    private const string Magic = "AVADEFENDEROFTHECROWN 1";

    public static string PathIn(string folder) => Path.Combine(folder, "campaign.sav");

    public static bool Exists(string folder) => File.Exists(PathIn(folder));

    public static void Delete(string folder)
    {
        try { File.Delete(PathIn(folder)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static string Write(World w)
    {
        var b = new StringBuilder();
        b.AppendLine(Magic);
        b.AppendLine(Line("game", w.Player, w.Turn, w.GilCalls, w.GilAid ? 1 : 0, (int)w.Lady, w.LadyHeldBy, w.LadyTurn, w.Renown,
            w.NormansBeaten, w.Tournaments, w.Raids, w.Won ? 1 : 0, w.Lost ? 1 : 0));
        b.AppendLine("dice " + w.Dice.State.ToString(CultureInfo.InvariantCulture));
        b.AppendLine(Line("owners", w.Owner));
        foreach (var l in w.Lords)
            b.AppendLine(Line("lord", l.Id, l.Gold, l.Army.Soldiers, l.Army.Knights, l.Army.Catapults, l.At, l.Garrison.Soldiers, l.Garrison.Knights, l.Castle));
        return b.ToString();
    }

    private static string Line(string key, params int[] values) => key + " " + string.Join(" ", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    public static World? Read(string text)
    {
        try
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length < 4 || lines[0] != Magic) return null;
            var w = new World();
            var lords = new List<Lord>();
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(' ');
                if (parts[0] == "dice") { w.Dice.State = ulong.Parse(parts[1], CultureInfo.InvariantCulture); continue; }
                int[] v = parts.Skip(1).Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
                switch (parts[0])
                {
                    case "game":
                        (w.Player, w.Turn, w.GilCalls, w.GilAid, w.Lady, w.LadyHeldBy, w.LadyTurn) = (v[0], v[1], v[2], v[3] != 0, (LadyState)v[4], v[5], v[6]);
                        (w.Renown, w.NormansBeaten, w.Tournaments, w.Raids, w.Won, w.Lost) = (v[7], v[8], v[9], v[10], v[11] != 0, v[12] != 0);
                        break;
                    case "owners":
                        if (v.Length != Realm.Count) return null;
                        w.Owner = v;
                        break;
                    case "lord":
                        lords.Add(new Lord { Id = v[0], Gold = v[1], Army = new Troops(v[2], v[3], v[4]), At = v[5], Garrison = new Troops(v[6], v[7]), Castle = v[8] });
                        break;
                }
            }
            if (lords.Count != Realm.LordCount || w.Player is < 0 or >= Realm.SaxonCount) return null;
            w.Lords = [.. lords.OrderBy(l => l.Id)];
            return w;
        }
        catch (Exception e) when (e is FormatException or OverflowException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    public static void Save(World w, string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string path = PathIn(folder), temp = path + ".tmp";
            File.WriteAllText(temp, Write(w));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A read-only home folder just means the campaign can't be continued.
        }
    }

    public static World? Load(string folder)
    {
        try
        {
            return File.Exists(PathIn(folder)) ? Read(File.ReadAllText(PathIn(folder))) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
