using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Gestures;

namespace TKCode.Arc.Mobile;

public partial class Sheet
{
    private void AttachGestures()
    {
        if (_handle is not null)
        {
            _drag = new DragTracker(_handle, new DragOptions { Axis = DragAxis.Vertical, Threshold = 3, RejectCrossAxis = false })
            {
                CanBegin = _ => IsOpen,
            };
            _drag.Started += (_, _) => BeginDrag(0);
            _drag.Moved += (_, args) => MoveDrag(args.Offset.Y);
            _drag.Completed += (_, args) => EndDrag(args.Velocity.Y);
            _drag.Cancelled += (_, _) => EndDrag(0);
        }
        if (_body is { } body)
        {
            // A finger on the content moves the sheet until it is at its tallest detent; there the content scrolls, and a
            // pull down from its top moves the sheet again.
            _bodyDrag = new DragTracker(body, new DragOptions { Axis = DragAxis.Vertical, Threshold = 4, HandledEventsToo = true })
            {
                CanBegin = args => IsOpen && args.Pointer.Type == PointerType.Touch,
            };
            _bodyDrag.Starting += (_, args) =>
            {
                if (DetentIndex == Top)
                {
                    var scrollable = body.Extent.Height > body.Viewport.Height + 1;
                    args.Cancel = scrollable && !(body.Offset.Y <= 0 && args.Offset.Y > 0);
                }
            };
            _bodyDrag.Started += (_, args) => BeginDrag(args.Offset.Y);
            _bodyDrag.Moved += (_, args) => MoveDrag(args.Offset.Y);
            _bodyDrag.Completed += (_, args) => EndDrag(args.Velocity.Y);
            _bodyDrag.Cancelled += (_, _) => EndDrag(0);
            body.AddHandler(PointerWheelChangedEvent, OnBodyWheel, RoutingStrategies.Tunnel);
        }
    }

    private void DetachGestures()
    {
        _drag?.Dispose();
        _drag = null;
        _bodyDrag?.Dispose();
        _bodyDrag = null;
        _body?.RemoveHandler(PointerWheelChangedEvent, OnBodyWheel);
    }

    private void OnBodyWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DetentIndex == Top || !_hasDetents)
        {
            return;
        }
        // Below the tallest detent the content does not scroll; scrolling down raises the sheet the rest of the way.
        e.Handled = true;
        if (e.Delta.Y < 0 && !_dragging)
        {
            Rest(Top);
        }
    }

    private void BeginDrag(double offsetAtStart)
    {
        if (_y is null)
        {
            return;
        }
        // Catch the panel where it is, even in flight; a stretched panel converts back to finger distance.
        _y.Stop();
        var current = _y.Value;
        var low = OffsetOf(0);
        double origin;
        if (current < 0)
        {
            origin = -RubberBand.Invert(-current, Stretch);
        }
        else if (!IsDismissible && current > low)
        {
            origin = low + RubberBand.Invert(current - low, Stretch);
        }
        else
        {
            origin = current;
        }
        _dragOrigin = origin - offsetAtStart;
        _dragFrom = DetentIndex;
        _dragging = true;
    }

    private void MoveDrag(double offset)
    {
        if (!_dragging || _y is null || Reduced)
        {
            return;
        }
        var raw = _dragOrigin + offset;
        var low = OffsetOf(0);
        // Between the detents (and down, when it may close) the panel follows the finger 1:1; past the tallest detent, and
        // past the lowest when it may not close, it resists like a rubber band.
        if (raw < 0)
        {
            _y.Jump(-RubberBand.Apply(-raw, Stretch));
        }
        else if (!IsDismissible && raw > low)
        {
            _y.Jump(low + RubberBand.Apply(raw - low, Stretch));
        }
        else
        {
            _y.Jump(raw);
        }
    }

    private void EndDrag(double velocity)
    {
        if (!_dragging || _y is null)
        {
            return;
        }
        _dragging = false;
        var current = _y.Value;
        var low = OffsetOf(0);
        // A stretched panel springs back without the finger's speed, so it never launches past the stretch.
        if (current < 0 || (!IsDismissible && current > low))
        {
            velocity = 0;
        }
        var count = _stops.Length;
        Span<double> points = stackalloc double[count + 1];
        for (var i = 0; i < count; i++)
        {
            points[i] = OffsetOf(i);
        }
        points[count] = ClosedOffset;
        var from = Math.Clamp(_dragFrom, 0, Top);
        var chosen = MomentumProjector.SelectSnapPoint(
            IsDismissible ? points : points[..count], current, velocity, SnapProjection.Linear(Projection, from, FlickVelocity));
        if (chosen == count)
        {
            // The exit starts in this frame with the finger's velocity; IsOpen = false then only waits for it to land.
            IsOpen = false;
            return;
        }
        Rest(chosen, velocity);
    }
}
