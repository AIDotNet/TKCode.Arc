using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What a press may start dragging: one pane (its tab) or several (a group, a floating window).</summary>
internal sealed class DockDragRequest
{
    public DockDragRequest(IReadOnlyList<DockPane> panes, DockPane lead, DockGroup sourceGroup, bool isGroupDrag)
    {
        Panes = panes;
        Lead = lead;
        SourceGroup = sourceGroup;
        IsGroupDrag = isGroupDrag;
    }

    /// <summary>The panes that move, in tab order.</summary>
    public IReadOnlyList<DockPane> Panes { get; }

    /// <summary>The pane shown on the ghost and selected after the drop.</summary>
    public DockPane Lead { get; }

    /// <summary>The group the drag starts in.</summary>
    public DockGroup SourceGroup { get; }

    /// <summary>True when the whole group (or floating window) moves.</summary>
    public bool IsGroupDrag { get; }

    /// <summary>The tab that was pressed (a tab drag starts by reordering it in its strip).</summary>
    public DockTab? Tab { get; init; }

    /// <summary>The floating host the drag moves along with the pointer (its only group, or its title bar), or null.</summary>
    public IDockFloatHost? MovingHost { get; init; }
}

/// <summary>
/// Turns presses on a tab strip, or on a floating host's title bar, into dock drags (spec 9.1): a left press that moves
/// more than 4px starts the drag (touch: held 400ms first, so a quick swipe still scrolls), the gesture then captures the
/// pointer and hands every move, the release and a lost capture to the layout's drag. Listens to handled events too: the
/// tab handles its own press (selection, focus) before the strip sees it.
/// </summary>
internal sealed class DockDragGesture : IDisposable
{
    /// <summary>Movement before a press becomes a drag (spec 9.1).</summary>
    public const double Threshold = 4;

    /// <summary>How long a touch must be held before it drags (spec 3, self-designed).</summary>
    public static readonly TimeSpan TouchHold = TimeSpan.FromMilliseconds(400);

    private readonly Control _target;
    private readonly DockLayout _layout;
    private readonly Func<PointerPressedEventArgs, DockDragRequest?> _resolve;
    private DockDragRequest? _pending;
    private IPointer? _pointer;
    private Point _start;
    private DateTime _pressedAt;
    private bool _dragging;

    public DockDragGesture(Control target, DockLayout layout, Func<PointerPressedEventArgs, DockDragRequest?> resolve)
    {
        _target = target;
        _layout = layout;
        _resolve = resolve;
        target.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        target.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        target.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        target.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Bubble | RoutingStrategies.Direct,
            handledEventsToo: true);
    }

    public Control Target => _target;

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_dragging)
        {
            return;
        }
        _pending = null;
        var point = e.GetCurrentPoint(_target);
        if (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (TopLevel.GetTopLevel(_target) is not { } top)
        {
            return;
        }
        _pending = _resolve(e);
        if (_pending is null)
        {
            return;
        }
        _pointer = e.Pointer;
        _start = e.GetPosition(top);
        _pressedAt = DateTime.UtcNow;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        if (_dragging)
        {
            if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(_target).Properties.IsLeftButtonPressed)
            {
                // The button went up outside every window: no release will come.
                _dragging = false;
                _pointer = null;
                _layout.EndDrag(e, drop: true);
                return;
            }
            _layout.MoveDrag(e);
            e.Handled = true;
            return;
        }
        if (_pending is null || TopLevel.GetTopLevel(_target) is not { } top)
        {
            return;
        }
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(_target).Properties.IsLeftButtonPressed)
        {
            _pending = null;
            return;
        }
        var position = e.GetPosition(top);
        var moved = position - _start;
        if (Math.Abs(moved.X) < Threshold && Math.Abs(moved.Y) < Threshold)
        {
            return;
        }
        if (e.Pointer.Type == PointerType.Touch && DateTime.UtcNow - _pressedAt < TouchHold)
        {
            // A quick touch swipe scrolls instead of dragging.
            _pending = null;
            return;
        }
        var request = _pending;
        _pending = null;
        if (!_layout.BeginDrag(request, this, e, _start))
        {
            return;
        }
        _dragging = true;
        e.Pointer.Capture(_target);
        e.PreventGestureRecognition();
        e.Handled = true;
        _layout.MoveDrag(e);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        _pending = null;
        _pointer = null;
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        e.Handled = true;
        e.Pointer.Capture(null);
        _layout.EndDrag(e, drop: true);
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_dragging || !ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        // The drop itself can take the strip out of the tree (its group empties); the layout ignores a drag that has ended.
        _dragging = false;
        _pointer = null;
        _layout.EndDrag(null, drop: false);
    }

    /// <summary>Forgets a drag the layout ended on its own (Escape): the release that follows is ignored.</summary>
    public void Abandon()
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        var pointer = _pointer;
        _pointer = null;
        if (pointer is not null && ReferenceEquals(pointer.Captured, _target))
        {
            pointer.Capture(null);
        }
    }

    public void Dispose()
    {
        _target.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        _target.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        _target.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        _target.RemoveHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
    }
}
