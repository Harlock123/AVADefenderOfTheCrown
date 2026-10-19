using Avalonia;
using Avalonia.Media;
using AVADefenderOfTheCrown.Engine;

namespace AVADefenderOfTheCrown.Views;

public sealed partial class GameCanvas
{
    // ───────────────────────────── heraldry ─────────────────────────────

    /// <summary>A heater shield's outline, <paramref name="h"/> tall, round its middle.</summary>
    private static StreamGeometry ShieldShape(Point c, double h)
    {
        double w = h * 0.84, top = c.Y - h / 2, bottom = c.Y + h / 2;
        var g = new StreamGeometry();
        using var s = g.Open();
        s.BeginFigure(new Point(c.X - w / 2, top), true);
        s.LineTo(new Point(c.X + w / 2, top));
        s.LineTo(new Point(c.X + w / 2, top + h * 0.36));
        s.QuadraticBezierTo(new Point(c.X + w / 2, top + h * 0.8), new Point(c.X, bottom));
        s.QuadraticBezierTo(new Point(c.X - w / 2, top + h * 0.8), new Point(c.X - w / 2, top + h * 0.36));
        s.EndFigure(true);
        return g;
    }

    /// <summary>A lord's arms on a heater shield (the outlaw's for <see cref="Realm.Outlaws"/>; a plain one for nobody).</summary>
    public static void DrawShield(DrawingContext dc, int lord, Point c, double h)
    {
        uint field, tincture;
        Charge charge;
        if (lord >= 0) (field, tincture, charge) = (Realm.Lords[lord].Field, Realm.Lords[lord].Tincture, Realm.Lords[lord].Charge);
        else if (lord == Realm.Outlaws) (field, tincture, charge) = (0xFF2E6A2A, 0xFFE8C040, Charge.Arrow);
        else (field, tincture, charge) = (0xFF9A8E78, 0xFF6A5E4A, Charge.Pale);
        var shape = ShieldShape(c, h);
        double w = h * 0.84, top = c.Y - h / 2;
        dc.DrawGeometry(Brush(0x60000000), null, ShieldShape(new Point(c.X + h * 0.04, c.Y + h * 0.05), h));
        using (dc.PushGeometryClip(shape))
        {
            dc.FillRectangle(Brush(field), new Rect(c.X - w / 2, top, w, h));
            var t = Brush(tincture);
            double band = h * 0.2;
            switch (charge)
            {
                case Charge.Chevron:
                    Poly(dc, tincture, null, 0, new(c.X - w / 2, c.Y + h * 0.22), new(c.X, c.Y - h * 0.2), new(c.X + w / 2, c.Y + h * 0.22),
                        new(c.X + w / 2, c.Y + h * 0.22 + band), new(c.X, c.Y - h * 0.2 + band), new(c.X - w / 2, c.Y + h * 0.22 + band));
                    break;
                case Charge.Cross:
                    dc.FillRectangle(t, new Rect(c.X - band / 2, top, band, h));
                    dc.FillRectangle(t, new Rect(c.X - w / 2, c.Y - h * 0.12 - band / 2, w, band));
                    break;
                case Charge.Saltire:
                    dc.DrawLine(new Pen(t, band * 0.9), new Point(c.X - w / 2, top), new Point(c.X + w / 2, c.Y + h / 2));
                    dc.DrawLine(new Pen(t, band * 0.9), new Point(c.X + w / 2, top), new Point(c.X - w / 2, c.Y + h / 2));
                    break;
                case Charge.Bend:
                    dc.DrawLine(new Pen(t, band * 1.1), new Point(c.X - w / 2, top), new Point(c.X + w / 2, c.Y + h * 0.4));
                    for (int k = 0; k < 2; k++)
                        dc.DrawEllipse(t, null, new Point(c.X + w * (k == 0 ? 0.22 : -0.24), c.Y + h * (k == 0 ? -0.22 : 0.18)), h * 0.07, h * 0.07);
                    break;
                case Charge.Pale:
                    dc.FillRectangle(t, new Rect(c.X - w / 6, top, w / 3, h));
                    break;
                case Charge.Quarterly:
                    dc.FillRectangle(t, new Rect(c.X, top, w / 2, h * 0.42));
                    dc.FillRectangle(t, new Rect(c.X - w / 2, top + h * 0.42, w / 2, h));
                    break;
                case Charge.Roundels:
                    foreach (var (dx, dy) in new[] { (-0.2, -0.2), (0.2, -0.2), (0.0, 0.15) })
                        dc.DrawEllipse(t, null, new Point(c.X + w * dx, c.Y + h * dy), h * 0.11, h * 0.11);
                    break;
                case Charge.Mullets:
                    dc.FillRectangle(t, new Rect(c.X - w / 2, top, w, h * 0.3));
                    foreach (var (dx, dy) in new[] { (-0.26, -0.35), (0.0, -0.35), (0.26, -0.35) })
                        Star(dc, new Point(c.X + w * dx, c.Y + h * dy), h * 0.085, field);
                    Star(dc, new Point(c.X, c.Y + h * 0.1), h * 0.17, tincture);
                    break;
                case Charge.Arrow:
                    dc.DrawLine(new Pen(t, h * 0.05), new Point(c.X, c.Y + h * 0.36), new Point(c.X, c.Y - h * 0.26));
                    Poly(dc, tincture, null, 0, new(c.X, c.Y - h * 0.4), new(c.X + h * 0.1, c.Y - h * 0.22), new(c.X - h * 0.1, c.Y - h * 0.22));
                    Poly(dc, tincture, null, 0, new(c.X, c.Y + h * 0.22), new(c.X + h * 0.1, c.Y + h * 0.38), new(c.X + h * 0.1, c.Y + h * 0.3), new(c.X, c.Y + h * 0.14));
                    Poly(dc, tincture, null, 0, new(c.X, c.Y + h * 0.22), new(c.X - h * 0.1, c.Y + h * 0.38), new(c.X - h * 0.1, c.Y + h * 0.3), new(c.X, c.Y + h * 0.14));
                    break;
            }
            // The gloss of enamel and the shadow of the shield's curve.
            dc.FillRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(c.X - w / 2, top, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(c.X + w / 2, top + h, RelativeUnit.Absolute),
                GradientStops = { new GradientStop(Color.FromUInt32(0x50FFFFFF), 0), new GradientStop(Color.FromUInt32(0x00FFFFFF), 0.45), new GradientStop(Color.FromUInt32(0x48000000), 1) },
            }, new Rect(c.X - w / 2, top, w, h));
        }
        dc.DrawGeometry(null, Pen(Gold, Math.Max(1, h * 0.045)), shape);
        dc.DrawGeometry(null, Pen(0xFF20140A, Math.Max(0.8, h * 0.018)), shape);
    }

    private static void Star(DrawingContext dc, Point c, double r, uint colour)
    {
        var pts = new Point[10];
        for (int k = 0; k < 10; k++)
        {
            double a = -Math.PI / 2 + k * Math.PI / 5, rr = k % 2 == 0 ? r : r * 0.42;
            pts[k] = new Point(c.X + Math.Cos(a) * rr, c.Y + Math.Sin(a) * rr);
        }
        Poly(dc, colour, null, 0, pts);
    }

    // ───────────────────────────── portraits ─────────────────────────────

    private const uint Skin = 0xFFE8B898, SkinShade = 0xFFC08868, Mail = 0xFF9AA0A8, Steel = 0xFFB8C0CC, SteelDark = 0xFF5A6270;

    /// <summary>A knight's portrait in an arched frame: sky, his surcoat in his colours, and his head, each knight his own.</summary>
    private void DrawPortrait(DrawingContext dc, Rect r, int knight)
    {
        var info = Realm.Lords[knight];
        var arch = new StreamGeometry();
        using (var s = arch.Open())
        {
            s.BeginFigure(new Point(r.X, r.Bottom), true);
            s.LineTo(new Point(r.X, r.Y + r.Width / 2));
            s.ArcTo(new Point(r.Right, r.Y + r.Width / 2), new Size(r.Width / 2, r.Width / 2), 0, false, SweepDirection.Clockwise);
            s.LineTo(new Point(r.Right, r.Bottom));
            s.EndFigure(true);
        }
        using (dc.PushGeometryClip(arch))
        {
            dc.FillRectangle(Vertical(0xFF3A5A9A, 0xFFE8C890, r), r);
            // Far hills behind him.
            Poly(dc, 0xFF5A7A5A, null, 0, new(r.X, r.Bottom - 50), new(r.X + 50, r.Bottom - 72), new(r.X + 110, r.Bottom - 58), new(r.Right, r.Bottom - 80), r.BottomRight, r.BottomLeft);
            double cx = r.Center.X, top = r.Y + 34;
            DrawBust(dc, new Point(cx, top), knight, info.Field, info.Tincture, 1);
        }
        dc.DrawGeometry(null, Pen(Gold, 4), arch);
        dc.DrawGeometry(null, Pen(Ink, 1.4), arch);
    }

    /// <summary>A knight from the chest up, his head's top at <paramref name="top"/>; four looks for the four Saxons.</summary>
    private static void DrawBust(DrawingContext dc, Point top, int look, uint field, uint tincture, double s)
    {
        double cx = top.X, y = top.Y;
        Point P(double x, double yy) => new(cx + x * s, y + yy * s);
        // Shoulders in mail, the surcoat over them with the arms on the chest.
        Poly(dc, Mail, 0xFF3A3E46, 1.2, P(-78, 176), P(-60, 118), P(-24, 104), P(24, 104), P(60, 118), P(78, 176));
        Poly(dc, field, 0xFF2A1A10, 1.2, P(-60, 176), P(-46, 124), P(-18, 112), P(18, 112), P(46, 124), P(60, 176));
        DrawShield(dc, look, P(0, 150), 46 * s);
        // The neck and the mail coif.
        dc.DrawGeometry(Brush(Mail), Pen(0xFF3A3E46, 1.2), new EllipseGeometry(new Rect(P(-30, 56), P(30, 118))));
        for (int k = 0; k < 6; k++) dc.DrawLine(Pen(0x603A3E46, 1), P(-26, 66 + k * 8), P(26, 66 + k * 8));
        switch (look)
        {
            case 0:
                // Edmund: a conical helm with a nasal, a brown beard.
                Face(dc, P, 0xFF6A4228, beard: true);
                Poly(dc, Steel, 0xFF2A2E36, 1.4, P(-28, 46), P(0, 4), P(28, 46));
                dc.FillRectangle(Brush(SteelDark), new Rect(P(-29, 42), P(29, 50)));
                dc.FillRectangle(Brush(Steel), new Rect(P(-3, 46), P(3, 72)));
                break;
            case 1:
                // Osric: a great helm with breaths and an eye slit, and a plume in his colour.
                Poly(dc, tincture, 0xFF2A1A10, 1, P(-6, 14), P(-20, -6), P(0, -14), P(22, -4), P(8, 14));
                dc.DrawGeometry(Brush(Steel), Pen(0xFF2A2E36, 1.6), new RectangleGeometry(new Rect(P(-30, 14), P(30, 92)), 6 * s, 6 * s));
                dc.FillRectangle(Brush(0xFF1A1C20), new Rect(P(-26, 44), P(26, 50)));
                dc.FillRectangle(Brush(SteelDark), new Rect(P(-2, 14), P(2, 92)));
                for (int k = 0; k < 4; k++) dc.DrawEllipse(Brush(0xFF1A1C20), null, P(12, 62 + k * 6), 1.6 * s, 1.6 * s);
                dc.DrawLine(Pen(0x80FFFFFF, 2), P(-24, 20), P(-24, 86));
                break;
            case 2:
                // Godwin: bareheaded, his red mane and beard.
                Face(dc, P, 0xFFB0401C, beard: true, hair: true);
                break;
            default:
                // Leofric: the coif drawn up round a fair face, a long moustache.
                dc.DrawGeometry(Brush(Mail), Pen(0xFF3A3E46, 1.2), new EllipseGeometry(new Rect(P(-30, 16), P(30, 94))));
                Face(dc, P, 0xFFD8B060, beard: false, moustache: true);
                break;
        }
    }

    private static void Face(DrawingContext dc, Func<double, double, Point> P, uint hairColour, bool beard, bool hair = false, bool moustache = false)
    {
        if (hair) dc.DrawGeometry(Brush(hairColour), null, new EllipseGeometry(new Rect(P(-30, 14), P(30, 74))));
        dc.DrawGeometry(Brush(Skin), Pen(0xFF6A4030, 1), new EllipseGeometry(new Rect(P(-21, 28), P(21, 86))));
        dc.DrawGeometry(Brush(SkinShade), null, new EllipseGeometry(new Rect(P(8, 36), P(20, 80))));
        dc.DrawEllipse(Brush(0xFF2A1A10), null, P(-8, 52), 2.4, 2);
        dc.DrawEllipse(Brush(0xFF2A1A10), null, P(8, 52), 2.4, 2);
        dc.DrawLine(Pen(0xFF5A3020, 1.6), P(-12, 46), P(-4, 45));
        dc.DrawLine(Pen(0xFF5A3020, 1.6), P(4, 45), P(12, 46));
        dc.DrawLine(Pen(0xFFA06048, 1.4), P(0, 54), P(-2, 64));
        if (beard) Poly(dc, hairColour, 0xFF2A1A10, 0.8, P(-21, 62), P(-14, 74), P(-6, 70), P(6, 70), P(14, 74), P(21, 62), P(18, 84), P(0, 96), P(-18, 84));
        if (moustache) Poly(dc, hairColour, null, 0, P(-16, 74), P(-4, 68), P(0, 70), P(4, 68), P(16, 74), P(4, 72), P(-4, 72));
        dc.DrawLine(Pen(0xFF7A3A2A, 1.4), P(-5, 76), P(5, 76));
        if (hair) Poly(dc, hairColour, null, 0, P(-24, 30), P(-10, 18), P(10, 18), P(24, 30), P(14, 30), P(0, 26), P(-14, 30));
    }

    // ───────────────────────────── the map's markers ─────────────────────────────

    /// <summary>A castle on the map, its banner in its holder's colours (in ruins, with no lord left).</summary>
    private void DrawCastleIcon(DrawingContext dc, Point at, int owner, bool taken)
    {
        double x = at.X, y = at.Y;
        uint stone = taken ? 0xFFB8AC98 : 0xFFD8D0C0, dark = 0xFF2A2018;
        dc.DrawEllipse(Brush(0x50000000), null, new Point(x + 2, y + 9), 24, 6);
        Poly(dc, stone, dark, 1.2, new(x - 16, y + 8), new(x - 16, y - 8), new(x + 16, y - 8), new(x + 16, y + 8));
        foreach (int tx in new[] { -20, 12 })
        {
            Poly(dc, stone, dark, 1.2, new(x + tx, y + 8), new(x + tx, y - 16), new(x + tx + 8, y - 16), new(x + tx + 8, y + 8));
            dc.FillRectangle(Brush(stone), new Rect(x + tx - 1, y - 20, 10, 4));
            dc.DrawRectangle(null, Pen(dark, 1), new Rect(x + tx - 1, y - 20, 10, 4));
        }
        Poly(dc, stone, dark, 1.2, new(x - 6, y - 8), new(x - 6, y - 24), new(x + 6, y - 24), new(x + 6, y - 8));
        Poly(dc, 0xFF3A2A1A, null, 0, new(x - 4, y + 8), new(x - 4, y), new(x, y - 4), new(x + 4, y), new(x + 4, y + 8));
        for (int k = -14; k < 14; k += 6) dc.FillRectangle(Brush(stone), new Rect(x + k, y - 11, 3, 3));
        // The banner on the keep.
        dc.DrawLine(Pen(dark, 1.2), new Point(x, y - 24), new Point(x, y - 40));
        uint flag = owner >= 0 ? Realm.Lords[owner].Field : 0xFFF0E8D8;
        var pts = new List<Point> { new(x, y - 40) };
        for (int k = 1; k <= 5; k++) pts.Add(new Point(x + k * 3.2, y - 40 + Math.Sin(_clock * 5 + k + x) * 1.2));
        for (int k = 5; k >= 1; k--) pts.Add(new Point(x + k * 3.2, y - 33 + Math.Sin(_clock * 5 + k + x) * 1.2));
        Poly(dc, flag, dark, 0.8, [.. pts]);
    }

    /// <summary>An army in the field: its lord's standard, his shield and its strength.</summary>
    private void DrawArmyToken(DrawingContext dc, Lord l, World w)
    {
        var art = MapArt.Shared;
        var (mx, my) = LabelAt(l.At);
        bool castle = Realm.Lords.Any(x => x.Home == l.At);
        var at = castle ? new Point(mx + 40, my - 24) : new Point(mx + 2, my - 34);
        bool mine = l.Id == w.Player;
        if (mine) dc.DrawEllipse(Brush(0x60FFE080), Pen(Gold, 1.6), at, 17, 17);
        // The standard's pole and its swallow-tailed banner.
        dc.DrawLine(Pen(0xFF3A2A18, 2), new Point(at.X - 10, at.Y + 12), new Point(at.X - 10, at.Y - 24));
        var pts = new List<Point> { new(at.X - 10, at.Y - 24) };
        for (int k = 1; k <= 6; k++) pts.Add(new Point(at.X - 10 + k * 3.4, at.Y - 24 + Math.Sin(_clock * 6 + k * 0.9 + l.Id) * 1.4));
        pts.Add(new Point(at.X + 6, at.Y - 18 + Math.Sin(_clock * 6 + 6 + l.Id)));
        for (int k = 6; k >= 1; k--) pts.Add(new Point(at.X - 10 + k * 3.4, at.Y - 12 + Math.Sin(_clock * 6 + k * 0.9 + l.Id) * 1.4));
        Poly(dc, Realm.Lords[l.Id].Field, 0xFF1A1008, 0.8, [.. pts]);
        DrawShield(dc, l.Id, new Point(at.X + 4, at.Y + 2), 20);
        var badge = new Rect(at.X - 14, at.Y + 13, 36, 15);
        dc.FillRectangle(Brush(mine ? 0xF0F8E8B0 : 0xE0F0E2BE), badge, 3);
        dc.DrawRectangle(null, Pen(mine ? Rubric : Ink, 1), badge, 3, 3);
        Text(dc, $"{l.Army.Strength}", badge.Center, 13, mine ? Rubric : Ink, FellSC, Align.Center);
    }

    private static void DrawCoins(DrawingContext dc, Point at)
    {
        for (int k = 2; k >= 0; k--)
        {
            var c = new Point(at.X - k * 3, at.Y + 4 - k * 4);
            dc.DrawEllipse(Brush(0xFFE8B838), Pen(0xFF7A5410, 1), c, 8, 4);
        }
    }

    // ───────────────────────────── little soldiers ─────────────────────────────

    /// <summary>A foot soldier with a spear and a kite shield, standing at <paramref name="at"/> (his feet).</summary>
    private static void DrawFootman(DrawingContext dc, Point at, double s, uint coat, bool facingLeft = false, double lean = 0)
    {
        double f = facingLeft ? -1 : 1;
        Point P(double x, double y) => new(at.X + (x + lean * -y * 0.3) * s * f, at.Y + y * s);
        if (s > 0.8) dc.DrawEllipse(Brush(0x40000000), null, new Point(at.X + 2 * s, at.Y + 1), 9 * s, 2.6 * s);
        dc.DrawLine(Pen(0xFF3A2A1A, 2.4 * s), P(-3, 0), P(-2, -14));
        dc.DrawLine(Pen(0xFF3A2A1A, 2.4 * s), P(3, 0), P(2, -14));
        Poly(dc, coat, 0xFF1A1008, 0.8, P(-6, -12), P(-5, -28), P(5, -28), P(6, -12));
        dc.DrawEllipse(Brush(Skin), null, P(0, -32), 4.2 * s, 4.2 * s);
        Poly(dc, Steel, 0xFF1A1008, 0.6, P(-5, -33), P(0, -39), P(5, -33));
        dc.DrawLine(Pen(0xFF6A4A2A, 1.4 * s), P(7, 4), P(7, -46));
        Poly(dc, 0xFFC8CCD4, null, 0, P(5.5, -46), P(7, -52), P(8.5, -46));
        Poly(dc, Pix.Scale(coat, 0.75), 0xFF1A1008, 0.8, P(-9, -26), P(-2, -26), P(-2, -14), P(-5.5, -8), P(-9, -14));
    }

    /// <summary>A mounted knight with a lance upright, his horse caparisoned in his colour.</summary>
    private static void DrawHorseman(DrawingContext dc, Point at, double s, uint coat, bool facingLeft = false, double gallop = 0)
    {
        double f = facingLeft ? -1 : 1;
        Point P(double x, double y) => new(at.X + x * s * f, at.Y + y * s);
        double l1 = Math.Sin(gallop) * 5, l2 = -l1;
        if (s > 0.8) dc.DrawEllipse(Brush(0x40000000), null, new Point(at.X, at.Y + 1), 24 * s, 3.5 * s);
        foreach (var (x, d) in new[] { (-12.0, l1), (-7.0, l2), (8.0, l2), (13.0, l1) })
            dc.DrawLine(Pen(0xFF3A2414, 2.6 * s), P(x, -14), P(x + d, 0));
        Poly(dc, 0xFF6A4228, 0xFF1A1008, 0.8, P(-18, -14), P(-16, -26), P(12, -28), P(20, -36), P(26, -34), P(22, -24), P(16, -14));
        Poly(dc, coat, 0xFF1A1008, 0.8, P(-19, -12), P(-17, -26), P(12, -26), P(16, -12));
        Poly(dc, coat, 0xFF1A1008, 0.8, P(-4, -26), P(-4, -42), P(6, -42), P(6, -26));
        dc.DrawEllipse(Brush(Steel), Pen(0xFF1A1008, 0.6), P(1, -46), 4.6 * s, 4.6 * s);
        dc.DrawLine(Pen(0xFF6A4A2A, 1.6 * s), P(9, -20), P(9, -64));
        Poly(dc, coat, null, 0, P(9, -64), P(18, -61), P(9, -58));
    }

    private static void DrawCatapultIcon(DrawingContext dc, Point at, double s)
    {
        Point P(double x, double y) => new(at.X + x * s, at.Y + y * s);
        dc.DrawLine(Pen(0xFF5A3A1E, 3 * s), P(-16, 0), P(16, 0));
        dc.DrawLine(Pen(0xFF5A3A1E, 3 * s), P(-6, 0), P(2, -16));
        dc.DrawLine(Pen(0xFF5A3A1E, 3 * s), P(10, 0), P(2, -16));
        dc.DrawLine(Pen(0xFF7A5230, 2.4 * s), P(-14, -4), P(14, -26));
        dc.DrawEllipse(Brush(0xFF6A6A6A), null, P(15, -28), 4 * s, 4 * s);
        foreach (var x in new[] { -12.0, 12.0 }) dc.DrawEllipse(Brush(0xFF3A2414), null, P(x, 2), 4 * s, 4 * s);
    }
}
