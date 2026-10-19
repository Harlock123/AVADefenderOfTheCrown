using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AVADefenderOfTheCrown.Engine;

namespace AVADefenderOfTheCrown.Views;

/// <summary>
/// The painted map of England, made once: the sea with its engraved ripples and waves, the land washed in ochre and
/// green with the hills shaded from the north-west, Wales and Scotland greyed off, the rivers, the woods and the
/// fells drawn as little pictures, the coast inked, and the shires' borders dotted in. Also which shire every pixel
/// belongs to (for the mouse), and a wash of each owner's colour laid over the shires, redrawn when they change.
/// </summary>
public sealed class MapArt
{
    public const int W = 624, H = 768;
    public const short Sea = -1, Beyond = -2;

    private static MapArt? _shared;
    public static MapArt Shared => _shared ??= new MapArt();

    /// <summary>Which shire each pixel is in (or <see cref="Sea"/>, or <see cref="Beyond"/> for Wales and Scotland).</summary>
    public readonly short[] Id = new short[W * H];
    /// <summary>How far each land pixel is from its shire's border, and each sea pixel from the coast.</summary>
    public readonly float[] Edge = new float[W * H];
    /// <summary>Where each shire's name goes, and its middle.</summary>
    public readonly (double X, double Y)[] Middle = new (double, double)[Realm.Count];

    public WriteableBitmap Base { get; }
    public WriteableBitmap Wash { get; }
    private string _washKey = "";

    private MapArt()
    {
        Classify();
        Distances();
        for (int s = 0; s < Realm.Count; s++)
        {
            double sx = 0, sy = 0;
            int n = 0;
            for (int i = 0; i < Id.Length; i++)
                if (Id[i] == s && Edge[i] > 6) { sx += i % W; sy += i / W; n++; }
            Middle[s] = n > 0 ? (sx / n, sy / n) : Realm.Where(s);
        }
        var p = Paint();
        Base = ToBitmap(p);
        Wash = new WriteableBitmap(new PixelSize(W, H), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
    }

    public short ShireAt(double x, double y)
    {
        int ix = (int)x, iy = (int)y;
        return ix < 0 || iy < 0 || ix >= W || iy >= H ? Sea : Id[iy * W + ix];
    }

    public static (double Lon, double Lat) Unproject(double x, double y) => ((x - Realm.MapLeft) / 75.5 - 5.85, 55.95 - (y - Realm.MapTop) / 123.0);

    // ───────────────────────────── which pixel is which ─────────────────────────────

    private static List<(double X, double Y)> Ring((double Lon, double Lat)[] pts) => pts.Select(p => Realm.Project(p.Lon, p.Lat)).ToList();

    /// <summary>Fills a polygon a row at a time (crossings sorted), calling back for each pixel inside.</summary>
    private static void Scan(List<(double X, double Y)> ring, Action<int, int> inside)
    {
        var xs = new List<double>();
        for (int y = 0; y < H; y++)
        {
            double yc = y + 0.5;
            xs.Clear();
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                if (a.Y <= yc && b.Y > yc || b.Y <= yc && a.Y > yc) xs.Add(a.X + (yc - a.Y) / (b.Y - a.Y) * (b.X - a.X));
            }
            xs.Sort();
            for (int k = 0; k + 1 < xs.Count; k += 2)
                for (int x = Math.Max(0, (int)Math.Ceiling(xs[k] - 0.5)); x <= Math.Min(W - 1, (int)Math.Floor(xs[k + 1] - 0.5)); x++)
                    inside(x, y);
        }
    }

