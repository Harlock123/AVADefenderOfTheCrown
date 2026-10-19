using Avalonia;
using Avalonia.Media;
using AVADefenderOfTheCrown.Engine;

namespace AVADefenderOfTheCrown.Views;

public sealed partial class GameCanvas
{
    /// <summary>A banner across the top or middle of a scene, with a line of large lettering.</summary>
    private static void Banner(DrawingContext dc, string text, double y, double size = 40, uint colour = Rubric)
    {
        double w = Math.Min(980, TextWidth(text, size) + 90);
        var r = new Rect(512 - w / 2, y - size * 0.75, w, size * 1.5);
        dc.FillRectangle(Brush(0x60000000), r.Translate(new Vector(4, 5)));
        dc.DrawImage(Art.Parchment(1000, 90, 61), new Rect(0, 0, w, r.Height), r);
        dc.DrawRectangle(null, Pen(Ink, 1.4), r);
        dc.DrawRectangle(null, Pen(Gold, 1.6), r.Deflate(4));
        Text(dc, text, new Point(512, y), size, colour, FellSC, Align.Center);
    }

    /// <summary>A small plate of parchment for a scene's numbers.</summary>
    private static void Plate(DrawingContext dc, Rect r)
    {
        dc.FillRectangle(Brush(0x50000000), r.Translate(new Vector(3, 4)));
        dc.DrawImage(Art.Parchment(400, 120, 71), new Rect(0, 0, Math.Min(400, r.Width), Math.Min(120, r.Height)), r);
        dc.DrawRectangle(null, Pen(Ink, 1.2), r);
        dc.DrawRectangle(null, Pen(Gold, 1.2), r.Deflate(3));
    }

    // ───────────────────────────── the joust ─────────────────────────────

    /// <summary>How big something at the meeting point looks <paramref name="p"/> of the way through the charge.</summary>
    private static double Near(double p) => 1 / (1 + 15 * (1 - p));

    private static Point Project(double x, double y, double f) => new(Joust.VanishX + (x - Joust.VanishX) * f, Joust.VanishY + (y - Joust.VanishY) * f);

    private void DrawJoust(DrawingContext dc, Joust j)
    {
        var w = Engine.World;
        dc.DrawImage(Art.Lists, new Rect(0, 0, ScreenWidth, ScreenHeight));
        DrawStandFlags(dc);
        double p = j.Progress, f = Near(p);
        var (sx, sy) = j.Shield;
        double t = j.Phase == JoustPhase.Charge ? j.T : 0;
        var foe = Realm.Lords[j.Foe];
        if (j.Phase == JoustPhase.Strike || j.Phase == JoustPhase.Done)
        {
            // He rides on past, out to the left; unhorsed, he goes over his horse's tail.
            double k = j.Phase == JoustPhase.Done ? 3 : Math.Min(3, j.T);
            bool down = j.Mine == Strike.Unhorse;
            using (dc.PushTransform(Matrix.CreateTranslation(-k * 260, k * 40)))
                DrawCharger(dc, new Point(sx, sy), 1 + k * 0.12, foe, j.T * 9, down ? Math.Min(1, k * 1.4) : 0, true);
            if (j.Mine == Strike.Shatter || j.Mine == Strike.Unhorse) Splinters(dc, j.Tip, j.T);
        }
        else
        {
            var at = Project(sx, sy, f);
            DrawCharger(dc, at, f, foe, t * 14, 0, false);
            // His lance comes at you, foreshortened: from his hand towards your shield, longer as he nears.
            var hand = new Point(at.X + (-78 - 70) * f, at.Y + (-60 + Math.Sin(t * 14) * 6) * f);
            var point = new Point(hand.X + (300 - hand.X) * (0.25 + 0.55 * p), hand.Y + (640 - hand.Y) * (0.25 + 0.55 * p));
            var d = point - hand;
            double len = Math.Sqrt(d.X * d.X + d.Y * d.Y), nx = -d.Y / Math.Max(1, len), ny = d.X / Math.Max(1, len);
            double w0 = 7 * f, w1 = 3 + 22 * p * p;
            Poly(dc, 0xFFE8DCC0, 0xFF3A2A18, 1, new(hand.X + nx * w0, hand.Y + ny * w0), new(point.X + nx * w1, point.Y + ny * w1), new(point.X - nx * w1, point.Y - ny * w1), new(hand.X - nx * w0, hand.Y - ny * w0));
            for (double k = 0.1; k < 0.95; k += 0.16)
            {
                var a = hand + d * k;
                double ww = w0 + (w1 - w0) * k;
                dc.DrawLine(Pen(foe.Field, Math.Max(1, ww * 0.7)), a + new Vector(nx, ny) * ww * 0.9, a + d * 0.05 - new Vector(nx, ny) * ww * 0.9);
            }
        }
        // His lance comes at you; then yours, from under your arm to its point.
        DrawMount(dc, j);
        var tip = j.Phase is JoustPhase.Strike or JoustPhase.Done ? new Point(j.Tip.X, j.Tip.Y) : Project(j.Tip.X, j.Tip.Y, f);
        DrawLance(dc, new Point(860, 800), tip, Realm.Lords[w.Player], j.Phase is JoustPhase.Strike or JoustPhase.Done && j.Mine != Strike.Miss ? 0.55 : 1);
        // The score and what's happening.
        var plate = new Rect(282, 14, 460, 64);
        Plate(dc, plate);
        DrawShield(dc, w.Player, new Point(plate.X + 30, plate.Center.Y), 42);
        DrawShield(dc, j.Foe, new Point(plate.Right - 30, plate.Center.Y), 42);
        Text(dc, $"{Realm.Lords[w.Player].Short}  {j.Points} - {j.FoePoints}  {foe.Short}", new Point(512, plate.Y + 24), 20, Ink, FellSC, Align.Center);
        Text(dc, $"PASS {j.Pass} OF {Joust.Passes}  -  {(Engine.ForLand ? "FOR LAND" : "FOR FAME")}", new Point(512, plate.Y + 47), 15, Rubric, FellSC, Align.Center);
        switch (j.Phase)
        {
            case JoustPhase.Ready:
                Banner(dc, j.Pass == 1 ? $"{foe.Name.ToUpperInvariant()} TAKES UP HIS LANCE" : "READY YOUR LANCE", 150, 30);
                break;
            case JoustPhase.Strike:
                string mine = j.BothDown ? "YOU BOTH GO DOWN!" : j.Mine switch { Strike.Unhorse => "UNHORSED!", Strike.Shatter => "YOUR LANCE SHATTERS ON HIS SHIELD", _ => "YOUR LANCE MISSES" };
                string his = j.BothDown ? "THE PASS IS RIDDEN AGAIN" : j.His switch { Strike.Unhorse => "...AND HE UNHORSES YOU", Strike.Shatter => "HIS LANCE BREAKS ON YOUR SHIELD", _ => "HIS LANCE GLANCES AWAY" };
                Banner(dc, mine, 150, j.Mine == Strike.Unhorse ? 44 : 30, j.Mine == Strike.Unhorse ? Rubric : Ink);
                Text(dc, his, new Point(512, 204), 22, 0xFFFFF0D0, FellSC, Align.Center, Shadow);
                break;
            case JoustPhase.Done:
                Banner(dc, j.Result > 0 ? "VICTORY IN THE LISTS!" : j.Result < 0 ? "DEFEATED!" : "NEITHER KNIGHT YIELDS", 150, 40, j.Result > 0 ? Rubric : Ink);
                break;
            default:
                Text(dc, "STEER YOUR LANCE ONTO HIS SHIELD: MOUSE OR ARROW KEYS", new Point(512, 742), 19, 0xFFFFF0D0, FellSC, Align.Center, Shadow);
                break;
        }
    }

    /// <summary>Pennants along the stands' awnings.</summary>
    private void DrawStandFlags(DrawingContext dc)
    {
        uint[] cols = [0xFFC02A22, 0xFFE8C040, 0xFF2A4AA0, 0xFFF0E6D0, 0xFF2E7A3A];
        for (int k = 0; k < 10; k++)
        {
            bool left = k < 5;
            double x = left ? 30 + k * 62 : 1000 - (k - 5) * 62, y = 22 + (left ? k : k - 5) * 22;
            dc.DrawLine(Pen(0xFF3A2A18, 2), new Point(x, y + 30), new Point(x, y - 10));
            var pts = new List<Point> { new(x, y - 10) };
            for (int i = 1; i <= 6; i++) pts.Add(new Point(x + (left ? 1 : -1) * i * 5, y - 10 + i * 0.6 + Math.Sin(_clock * 7 + i * 0.8 + k) * i * 0.5));
            pts.Add(new Point(x, y + 2));
            Poly(dc, cols[k % cols.Length], 0xFF2A1A10, 0.8, [.. pts]);
        }
    }

