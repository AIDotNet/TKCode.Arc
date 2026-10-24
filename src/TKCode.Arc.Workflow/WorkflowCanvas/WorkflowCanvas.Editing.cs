using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Gestures;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class WorkflowCanvas
{
    /// <summary>Whether people can move and delete steps (default false). Selecting works either way.</summary>
    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<WorkflowCanvas, bool>(nameof(IsEditable));

    /// <summary>Whether moved steps snap to the dot grid when let go (default true); holding Alt while dragging skips it.</summary>
    public static readonly StyledProperty<bool> SnapToGridProperty =
        AvaloniaProperty.Register<WorkflowCanvas, bool>(nameof(SnapToGrid), true);

    /// <summary>Raised once each time the selection changes, also when selected steps leave the workflow.</summary>
    public static readonly RoutedEvent<WorkflowSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<WorkflowCanvas, WorkflowSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised once after each edit a person makes (moving steps, deleting steps), when it is done: save the workflow or
    /// record the edit here.
    /// </summary>
    public static readonly RoutedEvent<WorkflowEditEventArgs> GraphEditedEvent =
        RoutedEvent.Register<WorkflowCanvas, WorkflowEditEventArgs>(nameof(GraphEdited), RoutingStrategies.Bubble);

    /// <summary>Distance from the view's edge where dragging scrolls the view, and the fastest scroll (px/s).</summary>
    private const double AutoPanEdge = 24;
    private const double AutoPanMaxSpeed = 900;

    private enum Gesture
    {
        None,
        Pending,
        Pan,
        Move,
        Marquee,
        ConnectPending,
        Connect
    }

    private readonly List<WorkflowNode> _selection = new();

    /// <summary>Steps being moved (a drag or the keyboard's move mode) and where their cards started.</summary>
    private readonly Dictionary<WorkflowNode, Point> _dragStart = new();

    private Vector _dragOffset;
    private Gesture _gesture;
    private WorkflowNode? _pressNode;
    private Point _pressWorld;
    private Point _lastPointer;
    private KeyModifiers _pressModifiers;
    private bool _keyboardMove;
    private int _dragsSincePress;

    /// <summary>True while a change a person made on the canvas is written to the workflow (see <see cref="ChangeGraph"/>).</summary>
    private bool _changingGraph;

    private LiveLoop? _autoPan;
    private WorkflowOverlayLayer? _overlay;

    /// <inheritdoc cref="IsEditableProperty"/>
    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <inheritdoc cref="SnapToGridProperty"/>
    public bool SnapToGrid
    {
        get => GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<WorkflowSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="GraphEditedEvent"/>
    public event EventHandler<WorkflowEditEventArgs>? GraphEdited
    {
        add => AddHandler(GraphEditedEvent, value);
        remove => RemoveHandler(GraphEditedEvent, value);
    }

    /// <summary>The selected steps, in the order they were selected.</summary>
    public IReadOnlyList<WorkflowNode> SelectedNodes => _selection;

    /// <summary>True while steps are being moved by a drag or the keyboard (tests).</summary>
    internal bool IsMoving => _dragStart.Count > 0;

    /// <summary>
    /// Selects <paramref name="nodes"/> (steps that are not in <see cref="Graph"/> are ignored); works before the canvas
    /// is shown, too.
    /// </summary>
    public void Select(IEnumerable<WorkflowNode> nodes) => Select(nodes, []);

    /// <summary>Selects <paramref name="nodes"/> and <paramref name="edges"/> (those not in <see cref="Graph"/> are ignored).</summary>
    public void Select(IEnumerable<WorkflowNode> nodes, IEnumerable<WorkflowEdge> edges)
        => SetSelection(nodes.Where(n => Graph?.Nodes.Contains(n) == true).Distinct().ToList(),
            edges.Where(e => Graph?.Edges.Contains(e) == true).Distinct().ToList());

    /// <summary>Selects nothing.</summary>
    public void ClearSelection() => SetSelection([]);

    /// <summary>
    /// Deletes the selected steps that are not locked (their edges go with them) and the selected edges, and returns how
    /// many steps and edges it deleted. With nothing selected it deletes the focused step. Undo brings them back.
    /// </summary>
    public int DeleteSelection()
    {
        var (nodes, edges) = DeleteSelected(announce: true);
        return nodes.Count + edges.Count;
    }

    /// <summary>Deletes the selection (see <see cref="DeleteSelection"/>); returns the steps and the selected edges it deleted.</summary>
    private (List<WorkflowNode> Nodes, List<WorkflowEdge> Edges) DeleteSelected(bool announce)
    {
        if (Graph is not { } graph)
        {
            return ([], []);
        }
        var focused = FocusedNode();
        var nothing = _selection.Count == 0 && _selectedEdges.Count == 0;
        var targets = (nothing && focused is not null ? [focused] : _selection.ToList())
            .Where(n => !n.IsLocked && graph.Nodes.Contains(n))
            .ToList();
        var edges = _selectedEdges.Where(e => graph.Edges.Contains(e) && !targets.Contains(e.Source) && !targets.Contains(e.Target))
            .ToList();
        if (targets.Count == 0 && edges.Count == 0)
        {
            return ([], []);
        }
        graph.History.Record(WorkflowRemoveChange.Removing(graph, targets, edges));
        var removedEdges = graph.Edges.Where(e => edges.Contains(e) || targets.Contains(e.Source) || targets.Contains(e.Target)).ToList();
        ChangeGraph(() =>
        {
            foreach (var edge in edges)
            {
                graph.Edges.Remove(edge);
            }
        });
        // Focus moves on to the next step in reading order that stays.
        var order = ReadingOrder();
        var after = focused is not null && targets.Contains(focused)
            ? order.Skip(order.IndexOf(focused) + 1).Concat(order.Take(order.IndexOf(focused)).Reverse())
                .FirstOrDefault(n => !targets.Contains(n))
            : null;
        ChangeGraph(() =>
        {
            foreach (var node in targets)
            {
                graph.Nodes.Remove(node);
            }
        });
        if (announce)
        {
            Announce(targets.Count > 0
                ? Count(WorkflowLangResourceKind.AnnounceDeletedOne, WorkflowLangResourceKind.AnnounceDeletedOther, targets.Count)
                : Count(WorkflowLangResourceKind.AnnounceDeletedEdgesOne, WorkflowLangResourceKind.AnnounceDeletedEdgesOther, edges.Count));
        }
        RaiseEvent(new WorkflowEditEventArgs(GraphEditedEvent, WorkflowEditKind.Delete, targets, removedEdges));
        if (after is not null)
        {
            FocusNode(after);
        }
        return (targets, edges);
    }

    /// <summary>
    /// Makes a change a person asked for on the canvas. The cards stay where they are: a workflow the canvas arranged is
    /// not arranged again, as it is when your code changes its edges.
    /// </summary>
    private void ChangeGraph(Action change)
    {
        _changingGraph = true;
        try
        {
            change();
        }
        finally
        {
            _changingGraph = false;
        }
    }

    /// <summary>
    /// Sets the selection; <paramref name="edges"/> null clears the selected edges (selecting steps alone), pass
    /// <see cref="_selectedEdges"/> to keep them.
    /// </summary>
    private void SetSelection(IReadOnlyList<WorkflowNode> next, IReadOnlyList<WorkflowEdge>? edges = null)
    {
        SceneChanged?.Invoke();
        edges ??= [];
        var removed = _selection.Where(n => !next.Contains(n)).ToList();
        var added = next.Where(n => !_selection.Contains(n)).ToList();
        var removedEdges = _selectedEdges.Where(e => !edges.Contains(e)).ToList();
        var addedEdges = edges.Where(e => !_selectedEdges.Contains(e)).ToList();
        if (removed.Count == 0 && added.Count == 0 && removedEdges.Count == 0 && addedEdges.Count == 0)
        {
            return;
        }
        _selection.Clear();
        _selection.AddRange(next);
        _selectedEdges.Clear();
        _selectedEdges.AddRange(edges);
        foreach (var edge in removedEdges.Concat(addedEdges))
        {
            if (_edges.TryGetValue(edge, out var visual))
            {
                visual.Selected = _selectedEdges.Contains(edge);
            }
        }
        _edgeLayer?.InvalidateVisual();
        foreach (var node in removed)
        {
            _cards.GetValueOrDefault(node)?.SetSelected(false);
        }
        foreach (var node in added)
        {
            _cards.GetValueOrDefault(node)?.SetSelected(true);
        }
        RefreshCommands();
        RaiseEvent(new WorkflowSelectionChangedEventArgs(SelectionChangedEvent, added, removed, addedEdges, removedEdges));
        UpdatePorts();
    }

    private void Toggle(WorkflowNode node)
        => SetSelection(_selection.Contains(node) ? _selection.Where(n => n != node).ToList() : [.. _selection, node], _selectedEdges);

    private void Toggle(WorkflowEdge edge)
        => SetSelection(_selection, _selectedEdges.Contains(edge) ? _selectedEdges.Where(e => e != edge).ToList() : [.. _selectedEdges, edge]);

    /// <summary>Drops selected steps that left the workflow (called after the cards are matched to it).</summary>
    private void PruneSelection()
    {
        if (_selection.Any(n => Graph?.Nodes.Contains(n) != true) || _selectedEdges.Any(e => Graph?.Edges.Contains(e) != true))
        {
            SetSelection(_selection.Where(n => Graph?.Nodes.Contains(n) == true).ToList(),
                _selectedEdges.Where(e => Graph?.Edges.Contains(e) == true).ToList());
        }
        foreach (var (edge, visual) in _edges)
        {
            visual.Selected = _selectedEdges.Contains(edge);
        }
        foreach (var node in _dragStart.Keys.Where(n => !_cards.ContainsKey(n)).ToList())
        {
            _dragStart.Remove(node);
        }
    }

    private WorkflowNode? FocusedNode() => _cards.FirstOrDefault(c => c.Value.IsFocused).Key;

    private static string Count(WorkflowLangResourceKind one, WorkflowLangResourceKind other, int count)
        => count == 1 ? WorkflowText.Get(one) : WorkflowText.Format(other, count.ToString("N0", WorkflowText.FormattingCulture));

    // Pointer --------------------------------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_pressPointer is not null || _keyboardMove)
        {
            return;
        }
        var point = e.GetCurrentPoint(this);
        var properties = point.Properties;
        var card = CardOf(e.Source);
        if (properties.IsMiddleButtonPressed)
        {
            if (IsPanZoomEnabled)
            {
                BeginPress(e, Gesture.Pan, null);
            }
            return;
        }
        if (!properties.IsLeftButtonPressed)
        {
            return;
        }
        var modifiers = e.KeyModifiers;
        var world = _view.ScreenToWorld(ViewPoint(e));
        // A port wins over its card: pressing it starts (or picks up) a connection.
        if (TryBeginConnect(e, world))
        {
            return;
        }
        if (card is { Node: { } node })
        {
            card.Focus(NavigationMethod.Pointer);
            // Ctrl / ⌘ toggles on release (it could still become a drag); Shift adds; a plain press on an unselected
            // card selects it alone so the drag moves just that step.
            if (modifiers.HasFlag(KeyModifiers.Shift))
            {
                if (!_selection.Contains(node))
                {
                    SetSelection([.. _selection, node], _selectedEdges);
                }
            }
            else if (!IsCommand(modifiers) && !_selection.Contains(node))
            {
                SetSelection([node]);
            }
            BeginPress(e, Gesture.Pending, node);
        }
        else
        {
            BeginPress(e, Gesture.Pending, null);
            _pressEdge = HitEdge(world);
        }
    }

    private void BeginPress(PointerPressedEventArgs e, Gesture gesture, WorkflowNode? node)
    {
        // A press after a release more than a double-click apart starts counting drags again.
        if (e.ClickCount <= 1)
        {
            _dragsSincePress = 0;
        }
        _pressPointer = e.Pointer;
        _pressPoint = ViewPoint(e);
        _lastPointer = _pressPoint;
        _pressView = _view;
        _pressWorld = _view.ScreenToWorld(_pressPoint);
        _pressNode = node;
        _pressModifiers = e.KeyModifiers;
        _gesture = gesture;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!ReferenceEquals(e.Pointer, _pressPointer))
        {
            UpdateHover(e);
            return;
        }
        var point = ViewPoint(e);
        _lastPointer = point;
        var delta = point - _pressPoint;
        if (_gesture == Gesture.ConnectPending)
        {
            // A connection needs less travel than a move: 3px.
            if (Math.Abs(delta.X) < 3 && Math.Abs(delta.Y) < 3)
            {
                return;
            }
            StartConnect();
            e.Pointer.Capture(this);
        }
        else if (_gesture == Gesture.Pending || (_gesture == Gesture.Pan && !_panning))
        {
            if (Math.Abs(delta.X) < PanThreshold && Math.Abs(delta.Y) < PanThreshold)
            {
                return;
            }
            StartGesture(e);
        }
        switch (_gesture)
        {
            case Gesture.Pan:
                SetViewFromInput(_pressView.PanBy(delta));
                break;
            case Gesture.Move:
                UpdateDrag();
                UpdateAutoPan();
                break;
            case Gesture.Marquee:
                if (_overlay is not null)
                {
                    _overlay.Marquee = new Rect(_pressPoint, point).Normalize();
                }
                UpdateAutoPan();
                break;
            case Gesture.Connect:
                UpdateConnect();
                UpdateAutoPan();
                break;
        }
        e.Handled = true;
    }

    /// <summary>Decides what a press that started moving does: move steps, draw a selection rectangle, or pan.</summary>
    private void StartGesture(PointerEventArgs e)
    {
        var canMove = IsEditable && _pressNode is { IsLocked: false };
        if (_gesture == Gesture.Pending && canMove)
        {
            BeginMove(_selection.Contains(_pressNode!) ? _selection : [_pressNode!]);
            _gesture = Gesture.Move;
            Cursor = new Cursor(StandardCursorType.DragMove);
        }
        else if (_gesture == Gesture.Pending && _pressNode is null && IsEditable && _pressModifiers.HasFlag(KeyModifiers.Shift))
        {
            _gesture = Gesture.Marquee;
        }
        else if (IsPanZoomEnabled)
        {
            _gesture = Gesture.Pan;
            _panning = true;
            Cursor = new Cursor(StandardCursorType.DragMove);
            PseudoClasses.Set(PanningPseudoClass, true);
            StopViewAnimation();
        }
        else
        {
            _gesture = Gesture.None;
            return;
        }
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!ReferenceEquals(e.Pointer, _pressPointer))
        {
            return;
        }
        var gesture = _gesture;
        switch (gesture)
        {
            case Gesture.Pending:
                // A click: Ctrl / ⌘ toggles a card, a plain click selects it alone, a click on the empty canvas clears.
                if (_pressNode is { } node)
                {
                    if (IsCommand(_pressModifiers))
                    {
                        Toggle(node);
                    }
                    else if (!_pressModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        SetSelection([node]);
                    }
                }
                else if (_pressEdge is { } edge)
                {
                    // A click on an edge selects it the same way.
                    if (IsCommand(_pressModifiers))
                    {
                        Toggle(edge);
                    }
                    else if (_pressModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        SetSelection(_selection, _selectedEdges.Contains(edge) ? _selectedEdges : [.. _selectedEdges, edge]);
                    }
                    else
                    {
                        SetSelection([], [edge]);
                    }
                }
                else if (!IsCommand(_pressModifiers) && !_pressModifiers.HasFlag(KeyModifiers.Shift))
                {
                    ClearSelection();
                }
                break;
            case Gesture.ConnectPending:
                EndConnect();
                break;
            case Gesture.Connect:
                FinishConnect();
                e.Handled = true;
                break;
            case Gesture.Move:
                CommitMove(snap: SnapToGrid && !e.KeyModifiers.HasFlag(KeyModifiers.Alt));
                e.Handled = true;
                break;
            case Gesture.Marquee:
                FinishMarquee();
                e.Handled = true;
                break;
            case Gesture.Pan:
                e.Handled = true;
                break;
        }
        EndPan();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (ReferenceEquals(e.Pointer, _pressPointer))
        {
            if (_gesture == Gesture.Move)
            {
                CancelMove();
            }
            else if (_gesture is Gesture.Connect or Gesture.ConnectPending)
            {
                CancelConnect();
            }
            EndPan();
        }
    }

    /// <summary>Ends whatever the pointer was doing: pan, move or selection rectangle.</summary>
    private void EndPan()
    {
        var pointer = _pressPointer;
        _pressPointer = null;
        _pressNode = null;
        _pressEdge = null;
        if (_gesture is not (Gesture.None or Gesture.Pending or Gesture.ConnectPending))
        {
            _dragsSincePress++;
        }
        _gesture = Gesture.None;
        _autoPan?.Stop();
        if (_overlay is not null)
        {
            _overlay.Marquee = null;
        }
        if (_panning)
        {
            _panning = false;
            PseudoClasses.Set(PanningPseudoClass, false);
        }
        ClearValue(CursorProperty);
        if (ReferenceEquals(pointer?.Captured, this))
        {
            pointer!.Capture(null);
        }
    }

    /// <summary>Tracks the step under the pointer (its ports show) and the cursor: a crosshair on a port, a hand on a movable card.</summary>
    private void UpdateHover(PointerEventArgs e)
    {
        if (_pressPointer is not null)
        {
            return;
        }
        var world = _view.ScreenToWorld(ViewPoint(e));
        var margin = PortHitRadius / _view.Zoom;
        var hover = CardOf(e.Source)?.Node ?? _cards.Keys.FirstOrDefault(n => DisplayRect(n).Inflate(margin).Contains(world));
        if (!ReferenceEquals(hover, _hoverNode))
        {
            _hoverNode = hover;
            UpdatePorts();
        }
        if (IsEditable && HitPort(world) is not null)
        {
            Cursor = new Cursor(StandardCursorType.Cross);
        }
        else if (IsEditable && CardOf(e.Source) is { Node.IsLocked: false })
        {
            Cursor = new Cursor(StandardCursorType.Hand);
        }
        else
        {
            ClearValue(CursorProperty);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverNode is not null && _pressPointer is null)
        {
            _hoverNode = null;
            UpdatePorts();
        }
    }

    private static bool IsCommand(KeyModifiers modifiers)
        => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

    // Moving -----------------------------------------------------------------------------------------------------------

    /// <summary>Starts moving <paramref name="nodes"/> (locked ones stay): their cards follow <see cref="_dragOffset"/>.</summary>
    private void BeginMove(IEnumerable<WorkflowNode> nodes)
    {
        _dragStart.Clear();
        _dragOffset = default;
        // Editing takes the view over from fitting, so it holds still while steps move.
        _manualView = true;
        StopViewAnimation();
        foreach (var node in nodes)
        {
            if (!node.IsLocked && _cards.TryGetValue(node, out var card))
            {
                _dragStart[node] = DisplayPosition(node);
                card.SetDragging(true);
            }
        }
        _moveFrom.Clear();
    }

    /// <summary>Follows the pointer: the world point under it now, less the world point it pressed.</summary>
    private void UpdateDrag()
    {
        _dragOffset = _view.ScreenToWorld(_lastPointer) - _pressWorld;
        OnMoveFrame();
    }

    /// <summary>Writes the moved steps' positions (snapped to the grid when asked) and glides the cards there.</summary>
    private void CommitMove(bool snap)
    {
        if (_dragStart.Count == 0)
        {
            return;
        }
        var spacing = _grid?.Spacing ?? 22;
        var from = new Dictionary<WorkflowNode, Point>();
        var moved = new List<WorkflowNode>();
        var before = new List<Point>();
        var after = new List<Point>();
        foreach (var (node, start) in _dragStart)
        {
            var at = start + _dragOffset;
            from[node] = at;
            var to = snap ? new Point(Math.Round(at.X / spacing) * spacing, Math.Round(at.Y / spacing) * spacing) : at;
            if (to != WorkflowWorldPanel.PositionOf(node))
            {
                moved.Add(node);
                before.Add(node.Position);
                after.Add(to);
            }
            node.Position = to;
        }
        if (moved.Count > 0)
        {
            Graph?.History.Record(new WorkflowMoveChange(moved, before, after));
        }
        EndDrag();
        StartMove(from, MotionSpec.Spring(ArcSpring.Snappy));
        OnMoveFrame();
        if (moved.Count > 0)
        {
            Announce(Count(WorkflowLangResourceKind.AnnounceMovedOne, WorkflowLangResourceKind.AnnounceMovedOther, moved.Count));
            RaiseEvent(new WorkflowEditEventArgs(GraphEditedEvent, WorkflowEditKind.Move, moved));
        }
    }

    /// <summary>Puts the cards back where they started (nothing was written to the steps).</summary>
    private void CancelMove()
    {
        if (_dragStart.Count == 0)
        {
            return;
        }
        var from = _dragStart.ToDictionary(p => p.Key, p => p.Value + _dragOffset);
        EndDrag();
        StartMove(from);
        OnMoveFrame();
    }

    private void EndDrag()
    {
        foreach (var node in _dragStart.Keys)
        {
            _cards.GetValueOrDefault(node)?.SetDragging(false);
        }
        _dragStart.Clear();
        _dragOffset = default;
        _keyboardMove = false;
    }

    /// <summary>Scrolls the view while the pointer is near its edge during a move or a selection rectangle.</summary>
    private void UpdateAutoPan()
    {
        if (!IsPanZoomEnabled || AutoPanVelocity() == default)
        {
            _autoPan?.Stop();
            return;
        }
        _autoPan ??= new LiveLoop(this, StepAutoPan) { MinimumLevel = ArcMotionLevel.Off };
        _autoPan.Start();
    }

    /// <summary>How fast the content scrolls (screen px/s): faster the deeper the pointer is into the edge band.</summary>
    private Vector AutoPanVelocity()
    {
        var size = ViewportSize;
        double Axis(double p, double length)
        {
            if (p < AutoPanEdge)
            {
                return AutoPanMaxSpeed * Math.Min(1, (AutoPanEdge - p) / AutoPanEdge);
            }
            if (p > length - AutoPanEdge)
            {
                return -AutoPanMaxSpeed * Math.Min(1, (p - (length - AutoPanEdge)) / AutoPanEdge);
            }
            return 0;
        }
        return new Vector(Axis(_lastPointer.X, size.Width), Axis(_lastPointer.Y, size.Height));
    }

    private bool StepAutoPan(double seconds)
    {
        var velocity = AutoPanVelocity();
        if (velocity == default || _gesture is not (Gesture.Move or Gesture.Marquee or Gesture.Connect))
        {
            return false;
        }
        SetViewFromInput(_view.PanBy(velocity * seconds));
        if (_gesture == Gesture.Move)
        {
            UpdateDrag();
        }
        else if (_gesture == Gesture.Connect)
        {
            UpdateConnect();
        }
        return true;
    }

    private void FinishMarquee()
    {
        if (_overlay?.Marquee is not { } screen)
        {
            return;
        }
        var world = _view.ScreenToWorld(screen);
        var hits = _cards.Keys.Where(n => DisplayRect(n).Intersects(world) && !_selection.Contains(n)).ToList();
        if (hits.Count > 0)
        {
            SetSelection([.. _selection, .. hits], _selectedEdges);
        }
    }

    // Keyboard ---------------------------------------------------------------------------------------------------------

    /// <summary>The editing keys; true when the key was used.</summary>
    private bool HandleEditKey(KeyEventArgs e, WorkflowNode? node)
    {
        var modifiers = e.KeyModifiers;
        if (_keyConnect)
        {
            HandleKeyConnect(e);
            return true;
        }
        if (_gesture == Gesture.Connect && e.Key == Key.Escape)
        {
            CancelConnect();
            EndPan();
            return true;
        }
        if (_gesture == Gesture.Move && e.Key == Key.Escape)
        {
            // Escape during a drag puts the steps back; the rest of the drag is ignored.
            CancelMove();
            EndPan();
            return true;
        }
        if (_keyboardMove)
        {
            var step = (_grid?.Spacing ?? 22) * (modifiers.HasFlag(KeyModifiers.Shift) ? 5 : 1);
            switch (e.Key)
            {
                case Key.Left:
                    NudgeMove(new Vector(-step, 0));
                    return true;
                case Key.Right:
                    NudgeMove(new Vector(step, 0));
                    return true;
                case Key.Up:
                    NudgeMove(new Vector(0, -step));
                    return true;
                case Key.Down:
                    NudgeMove(new Vector(0, step));
                    return true;
                case Key.Enter or Key.M:
                    CommitMove(snap: false);
                    return true;
                case Key.Escape:
                    CancelMove();
                    Announce(WorkflowText.Get(WorkflowLangResourceKind.AnnounceMoveCancelled));
                    return true;
                default:
                    // Other keys wait until the move is placed or cancelled.
                    return true;
            }
        }
        switch (e.Key)
        {
            case Key.Space when node is not null && !IsCommand(modifiers):
                Toggle(node);
                return true;
            case Key.A when IsCommand(modifiers):
                SetSelection(ReadingOrder(), Graph?.Edges.ToList());
                return true;
            case Key.Z when IsCommand(modifiers) && IsEditable:
                if (modifiers.HasFlag(KeyModifiers.Shift))
                {
                    Redo();
                }
                else
                {
                    Undo();
                }
                return true;
            case Key.Y when modifiers.HasFlag(KeyModifiers.Control) && IsEditable:
                Redo();
                return true;
            case Key.C when IsCommand(modifiers):
                _ = CopySelectionAsync();
                return true;
            case Key.X when IsCommand(modifiers) && IsEditable:
                _ = CutSelectionAsync();
                return true;
            case Key.V when IsCommand(modifiers) && IsEditable:
                _ = PasteAsync();
                return true;
            case Key.D when IsCommand(modifiers) && IsEditable:
                DuplicateSelection();
                return true;
            case Key.C when IsEditable && node is not null && !IsCommand(modifiers):
                BeginKeyConnect(node);
                return true;
            case Key.Escape when _selection.Count > 0 || _selectedEdges.Count > 0:
                ClearSelection();
                return true;
            case Key.M when IsEditable && node is not null && !IsCommand(modifiers):
            {
                var targets = _selection.Contains(node) ? _selection.ToList() : [node];
                BeginMove(targets);
                if (_dragStart.Count == 0)
                {
                    return true;
                }
                _keyboardMove = true;
                Announce(WorkflowText.Get(WorkflowLangResourceKind.AnnounceMoveMode));
                return true;
            }
            case Key.Delete or Key.Back when IsEditable && !IsCommand(modifiers):
                DeleteSelection();
                return true;
        }
        return false;
    }

    private void NudgeMove(Vector delta)
    {
        _dragOffset += delta;
        OnMoveFrame();
        if (_dragStart.Keys.FirstOrDefault() is { } first)
        {
            ScrollIntoView(first);
        }
    }
}
