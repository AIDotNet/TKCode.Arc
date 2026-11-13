using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What a press on the grid does once it becomes a drag.</summary>
internal enum WeekCalendarGesture
{
    /// <summary>No press, or one that does nothing (a press on a block that cannot move).</summary>
    None,

    /// <summary>Pressed, not yet past the drag threshold (or, for touch, before the long press).</summary>
    Pending,

    /// <summary>Drawing a new event on empty time.</summary>
    Draw,

    /// <summary>Moving an event between times and days.</summary>
    Move,

    /// <summary>Dragging an event's bottom edge.</summary>
    Resize,

    /// <summary>A sideways swipe between days (narrow calendars, touch).</summary>
    Swipe,

    /// <summary>Escape cancelled the drag; the rest of the press does nothing.</summary>
    Canceled,
}

/// <summary>
/// Pointer editing of the grid (notes B §4): drawing on empty time, moving and resizing events (4px threshold, then the
/// pointer is captured), the grid's auto-scroll near its top and bottom while dragging, the long press that starts both on
/// touch screens, and the sideways swipe between days of narrow calendars. The keys that edit (N, Enter, Delete,
/// Alt+arrows, Escape) come through <see cref="OnEditingKey"/>.
/// </summary>
internal sealed partial class WeekCalendarGridView
{
    private IPointer? _pointer;
    private WeekCalendarGesture _gesture;
    private WeekCalendarGesture _candidate;
    private WeekCalendarGridPane? _gesturePane;
    private WeekCalendarEventPresenter? _gesturePresenter;
    private Point _pressPoint;
    private Point _lastPoint;
    private double _pressMinute;
    private DateOnly _pressDay;
    private bool _isTouch;
    private readonly WeekCalendarHold _hold = new();
    private DispatcherTimer? _holdTimer;
    private TimeSpan _pressTime;
    private WeekCalendarEvent? _dragEvent;
    private DateOnly _dragDay;
    private double _dragStart;
    private double _dragEnd;
    private (int Start, int End) _drawn;
    private LiveLoop? _autoScroll;
    private double _autoScrolled;
    private readonly VelocityTracker _swipeVelocity = new(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(60));
    private WeekCalendarPaneSlot? _swipePrevious;
    private WeekCalendarPaneSlot? _swipeNext;
    private WeekCalendarPaneSlot? _swipeIncoming;
    private double _swipeTravel;
    private static Cursor? s_dragCursor;

    /// <summary>The gesture in progress (tests).</summary>
    public WeekCalendarGesture Gesture => _gesture;

    /// <summary>The pixels the grid auto-scrolled during the current drag (tests).</summary>
    public double AutoScrolled => _autoScrolled;

    /// <summary>True while the auto-scroll loop wants frames.</summary>
    public bool IsAutoScrolling => _autoScroll?.IsActive == true;

    /// <summary>The neighbouring slots a swipe drags along, while it runs.</summary>
    public (WeekCalendarPaneSlot? Previous, WeekCalendarPaneSlot? Next) SwipeNeighbours => (_swipePrevious, _swipeNext);

