using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

// Moves, autoplay, the drag and the rubber band. One frame loop drives them all: every word has its own position,
// opacity and typed length at every moment of a move, which a ControlTheme cannot express, and the move must be a
// function of its progress so a drag can scrub it and a new step can rebase it.
public partial class CodeMagicMove
{
    /// <summary>A drag commits past this share of the move.</summary>
    internal const double CommitShare = 1.0 / 3;

    /// <summary>A release faster than this (px/s) toward the next step commits too.</summary>
    internal const double FlickSpeed = 500;

    /// <summary>A full move takes this share of the code area's width of drag.</summary>
    internal const double DragSpan = 0.6;

    /// <summary>Rewinding a drag that did not commit runs this much faster than the move.</summary>
    internal const double RewindRate = 1.5;

    /// <summary>The rubber band gives this many pixels of drag before it starts to pull.</summary>
    internal const double RubberDeadZone = 20;

    /// <summary>Half the rubber band's limit is reached this far past the dead zone (px).</summary>
    internal const double RubberHalf = 84;

    private static readonly SpringSpec RubberSpring = SpringSpec.FromPhysical(625, 50);

    private readonly LiveLoop _loop;
    private DragTracker? _drag;
    private double _time;
    private double _direction = 1;
    private double _rate = 1;
    private int _planFrom = -1;
    private int _planTo = -1;
    private double _rest;
    private bool _hovered;
    private bool _announceNext;
    private bool _committingDrag;
    private bool _dragging;
    private int _dragTarget = -1;
    private double _rubber;
    private double _rubberFrom;
    private double _rubberClock = -1;

    /// <summary>The move under way, or null at rest.</summary>
    internal CodeMagicMovePlan? Plan { get; private set; }

    /// <summary>The move's clock (ms at speed 1).</summary>
    internal double MoveTime => _time;

    /// <summary>How long autoplay has rested on the current step (ms).</summary>
    internal double RestTime => _rest;

    /// <summary>The rubber band's pull (px).</summary>
    internal double RubberOffset => _rubber;

    private bool CanAnimate => ArcMotion.IsFull(this) && VisualRoot is not null;

    private void OnValueChanged(int oldValue, int newValue)
    {
        var announce = _announceNext || _committingDrag;
        if (_committingDrag)
        {
            // The drag already built the move; it now runs on from where the finger let go.
            _direction = 1;
            _rate = 1;
        }
        else
        {
            StartMove(oldValue, newValue);
        }
        _rest = 0;
        UpdateCurrent(announce);
        UpdateLoop();
    }

    private void StartMove(int from, int to)
    {
        EnsureSteps();
        if (to < 0 || to >= _sources.Length || !CanAnimate || _surface is null)
        {
            Settle();
            return;
        }
        if (Plan is { } plan)
        {
            if (_planTo == to)
            {
                _direction = 1;
                _rate = 1;
                return;
            }
            if (_planFrom == to)
            {
                // Back where it came from: the same timeline runs in reverse.
                _direction = -1;
                _rate = 1;
                return;
            }
            // Somewhere else: a new move from where every word is now.
            plan.Evaluate(_time);
            Begin(new CodeMagicMovePlan(plan.Snapshot(), _sources[to], _bands[to]), -1, to);
            return;
        }
        if (from < 0 || from >= _sources.Length || from == to)
        {
            Settle();
            return;
        }
        Begin(new CodeMagicMovePlan(new CodeMagicMoveScene(_sources[from], _bands[from]), _sources[to], _bands[to]), from, to);
    }

    private void Begin(CodeMagicMovePlan plan, int from, int to)
    {
        Plan = plan;
        _planFrom = from;
        _planTo = to;
        _time = 0;
        _direction = 1;
        _rate = 1;
        IsMoving = true;
        Invalidate();
    }

    /// <summary>Ends any move at once: the walkthrough shows the rest state of <see cref="Value"/>.</summary>
    private void Settle()
    {
        Plan = null;
        _planFrom = _planTo = -1;
        _time = 0;
        IsMoving = _dragging;
        Invalidate();
    }

    private void CancelMotion()
    {
        _dragging = false;
        _dragTarget = -1;
        _drag?.Cancel();
        _rubber = 0;
        _rubberClock = -1;
        PseudoClasses.Set(DraggingPseudoClass, false);
        Settle();
    }

    private void Invalidate()
    {
        if (_surface is null)
        {
            return;
        }
        _surface.RubberOffset = _rubber;
        _surface.InvalidateMeasure();
        _surface.InvalidateVisual();
    }

