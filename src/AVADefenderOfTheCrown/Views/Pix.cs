namespace AVADefenderOfTheCrown.Views;

/// <summary>
/// A picture as an array of 0xAARRGGBB pixels, with the few things the painted backgrounds need: rectangles, lines,
/// discs, shading and grain, smooth noise, and turning it into a bitmap.
/// </summary>
public sealed class Pix
{
    public readonly int W, H;
    public readonly uint[] P;

    public Pix(int w, int h, uint fill = 0xFF000000)
    {
        W = w;
        H = h;
        P = new uint[w * h];
        Array.Fill(P, fill);
    }

    public uint Get(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? P[y * W + x] : 0;

    public void Set(int x, int y, uint c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        uint a = c >> 24;
        if (a == 0xFF) P[y * W + x] = c;
        else if (a > 0) P[y * W + x] = Mix(P[y * W + x], c | 0xFF000000, a / 255.0);
    }

    public void Fill(int x, int y, int w, int h, uint c)
    {
        for (int j = y; j < y + h; j++)
        for (int i = x; i < x + w; i++) Set(i, j, c);
    }

    public void Frame(int x, int y, int w, int h, uint c)
    {
        HLine(x, x + w - 1, y, c);
        HLine(x, x + w - 1, y + h - 1, c);
        VLine(x, y, y + h - 1, c);
        VLine(x + w - 1, y, y + h - 1, c);
    }

    public void HLine(int x0, int x1, int y, uint c)
    {
        for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++) Set(x, y, c);
    }

    public void VLine(int x, int y0, int y1, uint c)
    {
        for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++) Set(x, y, c);
    }

    public void Line(int x0, int y0, int x1, int y1, uint c)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            Set(x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>A filled ellipse round (cx, cy), radii in pixels (half-pixel centres allowed).</summary>
    public void Disc(double cx, double cy, double rx, double ry, uint c)
    {
        for (int y = (int)Math.Floor(cy - ry); y <= (int)Math.Ceiling(cy + ry); y++)
        for (int x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++)
        {
            double u = (x + 0.5 - cx) / rx, v = (y + 0.5 - cy) / ry;
            if (u * u + v * v <= 1) Set(x, y, c);
        }
    }

    public void Disc(double cx, double cy, double r, uint c) => Disc(cx, cy, r, r, c);

    /// <summary>Darkens (k &lt; 1) or lightens what's already there.</summary>
    public void Shade(int x, int y, int w, int h, double k)
    {
        for (int j = y; j < y + h; j++)
        for (int i = x; i < x + w; i++)
            if (i >= 0 && j >= 0 && i < W && j < H) P[j * W + i] = Scale(P[j * W + i], k);
    }

    /// <summary>Speckles a rectangle with lighter and darker pixels.</summary>
    public void Grain(int x, int y, int w, int h, double amount, int seed)
    {
        for (int j = y; j < y + h; j++)
        for (int i = x; i < x + w; i++)
        {
            if (i < 0 || j < 0 || i >= W || j >= H) continue;
            double r = Rand(i, j, seed);
            if (r < 0.18) P[j * W + i] = Scale(P[j * W + i], 1 - amount);
            else if (r > 0.86) P[j * W + i] = Scale(P[j * W + i], 1 + amount * 0.8);
        }
    }

    public void Blit(Pix src, int x, int y)
    {
        for (int j = 0; j < src.H; j++)
        for (int i = 0; i < src.W; i++) Set(x + i, y + j, src.P[j * src.W + i]);
    }

    public static uint Scale(uint c, double k)
    {
        uint Ch(int shift) => (uint)Math.Clamp(Math.Round(((c >> shift) & 0xFF) * k), 0, 255);
        return (c & 0xFF000000) | Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }

    public static uint Mix(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        uint Ch(int shift) => (uint)Math.Round(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t);
        return 0xFF000000 | Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }

    /// <summary>Smooth noise: random values on a grid of <paramref name="cell"/> pixels, blended between.</summary>
    public static double Noise(double x, double y, double cell, int salt)
    {
        double u = x / cell, v = y / cell;
        int i = (int)Math.Floor(u), j = (int)Math.Floor(v);
        double fu = u - i, fv = v - j;
        fu = fu * fu * (3 - 2 * fu);
        fv = fv * fv * (3 - 2 * fv);
        double a = Rand(i, j, salt), b = Rand(i + 1, j, salt), c = Rand(i, j + 1, salt), d = Rand(i + 1, j + 1, salt);
        return a + (b - a) * fu + (c - a) * fv + (a - b - c + d) * fu * fv;
    }

    /// <summary>Noise in several octaves, each half the size and half the strength of the last (0 to 1).</summary>
    public static double Fbm(double x, double y, double cell, int salt, int octaves = 4)
    {
        double sum = 0, amp = 1, total = 0;
        for (int o = 0; o < octaves; o++)
        {
            sum += Noise(x, y, cell, salt + o * 101) * amp;
            total += amp;
            amp *= 0.5;
            cell *= 0.5;
        }
        return sum / total;
    }

    /// <summary>A colour from its red, green and blue.</summary>
    public static uint Rgb(double r, double g, double b) =>
        0xFF000000 | (uint)Math.Clamp(r, 0, 255) << 16 | (uint)Math.Clamp(g, 0, 255) << 8 | (uint)Math.Clamp(b, 0, 255);

    /// <summary>A vertical gradient through the colours given, top to bottom.</summary>
    public void Gradient(int y0, int y1, params uint[] stops)
    {
        for (int y = y0; y < y1; y++)
        {
            double t = (y - y0) / (double)Math.Max(1, y1 - y0 - 1) * (stops.Length - 1);
            int k = Math.Min(stops.Length - 2, (int)t);
            uint c = Mix(stops[k], stops[k + 1], t - k);
            for (int x = 0; x < W; x++) P[y * W + x] = c;
        }
    }

    public static double Rand(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return (h & 0xFFFF) / 65536.0;
    }
}