    /// <summary>
    /// The other knight riding straight at you: his horse head-on under its caparison, its legs going, the knight
    /// with his helm and his plume, his lance levelled and his shield (with the boss to aim for) at
    /// <paramref name="shield"/>. Drawn at scale <paramref name="f"/>; <paramref name="fall"/> tips him out of the saddle.
    /// </summary>
    private void DrawCharger(DrawingContext dc, Point shield, double f, LordInfo knight, double stride, double fall, bool passing)
    {
        using var _ = dc.PushTransform(Matrix.CreateScale(f, f) * Matrix.CreateTranslation(shield.X, shield.Y));
        double bob = Math.Sin(stride) * 6;
        uint field = knight.Field, tint = knight.Tincture;
        // The horse: legs, chest under the caparison, its head over it.
        double hx = -78;
        for (int k = 0; k < 4; k++)
        {
            double phase = stride + k * 1.6, lift = Math.Max(0, Math.Sin(phase)) * 40;
            double lx = hx + (k < 2 ? -36 : 36) + (k % 2 == 0 ? -10 : 10);
            dc.DrawLine(Pen(0xFF3A2414, 16), new Point(lx, 200 + bob), new Point(lx + (k < 2 ? -4 : 4), 320 - lift));
            dc.DrawEllipse(Brush(0xFF1A120A), null, new Point(lx + (k < 2 ? -4 : 4), 324 - lift), 10, 6);
        }
        dc.DrawEllipse(Brush(0x50000000), null, new Point(hx, 332), 110, 16);
        var cap = Shape(new(hx - 92, 40 + bob), new(hx + 92, 40 + bob), new(hx + 104, 240 + bob), new(hx + 70, 228 + bob), new(hx + 40, 246 + bob),
            new(hx, 232 + bob), new(hx - 40, 246 + bob), new(hx - 70, 228 + bob), new(hx - 104, 240 + bob));
        dc.DrawGeometry(Brush(field), Pen(0xFF1A1008, 2.5), cap);
        using (dc.PushGeometryClip(cap))
        {
            // The caparison's border and its folds.
            dc.DrawLine(Pen(tint, 10), new Point(hx - 110, 220 + bob), new Point(hx + 110, 220 + bob));
            for (int k = -2; k <= 2; k++) dc.DrawLine(Pen(0x40000000, 6), new Point(hx + k * 40, 60 + bob), new Point(hx + k * 46, 250 + bob));
        }
        // The head, long and narrow, in a chanfron with the lord's colour.
        var head = Shape(new(hx - 26, -40 + bob), new(hx + 26, -40 + bob), new(hx + 22, 70 + bob), new(hx + 14, 118 + bob), new(hx - 14, 118 + bob), new(hx - 22, 70 + bob));
        dc.DrawGeometry(Brush(0xFF5A3A22), Pen(0xFF1A1008, 2), head);
        Poly(dc, Steel, 0xFF2A2E36, 1.5, new(hx - 18, -34 + bob), new(hx + 18, -34 + bob), new(hx + 12, 60 + bob), new(hx - 12, 60 + bob));
        dc.DrawLine(Pen(tint, 5), new Point(hx, -30 + bob), new Point(hx, 54 + bob));
        foreach (var e in new[] { -1, 1 })
        {
            Poly(dc, 0xFF4A2E1A, 0xFF1A1008, 1.5, new(hx + e * 18, -38 + bob), new(hx + e * 28, -70 + bob), new(hx + e * 32, -36 + bob));
            dc.DrawEllipse(Brush(0xFF0E0A08), null, new Point(hx + e * 25, 14 + bob), 5, 7);
        }
        dc.DrawEllipse(Brush(0xFF1A1008), null, new Point(hx - 7, 108 + bob), 4, 3);
        dc.DrawEllipse(Brush(0xFF1A1008), null, new Point(hx + 7, 108 + bob), 4, 3);
        // The knight, tipped back if he's going down.
        using (dc.PushTransform(Matrix.CreateTranslation(-hx, -(-30 + bob)) * Matrix.CreateRotation(-fall * 1.3) * Matrix.CreateTranslation(hx + fall * 60, -30 + bob - fall * 40)))
        {
            // His body in his surcoat, arms, helm and plume.
            Poly(dc, field, 0xFF1A1008, 2, new(hx - 50, -30), new(hx - 44, -150), new(hx + 44, -150), new(hx + 50, -30));
            dc.DrawLine(Pen(tint, 14), new Point(hx, -146), new Point(hx, -34));
            dc.DrawLine(Pen(Mail, 24), new Point(hx - 44, -140), new Point(hx - 74, -64));
            dc.DrawGeometry(Brush(Steel), Pen(0xFF2A2E36, 2.5), new RectangleGeometry(new Rect(hx - 32, -236, 64, 90), 10, 10));
            dc.FillRectangle(Brush(0xFF14161A), new Rect(hx - 28, -200, 56, 8));
            dc.FillRectangle(Brush(SteelDark), new Rect(hx - 3, -236, 6, 90));
            dc.DrawLine(Pen(0x70FFFFFF, 4), new Point(hx - 24, -228), new Point(hx - 24, -156));
            var plume = Shape(new(hx - 8, -236), new(hx - 30, -276), new(hx + 4, -300), new(hx + 36, -282), new(hx + 10, -236));
            dc.DrawGeometry(Brush(tint), Pen(0xFF1A1008, 1.5), plume);
            // His gauntlet, couching the lance.
            if (!passing) dc.DrawGeometry(Brush(Steel), Pen(0xFF2A2E36, 1.5), new EllipseGeometry(new Rect(hx - 96, -96, 52, 52)));
            // His shield on his left arm, with the boss you aim for.
            DrawShield(dc, Array.IndexOf(Realm.Lords, knight), new Point(0, 8), 2 * Joust.ShieldRadius * 1.2);
            dc.DrawEllipse(Brush(0xFFE8C040), Pen(0xFF5A3A10, 3), new Point(0, 0), Joust.BossRadius * 0.62, Joust.BossRadius * 0.62);
            dc.DrawEllipse(null, Pen(0x805A3A10, 2), new Point(0, 0), Joust.BossRadius, Joust.BossRadius);
        }
    }

