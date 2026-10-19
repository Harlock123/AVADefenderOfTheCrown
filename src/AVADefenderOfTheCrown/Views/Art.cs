using Avalonia.Media.Imaging;
using AVADefenderOfTheCrown.Engine;

namespace AVADefenderOfTheCrown.Views;

/// <summary>
/// The painted backgrounds, each made once, pixel by pixel, and kept: parchment, the title's sunset over a castle,
/// the lists and their stands, the siege's field and castle, the great hall for a raid by night, and the open field
/// of a battle. What moves is drawn over them.
/// </summary>
public static class Art
{
    private const int W = 1024, H = 768;
    private static readonly Dictionary<string, WriteableBitmap> Cache = [];

    private static WriteableBitmap Get(string key, Func<Pix> paint)
    {
        if (!Cache.TryGetValue(key, out var b)) Cache[key] = b = MapArt.ToBitmap(paint());
        return b;
    }

    public static WriteableBitmap Parchment(int w, int h, int seed = 1) => Get($"parchment{w}x{h}:{seed}", () => PaintParchment(w, h, seed));
    public static WriteableBitmap Title => Get("title", PaintTitle);
    public static WriteableBitmap Lists => Get("lists", PaintLists);
    public static WriteableBitmap SiegeField => Get("siege", PaintSiege);
    public static WriteableBitmap Hall => Get("hall", PaintHall);
    public static WriteableBitmap Battlefield => Get("battle", PaintBattlefield);

    // ───────────────────────────── parchment ─────────────────────────────

