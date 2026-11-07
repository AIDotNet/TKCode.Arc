using Avalonia;
using TKCode.Arc.Gestures;

namespace TKCode.Arc;

/// <summary>What a pull input asks the article to do.</summary>
internal enum CardToArticlePullResult
{
    /// <summary>Nothing to do (the input was not a pull).</summary>
    None,

    /// <summary>The pull distance changed: follow it.</summary>
    Follow,

    /// <summary>Let go short of the threshold: spring back.</summary>
    SnapBack,

    /// <summary>Close the article.</summary>
    Dismiss
}

/// <summary>
/// Pull-to-dismiss of the open article (Arc Card to article, design reference): one distance d (px, down is positive,
/// 0 to 480, no rubber band) fed by three inputs, and the lift it maps to (down 0.3·d, scale 1 − d/1500, origin 50% 30%
/// of the window).
/// <list type="bullet">
/// <item>Mouse: only from the picture; follows from the first pixel; d is measured from the press.</item>
/// <item>Touch: anywhere; takes over only when the article is scrolled to the top and the finger moves down. A gesture
/// that starts at the top waits for <see cref="TouchSlop"/> and is measured from that point; a gesture that starts
/// scrolled first scrolls the article and hands the rest of the same movement over to the pull once the top is reached.</item>
/// <item>Wheel / touchpad at the top scrolling up: d += 0.6·|dy|; past 120 it closes at once (later events are
/// ignored); after <see cref="WheelIdle"/> without events it springs back.</item>
/// </list>
/// Releasing past <see cref="DismissDistance"/> or faster than <see cref="DismissVelocity"/> downward closes; otherwise
/// the lift springs back. Pure state (times come from the caller), so it is tested without a window.
/// <para>
/// SheetStack (Surfaces) needs a similar pull-to-dismiss; this helper is self-contained so the two can be merged into
/// <c>Surfaces/_Shared</c> later.
/// </para>
/// </summary>
internal sealed class CardToArticlePullGesture
{
    public const double MaxDistance = 480;
    public const double DismissDistance = 120;
    public const double DismissVelocity = 600;
    public const double TranslateFactor = 0.3;
    public const double ScaleDivisor = 1500;
    public const double WheelFactor = 0.6;

    /// <summary>Touch movement before a pull starts at the top (Arc: 11px did not start one, 16px did).</summary>
    public const double TouchSlop = 14;

    /// <summary>The lift's origin as a fraction of the window (50% across, 30% down).</summary>
    public static readonly RelativePoint Origin = new(0.5, 0.3, RelativeUnit.Relative);

    public static readonly TimeSpan WheelIdle = TimeSpan.FromMilliseconds(150);

    private enum Mode
    {
        Idle,
        MousePull,
        TouchPending,
        TouchScrolling,
        TouchPull,
        Wheel,
        Dismissed
    }

    private readonly VelocityTracker _velocity = new();
    private Mode _mode;
    private double _origin;
    private double _lastY;
    private TimeSpan _lastWheel;

    /// <summary>The pull distance (0 to <see cref="MaxDistance"/>).</summary>
    public double Distance { get; private set; }

    /// <summary>True while a pointer or the wheel is pulling.</summary>
    public bool IsPulling => _mode is Mode.MousePull or Mode.TouchPull or Mode.Wheel;

    /// <summary>True between a touch press and its release (whether it pulls or scrolls).</summary>
    public bool IsTracking => _mode is Mode.MousePull or Mode.TouchPending or Mode.TouchScrolling or Mode.TouchPull;

    /// <summary>The downward release velocity of the last pointer pull (px/s).</summary>
    public double ReleaseVelocity { get; private set; }

    /// <summary>The lift for a pull distance: how far the sheet moves down and how much it shrinks.</summary>
    public static (double TranslateY, double Scale) Lift(double distance)
    {
        var d = Math.Clamp(distance, 0, MaxDistance);
        return (TranslateFactor * d, 1 - d / ScaleDivisor);
    }

    /// <summary>The lift as a matrix about <paramref name="origin"/> (px in the window).</summary>
    public static Matrix LiftMatrix(double distance, Point origin)
    {
        var (dy, s) = Lift(distance);
        return Matrix.CreateTranslation(-origin.X, -origin.Y) * Matrix.CreateScale(s, s) *
               Matrix.CreateTranslation(origin.X, origin.Y + dy);
    }

    /// <summary>Starts over (the article opened or closed).</summary>
    public void Reset()
    {
        _mode = Mode.Idle;
        Distance = 0;
        ReleaseVelocity = 0;
        _velocity.Reset();
    }

    /// <summary>Takes the distance the lift springs back from (the caller animates it and reports nothing back).</summary>
    public void Settle() => Distance = 0;

    /// <summary>A mouse (or pen) press on the picture: the pull follows from here.</summary>
    /// <param name="y">Pointer position (px).</param>
    /// <param name="time">Event time.</param>
    /// <param name="current">The lift's current distance (a press during a spring back catches it where it is).</param>
    public void PressMouse(double y, TimeSpan time, double current = 0)
    {
        if (_mode == Mode.Dismissed)
        {
            return;
        }
        _mode = Mode.MousePull;
        Distance = Math.Clamp(current, 0, MaxDistance);
        _origin = y - Distance;
        _lastY = y;
        _velocity.Reset();
        _velocity.AddSample(new Point(0, y), time);
    }