    /// <summary>Your own horse seen from the saddle: the back of its head and its ears, its mane, and its neck under your caparison, bobbing at the gallop.</summary>
    private void DrawMount(DrawingContext dc, Joust j)
    {
        var info = Realm.Lords[Engine.World.Player];
        double bob = j.Phase == JoustPhase.Charge ? Math.Sin(j.T * Math.Tau * 2.4) * 9 : Math.Sin(_clock * 2) * 2;
        double cx = 400, top = 596 + bob;
        // The neck under the caparison, widening towards you.
        var neck = Shape(new(cx - 190, 800), new(cx - 64, top + 34), new(cx + 64, top + 34), new(cx + 200, 800));
        dc.DrawGeometry(Brush(info.Field), Pen(0xFF1A1008, 2), neck);
        using (dc.PushGeometryClip(neck))
        {
            dc.DrawLine(Pen(info.Tincture, 12), new Point(cx - 64 - 16, top + 50), new Point(cx - 200, 800));
            dc.DrawLine(Pen(info.Tincture, 12), new Point(cx + 64 + 16, top + 50), new Point(cx + 210, 800));
            dc.FillRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(cx - 190, 0, RelativeUnit.Absolute), EndPoint = new RelativePoint(cx + 200, 0, RelativeUnit.Absolute),
                GradientStops = { new GradientStop(Color.FromUInt32(0x30FFFFFF), 0), new GradientStop(Color.FromUInt32(0x00000000), 0.45), new GradientStop(Color.FromUInt32(0x50000000), 1) },
            }, new Rect(cx - 200, top, 420, 220));
        }
        // The mane down the middle of the neck.
        for (int k = 0; k < 12; k++)
        {
            double y = top + 36 + k * 15, w = 10 + k * 1.6;
            Poly(dc, 0xFF2A1A10, null, 0, new(cx - w, y), new(cx + w, y), new(cx + w * 0.6 + Math.Sin(_clock * 8 + k) * 4, y + 22), new(cx - w * 0.6 + Math.Sin(_clock * 8 + k) * 4, y + 22));
        }
        // The back of the head, its steel chanfron, and the ears pricked forward.
        var head = new StreamGeometry();
        using (var c = head.Open())
        {
            c.BeginFigure(new Point(cx - 60, top + 42), true);
            c.CubicBezierTo(new Point(cx - 66, top + 10), new Point(cx - 30, top - 34), new Point(cx, top - 38));
            c.CubicBezierTo(new Point(cx + 30, top - 34), new Point(cx + 66, top + 10), new Point(cx + 60, top + 42));
            c.EndFigure(true);
        }
        dc.DrawGeometry(Brush(0xFF6A4428), Pen(0xFF1A1008, 2), head);
        Poly(dc, Steel, 0xFF2A2E36, 1.6, new(cx - 30, top + 24), new(cx - 22, top - 20), new(cx, top - 30), new(cx + 22, top - 20), new(cx + 30, top + 24));
        dc.DrawLine(Pen(info.Tincture, 6), new Point(cx, top - 26), new Point(cx, top + 22));
        dc.DrawLine(Pen(0x70FFFFFF, 3), new Point(cx - 20, top - 14), new Point(cx - 24, top + 18));
        foreach (var e in new[] { -1, 1 })
            Poly(dc, 0xFF5A3A22, 0xFF1A1008, 1.6, new(cx + e * 30, top + 30), new(cx + e * 50, top - 38), new(cx + e * 62, top - 30), new(cx + e * 58, top + 34));
        // The reins back to your hands.
        dc.DrawLine(Pen(0xFF3A2410, 5), new Point(cx - 56, top + 30), new Point(cx - 120, 800));
        dc.DrawLine(Pen(0xFF3A2410, 5), new Point(cx + 56, top + 30), new Point(cx + 110, 800));
        // Your shield on your left arm, at the edge of the view.
        DrawShield(dc, Engine.World.Player, new Point(118, 650 + bob * 0.6), 230);
    }

    /// <summary>Your lance from your arm to its point: painted in your colours, tapering, with its guard at the grip.</summary>
    private static void DrawLance(DrawingContext dc, Point butt, Point tip, LordInfo info, double length)
    {
        var end = new Point(butt.X + (tip.X - butt.X) * length, butt.Y + (tip.Y - butt.Y) * length);
        double dx = end.X - butt.X, dy = end.Y - butt.Y, len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) return;
        double nx = -dy / len, ny = dx / len;
        double w0 = 30, w1 = length < 1 ? 14 : 4;
        Poly(dc, 0xFFE8DCC0, 0xFF3A2A18, 1.5, new(butt.X + nx * w0, butt.Y + ny * w0), new(end.X + nx * w1, end.Y + ny * w1), new(end.X - nx * w1, end.Y - ny * w1), new(butt.X - nx * w0, butt.Y - ny * w0));
        // The spiral stripes.
        for (double t = 0.05; t < 0.97; t += 0.07)
        {
            double wa = w0 + (w1 - w0) * t, wb = w0 + (w1 - w0) * (t + 0.035);
            var a = new Point(butt.X + dx * t, butt.Y + dy * t);
            var b = new Point(butt.X + dx * (t + 0.035), butt.Y + dy * (t + 0.035));
            Poly(dc, info.Field, null, 0, new(a.X + nx * wa, a.Y + ny * wa), new(b.X + nx * wb * 0.2, b.Y + ny * wb * 0.2), new(b.X - nx * wb, b.Y - ny * wb), new(a.X - nx * wa * 0.2, a.Y - ny * wa * 0.2));
        }
        if (length < 1)
        {
            // A broken end, splintered.
            Poly(dc, 0xFFE8DCC0, 0xFF3A2A18, 1, new(end.X + nx * w1, end.Y + ny * w1), new(end.X + dx * 0.04 + nx * 4, end.Y + dy * 0.04 + ny * 4),
                new(end.X + dx * 0.02, end.Y + dy * 0.02), new(end.X + dx * 0.05 - nx * 6, end.Y + dy * 0.05 - ny * 6), new(end.X - nx * w1, end.Y - ny * w1));
        }
        else Poly(dc, Steel, 0xFF2A2E36, 1, new(end.X + nx * 5, end.Y + ny * 5), new(end.X + dx / len * 18, end.Y + dy / len * 18), new(end.X - nx * 5, end.Y - ny * 5));
        // The vamplate guarding the hand.
        var v = new Point(butt.X + dx * 0.1, butt.Y + dy * 0.1);
        dc.DrawGeometry(Brush(Steel), Pen(0xFF2A2E36, 2), new EllipseGeometry(new Rect(v.X - 52, v.Y - 52, 104, 104)));
        dc.DrawGeometry(Brush(0x40FFFFFF), null, new EllipseGeometry(new Rect(v.X - 40, v.Y - 46, 50, 50)));
    }

    private static void Splinters(DrawingContext dc, (double X, double Y) at, double t)
    {
        if (t > 1.4) return;
        var dice = new Dice(5);
        for (int k = 0; k < 18; k++)
        {
            double a = dice.Range(0, Math.Tau), v = dice.Range(150, 420), len = dice.Range(10, 34), spin = dice.Range(-8, 8);
            double x = at.X + Math.Cos(a) * v * t, y = at.Y + Math.Sin(a) * v * t + 300 * t * t;
            double r = a + spin * t;
            dc.DrawLine(Pen(Alpha(0xFFE8DCC0, 1 - t / 1.4), 4), new Point(x, y), new Point(x + Math.Cos(r) * len, y + Math.Sin(r) * len));
        }
    }

    // ───────────────────────────── the siege ─────────────────────────────

    public static Rect AmmoRect(int i) => new(254 + i * 150, 690, 140, 40);
    public static readonly Rect StormRect = new(770, 690, 230, 40);

    private void DrawSiege(DrawingContext dc, Siege s)
    {
        dc.DrawImage(Art.SiegeField, new Rect(0, 0, ScreenWidth, ScreenHeight));
        var defender = s.Lord;
        // The castle's banners.
        foreach (var (x, y) in new[] { (962.0, 196.0), (766.0, 336.0) })
        {
            dc.DrawLine(Pen(0xFF2A1A10, 2), new Point(x, y + 20), new Point(x, y - 26));
            var pts = new List<Point> { new(x, y - 26) };
            for (int k = 1; k <= 8; k++) pts.Add(new Point(x - k * 5, y - 26 + Math.Sin(_clock * 5 + k) * k * 0.4));
            for (int k = 8; k >= 1; k--) pts.Add(new Point(x - k * 5, y - 12 + Math.Sin(_clock * 5 + k) * k * 0.4));
            Poly(dc, defender >= 0 ? Realm.Lords[defender].Field : 0xFF8A8A8A, 0xFF1A1008, 1, [.. pts]);
        }
        DrawBreach(dc, s);
        // The garrison on the walls, fewer as the siege kills them.
        int men = Math.Min(4, (s.Left.Soldiers + 5) / 6);
        for (int k = 0; k < men; k++)
            DrawFootman(dc, new Point(Siege.WallX + 10 + k * 11, Siege.WallTop - 2), 0.62, defender >= 0 ? Realm.Lords[defender].Field : 0xFF8A2A2A, facingLeft: true);
        for (int k = 0; k < Math.Min(3, men); k++)
            DrawFootman(dc, new Point(910 + k * 30, 220 - 2), 0.55, defender >= 0 ? Realm.Lords[defender].Field : 0xFF8A2A2A, facingLeft: true);
        // Fires the Greek fire left burning in the bailey.
        for (int k = 0; k < Math.Min(4, s.Fires); k++) Flame(dc, new Point(810 + k * 26, Siege.GroundY - 30 + (k % 2) * 8), 0.8 + 0.2 * k);
        // Your camp behind the catapult.
        var me = Realm.Lords[Engine.World.Player];
        Tent(dc, new Point(44, Siege.GroundY + 4), 70, me.Field, me.Tincture);
        Tent(dc, new Point(330, Siege.GroundY - 6), 50, me.Tincture, me.Field);
        DrawCatapult(dc, s);
        DrawShot(dc, s);
        // The numbers, the missiles and the storm.
        var plate = new Rect(16, 14, 330, 104);
        Plate(dc, plate);
        Text(dc, $"ANGLE {s.Angle:0}°", new Point(plate.X + 20, plate.Y + 24), 19, Ink);
        Gauge(dc, new Rect(plate.X + 130, plate.Y + 16, 180, 14), (s.Angle - Siege.MinAngle) / (Siege.MaxAngle - Siege.MinAngle), Lapis);
        Text(dc, $"POWER {s.Power:0}", new Point(plate.X + 20, plate.Y + 50), 19, Ink);
        Gauge(dc, new Rect(plate.X + 130, plate.Y + 42, 180, 14), (s.Power - Siege.MinPower) / (Siege.MaxPower - Siege.MinPower), Rubric);
        Text(dc, $"SHOTS LEFT {s.Shots}", new Point(plate.X + 20, plate.Y + 78), 19, Ink);
        Text(dc, $"GARRISON {s.Left.Soldiers}+{s.Left.Knights}", new Point(plate.Right - 16, plate.Y + 78), 17, 0xFF5A4A3A, align: Align.Right);
        var bp = new Rect(678, 14, 330, 60);
        Plate(dc, bp);
        Text(dc, $"THE WALL: {(s.Breached ? "BREACHED!" : $"{s.Breach:0}% BROKEN")}", new Point(bp.X + 20, bp.Y + 20), 18, s.Breached ? Rubric : Ink);
        Gauge(dc, new Rect(bp.X + 20, bp.Y + 34, 290, 14), s.Breach / 100, 0xFF8A5A2A);
        string[] ammo = ["BOULDERS", "GREEK FIRE", "DISEASE"];
        for (int i = 0; i < 3; i++)
        {
            int left = s.AmmoLeft((Ammo)i);
            int k = i;
            Plate(dc, AmmoRect(i));
            if (s.Ammo == (Ammo)i) dc.FillRectangle(Brush(0x60E0B040), AmmoRect(i).Deflate(3));
            Text(dc, $"{i + 1} {ammo[i]}" + (i > 0 ? $" ({left})" : ""), AmmoRect(i).Center, 16, left > 0 ? Ink : 0x802A1A0E, align: Align.Center);
            if (left > 0) Hit(AmmoRect(i), () => s.Select((Ammo)k));
        }
        Plate(dc, StormRect);
        Text(dc, "G  STORM THE CASTLE", StormRect.Center, 18, Rubric, align: Align.Center);
        Hit(StormRect, s.Storm);
        Text(dc, "AIM WITH THE MOUSE AND CLICK, OR ARROWS: ANGLE AND POWER, SPACE TO LOOSE", new Point(512, 752), 15, 0xFFFFF0D0, FellSC, Align.Center, Shadow);
        if (s.Phase == SiegePhase.Result) Banner(dc, s.Report, 172, 28, s.Last is Landing.Wall or Landing.Inside or Landing.Battlements ? Rubric : Ink);
        if (s.Phase == SiegePhase.Done) Banner(dc, s.Breached ? "THROUGH THE BREACH!" : "TO THE WALLS!", 172, 40);
        if (s.Phase == SiegePhase.Aim && s.Shots > 0 && s.Hits == 0 && s.Last == Landing.None)
            Banner(dc, $"THE SIEGE OF {(defender >= 0 ? Realm.Lords[defender].CastleName.ToUpperInvariant() : "THE CASTLE")}", 172, 30);
    }

    /// <summary>A round pavilion, striped in two colours, with a pennant on its pole.</summary>
    private void Tent(DrawingContext dc, Point at, double w, uint a, uint b)
    {
        double h = w * 0.95;
        for (int k = 0; k < 6; k++)
        {
            double x0 = at.X - w / 2 + k * w / 6, x1 = x0 + w / 6;
            Poly(dc, k % 2 == 0 ? a : b, null, 0, new(at.X, at.Y - h), new(x1, at.Y - h * 0.38), new(x1, at.Y), new(x0, at.Y), new(x0, at.Y - h * 0.38));
        }
        Poly(dc, 0, 0xFF1A1008, 1.4, new(at.X, at.Y - h), new(at.X + w / 2, at.Y - h * 0.38), new(at.X + w / 2, at.Y), new(at.X - w / 2, at.Y), new(at.X - w / 2, at.Y - h * 0.38));
        for (int k = 0; k < 6; k++) dc.DrawEllipse(Brush(b), Pen(0xFF1A1008, 0.8), new Point(at.X - w / 2 + (k + 0.5) * w / 6, at.Y - h * 0.38), w / 16, w / 22);
        Poly(dc, 0xFF1A120A, null, 0, new(at.X - w * 0.1, at.Y), new(at.X, at.Y - h * 0.3), new(at.X + w * 0.1, at.Y));
        dc.DrawLine(Pen(0xFF2A1A10, 1.6), new Point(at.X, at.Y - h), new Point(at.X, at.Y - h - 18));
        Poly(dc, a, 0xFF1A1008, 0.6, new(at.X, at.Y - h - 18), new(at.X + 14, at.Y - h - 14 + Math.Sin(_clock * 6) * 2), new(at.X, at.Y - h - 10));
    }

    private static void Gauge(DrawingContext dc, Rect r, double v, uint colour)
    {
        dc.FillRectangle(Brush(0x302A1A0E), r, 3);
        dc.FillRectangle(Brush(colour), new Rect(r.X, r.Y, r.Width * Math.Clamp(v, 0, 1), r.Height), 3);
        dc.DrawRectangle(null, Pen(Ink, 1), r, 3, 3);
    }

    /// <summary>The broken stones: bites out of the wall's face where the shots struck, deeper as it breaks, and rubble at its foot.</summary>
    private static void DrawBreach(DrawingContext dc, Siege s)
    {
        if (s.Breach <= 0) return;
        double h = Siege.GroundY - s.BreachHeight * (Siege.GroundY - (Siege.WallTop - Siege.Merlon));
        double depth = Math.Min(Siege.WallThick + 4, 6 + s.Breach * 0.48), size = 16 + s.Breach * 0.62;
        var dice = new Dice(9);
        var pts = new List<Point> { new(Siege.WallX - 1, h - size) };
        for (int k = 0; k <= 10; k++)
        {
            double a = -Math.PI / 2 + k * Math.PI / 10;
            double r = dice.Range(0.7, 1.05);
            pts.Add(new Point(Siege.WallX + Math.Cos(a) * depth * r, h + Math.Sin(a) * size * r));
        }
        pts.Add(new Point(Siege.WallX - 1, h + size));
        Poly(dc, s.Breached ? 0xFF2E2A2C : 0xFF3A3028, 0xFF1A1410, 1.5, [.. pts]);
        if (s.Breached)
        {
            // Light through the gap.
            Poly(dc, 0xFF6A6A70, null, 0, new(Siege.WallX + Siege.WallThick - 6, h - size * 0.5), new(Siege.WallX + Siege.WallThick + 1, h - size * 0.4), new(Siege.WallX + Siege.WallThick + 1, h + size * 0.6), new(Siege.WallX + Siege.WallThick - 8, h + size * 0.5));
        }
        // Cracks running out from it.
        for (int k = 0; k < 5; k++)
        {
            double a = dice.Range(-1.2, 1.2), l = size * dice.Range(0.4, 1.0);
            double x0 = Siege.WallX + 2, y0 = h + dice.Range(-size, size);
            dc.DrawLine(Pen(0xC0201810, 1.4), new Point(x0, y0), new Point(x0 + Math.Abs(Math.Cos(a)) * l * 0.3 + 4, y0 + Math.Sin(a) * l));
        }
        // Rubble at the foot of the wall.
        double pile = Math.Min(40, s.Breach * 0.4);
        for (int k = 0; k < 4 + (int)(s.Breach / 8); k++)
        {
            double x = Siege.WallX - dice.Range(0, pile * 1.6), y = Siege.GroundY - dice.Range(0, pile * 0.6) * (1 - (Siege.WallX - x) / (pile * 1.7 + 1));
            dc.DrawEllipse(Brush(Pix.Scale(0xFF9A9284, dice.Range(0.6, 1.1))), Pen(0xFF2A2420, 0.8), new Point(x, y), dice.Range(3, 8), dice.Range(2.5, 6));
        }
    }

    /// <summary>The mangonel: its frame on wheels and its throwing arm, swinging up when it looses.</summary>
    private void DrawCatapult(DrawingContext dc, Siege s)
    {
        double gx = 150, gy = Siege.GroundY;
        // How far the arm has swung: back and down while aiming, up to the stop just after loosing.
        double swing = s.Phase == SiegePhase.Flight ? Math.Min(1, s.T * 6) : s.Phase == SiegePhase.Result ? Math.Max(0, 1 - s.T * 1.2) : 0;
        var pivot = new Point(gx + 22, gy - 62);
        double restA = Math.PI * 1.06, endA = Math.PI * 1.5 + (s.Angle - 45) * Math.PI / 180 * 0.6;
        double a = restA + (endA - restA) * swing;
        var cup = new Point(pivot.X + Math.Cos(a) * 92, pivot.Y + Math.Sin(a) * 92);
        // The frame.
        dc.DrawLine(Pen(0xFF4A2E16, 12), new Point(gx - 70, gy - 22), new Point(gx + 70, gy - 22));
        dc.DrawLine(Pen(0xFF5A3A1E, 10), new Point(gx - 10, gy - 22), new Point(pivot.X, pivot.Y));
        dc.DrawLine(Pen(0xFF5A3A1E, 10), new Point(gx + 54, gy - 22), new Point(pivot.X, pivot.Y));
        dc.DrawLine(Pen(0xFF3A2410, 6), new Point(gx + 64, gy - 22), new Point(gx + 64, gy - 70));
        dc.DrawLine(Pen(0xFF6A4A2A, 4), new Point(gx + 58, gy - 66), new Point(gx + 72, gy - 66));
        // The arm and its cup.
        dc.DrawLine(Pen(0xFF6A4424, 11), new Point(pivot.X - Math.Cos(a) * 18, pivot.Y - Math.Sin(a) * 18), cup);
        dc.DrawLine(Pen(0x40FFFFFF, 3), new Point(pivot.X - Math.Cos(a) * 14, pivot.Y - Math.Sin(a) * 14 - 3), new Point(cup.X, cup.Y - 3));
        dc.DrawEllipse(Brush(0xFF3A2410), null, cup, 12, 8);
        if (s.Phase == SiegePhase.Aim) DrawMissile(dc, s.Ammo, new Point(cup.X, cup.Y - 8), 0);
        dc.DrawEllipse(Brush(0xFF2A1A0A), Pen(0xFF1A1008, 1), pivot, 7, 7);
        // Wheels and the crew.
        foreach (var wx in new[] { gx - 50, gx + 50 })
        {
            dc.DrawEllipse(Brush(0xFF3A2410), Pen(0xFF1A1008, 2), new Point(wx, gy - 14), 16, 16);
            for (int k = 0; k < 4; k++) dc.DrawLine(Pen(0xFF6A4424, 2), new Point(wx, gy - 14), new Point(wx + Math.Cos(k * 0.8) * 14, gy - 14 + Math.Sin(k * 0.8) * 14));
        }
        uint coat = Realm.Lords[Engine.World.Player].Field;
        DrawFootman(dc, new Point(gx - 92, gy), 0.9, coat);
        DrawFootman(dc, new Point(gx + 96, gy), 0.9, coat, facingLeft: true);
        // While aiming: where the throw will start to go.
        if (s.Phase == SiegePhase.Aim)
            for (double t = 0.08; t < 0.34; t += 0.045)
            {
                var (x, y) = Siege.Arc(s.Angle, s.Power, t);
                dc.DrawEllipse(Brush(0xB0FFF0C0), null, new Point(x, y), 3, 3);
            }
    }

    private void DrawShot(DrawingContext dc, Siege s)
    {
        if (s.Phase == SiegePhase.Flight)
        {
            // A trail behind it.
            for (int k = 1; k <= 6; k++)
            {
                double t = s.T - k * 0.035;
                if (t <= 0) break;
                var (x, y) = Siege.Arc(s.FlightAngle, s.FlightPower, t);
                uint c = s.Flying == Ammo.GreekFire ? 0xFFFF9A30 : s.Flying == Ammo.Disease ? 0xFF8AB060 : 0xFFD8D0C0;
                dc.DrawEllipse(Brush(Alpha(c, 0.45 - k * 0.06)), null, new Point(x, y), 7 - k * 0.6, 7 - k * 0.6);
            }
            DrawMissile(dc, s.Flying, new Point(s.X, s.Y), s.T * 8);
        }
        else if (s.Phase == SiegePhase.Result && s.Last != Landing.None)
        {
            // Dust (or flame, or a sick green haze) where it came down.
            double k = Math.Min(1, s.T / Siege.ResultSeconds);
            uint c = s.Flying == Ammo.GreekFire && s.Last == Landing.Inside ? 0xFFFF8A30 : s.Flying == Ammo.Disease ? 0xFF8AA060 : 0xFFB8A888;
            for (int i = 0; i < 6; i++)
            {
                double a = i * 1.05, r = 10 + k * 40;
                dc.DrawEllipse(Brush(Alpha(c, 0.55 * (1 - k))), null, new Point(s.X + Math.Cos(a) * r * 0.6, s.Y - 6 + Math.Sin(a) * r * 0.3 - k * 20), r * 0.5, r * 0.4);
            }
            if (s.Flying == Ammo.GreekFire) Flame(dc, new Point(s.X, s.Y), 1.4 * (1 - k * 0.5));
        }
    }

    private void DrawMissile(DrawingContext dc, Ammo a, Point at, double spin)
    {
        switch (a)
        {
            case Ammo.GreekFire:
                dc.DrawEllipse(Brush(0xFF6A4A2A), Pen(0xFF2A1A10, 1), at, 7, 7);
                Flame(dc, new Point(at.X - 2, at.Y + 4), 0.7);
                break;
            case Ammo.Disease:
                dc.DrawEllipse(Brush(0x508AB060), null, at, 18, 14);
                using (dc.PushTransform(Matrix.CreateTranslation(-at.X, -at.Y) * Matrix.CreateRotation(spin) * Matrix.CreateTranslation(at.X, at.Y)))
                {
                    dc.DrawEllipse(Brush(0xFF7A5A3A), Pen(0xFF2A1A10, 1), at, 11, 6);
                    for (int k = -1; k <= 1; k += 2) { dc.DrawLine(Pen(0xFF5A3A22, 2), new Point(at.X + k * 6, at.Y + 4), new Point(at.X + k * 8, at.Y + 11)); }
                }
                break;
            default:
                dc.DrawEllipse(Brush(0xFF8A8478), Pen(0xFF2A2620, 1.2), at, 8, 8);
                dc.DrawEllipse(Brush(0x60FFFFFF), null, new Point(at.X - 2.5, at.Y - 2.5), 3, 3);
                break;
        }
    }

    /// <summary>A flickering flame.</summary>
    private void Flame(DrawingContext dc, Point at, double s)
    {
        for (int k = 0; k < 3; k++)
        {
            double h = (22 + 8 * Math.Sin(_clock * 13 + k * 2 + at.X)) * s, w = (10 - k * 2.5) * s;
            uint c = k == 0 ? 0xD0E04A18 : k == 1 ? 0xE0FF9A30 : 0xF0FFE8A0;
            Poly(dc, c, null, 0, new(at.X - w, at.Y), new(at.X - w * 0.5, at.Y - h * 0.5), new(at.X + Math.Sin(_clock * 9 + k) * 3 * s, at.Y - h), new(at.X + w * 0.6, at.Y - h * 0.45), new(at.X + w, at.Y));
        }
    }

    // ───────────────────────────── the raid ─────────────────────────────

    public const double RaidFloor = 646, RaidHigh = 470, RaidLow = 548;
    public static Rect SwordRect(int i) => new(150 + i * 186, 700, 176, 46);

    private void DrawRaid(DrawingContext dc, Raid r)
    {
        dc.DrawImage(Art.Hall, new Rect(0, 0, ScreenWidth, ScreenHeight));
        var lord = Realm.Lords[r.Lord];
        // The lord's banners on the wall, and the torches.
        foreach (var bx in new[] { 450.0, 820.0 })
        {
            Poly(dc, lord.Field, 0xFF1A1008, 1.5, new(bx - 34, 90), new(bx + 34, 90), new(bx + 34, 300), new(bx, 330), new(bx - 34, 300));
            dc.DrawLine(Pen(0xFF3A2410, 5), new Point(bx - 44, 90), new Point(bx + 44, 90));
            DrawShield(dc, r.Lord, new Point(bx, 190), 70);
        }
        foreach (var tx in new[] { 110.0, 560.0, 980.0 }) Torch(dc, new Point(tx, 300));
        var hero = r.Hero;
        var player = Realm.Lords[Engine.World.Player];
        // The lady, waiting at the door once the guards are beaten.
        if (r.ForLady && r.Phase is RaidPhase.Won or RaidPhase.Done) DrawLady(dc, new Point(900 - Math.Min(1, r.T) * 120, RaidFloor));
        for (int i = r.Guards.Count - 1; i >= 0; i--)
        {
            var g = r.Guards[i];
            if (i > r.Current && g.X > 1000) continue;
            double fade = g.Act == Act.Down ? 1 - g.T / Raid.DownSeconds : 1;
            if (fade <= 0.03 || i < r.Current) continue;
            using (dc.PushTransform(Larger(g.X)))
                DrawSwordsman(dc, g, -1, lord.Field, lord.Tincture, norman: Realm.IsNorman(r.Lord), fade: fade);
        }
        using (dc.PushTransform(Larger(hero.X)))
            DrawSwordsman(dc, hero, 1, player.Field, player.Tincture, norman: false, fade: 1);
        // Strength: your shields, the guard's.
        var plate = new Rect(16, 14, 320, 66);
        Plate(dc, plate);
        Text(dc, player.Short, new Point(plate.X + 18, plate.Y + 22), 19, Ink);
        for (int k = 0; k < hero.MaxHp; k++)
            Heart(dc, new Point(plate.X + 28 + k * 30, plate.Y + 46), k < hero.Hp);
        if (r.Foe is { } foe)
        {
            var gp = new Rect(688, 14, 320, 66);
            Plate(dc, gp);
            Text(dc, $"GUARD {r.Current + 1} OF {r.Guards.Count}", new Point(gp.X + 18, gp.Y + 22), 19, Ink);
            for (int k = 0; k < foe.MaxHp; k++) Heart(dc, new Point(gp.X + 28 + k * 30, gp.Y + 46), k < foe.Hp);
        }
        string[] names = ["↑ HIGH CUT", "→ THRUST", "↓ LOW CUT", "← PARRY"];
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            Plate(dc, SwordRect(i));
            Text(dc, names[i], SwordRect(i).Center, 19, i == 3 ? Lapis : Rubric, align: Align.Center);
            Hit(SwordRect(i), () => { if (k == 3) Engine.Sword(null, true); else Engine.Sword((Line)k); });
        }
        switch (r.Phase)
        {
            case RaidPhase.Enter:
                Banner(dc, r.ForLady ? $"INTO {lord.CastleName.ToUpperInvariant()} TO FREE {Realm.LadyName.ToUpperInvariant()}" : $"INTO {lord.CastleName.ToUpperInvariant()} BY NIGHT", 140, 30);
                break;
            case RaidPhase.Won or RaidPhase.Done when r.Success:
                Banner(dc, r.ForLady ? $"{Realm.LadyName.ToUpperInvariant()} IS FREE!" : "THE STRONGROOM IS YOURS!", 140, 40);
                break;
            case RaidPhase.Lost or RaidPhase.Done:
                Banner(dc, "OVERPOWERED AND TAKEN!", 140, 40, Ink);
                break;
            default:
                Text(dc, "CUT WHERE HIS BLADE IS NOT; PARRY AS HIS BLOW FALLS", new Point(512, 678), 17, 0xFFFFF0D0, FellSC, Align.Center, Shadow);
                break;
        }
    }

    /// <summary>The swordsmen are drawn a little larger than life, standing where they stand.</summary>
    private static Matrix Larger(double x) => Matrix.CreateTranslation(-x, -RaidFloor) * Matrix.CreateScale(1.18, 1.18) * Matrix.CreateTranslation(x, RaidFloor);

    private static void Heart(DrawingContext dc, Point c, bool full)
    {
        var g = ShieldShape(c, 22);
        dc.DrawGeometry(Brush(full ? Rubric : 0x302A1A0E), Pen(Ink, 1.2), g);
    }

    private void Torch(DrawingContext dc, Point at)
    {
        double flick = 1 + 0.08 * Math.Sin(_clock * 17 + at.X) + 0.05 * Math.Sin(_clock * 29 + at.X * 2);
        dc.DrawEllipse(new RadialGradientBrush
        {
            GradientStops = { new GradientStop(Color.FromUInt32(0x70FFB040), 0), new GradientStop(Color.FromUInt32(0x00FF8020), 1) },
        }, null, at, 150 * flick, 150 * flick);
        dc.DrawLine(Pen(0xFF2A1A10, 6), new Point(at.X, at.Y + 6), new Point(at.X, at.Y + 46));
        dc.DrawLine(Pen(0xFF2A1A10, 4), new Point(at.X - 10, at.Y + 46), new Point(at.X + 10, at.Y + 46));
        Flame(dc, new Point(at.X, at.Y + 4), 1.3 * flick);
    }

    /// <summary>
    /// A swordsman side-on, his feet on the hall floor at his <see cref="Swordsman.X"/>: his stance, his body and helm,
    /// and his sword where his stroke, guard or parry puts it. <paramref name="dir"/> is the way he faces.
    /// </summary>
    private void DrawSwordsman(DrawingContext dc, Swordsman m, int dir, uint coat, uint trim, bool norman, double fade)
    {
        using var op = dc.PushOpacity(fade);
        double x = m.X, y = RaidFloor;
        double k(double from, double to, double t) => from + (to - from) * Math.Clamp(t, 0, 1);
        // Where the sword hand is and the blade's angle (in degrees from the facing direction, up is negative).
        double hx, hy, ang, lean = 0;
        switch (m.Act)
        {
            case Act.Cut:
            {
                double t = m.T / Raid.CutSeconds;
                var (endX, endY, endA) = m.Line switch { Line.High => (70.0, -190.0, 20.0), Line.Low => (72.0, -92.0, 30.0), _ => (96.0, -150.0, -2.0) };
                if (t < 0.45) { double u = t / 0.45; (hx, hy, ang) = (k(40, m.Line == Line.Middle ? 0 : 14, u), k(-150, m.Line == Line.High ? -250 : m.Line == Line.Low ? -160 : -150, u), k(-20, m.Line == Line.High ? -130 : m.Line == Line.Low ? -70 : -5, u)); }
                else { double u = (t - 0.45) / 0.55; (hx, hy, ang) = (k(m.Line == Line.Middle ? 0 : 14, endX, u * 2), k(m.Line == Line.High ? -250 : m.Line == Line.Low ? -160 : -150, endY, u * 2), k(m.Line == Line.High ? -130 : m.Line == Line.Low ? -70 : -5, endA, u * 2)); lean = 8; }
                break;
            }
            case Act.Parry:
                (hx, hy, ang, lean) = (42, -168, -92, -4);
                break;
            case Act.Wind:
            {
                double t = m.T / Raid.WindSeconds, shake = Math.Sin(m.T * 60) * 2;
                var (wx, wy, wa) = m.Line switch { Line.High => (-2.0, -266.0, -118.0), Line.Low => (-16.0, -236.0, -168.0), _ => (-10.0, -252.0, -146.0) };
                (hx, hy, ang) = (k(40, wx, t * 1.6), k(-150, wy, t * 1.6) + shake, k(-20, wa, t * 1.6));
                lean = -6;
                break;
            }
            case Act.Recover:
                (hx, hy, ang, lean) = (80, -120, 26, 10);
                break;
            case Act.Stagger:
                (hx, hy, ang, lean) = (-24, -176, -160, -16);
                break;
            case Act.Hurt:
                (hx, hy, ang, lean) = (20, -140, 30, -12);
                break;
            case Act.Down:
                (hx, hy, ang, lean) = (40, -60, 0, 0);
                break;
            default:
                (hx, hy, ang) = m.Guard switch { Line.High => (36.0, -206.0, -62.0), Line.Low => (40.0, -108.0, 34.0), _ => (46.0, -152.0, -14.0) };
                break;
        }
        var rot = m.Act == Act.Down ? Matrix.CreateTranslation(-x, -y) * Matrix.CreateRotation(-dir * Math.PI / 2 * Math.Min(1, m.T * 3)) * Matrix.CreateTranslation(x, y) : Matrix.Identity;
        using var _ = dc.PushTransform(rot);
        Point P(double px, double py) => new(x + dir * (px + lean * (-py / 220)), y + py);
        // Legs, striding when he walks.
        double stride = m.Act == Act.Walk ? Math.Sin(m.T * 10) * 18 : 0;
        dc.DrawEllipse(Brush(0x50000000), null, new Point(x, y + 2), 50, 9);
        dc.DrawLine(Pen(0xFF3A2A1E, 15), P(-6, -108), P(-28 - stride, -54));
        dc.DrawLine(Pen(0xFF3A2A1E, 14), P(-28 - stride, -54), P(-34 - stride, 0));
        dc.DrawLine(Pen(0xFF4A3A2A, 15), P(6, -108), P(26 + stride, -54));
        dc.DrawLine(Pen(0xFF4A3A2A, 14), P(26 + stride, -54), P(34 + stride, 0));
        Poly(dc, 0xFF1A120A, null, 0, P(-46 - stride, 2), P(-26 - stride, 2), P(-26 - stride, -8), P(-40 - stride, -8));
        Poly(dc, 0xFF1A120A, null, 0, P(24 + stride, 2), P(48 + stride, 2), P(44 + stride, -8), P(26 + stride, -8));
        // The back arm, the mail and the surcoat over it.
        dc.DrawLine(Pen(Mail, 13), P(-8, -196), P(-30, -140));
        Poly(dc, Mail, 0xFF2A2E36, 1.5, P(-24, -112), P(-22, -204), P(24, -204), P(26, -112));
        Poly(dc, coat, 0xFF1A1008, 1.5, P(-26, -88), P(-20, -198), P(22, -198), P(28, -88));
        dc.DrawLine(Pen(trim, 6), P(-24, -100), P(26, -100));
        dc.DrawLine(Pen(0xFF3A2410, 7), P(-22, -122), P(24, -122));
        // The head and helm: a Saxon's rounded helm, a Norman's nasal.
        var head = P(4, -226);
        dc.DrawEllipse(Brush(Skin), Pen(0xFF6A4030, 1), head, 17, 19);
        dc.DrawEllipse(Brush(0xFF1A1008), null, P(12, -230), 2.2, 2.2);
        if (norman)
        {
            Poly(dc, Steel, 0xFF2A2E36, 1.5, P(-16, -228), P(4, -262), P(22, -228));
            dc.DrawLine(Pen(Steel, 5), P(17, -230), P(18, -210));
        }
        else
        {
            dc.DrawGeometry(Brush(Steel), Pen(0xFF2A2E36, 1.5), new EllipseGeometry(new Rect(head.X - 18, head.Y - 24, 36, 26)));
            dc.FillRectangle(Brush(trim), new Rect(head.X - 18, head.Y - 6, 36, 4));
        }
        dc.DrawLine(Pen(Mail, 6), P(-14, -218), P(-12, -198));
        // The sword arm and the blade.
        var shoulder = P(10, -192);
        var hand = P(hx, hy);
        dc.DrawLine(Pen(Mail, 12), shoulder, hand);
        double a = ang * Math.PI / 180;
        var tip = new Point(hand.X + dir * Math.Cos(a) * 118, hand.Y + Math.Sin(a) * 118);
        var guard1 = new Point(hand.X + dir * Math.Cos(a + Math.PI / 2) * 13, hand.Y + Math.Sin(a + Math.PI / 2) * 13);
        var guard2 = new Point(hand.X - dir * Math.Cos(a + Math.PI / 2) * 13, hand.Y - Math.Sin(a + Math.PI / 2) * 13);
        var pommel = new Point(hand.X - dir * Math.Cos(a) * 16, hand.Y - Math.Sin(a) * 16);
        dc.DrawLine(Pen(0xFF3A2410, 5), hand, pommel);
        dc.DrawEllipse(Brush(Gold), null, pommel, 4, 4);
        dc.DrawLine(Pen(0xFFD8DEE8, 6), hand, tip);
        dc.DrawLine(Pen(0xFF7A8290, 1.5), hand, tip);
        dc.DrawLine(Pen(Gold, 5), guard1, guard2);
        dc.DrawEllipse(Brush(Skin), Pen(0xFF6A4030, 1), hand, 6, 6);
        if (m.Act == Act.Wind && m.T > Raid.WindSeconds * 0.55)
            dc.DrawEllipse(Brush(0xA0FFFFFF), null, tip, 6 + Math.Sin(_clock * 40) * 2, 6 + Math.Sin(_clock * 40) * 2);
        if (m.Act == Act.Hurt) dc.DrawEllipse(Brush(Alpha(0xFFE02020, 0.5 * (1 - m.T / Raid.HurtSeconds))), null, P(0, -160), 40, 70);
    }

    private static void DrawLady(DrawingContext dc, Point feet)
    {
        double x = feet.X, y = feet.Y;
        Poly(dc, 0xFF2A5AA0, 0xFF1A1008, 1.5, new(x - 40, y), new(x - 16, y - 150), new(x + 16, y - 150), new(x + 40, y));
        dc.DrawLine(Pen(0xFFE8C040, 5), new Point(x - 16, y - 124), new Point(x + 16, y - 124));
        dc.DrawLine(Pen(0xFF2A5AA0, 10), new Point(x - 14, y - 146), new Point(x - 30, y - 96));
        dc.DrawEllipse(Brush(Skin), Pen(0xFF6A4030, 1), new Point(x, y - 168), 14, 17);
        Poly(dc, 0xFFD8B060, null, 0, new(x - 15, y - 176), new(x - 22, y - 110), new(x - 12, y - 110), new(x - 8, y - 168));
        Poly(dc, 0xFFF0EAD8, 0xFF8A7A6A, 1, new(x - 16, y - 180), new(x, y - 190), new(x + 16, y - 180), new(x + 18, y - 170), new(x - 18, y - 170));
    }

    // ───────────────────────────── the battle ─────────────────────────────

    public static Rect TacticRect(int i) => new(52 + i * 236, 700, 220, 48);

    private void DrawBattle(DrawingContext dc, Battle b)
    {
        dc.DrawImage(Art.Battlefield, new Rect(0, 0, ScreenWidth, ScreenHeight));
        bool castle = b.Walls > 1.01;
        if (castle)
        {
            // The castle the defenders hold, its wall broken as the siege left it.
            var wall = new Rect(860, 300, 164, 330);
            dc.FillRectangle(Brush(0xFF8A8478), wall);
            for (int k = 0; k < 10; k++) dc.DrawLine(Pen(0x602A2420, 1), new Point(860, 300 + k * 33), new Point(1024, 300 + k * 33));
            for (int k = 0; k < 6; k++) dc.FillRectangle(Brush(0xFF8A8478), new Rect(864 + k * 28, 284, 16, 18));
            double gap = (Sim.CastleWalls - b.Walls) / (Sim.CastleWalls - 1);
            if (gap > 0.05) Poly(dc, 0xFF3A3028, null, 0, new(860, 600 - 240 * gap), new(890, 560 - 200 * gap), new(900, 630), new(860, 630));
        }
        double clash = b.Phase == BattlePhase.Clash ? Math.Min(1, b.T / Battle.ClashSeconds) : b.Phase == BattlePhase.Done ? 1 : 0;
        for (int side = 0; side < 2; side++) DrawHost(dc, b, side, clash);
        if (b.Phase == BattlePhase.Clash && b.Last[0] != Tactic.Retreat && b.Last[1] != Tactic.Retreat)
        {
            // Dust where the lines meet.
            double near = Advance(b.Last[0], b.Last[1], clash);
            if (near > 0.5)
                for (int k = 0; k < 7; k++)
                {
                    double a = k * 0.9 + _clock;
                    dc.DrawEllipse(Brush(0x40B8A07A), null, new Point(512 + Math.Cos(a) * 60, 520 + Math.Sin(a) * 30 - k * 6), 60 + k * 6, 30);
                }
            // Boulders from the catapults, and the outlaws' arrows.
            for (int side = 0; side < 2; side++)
                if (b.Last[side] == Tactic.Bombard)
                    for (int k = 0; k < 3; k++)
                    {
                        double t = (clash * 2.2 + k * 0.33) % 1;
                        double sx = side == 0 ? 120 : 904, ex = side == 0 ? 760 + k * 40 : 264 - k * 40;
                        dc.DrawEllipse(Brush(0xFF6A6458), null, new Point(sx + (ex - sx) * t, 470 - 260 * t * (1 - t) * 2), 6, 6);
                    }
            if (b.Archers && clash < 0.6)
                for (int k = 0; k < 24; k++)
                {
                    double t = (clash / 0.6 + k * 0.04) % 1;
                    double sx = b.Mine == 0 ? 60 + k * 6 : 964 - k * 6, ex = b.Mine == 0 ? 720 + k * 9 : 300 - k * 9;
                    var at = new Point(sx + (ex - sx) * t, 440 - 300 * t * (1 - t) * 2 + k * 3);
                    var dir = new Vector((ex - sx) * 0.02, (1 - 2 * t) * -12);
                    dc.DrawLine(Pen(0xFF2A1A10, 1.5), at, at + dir);
                }
        }
        // Both hosts' numbers.
        for (int side = 0; side < 2; side++)
        {
            var plate = new Rect(side == 0 ? 16 : 618, 14, 390, 92);
            Plate(dc, plate);
            int lord = b.Lord[side];
            DrawShield(dc, lord >= 0 ? lord : Realm.Nobody, new Point(plate.X + 34, plate.Center.Y), 62);
            string name = lord >= 0 ? Realm.Lords[lord].Name.ToUpperInvariant() : "THE SHIRE'S VASSALS";
            Text(dc, name + (side == b.Mine ? " (YOU)" : ""), new Point(plate.X + 70, plate.Y + 22), name.Length > 18 ? 16 : 18, side == b.Mine ? Rubric : Ink);
            var left = b.Left(side);
            Text(dc, $"{left.Soldiers} SOLDIERS   {left.Knights} KNIGHTS" + (b.Catapults[side] > 0 ? $"   {b.Catapults[side]} CATAPULT{(b.Catapults[side] > 1 ? "S" : "")}" : ""),
                new Point(plate.X + 70, plate.Y + 48), 15, Ink);
            string note = side == 1 && b.Walls > 1.01 ? $"BEHIND THEIR WALLS (x{b.Walls:0.0})" : side == b.Mine && b.Archers ? "GIL'S ARCHERS WITH THEM" : $"LEADERSHIP x{b.Lead[side]:0.00}";
            Text(dc, note, new Point(plate.X + 70, plate.Y + 72), 14, 0xFF5A4A3A);
        }
        Text(dc, $"ROUND {Math.Max(1, b.Round + (b.Phase == BattlePhase.Choose ? 1 : 0))} OF {Battle.MaxRounds}", new Point(512, 130), 20, 0xFFFFF0D0, FellSC, Align.Center, Shadow);
        if (b.Round > 0 && b.Phase != BattlePhase.Done)
        {
            int me = Math.Max(0, b.Mine), them = 1 - me;
            Text(dc, $"YOUR {TacticName(b.Last[me])} MEETS THEIR {TacticName(b.Last[them])}: YOU LOSE {b.Fell[me]:0}, THEY LOSE {b.Fell[them]:0}",
                new Point(512, 158), 17, 0xFFFFF0D0, FellSC, Align.Center, Shadow);
        }
        if (b.Phase == BattlePhase.Choose && b.Mine >= 0)
        {
            for (int i = 0; i < 4; i++)
            {
                var t = (Tactic)i;
                bool can = t != Tactic.Bombard || b.CanBombard(b.Mine);
                int k = i;
                Plate(dc, TacticRect(i));
                if (Engine.TacticIndex == i) dc.FillRectangle(Brush(0x60E0B040), TacticRect(i).Deflate(3));
                Text(dc, $"{i + 1}  {TacticName(t)}", TacticRect(i).Center, 20, can ? (t == Tactic.Retreat ? Lapis : Rubric) : 0x802A1A0E, align: Align.Center);
                if (can) Hit(TacticRect(i), () => Engine.Command((Tactic)k));
            }
            Text(dc, "FIERCE BEATS BOMBARD, BOMBARD BEATS STAND FAST, STAND FAST BEATS FIERCE", new Point(512, 680), 15, 0xFFFFF0D0, FellSC, Align.Center, Shadow);
        }
        if (b.Phase == BattlePhase.Done)
            Banner(dc, b.Winner == b.Mine ? $"VICTORY! {b.Report}" : $"DEFEAT: {b.Report}", 220, 34, b.Winner == b.Mine ? Rubric : Ink);
    }

    private static string TacticName(Tactic t) => t switch
    {
        Tactic.StandFast => "STAND FAST", Tactic.Bombard => "BOMBARD", Tactic.FierceAttack => "FIERCE ATTACK", _ => "RETREAT",
    };

    /// <summary>How far a host's line moves forward during the clash (0 none, 1 to the middle; less than 0 falling back).</summary>
    private static double Advance(Tactic mine, Tactic theirs, double t)
    {
        double rush = Math.Sin(Math.Min(1, t * 1.6) * Math.PI / 2);
        return mine switch
        {
            Tactic.FierceAttack => rush,
            Tactic.StandFast => theirs == Tactic.FierceAttack ? 0.25 * rush : 0.1 * rush,
            Tactic.Bombard => 0,
            _ => -rush,
        };
    }

    /// <summary>A host: its knights in front, its soldiers in ranks behind, catapults at the back, the fallen dropping as the clash goes on.</summary>
    private void DrawHost(DrawingContext dc, Battle b, int side, double clash)
    {
        int lord = b.Lord[side];
        uint coat = lord >= 0 ? Realm.Lords[lord].Field : 0xFF8A7A5A;
        int dir = side == 0 ? 1 : -1;
        double baseX = side == 0 ? 300 : 724;
        double move = b.Phase == BattlePhase.Clash ? Advance(b.Last[side], b.Last[1 - side], clash) : 0;
        if (b.Phase == BattlePhase.Done && b.Winner != side) move = -0.6;
        double front = baseX + dir * move * 150;
        var left = b.Left(side);
        // The fallen of the round are still standing at its start.
        double shown = b.Strength(side) + (b.Phase == BattlePhase.Clash ? b.Fell[side] * (1 - Math.Clamp((clash - 0.4) / 0.5, 0, 1)) : 0);
        // Each figure stands for several men in a big host; those who fell this round drop out as the clash goes on.
        double grow = b.Strength(side) > 0.5 ? shown / b.Strength(side) : shown > 0 ? 1 : 0;
        double kn = b.Knights[side] * grow, so = b.Soldiers[side] * grow + (b.Strength(side) <= 0.5 ? shown : 0);
        double per = Math.Max(1, (b.Knights[side] + b.Soldiers[side] + b.Fell[side]) / 56.0);
        int knights = Math.Min(10, (int)Math.Ceiling(kn / per - 0.05));
        int foot = Math.Min(48, (int)Math.Ceiling(so / per - 0.05));
        if (left.Empty && b.Phase != BattlePhase.Clash) knights = foot = 0;
        bool melee = b.Phase == BattlePhase.Clash && move > 0.5;
        var dice = new Dice(side + 3);
        // Catapults at the back.
        for (int k = 0; k < Math.Min(3, b.Catapults[side]); k++)
            DrawCatapultIcon(dc, new Point(baseX - dir * (250 - k * 12), 440 + k * 40), 1.4 + k * 0.2);
        // Every figure in the host, placed on the field and drawn from the back of the field forwards.
        var figures = new List<(double X, double Y, bool Knight, int K)>();
        for (int k = 0; k < foot; k++)
        {
            int rank = k / 8, file = k % 8;
            double depth = file / 7.0;
            double y = 418 + file * 34 + dice.Range(-4, 4);
            double x = front - dir * (70 + rank * (30 + depth * 16) + (file % 2) * 12) + dice.Range(-5, 5);
            figures.Add((x, y, false, k));
        }
        for (int k = 0; k < knights; k++)
        {
            double y = 430 + k * (230.0 / Math.Max(1, knights)) + dice.Range(-4, 4);
            double x = front + dir * (8 + (k % 2) * 26) + dice.Range(-5, 5);
            figures.Add((x, y, true, k));
        }
        foreach (var f in figures.OrderBy(f => f.Y))
        {
            double s = 0.95 + (f.Y - 418) / 280 * 0.85;
            if (f.Knight) DrawHorseman(dc, new Point(f.X, f.Y), s * 1.15, coat, facingLeft: side == 1, gallop: b.Phase == BattlePhase.Clash && move > 0.05 ? _clock * 14 + f.K : 0);
            else
            {
                double jig = melee ? Math.Sin(_clock * 12 + f.K) * 4 : 0;
                DrawFootman(dc, new Point(f.X + jig, f.Y), s * 1.2, coat, facingLeft: side == 1, lean: melee ? 0.3 : 0);
            }
        }
        // The standard.
        double sx = front - dir * 90;
        dc.DrawLine(Pen(0xFF3A2A18, 3), new Point(sx, 470), new Point(sx, 380));
        var pts = new List<Point> { new(sx, 380) };
        for (int k = 1; k <= 8; k++) pts.Add(new Point(sx + dir * k * 6, 380 + Math.Sin(_clock * 6 + k * 0.8) * 2));
        for (int k = 8; k >= 1; k--) pts.Add(new Point(sx + dir * k * 6, 410 + Math.Sin(_clock * 6 + k * 0.8) * 2));
        Poly(dc, coat, 0xFF1A1008, 1, [.. pts]);
        DrawShield(dc, lord >= 0 ? lord : Realm.Nobody, new Point(sx + dir * 26, 395), 24);
    }
}
