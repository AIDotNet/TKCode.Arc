using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What a lifted card is doing.</summary>
internal enum KanbanDragMode
{
    None,

    /// <summary>Following a mouse, pen or touch pointer; a slot holds its place.</summary>
    Pointer,

    /// <summary>Lifted with Space; the card itself moves through the cells.</summary>
    Keyboard,

    /// <summary>Released (or cancelled): the card rides the catch spring into its slot.</summary>
    Landing
}

/// <summary>The state of one drag, from lift to landing (internal).</summary>
internal sealed class KanbanDragSession
{
    public KanbanDragSession(KanbanCard card, KanbanCardView view, KanbanMoveSource source)
    {
        Card = card;
        View = view;
        Source = source;
    }

    public KanbanCard Card { get; }

    public KanbanCardView View { get; }

    public KanbanMoveSource Source { get; }

    public KanbanDragMode Mode { get; set; }

    public int OriginLane { get; set; }

    public int OriginColumn { get; set; }

    public int OriginIndex { get; set; }

    public KanbanSlot OriginSlot { get; set; }

    public int TargetLane { get; set; }

    public int TargetColumn { get; set; }

    public int TargetIndex { get; set; }

    public KanbanSlot? LastTarget { get; set; }

    /// <summary>The slot is as tall as the lifted card.</summary>
    public double SlotHeight { get; set; }

    /// <summary>The card's layout rect when it was lifted (the pointer-dragged card stays arranged there).</summary>
    public Rect OriginRect { get; set; }

    /// <summary>Where the pointer-dragged card's top left corner is drawn (board coordinates).</summary>
    public Point CardPosition { get; set; }

    /// <summary>Press point minus the card's corner: the card keeps the spot it was grabbed at.</summary>
    public Vector GrabOffset { get; set; }

    public IPointer? Pointer { get; set; }

    /// <summary>The pointer in the scroll viewport's coordinates (auto-scroll re-hits with it while the pointer rests).</summary>
    public Point ViewportPoint { get; set; }

    public VelocityTracker Velocity { get; } = new(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(60));

    /// <summary>The next arrange starts the landing flight from <see cref="LandingFrom"/> with <see cref="LandingVelocity"/>.</summary>
    public bool LandingPending { get; set; }

    public Point LandingFrom { get; set; }

    public Vector LandingVelocity { get; set; }

    public bool IsReturning { get; set; }
}

public partial class KanbanBoard
{
    private readonly KanbanHoldRecognizer _hold = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private KanbanDragSession? _session;
    private KanbanCardView? _pressView;
    private IPointer? _pressPointer;
    private Point _pressViewport;
    private Point _touchViewport;
    private bool _ignoreUntilRelease;
    private LiveLoop? _dragLoop;
    private LiveLoop? _holdLoop;
    private TopLevel? _keyHost;
    private MotionValue<Vector>? _chasedSlot;

    /// <summary>The time base for pointer velocity (tests replace it to drive time with the frame clock).</summary>
    internal Func<TimeSpan>? DragClock { get; set; }

    internal KanbanDragSession? Session => _session;

    private TimeSpan Now => DragClock?.Invoke() ?? _clock.Elapsed;

    private void InitializeDrag()
    {
        // The drag loop runs at every motion level: auto-scroll is function, not decoration (Arc keeps it under
        // reduced motion); it also lets a resting pointer's tilt settle and watches the landing.
        _dragLoop = new LiveLoop(this, OnDragFrame) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
        _holdLoop = new LiveLoop(this, OnHoldFrame) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
    }

    // Pointer ----------------------------------------------------------------------------------------------------------

