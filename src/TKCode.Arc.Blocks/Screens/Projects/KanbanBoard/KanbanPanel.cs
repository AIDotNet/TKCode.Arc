using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// The Kanban board's single layout panel (internal), the content of the board's one two-axis scroll viewer. It lays
/// out everything: the sticky column head row (arranged at the vertical scroll offset), the swimlane heads (arranged at
/// the horizontal offset, sticky left), the cells, every card, the slot of a dragged card, the Add card rows and the
/// composer. Cards never change parent, so a card moving to another column or lane keeps its visual, its focus and
/// its running flight; every card position goes through <see cref="ProjectsFlip"/>.
/// </summary>
internal sealed class KanbanPanel : Panel
{
    /// <summary>Draw order: cells under cards, lane heads over cards, the sticky head row over everything but a lifted card.</summary>
    private const int CellZ = 0;
    private const int RowZ = 1;
    private const int SlotZ = 1;
    private const int CardZ = 2;
    private const int LaneHeadZ = 5;
    private const int HeadRowZ = 20;
    private const int LiftedZ = 100;

    private readonly ProjectsFlip _flip;
    private readonly Border _headBackground = new() { IsHitTestVisible = true, Focusable = false };
    private readonly List<KanbanColumnHead> _heads = new();
    private readonly List<KanbanLaneHead> _laneHeads = new();
    private readonly List<KanbanCellView> _cells = new();
    private readonly List<KanbanAddRow> _addRows = new();
    private readonly Dictionary<KanbanCard, KanbanCardView> _views = new(ReferenceEqualityComparer.Instance);
    private readonly List<List<Control>> _items = new();
    private readonly List<List<double>> _midlines = new();
    private readonly KanbanSlotView _slot = new();
    private readonly KanbanComposer _composer = new();
    private KanbanBoardLayout? _layout;
    private Rect _slotRect;
    private bool _slotJump;

    public KanbanPanel()
    {
        _flip = new ProjectsFlip(this);
        Background = Avalonia.Media.Brushes.Transparent;
        ClipToBounds = false;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Local);
        _headBackground.ZIndex = HeadRowZ;
        _slot.ZIndex = SlotZ;
        _slot.IsVisible = false;
        _composer.ZIndex = CardZ;
        Children.Add(_headBackground);
        Children.Add(_slot);
        Children.Add(_composer);
        // The sticky row is opaque: cards slide under it.
        ArcTokens.Bind(_headBackground, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
    }

    public KanbanBoard? Board { get; set; }

    public ProjectsFlip Flip => _flip;

    public KanbanSlotView Slot => _slot;

    public KanbanComposer Composer => _composer;

    public KanbanBoardLayout? CurrentLayout => _layout;

    public IReadOnlyList<KanbanColumnHead> Heads => _heads;

    public IReadOnlyList<KanbanLaneHead> LaneHeads => _laneHeads;

    public IReadOnlyList<KanbanCellView> Cells => _cells;

    public IReadOnlyList<KanbanAddRow> AddRows => _addRows;

    /// <summary>The slot's rect (board coordinates) after the last arrange.</summary>
    public Rect SlotRect => _slotRect;

    public KanbanCardView? ViewOf(KanbanCard card) => _views.TryGetValue(card, out var view) ? view : null;

    public IEnumerable<KanbanCardView> CardViews => _views.Values;

    /// <summary>The next arrange jumps the slot to its new cell (no glide) and fades it in.</summary>
    public void JumpSlot() => _slotJump = true;

    // Children ---------------------------------------------------------------------------------------------------------

