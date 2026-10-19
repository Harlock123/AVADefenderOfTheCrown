namespace AVADefenderOfTheCrown.Engine;

public enum Rating { Weak = 1, Average = 2, Strong = 3 }

public enum Side { Saxon, Norman }

/// <summary>The figure on a lord's shield.</summary>
public enum Charge { Chevron, Cross, Saltire, Bend, Pale, Quarterly, Roundels, Mullets, Arrow }

/// <summary>A lord of the realm: who he is, how good he is, where his castle stands, and his arms.</summary>
public sealed record LordInfo(
    string Name, string Short, Side Side, Rating Leadership, Rating Jousting, Rating Swordplay,
    int Home, string CastleName, uint Field, uint Tincture, Charge Charge, string Motto);

/// <summary>A shire of England: its name, what it pays its lord each season, and where it lies on the map.</summary>
public sealed record Shire(string Name, int Income, double Lon, double Lat, double Weight = 1);

/// <summary>
/// England after the king's death: the eighteen shires, which border which, and the lords who fight over them.
/// None of it changes during a game (that's all in the <see cref="World"/>).
/// </summary>
public static class Realm
{
    public const int Northumbria = 0, Cumbria = 1, York = 2, Lancaster = 3, Lincoln = 4, Chester = 5, Derby = 6, Sherwood = 7,
        Norfolk = 8, Warwick = 9, Hereford = 10, Gloucester = 11, Oxford = 12, Essex = 13, Kent = 14, Sussex = 15, Wessex = 16, Cornwall = 17;

    /// <summary>Owners that aren't lords: nobody (the shire's own vassals hold it) and the outlaws of Sherwood.</summary>
    public const int Nobody = -1, Outlaws = -2;

    public const int Edmund = 0, Osric = 1, Godwin = 2, Leofric = 3, Hugh = 4, Raoul = 5, Guy = 6;
    public const int LordCount = 7, SaxonCount = 4;

    public static readonly Shire[] Shires =
    [
        new("Northumbria", 5, -1.95, 55.18, 1.1),
        new("Cumbria", 4, -2.95, 54.55),
        new("York", 8, -1.15, 54.05, 1.25),
        new("Lancaster", 4, -2.6, 53.92, 0.95),
        new("Lincoln", 6, -0.25, 53.2, 1.05),
        new("Chester", 5, -2.72, 53.1, 0.9),
        new("Derby", 4, -1.72, 53.12, 0.85),
        new("Sherwood", 3, -1.05, 53.15, 0.62),
        new("Norfolk", 7, 0.95, 52.62, 1.05),
        new("Warwick", 6, -1.45, 52.38, 1.1),
        new("Hereford", 4, -2.75, 52.1, 0.85),
        new("Gloucester", 6, -2.15, 51.7, 0.9),
        new("Oxford", 6, -1.1, 51.78, 0.95),
        new("Essex", 6, 0.55, 51.88, 0.95),
        new("Kent", 7, 0.8, 51.2, 0.9),
        new("Sussex", 5, -0.3, 50.95, 0.95),
        new("Wessex", 7, -1.95, 51.0, 1.15),
        new("Cornwall", 5, -4.3, 50.5, 1.1),
    ];

    public static int Count => Shires.Length;

    private static readonly int[][] Borders =
    [
        [Cumbria, York],
        [Northumbria, York, Lancaster],
        [Northumbria, Cumbria, Lancaster, Derby, Sherwood, Lincoln],
        [Cumbria, York, Chester, Derby],
        [York, Sherwood, Warwick, Norfolk],
        [Lancaster, Derby, Warwick, Hereford],
        [York, Lancaster, Chester, Sherwood, Warwick],
        [York, Derby, Lincoln, Warwick],
        [Lincoln, Warwick, Essex],
        [Lincoln, Chester, Derby, Sherwood, Norfolk, Hereford, Gloucester, Oxford],
        [Chester, Warwick, Gloucester],
        [Hereford, Warwick, Oxford, Wessex],
        [Warwick, Gloucester, Wessex, Sussex, Essex],
        [Norfolk, Oxford, Kent],
        [Essex, Sussex],
        [Kent, Oxford, Wessex],
        [Sussex, Oxford, Gloucester, Cornwall],
        [Wessex],
    ];

