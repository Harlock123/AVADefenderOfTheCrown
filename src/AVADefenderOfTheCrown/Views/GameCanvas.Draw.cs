using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AVADefenderOfTheCrown.Engine;

namespace AVADefenderOfTheCrown.Views;

public enum Align { Left, Center, Right }

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Typeface Fell = new(new FontFamily("avares://AVADefenderOfTheCrown/Assets/Fonts#IM FELL English"));
    private static readonly Typeface FellSC = new(new FontFamily("avares://AVADefenderOfTheCrown/Assets/Fonts#IM FELL English SC"));
    private static readonly Typeface Gothic = new(new FontFamily("avares://AVADefenderOfTheCrown/Assets/Fonts#UnifrakturMaguntia"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        return p;
    }

    /// <summary>The colours of a manuscript: its ink, the red of its rubrics, gold leaf, lapis blue, and the parchment.</summary>
    private const uint Ink = 0xFF2A1A0E, Rubric = 0xFFA01E14, Gold = 0xFFE0B040, GoldDeep = 0xFF8A6418, Lapis = 0xFF1E3C8C, Vellum = 0xFFF0E2BE;
    private const uint Cream = 0xFFF4E8C8, Shadow = 0xC0000000;

    private static FormattedText Format(string text, double size, uint colour, Typeface? face = null) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face ?? FellSC, size, Brush(colour));

    /// <summary>A line of text, its middle at <paramref name="at"/>'s height.</summary>
    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, Typeface? face = null, Align align = Align.Left, uint shadow = 0)
    {
        var ft = Format(text, size, colour, face);
        double x = align == Align.Center ? at.X - ft.Width / 2 : align == Align.Right ? at.X - ft.Width : at.X;
        if (shadow != 0) dc.DrawText(Format(text, size, shadow, face), new Point(x + size * 0.06 + 0.8, at.Y - ft.Height / 2 + size * 0.06 + 0.8));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    private static double TextWidth(string text, double size, Typeface? face = null) => Format(text, size, 0xFF000000, face).Width;

    /// <summary>Words wrapped into a box; gives back the height they took.</summary>
    private static double Paragraph(DrawingContext dc, string text, Rect r, double size, uint colour, Typeface? face = null, TextAlignment align = TextAlignment.Left)
    {
        var ft = Format(text, size, colour, face ?? Fell);
        ft.MaxTextWidth = r.Width;
        ft.TextAlignment = align;
        ft.LineHeight = size * 1.18;
        dc.DrawText(ft, r.TopLeft);
        return ft.Height;
    }

    /// <summary>Lettering filled with a gradient (gold leaf) and outlined.</summary>
    private static void Lettering(DrawingContext dc, string text, Point at, double size, Typeface face, IBrush fill, uint outline, double width, Align align = Align.Center)
    {
        var ft = Format(text, size, 0xFF000000, face);
        double x = align == Align.Center ? at.X - ft.Width / 2 : align == Align.Right ? at.X - ft.Width : at.X;
        var geo = ft.BuildGeometry(new Point(x, at.Y - ft.Height / 2));
        if (geo == null) return;
        using (dc.PushTransform(Matrix.CreateTranslation(3, 4))) dc.DrawGeometry(Brush(0x90000000), null, geo);
        dc.DrawGeometry(fill, Pen(outline, width), geo);
    }

    private static IBrush GoldLeaf(double top, double bottom) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, top, RelativeUnit.Absolute),
        EndPoint = new RelativePoint(0, bottom, RelativeUnit.Absolute),
        GradientStops =
        {
            new GradientStop(Color.FromUInt32(0xFFFFF4C0), 0), new GradientStop(Color.FromUInt32(0xFFF0C050), 0.42),
            new GradientStop(Color.FromUInt32(0xFFA87418), 0.55), new GradientStop(Color.FromUInt32(0xFFE8B848), 0.8), new GradientStop(Color.FromUInt32(0xFFFFE8A0), 1),
        },
    };

    private static IBrush Vertical(uint top, uint bottom, Rect r) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, r.Top, RelativeUnit.Absolute),
        EndPoint = new RelativePoint(0, r.Bottom, RelativeUnit.Absolute),
        GradientStops = { new GradientStop(Color.FromUInt32(top), 0), new GradientStop(Color.FromUInt32(bottom), 1) },
    };

    private static void Poly(DrawingContext dc, uint fill, uint? edge, double width, params Point[] pts)
    {
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, width) : null, Shape(pts));
    }

    private static StreamGeometry Shape(params Point[] pts)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        c.BeginFigure(pts[0], true);
        foreach (var p in pts.Skip(1)) c.LineTo(p);
        c.EndFigure(true);
        return g;
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);

    /// <summary>A place on the screen that answers a click.</summary>
    private void Hit(Rect r, Action a) => _hits.Add((r, a));

    /// <summary>A sheet of parchment with an inked and gilded border, and knots at its corners.</summary>
    private static void Sheet(DrawingContext dc, Rect r, int seed = 1, bool gilded = true)
    {
        dc.FillRectangle(Brush(0x70000000), r.Translate(new Vector(6, 8)));
        dc.DrawImage(Art.Parchment((int)r.Width, (int)r.Height, seed), r);
        if (!gilded) return;
        var inner = r.Deflate(10);
        dc.DrawRectangle(null, Pen(Ink, 1.6), inner);
        dc.DrawRectangle(null, Pen(Gold, 2.4), inner.Deflate(4));
        dc.DrawRectangle(null, Pen(Rubric, 1), inner.Deflate(7.5));
        foreach (var c in new[] { inner.TopLeft, inner.TopRight, inner.BottomLeft, inner.BottomRight })
        {
            Poly(dc, Gold, Ink, 1.2, new(c.X, c.Y - 8), new(c.X + 8, c.Y), new(c.X, c.Y + 8), new(c.X - 8, c.Y));
            dc.DrawEllipse(Brush(Rubric), null, c, 2.6, 2.6);
        }
    }

    /// <summary>A button on parchment: inked, gold when chosen, its first letter in red.</summary>
    private void Button(DrawingContext dc, Rect r, string label, Action act, bool on = false, double size = 22, bool enabled = true, string? key = null)
    {
        if (on)
        {
            dc.FillRectangle(Vertical(0xFFF8DC80, 0xFFD0A040, r), r, 5);
            dc.DrawRectangle(null, Pen(GoldDeep, 1.6), r, 5, 5);
        }
        else
        {
            dc.FillRectangle(Brush(enabled ? 0x30FFF8E0u : 0x18000000u), r, 5);
            dc.DrawRectangle(null, Pen(enabled ? 0xA02A1A0E : 0x502A1A0E, 1.2), r, 5, 5);
        }
        uint ink = enabled ? Ink : 0x802A1A0E;
        double w = TextWidth(label[1..], size) + TextWidth(label[..1], size * 1.25, FellSC);
        double x = r.X + r.Width / 2 - w / 2;
        Text(dc, label[..1], new Point(x, r.Center.Y - 1), size * 1.25, enabled ? Rubric : 0x80A01E14);
        Text(dc, label[1..], new Point(x + TextWidth(label[..1], size * 1.25), r.Center.Y), size, ink);
        if (key != null) Text(dc, key, new Point(r.Right - 8, r.Center.Y), size * 0.72, enabled ? 0xB06A4A2A : 0x506A4A2A, Fell, Align.Right);
        if (enabled) Hit(r, act);
    }

    // ───────────────────────────── the screens ─────────────────────────────

    private void DrawScreen(DrawingContext dc)
    {
        _hits.Clear();
        var g = Engine;
        switch (g.State)
        {
            case GameState.Title:
                DrawTitle(dc);
                return;
            case GameState.Choose:
                DrawChoose(dc);
                return;
            case GameState.HighScores or GameState.EnterInitials:
                DrawHighScores(dc);
                return;
        }
        switch (g.Scene)
        {
            case Scene.Joust when g.Joust != null: DrawJoust(dc, g.Joust); break;
            case Scene.Siege when g.Siege != null: DrawSiege(dc, g.Siege); break;
            case Scene.Raid when g.Raid != null: DrawRaid(dc, g.Raid); break;
            case Scene.Battle when g.Battle != null: DrawBattle(dc, g.Battle); break;
            default: DrawMapScreen(dc); break;
        }
        if (g.Card is { } card) DrawCard(dc, card);
        if (g.State == GameState.Attract)
        {
            var band = new Rect(0, 708, ScreenWidth, 40);
            dc.FillRectangle(Brush(0xC0100808), band);
            Text(dc, "A DEMONSTRATION: THE COMPUTER RIDES FOR THE SAXONS  -  PRESS ENTER", band.Center, 22, Gold, align: Align.Center);
        }
        if (g.State == GameState.Paused) DrawPaused(dc);
        if (g.State == GameState.GameOver) DrawGameOver(dc);
    }

    // ───────────────────────────── the title ─────────────────────────────

    private void DrawTitle(DrawingContext dc)
    {
        var g = Engine;
        dc.DrawImage(Art.Title, new Rect(0, 0, ScreenWidth, ScreenHeight));
        DrawPennants(dc);
        DrawRiderSilhouette(dc, new Point(180, 642), 2.3);
        Text(dc, "A V A", new Point(512, 54), 28, 0xFFF0D890, FellSC, Align.Center, Shadow);
        Lettering(dc, "Defender of the Crown", new Point(512, 138), 98, Gothic, GoldLeaf(88, 190), 0xFF3A2208, 2.2);
        Text(dc, "AFTER CINEMAWARE'S 1986 TALE OF KNIGHTS, CASTLES AND A CROWN", new Point(512, 214), 20, 0xFFF4E4C0, FellSC, Align.Center, Shadow);
        const double cx = 330;
        var story = new Rect(cx - 250, 262, 500, 120);
        dc.FillRectangle(Brush(0x58100818), story.Inflate(new Thickness(16, 10)), 8);
        Paragraph(dc, "England, 1149. The king is dead without an heir, and the Norman lords reach for his crown. Ride for the Saxons: " +
                      "win the shires with the sword, the lance and the siege engine, and take the three Norman castles.",
            story, 21, 0xFFF8ECD0, Fell, TextAlignment.Center);
        double y = 412;
        Button(dc, new Rect(cx - 120, y, 240, 46), "NEW GAME", g.NewGame, on: true, size: 24);
        if (g.HasSave) Button(dc, new Rect(cx - 120, y + 58, 240, 46), "CONTINUE", () => g.Continue(), size: 24, key: "C");
        if ((int)(_clock * 1.6) % 2 == 0)
            Text(dc, "PRESS ENTER TO RIDE OUT", new Point(cx, g.HasSave ? y + 134 : y + 78), 22, 0xFFFFE8A0, FellSC, Align.Center, Shadow);
        var bar = new Rect(0, 690, ScreenWidth, 78);
        dc.FillRectangle(Brush(0xB0100A08), bar);
        Text(dc, "MOUSE: CLICK THE MAP AND THE ORDERS    ARROWS: CHOOSE, AIM    SPACE: FIRE    X: PARRY    P: PAUSE    M: SOUND",
            new Point(512, 712), 16, 0xFFE8D8B0, FellSC, Align.Center);
        Text(dc, "TITLE MUSIC: GREENSLEEVES, AN ENGLISH TUNE OF THE 1580S", new Point(512, 742), 16, 0xFFB0A080, FellSC, Align.Center);
    }

    /// <summary>The pennants on the title's castle, streaming in the wind.</summary>
    private void DrawPennants(DrawingContext dc)
    {
        double cx = Art.TitleCastleX, gy = Art.TitleCastleY, s = Art.TitleCastleScale;
        foreach (var (x, y) in new[] { (cx - 123 * s, gy - 97 * s), (cx + 123 * s, gy - 97 * s), (cx + 1 * s, gy - 165 * s) })
        {
            dc.DrawLine(Pen(0xFF1E1628, 2), new Point(x, y), new Point(x, y - 26));
            var pts = new List<Point> { new(x, y - 26) };
            for (int k = 1; k <= 9; k++) pts.Add(new Point(x + k * 4.5, y - 22 + Math.Sin(_clock * 6 + k * 0.8) * k * 0.5));
            for (int k = 9; k >= 1; k--) pts.Add(new Point(x + k * 4.5, y - 15 + Math.Sin(_clock * 6 + k * 0.8) * k * 0.5 - k * 0.4));
            Poly(dc, 0xFFC02A22, 0xFF1E1628, 0.8, [.. pts]);
        }
    }

    // ───────────────────────────── choosing a knight ─────────────────────────────

    public static Rect KnightCard(int i) => new(34 + i * 244, 128, 224, 548);

    private void DrawChoose(DrawingContext dc)
    {
        var g = Engine;
        dc.DrawImage(Art.Title, new Rect(0, 0, ScreenWidth, ScreenHeight));
        dc.FillRectangle(Brush(0x90100810), new Rect(0, 0, ScreenWidth, ScreenHeight));
        Lettering(dc, "Choose your Knight", new Point(512, 62), 62, Gothic, GoldLeaf(30, 95), 0xFF3A2208, 1.6);
        Text(dc, "FOUR SAXON LORDS STAND READY. THE THREE YOU DO NOT CHOOSE WILL RIDE FOR THEMSELVES.", new Point(512, 106), 18, 0xFFE8D8B0, FellSC, Align.Center, Shadow);
        for (int i = 0; i < Realm.SaxonCount; i++)
        {
            var r = KnightCard(i);
            var info = Realm.Lords[i];
            bool on = i == g.ChooseIndex;
            if (on) dc.FillRectangle(Brush(0x60FFD860), r.Inflate(6), 10);
            Sheet(dc, r, 10 + i);
            int k = i;
            Hit(r, () => g.Begin(k));
            // The portrait, in an arched frame.
            var frame = new Rect(r.X + 26, r.Y + 26, r.Width - 52, 210);
            DrawPortrait(dc, frame, i);
            Text(dc, info.Name, new Point(r.Center.X, r.Y + 258), info.Name.Length > 16 ? 19 : 21, Ink, FellSC, Align.Center);
            Text(dc, $"“{info.Motto}”", new Point(r.Center.X, r.Y + 284), 16, Rubric, Fell, Align.Center);
            double y = r.Y + 322;
            foreach (var (label, rating) in new[] { ("LEADERSHIP", info.Leadership), ("JOUSTING", info.Jousting), ("SWORDPLAY", info.Swordplay) })
            {
                Text(dc, label, new Point(r.X + 26, y), 17, Ink);
                for (int p = 0; p < 3; p++)
                {
                    var c = new Point(r.X + 32 + p * 22, y + 22);
                    Poly(dc, p < (int)rating ? Gold : 0x302A1A0E, Ink, 1, new(c.X, c.Y - 7), new(c.X + 6, c.Y), new(c.X, c.Y + 7), new(c.X - 6, c.Y));
                }
                Text(dc, Realm.RatingName(rating), new Point(r.Right - 26, y + 22), 17, rating == Rating.Strong ? Rubric : rating == Rating.Weak ? 0xFF6A5A4A : Lapis, align: Align.Right);
                y += 52;
            }
            Text(dc, $"SEAT: {info.CastleName.ToUpperInvariant()}", new Point(r.Center.X, r.Y + 480), 15, 0xFF5A4A3A, align: Align.Center);
            Text(dc, $"IN {Realm.Shires[info.Home].Name.ToUpperInvariant()}", new Point(r.Center.X, r.Y + 500), 15, 0xFF5A4A3A, align: Align.Center);
            Text(dc, $"PRESS {i + 1}", new Point(r.Center.X, r.Y + 524), 14, on ? Rubric : 0xFF8A7A6A, align: Align.Center);
        }
        Text(dc, "CLICK A KNIGHT, OR ARROWS AND ENTER (1 TO 4)    ESC: BACK", new Point(512, 726), 19, 0xFFE8D8B0, FellSC, Align.Center, Shadow);
    }

    // ───────────────────────────── the map ─────────────────────────────

    private static uint LordColour(int lord) => lord switch
    {
        // A black field washes the map too dark: its wash is a slate grey.
        >= 0 when Realm.Lords[lord].Field == 0xFF181818 => 0xFF4A4E62,
        >= 0 => Realm.Lords[lord].Field,
        Realm.Outlaws => 0xFF2E6A2A,
        _ => 0x00000000,
    };

    private void DrawMapScreen(DrawingContext dc)
    {
        var g = Engine;
        var w = g.World;
        var art = MapArt.Shared;
        var lit = g.Mode is MapMode.Conquer or MapMode.Raid or MapMode.GilRaid ? g.Candidates : [];
        art.UpdateWash(w.Owner, o => LordColour(o) is var c && c != 0 ? c : 0, lit.ToList(), g.Selected);
        var mapRect = new Rect(0, 0, MapArt.W, MapArt.H);
        dc.DrawImage(art.Base, mapRect);
        dc.DrawImage(art.Wash, mapRect);
        DrawMapDecor(dc);
        // The shires' names.
        for (int s = 0; s < Realm.Count; s++)
        {
            var (x, y) = LabelAt(s);
            bool castle = Realm.Lords.Any(l => l.Home == s);
            double ly = castle ? y + 24 : y;
            string name = Realm.Shires[s].Name.ToUpperInvariant();
            Text(dc, name, new Point(x, ly), 14, 0xFF2A1A0E, FellSC, Align.Center, 0x80F0E0C0);
            Text(dc, $"{Realm.Shires[s].Income}", new Point(x, ly + 13), 11, 0xFF6A3A1A, Fell, Align.Center);
        }
        // Castles, with their lords' banners; then the armies in the field.
        for (int i = 0; i < Realm.LordCount; i++)
        {
            int home = Realm.Lords[i].Home;
            var (x, y) = art.Middle[home];
            int owner = w.Owner[home];
            DrawCastleIcon(dc, new Point(x, y + 2), owner, !w.Lords[i].Alive);
        }
        foreach (var l in w.Lords)
            if (l.Alive && !l.Army.Empty) DrawArmyToken(dc, l, w);
        DrawGilCamp(dc, art.Middle[Realm.Sherwood]);
        // The highlighted choices pulse.
        foreach (int s in lit)
        {
            var (x, y) = art.Middle[s];
            double r = 10 + 3 * Math.Sin(_clock * 5);
            dc.DrawEllipse(null, Pen(0xC0FFF0A0, 2.5), new Point(x, y - 18), r, r);
        }
        dc.DrawRectangle(null, Pen(0xFF2A1A0E, 3), mapRect.Deflate(1.5));
        dc.DrawRectangle(null, Pen(Gold, 1.6), mapRect.Deflate(6));
        if (g.Message is { } msg)
        {
            var band = new Rect(20, MapArt.H - 52, MapArt.W - 40, 34);
            dc.FillRectangle(Brush(0xE0F0E2BE), band, 4);
            dc.DrawRectangle(null, Pen(Ink, 1.2), band, 4, 4);
            Text(dc, msg, band.Center, 18, Ink, FellSC, Align.Center);
        }
        DrawPanel(dc);
    }

    /// <summary>Where a shire's name goes: its middle, nudged where two small shires crowd each other.</summary>
    private static (double X, double Y) LabelAt(int s)
    {
        var (x, y) = MapArt.Shared.Middle[s];
        return s switch
        {
            Realm.Sherwood => (x + 12, y - 30),
            Realm.Derby => (x - 12, y - 4),
            Realm.Lancaster => (x - 4, y - 8),
            _ => (x, y),
        };
    }

    /// <summary>A knight on horseback in silhouette, lance raised with a pennant, on the title's near hill.</summary>
    private void DrawRiderSilhouette(DrawingContext dc, Point at, double s)
    {
        const uint ink = 0xFF0C0A10;
        Point P(double x, double y) => new(at.X + x * s, at.Y + y * s);
        var horse = new StreamGeometry();
        using (var c = horse.Open())
        {
            // From the withers up the crest to the ears, down the face to the muzzle, back under the jaw, down the
            // chest, along the belly, round the rump and along the back.
            c.BeginFigure(P(10, -36), true);
            c.CubicBezierTo(P(20, -40), P(30, -48), P(38, -54));
            c.LineTo(P(39, -62));
            c.LineTo(P(43, -55));
            c.CubicBezierTo(P(48, -52), P(56, -44), P(60, -39));
            c.CubicBezierTo(P(61, -36), P(57, -34), P(54, -35));
            c.CubicBezierTo(P(49, -37), P(45, -39), P(42, -38));
            c.CubicBezierTo(P(40, -33), P(37, -27), P(33, -20));
            c.CubicBezierTo(P(20, -12), P(-10, -12), P(-28, -14));
            c.CubicBezierTo(P(-36, -16), P(-40, -24), P(-37, -31));
            c.CubicBezierTo(P(-33, -37), P(-14, -37), P(10, -36));
            c.EndFigure(true);
        }
        dc.DrawGeometry(Brush(ink), null, horse);
        // Legs, one forefoot raised as it walks.
        foreach (var (x0, x1, y1, lift) in new[] { (-30.0, -32.0, 0.0, 0.0), (-22.0, -18.0, 0.0, 0.0), (22.0, 20.0, 0.0, 0.0), (28.0, 38.0, -8.0, 1.0) })
        {
            dc.DrawLine(Pen(ink, 3.4 * s), P(x0, -16), P((x0 + x1) / 2 + lift * 6, -8 + lift * -2));
            dc.DrawLine(Pen(ink, 2.6 * s), P((x0 + x1) / 2 + lift * 6, -8 + lift * -2), P(x1, y1));
        }
        // The tail, the caparison's scalloped hem.
        var tail = new StreamGeometry();
        using (var c = tail.Open())
        {
            c.BeginFigure(P(-37, -30), true);
            c.CubicBezierTo(P(-46, -26), P(-48, -14), P(-44, -6));
            c.CubicBezierTo(P(-44, -14), P(-42, -22), P(-36, -24));
            c.EndFigure(true);
        }
        dc.DrawGeometry(Brush(ink), null, tail);
        for (int k = 0; k < 7; k++) dc.DrawEllipse(Brush(ink), null, P(-28 + k * 9, -12), 5 * s, 3 * s);
        // The rider: leg, body, helm with its crest, shield on his back, lance and pennant.
        Poly(dc, ink, null, 0, P(-2, -34), P(-6, -58), P(6, -60), P(10, -34));
        dc.DrawLine(Pen(ink, 3 * s), P(4, -34), P(8, -20));
        dc.DrawGeometry(Brush(ink), null, new RectangleGeometry(new Rect(P(-4, -72), P(7, -60)), 2, 2));
        Poly(dc, ink, null, 0, P(0, -72), P(-6, -78), P(4, -76));
        dc.DrawGeometry(Brush(ink), null, ShieldShape(P(-7, -48), 16 * s));
        dc.DrawLine(Pen(ink, 1.6 * s), P(10, -44), P(34, -118));
        var pts = new List<Point> { P(34, -118) };
        for (int k = 1; k <= 8; k++) pts.Add(P(34 + k * 3, -117 + k * 0.4 + Math.Sin(_clock * 5 + k) * 0.8));
        for (int k = 8; k >= 1; k--) pts.Add(P(34 + k * 3, -111 + Math.Sin(_clock * 5 + k) * 0.8));
        Poly(dc, 0xFF8A1A1A, ink, 0.6, [.. pts]);
        // The last of the sunset on his back and his horse's.
        dc.DrawLine(Pen(0x70F0A060, 1.4), P(-4, -59), P(6, -61));
        dc.DrawLine(Pen(0x60F0A060, 1.4), P(-24, -36), P(20, -35));
    }

    /// <summary>The cartouche, the compass rose and the seas' names.</summary>
    private void DrawMapDecor(DrawingContext dc)
    {
        var w = Engine.World;
        var cart = new Rect(424, 26, 182, 104);
        dc.FillRectangle(Brush(0xE8F0E2BE), cart, 6);
        dc.DrawRectangle(null, Pen(Ink, 1.4), cart, 6, 6);
        dc.DrawRectangle(null, Pen(Gold, 1.6), cart.Deflate(4), 4, 4);
        Text(dc, "Anglia", new Point(cart.Center.X, cart.Y + 32), 36, Rubric, Gothic, Align.Center);
        Text(dc, w.Season, new Point(cart.Center.X, cart.Y + 64), 17, Ink, FellSC, Align.Center);
        Text(dc, $"ANNO DOMINI {w.Year}", new Point(cart.Center.X, cart.Y + 84), 15, Ink, FellSC, Align.Center);
        // The compass rose out in the Irish Sea.
        var c = new Point(76, 330);
        for (int k = 0; k < 8; k++)
        {
            double a = k * Math.PI / 4 - Math.PI / 2, len = k % 2 == 0 ? 34 : 20, side = k % 2 == 0 ? 7 : 5;
            var tip = new Point(c.X + Math.Cos(a) * len, c.Y + Math.Sin(a) * len);
            var l = new Point(c.X + Math.Cos(a - Math.PI / 2) * side, c.Y + Math.Sin(a - Math.PI / 2) * side);
            var r = new Point(c.X + Math.Cos(a + Math.PI / 2) * side, c.Y + Math.Sin(a + Math.PI / 2) * side);
            Poly(dc, k % 2 == 0 ? 0xFFF0E2BE : Gold, Ink, 1, c, l, tip);
            Poly(dc, k % 2 == 0 ? Rubric : GoldDeep, Ink, 1, c, tip, r);
        }
        dc.DrawEllipse(null, Pen(Ink, 1), c, 24, 24);
        Text(dc, "N", new Point(c.X, c.Y - 44), 16, Ink, FellSC, Align.Center);
        Text(dc, "The North Sea", new Point(520, 300), 19, 0xA0E8F0E8, Fell, Align.Center);
        Text(dc, "The Irish Sea", new Point(96, 400), 16, 0xA0E8F0E8, Fell, Align.Center);
        Text(dc, "WALES", new Point(170, 515), 16, 0x904A3A2A, FellSC, Align.Center);
        Text(dc, "SCOTLAND", new Point(170, 40), 16, 0x904A3A2A, FellSC, Align.Center);
    }

    private void DrawGilCamp(DrawingContext dc, (double X, double Y) at)
    {
        var p = new Point(at.X + 2, at.Y - 18);
        // A longbow and an arrow, crossed.
        var bow = new StreamGeometry();
        using (var c = bow.Open())
        {
            c.BeginFigure(new Point(p.X - 9, p.Y - 10), false);
            c.QuadraticBezierTo(new Point(p.X + 9, p.Y), new Point(p.X - 9, p.Y + 10));
            c.EndFigure(false);
        }
        dc.DrawGeometry(null, Pen(0xFF4A2A10, 2.2), bow);
        dc.DrawLine(Pen(0xFFE8E0C8, 0.8), new Point(p.X - 9, p.Y - 10), new Point(p.X - 9, p.Y + 10));
        dc.DrawLine(Pen(0xFF3A2A18, 1.4), new Point(p.X - 12, p.Y + 6), new Point(p.X + 10, p.Y - 6));
        Poly(dc, 0xFF8A8A8A, 0xFF2A2A2A, 0.6, new(p.X + 12, p.Y - 7), new(p.X + 7, p.Y - 7), new(p.X + 10, p.Y - 3));
    }

    // ───────────────────────────── the lord's parchment ─────────────────────────────

    public const double PanelX = 624, PanelW = 400;
    public static Rect OrderRect(int i) => new(PanelX + 36, 318 + i * 39, PanelW - 72, 34);

    private void DrawPanel(DrawingContext dc)
    {
        var g = Engine;
        var w = g.World;
        var me = w.Me;
        var info = Realm.Lords[w.Player];
        var r = new Rect(PanelX, 0, PanelW, ScreenHeight);
        dc.DrawImage(Art.Parchment((int)PanelW, (int)ScreenHeight, 5), r);
        dc.DrawRectangle(null, Pen(Ink, 1.6), r.Deflate(8));
        dc.DrawRectangle(null, Pen(Gold, 2), r.Deflate(12));
        DrawShield(dc, w.Player, new Point(PanelX + 62, 70), 82);
        Text(dc, info.Name, new Point(PanelX + 112, 46), info.Name.Length > 16 ? 24 : 27, Ink, FellSC);
        Text(dc, $"LORD OF {info.CastleName.ToUpperInvariant()}", new Point(PanelX + 112, 74), 15, 0xFF5A4A3A, FellSC);
        Text(dc, $"{w.Season} {w.Year}  -  SEASON {w.Turn}", new Point(PanelX + 112, 98), 15, Rubric, FellSC);
        // The treasury and the armies.
        double y = 140;
        Rule(dc, y - 14);
        Text(dc, "GOLD", new Point(PanelX + 30, y), 18, Ink);
        DrawCoins(dc, new Point(PanelX + 104, y));
        Text(dc, $"{me.Gold}", new Point(PanelX + 122, y), 24, Rubric);
        int pay = Sim.Upkeep(me.Army);
        Text(dc, $"+{Sim.Income(w, w.Player)} TAXES" + (pay > 0 ? $"  -{pay} PAY" : "") + " A SEASON", new Point(PanelX + 372, y), 15, 0xFF5A4A3A, align: Align.Right);
        y += 32;
        string where = me.Army.Empty ? "NONE IN THE FIELD" : $"IN {Realm.Shires[me.At].Name.ToUpperInvariant()}";
        Text(dc, $"ARMY  ({where})", new Point(PanelX + 30, y), 17, Ink);
        y += 26;
        TroopLine(dc, y, me.Army, true);
        y += 30;
        Text(dc, "GARRISON", new Point(PanelX + 30, y), 17, Ink);
        y += 26;
        TroopLine(dc, y, me.Garrison, false);
        y += 30;
        Text(dc, $"SHIRES {w.LandCount(w.Player)}   RENOWN {w.Renown}   OUTLAW'S HELP {w.GilCalls}{(w.GilAid ? " (ARCHERS)" : "")}", new Point(PanelX + 30, y), 15, 0xFF5A4A3A);
        Rule(dc, 298);
        switch (g.Mode)
        {
            case MapMode.Buy: DrawBuy(dc); break;
            case MapMode.Garrison: DrawMuster(dc); break;
            case MapMode.Tournament: DrawChoice(dc, "HOLD A TOURNAMENT", "FOR LAND", "Joust a lord for one of his shires. Lose, and he takes one of yours.",
                "FOR FAME", "Joust for a purse of gold and renown; knights flock to a champion.", g.HoldTournament); break;
            case MapMode.Gil: DrawChoice(dc, "SEND TO SHERWOOD", "RAID A CASTLE", $"{Realm.OutlawName}'s men rob a lord's tax wagons: half his gold is yours.",
                "ARCHERS", "His archers march with your army, and loose a deadly volley in its next battle.", g.CallGil); break;
            case MapMode.Conquer or MapMode.Raid or MapMode.GilRaid: DrawPicking(dc); break;
            default: DrawOrders(dc); break;
        }
        DrawShireInfo(dc);
    }

    private static void Rule(DrawingContext dc, double y)
    {
        dc.DrawLine(Pen(0x802A1A0E, 1), new Point(PanelX + 26, y), new Point(PanelX + PanelW - 26, y));
        Poly(dc, Rubric, null, 0, new(PanelX + PanelW / 2, y - 4), new(PanelX + PanelW / 2 + 4, y), new(PanelX + PanelW / 2, y + 4), new(PanelX + PanelW / 2 - 4, y));
    }

    private static void TroopLine(DrawingContext dc, double y, Troops t, bool catapults)
    {
        double x = PanelX + 40;
        DrawFootman(dc, new Point(x, y + 10), 0.55, 0xFF3A4A8A);
        Text(dc, $"{t.Soldiers}", new Point(x + 14, y), 21, Ink, Fell);
        x += 74;
        DrawHorseman(dc, new Point(x, y + 10), 0.5, 0xFF8A2A2A);
        Text(dc, $"{t.Knights}", new Point(x + 16, y), 21, Ink, Fell);
        x += 70;
        if (catapults)
        {
            DrawCatapultIcon(dc, new Point(x, y + 8), 0.5);
            Text(dc, $"{t.Catapults}", new Point(x + 14, y), 21, Ink, Fell);
        }
        Text(dc, $"STRENGTH {t.Strength}", new Point(PanelX + 372, y), 15, 0xFF6A5A4A, align: Align.Right);
    }

    private void DrawOrders(DrawingContext dc)
    {
        var g = Engine;
        for (int i = 0; i < 7; i++)
        {
            var o = (Order)i;
            int k = i;
            Button(dc, OrderRect(i), GameEngine.OrderName(o), () => g.DoOrder((Order)k), on: g.MenuIndex == i, size: 20, enabled: g.CanOrder(o), key: $"{i + 1}");
        }
    }

    private void DrawPicking(DrawingContext dc)
    {
        var g = Engine;
        string title = g.Mode switch { MapMode.Conquer => "GO CONQUERING", MapMode.Raid => "GO RAIDING", _ => "THE OUTLAWS' RAID" };
        string help = g.Mode == MapMode.Conquer
            ? "Click a glowing shire next to your army to march into it. A shire of your own, or one left undefended, is yours for the walking; anywhere else, there will be a battle, and a castle must be besieged first."
            : g.Mode == MapMode.Raid
                ? "Click a castle to slip into it by night and fight your way to its strongroom, sword in hand." + (g.World.Lady == LadyState.Held ? $" {Realm.LadyName} is held in {Realm.Lords[g.World.LadyHeldBy].CastleName}." : "")
                : "Click the castle whose tax wagons Gil's men should waylay.";
        Text(dc, title, new Point(PanelX + PanelW / 2, 324), 24, Rubric, FellSC, Align.Center);
        Paragraph(dc, help, new Rect(PanelX + 36, 346, PanelW - 72, 140), 18, Ink);
        Text(dc, "ARROWS AND ENTER ALSO CHOOSE", new Point(PanelX + PanelW / 2, 500), 14, 0xFF6A5A4A, align: Align.Center);
        Button(dc, new Rect(PanelX + 120, 516, 160, 34), "BACK", g.Cancel, size: 18);
    }

    private void DrawChoice(DrawingContext dc, string title, string a, string aText, string b, string bText, Action<bool> pick)
    {
        var g = Engine;
        Text(dc, title, new Point(PanelX + PanelW / 2, 324), 24, Rubric, FellSC, Align.Center);
        Button(dc, new Rect(PanelX + 36, 348, PanelW - 72, 36), a, () => pick(true), on: g.Row == 0, size: 20, key: "1");
        Paragraph(dc, aText, new Rect(PanelX + 44, 390, PanelW - 88, 50), 16, Ink);
        Button(dc, new Rect(PanelX + 36, 440, PanelW - 72, 36), b, () => pick(false), on: g.Row == 1, size: 20, key: "2");
        Paragraph(dc, bText, new Rect(PanelX + 44, 482, PanelW - 88, 50), 16, Ink);
        Button(dc, new Rect(PanelX + 120, 534, 160, 32), "BACK", g.Cancel, size: 18);
    }

    private void DrawBuy(DrawingContext dc)
    {
        var g = Engine;
        Text(dc, "BUY ARMY", new Point(PanelX + PanelW / 2, 322), 24, Rubric, FellSC, Align.Center);
        string[] names = ["SOLDIERS", "KNIGHTS", "CATAPULTS"];
        int[] counts = [g.BuySoldiers, g.BuyKnights, g.BuyCatapults];
        int[] prices = [Realm.SoldierCost, Realm.KnightCost, Realm.CatapultCost];
        for (int i = 0; i < 3; i++)
        {
            double y = 356 + i * 40;
            int k = i;
            if (g.Row == i) dc.FillRectangle(Brush(0x40E0B040), new Rect(PanelX + 30, y - 16, PanelW - 60, 32), 4);
            Text(dc, names[i], new Point(PanelX + 40, y), 19, Ink);
            Text(dc, $"{prices[i]} GOLD", new Point(PanelX + 160, y), 15, 0xFF6A5A4A);
            Button(dc, new Rect(PanelX + 236, y - 14, 30, 28), "-", () => g.AdjustBuy(k, -1), size: 20);
            Text(dc, $"{counts[i]}", new Point(PanelX + 296, y), 21, Rubric, align: Align.Center);
            Button(dc, new Rect(PanelX + 326, y - 14, 30, 28), "+", () => g.AdjustBuy(k, 1), size: 20);
        }
        double yd = 476;
        if (g.Row == 3) dc.FillRectangle(Brush(0x40E0B040), new Rect(PanelX + 30, yd - 16, PanelW - 60, 32), 4);
        Text(dc, "TO", new Point(PanelX + 40, yd), 19, Ink);
        Button(dc, new Rect(PanelX + 90, yd - 15, 130, 30), "THE ARMY", () => g.BuyToGarrison = false, on: !g.BuyToGarrison, size: 17);
        Button(dc, new Rect(PanelX + 228, yd - 15, 130, 30), "GARRISON", () => g.BuyToGarrison = true, on: g.BuyToGarrison, size: 17);
        Text(dc, $"COST {g.BuyCost} OF {g.World.Me.Gold} GOLD", new Point(PanelX + PanelW / 2, 516), 18, g.BuyCost > g.World.Me.Gold ? Rubric : Ink, align: Align.Center);
        Button(dc, new Rect(PanelX + 50, 536, 140, 34), "BUY", g.ConfirmBuy, on: true, size: 20, enabled: g.BuyCost > 0);
        Button(dc, new Rect(PanelX + 210, 536, 140, 34), "BACK", g.Cancel, size: 20);
    }

    private void DrawMuster(DrawingContext dc)
    {
        var g = Engine;
        var me = g.World.Me;
        Text(dc, "MUSTER THE GARRISON", new Point(PanelX + PanelW / 2, 322), 24, Rubric, FellSC, Align.Center);
        double yd = 356;
        if (g.Row == 2) dc.FillRectangle(Brush(0x40E0B040), new Rect(PanelX + 30, yd - 16, PanelW - 60, 32), 4);
        Button(dc, new Rect(PanelX + 40, yd - 15, 156, 30), "TO THE WALLS", () => { if (!g.MoveToGarrison) g.AdjustMove(2, 1); }, on: g.MoveToGarrison, size: 16);
        Button(dc, new Rect(PanelX + 204, yd - 15, 156, 30), "TO THE ARMY", () => { if (g.MoveToGarrison) g.AdjustMove(2, 1); }, on: !g.MoveToGarrison, size: 16);
        var from = g.MoveToGarrison ? me.Army : me.Garrison;
        string[] names = ["SOLDIERS", "KNIGHTS"];
        int[] counts = [g.MoveSoldiers, g.MoveKnights];
        int[] have = [from.Soldiers, from.Knights];
        for (int i = 0; i < 2; i++)
        {
            double y = 400 + i * 40;
            int k = i;
            if (g.Row == i) dc.FillRectangle(Brush(0x40E0B040), new Rect(PanelX + 30, y - 16, PanelW - 60, 32), 4);
            Text(dc, names[i], new Point(PanelX + 40, y), 19, Ink);
            Text(dc, $"OF {have[i]}", new Point(PanelX + 160, y), 15, 0xFF6A5A4A);
            Button(dc, new Rect(PanelX + 236, y - 14, 30, 28), "-", () => g.AdjustMove(k, -1), size: 20);
            Text(dc, $"{counts[i]}", new Point(PanelX + 296, y), 21, Rubric, align: Align.Center);
            Button(dc, new Rect(PanelX + 326, y - 14, 30, 28), "+", () => g.AdjustMove(k, 1), size: 20);
        }
        Paragraph(dc, "Men pass between the army and the castle only while the army is at home.", new Rect(PanelX + 40, 470, PanelW - 80, 50), 16, 0xFF5A4A3A);
        Button(dc, new Rect(PanelX + 50, 536, 140, 34), "MUSTER", g.ConfirmMove, on: true, size: 20, enabled: g.MoveSoldiers + g.MoveKnights > 0);
        Button(dc, new Rect(PanelX + 210, 536, 140, 34), "BACK", g.Cancel, size: 20);
    }

    /// <summary>The shire the mouse is over (or chosen): whose it is, what it pays and who holds it; else the lords of the realm.</summary>
    private void DrawShireInfo(DrawingContext dc)
    {
        var g = Engine;
        var w = g.World;
        var r = new Rect(PanelX + 26, 594, PanelW - 52, 150);
        Rule(dc, r.Y - 8);
        int s = g.Selected;
        if (s < 0)
        {
            // The lords of the realm, with their lands.
            for (int i = 0; i < Realm.LordCount; i++)
            {
                var l = w.Lords[i];
                double x = r.X + 24 + i * 49, ly = r.Y + 40;
                using (dc.PushOpacity(l.Alive ? 1 : 0.35)) DrawShield(dc, i, new Point(x, ly), 40);
                if (!l.Alive) dc.DrawLine(Pen(Rubric, 3), new Point(x - 14, ly - 16), new Point(x + 14, ly + 16));
                Text(dc, $"{w.LandCount(i)}", new Point(x, ly + 34), 17, i == w.Player ? Rubric : Ink, align: Align.Center);
                Text(dc, Realm.Lords[i].Short.Split(' ')[^1][..3], new Point(x, ly + 54), 12, 0xFF5A4A3A, align: Align.Center);
            }
            Text(dc, "THE LORDS OF THE REALM AND THEIR SHIRES", new Point(r.Center.X, r.Bottom - 22), 13, 0xFF6A5A4A, align: Align.Center);
            return;
        }
        var shire = Realm.Shires[s];
        int owner = w.Owner[s];
        Text(dc, shire.Name.ToUpperInvariant(), new Point(r.X + 4, r.Y + 14), 22, Rubric);
        Text(dc, $"PAYS {shire.Income} GOLD A SEASON", new Point(r.Right - 4, r.Y + 14), 15, Ink, align: Align.Right);
        string whose = owner switch
        {
            Realm.Outlaws => $"THE GREENWOOD OF {Realm.OutlawName.ToUpperInvariant()}",
            Realm.Nobody => "UNCLAIMED: ITS OWN VASSALS HOLD IT",
            _ when owner == w.Player => "YOURS",
            _ => Realm.Lords[owner].Name.ToUpperInvariant(),
        };
        if (owner >= 0) DrawShield(dc, owner, new Point(r.X + 18, r.Y + 54), 34);
        Text(dc, whose, new Point(r.X + (owner >= 0 ? 44 : 4), r.Y + 50), 16, Ink);
        var castle = w.CastleOf(s);
        double y = r.Y + 82;
        if (castle != null)
        {
            Text(dc, $"{castle.Info.CastleName.ToUpperInvariant()}: GARRISON {castle.Garrison.Soldiers} + {castle.Garrison.Knights} KNIGHTS", new Point(r.X + 4, y), 15, Ink);
            y += 22;
        }
        if (w.ArmyIn(s) is { } army)
        {
            Text(dc, $"{army.Info.Short}'S ARMY: {army.Army.Soldiers} + {army.Army.Knights} KNIGHTS" + (army.Army.Catapults > 0 ? $" + {army.Army.Catapults} CAT." : ""), new Point(r.X + 4, y), 15, army.Id == w.Player ? Lapis : Rubric);
            y += 22;
        }
        if (owner != w.Player && owner != Realm.Outlaws && castle == null && w.ArmyIn(s) == null)
            Text(dc, $"DEFENDED BY {Sim.Defenders(w, s, w.Player).Troops.Strength} STRENGTH OF VASSALS", new Point(r.X + 4, y), 15, 0xFF5A4A3A);
        if (Sim.HoldsLady(w, castle?.Id ?? -9)) Text(dc, $"{Realm.LadyName.ToUpperInvariant()} IS HELD HERE", new Point(r.X + 4, r.Bottom - 12), 15, Rubric);
    }

    // ───────────────────────────── cards ─────────────────────────────

    private void DrawCard(DrawingContext dc, Card card)
    {
        var g = Engine;
        double cx = g.Scene == Scene.Card && g.Joust == null && g.Battle == null ? MapArt.W / 2.0 : 512;
        var r = new Rect(cx - 286, 196, 572, 360);
        dc.FillRectangle(Brush(0x60000000), new Rect(0, 0, ScreenWidth, ScreenHeight));
        Sheet(dc, r, 21);
        Hit(new Rect(0, 0, ScreenWidth, ScreenHeight), g.DismissCard);
        // The drop cap: a gilded square with the first letter in red.
        var cap = new Rect(r.X + 34, r.Y + 38, 74, 74);
        dc.FillRectangle(card.Kind is CardKind.War or CardKind.Loss or CardKind.Defeat ? Brush(0xFF2A2A6A) : Brush(Lapis), cap);
        dc.DrawRectangle(null, Pen(Gold, 3), cap);
        for (int k = 0; k < 6; k++)
            dc.DrawEllipse(Brush(0x60F0D080), null, new Point(cap.X + 8 + k * 11.5, cap.Bottom - 8), 2, 2);
        Lettering(dc, card.Title[..1], cap.Center, 66, Gothic, GoldLeaf(cap.Top, cap.Bottom), 0xFF4A2A08, 1.2);
        // The title, the arms and the tale.
        double titleSize = Math.Min(28, 28 * (r.Width - 122 - 120) / Math.Max(1, TextWidth(card.Title, 28)));
        Text(dc, card.Title, new Point(r.X + 122, r.Y + 60), titleSize, Rubric, FellSC);
        Text(dc, $"{g.World.Date}", new Point(r.X + 122, r.Y + 94), 16, 0xFF6A5A4A, FellSC);
        if (card.Lord >= 0 || card.Lord == Realm.Outlaws) DrawShield(dc, card.Lord, new Point(r.Right - 74, r.Y + 84), 80);
        Paragraph(dc, card.Text, new Rect(r.X + 40, r.Y + 134, r.Width - 80, 170), 22, Ink, Fell);
        // A flourish, and how to go on.
        dc.DrawLine(Pen(Gold, 2), new Point(r.X + 150, r.Bottom - 48), new Point(r.Right - 150, r.Bottom - 48));
        Poly(dc, Rubric, Ink, 1, new(r.Center.X, r.Bottom - 55), new(r.Center.X + 7, r.Bottom - 48), new(r.Center.X, r.Bottom - 41), new(r.Center.X - 7, r.Bottom - 48));
        if (g.State == GameState.Playing) Text(dc, "CLICK OR PRESS SPACE TO GO ON", new Point(r.Center.X, r.Bottom - 28), 15, 0xFF6A5A4A, align: Align.Center);
    }

    // ───────────────────────────── the end, the table, the pause ─────────────────────────────

    private void DrawPaused(DrawingContext dc)
    {
        dc.FillRectangle(Brush(0x90000000), new Rect(0, 0, ScreenWidth, ScreenHeight));
        var r = new Rect(312, 300, 400, 160);
        Sheet(dc, r, 31);
        Lettering(dc, "Paused", new Point(512, 360), 58, Gothic, GoldLeaf(330, 390), 0xFF3A2208, 1.4);
        Text(dc, "P OR ESC TO RIDE ON", new Point(512, 420), 19, Ink, align: Align.Center);
    }

    private void DrawGameOver(DrawingContext dc)
    {
        var g = Engine;
        var w = g.World;
        dc.FillRectangle(Brush(0xA0000000), new Rect(0, 0, ScreenWidth, ScreenHeight));
        var r = new Rect(212, 220, 600, 300);
        Sheet(dc, r, 41);
        Lettering(dc, w.Won ? "Long live the King!" : "The Saxon cause is lost", new Point(512, 290), w.Won ? 58 : 50, Gothic, GoldLeaf(260, 320), 0xFF3A2208, 1.4);
        Text(dc, $"{Realm.Lords[w.Player].Name.ToUpperInvariant()}, {w.Date}", new Point(512, 350), 20, Ink, align: Align.Center);
        Text(dc, $"{w.LandCount(w.Player)} SHIRE{(w.LandCount(w.Player) == 1 ? "" : "S")}   {w.Me.Gold} GOLD   {w.NormansBeaten} NORMAN{(w.NormansBeaten == 1 ? "" : "S")} BEATEN   {w.Turn} SEASON{(w.Turn == 1 ? "" : "S")}", new Point(512, 390), 18, Ink, align: Align.Center);
        if (w.Won) DrawCrown(dc, new Point(512, 226), 1.2);
        Text(dc, $"SCORE {g.Score:N0}", new Point(512, 440), 34, Rubric, align: Align.Center);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        dc.DrawImage(Art.Title, new Rect(0, 0, ScreenWidth, ScreenHeight));
        dc.FillRectangle(Brush(0x80100810), new Rect(0, 0, ScreenWidth, ScreenHeight));
        var r = new Rect(222, 60, 580, 650);
        Sheet(dc, r, 51);
        Lettering(dc, "The Roll of Honour", new Point(512, 124), 54, Gothic, GoldLeaf(95, 155), 0xFF3A2208, 1.4);
        dc.DrawLine(Pen(Gold, 2), new Point(300, 168), new Point(724, 168));
        if (g.State == GameState.EnterInitials && g.Initials is { } entry)
        {
            Text(dc, "YOUR NAME SHALL BE WRITTEN IN THE ROLL", new Point(512, 230), 22, Ink, align: Align.Center);
            Text(dc, $"SCORE {g.Score:N0}", new Point(512, 270), 30, Rubric, align: Align.Center);
            for (int i = 0; i < HighScoreTable.InitialsLength; i++)
            {
                var box = new Rect(420 + i * 66, 320, 56, 72);
                dc.FillRectangle(Brush(i == entry.Cursor ? 0x50E0B040u : 0x20000000u), box, 4);
                dc.DrawRectangle(null, Pen(i == entry.Cursor ? Rubric : Ink, i == entry.Cursor ? 2.5 : 1.2), box, 4, 4);
                Text(dc, entry.Initials[i].ToString(), box.Center, 50, Ink, FellSC, Align.Center);
            }
            Paragraph(dc, "Type your initials, or use up and down to choose each letter and right to go on. Enter when done.",
                new Rect(300, 420, 424, 80), 19, 0xFF5A4A3A, Fell, TextAlignment.Center);
            DrawShield(dc, g.World.Player, new Point(512, 580), 150);
            if (g.World.Won) DrawCrown(dc, new Point(512, 488), 1);
            return;
        }
        double y = 210;
        Text(dc, "KNIGHT", new Point(330, y - 22), 15, 0xFF6A5A4A);
        Text(dc, "SCORE", new Point(560, y - 22), 15, 0xFF6A5A4A, align: Align.Right);
        Text(dc, "SEASONS", new Point(690, y - 22), 15, 0xFF6A5A4A, align: Align.Right);
        for (int i = 0; i < g.HighScores.Entries.Count; i++)
        {
            var e = g.HighScores.Entries[i];
            uint c = i == g.NewEntryIndex ? Rubric : Ink;
            Text(dc, ToRoman(i + 1) + ".", new Point(316, y + i * 44), 22, Rubric, Fell, Align.Right);
            Text(dc, e.Initials, new Point(334, y + i * 44), 28, c, FellSC);
            Text(dc, $"{e.Score:N0}", new Point(560, y + i * 44), 24, c, align: Align.Right);
            Text(dc, $"{e.Turns}", new Point(690, y + i * 44), 22, c, align: Align.Right);
        }
        Text(dc, "PRESS ENTER FOR A NEW CAMPAIGN", new Point(512, 668), 19, (int)(_clock * 1.6) % 2 == 0 ? Rubric : Ink, align: Align.Center);
    }

    /// <summary>A crown of gold: its band set with jewels, five points topped with pearls and fleurs.</summary>
    private static void DrawCrown(DrawingContext dc, Point c, double s)
    {
        Point P(double x, double y) => new(c.X + x * s, c.Y + y * s);
        var g = Shape(P(-56, 24), P(-60, -18), P(-38, 4), P(-20, -26), P(0, 0), P(20, -26), P(38, 4), P(60, -18), P(56, 24));
        dc.DrawGeometry(GoldLeaf(c.Y - 30 * s, c.Y + 26 * s), Pen(0xFF5A3A08, 2), g);
        dc.DrawGeometry(GoldLeaf(c.Y + 12 * s, c.Y + 34 * s), Pen(0xFF5A3A08, 2), new RectangleGeometry(new Rect(P(-58, 14), P(58, 34)), 3, 3));
        foreach (var (x, colour) in new[] { (-36.0, 0xFFC02020u), (0.0, 0xFF2040B0u), (36.0, 0xFF209040u) })
            dc.DrawEllipse(Brush(colour), Pen(0xFF5A3A08, 1), P(x, 24), 6 * s, 5 * s);
        foreach (var (x, y) in new[] { (-60.0, -18.0), (-20.0, -26.0), (20.0, -26.0), (60.0, -18.0), (0.0, 0.0) })
            dc.DrawEllipse(Brush(0xFFF8F0E0), Pen(0xFF8A7A5A, 0.8), P(x, y - 5), 5 * s, 5 * s);
    }

    private static string ToRoman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => "X" };
}