    /// <summary>Matches the children to the board's grid: heads, lane heads, cells, rows and one view per placed card.</summary>
    public void Sync()
    {
        var board = Board;
        var grid = board?.Grid;
        if (board is null || grid is null)
        {
            return;
        }
        var columns = grid.Columns.Count;
        SyncList(_heads, columns, i =>
        {
            var head = new KanbanColumnHead(i) { ZIndex = HeadRowZ + 1 };
            head.AddRequested += OnHeadAdd;
            return head;
        }, head => head.AddRequested -= OnHeadAdd);
        var laneHeads = grid.HasLaneHeads ? grid.Lanes.Count : 0;
        SyncList(_laneHeads, laneHeads, _ => new KanbanLaneHead { ZIndex = LaneHeadZ }, null);
        SyncList(_cells, grid.Lanes.Count * columns, _ => new KanbanCellView { ZIndex = CellZ }, null);
        var rows = grid.HasLaneHeads || !board.CanQuickAdd ? 0 : columns;
        SyncList(_addRows, rows, i =>
        {
            var row = new KanbanAddRow(i) { ZIndex = RowZ };
            row.Click += OnAddRowClick;
            return row;
        }, row => row.Click -= OnAddRowClick);
        for (var i = 0; i < _heads.Count; i++)
        {
            _heads[i].Index = i;
        }
        for (var i = 0; i < _addRows.Count; i++)
        {
            _addRows[i].Column = i;
        }

        var placed = new HashSet<KanbanCard>(grid.Placed, ReferenceEqualityComparer.Instance);
        foreach (var gone in _views.Keys.Where(c => !placed.Contains(c)).ToList())
        {
            var view = _views[gone];
            _views.Remove(gone);
            view.Detach();
            _flip.Forget(view);
            Children.Remove(view);
        }
        foreach (var card in grid.Placed)
        {
            if (!_views.ContainsKey(card))
            {
                var view = new KanbanCardView(card, board) { ZIndex = CardZ };
                _views[card] = view;
                Children.Add(view);
            }
        }
        RefreshTexts();
        InvalidateMeasure();
    }

    private void SyncList<T>(List<T> list, int count, Func<int, T> create, Action<T>? release) where T : Control
    {
        while (list.Count > count)
        {
            var last = list[^1];
            release?.Invoke(last);
            Children.Remove(last);
            list.RemoveAt(list.Count - 1);
        }
        while (list.Count < count)
        {
            var item = create(list.Count);
            list.Add(item);
            Children.Add(item);
        }
    }

    private void OnHeadAdd(KanbanColumnHead head)
    {
        if (Board?.Grid is { } grid && head.Index < grid.Columns.Count)
        {
            Board.BeginQuickAdd(grid.Columns[head.Index], null, head.AddButton);
        }
    }

