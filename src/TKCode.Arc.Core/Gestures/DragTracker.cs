using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gestures;

/// <summary>Event data of a <see cref="DragTracker"/> gesture. Positions are in the target's TopLevel coordinates, so a
/// target that moves with the drag (a panel following the finger) never feeds back into its own offsets.</summary>
public sealed class DragGestureEventArgs : CancelEventArgs
{
    internal DragGestureEventArgs(Point start, Point position, Vector offset, Vector delta, Vector velocity,
                                  PointerType pointerType, KeyModifiers modifiers)
    {
        Start = start;
        Position = position;
        Offset = offset;
        Delta = delta;
        Velocity = velocity;
        PointerType = pointerType;
        KeyModifiers = modifiers;
    }

    /// <summary>Press position.</summary>
    public Point Start { get; }

    /// <summary>Current position.</summary>
    public Point Position { get; }

    /// <summary>Offset from the press, masked to the locked axis.</summary>
    public Vector Offset { get; }

    /// <summary>Change since the previous event, masked to the locked axis.</summary>
    public Vector Delta { get; }

    /// <summary>Velocity in px/s (on <see cref="DragTracker.Completed"/>: the release velocity, zero after a pause).</summary>
    public Vector Velocity { get; }

    public PointerType PointerType { get; }

    public KeyModifiers KeyModifiers { get; }
}

/// <summary>
/// Pointer drag gesture on a control (S10): threshold before start, optional axis lock with cross-axis rejection
/// (vertical scrolling passes through a horizontal drag), pointer capture once started, velocity on release.
/// Mouse, pen and touch are handled alike. Losing capture cancels the gesture (spring back).
/// <code>
/// var drag = new DragTracker(header, new DragOptions { Axis = DragAxis.Vertical });
/// drag.Moved += (_, e) =&gt; offset.Jump(start + e.Offset.Y);
/// drag.Completed += (_, e) =&gt; MomentumProjector.SettleTo(offset, target, e.Velocity.Y, ArcSpring.Smooth);
/// </code>
/// </summary>
public sealed class DragTracker : IDisposable
{
    private readonly Control _target;
    private readonly DragRecognizer _recognizer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private IPointer? _pointer;
    private Visual? _root;
    private bool _useEventTime;
    private ulong _eventTimeOrigin;
    private ulong _lastEventStamp;
    private bool _disposed;