    public static IReadOnlyList<int> Neighbours(int shire) => Borders[shire];

    public static bool Adjacent(int a, int b) => Borders[a].Contains(b);

    /// <summary>The four Saxon knights (one is you; the others ride for themselves) and the three Norman lords.</summary>
    public static readonly LordInfo[] Lords =
    [
        new("Edmund of Ashdown", "EDMUND", Side.Saxon, Rating.Strong, Rating.Weak, Rating.Average, Wessex, "Ashdown",
            0xFF1F4FA0, 0xFFE8C040, Charge.Chevron, "Men follow where I ride"),
        new("Osric the Bold", "OSRIC", Side.Saxon, Rating.Weak, Rating.Strong, Rating.Average, York, "Ravensworth",
            0xFFB8202A, 0xFFF0EAD8, Charge.Cross, "No lance unbroken"),
        new("Godwin Redmane", "GODWIN", Side.Saxon, Rating.Weak, Rating.Average, Rating.Strong, Northumbria, "Bamburgh Rock",
            0xFF1E6A3A, 0xFFE8C040, Charge.Saltire, "The blade decides"),
        new("Leofric of the Fens", "LEOFRIC", Side.Saxon, Rating.Average, Rating.Average, Rating.Average, Norfolk, "Fenwatch",
            0xFF5A2A78, 0xFFF0EAD8, Charge.Bend, "Steady as the tide"),
        new("Hugh de Valmont", "DE VALMONT", Side.Norman, Rating.Strong, Rating.Strong, Rating.Average, Kent, "Valmont Keep",
            0xFFC8A020, 0xFFA01818, Charge.Quarterly, "Par la force"),
        new("Raoul de Mortaine", "DE MORTAINE", Side.Norman, Rating.Average, Rating.Average, Rating.Strong, Lincoln, "Mortaine Tower",
            0xFF181818, 0xFFD8D0C0, Charge.Pale, "Rien ne m'arrete"),
        new("Guy de Brecy", "DE BRECY", Side.Norman, Rating.Strong, Rating.Average, Rating.Weak, Gloucester, "Brecy Castle",
            0xFF3A8AA8, 0xFFF0EAD8, Charge.Mullets, "Toujours plus"),
    ];

    /// <summary>The outlaw of Sherwood, who answers a Saxon's call a few times.</summary>
    public const string OutlawName = "Gil of the Greenwood";

    /// <summary>The Saxon lady carried off by a Norman, and the shire her late father held.</summary>
    public const string LadyName = "Lady Elfrida";
    public const int LadyLand = Warwick;

    public static readonly string[] Seasons = ["SPRING", "SUMMER", "AUTUMN", "WINTER"];

    public const int FirstYear = 1149;

    /// <summary>What a soldier, a knight and a catapult cost.</summary>
    public const int SoldierCost = 1, KnightCost = 4, CatapultCost = 12;

    public static string RatingName(Rating r) => r switch { Rating.Weak => "WEAK", Rating.Average => "AVERAGE", _ => "STRONG" };

    public static bool IsNorman(int lord) => lord >= SaxonCount && lord < LordCount;
    public static bool IsSaxon(int lord) => lord is >= 0 and < SaxonCount;

    /// <summary>A rating as a multiplier on what an army does in battle.</summary>
    public static double Lead(Rating r) => 0.8 + 0.15 * (int)r;

    /// <summary>Where a shire lies on the painted map (in screen pixels): a simple projection of its longitude and latitude.</summary>
    public static (double X, double Y) Project(double lon, double lat) => (MapLeft + (lon + 5.85) * 75.5, MapTop + (55.95 - lat) * 123.0);

    public const double MapLeft = 26, MapTop = 22;

    public static (double X, double Y) Where(int shire) => Project(Shires[shire].Lon, Shires[shire].Lat);