    private void InitializeEditing()
    {
        _body.AddHandler(PointerPressedEvent, OnBodyPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        _body.AddHandler(PointerMovedEvent, OnBodyPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        _body.AddHandler(PointerReleasedEvent, OnBodyPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        _body.AddHandler(PointerCaptureLostEvent, OnBodyCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>
    /// The point of the current pane under <paramref name="viewPoint"/> (grid view coordinates). Computed from the scroll
    /// offset rather than the visual tree, so it is right inside an auto-scroll frame, before layout catches up.
    /// </summary>
    public Point PanePoint(Point viewPoint) =>
        new(viewPoint.X - WeekCalendarMetrics.GutterWidth, viewPoint.Y - _scroll.Bounds.Top + _scroll.Offset.Y);

    // Press, move, release -------------------------------------------------------------------------------------------------

    private void OnBodyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_owner.IsPressConsumed(e) || _gesture != WeekCalendarGesture.None || !_owner.IsEditable)
        {
            return;
        }
        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (Current?.Grid is not { } pane || e.Source is not Visual source || !ReferenceEquals(source.FindAncestorOfType<WeekCalendarGridPane>(true), pane))
        {
            return;
        }
        var panePoint = e.GetPosition(pane);
        var presenter = source.FindAncestorOfType<WeekCalendarEventPresenter>(includeSelf: true);
        var handle = pane.ResizeHandleAt(panePoint, presenter);
        if (handle is not null)
        {
            _candidate = WeekCalendarGesture.Resize;
            presenter = handle;
        }
        else if (presenter is not null)
        {
            _candidate = presenter.CanMove ? WeekCalendarGesture.Move : WeekCalendarGesture.None;
        }
        else
        {
            _candidate = WeekCalendarGesture.Draw;
        }
        if (_candidate == WeekCalendarGesture.None)
        {
            return;
        }
        _pointer = e.Pointer;
        _gesture = WeekCalendarGesture.Pending;
        _gesturePane = pane;
        _gesturePresenter = presenter;
        _pressPoint = point.Position;
        _lastPoint = point.Position;
        _pressMinute = _owner.TimeGrid.MinuteAt(panePoint.Y);
        _pressDay = presenter?.Day ?? pane.TimeAt(panePoint).Day;
        _isTouch = e.Pointer.Type == PointerType.Touch;
        _pressTime = TimeSpan.FromMilliseconds(e.Timestamp);
        _autoScrolled = 0;
        if (_isTouch)
        {
            _hold.Press(point.Position, _pressTime);
            _holdTimer ??= new DispatcherTimer(DispatcherPriority.Input) { Interval = WeekCalendarEditMetrics.LongPressDelay };
            _holdTimer.Tick -= OnHoldTick;
            _holdTimer.Tick += OnHoldTick;
            _holdTimer.Stop();
            _holdTimer.Start();
            _swipeVelocity.Reset();
            _swipeVelocity.AddSample(point.Position, _pressTime);
        }
    }

    private void OnHoldTick(object? sender, EventArgs e)
    {
        _holdTimer?.Stop();
        FireHold(_pressTime + WeekCalendarEditMetrics.LongPressDelay);
    }

    /// <summary>The long press of a touch fired: drawing starts on the pressed slot, or the pressed event lifts.</summary>
    internal bool FireHold(TimeSpan now)
    {
        if (_gesture != WeekCalendarGesture.Pending || !_isTouch || !_hold.TryFire(now))
        {
            return false;
        }
        _pointer?.Capture(_body);
        Begin(_candidate);
        return true;
    }

    private void OnBodyPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        var position = e.GetPosition(this);
        _lastPoint = position;
        switch (_gesture)
        {
            case WeekCalendarGesture.Pending when _isTouch:
                _swipeVelocity.AddSample(position, TimeSpan.FromMilliseconds(e.Timestamp));
                if (_hold.Move(position))
                {
                    return;
                }
                _holdTimer?.Stop();
                if (WeekCalendarEditMath.PanAxis(position - _pressPoint) == WeekCalendarPanAxis.Horizontal && CanSwipe())
                {
                    e.PreventGestureRecognition();
                    e.Pointer.Capture(_body);
                    BeginSwipe();
                    UpdateSwipe(position.X - _pressPoint.X);
                }
                else
                {
                    // A vertical pan: the scroller takes the touch.
                    ResetGesture();
                }
                break;
            case WeekCalendarGesture.Pending:
                if (WeekCalendarEditMath.PassedThreshold(position - _pressPoint))
                {
                    e.Pointer.Capture(_body);
                    Begin(_candidate);
                }
                break;
            case WeekCalendarGesture.Draw or WeekCalendarGesture.Move or WeekCalendarGesture.Resize:
                if (_isTouch)
                {
                    e.PreventGestureRecognition();
                }
                UpdateDrag();
                UpdateAutoScroll();
                e.Handled = true;
                break;
            case WeekCalendarGesture.Swipe:
                e.PreventGestureRecognition();
                _swipeVelocity.AddSample(position, TimeSpan.FromMilliseconds(e.Timestamp));
                UpdateSwipe(position.X - _pressPoint.X);
                e.Handled = true;
                break;
        }
    }

    private void OnBodyPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        var gesture = _gesture;
        var pane = _gesturePane;
        _holdTimer?.Stop();
        switch (gesture)
        {
            case WeekCalendarGesture.Pending when _candidate == WeekCalendarGesture.Draw && pane is not null:
            {
                // A click or a tap on empty time drafts an hour.
                var (start, end) = WeekCalendarEditMath.ClickDraft(_pressMinute, _owner.TimeGrid);
                ResetGesture();
                _owner.OpenQuickCreate(pane, _pressDay, start, end, keyboard: false);
                return;
            }
            case WeekCalendarGesture.Draw when pane is not null:
            {
                var (start, end) = _drawn;
                var day = _pressDay;
                ResetGesture();
                _owner.OpenQuickCreate(pane, day, start, end, keyboard: false);
                return;
            }
            case WeekCalendarGesture.Move or WeekCalendarGesture.Resize when pane is not null:
                CommitDrag(pane, gesture);
                break;
            case WeekCalendarGesture.Swipe:
                EndSwipe(_swipeVelocity.GetVelocity(TimeSpan.FromMilliseconds(e.Timestamp)).X);
                break;
        }
        ResetGesture();
    }

    private void OnBodyCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer) || ReferenceEquals(e.Pointer.Captured, _body))
        {
            return;
        }
        switch (_gesture)
        {
            case WeekCalendarGesture.Pending:
                // The scroller took the touch (a vertical pan): no long press, no click draft.
                ResetGesture();
                break;
            case WeekCalendarGesture.Draw or WeekCalendarGesture.Move or WeekCalendarGesture.Resize:
                CancelGesture(announce: false);
                ResetGesture();
                break;
            case WeekCalendarGesture.Swipe:
                EndSwipe(0);
                ResetGesture();
                break;
        }
    }

    private void ResetGesture()
    {
        _holdTimer?.Stop();
        _hold.Cancel();
        StopAutoScroll();
        var pointer = _pointer;
        _pointer = null;
        _gesture = WeekCalendarGesture.None;
        _candidate = WeekCalendarGesture.None;
        _gesturePane = null;
        _gesturePresenter = null;
        _dragEvent = null;
        _owner.SetDragging(false);
        _body.ClearValue(CursorProperty);
        // Released last: losing the capture must find the gesture already over.
        if (pointer?.Captured is { } captured && ReferenceEquals(captured, _body))
        {
            pointer.Capture(null);
        }
    }

    // Drags ------------------------------------------------------------------------------------------------------------------

    private void Begin(WeekCalendarGesture mode)
    {
        var pane = _gesturePane!;
        var grid = _owner.TimeGrid;
        _gesture = mode;
        _owner.SetDragging(true);
        _body.Cursor = s_dragCursor ??= new Cursor(StandardCursorType.DragMove);
        switch (mode)
        {
            case WeekCalendarGesture.Draw:
                _owner.CloseEditor();
                _drawn = WeekCalendarEditMath.DrawRange(_pressMinute, _isTouch ? _pressMinute : grid.MinuteAt(PanePoint(_lastPoint).Y), grid);
                pane.ShowDraft(_pressDay, _drawn.Start, _drawn.End, _owner.NewEventCalendarId);
                break;
            case WeekCalendarGesture.Move or WeekCalendarGesture.Resize:
                var presenter = _gesturePresenter!;
                var segment = presenter.Segment!;
                _dragEvent = segment.Event;
                _dragDay = segment.Day;
                _dragStart = segment.StartMinute;
                _dragEnd = segment.EndMinute;
                _owner.CloseEditor();
                _owner.SelectEvent(_dragEvent);
                pane.Lift(presenter);
                if (!_isTouch)
                {
                    UpdateDrag();
                }
                break;
        }
    }

    /// <summary>Recomputes the dragged range from the last pointer position (also while it stands still and the grid scrolls).</summary>
    private void UpdateDrag()
    {
        if (_gesturePane is not { } pane)
        {
            return;
        }
        var grid = _owner.TimeGrid;
        var point = PanePoint(_lastPoint);
        var minute = grid.MinuteAt(point.Y);
        switch (_gesture)
        {
            case WeekCalendarGesture.Draw:
                // Only the pressed day: the draft never changes columns (Arc).
                var drawn = WeekCalendarEditMath.DrawRange(_pressMinute, minute, grid);
                if (drawn != _drawn)
                {
                    _drawn = drawn;
                    pane.ShowDraft(_pressDay, drawn.Start, drawn.End, _owner.NewEventCalendarId);
                }
                break;
            case WeekCalendarGesture.Move:
            {
                var day = pane.TimeAt(point).Day;
                var duration = _dragEnd - _dragStart;
                var start = WeekCalendarEditMath.MoveStart(_dragStart, duration, minute - _pressMinute, grid);
                pane.MoveLifted(day, start, start + duration);
                break;
            }
            case WeekCalendarGesture.Resize:
            {
                var end = WeekCalendarEditMath.ResizeEnd(_dragStart, minute, grid);
                pane.MoveLifted(_dragDay, _dragStart, end);
                break;
            }
        }
    }

    private void CommitDrag(WeekCalendarGridPane pane, WeekCalendarGesture gesture)
    {
        if (_dragEvent is not { } e || pane.LiftedAt is not { } at)
        {
            pane.EndLift();
            return;
        }
        var unchanged = at.Day == _dragDay && Math.Abs(at.Start - _dragStart) < 0.01 && Math.Abs(at.End - _dragEnd) < 0.01;
        var presenter = pane.Lifted;
        if (unchanged)
        {
            // A drag that stayed in its slot changes nothing (no announcement, no toast, no details).
            pane.EndLift(animate: true);
            presenter?.Focus(NavigationMethod.Pointer);
            return;
        }
        pane.EndLift(animate: true);
        if (gesture == WeekCalendarGesture.Move)
        {
            _owner.MoveEventTo(e, at.Day, at.Start, WeekCalendarEditSource.Pointer);
        }
        else
        {
            _owner.ResizeEventTo(e, at.Day, at.End, WeekCalendarEditSource.Pointer);
        }
    }

    /// <summary>Escape during a drag: the draft goes, or the lifted event springs back to where it was.</summary>
    public bool CancelGesture(bool announce = true)
    {
        switch (_gesture)
        {
            case WeekCalendarGesture.Draw:
                _gesturePane?.HideDraft();
                break;
            case WeekCalendarGesture.Move or WeekCalendarGesture.Resize:
                _gesturePane?.EndLift(animate: true);
                break;
            case WeekCalendarGesture.Swipe:
                EndSwipe(0);
                break;
            default:
                return false;
        }
        StopAutoScroll();
        _gesture = WeekCalendarGesture.Canceled;
        _owner.SetDragging(false);
        if (announce)
        {
            _owner.Announce(_owner.Labels.DragCanceledAnnouncement);
        }
        return true;
    }

    /// <summary>True while a press is drawing, moving or resizing.</summary>
    public bool IsDragging => _gesture is WeekCalendarGesture.Draw or WeekCalendarGesture.Move or WeekCalendarGesture.Resize;

    // Auto-scroll ------------------------------------------------------------------------------------------------------------

    /// <summary>The auto-scroll step (px per 60 Hz frame, signed: negative scrolls up) for the pointer at <paramref name="viewPoint"/>.</summary>
    public double AutoScrollStepAt(Point viewPoint)
    {
        var top = _scroll.Bounds.Top;
        var bottom = _scroll.Bounds.Bottom;
        var up = WeekCalendarEditMath.AutoScrollStep(viewPoint.Y - top);
        var down = WeekCalendarEditMath.AutoScrollStep(bottom - viewPoint.Y);
        return up >= down ? -up : down;
    }

    private void UpdateAutoScroll()
    {
        if (!IsDragging || Math.Abs(AutoScrollStepAt(_lastPoint)) < 0.001)
        {
            StopAutoScroll();
            return;
        }
        // Frame-driven: the grid keeps scrolling while the pointer stands still in the zone, at a speed set per frame.
        _autoScroll ??= new LiveLoop(this, OnAutoScrollFrame) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
        _autoScroll.Start();
    }

    private void StopAutoScroll() => _autoScroll?.Stop();

    /// <summary>One auto-scroll frame of <paramref name="seconds"/>: scrolls by the step × 60 px/s and redoes the drag target.</summary>
    internal bool OnAutoScrollFrame(double seconds)
    {
        if (!IsDragging)
        {
            return false;
        }
        var step = AutoScrollStepAt(_lastPoint);
        if (Math.Abs(step) < 0.001)
        {
            return false;
        }
        var max = Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);
        var y = _scroll.Offset.Y;
        var next = Math.Clamp(y + step * 60 * Math.Clamp(seconds, 0, 0.1), 0, max);
        if (Math.Abs(next - y) < 0.001)
        {
            return true;
        }
        _scroll.Offset = new Vector(_scroll.Offset.X, next);
        _autoScrolled += next - y;
        UpdateDrag();
        return true;
    }

    /// <summary>The grid scrolled (wheel, auto-scroll): a drag in progress follows the pointer's new time.</summary>
    private void OnScrolledWhileEditing()
    {
        if (IsDragging)
        {
            UpdateDrag();
        }
    }

    // Swipe ------------------------------------------------------------------------------------------------------------------

    private bool CanSwipe() =>
        _owner.IsCompact && _owner.EffectiveView != WeekCalendarView.Agenda && _slots.Count == 1 && Current is not null &&
        _headViewport.Bounds.Width > 0;

    /// <summary>Starts a swipe: the previous and next ranges sit either side of the current one and travel with it.</summary>
    internal void BeginSwipe()
    {
        if (Current is not { } current)
        {
            return;
        }
        _gesture = WeekCalendarGesture.Swipe;
        _swipeTravel = 0;
        var view = _owner.EffectiveView;
        var (previous, previousCursor) = WeekCalendarRange.Step(view, current.Days, _owner.Date, -1);
        var (next, nextCursor) = WeekCalendarRange.Step(view, current.Days, _owner.Date, +1);
        _swipePrevious = RentNeighbour(previous, previousCursor);
        _swipeNext = RentNeighbour(next, nextCursor);
        UpdateSwipe(0);
        _owner.SetSliding(true);
    }

    private WeekCalendarPaneSlot RentNeighbour(WeekCalendarDays days, DateOnly cursor)
    {
        var slot = Rent();
        // Neighbours sit under the current slot, which stays the last (Current) one.
        _slots.Remove(slot);
        _slots.Insert(0, slot);
        Restack();
        slot.IsLeaving = false;
        slot.SetActive(false);
        slot.Fade.Jump(1);
        slot.Bind(days, cursor);
        return slot;
    }

    /// <summary>Moves the three slots with the finger, 1:1 (<paramref name="travel"/>: px, negative toward later days).</summary>
    internal void UpdateSwipe(double travel)
    {
        if (Current is not { } current)
        {
            return;
        }
        _swipeTravel = travel;
        var width = _headViewport.Bounds.Width;
        current.X.Jump(travel);
        _swipePrevious?.X.Jump(travel - width);
        _swipeNext?.X.Jump(travel + width);
    }

    /// <summary>The finger lifted: past 20% (or on a flick) the next days come in from where they are; else everything springs back.</summary>
    internal void EndSwipe(double velocity)
    {
        var previous = _swipePrevious;
        var next = _swipeNext;
        _swipePrevious = null;
        _swipeNext = null;
        if (Current is not { } current || previous is null || next is null)
        {
            return;
        }
        var width = _headViewport.Bounds.Width;
        var direction = WeekCalendarEditMath.SwipeDirection(_swipeTravel, velocity, width);
        if (direction == 0)
        {
            var spring = MotionSpec.Spring(WeekCalendarMotion.RangeSlide);
            _ = current.X.AnimateTo(0, spring);
            ParkNeighbour(previous, previous.X.AnimateTo(-width, spring));
            ParkNeighbour(next, next.X.AnimateTo(width, spring));
            UpdateSliding();
            return;
        }
        var incoming = direction > 0 ? next : previous;
        Recycle(direction > 0 ? previous : next);
        _swipeIncoming = incoming;
        try
        {
            _owner.SwipeTo(direction, incoming.Days);
        }
        finally
        {
            if (ReferenceEquals(_swipeIncoming, incoming) && _slots.Contains(incoming) && !ReferenceEquals(Current, incoming))
            {
                Recycle(incoming);
            }
            _swipeIncoming = null;
        }
    }

    private async void ParkNeighbour(WeekCalendarPaneSlot slot, Task<bool> motion)
    {
        var generation = slot.Generation;
        await motion;
        if (slot.Generation == generation && _slots.Contains(slot) && !ReferenceEquals(slot, Current))
        {
            Recycle(slot);
        }
        UpdateSliding();
    }

    /// <summary>
    /// The swipe's incoming slot becomes the current one and keeps going from where the finger left it (Full motion): the
    /// range spring takes it to 0 while the old slot slides out. Returns false when there is no swipe to continue.
    /// </summary>
    private bool ContinueSwipe(WeekCalendarDays days, WeekCalendarTransition transition, int direction)
    {
        if (_swipeIncoming is not { } incoming || incoming.Days != days || Current is not { } current || ReferenceEquals(current, incoming))
        {
            return false;
        }
        _swipeIncoming = null;
        if (transition != WeekCalendarTransition.Slide)
        {
            // Reduced or no motion: no travel; the days change in place (with the crossfade at Reduced).
            Recycle(incoming);
            current.X.Jump(0);
            return false;
        }
        var width = _headViewport.Bounds.Width;
        _slots.Remove(incoming);
        _slots.Add(incoming);
        Restack();
        incoming.IsLeaving = false;
        incoming.SetActive(true);
        current.IsLeaving = true;
        current.SetActive(false);
        var spring = MotionSpec.Spring(WeekCalendarMotion.RangeSlide);
        _ = incoming.X.AnimateTo(0, spring);
        Leave(current, current.X.AnimateTo(-Math.Sign(direction) * width, spring));
        AfterRangeChanged(animate: false);
        UpdateSliding();
        return true;
    }

    // Selection ----------------------------------------------------------------------------------------------------------------

    /// <summary>The selected event changed: every pane marks its block.</summary>
    public void SetSelected(WeekCalendarEvent? selected)
    {
        foreach (var slot in _slots)
        {
            slot.Grid.SetSelected(selected);
        }
    }

    /// <summary>Scrolls so <paramref name="start"/>–<paramref name="end"/> is in view (24 below the top when it was not).</summary>
    public void ScrollRangeIntoView(double start, double end)
    {
        var grid = _owner.TimeGrid;
        var viewport = _scroll.Viewport.Height > 0 ? _scroll.Viewport.Height : BodyViewportHeight;
        var top = grid.YOf(start);
        var bottom = grid.YOf(end);
        var offset = _scroll.Offset.Y;
        if (top >= offset && bottom <= offset + viewport)
        {
            return;
        }
        var max = Math.Max(0, grid.BodyHeight - viewport);
        _initialScrollDone = true;
        _scroll.Offset = new Vector(0, Math.Clamp(top - WeekCalendarEditMetrics.ScrollIntoViewMargin, 0, max));
    }

    /// <summary>The bottom of the visible hours in <paramref name="relativeTo"/>'s coordinates.</summary>
    public double VisibleBottomIn(Visual relativeTo) =>
        this.TranslatePoint(new Point(0, _scroll.Bounds.Bottom), relativeTo)?.Y ?? double.PositiveInfinity;

    // Keys -------------------------------------------------------------------------------------------------------------------

    partial void OnEditingKey(KeyEventArgs e, ref bool handled)
    {
        var source = e.Source as Visual;
        var presenter = source?.FindAncestorOfType<WeekCalendarEventPresenter>(includeSelf: true);
        var cell = source?.FindAncestorOfType<WeekCalendarDayCell>(includeSelf: true);
        var modifiers = e.KeyModifiers & ~KeyModifiers.Shift;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (CancelGesture())
            {
                handled = true;
            }
            else if (_owner.SelectedEvent is not null)
            {
                _owner.ClearSelection(announce: true);
                handled = true;
            }
            return;
        }
        if (!_owner.IsEditable)
        {
            return;
        }
        if (modifiers == KeyModifiers.Alt && presenter?.Event is { } moved)
        {
            switch (e.Key)
            {
                case Key.Up or Key.Down when shift:
                    handled = _owner.NudgeEvent(moved, 0, 0, e.Key == Key.Down ? WeekCalendarEditMetrics.SnapMinutes : -WeekCalendarEditMetrics.SnapMinutes);
                    return;
                case Key.Up or Key.Down:
                    handled = _owner.NudgeEvent(moved, 0, e.Key == Key.Down ? WeekCalendarEditMetrics.SnapMinutes : -WeekCalendarEditMetrics.SnapMinutes, 0);
                    return;
                case Key.Left or Key.Right when !shift:
                    handled = _owner.NudgeEvent(moved, e.Key == Key.Right ? 1 : -1, 0, 0);
                    return;
            }
        }
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Enter when cell is not null:
                _owner.BeginCreate(cell.Day);
                handled = true;
                break;
            case Key.Delete or Key.Back:
                if ((presenter?.Event ?? _owner.SelectedEvent) is { } doomed)
                {
                    handled = _owner.DeleteEvent(doomed, keyboard: true);
                }
                break;
        }
    }
}