    public DragTracker(Control target, DragOptions? options = null)
    {
        _target = target;
        _recognizer = new DragRecognizer(options);
        var handledToo = _recognizer.Options.HandledEventsToo;
        target.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Bubble, handledToo);
        target.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Bubble, true);
        target.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, true);
        target.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Direct | RoutingStrategies.Bubble, true);
    }

    /// <summary>The control the gesture is recognized on.</summary>
    public Control Target => _target;

    public DragOptions Options => _recognizer.Options;

    /// <summary>When false, presses are ignored (for example under reduced motion).</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Optional filter evaluated on press; return false to ignore the press (for example presses on buttons).</summary>
    public Func<PointerPressedEventArgs, bool>? CanBegin { get; set; }

    /// <summary>True while a drag is in progress (after the threshold).</summary>
    public bool IsDragging => _recognizer.Phase == DragPhase.Dragging;

    /// <summary>A press was accepted (before the threshold).</summary>
    public event EventHandler<DragGestureEventArgs>? Pressed;

    /// <summary>The threshold was crossed. Set <see cref="CancelEventArgs.Cancel"/> to refuse the drag (it is then ignored until release).</summary>
    public event EventHandler<DragGestureEventArgs>? Starting;

    /// <summary>The drag started (pointer captured).</summary>
    public event EventHandler<DragGestureEventArgs>? Started;

    /// <summary>The pointer moved while dragging.</summary>
    public event EventHandler<DragGestureEventArgs>? Moved;

    /// <summary>The drag ended with a release; <see cref="DragGestureEventArgs.Velocity"/> is the release velocity.</summary>
    public event EventHandler<DragGestureEventArgs>? Completed;

    /// <summary>The drag was abandoned (capture lost, <see cref="Cancel"/>, disposed). Spring back.</summary>
    public event EventHandler<DragGestureEventArgs>? Cancelled;

    /// <summary>Released without dragging (a press that stayed under the threshold or was rejected).</summary>
    public event EventHandler<DragGestureEventArgs>? Released;

    /// <summary>Cancels a drag in progress.</summary>
    public void Cancel()
    {
        if (_recognizer.Phase == DragPhase.Idle)
        {
            return;
        }
        var wasDragging = _recognizer.Phase == DragPhase.Dragging;
        var args = Args();
        _recognizer.Cancel();
        ReleaseCapture();
        if (wasDragging)
        {
            Cancelled?.Invoke(this, args);
        }
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || _disposed || _recognizer.Phase != DragPhase.Idle)
        {
            return;
        }
        var point = e.GetCurrentPoint(_target);
        if (_recognizer.Options.LeftButtonOnly && e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (CanBegin is { } canBegin && !canBegin(e))
        {
            return;
        }
        _root = TopLevel.GetTopLevel(_target);
        if (_root is null)
        {
            return;
        }
        _pointer = e.Pointer;
        _useEventTime = e.Timestamp != 0;
        _eventTimeOrigin = e.Timestamp;
        _lastEventStamp = e.Timestamp;
        _clock.Restart();
        _pointerType = e.Pointer.Type;
        _modifiers = e.KeyModifiers;
        _recognizer.Begin(e.GetPosition(_root), TimeSpan.Zero);
        Pressed?.Invoke(this, Args());
    }

    private PointerType _pointerType;
    private KeyModifiers _modifiers;

    private TimeSpan Now(PointerEventArgs e)
    {
        // Platform event times are more precise than processing time; a source that goes backwards switches to the clock.
        if (_useEventTime && e.Timestamp >= _lastEventStamp)
        {
            _lastEventStamp = e.Timestamp;
            return TimeSpan.FromMilliseconds(e.Timestamp - _eventTimeOrigin);
        }
        _useEventTime = false;
        return _clock.Elapsed;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_root is null || !ReferenceEquals(e.Pointer, _pointer) || _recognizer.Phase is DragPhase.Idle or DragPhase.Rejected)
        {
            return;
        }
        _modifiers = e.KeyModifiers;
        // A mouse whose button was released outside the window never sends a release: end the drag.
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(_target).Properties.IsLeftButtonPressed &&
            _recognizer.Options.LeftButtonOnly)
        {
            Finish(e);
            return;
        }
        var transition = _recognizer.Move(e.GetPosition(_root), Now(e));
        switch (transition)
        {
            case DragTransition.Started:
            {
                var starting = Args();
                Starting?.Invoke(this, starting);
                if (starting.Cancel)
                {
                    _recognizer.Reject();
                    return;
                }
                if (_recognizer.Options.CapturePointer)
                {
                    e.Pointer.Capture(_target);
                }
                e.PreventGestureRecognition();
                Started?.Invoke(this, Args());
                Moved?.Invoke(this, Args());
                e.Handled = true;
                break;
            }
            case DragTransition.Moved:
                Moved?.Invoke(this, Args());
                e.Handled = true;
                break;
        }
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer) || _recognizer.Phase == DragPhase.Idle)
        {
            return;
        }
        if (_recognizer.Phase == DragPhase.Dragging)
        {
            e.Handled = true;
        }
        Finish(e);
    }

    private void Finish(PointerEventArgs e)
    {
        var position = _root is { } root ? e.GetPosition(root) : _recognizer.Position;
        var completed = _recognizer.End(position, Now(e));
        var args = new DragGestureEventArgs(_recognizer.Origin, _recognizer.Position, _recognizer.Offset, _recognizer.Delta,
            _recognizer.ReleaseVelocity, _pointerType, _modifiers);
        ReleaseCapture();
        if (completed)
        {
            Completed?.Invoke(this, args);
        }
        else
        {
            Released?.Invoke(this, args);
        }
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer) || _recognizer.Phase != DragPhase.Dragging)
        {
            return;
        }
        var args = Args();
        _recognizer.Cancel();
        _pointer = null;
        Cancelled?.Invoke(this, args);
    }

    private void ReleaseCapture()
    {
        var pointer = _pointer;
        _pointer = null;
        if (pointer is not null && ReferenceEquals(pointer.Captured, _target))
        {
            pointer.Capture(null);
        }
    }

    private DragGestureEventArgs Args() => new(_recognizer.Origin, _recognizer.Position, _recognizer.Offset, _recognizer.Delta,
        _recognizer.Velocity, _pointerType, _modifiers);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Cancel();
        _disposed = true;
        _target.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        _target.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        _target.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        _target.RemoveHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
    }
}