    /// <summary>A touch press anywhere on the article; <paramref name="atTop"/> says whether the article is scrolled to the top.</summary>
    public void PressTouch(double y, bool atTop, TimeSpan time)
    {
        if (_mode == Mode.Dismissed)
        {
            return;
        }
        _mode = atTop ? Mode.TouchPending : Mode.TouchScrolling;
        _origin = y;
        _lastY = y;
        _velocity.Reset();
        _velocity.AddSample(new Point(0, y), time);
    }

    /// <summary>
    /// A pointer move. Returns <see cref="CardToArticlePullResult.Follow"/> while the pull owns the movement (the caller
    /// stops the article from scrolling), otherwise <see cref="CardToArticlePullResult.None"/>.
    /// </summary>
    public CardToArticlePullResult Move(double y, bool atTop, TimeSpan time)
    {
        var previous = _lastY;
        _lastY = y;
        switch (_mode)
        {
            case Mode.MousePull:
            case Mode.TouchPull:
                _velocity.AddSample(new Point(0, y), time);
                Distance = Math.Clamp(y - _origin, 0, MaxDistance);
                return CardToArticlePullResult.Follow;
            case Mode.TouchPending:
                if (!atTop)
                {
                    _mode = Mode.TouchScrolling;
                    return CardToArticlePullResult.None;
                }
                if (y - _origin >= TouchSlop)
                {
                    // Measured from the point where the pull took over.
                    _mode = Mode.TouchPull;
                    _origin = y;
                    _velocity.Reset();
                    _velocity.AddSample(new Point(0, y), time);
                    Distance = 0;
                    return CardToArticlePullResult.Follow;
                }
                return CardToArticlePullResult.None;
            case Mode.TouchScrolling:
                if (atTop && y > previous)
                {
                    // The content reached its top during this movement: the rest of it pulls.
                    _mode = Mode.TouchPull;
                    _origin = previous;
                    _velocity.Reset();
                    _velocity.AddSample(new Point(0, previous), time);
                    _velocity.AddSample(new Point(0, y), time);
                    Distance = Math.Clamp(y - _origin, 0, MaxDistance);
                    return CardToArticlePullResult.Follow;
                }
                return CardToArticlePullResult.None;
            default:
                return CardToArticlePullResult.None;
        }
    }

    /// <summary>The pointer was released: dismiss, spring back or nothing.</summary>
    public CardToArticlePullResult Release(TimeSpan time)
    {
        var pulling = _mode is Mode.MousePull or Mode.TouchPull;
        if (_mode != Mode.Dismissed && _mode != Mode.Wheel)
        {
            _mode = Mode.Idle;
        }
        if (!pulling)
        {
            return CardToArticlePullResult.None;
        }
        ReleaseVelocity = _velocity.GetVelocity(time).Y;
        _velocity.Reset();
        if (Distance > DismissDistance || ReleaseVelocity > DismissVelocity)
        {
            _mode = Mode.Dismissed;
            return CardToArticlePullResult.Dismiss;
        }
        return Distance > 0 ? CardToArticlePullResult.SnapBack : CardToArticlePullResult.None;
    }

    /// <summary>The pointer was lost (capture taken away): spring back.</summary>
    public CardToArticlePullResult Cancel()
    {
        var pulling = _mode is Mode.MousePull or Mode.TouchPull;
        if (_mode != Mode.Dismissed)
        {
            _mode = Mode.Idle;
        }
        _velocity.Reset();
        return pulling && Distance > 0 ? CardToArticlePullResult.SnapBack : CardToArticlePullResult.None;
    }

    /// <summary>
    /// A wheel event: <paramref name="upPixels"/> is how far it scrolls up (negative scrolls down). At the top, scrolling up
    /// pulls by 0.6 of the distance; while pulled, scrolling down takes the pull back first.
    /// </summary>
    public CardToArticlePullResult Wheel(double upPixels, bool atTop, TimeSpan time)
    {
        if (_mode == Mode.Dismissed || IsTracking)
        {
            return _mode == Mode.Dismissed ? CardToArticlePullResult.Follow : CardToArticlePullResult.None;
        }
        var pulled = _mode == Mode.Wheel && Distance > 0;
        if (!pulled && !(atTop && upPixels > 0))
        {
            return CardToArticlePullResult.None;
        }
        _mode = Mode.Wheel;
        _lastWheel = time;
        Distance = Math.Clamp(Distance + WheelFactor * upPixels, 0, MaxDistance);
        if (Distance > DismissDistance)
        {
            _mode = Mode.Dismissed;
            return CardToArticlePullResult.Dismiss;
        }
        if (Distance <= 0)
        {
            _mode = Mode.Idle;
        }
        return CardToArticlePullResult.Follow;
    }

    /// <summary>
    /// Asked by the caller's idle timer: true when a wheel pull has had no event for <see cref="WheelIdle"/> and should
    /// spring back (the pull is over).
    /// </summary>
    public bool WheelIdleElapsed(TimeSpan now)
    {
        if (_mode != Mode.Wheel || now - _lastWheel < WheelIdle)
        {
            return false;
        }
        _mode = Mode.Idle;
        return true;
    }
}