    /// <summary>The coast of Britain from the Scottish lowlands to Land's End and round, in (longitude, latitude).</summary>
    public static readonly (double Lon, double Lat)[] Coast =
    [
        (-2.25, 56.05), (-2.05, 55.86), (-1.85, 55.7), (-1.64, 55.6), (-1.6, 55.4), (-1.52, 55.2), (-1.42, 55.0), (-1.33, 54.8),
        (-1.17, 54.65), (-0.95, 54.58), (-0.62, 54.48), (-0.4, 54.3), (-0.2, 54.16), (-0.08, 54.11), (-0.25, 53.98), (-0.12, 53.7),
        (0.12, 53.58), (0.02, 53.5), (0.3, 53.32), (0.35, 53.1), (0.15, 52.95), (0.3, 52.82), (0.5, 52.95), (0.95, 52.96),
        (1.3, 52.92), (1.62, 52.72), (1.75, 52.47), (1.62, 52.15), (1.32, 51.95), (1.08, 51.8), (0.9, 51.62), (0.55, 51.5),
        (0.85, 51.43), (1.42, 51.37), (1.38, 51.15), (1.0, 50.95), (0.75, 50.88), (0.25, 50.74), (-0.2, 50.82), (-0.8, 50.76),
        (-1.1, 50.8), (-1.45, 50.74), (-1.95, 50.6), (-2.45, 50.56), (-2.95, 50.71), (-3.42, 50.62), (-3.52, 50.4), (-3.65, 50.23),
        (-3.95, 50.3), (-4.2, 50.35), (-4.55, 50.33), (-4.88, 50.2), (-5.2, 49.96), (-5.55, 50.05), (-5.7, 50.1), (-5.42, 50.22),
        (-5.0, 50.45), (-4.75, 50.6), (-4.55, 50.82), (-4.45, 51.0), (-4.2, 51.08), (-3.75, 51.22), (-3.3, 51.2), (-3.0, 51.32),
        (-2.75, 51.45), (-2.62, 51.62), (-2.85, 51.58), (-3.15, 51.45), (-3.6, 51.42), (-3.95, 51.6), (-4.3, 51.55), (-4.7, 51.65),
        (-5.15, 51.68), (-5.3, 51.86), (-4.85, 52.03), (-4.45, 52.15), (-4.1, 52.38), (-4.05, 52.6), (-4.15, 52.85), (-4.55, 52.78),
        (-4.75, 52.8), (-4.42, 53.05), (-4.6, 53.28), (-4.35, 53.42), (-4.05, 53.3), (-3.6, 53.3), (-3.1, 53.36), (-3.05, 53.5),
        (-2.98, 53.72), (-3.05, 53.95), (-2.82, 54.15), (-3.25, 54.12), (-3.42, 54.3), (-3.65, 54.5), (-3.45, 54.78), (-3.3, 54.92),
        (-3.6, 54.88), (-4.1, 54.78), (-4.4, 54.85), (-4.88, 54.65), (-5.15, 54.85), (-5.05, 55.05), (-4.75, 55.3), (-4.62, 55.5),
        (-4.88, 55.8), (-4.9, 56.05),
    ];

    /// <summary>The islands drawn on the map: Wight, Man, and the Scillies' rocks.</summary>
    public static readonly (double Lon, double Lat)[][] Islands =
    [
        [(-1.58, 50.68), (-1.3, 50.77), (-1.08, 50.7), (-1.28, 50.58)],
        [(-4.82, 54.07), (-4.62, 54.08), (-4.36, 54.38), (-4.6, 54.4)],
    ];

    /// <summary>Land that is no part of the game: Wales and Scotland, painted in on the map as hills.</summary>
    public static readonly (double Lon, double Lat, double Weight)[] Beyond =
    [
        (-3.85, 52.4, 1.0), (-3.55, 51.78, 0.7), (-3.85, 52.98, 0.85), (-4.95, 51.85, 0.8), (-3.6, 55.55, 1.0), (-4.6, 55.15, 1.0), (-2.55, 55.9, 0.75),
    ];
}