    /// <summary>Land or sea, then each land pixel to the nearest shire's seat (by a weighted, wobbly distance).</summary>
    private void Classify()
    {
        Array.Fill(Id, Sea);
        var land = new bool[W * H];
        Scan(Ring(Realm.Coast), (x, y) => land[y * W + x] = true);
        foreach (var isle in Realm.Islands) Scan(Ring(isle), (x, y) => land[y * W + x] = true);
        var seeds = new List<(double X, double Y, double Wt, short Id)>();
        for (int s = 0; s < Realm.Count; s++)
        {
            var (x, y) = Realm.Where(s);
            seeds.Add((x, y, Realm.Shires[s].Weight, (short)s));
        }
        foreach (var b in Realm.Beyond)
        {
            var (x, y) = Realm.Project(b.Lon, b.Lat);
            seeds.Add((x, y, b.Weight, Beyond));
        }
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (!land[y * W + x]) continue;
            // The borders wander like old county boundaries.
            double wx = x + 22 * (Pix.Fbm(x, y, 70, 11, 3) - 0.5), wy = y + 22 * (Pix.Fbm(x, y, 70, 23, 3) - 0.5);
            double best = double.MaxValue;
            short id = Beyond;
            foreach (var s in seeds)
            {
                double d = Math.Sqrt((wx - s.X) * (wx - s.X) + (wy - s.Y) * (wy - s.Y)) / s.Wt;
                if (d < best) { best = d; id = s.Id; }
            }
            Id[y * W + x] = id;
        }
        // Man is nobody's (it lies off the Lancaster shore), nor is any scrap cut off from its shire.
        var (mx, my) = Realm.Project(-4.6, 54.22);
        Flood((int)mx, (int)my, Beyond);
    }

    private void Flood(int x0, int y0, short to)
    {
        if (x0 < 0 || y0 < 0 || x0 >= W || y0 >= H) return;
        short from = Id[y0 * W + x0];
        if (from == Sea || from == to) return;
        var q = new Queue<int>([y0 * W + x0]);
        Id[y0 * W + x0] = to;
        while (q.Count > 0)
        {
            int i = q.Dequeue(), x = i % W, y = i / W;
            foreach (int j in new[] { x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < H - 1 ? i + W : -1 })
                if (j >= 0 && Id[j] == from) { Id[j] = to; q.Enqueue(j); }
        }
    }

    /// <summary>Distances: each pixel to the nearest pixel of another kind (a chamfer sweep, both ways).</summary>
    private void Distances()
    {
        const float big = 99;
        for (int i = 0; i < Id.Length; i++)
        {
            int x = i % W, y = i / W;
            bool edge = false;
            for (int d = 0; d < 4 && !edge; d++)
            {
                int u = x + (d == 0 ? 1 : d == 1 ? -1 : 0), v = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (u < 0 || v < 0 || u >= W || v >= H) continue;
                short o = Id[v * W + u];
                edge = Id[i] == Sea ? o != Sea : o != Id[i];
            }
            Edge[i] = edge ? 0 : big;
        }
        const float s2 = 1.4142f;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (x > 0 && Same(i, i - 1)) Edge[i] = Math.Min(Edge[i], Edge[i - 1] + 1);
                if (y > 0 && Same(i, i - W)) Edge[i] = Math.Min(Edge[i], Edge[i - W] + 1);
                if (x > 0 && y > 0 && Same(i, i - W - 1)) Edge[i] = Math.Min(Edge[i], Edge[i - W - 1] + s2);
                if (x < W - 1 && y > 0 && Same(i, i - W + 1)) Edge[i] = Math.Min(Edge[i], Edge[i - W + 1] + s2);
            }
            for (int y = H - 1; y >= 0; y--)
            for (int x = W - 1; x >= 0; x--)
            {
                int i = y * W + x;
                if (x < W - 1 && Same(i, i + 1)) Edge[i] = Math.Min(Edge[i], Edge[i + 1] + 1);
                if (y < H - 1 && Same(i, i + W)) Edge[i] = Math.Min(Edge[i], Edge[i + W] + 1);
                if (x < W - 1 && y < H - 1 && Same(i, i + W + 1)) Edge[i] = Math.Min(Edge[i], Edge[i + W + 1] + s2);
                if (x > 0 && y < H - 1 && Same(i, i + W - 1)) Edge[i] = Math.Min(Edge[i], Edge[i + W - 1] + s2);
            }
        }
    }

    /// <summary>Two pixels measured together: both sea, or both of the same shire (for the borders' distance).</summary>
    private bool Same(int a, int b) => Id[a] == Sea ? Id[b] == Sea : Id[b] == Id[a];

    // ───────────────────────────── painting ─────────────────────────────

    private static readonly (double Lon, double Lat)[][] Rivers =
    [
        [(-2.0, 51.68), (-1.6, 51.71), (-1.27, 51.76), (-1.1, 51.6), (-0.9, 51.52), (-0.6, 51.49), (-0.3, 51.47), (0.0, 51.5), (0.35, 51.47), (0.62, 51.49)],
        [(-3.55, 52.45), (-3.1, 52.66), (-2.75, 52.71), (-2.42, 52.6), (-2.3, 52.3), (-2.2, 52.05), (-2.3, 51.85), (-2.5, 51.7), (-2.66, 51.6)],
        [(-2.15, 53.0), (-1.9, 52.8), (-1.5, 52.86), (-1.2, 52.93), (-0.95, 53.06), (-0.82, 53.27), (-0.76, 53.5), (-0.7, 53.7)],
        [(-1.85, 54.27), (-1.35, 54.02), (-1.08, 53.96), (-0.9, 53.76), (-0.7, 53.7), (-0.3, 53.72), (0.0, 53.66)],
        [(-2.45, 54.95), (-1.95, 54.97), (-1.6, 54.97), (-1.43, 55.0)],
        [(-2.2, 53.43), (-2.6, 53.4), (-2.95, 53.38)],
        [(-1.7, 52.25), (-1.4, 52.45), (-1.05, 52.6), (-0.6, 52.45), (-0.25, 52.4), (0.2, 52.55), (0.38, 52.78)],
        [(-2.0, 50.98), (-1.82, 50.9), (-1.75, 50.74)],
    ];

    /// <summary>Woods: where, how wide (in degrees), and how many trees.</summary>
    private static readonly (double Lon, double Lat, double R, int N)[] Woods =
    [
        (-1.07, 53.17, 0.2, 46), (0.3, 51.05, 0.32, 20), (-1.6, 50.88, 0.14, 12), (-2.55, 51.8, 0.11, 8), (-2.4, 55.2, 0.22, 12),
        (-1.75, 52.3, 0.18, 10), (0.1, 51.7, 0.1, 6), (-1.15, 54.15, 0.13, 7), (-2.75, 53.2, 0.12, 5), (0.7, 52.4, 0.15, 6),
    ];

    /// <summary>Uplands: where, how wide, how many fells to draw, and how high the land rises.</summary>
    private static readonly (double Lon, double Lat, double R, int N, double Rise)[] Hills =
    [
        (-2.15, 54.25, 0.38, 13, 1), (-3.08, 54.48, 0.26, 9, 1.2), (-1.85, 53.35, 0.2, 5, 0.8), (-2.2, 55.38, 0.22, 6, 0.9),
        (-0.92, 54.38, 0.2, 5, 0.7), (-3.9, 50.6, 0.15, 4, 0.8), (-3.65, 51.13, 0.12, 3, 0.6), (-3.7, 52.6, 0.6, 18, 1.1),
        (-3.6, 51.8, 0.3, 6, 0.9), (-3.8, 55.45, 0.55, 12, 1), (-2.35, 52.1, 0.08, 2, 0.5), (-2.0, 51.82, 0.18, 2, 0.4),
        (-4.6, 50.55, 0.15, 2, 0.5), (-2.8, 52.5, 0.12, 2, 0.5),
    ];

    private double Height(double x, double y)
    {
        var (lon, lat) = Unproject(x, y);
        double h = 0;
        foreach (var hl in Hills)
        {
            double dx = (lon - hl.Lon) * 0.62, dy = lat - hl.Lat, d = Math.Sqrt(dx * dx + dy * dy) / (hl.R * 1.4);
            if (d < 1) h += hl.Rise * (1 - d * d) * (1 - d * d);
        }
        return h * (0.55 + 0.9 * Pix.Fbm(x, y, 34, 5, 4)) + 0.25 * Pix.Fbm(x, y, 90, 7, 3);
    }

    private Pix Paint()
    {
        var p = new Pix(W, H);
        var height = new double[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
            if (Id[y * W + x] != Sea) height[y * W + x] = Height(x, y);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            int i = y * W + x;
            double grain = Pix.Fbm(x, y, 6, 3, 2) - 0.5;
            double paper = Pix.Fbm(x, y, 120, 9, 3);
            if (Id[i] == Sea)
            {
                // The sea: deep teal, lighter towards the coast, with the old engravers' ripple lines.
                double d = Edge[i];
                uint deep = Pix.Mix(0xFF2E5560, 0xFF3D6670, paper), shallow = 0xFF6E9A94;
                uint c = Pix.Mix(deep, shallow, Math.Clamp(1 - d / 26, 0, 1) * 0.8);
                if (d is > 3.2 and < 4.6 || d is > 8.4 and < 9.6 || d is > 14.5 and < 15.4) c = Pix.Mix(c, 0xFFB8D0C4, d < 5 ? 0.55 : d < 10 ? 0.38 : 0.22);
                c = Pix.Scale(c, 1 + grain * 0.18);
                p.P[i] = c;
                continue;
            }
            // The land: ochre and green, shaded by the lie of it.
            double h = height[i];
            double gx = (x > 0 ? height[i] - height[i - 1] : 0) + (y > 0 ? height[i] - height[i - W] : 0);
            double light = Math.Clamp(1 + gx * 9, 0.62, 1.35);
            double g = Pix.Fbm(x, y, 46, 31, 3);
            uint low = Pix.Mix(0xFF9DAA66, 0xFFB7B276, g), up = Pix.Mix(0xFF9C8A62, 0xFF7E7A5A, g);
            uint land = Pix.Mix(low, up, Math.Clamp(h * 0.9, 0, 1));
            if (Id[i] == Beyond) land = Pix.Mix(Pix.Mix(land, 0xFFA49A84, 0.55), 0xFF8C8270, 0.2);
            land = Pix.Scale(land, light * (1 + grain * 0.14));
            p.P[i] = land;
        }
        // The shores: a pale fringe inside the coast, then the ink.
        for (int y = 1; y < H - 1; y++)
        for (int x = 1; x < W - 1; x++)
        {
            int i = y * W + x;
            if (Id[i] == Sea) continue;
            int seaNear = 0;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int u = x + dx, v = y + dy;
                if (u >= 0 && v >= 0 && u < W && v < H && Id[v * W + u] == Sea) seaNear++;
            }
            if (seaNear > 0) p.P[i] = Pix.Mix(p.P[i], 0xFFE0D2A0, 0.35);
            if (Id[i - 1] == Sea || Id[i + 1] == Sea || Id[i - W] == Sea || Id[i + W] == Sea) p.P[i] = 0xFF2E2418;
        }
        foreach (var r in Rivers) River(p, r);
        // The shires' borders, dotted; the line round Wales and Scotland, solid and faint.
        for (int y = 1; y < H - 1; y++)
        for (int x = 1; x < W - 1; x++)
        {
            int i = y * W + x;
            short a = Id[i];
            if (a == Sea) continue;
            short r = Id[i + 1], d = Id[i + W];
            bool border = r != Sea && r != a || d != Sea && d != a;
            if (!border) continue;
            bool beyond = a == Beyond || r == Beyond || d == Beyond;
            if (beyond) { p.Set(x, y, 0x90503A28); continue; }
            if ((x + y) % 7 < 4) { p.Set(x, y, 0xFF7A2618); p.Set(x + 1, y, 0x807A2618); }
        }
        var dice = new Dice(1149);
        foreach (var hl in Hills)
            for (int k = 0; k < hl.N; k++)
            {
                double a = dice.Range(0, Math.Tau), rr = Math.Sqrt(dice.Next()) * hl.R;
                var (x, y) = Realm.Project(hl.Lon + rr * Math.Cos(a) / 0.62, hl.Lat + rr * Math.Sin(a) * 0.75);
                int id = Id[Math.Clamp((int)y, 0, H - 1) * W + Math.Clamp((int)x, 0, W - 1)];
                if (id == Sea) continue;
                Fell(p, x, y, dice.Range(6, 10), id == Beyond);
            }
        var trees = new List<(double X, double Y, double S)>();
        foreach (var wd in Woods)
            for (int k = 0; k < wd.N; k++)
            {
                double a = dice.Range(0, Math.Tau), rr = Math.Sqrt(dice.Next()) * wd.R;
                var (x, y) = Realm.Project(wd.Lon + rr * Math.Cos(a) / 0.62, wd.Lat + rr * Math.Sin(a) * 0.7);
                if (Id[Math.Clamp((int)y, 0, H - 1) * W + Math.Clamp((int)x, 0, W - 1)] is Sea or Beyond) continue;
                trees.Add((x, y, dice.Range(3.2, 4.6)));
            }
        foreach (var t in trees.OrderBy(t => t.Y)) Tree(p, t.X, t.Y, t.S);
        Waves(p);
        p.Grain(0, 0, W, H, 0.05, 77);
        return p;
    }

    private static void River(Pix p, (double Lon, double Lat)[] pts)
    {
        var ring = pts.Select(q => Realm.Project(q.Lon, q.Lat)).ToList();
        for (int k = 0; k + 1 < ring.Count; k++)
        {
            var (x0, y0) = ring[k];
            var (x1, y1) = ring[k + 1];
            int n = (int)(Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) * 2);
            for (int j = 0; j <= n; j++)
            {
                double t = j / (double)Math.Max(1, n);
                double x = x0 + (x1 - x0) * t + 2.2 * Math.Sin((k * 7 + t * 9) * 1.3), y = y0 + (y1 - y0) * t + 1.6 * Math.Cos((k * 5 + t * 8) * 1.1);
                double width = 0.8 + 1.1 * (k + t) / ring.Count;
                p.Disc(x, y, width + 0.6, 0xC0304A54);
                p.Disc(x, y, width, 0xFF5E8C98);
            }
        }
    }

    /// <summary>A fell: a little rounded peak, lit on its western side, inked round.</summary>
    private static void Fell(Pix p, double cx, double cy, double s, bool faint)
    {
        uint lit = faint ? 0xFFB0A486 : 0xFFC2AE7A, dark = faint ? 0xFF7E7460 : 0xFF7C6840, ink = faint ? 0xA0503E2C : 0xFF3E2E1C;
        for (int y = (int)(cy - s); y <= (int)cy + 1; y++)
        for (int x = (int)(cx - s * 1.3); x <= (int)(cx + s * 1.3); x++)
        {
            double u = (x - cx) / (s * 1.3), v = (cy - y) / s;
            double top = 1 - u * u * 1.1;
            if (v < 0 || v > top) continue;
            bool edge = v > top - 0.16 || v < 0.12;
            p.Set(x, y, edge ? ink : u < -0.05 + 0.15 * v ? lit : dark);
        }
        // A few hatch strokes on the shaded side.
        for (int k = 1; k <= 2; k++) p.Line((int)(cx + s * 0.2 * k), (int)(cy - s * 0.25), (int)(cx + s * 0.35 * k), (int)cy, ink);
    }

    /// <summary>A tree: its shadow, its trunk, and a round crown lit from the north-west.</summary>
    private static void Tree(Pix p, double x, double y, double s)
    {
        p.Disc(x + s * 0.5, y + s * 0.2, s * 1.1, s * 0.5, 0x50302010);
        p.Fill((int)x, (int)(y - s * 0.4), 1, (int)(s * 0.8) + 1, 0xFF4A3420);
        p.Disc(x, y - s * 1.1, s + 0.6, 0xFF1E3418);
        p.Disc(x, y - s * 1.1, s, 0xFF3C6A2C);
        p.Disc(x - s * 0.3, y - s * 1.4, s * 0.5, 0xFF5E8E40);
    }

    /// <summary>Little wave strokes out at sea.</summary>
    private void Waves(Pix p)
    {
        var dice = new Dice(66);
        for (int k = 0; k < 260; k++)
        {
            int x = dice.Next(W), y = dice.Next(H);
            if (Id[y * W + x] != Sea || Edge[y * W + x] < 22) continue;
            for (int j = -5; j <= 5; j++)
            {
                double yy = y - 1.6 * Math.Cos(j / 5.0 * Math.PI) - 1;
                p.Set(x + j, (int)yy, 0xA0C8DCD0);
                p.Set(x + j + 7, (int)(y - 1.2 * Math.Cos(j / 5.0 * Math.PI)) + 3, 0x70C8DCD0);
            }
        }
    }

    // ───────────────────────────── the owners' wash ─────────────────────────────

    /// <summary>
    /// Lays each owner's colour over his shires like a watercolour wash, darker where it pools at the borders; the
    /// shires in <paramref name="lit"/> glow, and <paramref name="hover"/> brightens. Only redrawn when something changed.
    /// </summary>
    public unsafe void UpdateWash(int[] owners, Func<int, uint> colour, IReadOnlyCollection<int> lit, int hover)
    {
        string key = string.Join(",", owners) + "|" + string.Join(",", lit) + "|" + hover + "|" + string.Join(",", owners.Select(o => colour(o)));
        if (key == _washKey) return;
        _washKey = key;
        using var fb = Wash.Lock();
        uint* dst = (uint*)fb.Address;
        int stride = fb.RowBytes / 4;
        var cols = new uint[Realm.Count];
        var glow = new bool[Realm.Count];
        for (int s = 0; s < Realm.Count; s++)
        {
            cols[s] = colour(owners[s]);
            glow[s] = lit.Contains(s);
        }
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            int i = y * W + x;
            short id = Id[i];
            uint c = 0;
            if (id >= 0)
            {
                uint col = cols[id];
                double e = Edge[i];
                double a = (col >> 24) / 255.0;
                if (a > 0)
                {
                    double pool = e < 5 ? 0.5 + (5 - e) * 0.1 : 0.32 + 0.12 * Pix.Fbm(x, y, 40, id * 7 + 1, 2);
                    a *= pool;
                }
                uint rgb = col & 0xFFFFFF;
                if (glow[id])
                {
                    double pulse = e < 4 ? 0.85 : 0.22;
                    rgb = Pix.Mix(rgb | 0xFF000000, 0xFFFFF0B0, 0.6) & 0xFFFFFF;
                    a = Math.Max(a, pulse);
                }
                if (id == hover) a = Math.Min(1, a + (e < 3 ? 0.5 : 0.12));
                c = Premul(rgb, a);
            }
            dst[y * stride + x] = c;
        }
    }

    private static uint Premul(uint rgb, double a)
    {
        a = Math.Clamp(a, 0, 1);
        uint A = (uint)(a * 255);
        uint R = (uint)(((rgb >> 16) & 0xFF) * a), G = (uint)(((rgb >> 8) & 0xFF) * a), B = (uint)((rgb & 0xFF) * a);
        return A << 24 | R << 16 | G << 8 | B;
    }

    public static unsafe WriteableBitmap ToBitmap(Pix p)
    {
        var bmp = new WriteableBitmap(new PixelSize(p.W, p.H), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = bmp.Lock();
        uint* dst = (uint*)fb.Address;
        int stride = fb.RowBytes / 4;
        for (int y = 0; y < p.H; y++)
        for (int x = 0; x < p.W; x++)
        {
            uint c = p.P[y * p.W + x];
            uint a = c >> 24;
            dst[y * stride + x] = a == 255 ? c : Premul(c & 0xFFFFFF, a / 255.0);
        }
        return bmp;
    }
}
