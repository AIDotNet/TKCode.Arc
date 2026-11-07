using Avalonia;

namespace TKCode.Arc;

/// <summary>What a pointer move did to a <see cref="StretchRefreshPullGesture"/>.</summary>
internal enum StretchRefreshPullStep
{
    /// <summary>Nothing to do: still under the slop, or the gesture is not a pull.</summary>
    None,

    /// <summary>The pull began with this move (capture the pointer).</summary>
    Started,

    /// <summary>The pull moved.</summary>
    Moved,

    /// <summary>The gesture turned out not to be a pull (leave it to the scroller).</summary>
    Rejected
}

/// <summary>
/// The pointer and wheel side of a pull, without any UI (measured on Arc Stretch refresh, design reference):
/// <list type="bullet">
/// <item>The raw distance is the vertical difference between the pointer and where it was pressed (no slop is
/// subtracted, no axis lock: a mostly sideways drag that goes more than 4 px down still pulls). It is clamped at 0, so
/// dragging back up returns the sheet to the top while the pull is still held.</item>
/// <item>A mouse or pen pull starts only if the feed is at its top when the move crosses the slop; a touch pull only
/// if the feed was at its top when the finger went down (otherwise the finger scrolls the feed, and that gesture never
/// becomes a pull). A first move that goes up rejects a touch gesture.</item>
/// <item>The wheel pulls only at the top, only upwards; each event adds its distance and the pull ends
/// <see cref="StretchRefreshMath.WheelIdle"/> after the last event.</item>
/// </list>
/// Arc tracks the move only while the pointer is over the feed, so a pull whose first move leaves the feed gets stuck;
/// the control feeds this gesture with window-level moves instead.
/// </summary>
internal sealed class StretchRefreshPullGesture
{
    private Point _origin;
    private double _base;
    private bool _touch;
    private bool _pressed;
    private bool _rejected;
    private bool _wheeling;
    private TimeSpan _lastWheel;

    /// <summary>True while a pointer pull is in progress (after the slop).</summary>
    public bool IsPointerPulling { get; private set; }

    /// <summary>True while a wheel pull is in progress.</summary>
    public bool IsWheelPulling => _wheeling;

    /// <summary>True while a pointer is down and tracked (pending or pulling).</summary>
    public bool IsPressed => _pressed;

    /// <summary>The raw pull distance (before resistance), 0 or more.</summary>
    public double Distance { get; private set; }

    /// <summary>
    /// A pointer went down. <paramref name="atTop"/>: whether the feed is scrolled to its top now; <paramref name="from"/>:
    /// the raw distance to start from (a sheet still springing back is caught where it is).
    /// </summary>
    public void Press(Point position, bool touch, bool atTop, double from = 0)
    {
        _origin = position;
        _touch = touch;
        _pressed = true;
        _rejected = touch && !atTop;
        _base = Math.Max(0, from);
        IsPointerPulling = false;
        Distance = 0;
    }

    /// <summary>The pointer moved. <paramref name="atTop"/> is read only when the move could start the pull.</summary>
    public StretchRefreshPullStep Move(Point position, Func<bool> atTop)
    {
        if (!_pressed || _rejected)
        {
            return StretchRefreshPullStep.None;
        }
        var d = position.Y - _origin.Y;
        if (!IsPointerPulling)
        {
            if (_touch && d < -StretchRefreshMath.StartSlop)
            {
                _rejected = true;
                return StretchRefreshPullStep.Rejected;
            }
            if (d <= StretchRefreshMath.StartSlop)
            {
                return StretchRefreshPullStep.None;
            }
            if (!atTop())
            {
                _rejected = true;
                return StretchRefreshPullStep.Rejected;
            }
            IsPointerPulling = true;
            Distance = Math.Max(0, _base + d);
            return StretchRefreshPullStep.Started;
        }
        Distance = Math.Max(0, _base + d);
        return StretchRefreshPullStep.Moved;
    }

    /// <summary>The pointer went up (or was lost). Returns true when a pull was in progress.</summary>
    public bool Release()
    {
        var was = IsPointerPulling;
        _pressed = false;
        _rejected = false;
        IsPointerPulling = false;
        if (was && !_wheeling)
        {
            Distance = 0;
        }
        return was;
    }

    /// <summary>
    /// A wheel event: <paramref name="pixels"/> is the distance it scrolls, positive upwards (towards the top of the
    /// feed). Returns true when the event belongs to the pull (the control marks it handled so the feed does not scroll).
    /// <paramref name="from"/> is the raw distance a new wheel pull starts from.
    /// </summary>
    public bool Wheel(double pixels, bool atTop, TimeSpan now, double from = 0)
    {
        if (IsPointerPulling)
        {
            return false;
        }
        if (!_wheeling)
        {
            if (!atTop || pixels <= 0)
            {
                return false;
            }
            _wheeling = true;
            Distance = Math.Max(0, from);
        }
        Distance = Math.Max(0, Distance + pixels);
        _lastWheel = now;
        return true;
    }

    /// <summary>True when a wheel pull has had no event for <see cref="StretchRefreshMath.WheelIdle"/>.</summary>
    public bool IsWheelIdle(TimeSpan now) => _wheeling && now - _lastWheel >= StretchRefreshMath.WheelIdle;

    /// <summary>Ends a wheel pull. Returns true when one was in progress.</summary>
    public bool EndWheel()
    {
        var was = _wheeling;
        _wheeling = false;
        if (was)
        {
            Distance = 0;
        }
        return was;
    }

    /// <summary>Forgets everything (detach, a refresh started from elsewhere).</summary>
    public void Reset()
    {
        _pressed = false;
        _rejected = false;
        _wheeling = false;
        IsPointerPulling = false;
        Distance = 0;
    }
}