    private static KanbanCardView? CardViewOf(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is KanbanCardView view)
            {
                return view;
            }
            if (visual is KanbanBoard)
            {
                return null;
            }
        }
        return null;
    }

    private Point ViewportPointOf(PointerEventArgs e) => _scroller is { } scroller ? e.GetPosition(scroller) : e.GetPosition(this);

    /// <summary>A viewport point in board (panel) coordinates: the panel is scrolled by the viewer's offset.</summary>
    private Point ToBoard(Point viewportPoint) => viewportPoint + ScrollOffset;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || _panel is null)
        {
            return;
        }
        var view = CardViewOf(e.Source);
        if (view is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (_session is { Mode: KanbanDragMode.Pointer })
        {
            return;
        }
        if (_session is { Mode: KanbanDragMode.Keyboard })
        {
            CancelKeyboardDrag(KanbanDragCancelReason.Programmatic);
        }
        FinishLanding();
        _pressView = view;
        _pressPointer = e.Pointer;
        _pressViewport = ViewportPointOf(e);
        _ignoreUntilRelease = false;
        if (e.Pointer.Type == PointerType.Touch)
        {
            // Touch waits for a still hold; until then the press stays with the scroll viewer (swipes pan the board).
            if (CanDrag)
            {
                _touchViewport = _pressViewport;
                _hold.Delay = TouchHoldDelay;
                _hold.Press(_pressViewport, Now);
                _holdLoop?.Start();
            }
            return;
        }
        view.Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_session is { Mode: KanbanDragMode.Pointer } session && ReferenceEquals(e.Pointer, session.Pointer))
        {
            e.Handled = true;
            e.PreventGestureRecognition();
            if (!_ignoreUntilRelease)
            {
                UpdatePointerDrag(ViewportPointOf(e), sample: true);
            }
            return;
        }
        if (_pressView is null || !ReferenceEquals(e.Pointer, _pressPointer) || _ignoreUntilRelease)
        {
            return;
        }
        var point = ViewportPointOf(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touchViewport = point;
            if (_hold.Move(point, Now) == KanbanHoldState.Panning)
            {
                ResetPress();
            }
            return;
        }
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ResetPress();
            return;
        }
        if (CanDrag && KanbanDragPhysics.PassedThreshold(_pressViewport, point, DragThreshold))
        {
            BeginPointerDrag(e.Pointer, point, KanbanMoveSource.Pointer);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_session is { Mode: KanbanDragMode.Pointer } session && ReferenceEquals(e.Pointer, session.Pointer))
        {
            e.Handled = true;
            DropPointer();
            ResetPress();
            return;
        }
        var view = _pressView;
        var pointer = _pressPointer;
        var ignored = _ignoreUntilRelease;
        ResetPress();
        if (ignored || view is null || !ReferenceEquals(pointer, e.Pointer) || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }
        // A press that never lifted is a click (touch: a tap that neither held nor panned).
        e.Handled = true;
        InvokeCard(view.Card, view, NavigationMethod.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_session is { Mode: KanbanDragMode.Pointer } session && ReferenceEquals(e.Pointer, session.Pointer))
        {
            CancelPointerDrag(KanbanDragCancelReason.PointerCancelled);
        }
    }

    private void ResetPress()
    {
        _pressView = null;
        _pressPointer = null;
        _ignoreUntilRelease = false;
        _hold.Reset();
        _holdLoop?.Stop();
    }

    private bool OnHoldFrame(double dt)
    {
        if (_pressView is null || _hold.State != KanbanHoldState.Pending)
        {
            return false;
        }
        if (_hold.Tick(Now) == KanbanHoldState.Lifted && _pressPointer is { } pointer)
        {
            BeginPointerDrag(pointer, _touchViewport, KanbanMoveSource.Touch);
            return false;
        }
        return true;
    }

    private bool LocateCard(KanbanCard card, out int lane, out int column, out int index)
    {
        lane = column = index = -1;
        return _grid is not null && _grid.TryLocate(card, out lane, out column, out index);
    }

    /// <summary>The public slot of a cell position (Status view keeps the card's own owner).</summary>
    private KanbanSlot SlotAt(KanbanCard card, int lane, int column, int index)
    {
        var grid = _grid!;
        var assignee = grid.GroupBy == KanbanGroupBy.Status ? card.AssigneeKey : grid.Lanes[lane].AssigneeKey;
        return new KanbanSlot(grid.Columns[column].Identity, assignee, index);
    }

    private KanbanSlot TargetSlot(KanbanDragSession session) =>
        SlotAt(session.Card, session.TargetLane, session.TargetColumn, session.TargetIndex);

    private void BeginPointerDrag(IPointer pointer, Point viewportPoint, KanbanMoveSource source)
    {
        var view = _pressView;
        if (view is null || _panel is null || !LocateCard(view.Card, out var lane, out var column, out var index))
        {
            return;
        }
        var card = view.Card;
        var origin = SlotAt(card, lane, column, index);
        var starting = new KanbanCardDragStartingEventArgs(CardDragStartingEvent, card, origin, source);
        RaiseEvent(starting);
        if (starting.Cancel)
        {
            _ignoreUntilRelease = true;
            return;
        }
        var motion = _panel.Flip.Get(view);
        var visual = _panel.VisualRect(view);
        var press = ToBoard(_pressViewport);
        var session = new KanbanDragSession(card, view, source)
        {
            Mode = KanbanDragMode.Pointer,
            OriginLane = lane,
            OriginColumn = column,
            OriginIndex = index,
            OriginSlot = origin,
            // Arc's first frame counted the card's own middle and jumped the slot when grabbed below it: the slot
            // starts where the card was.
            TargetLane = lane,
            TargetColumn = column,
            TargetIndex = index,
            SlotHeight = visual.Height,
            OriginRect = motion.Layout ?? view.Bounds,
            GrabOffset = press - visual.TopLeft,
            Pointer = pointer,
            ViewportPoint = viewportPoint
        };
        session.CardPosition = ToBoard(viewportPoint) - session.GrabOffset;
        session.LastTarget = origin;
        _session = session;
        _hold.Reset();
        _holdLoop?.Stop();
        pointer.Capture(this);
        view.Focus(NavigationMethod.Pointer);
        _panel.JumpSlot();
        _panel.Slot.IsVisible = true;
        _panel.SetLifted(view, true);
        view.SetState(lifted: false, dragged: true, landing: false, instant: true);
        _ = motion.Scale.AnimateTo(KanbanDragPhysics.LiftScale, KanbanMotion.Quick);
        session.Velocity.AddSample(viewportPoint, Now);
        Cursor = new Cursor(StandardCursorType.Hand);
        StartDragState(card);
        WatchKeysWhileDragging(true);
        UpdateLiveCounts();
        _panel.InvalidateMeasure();
        PlacePointerCard(session);
        ShowProxy(session);
        AnnounceDrag(Strings.PickedUp(TitleOf(card)));
        RaiseEvent(new KanbanCardDragEventArgs(CardDragStartedEvent, card, origin, origin, null, IsTargetOverLimit(session), source));
        _dragLoop?.Start();
    }

    private void StartDragState(KanbanCard card)
    {
        IsDragging = true;
        DraggedCard = card;
        PseudoClasses.Set(DraggingPseudoClass, true);
    }

    private void EndDragState()
    {
        IsDragging = false;
        DraggedCard = null;
        PseudoClasses.Set(DraggingPseudoClass, false);
        ClearValue(CursorProperty);
        WatchKeysWhileDragging(false);
    }

    /// <summary>The panel arranges the pointer-dragged card at its origin rect; its offset carries it to the pointer.</summary>
    internal void PlacePointerCard(KanbanDragSession session)
    {
        if (_panel is null)
        {
            return;
        }
        var motion = _panel.Flip.Get(session.View);
        motion.Layout = session.OriginRect;
        motion.Offset.Jump(session.CardPosition - session.OriginRect.TopLeft);
    }

    private void UpdatePointerDrag(Point viewportPoint, bool sample)
    {
        if (_session is not { Mode: KanbanDragMode.Pointer } session || _panel is null)
        {
            return;
        }
        session.ViewportPoint = viewportPoint;
        var point = ToBoard(viewportPoint);
        // Direct follow, no smoothing (Arc: the card is one frame behind the pointer).
        session.CardPosition = point - session.GrabOffset;
        PlacePointerCard(session);
        if (sample)
        {
            session.Velocity.AddSample(viewportPoint, Now);
        }
        UpdateTilt(session);
        if (_panel.TryHit(point, session.TargetLane, out var lane, out var column, out var index))
        {
            SetTarget(session, lane, column, index);
        }
    }

    /// <summary>
    /// Tilt from the horizontal speed (clockwise to the right, 0.0064° per px/s, at most 7°) on the Quick spring. The
    /// speed is a time-window estimate that goes stale 60ms after the last move, so a card the pointer holds still
    /// levels out (Arc keeps the last angle; a still card looks better level).
    /// </summary>
    private void UpdateTilt(KanbanDragSession session)
    {
        if (_panel is null)
        {
            return;
        }
        var motion = _panel.Flip.Get(session.View);
        var tilt = ArcMotion.IsFull(this) ? KanbanDragPhysics.TiltFor(session.Velocity.GetVelocity(Now).X) : 0;
        if (Math.Abs(motion.Rotation.Target - tilt) > 0.001 || (!motion.Rotation.IsAnimating && Math.Abs(motion.Rotation.Value - tilt) > 0.001))
        {
            _ = motion.Rotation.AnimateTo(tilt, KanbanMotion.Quick);
        }
    }

    /// <summary>Moves the drag's target; neighbours and the slot glide on the layout spring, keeping their speed.</summary>
    private void SetTarget(KanbanDragSession session, int lane, int column, int index)
    {
        if (session.TargetLane == lane && session.TargetColumn == column && session.TargetIndex == index)
        {
            return;
        }
        var newCell = session.TargetLane != lane || session.TargetColumn != column;
        if (newCell && session.Mode == KanbanDragMode.Pointer)
        {
            _panel?.JumpSlot();
        }
        _panel?.Flip.Arm(KanbanMotion.Layout, keepVelocity: true);
        session.TargetLane = lane;
        session.TargetColumn = column;
        session.TargetIndex = index;
        UpdateLiveCounts();
        _panel?.InvalidateMeasure();
        var target = TargetSlot(session);
        var previous = session.LastTarget;
        session.LastTarget = target;
        RaiseEvent(new KanbanCardDragEventArgs(CardDragTargetChangedEvent, session.Card, session.OriginSlot, target, previous,
            IsTargetOverLimit(session), session.Source));
    }

    private bool IsTargetOverLimit(KanbanDragSession session)
    {
        if (_grid is null || session.TargetColumn < 0 || session.TargetColumn >= _grid.Columns.Count)
        {
            return false;
        }
        return KanbanLayout.IsOverLimit(LiveColumnCount(session.TargetColumn), _grid.Columns[session.TargetColumn].Limit);
    }

    private void DropPointer()
    {
        if (_session is not { Mode: KanbanDragMode.Pointer } session || _panel is null)
        {
            return;
        }
        var target = TargetSlot(session);
        var moving = new KanbanCardMovingEventArgs(CardMovingEvent, session.Card, session.OriginSlot, target,
            IsTargetOverLimit(session), session.Source);
        RaiseEvent(moving);
        if (moving.Cancel)
        {
            CancelPointerDrag(KanbanDragCancelReason.Refused);
            return;
        }
        BeginLanding(session, returning: false);
        var args = CommitMove(session.Card, target, session.OriginSlot, session.Source, animate: false);
        var column = ColumnFor(session.Card.ColumnKey)?.Name ?? string.Empty;
        var person = PersonFor(session.Card.AssigneeKey);
        if (GroupBy == KanbanGroupBy.Assignee && args is not null && !ReferenceEquals(args.FromAssignee, args.ToAssignee))
        {
            AnnounceDrag(Strings.DroppedForPerson(TitleOf(session.Card), column, person?.Name ?? Strings.Unassigned));
        }
        else
        {
            AnnounceDrag(Strings.Dropped(TitleOf(session.Card), column));
        }
    }

    /// <summary>Ends the pointer phase: the card starts flying into its slot on the next arrange.</summary>
    private void BeginLanding(KanbanDragSession session, bool returning)
    {
        var motion = _panel!.Flip.Get(session.View);
        session.LandingFrom = session.CardPosition;
        session.LandingVelocity = ArcMotion.IsFull(this) ? session.Velocity.GetVelocity(Now) : default;
        session.Mode = KanbanDragMode.Landing;
        session.LandingPending = true;
        session.IsReturning = returning;
        if (session.Pointer is { } pointer && ReferenceEquals(pointer.Captured, this))
        {
            pointer.Capture(null);
        }
        // Shadow and border ease back to rest (240 / 160ms in the theme); scale and tilt settle on the Quick spring.
        session.View.SetState(lifted: false, dragged: false, landing: true);
        _ = motion.Scale.AnimateTo(1, KanbanMotion.Quick);
        _ = motion.Rotation.AnimateTo(0, KanbanMotion.Quick);
        EndDragState();
        UpdateLiveCounts();
        _panel.InvalidateMeasure();
        _dragLoop?.Start();
    }

    /// <summary>
    /// The landing card's first arrange in its slot: it starts from where it was drawn, with the release velocity, and
    /// chases the slot's live position on the catch spring (the slot may still be gliding). Code-driven because the
    /// target moves every frame and the start velocity comes from the pointer.
    /// </summary>
    internal void PlaceLanding(KanbanCardView view, Rect rect)
    {
        if (_session is not { Mode: KanbanDragMode.Landing } session || _panel is null)
        {
            return;
        }
        var motion = _panel.Flip.Get(view);
        if (!session.LandingPending)
        {
            motion.Layout = rect;
            return;
        }
        session.LandingPending = false;
        motion.Layout = rect;
        motion.Offset.Jump(session.LandingFrom - rect.TopLeft);
        var slot = _panel.Flip.Get(_panel.Slot).Offset;
        if (_chasedSlot is not null)
        {
            _chasedSlot.Changed -= OnChasedSlotMoved;
        }
        _chasedSlot = slot;
        slot.Changed += OnChasedSlotMoved;
        _ = motion.Offset.AnimateTo(slot.Value, KanbanMotion.Catch, session.LandingVelocity);
    }

    private void OnChasedSlotMoved(Vector slotOffset)
    {
        if (_session is { Mode: KanbanDragMode.Landing, LandingPending: false } session && _panel is not null)
        {
            // Retargeting keeps the card's current velocity: it bends toward the moving slot instead of restarting.
            _ = _panel.Flip.Get(session.View).Offset.AnimateTo(slotOffset, KanbanMotion.Catch);
        }
    }

    /// <summary>The card reached its slot (or a new drag needs the board): drop the slot and the lifted draw order.</summary>
    private void FinishLanding()
    {
        if (_session is not { Mode: KanbanDragMode.Landing } session || _panel is null)
        {
            return;
        }
        _session = null;
        if (_chasedSlot is not null)
        {
            _chasedSlot.Changed -= OnChasedSlotMoved;
            _chasedSlot = null;
        }
        // Swap back to the real card in the same pass the copy goes.
        HideProxy();
        _panel.Flip.Get(session.View).Rest();
        _panel.SetLifted(session.View, false);
        session.View.SetState(lifted: false, dragged: false, landing: false);
        _panel.Slot.IsVisible = false;
        var slotMotion = _panel.Flip.Get(_panel.Slot);
        slotMotion.Rest();
        slotMotion.Layout = null;
        UpdateLiveCounts();
        _panel.InvalidateMeasure();
    }

    /// <summary>Returns a pointer-dragged card to where it was lifted (Escape, lost pointer, refused drop).</summary>
    private void CancelPointerDrag(KanbanDragCancelReason reason)
    {
        if (_session is not { Mode: KanbanDragMode.Pointer } session || _panel is null)
        {
            return;
        }
        RaiseEvent(new KanbanCardDragCancelledEventArgs(CardDragCancelledEvent, session.Card, session.OriginSlot, reason));
        if (session.TargetLane != session.OriginLane || session.TargetColumn != session.OriginColumn)
        {
            _panel.JumpSlot();
        }
        _panel.Flip.Arm(KanbanMotion.Layout, keepVelocity: true);
        session.TargetLane = session.OriginLane;
        session.TargetColumn = session.OriginColumn;
        session.TargetIndex = session.OriginIndex;
        BeginLanding(session, returning: true);
        AnnounceDrag(Strings.Returned(TitleOf(session.Card)));
    }

    private bool OnDragFrame(double dt)
    {
        var session = _session;
        if (session is null || _panel is null)
        {
            return !TryReleaseLingeringProxy();
        }
        if (session.Mode == KanbanDragMode.Landing)
        {
            if (session.LandingPending)
            {
                return true;
            }
            var card = _panel.Flip.Get(session.View);
            var slot = _panel.Flip.Get(_panel.Slot);
            var offset = card.Offset.Value;
            // Arc unmounts the flying card once both axes are within half a pixel of the slot (the leftover tilt and
            // scale are invisible and dropped).
            var close = Math.Abs(offset.X) < 0.5 && Math.Abs(offset.Y) < 0.5;
            if (!slot.Offset.IsAnimating && (close || !card.Offset.IsAnimating))
            {
                FinishLanding();
                return false;
            }
            return true;
        }
        if (session.Mode != KanbanDragMode.Pointer)
        {
            return session.Mode == KanbanDragMode.Keyboard;
        }
        UpdateTilt(session);
        AutoScroll(session, dt);
        return true;
    }

    /// <summary>
    /// Edge auto-scroll on both axes: inside 64px of an edge the board scrolls at ceil(20·((64 − d)/64)²) px per 60Hz
    /// frame (up to 1200 px/s), integrated over the real frame time, and the target is hit again with the resting
    /// pointer as the content moves under it. The edges are the viewport's, clipped to the window.
    /// </summary>
    private void AutoScroll(KanbanDragSession session, double dt)
    {
        if (_scroller is null || _ignoreUntilRelease)
        {
            return;
        }
        var visible = VisibleViewport();
        if (visible.Width <= 0 || visible.Height <= 0)
        {
            return;
        }
        var p = session.ViewportPoint;
        var vx = KanbanLayout.AutoScrollVelocity(p.X - visible.X, visible.Width);
        var vy = KanbanLayout.AutoScrollVelocity(p.Y - visible.Y, visible.Height);
        if (vx == 0 && vy == 0)
        {
            return;
        }
        var extent = _scroller.Extent;
        var viewport = _scroller.Viewport;
        var max = new Vector(Math.Max(0, extent.Width - viewport.Width), Math.Max(0, extent.Height - viewport.Height));
        var current = _scroller.Offset;
        var next = new Vector(Math.Clamp(current.X + vx * dt, 0, max.X), Math.Clamp(current.Y + vy * dt, 0, max.Y));
        if (Math.Abs(next.X - current.X) < 0.01 && Math.Abs(next.Y - current.Y) < 0.01)
        {
            return;
        }
        _scroller.Offset = next;
    }

    /// <summary>The scroll viewport in its own coordinates, clipped to the window's client area.</summary>
    private Rect VisibleViewport()
    {
        if (_scroller is null)
        {
            return default;
        }
        var rect = new Rect(_scroller.Viewport.Width > 0 ? _scroller.Viewport : _scroller.Bounds.Size);
        if (TopLevel.GetTopLevel(this) is { } top && top.TranslatePoint(default, _scroller) is { } origin)
        {
            rect = rect.Intersect(new Rect(origin, top.ClientSize));
        }
        return rect;
    }

    private void OnScrolledDuringDrag()
    {
        if (_session is { Mode: KanbanDragMode.Pointer } session)
        {
            UpdatePointerDrag(session.ViewportPoint, sample: false);
        }
    }

    private void WatchKeysWhileDragging(bool watch)
    {
        if (_keyHost is not null)
        {
            _keyHost.RemoveHandler(KeyDownEvent, OnDragKeyDown);
            _keyHost = null;
        }
        if (watch && TopLevel.GetTopLevel(this) is { } top)
        {
            // Escape cancels a pointer drag wherever the focus is.
            _keyHost = top;
            top.AddHandler(KeyDownEvent, OnDragKeyDown, RoutingStrategies.Tunnel);
        }
    }

    private void OnDragKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _session is { Mode: KanbanDragMode.Pointer })
        {
            e.Handled = true;
            CancelPointerDrag(KanbanDragCancelReason.Escape);
            // Arc re-lifted the card with a stale grab offset if the button was still down: ignore moves until release.
            _ignoreUntilRelease = true;
        }
    }

    // Keyboard ---------------------------------------------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled)
        {
            base.OnKeyDown(e);
            return;
        }
        var view = CardViewOf(e.Source);
        if (_session is { Mode: KanbanDragMode.Keyboard } session && view is not null && ReferenceEquals(view, session.View))
        {
            if (HandleLiftedKey(session, e))
            {
                e.Handled = true;
                return;
            }
        }
        else if (view is not null && _session is not { Mode: KanbanDragMode.Pointer })
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                InvokeCard(view.Card, view, NavigationMethod.Tab);
                return;
            }
            if (e.Key == Key.Space && CanDrag)
            {
                e.Handled = true;
                BeginKeyboardDrag(view.Card);
                return;
            }
        }
        base.OnKeyDown(e);
    }

    private bool HandleLiftedKey(KanbanDragSession session, KeyEventArgs e)
    {
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                DropKeyboard();
                return true;
            case Key.Escape:
                CancelKeyboardDrag(KanbanDragCancelReason.Escape);
                return true;
            case Key.Up:
                StepKeyboard(session, shift && GroupBy == KanbanGroupBy.Assignee ? -1 : 0, 0, shift && GroupBy == KanbanGroupBy.Assignee ? 0 : -1);
                return true;
            case Key.Down:
                StepKeyboard(session, shift && GroupBy == KanbanGroupBy.Assignee ? 1 : 0, 0, shift && GroupBy == KanbanGroupBy.Assignee ? 0 : 1);
                return true;
            case Key.Left:
                StepKeyboard(session, 0, -1, 0);
                return true;
            case Key.Right:
                StepKeyboard(session, 0, 1, 0);
                return true;
        }
        return false;
    }

    /// <summary>Lifts <paramref name="card"/> as if Space was pressed on it (the arrows then move it).</summary>
    public void BeginKeyboardDrag(KanbanCard card)
    {
        FlushRebuild();
        if (!CanDrag || _panel is null || _session is { Mode: KanbanDragMode.Pointer or KanbanDragMode.Keyboard })
        {
            return;
        }
        FinishLanding();
        var view = _panel.ViewOf(card);
        if (view is null || !LocateCard(card, out var lane, out var column, out var index))
        {
            return;
        }
        var origin = SlotAt(card, lane, column, index);
        var starting = new KanbanCardDragStartingEventArgs(CardDragStartingEvent, card, origin, KanbanMoveSource.Keyboard);
        RaiseEvent(starting);
        if (starting.Cancel)
        {
            return;
        }
        var session = new KanbanDragSession(card, view, KanbanMoveSource.Keyboard)
        {
            Mode = KanbanDragMode.Keyboard,
            OriginLane = lane,
            OriginColumn = column,
            OriginIndex = index,
            OriginSlot = origin,
            TargetLane = lane,
            TargetColumn = column,
            TargetIndex = index,
            LastTarget = origin
        };
        _session = session;
        _panel.SetLifted(view, true);
        view.SetState(lifted: true, dragged: false, landing: false);
        view.LostFocus += OnLiftedLostFocus;
        if (!view.IsFocused)
        {
            view.Focus(NavigationMethod.Tab);
        }
        ShowProxy(session);
        StartDragState(card);
        UpdateLiveCounts();
        AnnounceDrag(Strings.Lifted(TitleOf(card), PositionText(session)));
        RaiseEvent(new KanbanCardDragEventArgs(CardDragStartedEvent, card, origin, origin, null, IsTargetOverLimit(session),
            KanbanMoveSource.Keyboard));
    }

    private void OnLiftedLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_session is { Mode: KanbanDragMode.Keyboard } session && ReferenceEquals(sender, session.View))
        {
            // Arc kept the card lifted after the focus left (and then ignored every key elsewhere): we put it back.
            CancelKeyboardDrag(KanbanDragCancelReason.FocusLost);
        }
    }

    /// <summary>
    /// One keyboard step: up and down inside the cell (no wrap), left and right to the neighbouring column at the same
    /// position (clamped to its end), Shift+up and down to the neighbouring swimlane. A step past an edge does nothing.
    /// </summary>
    private void StepKeyboard(KanbanDragSession session, int laneStep, int columnStep, int indexStep)
    {
        var grid = _grid;
        if (grid is null)
        {
            return;
        }
        var lane = session.TargetLane + laneStep;
        var column = session.TargetColumn + columnStep;
        if (lane < 0 || lane >= grid.Lanes.Count || column < 0 || column >= grid.Columns.Count)
        {
            return;
        }
        var others = grid.Cell(lane, column).Count(c => !ReferenceEquals(c, session.Card));
        int index;
        if (indexStep != 0)
        {
            index = session.TargetIndex + indexStep;
            if (index < 0 || index > others)
            {
                return;
            }
        }
        else
        {
            index = Math.Min(session.TargetIndex, others);
        }
        if (lane == session.TargetLane && column == session.TargetColumn && index == session.TargetIndex)
        {
            return;
        }
        SetTarget(session, lane, column, index);
        AnnounceDrag(PositionText(session));
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => session.View.BringIntoView(), Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private string PositionText(KanbanDragSession session)
    {
        var grid = _grid!;
        var column = grid.Columns[session.TargetColumn].Name;
        if (grid.HasLaneHeads)
        {
            var lane = grid.Lanes[session.TargetLane];
            return Strings.LanePosition(column, lane.IsUnassigned ? Strings.Unassigned : lane.Person?.Name ?? string.Empty, session.TargetIndex + 1);
        }
        return Strings.Position(column, session.TargetIndex + 1);
    }

    private void DropKeyboard()
    {
        if (_session is not { Mode: KanbanDragMode.Keyboard } session || _panel is null)
        {
            return;
        }
        var target = TargetSlot(session);
        var moving = new KanbanCardMovingEventArgs(CardMovingEvent, session.Card, session.OriginSlot, target,
            IsTargetOverLimit(session), KanbanMoveSource.Keyboard);
        RaiseEvent(moving);
        if (moving.Cancel)
        {
            CancelKeyboardDrag(KanbanDragCancelReason.Refused);
            return;
        }
        EndKeyboardSession(session);
        var args = CommitMove(session.Card, target, session.OriginSlot, KanbanMoveSource.Keyboard, animate: false);
        var column = ColumnFor(session.Card.ColumnKey)?.Name ?? string.Empty;
        if (GroupBy == KanbanGroupBy.Assignee && args is not null && !ReferenceEquals(args.FromAssignee, args.ToAssignee))
        {
            AnnounceDrag(Strings.DroppedForPerson(TitleOf(session.Card), column, PersonFor(session.Card.AssigneeKey)?.Name ?? Strings.Unassigned));
        }
        else
        {
            AnnounceDrag(Strings.Dropped(TitleOf(session.Card), column));
        }
    }

    private void CancelKeyboardDrag(KanbanDragCancelReason reason)
    {
        if (_session is not { Mode: KanbanDragMode.Keyboard } session || _panel is null)
        {
            return;
        }
        // The card glides back to where it was lifted from; nothing was committed.
        _panel.Flip.Arm(KanbanMotion.Layout, keepVelocity: true);
        EndKeyboardSession(session);
        UpdateLiveCounts();
        _panel.InvalidateMeasure();
        AnnounceDrag(Strings.Returned(TitleOf(session.Card)));
        RaiseEvent(new KanbanCardDragCancelledEventArgs(CardDragCancelledEvent, session.Card, session.OriginSlot, reason));
    }

    private void EndKeyboardSession(KanbanDragSession session)
    {
        session.View.LostFocus -= OnLiftedLostFocus;
        _session = null;
        _panel?.SetLifted(session.View, false);
        session.View.SetState(lifted: false, dragged: false, landing: false);
        EndDragState();
        ReleaseProxyWhenSettled();
    }

    /// <summary>Cancels a drag in progress: the card goes back to where it was lifted from.</summary>
    public void CancelDrag()
    {
        if (_session is { Mode: KanbanDragMode.Pointer })
        {
            CancelPointerDrag(KanbanDragCancelReason.Programmatic);
        }
        else if (_session is { Mode: KanbanDragMode.Keyboard })
        {
            CancelKeyboardDrag(KanbanDragCancelReason.Programmatic);
        }
        FinishLanding();
    }

    /// <summary>A rebuild while dragging (outside data change): a card that left the board ends the drag.</summary>
    private void ValidateDragAfterRebuild()
    {
        var session = _session;
        if (session is null || _grid is null)
        {
            return;
        }
        if (!_grid.TryLocate(session.Card, out var lane, out var column, out _))
        {
            HideProxy();
            _session = null;
            session.View.LostFocus -= OnLiftedLostFocus;
            EndDragState();
            _panel?.SetLifted(session.View, false);
            session.View.SetState(false, false, false);
            if (_panel is not null)
            {
                _panel.Slot.IsVisible = false;
            }
            return;
        }
        if (session.Mode is KanbanDragMode.Pointer or KanbanDragMode.Keyboard)
        {
            session.OriginLane = lane;
            session.OriginColumn = column;
            session.TargetLane = Math.Clamp(session.TargetLane, 0, _grid.Lanes.Count - 1);
            session.TargetColumn = Math.Clamp(session.TargetColumn, 0, _grid.Columns.Count - 1);
        }
    }

    /// <summary>
    /// Moves <paramref name="card"/> to position <paramref name="index"/> of column <paramref name="columnKey"/> (and, in
    /// swimlanes or with <paramref name="assigneeKey"/>, to that person), with the same glide a keyboard move uses.
    /// </summary>
    public void MoveCard(KanbanCard card, string columnKey, int index, string? assigneeKey = null, bool animate = true)
    {
        FlushRebuild();
        CancelDrag();
        if (_grid is null || _grid.ColumnIndex(columnKey) < 0 || _grid.SlotOf(card) is not { } from)
        {
            return;
        }
        var owner = assigneeKey ?? card.AssigneeKey;
        if (assigneeKey is not null && GroupBy == KanbanGroupBy.Status && assigneeKey != card.AssigneeKey)
        {
            SetQuietly(card, KanbanCard.AssigneeKeyProperty, assigneeKey);
        }
        CommitMove(card, new KanbanSlot(columnKey, owner, Math.Max(0, index)), from, KanbanMoveSource.Programmatic, animate);
    }

    // Live counts ------------------------------------------------------------------------------------------------------

    /// <summary>A column's count right now: a lifted card counts in its target column, not in the one it left.</summary>
    internal int LiveColumnCount(int column)
    {
        if (_grid is null)
        {
            return 0;
        }
        var count = _grid.ColumnCount(column);
        if (_session is { Mode: KanbanDragMode.Pointer or KanbanDragMode.Keyboard } s)
        {
            if (s.OriginColumn == column)
            {
                count--;
            }
            if (s.TargetColumn == column)
            {
                count++;
            }
        }
        return count;
    }

    private int LiveLaneCount(int lane)
    {
        if (_grid is null)
        {
            return 0;
        }
        var count = _grid.LaneCount(lane);
        if (_grid.HasLaneHeads && _session is { Mode: KanbanDragMode.Pointer or KanbanDragMode.Keyboard } s)
        {
            if (s.OriginLane == lane)
            {
                count--;
            }
            if (s.TargetLane == lane)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Refreshes the column heads, cells, lane heads and the slot with the live counts.</summary>
    internal void UpdateLiveCounts()
    {
        var grid = _grid;
        var panel = _panel;
        if (grid is null || panel is null)
        {
            return;
        }
        var strings = Strings;
        var session = _session is { Mode: KanbanDragMode.Pointer or KanbanDragMode.Keyboard } s ? s : null;
        for (var i = 0; i < grid.Columns.Count && i < panel.Heads.Count; i++)
        {
            var column = grid.Columns[i];
            var count = LiveColumnCount(i);
            var countName = column.Limit is { } limit ? strings.CardCountOfLimit(count, limit) : strings.CardCount(count);
            panel.Heads[i].SetCount(count, column.Limit, countName, strings.AddCardTo(column.Name));
            var over = KanbanLayout.IsOverLimit(count, column.Limit);
            for (var lane = 0; lane < grid.Lanes.Count; lane++)
            {
                var target = session is { Mode: KanbanDragMode.Pointer } && session.TargetLane == lane && session.TargetColumn == i;
                panel.CellView(lane, i)?.SetState(over, target);
            }
        }
        for (var lane = 0; lane < panel.LaneHeads.Count && lane < grid.Lanes.Count; lane++)
        {
            panel.LaneHeads[lane].Set(grid.Lanes[lane], LiveLaneCount(lane), strings);
        }
        if (session is not null && session.TargetColumn < grid.Columns.Count)
        {
            var over = IsTargetOverLimit(session);
            var limit = grid.Columns[session.TargetColumn].Limit;
            panel.Slot.SetState(over, over && limit is { } l ? strings.SlotOverLimit(l) : null);
        }
    }

    private string TitleOf(KanbanCard card) => string.IsNullOrWhiteSpace(card.Title) ? Strings.Untitled : card.Title;
}
