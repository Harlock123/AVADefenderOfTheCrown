using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AVADefenderOfTheCrown.Engine;

namespace AVADefenderOfTheCrown.Views;

/// <summary>
/// The screen: a fixed 1024×768 picture scaled to fit the window. On the map, the painted map of England on the left
/// and the lord's parchment on the right with his treasury, his armies and the orders; then the scenes: the joust,
/// the siege, the raid and the battle; and the cards that tell the story. Runs the engine from a 60 Hz timer, and
/// turns the mouse and the keys into the engine's calls.
/// </summary>
public sealed partial class GameCanvas : Control
{
    public const double ScreenWidth = 1024;
    public const double ScreenHeight = 768;

    private readonly DispatcherTimer? _timer;
    private DateTime _last = DateTime.Now;
    private double _clock;
    private readonly List<(Rect R, Action Do)> _hits = [];
    private int _hover = -1;
    private bool _padPausePrev, _padBackPrev, _padUpPrev, _padDownPrev, _padLeftPrev, _padRightPrev, _padAcceptPrev, _padDonePrev;

    public GameEngine Engine { get; }
    public SoundManager Sound { get; }
    public InputHandler Input { get; }

    public GameCanvas(bool silent = false) : this(new GameEngine(), new SoundManager(silent), startTimer: true) { }

