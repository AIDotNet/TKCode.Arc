using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;

namespace TKCode.Arc.Mobile;

public partial class SwipeableRow
{
    private void AttachGestures()
    {
        if (_root is null)
        {
            return;
        }
        _drag = new DragTracker(_root, new DragOptions { Axis = DragAxis.Horizontal, Threshold = 6, HandledEventsToo = true })
        {
            CanBegin = _ => IsEffectivelyEnabled && (LeadingWidth > 0 || TrailingWidth > 0 || OpenSide != SwipeRowSide.None),
        };
        _drag.Starting += OnDragStarting;
        _drag.Started += (_, args) => BeginDrag(args.Offset.X);
        _drag.Moved += (_, args) => MoveDrag(args.Offset.X);
        _drag.Completed += (_, args) => EndDrag(args.Velocity.X, false);
        _drag.Cancelled += (_, _) => EndDrag(0, true);
    }

    private void DetachGestures()
    {
        _drag?.Dispose();
        _drag = null;
    }

    private void OnContentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Tapping the slid-aside content closes the tray instead of invoking the wrapped ListItem underneath.
        if (OpenSide != SwipeRowSide.None && !_dragging)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnDragStarting(object? sender, DragGestureEventArgs e)
    {
        // Yield rightward drags from the left edge of a NavigationStack that can pop, so edge-back keeps working.
        if (e.Offset.X > 0 && OpenSide == SwipeRowSide.None
            && this.FindAncestorOfType<NavigationStack>() is { IsSwipeBackEnabled: true, CanGoBack: true } stack)
        {
            var startInStack = _root?.TranslatePoint(e.Start, stack);
            if (startInStack is { X: <= NavigationStack.EdgeWidth })
            {
                e.Cancel = true;
                return;
            }
        }
        if (OpenSide == SwipeRowSide.None)
        {
            if (e.Offset.X > 0 && LeadingWidth <= 0)
            {
                e.Cancel = true;
            }
            else if (e.Offset.X < 0 && TrailingWidth <= 0)
            {
                e.Cancel = true;
            }
        }
    }

    private void BeginDrag(double offsetAtStart)
    {
        if (_x is null)
        {
            return;
        }
        CloseSiblingRows();
        _x.Stop();
        _dragOrigin = _x.Value - offsetAtStart;
        _fullSwipeArmed = false;
        _dragging = true;
    }

    private double FullSwipeDistance(double trayWidth)
    {
        var width = Bounds.Width > 0 ? Bounds.Width * FullSwipeFraction : trayWidth + FullSwipeExtra;
        return Math.Max(trayWidth + FullSwipeExtra, width);
    }

    private void MoveDrag(double offset)
    {
        if (!_dragging || _x is null)
        {
            return;
        }
        var raw = _dragOrigin + offset;
        var leading = LeadingWidth;
        var trailing = TrailingWidth;
        double clamped;
        if (raw > 0)
        {
            if (leading <= 0)
            {
                clamped = 0;
            }
            else if (raw <= leading)
            {
                clamped = raw;
            }
            else
            {
                clamped = leading + RubberBand.Apply(raw - leading, Stretch);
            }
        }
        else if (raw < 0)
        {
            if (trailing <= 0)
            {
                clamped = 0;
            }
            else if (-raw <= trailing)
            {
                clamped = raw;
            }
            else
            {
                clamped = -(trailing + RubberBand.Apply(-raw - trailing, Stretch));
            }
        }
        else
        {
            clamped = 0;
        }

        if (AllowFullSwipe)
        {
            var armed = (raw > 0 && leading > 0 && FirstEnabledAction(LeadingActions) is not null && raw >= FullSwipeDistance(leading))
                        || (raw < 0 && trailing > 0 && FirstEnabledAction(TrailingActions) is not null && -raw >= FullSwipeDistance(trailing));
            if (armed && !_fullSwipeArmed)
            {
                Haptics.Trigger(HapticKind.Medium);
            }
            _fullSwipeArmed = armed;
        }

        if (!Reduced)
        {
            _x.Jump(clamped);
        }
    }

    private void EndDrag(double velocity, bool cancelled)
    {
        if (!_dragging || _x is null)
        {
            return;
        }
        _dragging = false;
        var current = _x.Value;
        var leading = LeadingWidth;
        var trailing = TrailingWidth;

        if (!cancelled && AllowFullSwipe && _fullSwipeArmed)
        {
            _fullSwipeArmed = false;
            if (current > 0 && FirstEnabledAction(LeadingActions) is { } leadingAction)
            {
                InvokeAction(leadingAction, SwipeRowSide.Leading);
                if (!leadingAction.ClosesOnInvoke)
                {
                    SettleTo(SwipeRowSide.Leading, 0);
                }
                return;
            }
            if (current < 0 && FirstEnabledAction(TrailingActions) is { } trailingAction)
            {
                InvokeAction(trailingAction, SwipeRowSide.Trailing);
                if (!trailingAction.ClosesOnInvoke)
                {
                    SettleTo(SwipeRowSide.Trailing, 0);
                }
                return;
            }
        }
        _fullSwipeArmed = false;

        if (cancelled)
        {
            SettleTo(OpenSide, 0);
            return;
        }

        if (current > 0 && leading > 0)
        {
            Span<double> points = [0, leading];
            var from = OpenSide == SwipeRowSide.Leading ? 1 : 0;
            var chosen = MomentumProjector.SelectSnapPoint(
                points, current, velocity, SnapProjection.Linear(Projection, from, FlickVelocity));
            SettleTo(chosen == 1 ? SwipeRowSide.Leading : SwipeRowSide.None, velocity);
            return;
        }
        if (current < 0 && trailing > 0)
        {
            Span<double> points = [-trailing, 0];
            var from = OpenSide == SwipeRowSide.Trailing ? 0 : 1;
            var chosen = MomentumProjector.SelectSnapPoint(
                points, current, velocity, SnapProjection.Linear(Projection, from, FlickVelocity));
            SettleTo(chosen == 0 ? SwipeRowSide.Trailing : SwipeRowSide.None, velocity);
            return;
        }
        SettleTo(SwipeRowSide.None, 0);
    }
}