    private void OnAddRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is KanbanAddRow row && Board?.Grid is { } grid && row.Column < grid.Columns.Count)
        {
            e.Handled = true;
            Board.BeginQuickAdd(grid.Columns[row.Column], null, row);
        }
    }

    /// <summary>Re-reads every text (names, counts, accessible names) from the board's data and strings.</summary>
    public void RefreshTexts()
    {
        var board = Board;
        var grid = board?.Grid;
        if (board is null || grid is null)
        {
            return;
        }
        var strings = board.Strings;
        for (var i = 0; i < _heads.Count && i < grid.Columns.Count; i++)
        {
            var column = grid.Columns[i];
            var head = _heads[i];
            head.ColumnName = column.Name;
            head.ChipText = strings.OverLimit;
            head.CanAdd = board.CanQuickAdd;
        }
        for (var i = 0; i < _addRows.Count && i < grid.Columns.Count; i++)
        {
            _addRows[i].Content = strings.AddCard;
            AutomationProperties.SetName(_addRows[i], strings.AddCardTo(grid.Columns[i].Name));
        }
        foreach (var view in _views.Values)
        {
            view.Refresh();
        }
        _composer.SubmitText = strings.AddCard;
        _composer.CancelText = strings.Cancel;
        _composer.Placeholder = strings.ComposerPlaceholder;
        board.UpdateLiveCounts();
        ApplyTabOrder();
    }

    /// <summary>
    /// Tab order as Arc's markup, independent of the draw order: the heads' "+" buttons, then column by column (lane by
    /// lane in swimlanes) every card top to bottom followed by the column's Add card row or the open composer.
    /// </summary>
    public void ApplyTabOrder()
    {
        var grid = Board?.Grid;
        if (grid is null)
        {
            return;
        }
        // Tab indices are compared between the tab stops themselves (the buttons), not their containers.
        for (var i = 0; i < _heads.Count; i++)
        {
            if (_heads[i].AddButton is { } add)
            {
                KeyboardNavigation.SetTabIndex(add, i);
            }
        }
        var columns = grid.Columns.Count;
        for (var lane = 0; lane < grid.Lanes.Count; lane++)
        {
            for (var column = 0; column < columns; column++)
            {
                var cell = lane * columns + column;
                var baseIndex = 1000 + cell * 10000;
                var items = cell < _items.Count ? _items[cell] : null;
                var n = 0;
                if (items is not null)
                {
                    foreach (var item in items)
                    {
                        if (item is KanbanCardView view)
                        {
                            KeyboardNavigation.SetTabIndex(view, baseIndex + n++);
                        }
                    }
                }
                else
                {
                    foreach (var card in grid.Cell(lane, column))
                    {
                        if (ViewOf(card) is { } view)
                        {
                            KeyboardNavigation.SetTabIndex(view, baseIndex + n++);
                        }
                    }
                }
                if (lane == 0 && column < _addRows.Count)
                {
                    KeyboardNavigation.SetTabIndex(_addRows[column], baseIndex + 9999);
                }
                if (_composer.Cell == (lane, column))
                {
                    _composer.SetTabIndices(baseIndex + 9996);
                }
            }
        }
    }

    // Layout -----------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var board = Board;
        var grid = board?.Grid;
        if (board is null || grid is null)
        {
            _layout = null;
            return default;
        }
        var viewport = board.Viewport;
        if (!(viewport.Width > 0))
        {
            viewport = new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 1200,
                double.IsFinite(availableSize.Height) ? availableSize.Height : 800);
        }
        var columns = grid.Columns.Count;
        var metrics = KanbanLayout.Measure(viewport.Width, columns, board.MinColumnWidth, board.CompactColumnWidth, board.CompactWidth);
        var cardWidth = metrics.CardWidth;
        _headBackground.Measure(new Size(metrics.ExtentWidth, KanbanLayout.HeadRowHeight));
        foreach (var head in _heads)
        {
            head.Measure(new Size(metrics.ColumnWidth, KanbanLayout.ColumnHeadHeight));
        }
        foreach (var laneHead in _laneHeads)
        {
            laneHead.Measure(new Size(double.PositiveInfinity, KanbanLayout.LaneHeadHeight));
        }
        UpdateAddRowVisibility();
        foreach (var row in _addRows)
        {
            row.Measure(new Size(cardWidth, KanbanLayout.AddRowHeight));
        }
        _composer.Measure(new Size(cardWidth, double.PositiveInfinity));
        foreach (var view in _views.Values)
        {
            view.Measure(new Size(cardWidth, double.PositiveInfinity));
        }
        foreach (var cell in _cells)
        {
            cell.Measure(new Size(metrics.ColumnWidth, double.PositiveInfinity));
        }

        var session = board.Session;
        var lifted = session is { Mode: KanbanDragMode.Pointer or KanbanDragMode.Keyboard } ? session : null;
        var slotHeight = session?.SlotHeight ?? 0;
        if (session is not null)
        {
            _slot.Measure(new Size(cardWidth, slotHeight));
        }
        var inputs = new KanbanCellInput[grid.Lanes.Count * columns];
        while (_items.Count < inputs.Length)
        {
            _items.Add(new List<Control>());
            _midlines.Add(new List<double>());
        }
        if (_items.Count > inputs.Length)
        {
            // Cells that are gone (swimlanes back to Status) would keep the views of deleted cards alive.
            _items.RemoveRange(inputs.Length, _items.Count - inputs.Length);
            _midlines.RemoveRange(inputs.Length, _midlines.Count - inputs.Length);
        }
        var composerCell = _composer.IsVisible ? _composer.Cell : (-1, -1);
        for (var lane = 0; lane < grid.Lanes.Count; lane++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = lane * columns + column;
                var items = _items[index];
                items.Clear();
                foreach (var card in grid.Cell(lane, column))
                {
                    if (lifted is not null && ReferenceEquals(card, lifted.Card))
                    {
                        continue;
                    }
                    if (ViewOf(card) is { } view)
                    {
                        items.Add(view);
                    }
                }
                if (lifted is not null && lifted.TargetLane == lane && lifted.TargetColumn == column)
                {
                    var at = Math.Clamp(lifted.TargetIndex, 0, items.Count);
                    items.Insert(at, lifted.Mode == KanbanDragMode.Keyboard ? lifted.View : _slot);
                }
                var heights = new double[items.Count];
                for (var i = 0; i < items.Count; i++)
                {
                    heights[i] = ReferenceEquals(items[i], _slot) ? slotHeight : items[i].DesiredSize.Height;
                }
                double tail = double.NaN;
                if (composerCell == (lane, column))
                {
                    tail = board.ComposerHeight;
                }
                else if (!grid.HasLaneHeads && board.CanQuickAdd && column < _addRows.Count)
                {
                    tail = KanbanLayout.AddRowHeight;
                }
                inputs[index] = new KanbanCellInput(heights, tail);
            }
        }
        _layout = KanbanLayout.Layout(metrics, viewport, grid.Lanes.Count, grid.HasLaneHeads, inputs);
        // Midlines of the other cards in each cell, for the pointer's insertion index.
        for (var i = 0; i < inputs.Length; i++)
        {
            var mids = _midlines[i];
            mids.Clear();
            var rect = _layout.Cells[i];
            var items = _items[i];
            for (var k = 0; k < items.Count; k++)
            {
                if (items[k] is KanbanCardView)
                {
                    mids.Add(rect.ItemTops[k] + rect.ItemHeights[k] / 2);
                }
            }
        }
        ApplyTabOrder();
        return _layout.Extent;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var board = Board;
        var grid = board?.Grid;
        var layout = _layout;
        if (board is null || grid is null || layout is null)
        {
            return finalSize;
        }
        var metrics = layout.Metrics;
        var offset = board.ScrollOffset;
        var width = Math.Max(finalSize.Width, layout.Extent.Width);
        // Sticky head row: always at the top of the viewport, the cards slide under its opaque background.
        _headBackground.Arrange(new Rect(0, offset.Y, width, KanbanLayout.HeadRowHeight));
        for (var i = 0; i < _heads.Count; i++)
        {
            _heads[i].Arrange(new Rect(metrics.ColumnX(i), offset.Y + KanbanLayout.HeadRowTopPadding, metrics.ColumnWidth,
                KanbanLayout.ColumnHeadHeight));
        }
        // Sticky lane heads: they stop at the scroller's left padding while the board scrolls sideways.
        for (var i = 0; i < _laneHeads.Count && i < layout.Lanes.Length; i++)
        {
            var head = _laneHeads[i];
            var lane = layout.Lanes[i];
            head.Arrange(new Rect(metrics.Padding + Math.Max(0, offset.X), lane.Top, head.DesiredSize.Width, KanbanLayout.LaneHeadHeight));
        }
        for (var i = 0; i < _cells.Count && i < layout.Cells.Length; i++)
        {
            _cells[i].Arrange(layout.Cells[i].Bounds);
        }

        var session = board.Session;
        var cardWidth = metrics.CardWidth;
        var slotPlaced = false;
        for (var i = 0; i < layout.Cells.Length && i < _items.Count; i++)
        {
            var cell = layout.Cells[i];
            var items = _items[i];
            for (var k = 0; k < items.Count && k < cell.ItemTops.Length; k++)
            {
                var rect = new Rect(cell.ItemX, cell.ItemTops[k], cardWidth, cell.ItemHeights[k]);
                var item = items[k];
                if (ReferenceEquals(item, _slot))
                {
                    PlaceSlot(rect);
                    slotPlaced = true;
                }
                else if (item is KanbanCardView view)
                {
                    view.Arrange(rect);
                    if (session is { Mode: KanbanDragMode.Landing } landing && ReferenceEquals(view, landing.View))
                    {
                        // The slot stays under the landing card until it arrives; it is placed first so the card
                        // chases the slot's flight of this very pass.
                        PlaceSlot(rect);
                        slotPlaced = true;
                        board.PlaceLanding(view, rect);
                    }
                    else
                    {
                        _flip.Place(view, rect);
                    }
                }
            }
            if (cell.Tail is { } tail)
            {
                if (_composer.IsVisible && _composer.Cell == (cell.Lane, cell.Column))
                {
                    _composer.Arrange(tail);
                    if (board.IsComposerClosing && cell.Lane == 0 && cell.Column < _addRows.Count && !grid.HasLaneHeads)
                    {
                        // The Add card row is back at once and rides up under the collapsing composer, its gap closing
                        // with it (Arc closes the last 8px in a second spring; one motion reads better).
                        var full = Math.Max(1, _composer.DesiredSize.Height);
                        var gap = KanbanLayout.ItemGap * Math.Clamp(tail.Height / full, 0, 1);
                        _addRows[cell.Column].Arrange(new Rect(tail.X, tail.Bottom + gap, tail.Width, KanbanLayout.AddRowHeight));
                    }
                }
                else if (cell.Lane == 0 && cell.Column < _addRows.Count)
                {
                    _addRows[cell.Column].Arrange(tail);
                }
            }
        }
        if (session is { Mode: KanbanDragMode.Pointer } pointer)
        {
            // The lifted card stays at the rect it was lifted from; its offset follows the pointer.
            pointer.View.Arrange(pointer.OriginRect);
            board.PlacePointerCard(pointer);
        }
        if (!slotPlaced)
        {
            _slot.Arrange(new Rect(_slotRect.Position, _slot.DesiredSize));
        }
        if (!_composer.IsVisible)
        {
            _composer.Arrange(default);
        }
        // Flights that this pass started moved the lifted card: its copy above the board follows.
        board.SyncProxy();
        return new Size(width, Math.Max(finalSize.Height, layout.Extent.Height));
    }

    private void PlaceSlot(Rect rect)
    {
        _slot.Arrange(rect);
        if (_slotJump)
        {
            // A new cell: the old slot is gone at once and the new one fades in where it lands (Arc: no exit, Smooth fade).
            _slotJump = false;
            var motion = _flip.Get(_slot);
            motion.Layout = rect;
            motion.Offset.Jump(default);
            motion.Opacity.Jump(0);
            _ = motion.Opacity.AnimateTo(1, Board is { } board && Motion.ArcMotion.GetEffectiveLevel(board) == Motion.ArcMotionLevel.Full
                ? KanbanMotion.Layout
                : ProjectsSprings.FadeInstant());
        }
        else
        {
            _flip.Place(_slot, rect);
        }
        _slotRect = rect;
    }

    /// <summary>Hides the Add card row of the column the composer is open in (it comes back as soon as it closes).</summary>
    public void UpdateAddRowVisibility()
    {
        var open = Board is { IsComposerOpen: true } && Board.Grid is { HasLaneHeads: false };
        for (var i = 0; i < _addRows.Count; i++)
        {
            _addRows[i].IsVisible = !(open && _composer.Cell.Column == i);
        }
    }

    /// <summary>Raises the draw order of a lifted or landing card above the sticky heads, or puts it back.</summary>
    public void SetLifted(KanbanCardView view, bool lifted) => view.ZIndex = lifted ? LiftedZ : CardZ;

    // Hit testing --------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The cell and position under board point <paramref name="point"/>: the column by x (gap midpoints), the lane by y
    /// (heads are dead zones that keep <paramref name="currentLane"/>), the position by the other cards' middles.
    /// </summary>
    public bool TryHit(Point point, int currentLane, out int lane, out int column, out int index)
    {
        lane = column = index = -1;
        var layout = _layout;
        if (layout is null || layout.Metrics.ColumnCount == 0 || layout.Lanes.Length == 0)
        {
            return false;
        }
        column = KanbanLayout.ColumnAt(layout.Metrics, point.X);
        lane = KanbanLayout.LaneAt(layout.Lanes, point.Y, currentLane);
        var cell = lane * layout.Metrics.ColumnCount + column;
        if (cell < 0 || cell >= _midlines.Count)
        {
            return false;
        }
        index = KanbanLayout.IndexAt(_midlines[cell], point.Y);
        return true;
    }

    /// <summary>The rect of the card view <paramref name="view"/> as drawn now (layout position plus flight offset).</summary>
    public Rect VisualRect(KanbanCardView view)
    {
        if (_flip.TryGet(view, out var motion) && motion.Layout is { } layout)
        {
            return new Rect(motion.VisualTopLeft, layout.Size);
        }
        return view.Bounds;
    }

    /// <summary>The cell rect of lane <paramref name="lane"/>, column <paramref name="column"/>, or null.</summary>
    public Rect? CellRect(int lane, int column)
    {
        var layout = _layout;
        if (layout is null || column < 0 || lane < 0 || column >= layout.Metrics.ColumnCount || lane >= layout.Lanes.Length)
        {
            return null;
        }
        return layout.Cell(lane, column).Bounds;
    }

    public KanbanCellView? CellView(int lane, int column)
    {
        var columns = _heads.Count;
        var index = lane * columns + column;
        return lane >= 0 && column >= 0 && index < _cells.Count ? _cells[index] : null;
    }
}