    private static Pix PaintParchment(int w, int h, int seed)
    {
        var p = new Pix(w, h);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            double n = Pix.Fbm(x, y, 90, seed, 4), fine = Pix.Fbm(x, y, 7, seed + 50, 2);
            uint c = Pix.Mix(0xFFEEE0BC, 0xFFD9C394, n * 0.9);
            // Stains here and there.
            double stain = Pix.Fbm(x, y, 140, seed + 9, 3);
            if (stain > 0.62) c = Pix.Mix(c, 0xFFC4A472, (stain - 0.62) * 1.6);
            // The edges browned with age.
            double ex = Math.Min(x, w - 1 - x), ey = Math.Min(y, h - 1 - y), e = Math.Min(ex, ey) + 10 * (Pix.Noise(x, y, 30, seed + 3) - 0.5);
            if (e < 26) c = Pix.Mix(c, 0xFF8C6A3C, Math.Pow((26 - Math.Max(0, e)) / 26, 2.2) * 0.85);
            p.P[y * w + x] = Pix.Scale(c, 1 + (fine - 0.5) * 0.1);
        }
        return p;
    }

    // ───────────────────────────── the title ─────────────────────────────

    /// <summary>A ridge line: the height of a range of hills across the screen.</summary>
    private static double Ridge(double x, double baseY, double rise, double cell, int salt) =>
        baseY - rise * (Pix.Fbm(x, 0, cell, salt, 4) - 0.3);

    /// <summary>Where the castle stands on the title: on its hill, against the setting sun.</summary>
    public const double TitleCastleX = 790, TitleCastleY = 450, TitleCastleScale = 1.3;

    private static Pix PaintTitle()
    {
        var p = new Pix(W, H);
        p.Gradient(0, H, 0xFF141838, 0xFF2E2658, 0xFF6E3A62, 0xFFC86848, 0xFFF0A858, 0xFFF0B060);
        // The sun going down behind the castle, and its glow.
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            double dx = x - 586, dy = (y - 412) * 1.25, d = Math.Sqrt(dx * dx + dy * dy);
            uint c = p.P[y * W + x];
            if (d < 360) c = Pix.Mix(c, 0xFFFFD890, Math.Pow(1 - d / 360, 2.4) * 0.75);
            if (d < 58) c = Pix.Mix(c, 0xFFFFF4D0, 0.92);
            // Clouds in long bands, lit gold from below near the sun.
            double band = Pix.Fbm(x * 0.32, y * 2.6, 64, 41, 5);
            double cloud = Math.Clamp((band - 0.54) * 3.4, 0, 1) * Math.Clamp(1 - Math.Abs(y - 290) / 210.0, 0, 1);
            if (cloud > 0)
            {
                uint lit = Pix.Mix(0xFF5A3460, 0xFFF8B878, Math.Clamp(1 - d / 520, 0, 1) * 0.9 + (y > 320 ? 0.2 : 0));
                c = Pix.Mix(c, lit, cloud * 0.8);
            }
            p.P[y * W + x] = c;
        }
        // Ranges of hills, each darker and nearer; the castle's hill last.
        Range(p, 420, 60, 240, 12, 0xFF7A4E6E, 0xFF5E4064);
        Range(p, 470, 80, 170, 13, 0xFF4E3656, 0xFF3C2C48);
        Castle(p, TitleCastleX, TitleCastleY, TitleCastleScale, 0xFF1E1628, 0xFFFFC070);
        Range(p, 600, 90, 280, 15, 0xFF2A2232, 0xFF141018, hill: (TitleCastleX, TitleCastleY, 240));
        // Trees along the near ridge, black against the light.
        var dice = new Dice(5);
        for (int k = 0; k < 60; k++)
        {
            double x = dice.Range(0, W);
            if (Math.Abs(x - TitleCastleX) < 200) continue;
            double ridge = Ridge(x, 600, 90, 280, 15);
            double r = dice.Range(6, 15);
            p.Fill((int)x - 1, (int)(ridge - r * 0.6), 3, (int)(r * 0.8) + 4, 0xFF100C14);
            for (int j = 0; j < 4; j++)
                p.Disc(x + dice.Range(-r * 0.6, r * 0.6), ridge - r * 1.2 - dice.Range(0, r * 0.9), r * dice.Range(0.45, 0.7), 0xFF100C14);
        }
        p.Grain(0, 0, W, H, 0.04, 3);
        return p;
    }

    /// <summary>A range of hills: everything below its ridge filled, lighter at the top. A hill can be raised under a castle.</summary>
    private static void Range(Pix p, double baseY, double rise, double cell, int salt, uint top, uint bottom, (double X, double Y, double R)? hill = null)
    {
        for (int x = 0; x < W; x++)
        {
            double ridge = Ridge(x, baseY, rise, cell, salt);
            if (hill is { } h)
            {
                double t = Math.Max(0, Math.Abs(x - h.X) / h.R - 0.78);
                ridge = Math.Min(ridge, h.Y + 4 + 260 * t * t + 30 * t + 5 * Pix.Noise(x, 0, 14, 3));
            }
            for (int y = Math.Max(0, (int)ridge); y < H; y++)
            {
                uint c = Pix.Mix(top, bottom, Math.Clamp((y - ridge) / 140, 0, 1));
                p.P[y * W + x] = Pix.Scale(c, 1 + 0.1 * (Pix.Fbm(x, y, 22, salt, 2) - 0.5));
            }
        }
    }

    /// <summary>
    /// A castle in silhouette: curtain wall, towers and a tall keep, with merlons, arrow slits and a few lit windows,
    /// the low sun catching its edges.
    /// </summary>
    private static void Castle(Pix p, double cx, double ground, double s, uint body, uint lit)
    {
        uint rim = Pix.Mix(body, lit, 0.45);
        void Block(double x, double y, double w, double h)
        {
            int x0 = (int)(cx + x * s), x1 = (int)(cx + (x + w) * s), y0 = (int)(ground - (y + h) * s), y1 = (int)(ground - y * s) + 2;
            for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
                p.Set(xx, yy, Pix.Scale(body, 1 + 0.12 * (Pix.Rand(xx / 5, yy / 3, 2) - 0.5)));
            for (double m = x; m < x + w - 3; m += 7)
            {
                p.Fill((int)(cx + m * s), (int)(ground - (y + h + 5) * s), (int)(4 * s), (int)(5 * s) + 1, body);
                p.Fill((int)(cx + m * s), (int)(ground - (y + h + 5) * s), (int)(4 * s), 1, rim);
            }
            // The sun, low on the left, catches the left-hand edges and the tops.
            p.Fill(x0, y0, 2, y1 - y0, rim);
            p.Fill(x0, y0, x1 - x0, 1, rim);
        }
        Block(-120, 0, 240, 52);
        Block(-140, 0, 34, 92);
        Block(106, 0, 34, 92);
        Block(-60, 0, 30, 78);
        Block(-38, 0, 76, 118);
        Block(-14, 0, 30, 160);
        // Arrow slits and a few lit windows.
        foreach (var (wx, wy) in new[] { (-126.0, 70.0), (118.0, 70.0), (-26.0, 92.0), (22.0, 92.0), (-4.0, 136.0), (-90.0, 30.0), (60.0, 30.0) })
            p.Fill((int)(cx + wx * s), (int)(ground - wy * s), (int)(2 * s) + 1, (int)(7 * s), 0xFF0A0810);
        foreach (var (wx, wy) in new[] { (-18.0, 100.0), (8.0, 100.0), (2.0, 146.0), (-124.0, 50.0) })
            p.Fill((int)(cx + wx * s), (int)(ground - wy * s), (int)(4 * s), (int)(6 * s), 0xFFFFC860);
        // The gate.
        for (int k = 0; k < (int)(26 * s); k++)
        for (int j = 0; j < (int)(18 * s); j++)
        {
            double u = (j - 9 * s) / (9 * s);
            if (k / s < 26 - 8 * u * u) p.Set((int)(cx + (-79 * s) + j), (int)(ground - k), 0xFF08060A);
        }
    }

    // ───────────────────────────── the lists ─────────────────────────────

    private static Pix PaintLists()
    {
        var p = new Pix(W, H);
        const int horizon = (int)Joust.VanishY;
        p.Gradient(0, horizon, 0xFF4A7EC8, 0xFF8AB4E0, 0xFFD8E4EC);
        for (int y = 0; y < horizon; y++)
        for (int x = 0; x < W; x++)
        {
            double c = Pix.Fbm(x, y * 1.8, 110, 61, 5);
            double a = Math.Clamp((c - 0.52) * 3, 0, 1);
            if (a > 0) p.P[y * W + x] = Pix.Mix(p.P[y * W + x], Pix.Mix(0xFFFFFFFF, 0xFFB8C4D8, Pix.Fbm(x, y, 30, 62, 2)), a * 0.85);
        }
        // Woods and a far castle on the horizon.
        for (int x = 0; x < W; x++)
        {
            double top = horizon - 14 - 22 * Pix.Fbm(x, 0, 26, 5, 3);
            for (int y = (int)top; y < horizon + 2; y++) p.P[y * W + x] = Pix.Mix(0xFF4A6A4C, 0xFF2E4A34, (y - top) / 30);
        }
        Castle(p, 210, horizon - 6, 0.42, 0xFF8A90A0, 0xFFFFFFFF);
        // The field: green, mown in stripes that run away to the far end.
        for (int y = horizon; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            double depth = (y - horizon) / (double)(H - horizon);
            double u = (x - Joust.VanishX) / Math.Max(1, depth * 900);
            bool stripe = ((int)Math.Floor(u * 9 + 100)) % 2 == 0;
            uint c = Pix.Mix(0xFF5E8C3C, 0xFF7AA44C, Pix.Fbm(x / (depth + 0.05), y, 40, 4, 3));
            if (stripe) c = Pix.Scale(c, 1.06);
            // A sanded track along the tilt.
            double tiltX = TiltX(y);
            double off = (x - tiltX) / Math.Max(1, depth * 600);
            if (off is > -0.6 and < 0.5) c = Pix.Mix(c, Pix.Mix(0xFFC8A86C, 0xFFB08E58, Pix.Fbm(x, y, 9, 3, 2)), 0.85);
            p.P[y * W + x] = Pix.Scale(c, 0.85 + 0.25 * depth);
        }
        // The stands on either side, crowded, under striped awnings.
        Stand(p, left: true);
        Stand(p, left: false);
        Tilt(p);
        p.Grain(0, 0, W, H, 0.04, 8);
        return p;
    }

    /// <summary>Where the tilt (the barrier down the middle of the lists) is at a height on the screen.</summary>
    public static double TiltX(double y) => 544 + (y - Joust.VanishY) / (H - Joust.VanishY) * 150;

    /// <summary>The tilt: a painted wooden barrier running away down the middle of the lists, its top rail lit, its boards in bands of colour.</summary>
    private static void Tilt(Pix p)
    {
        const double horizon = Joust.VanishY;
        for (int x = (int)TiltX(horizon); x < W; x++)
        {
            double groundY = horizon + (x - TiltX(horizon)) * (H - horizon) / (TiltX(H) - TiltX(horizon));
            double depth = (groundY - horizon) / (H - horizon), height = 4 + depth * 150;
            double top = groundY - height;
            if (top > H) break;
            int band = (int)Math.Floor(1 / (depth + 0.04) * 2.2);
            uint board = band % 2 == 0 ? 0xFFB02A22 : 0xFFEDE6D6;
            for (int y = Math.Max(0, (int)top); y < Math.Min(H, (int)groundY); y++)
            {
                double v = (y - top) / height;
                uint c = v < 0.12 ? 0xFF8A6A40 : Pix.Scale(board, 0.78 + 0.22 * (1 - v));
                if (v is > 0.12 and < 0.16) c = 0xFF4A3220;
                p.P[y * W + x] = Pix.Scale(c, 0.9 + 0.1 * Pix.Rand(x, y / 4, 6));
            }
            // Its shadow on the sand.
            for (int k = 0; k < (int)(depth * 30); k++) p.Set(x + k + 1, (int)groundY, 0x40000000);
        }
    }

    private static void Stand(Pix p, bool left)
    {
        const int horizon = (int)Joust.VanishY;
        var dice = new Dice(left ? 3 : 4);
        uint[] coats = [0xFFB82A22, 0xFF2A4AA0, 0xFFE8C040, 0xFF2E7A3A, 0xFFEDE6D6, 0xFF7A3A8A, 0xFFC86A20, 0xFF5A3A22];
        uint[] faces = [0xFFE8C0A0, 0xFFD8A888, 0xFFC89070];
        for (int y = horizon - 40; y < H; y++)
        {
            double depth = Math.Max(0, (y - horizon + 40) / (double)(H - horizon + 40));
            // The stand's inner edge runs from near the tilt far off to the screen's edge close by.
            double edge = left ? 330 - depth * 360 : 760 + depth * 380;
            for (int x = 0; x < W; x++)
            {
                bool inside = left ? x < edge : x > edge;
                if (!inside) continue;
                double front = left ? edge - x : x - edge;
                uint c;
                // The back of the stand hung with cloths in the lords' colours; the tiers below.
                if (y < horizon + 10 + depth * 30 && front > 0)
                {
                    uint[] drapes = [0xFFB02A28, 0xFFE0B040, 0xFF2A4A9A, 0xFFEDE6D6, 0xFF2E6A3A];
                    int k = (int)Math.Floor((left ? x : W - x) / (16 + depth * 26));
                    c = Pix.Scale(drapes[k % drapes.Length], 0.75 + 0.3 * Math.Abs(Math.Sin(x / (3 + depth * 6))));
                }
                else c = 0xFF6A4E34;
                p.P[y * W + x] = c;
            }
        }
        // The crowd: heads and shoulders in rows, nearer ones bigger.
        for (int row = 0; row < 26; row++)
        {
            double depth = 0.02 + row / 26.0 * 0.75;
            double y = horizon - 6 + depth * (H - horizon) * 0.62;
            double s = 2 + depth * 11;
            double edge = left ? 330 - depth * 360 : 760 + depth * 380;
            for (double x = left ? edge - s : edge + s; left ? x > -s : x < W + s; x += left ? -s * 1.25 : s * 1.25)
            {
                double yy = y + dice.Range(-s * 0.3, s * 0.3);
                uint coat = coats[dice.Next(coats.Length)];
                p.Disc(x, yy + s * 1.1, s * 0.75, s * 0.9, Pix.Scale(coat, 0.7 + depth * 0.4));
                p.Disc(x, yy, s * 0.42, faces[dice.Next(faces.Length)]);
                if (dice.Chance(0.3)) p.Disc(x, yy - s * 0.3, s * 0.45, s * 0.25, coats[dice.Next(coats.Length)]);
            }
        }
        // The awning over them, striped, with a scalloped hem.
        for (int y = 0; y < horizon + 60; y++)
        for (int x = 0; x < W; x++)
        {
            double depth = Math.Max(0, (y + 140 - horizon) / 260.0);
            double edge = left ? 360 - depth * 420 : 730 + depth * 420;
            bool inside = left ? x < edge : x > edge;
            double hem = 20 + 120 * Math.Pow(1 - Math.Min(1, Math.Abs(x - (left ? 0 : W)) / 400.0), 1.2);
            if (!inside || y > hem + 6 * Math.Abs(Math.Sin(x / 14.0))) continue;
            int stripe = (int)Math.Floor((left ? x : W - x) / (18 + depth * 30));
            uint c = stripe % 2 == 0 ? 0xFFB02A28 : 0xFFF0E6D0;
            p.P[y * W + x] = Pix.Scale(c, 0.8 + 0.25 * Pix.Fbm(x, y, 20, 2, 2));
        }
    }

    // ───────────────────────────── the siege ─────────────────────────────

    private static Pix PaintSiege()
    {
        var p = new Pix(W, H);
        p.Gradient(0, H, 0xFF243460, 0xFF4E5A8A, 0xFFA87A78, 0xFFE8A868, 0xFFE8A868);
        for (int y = 0; y < 520; y++)
        for (int x = 0; x < W; x++)
        {
            double band = Pix.Fbm(x * 0.4, y * 2, 70, 15, 4);
            double a = Math.Clamp((band - 0.56) * 3, 0, 1) * Math.Clamp(1 - Math.Abs(y - 220) / 220.0, 0, 1);
            if (a > 0) p.P[y * W + x] = Pix.Mix(p.P[y * W + x], y > 300 ? 0xFFF0B080 : 0xFF6A5A80, a * 0.7);
        }
        Range(p, 520, 60, 200, 21, 0xFF5A5A78, 0xFF4A4A64);
        Range(p, 560, 50, 150, 22, 0xFF3E5040, 0xFF34443A);
        // The ground before the castle: churned turf.
        for (int y = 560; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            double groundY = Siege.GroundY - 6 + 10 * Pix.Fbm(x, 0, 60, 3, 2);
            if (y < groundY) continue;
            uint c = Pix.Mix(0xFF4E6A30, 0xFF3A4E26, Pix.Fbm(x, y, 18, 6, 3));
            if (y > groundY + 40) c = Pix.Mix(c, 0xFF5A4630, Math.Clamp((y - groundY - 40) / 100, 0, 0.6));
            if (y < groundY + 3) c = Pix.Scale(c, 1.25);
            p.P[y * W + x] = c;
        }
        // Trees along the far ridge.
        var dice = new Dice(31);
        for (int k = 0; k < 40; k++)
        {
            double x = dice.Range(0, 700), ridge = Ridge(x, 560, 50, 150, 22), r = dice.Range(5, 11);
            for (int j = 0; j < 3; j++) p.Disc(x + dice.Range(-r * 0.5, r * 0.5), ridge - r - dice.Range(0, r * 0.6), r * 0.7, 0xFF2A3A2C);
        }
        // The keep behind, its curtain wall round the bailey, then the front wall the catapult aims at.
        StoneBlock(p, 892, 214, 132, (int)Siege.GroundY - 214, 0xFF7E786C, merlons: true);
        foreach (var (wx, wy, ww, wh) in new[] { (930, 290, 8, 18), (972, 290, 8, 18), (950, 370, 10, 22), (930, 450, 8, 18), (990, 450, 8, 18) })
        {
            p.Fill(wx, wy, ww, wh, 0xFF181210);
            p.Disc(wx + ww / 2.0, wy, ww / 2.0, 0xFF181210);
        }
        StoneBlock(p, (int)(Siege.WallX + Siege.WallThick), 470, 892 - (int)(Siege.WallX + Siege.WallThick), (int)Siege.GroundY - 470, 0xFF6E685E, merlons: true);
        for (int x = (int)(Siege.WallX + Siege.WallThick); x < 892; x++)
        for (int y = (int)Siege.GroundY - 46; y < Siege.GroundY; y++)
            p.P[y * W + x] = Pix.Mix(0xFF6A5A40, 0xFF4A4030, Pix.Fbm(x, y, 12, 4, 2));
        // Huts in the bailey, thatched.
        foreach (var hx in new[] { 806, 846 })
        {
            p.Fill(hx, (int)Siege.GroundY - 32, 32, 32, 0xFF7A6448);
            p.Fill(hx + 12, (int)Siege.GroundY - 16, 8, 16, 0xFF2A1A10);
            for (int k = 0; k < 20; k++) p.HLine(hx - 4 + k, hx + 36 - k, (int)Siege.GroundY - 32 - k, Pix.Scale(0xFF9A7A3A, 0.85 + 0.15 * Pix.Rand(k, hx, 2)));
        }
        StoneBlock(p, (int)Siege.WallX, (int)Siege.WallTop, (int)Siege.WallThick, (int)(Siege.GroundY - Siege.WallTop), 0xFF9A9284, merlons: true);
        p.Grain(0, 0, W, H, 0.04, 9);
        return p;
    }

    /// <summary>A block of masonry: courses of stones, each a little different, lit from the left, with merlons on top.</summary>
    public static void StoneBlock(Pix p, int x0, int y0, int w, int h, uint stone, bool merlons)
    {
        for (int y = y0; y < y0 + h; y++)
        for (int x = x0; x < x0 + w; x++)
        {
            int course = (y - y0) / 14, off = course % 2 == 0 ? 0 : 11;
            int col = (x - x0 + off) / 22;
            bool joint = (y - y0) % 14 == 0 || (x - x0 + off) % 22 == 0;
            uint c = Pix.Scale(stone, 0.86 + 0.24 * Pix.Rand(col, course, 7));
            c = Pix.Scale(c, 1 + 0.12 * (Pix.Fbm(x, y, 5, 3, 2) - 0.5));
            if (joint) c = Pix.Scale(stone, 0.55);
            if (x - x0 < 3) c = Pix.Scale(c, 1.15);
            if (x0 + w - x < 4) c = Pix.Scale(c, 0.75);
            p.Set(x, y, c);
        }
        if (merlons)
            for (int x = x0; x < x0 + w - 6; x += 16)
            for (int y = y0 - (int)Siege.Merlon; y < y0; y++)
            for (int k = 0; k < 10; k++)
                p.Set(x + k, y, Pix.Scale(stone, k < 2 ? 1.12 : k > 8 ? 0.75 : 0.95 + 0.08 * Pix.Rand(x + k, y, 3)));
    }

    // ───────────────────────────── the hall ─────────────────────────────

    public const int HallFloor = 560;

    private static Pix PaintHall()
    {
        var p = new Pix(W, H);
        // The back wall: big dressed stones, dim and warm.
        for (int y = 0; y < HallFloor; y++)
        for (int x = 0; x < W; x++)
        {
            int course = y / 34, off = course % 2 == 0 ? 0 : 30;
            int col = (x + off) / 60;
            bool joint = y % 34 < 2 || (x + off) % 60 < 2;
            uint c = Pix.Scale(0xFF6A5E50, 0.75 + 0.35 * Pix.Rand(col, course, 4));
            c = Pix.Scale(c, 1 + 0.2 * (Pix.Fbm(x, y, 6, 5, 2) - 0.5));
            if (joint) c = 0xFF2A2420;
            p.P[y * W + x] = c;
        }
        // Two tall windows with the night outside, and a door at the far end.
        foreach (var wx in new[] { 250, 650 })
            Arch(p, wx, 120, 90, 230, (x, y) => Pix.Mix(0xFF0E1430, 0xFF243466, y / 350.0 * 0.8) is var c && Pix.Rand(x, y, 9) > 0.996 ? 0xFFE8E8FF : c);
        Arch(p, 900, 300, 110, HallFloor - 300, (x, y) => Pix.Mix(0xFF1A120C, 0xFF2A1E14, Pix.Rand(x / 12, 0, 3)));
        // Beams across the top.
        p.Fill(0, 0, W, 34, 0xFF2E2018);
        p.Fill(0, 34, W, 6, 0xFF1A120C);
        for (int x = 40; x < W; x += 180) p.Fill(x, 40, 22, 40, 0xFF3A281C);
        // The floor: flagstones going away.
        for (int y = HallFloor; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            double depth = (y - HallFloor) / (double)(H - HallFloor);
            double u = (x - 512) / (300 + depth * 700);
            int row = (int)(Math.Sqrt(depth) * 8), colN = (int)Math.Floor(u * 6 + 50);
            bool joint = Math.Abs(Math.Sqrt(depth) * 8 - row) < 0.06 || Math.Abs(u * 6 + 50 - colN) < 0.04;
            uint c = Pix.Scale(0xFF5A5048, 0.75 + 0.3 * Pix.Rand(colN, row, 6));
            if (joint) c = 0xFF2A2420;
            p.P[y * W + x] = Pix.Scale(c, 0.7 + 0.45 * depth);
        }
        p.Fill(0, HallFloor - 4, W, 4, 0xFF2A2018);
        // Darkness at the edges, where the torchlight doesn't reach.
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            double dx = (x - 512) / 600.0, dy = (y - 420) / 520.0;
            p.P[y * W + x] = Pix.Scale(p.P[y * W + x], Math.Clamp(1.15 - (dx * dx + dy * dy) * 0.9, 0.35, 1.1));
        }
        p.Grain(0, 0, W, H, 0.05, 12);
        return p;
    }

    private static void Arch(Pix p, int cx, int top, int w, int h, Func<int, int, uint> fill)
    {
        int r = w / 2;
        for (int y = top; y < top + h; y++)
        for (int x = cx - r; x < cx + r; x++)
        {
            double dy = y - (top + r);
            if (dy < 0 && (x - cx) * (x - cx) + dy * dy > r * r) continue;
            p.Set(x, y, fill(x, y));
        }
        // The dressed stones round it.
        for (double a = Math.PI; a <= Math.Tau; a += 0.02)
            for (int k = 0; k < 10; k++)
                p.Set((int)(cx + Math.Cos(a) * (r + k)), (int)(top + r + Math.Sin(a) * (r + k)), k < 2 ? 0xFF2A2420 : 0xFF8A7C6A);
        for (int y = top + r; y < top + h; y++)
            for (int k = 0; k < 10; k++) { p.Set(cx - r - k, y, k < 2 ? 0xFF2A2420 : 0xFF8A7C6A); p.Set(cx + r + k - 1, y, k < 2 ? 0xFF2A2420 : 0xFF7A6C5A); }
    }

    // ───────────────────────────── the battlefield ─────────────────────────────

    public const int FieldHorizon = 300;

    private static Pix PaintBattlefield()
    {
        var p = new Pix(W, H);
        p.Gradient(0, H, 0xFF5A82B8, 0xFFA8C0D8, 0xFFE4E0D0, 0xFFE4E0D0);
        for (int y = 0; y < FieldHorizon; y++)
        for (int x = 0; x < W; x++)
        {
            double c = Pix.Fbm(x, y * 2, 120, 33, 5);
            double a = Math.Clamp((c - 0.5) * 2.5, 0, 1);
            if (a > 0) p.P[y * W + x] = Pix.Mix(p.P[y * W + x], Pix.Mix(0xFFFFFFFF, 0xFF9AA4B8, Pix.Fbm(x, y + 40, 40, 34, 2)), a * 0.8);
        }
        Range(p, FieldHorizon - 10, 70, 240, 31, 0xFF7A8AA0, 0xFF6A7A8C);
        Range(p, FieldHorizon + 12, 50, 170, 32, 0xFF5A7A4A, 0xFF4E6A40);
        for (int x = 0; x < W; x++)
        {
            double ridge = Ridge(x, FieldHorizon + 12, 50, 170, 32);
            for (int y = Math.Max(0, (int)ridge); y < H; y++)
            {
                double depth = Math.Clamp((y - FieldHorizon) / (double)(H - FieldHorizon), 0, 1);
                uint c = Pix.Mix(0xFF6A9A40, 0xFF4E7A30, Pix.Fbm(x, y / (0.3 + depth), 50, 7, 4));
                if (Pix.Rand(x, y, 3) > 0.97) c = Pix.Scale(c, 1.3);
                c = Pix.Scale(c, 0.8 + 0.3 * depth);
                p.P[y * W + x] = Pix.Mix(p.P[y * W + x], c, Math.Clamp((y - ridge) / 70, 0, 1));
            }
        }
        // Woods on the far hills, and a few bushes nearer.
        var dice = new Dice(41);
        for (int k = 0; k < 70; k++)
        {
            double x = dice.Range(0, W), ridge = Ridge(x, FieldHorizon + 12, 50, 170, 32), r = dice.Range(5, 12);
            for (int j = 0; j < 3; j++) p.Disc(x + dice.Range(-r * 0.6, r * 0.6), ridge - r * 0.6 - dice.Range(0, r * 0.8), r * 0.75, Pix.Scale(0xFF3A5A30, dice.Range(0.8, 1.1)));
        }
        for (int k = 0; k < 14; k++)
        {
            double x = dice.Range(0, W), y = dice.Range(FieldHorizon + 90, H), r = 4 + (y - FieldHorizon) / 40;
            p.Disc(x + r * 0.4, y + r * 0.3, r * 1.1, r * 0.4, 0x40000000);
            for (int j = 0; j < 3; j++) p.Disc(x + dice.Range(-r * 0.5, r * 0.5), y - dice.Range(0, r * 0.6), r * 0.7, Pix.Scale(0xFF3E6A2C, dice.Range(0.8, 1.15)));
        }
        p.Grain(0, 0, W, H, 0.04, 14);
        return p;
    }
}
