using Avalonia;

namespace TKCode.Arc.Gestures;

/// <summary>Which axes a drag follows.</summary>
public enum DragAxis
{
    /// <summary>Free movement on both axes.</summary>
    Both,

    /// <summary>Locked to X; a gesture that starts mostly vertical is rejected (vertical scrolling passes through).</summary>
    Horizontal,

    /// <summary>Locked to Y; a gesture that starts mostly horizontal is rejected.</summary>
    Vertical
}

/// <summary>State of a <see cref="DragRecognizer"/>.</summary>
public enum DragPhase
{
    /// <summary>No pointer is down.</summary>
    Idle,

    /// <summary>Pressed, still inside the threshold.</summary>
    Pending,

    /// <summary>The threshold was crossed along an accepted axis; moves are reported.</summary>
    Dragging,

    /// <summary>The gesture went the other way (or was refused); ignored until release.</summary>
    Rejected
}

/// <summary>What a <see cref="DragRecognizer.Move"/> call changed.</summary>
public enum DragTransition
{
    None,
    Started,
    Moved,
    Rejected
}

/// <summary>Drag recognition options (S10).</summary>
public sealed class DragOptions
{
    /// <summary>Distance in px the pointer must travel before the drag starts (a plain click stays a click).</summary>
    public double Threshold { get; init; } = 4;

    /// <summary>Axis lock. Offsets on the other axis are reported as zero.</summary>
    public DragAxis Axis { get; init; } = DragAxis.Both;

    /// <summary>
    /// With a locked axis, reject a gesture whose first movement is mostly along the other axis (scroll pass-through).
    /// Default true.
    /// </summary>
    public bool RejectCrossAxis { get; init; } = true;

    /// <summary>Capture the pointer once the drag starts (not before, so buttons under the press still click).</summary>
    public bool CapturePointer { get; init; } = true;

    /// <summary>Also start on presses already handled by a child (for example a grabber button).</summary>
    public bool HandledEventsToo { get; init; }

    /// <summary>Accept mouse presses only with the left button (default true).</summary>
    public bool LeftButtonOnly { get; init; } = true;

    /// <summary>Velocity estimation window (default 100ms).</summary>
    public TimeSpan VelocityWindow { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>A release later than this after the last move reports zero velocity (default 60ms).</summary>
    public TimeSpan VelocityStaleAfter { get; init; } = TimeSpan.FromMilliseconds(60);
}

/// <summary>
/// Platform-free drag state machine (S10): threshold, axis lock, cross-axis rejection and velocity. Feed it positions
/// and times; <see cref="DragTracker"/> wires it to Avalonia pointer events. Useful on its own in custom pointer code
/// and in tests.
/// </summary>
public sealed class DragRecognizer
{
    private readonly VelocityTracker _velocity;

    public DragRecognizer(DragOptions? options = null)
    {
        Options = options ?? new DragOptions();
        _velocity = new VelocityTracker(Options.VelocityWindow, Options.VelocityStaleAfter);
    }

    public DragOptions Options { get; }

    public DragPhase Phase { get; private set; }

    /// <summary>Press position.</summary>
    public Point Origin { get; private set; }

    /// <summary>Latest position.</summary>
    public Point Position { get; private set; }

    /// <summary>Offset from <see cref="Origin"/>, masked to the locked axis.</summary>
    public Vector Offset => Mask(Position - Origin);

    /// <summary>Change since the previous reported move, masked to the locked axis.</summary>
    public Vector Delta { get; private set; }

    /// <summary>Time of the latest sample.</summary>
    public TimeSpan Time { get; private set; }

    /// <summary>Current velocity estimate (px/s), masked to the locked axis.</summary>
    public Vector Velocity => Mask(_velocity.Velocity);

    /// <summary>Starts tracking a press.</summary>
    public void Begin(Point position, TimeSpan time)
    {
        _velocity.Reset();
        Origin = position;
        Position = position;
        Delta = default;
        Time = time;
        Phase = DragPhase.Pending;
        _velocity.AddSample(position, time);
    }

    /// <summary>
    /// Reports a move. Returns <see cref="DragTransition.Started"/> when the threshold is crossed along an accepted axis
    /// (the caller may still refuse it with <see cref="Reject"/>), <see cref="DragTransition.Moved"/> while dragging, or
    /// <see cref="DragTransition.Rejected"/> when the movement went along the locked-out axis.
    /// </summary>
    public DragTransition Move(Point position, TimeSpan time)
    {
        if (Phase is DragPhase.Idle or DragPhase.Rejected)
        {
            return DragTransition.None;
        }
        var previous = Position;
        Position = position;
        Time = time;
        _velocity.AddSample(position, time);
        if (Phase == DragPhase.Pending)
        {
            var raw = position - Origin;
            if (Math.Sqrt(raw.X * raw.X + raw.Y * raw.Y) < Options.Threshold)
            {
                return DragTransition.None;
            }
            if (Options.RejectCrossAxis && Options.Axis != DragAxis.Both)
            {
                var primary = Options.Axis == DragAxis.Horizontal ? Math.Abs(raw.X) : Math.Abs(raw.Y);
                var cross = Options.Axis == DragAxis.Horizontal ? Math.Abs(raw.Y) : Math.Abs(raw.X);
                if (primary < cross)
                {
                    Phase = DragPhase.Rejected;
                    return DragTransition.Rejected;
                }
            }
            Phase = DragPhase.Dragging;
            Delta = Offset;
            return DragTransition.Started;
        }
        Delta = Mask(position - previous);
        return DragTransition.Moved;
    }

    /// <summary>Marks the gesture as rejected (for example a start refused by the consumer).</summary>
    public void Reject() => Phase = Phase == DragPhase.Idle ? DragPhase.Idle : DragPhase.Rejected;

    /// <summary>
    /// Ends the gesture at <paramref name="time"/>. Returns true when a drag was in progress (completed), false when it
    /// was only a press or a rejected gesture.
    /// </summary>
    public bool End(Point position, TimeSpan time)
    {
        var wasDragging = Phase == DragPhase.Dragging;
        if (wasDragging && position != Position)
        {
            Delta = Mask(position - Position);
            Position = position;
            _velocity.AddSample(position, time);
        }
        Time = time;
        ReleaseVelocity = wasDragging ? Mask(_velocity.GetVelocity(time)) : default;
        Phase = DragPhase.Idle;
        return wasDragging;
    }

    /// <summary>Velocity at the last <see cref="End"/> (zero after a pause before release).</summary>
    public Vector ReleaseVelocity { get; private set; }

    /// <summary>Abandons the gesture.</summary>
    public void Cancel()
    {
        Phase = DragPhase.Idle;
        ReleaseVelocity = default;
    }

    private Vector Mask(Vector v) => Options.Axis switch
    {
        DragAxis.Horizontal => new Vector(v.X, 0),
        DragAxis.Vertical => new Vector(0, v.Y),
        _ => v
    };
}