    /// <summary>For tests: an engine of your own, and no timer (call <see cref="Tick"/>), and no gamepad.</summary>
    public GameCanvas(GameEngine engine, SoundManager sound, bool startTimer)
    {
        Engine = engine;
        Sound = sound;
        Input = new InputHandler(gamepad: startTimer);
        Engine.Sound += Sound.Play;
        Focusable = true;
        ClipToBounds = true;
        if (startTimer)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, _) => Tick();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer?.Start();
        Focus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        Sound.Dispose();
        Input.Dispose();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Input.Clear();
    }

    /// <summary>One frame: read the controls, run the game, redraw.</summary>
    public void Tick()
    {
        var now = DateTime.Now;
        double dt = Math.Min(0.1, (now - _last).TotalSeconds);
        _last = now;
        _clock += dt;
        Input.Update();
        HandlePad();
        Engine.Update(dt, Input.Player, Input.Start);
        InvalidateVisual();
    }

    /// <summary>For tests: moves the drawing clock (flags, torches, waves) without running the game.</summary>
    public void Advance(double dt)
    {
        _clock += dt;
        InvalidateVisual();
    }

    private void HandlePad()
    {
        bool Edge(bool now, ref bool prev) { bool e = now && !prev; prev = now; return e; }
        if (Edge(Input.PadPause, ref _padPausePrev))
        {
            if (Engine.State == GameState.Playing) Engine.Pause();
            else if (Engine.State == GameState.Paused) Engine.Resume();
        }
        if (Edge(Input.PadBack, ref _padBackPrev))
        {
            if (Engine.State == GameState.Paused) (TopLevel.GetTopLevel(this) as Window)?.Close();
            else if (Engine.State == GameState.Playing) Engine.Pause();
            else if (Engine.State is GameState.Attract or GameState.HighScores or GameState.Choose) Engine.ReturnToTitle();
        }
        if (Engine.State == GameState.EnterInitials && Engine.Initials is { } entry)
        {
            if (Edge(Input.PadUp, ref _padUpPrev)) entry.Cycle(1);
            if (Edge(Input.PadDown, ref _padDownPrev)) entry.Cycle(-1);
            if (Edge(Input.PadLeft, ref _padLeftPrev)) entry.Back();
            if (Edge(Input.PadRight, ref _padRightPrev)) entry.Move(1);
            if (Edge(Input.PadAccept, ref _padAcceptPrev)) entry.Advance();
            if (Edge(Input.PadPause, ref _padDonePrev)) entry.Finish();
        }
    }

    // ───────────────────────────── keys ─────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var g = Engine;
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (e.Key == Key.S && ctrl)
        {
            SaveScreenshot(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), $"AVADefenderOfTheCrown_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"));
            return;
        }
        // While entering initials every key belongs to the entry.
        if (g.State == GameState.EnterInitials && g.Initials is { } entry)
        {
            InitialsKey(entry, e.Key);
            return;
        }
        PressKey(e.Key, e.PhysicalKey);
    }

    /// <summary>A key pressed (for tests too): the keys that do one thing once, then the controls.</summary>
    public void PressKey(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        var g = Engine;
        if (g.State == GameState.Attract) { g.ReturnToTitle(); return; }
        switch (key)
        {
            case Key.M:
                Sound.IsMuted = !Sound.IsMuted;
                return;
            case Key.P or Key.Pause or Key.Escape when g.State == GameState.Playing:
                g.Pause();
                return;
            case Key.P or Key.Pause or Key.Escape when g.State == GameState.Paused:
                g.Resume();
                return;
            case Key.Escape when g.State is GameState.HighScores or GameState.Choose:
                g.ReturnToTitle();
                return;
            case Key.Escape when g.State == GameState.Title:
                (TopLevel.GetTopLevel(this) as Window)?.Close();
                return;
            case Key.C when g.State == GameState.Title && g.HasSave:
                g.Continue();
                return;
        }
        if (g.State == GameState.Paused) return;
        Input.OnKeyDown(key, physical);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        Input.OnKeyUp(e.Key, e.PhysicalKey);
        e.Handled = true;
    }

    private static void InitialsKey(InitialsEntry entry, Key key)
    {
        switch (key)
        {
            case Key.Up: entry.Cycle(1); break;
            case Key.Down: entry.Cycle(-1); break;
            case Key.Left or Key.Back: entry.Back(); break;
            case Key.Right: entry.Move(1); break;
            case Key.Space: entry.Advance(); break;
            case Key.Enter: entry.Finish(); break;
            case Key.OemPeriod or Key.Decimal: entry.Type('.'); break;
            case >= Key.A and <= Key.Z: entry.Type((char)('A' + (key - Key.A))); break;
            case >= Key.D0 and <= Key.D9: entry.Type((char)('0' + (key - Key.D0))); break;
            case >= Key.NumPad0 and <= Key.NumPad9: entry.Type((char)('0' + (key - Key.NumPad0))); break;
        }
    }

    // ───────────────────────────── the mouse ─────────────────────────────

    /// <summary>A point in the window to the fixed screen.</summary>
    private Point ToScreen(Point p)
    {
        var (s, ox, oy) = Fit();
        return s <= 0 ? new Point(-1, -1) : new Point((p.X - ox) / s, (p.Y - oy) / s);
    }

    /// <summary>The shire under a point on the screen, if the map is showing.</summary>
    public int ShireAt(Point at)
    {
        if (Engine.State is not (GameState.Playing or GameState.Attract) || Engine.Scene != Scene.Map || at.X >= MapArt.W) return -1;
        int s = MapArt.Shared.ShireAt(at.X, at.Y);
        return s >= 0 ? s : -1;
    }

    /// <summary>The button (or other place that answers a click) under a point, topmost first.</summary>
    private Action? HitAt(Point at)
    {
        for (int i = _hits.Count - 1; i >= 0; i--)
            if (_hits[i].R.Contains(at)) return _hits[i].Do;
        return null;
    }

    private bool InScene => Engine.State is GameState.Playing && Engine.Scene is Scene.Joust or Scene.Siege or Scene.Raid or Scene.Battle;

    /// <summary>A click on the screen at a point: a button, a shire, or (in a scene) the fire button. For tests too.</summary>
    public void Click(Point at, bool right = false)
    {
        var g = Engine;
        if (g.State == GameState.Attract) { g.ReturnToTitle(); return; }
        if (g.State == GameState.Paused) { g.Resume(); return; }
        if (!right && HitAt(at) is { } act) { act(); return; }
        if (g.State == GameState.Title && !right) { g.NewGame(); return; }
        if (g.State != GameState.Playing) return;
        switch (g.Scene)
        {
            case Scene.Card:
                g.DismissCard();
                return;
            case Scene.Map:
                if (right) { g.Cancel(); return; }
                if (ShireAt(at) is >= 0 and var s) g.ClickShire(s);
                return;
            case Scene.Raid:
                // A click on the guard: a stroke at the height clicked; the right button parries.
                if (right) g.Sword(null, parry: true);
                else g.Sword(at.Y < RaidHigh ? Line.High : at.Y > RaidLow ? Line.Low : Line.Middle);
                return;
            default:
                if (right) Input.MouseRight = true; else Input.MouseLeft = true;
                Input.PointAt((at.X, at.Y));
                return;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var at = ToScreen(e.GetPosition(this));
        _hover = ShireAt(at);
        if (Engine.Scene == Scene.Map && Engine.Mode == MapMode.Orders && _hover >= 0) Engine.Selected = _hover;
        if (InScene && Engine.Scene is Scene.Joust or Scene.Siege) Input.PointAt((at.X, at.Y));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus();
        var at = ToScreen(e.GetPosition(this));
        var props = e.GetCurrentPoint(this).Properties;
        Click(at, right: props.IsRightButtonPressed || props.IsMiddleButtonPressed);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Input.MouseLeft = Input.MouseRight = false;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        Input.PointAt(null);
    }

    /// <summary>Saves what's on screen as a PNG.</summary>
    public void SaveScreenshot(string path)
    {
        var size = new PixelSize((int)Math.Max(1, Bounds.Width), (int)Math.Max(1, Bounds.Height));
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(this);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    /// <summary>How the fixed screen fits the window: scale and letterbox offsets.</summary>
    private (double Scale, double X, double Y) Fit()
    {
        double s = Math.Min(Bounds.Width / ScreenWidth, Bounds.Height / ScreenHeight);
        return (s, (Bounds.Width - ScreenWidth * s) / 2, (Bounds.Height - ScreenHeight * s) / 2);
    }

    public override void Render(DrawingContext dc)
    {
        dc.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        var (s, ox, oy) = Fit();
        if (s <= 0) return;
        using (dc.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(ox, oy)))
        using (dc.PushClip(new Rect(0, 0, ScreenWidth, ScreenHeight)))
            DrawScreen(dc);
    }
}
