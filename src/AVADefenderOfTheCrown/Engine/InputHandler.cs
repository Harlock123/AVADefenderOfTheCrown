using Avalonia.Input;

namespace AVADefenderOfTheCrown.Engine;

/// <summary>
/// The keys held, the mouse and the gamepad, boiled down to the controls. The arrows (or WASD, or the numeric pad)
/// point; Space, Enter or Z is fire; X or Shift the second button (a parry, the next missile); Backspace is back; G
/// storms a castle; the number keys pick from a menu. The mouse points (once it has moved, until a direction key is
/// pressed) and its left button fires, its right the second button. On a gamepad: the stick or D-pad points, A fires,
/// X is the second button, B is back and Y storms.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly Dictionary<PhysicalKey, Key> _pressedAs = [];
    private readonly ControllerManager? _pad;
    private (double X, double Y)? _point;

    public InputHandler(bool gamepad = true) => _pad = gamepad ? new ControllerManager(0) : null;

    public bool MouseLeft { get; set; }
    public bool MouseRight { get; set; }

    public void OnKeyDown(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        if (physical != PhysicalKey.None)
        {
            if (_pressedAs.TryGetValue(physical, out var was) && was != key) _held.Remove(was);
            _pressedAs[physical] = key;
        }
        _held.Remove(key);
        _held.Add(key);
        if (Direction(key)) _point = null;
    }

    /// <summary>A key comes up. The key it went down as is let go too: with the keyboard option
    /// shift:both_capslock_cancel, letting go of a Shift arrives as CapsLock, and the Shift would stay held.</summary>
    public void OnKeyUp(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        _held.Remove(key);
        if (physical != PhysicalKey.None && _pressedAs.Remove(physical, out var was)) _held.Remove(was);
    }

    public bool Has(Key key) => _held.Contains(key);
    private bool Any(params Key[] keys) => keys.Any(_held.Contains);

    /// <summary>Let go of everything (the window lost the focus).</summary>
    public void Clear()
    {
        _held.Clear();
        _pressedAs.Clear();
        MouseLeft = MouseRight = false;
    }

    /// <summary>The mouse moved to a point on the screen (or off it).</summary>
    public void PointAt((double X, double Y)? at) => _point = at;

    public void Update() => _pad?.Update();

    private static bool Direction(Key k) => k is Key.Left or Key.Right or Key.Up or Key.Down or Key.A or Key.D or Key.W or Key.S
        or Key.NumPad2 or Key.NumPad4 or Key.NumPad6 or Key.NumPad8;

    /// <summary>The newer of two opposite keys wins, so rolling from one to the other turns at once.</summary>
    private int Axis(Key[] minus, Key[] plus)
    {
        for (int i = _held.Count - 1; i >= 0; i--)
        {
            if (minus.Contains(_held[i])) return -1;
            if (plus.Contains(_held[i])) return 1;
        }
        return 0;
    }

    /// <summary>The controls this step. Up is -1, as on the screen (and in the engine).</summary>
    public Controls Player
    {
        get
        {
            int dx = Axis([Key.Left, Key.A, Key.NumPad4], [Key.Right, Key.D, Key.NumPad6]);
            int dy = Axis([Key.Up, Key.W, Key.NumPad8], [Key.Down, Key.S, Key.NumPad2]);
            bool fire = Any(Key.Space, Key.Enter, Key.Z) || MouseLeft;
            bool alt = Any(Key.X, Key.LeftShift, Key.RightShift) || MouseRight;
            bool back = Any(Key.Back);
            bool storm = Any(Key.G);
            int pick = 0;
            for (int k = 1; k <= 9 && pick == 0; k++)
                if (Any(Key.D0 + k, Key.NumPad0 + k)) pick = k;
            if (_pad is { } pad)
            {
                if (dx == 0) dx = pad.DPadLeft || pad.LeftStickX < -0.4f ? -1 : pad.DPadRight || pad.LeftStickX > 0.4f ? 1 : 0;
                if (dy == 0) dy = pad.DPadUp || pad.LeftStickY < -0.4f ? -1 : pad.DPadDown || pad.LeftStickY > 0.4f ? 1 : 0;
                fire |= pad.ButtonA;
                alt |= pad.ButtonX;
                back |= pad.ButtonB;
                storm |= pad.ButtonY;
                if (dx != 0 || dy != 0) _point = null;
            }
            return new Controls(dx, dy, fire, alt, back, pick, dx == 0 && dy == 0 ? _point : null, storm);
        }
    }

    /// <summary>Enter, or the gamepad's Start.</summary>
    public bool Start => Any(Key.Enter) || (_pad?.ButtonStart ?? false);

    public bool PadPause => _pad?.ButtonStart ?? false;
    public bool PadBack => _pad?.ButtonBack ?? false;
    public bool PadUp => _pad is { } p && (p.DPadUp || p.LeftStickY < -0.5f);
    public bool PadDown => _pad is { } p && (p.DPadDown || p.LeftStickY > 0.5f);
    public bool PadLeft => _pad is { } p && (p.DPadLeft || p.LeftStickX < -0.5f);
    public bool PadRight => _pad is { } p && (p.DPadRight || p.LeftStickX > 0.5f);
    public bool PadAccept => _pad is { } p && (p.ButtonA || p.ButtonX);

    public void Dispose() => _pad?.Dispose();
}
