using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The measured physics of a lifted card (Arc Kanban board, design reference): the lift threshold, the lift scale, the
/// tilt from horizontal speed, and the small spring a dropped card rides into its slot.
/// </summary>
internal static class KanbanDragPhysics
{
    /// <summary>A mouse or pen press lifts its card once the pointer has moved farther than this (5.0 does not, 5.2 does).</summary>
    public const double Threshold = 5;

    /// <summary>The lifted card's scale.</summary>
    public const double LiftScale = 1.035;

    /// <summary>Degrees of tilt per px/s of horizontal pointer speed, and the most tilt.</summary>
    public const double TiltPerVelocity = 0.0064;

    public const double MaxTilt = 7;

    /// <summary>A touch press lifts its card after this long without moving more than <see cref="TouchSlop"/>.</summary>
    public static readonly TimeSpan TouchHoldDelay = TimeSpan.FromMilliseconds(250);

    public const double TouchSlop = 10;

    /// <summary>
    /// The drop spring (k 690, c 51: ζ ≈ 0.97, about 0.2s; fitted to three drops). It chases the slot's live position
    /// and starts with the throw velocity. Escape returns ride it too.
    /// </summary>
    public static readonly SpringSpec Catch = SpringSpec.FromPhysical(690, 51);

    /// <summary>Layout motion: neighbours, the slot, keyboard moves, the swimlanes flight, the drawer, the composer.</summary>
    public static readonly SpringSpec Layout = ArcMotionPresets.Smooth;

    /// <summary>Lift scale, tilt, count wheels and the over-limit chip.</summary>
    public static readonly SpringSpec Quick = ArcMotionPresets.Quick;

    /// <summary>The tilt for a horizontal speed of <paramref name="velocityX"/> px/s: clockwise to the right, at most 7°.</summary>
    public static double TiltFor(double velocityX)
    {
        if (!double.IsFinite(velocityX))
        {
            return 0;
        }
        return Math.Clamp(velocityX * TiltPerVelocity, -MaxTilt, MaxTilt);
    }

    /// <summary>True when the pointer has moved far enough from the press to lift (strictly farther than the threshold).</summary>
    public static bool PassedThreshold(Point press, Point now, double threshold = Threshold)
    {
        var dx = now.X - press.X;
        var dy = now.Y - press.Y;
        return dx * dx + dy * dy > threshold * threshold;
    }
}

/// <summary>What a <see cref="KanbanHoldRecognizer"/> decided.</summary>
internal enum KanbanHoldState
{
    /// <summary>No press is being watched.</summary>
    Idle,

    /// <summary>A touch press is held, still waiting for the delay.</summary>
    Pending,

    /// <summary>The press was held long enough: lift the card.</summary>
    Lifted,

    /// <summary>The finger moved before the delay: let the board pan.</summary>
    Panning
}

/// <summary>
/// Touch press-and-hold for the Kanban board: a touch lifts its card after <see cref="Delay"/> without moving more than
/// <see cref="Slop"/>; moving earlier hands the gesture to the scroll viewer, so swipes still scroll the board. A pure
/// state machine (the board feeds it times), so tests do not depend on a timer.
/// </summary>
internal sealed class KanbanHoldRecognizer
{
    private Point _start;
    private TimeSpan _pressedAt;

    public KanbanHoldRecognizer(TimeSpan? delay = null, double slop = KanbanDragPhysics.TouchSlop)
    {
        Delay = delay ?? KanbanDragPhysics.TouchHoldDelay;
        Slop = slop;
    }

    public TimeSpan Delay { get; set; }

    public double Slop { get; }

    public KanbanHoldState State { get; private set; }

    /// <summary>Where the press started.</summary>
    public Point Start => _start;

    /// <summary>A touch went down.</summary>
    public void Press(Point position, TimeSpan time)
    {
        _start = position;
        _pressedAt = time;
        State = KanbanHoldState.Pending;
    }

    /// <summary>The touch moved; returns the new state.</summary>
    public KanbanHoldState Move(Point position, TimeSpan time)
    {
        if (State == KanbanHoldState.Pending)
        {
            if (KanbanDragPhysics.PassedThreshold(_start, position, Slop))
            {
                State = KanbanHoldState.Panning;
            }
            else
            {
                Tick(time);
            }
        }
        return State;
    }

    /// <summary>Time passed; returns <see cref="KanbanHoldState.Lifted"/> once the hold is long enough.</summary>
    public KanbanHoldState Tick(TimeSpan time)
    {
        if (State == KanbanHoldState.Pending && time - _pressedAt >= Delay)
        {
            State = KanbanHoldState.Lifted;
        }
        return State;
    }

    /// <summary>The touch ended or was taken; back to idle.</summary>
    public void Reset() => State = KanbanHoldState.Idle;
}