    private void UpdateLoop()
    {
        IsPlaying = AutoPlay && !IsPaused && Steps.Count > 1 && ArcMotion.IsFull(this);
        UpdateFill();
        if (Plan is not null || IsPlaying || _rubberClock >= 0)
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    private void UpdateFill()
    {
        if (_dots is null)
        {
            return;
        }
        var interval = Interval.TotalMilliseconds;
        var fill = !IsPlaying ? 1 : Plan is not null || _dragging || interval <= 0 ? 0 : Math.Clamp(_rest / interval, 0, 1);
        var index = Value;
        if (index >= 0 && index < _dots.Children.Count && _dots.Children[index] is CodeMagicMoveDot dot)
        {
            dot.Fill = fill;
        }
    }

    /// <summary>Advances everything by <paramref name="seconds"/> (the frame loop; tests call it directly).</summary>
    internal bool Step(double seconds)
    {
        var ms = seconds * 1000;
        var changed = false;
        // Autoplay rests only from the frame after a move lands.
        var wasMoving = Plan is not null || _dragging;
        if (_rubberClock >= 0)
        {
            _rubberClock += seconds;
            var (displacement, velocity) = SpringSolver.Evaluate(RubberSpring, _rubberFrom, 0, _rubberClock);
            _rubber = displacement;
            if (Math.Abs(displacement) < 0.05 && Math.Abs(velocity) < 0.5)
            {
                _rubber = 0;
                _rubberClock = -1;
            }
            changed = true;
        }
        if (Plan is { } plan && !_dragging)
        {
            _time += ms * Speed * _rate * _direction;
            if (_direction > 0 && _time >= plan.Duration)
            {
                Settle();
            }
            else if (_direction < 0 && _time <= 0)
            {
                Settle();
            }
            else
            {
                plan.Evaluate(_time);
            }
            changed = true;
        }
        IsPlaying = AutoPlay && !IsPaused && Steps.Count > 1 && ArcMotion.IsFull(this);
        if (IsPlaying && !wasMoving && Plan is null && !_dragging && !_hovered)
        {
            _rest += ms;
            if (_rest >= Interval.TotalMilliseconds)
            {
                _rest = 0;
                Value = (Value + 1) % Steps.Count;
            }
        }
        UpdateFill();
        if (changed)
        {
            Invalidate();
        }
        return Plan is not null || IsPlaying || _rubberClock >= 0 || _dragging;
    }

    // --- drag ----------------------------------------------------------------------------------------------------------

    private void AttachDrag(CodeMagicMoveSurface surface)
    {
        _drag = new DragTracker(surface, new DragOptions { Axis = DragAxis.Horizontal });
        _drag.Started += OnDragStarted;
        _drag.Moved += OnDragMoved;
        _drag.Completed += OnDragCompleted;
        _drag.Cancelled += OnDragCancelled;
    }

    private void DetachDrag()
    {
        if (_drag is null)
        {
            return;
        }
        _drag.Started -= OnDragStarted;
        _drag.Moved -= OnDragMoved;
        _drag.Completed -= OnDragCompleted;
        _drag.Cancelled -= OnDragCancelled;
        _drag.Dispose();
        _drag = null;
    }

    private void OnDragStarted(object? sender, DragGestureEventArgs e) => BeginDrag();

    private void OnDragMoved(object? sender, DragGestureEventArgs e) => DragTo(e.Offset.X);

    private void OnDragCompleted(object? sender, DragGestureEventArgs e) => EndDrag(e.Offset.X, e.Velocity.X);

    private void OnDragCancelled(object? sender, DragGestureEventArgs e) => EndDrag(0, 0);

    /// <summary>A sideways drag starts: a move under way lands first, and autoplay stops.</summary>
    internal void BeginDrag()
    {
        if (Steps.Count == 0)
        {
            return;
        }
        Settle();
        IsPaused = true;
        _dragging = true;
        _dragTarget = -1;
        _rubberClock = -1;
        IsMoving = true;
        PseudoClasses.Set(DraggingPseudoClass, true);
        UpdateLoop();
    }

    /// <summary>The finger is <paramref name="offset"/> px from where the drag started (left is toward the next step).</summary>
    internal void DragTo(double offset)
    {
        if (!_dragging)
        {
            return;
        }
        var target = offset < 0 ? Value + 1 : offset > 0 ? Value - 1 : -1;
        if (target < 0 || target >= Steps.Count || !CanAnimate)
        {
            if (Plan is not null)
            {
                Settle();
                _dragTarget = -1;
            }
            // Past the first or last step the code gives a little and pulls back.
            var d = Math.Max(0, Math.Abs(offset) - RubberDeadZone);
            var limit = _surface?.RubberBandLimit ?? 40;
            _rubber = target == -1 && offset == 0 ? 0 : Math.Sign(offset) * limit * d / (d + RubberHalf);
            Invalidate();
            return;
        }
        _rubber = 0;
        if (_dragTarget != target || Plan is null)
        {
            EnsureSteps();
            Begin(new CodeMagicMovePlan(new CodeMagicMoveScene(_sources[Value], _bands[Value]), _sources[target], _bands[target]), Value, target);
            _dragTarget = target;
        }
        _time = DragShare(offset) * Plan!.Duration;
        Plan.Evaluate(_time);
        Invalidate();
    }

    /// <summary>The share of the move a drag of <paramref name="offset"/> px covers.</summary>
    internal double DragShare(double offset)
    {
        var width = _surface?.Bounds.Width ?? 0;
        var span = Math.Max(1, (width > 0 ? width : 600) * DragSpan);
        return Math.Clamp(Math.Abs(offset) / span, 0, 1);
    }

    /// <summary>The finger lets go: past a third or on a flick the move commits, otherwise it rewinds.</summary>
    internal void EndDrag(double offset, double velocity)
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        PseudoClasses.Set(DraggingPseudoClass, false);
        if (_rubber != 0)
        {
            _rubberFrom = _rubber;
            _rubberClock = 0;
        }
        var target = _dragTarget;
        _dragTarget = -1;
        if (Plan is null || target < 0)
        {
            IsMoving = false;
            UpdateLoop();
            return;
        }
        var toward = target > Value ? -1 : 1;
        var flick = Math.Sign(velocity) == toward && Math.Abs(velocity) >= FlickSpeed;
        if (DragShare(offset) > CommitShare || flick)
        {
            _committingDrag = true;
            try
            {
                Value = target;
            }
            finally
            {
                _committingDrag = false;
            }
        }
        else
        {
            _direction = -1;
            _rate = RewindRate;
        }
        UpdateLoop();
    }

}
