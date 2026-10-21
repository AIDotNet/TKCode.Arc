using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What a point of the body is over.</summary>
internal readonly record struct DataGridHit(int Line, int Row, int Column, bool Gutter, DataGridGroupNode? Group, bool FillHandle);

/// <summary>
/// The scrolled content of a <see cref="DataGrid"/> (an <see cref="ILogicalScrollable"/> inside the template's
/// ScrollViewer): it keeps the header, filter and totals rows stuck to the top and bottom and the gutter and pinned
/// columns stuck to the left, draws the body with two self-drawn layers (pinned and scrolling columns) plus the selection
/// layer, hosts the single cell editor, and turns pointer input into selection, fill and auto-scroll.
/// </summary>
internal sealed class DataGridViewport : Control, ILogicalScrollable
{
    private const int OverscanLines = 6;

    private readonly DataGrid _grid;
    private readonly DataGridCellsLayer _pinnedCells;
    private readonly DataGridCellsLayer _scrollCells;
    private readonly DataGridSelectionLayer _selection;
    private readonly DataGridEditorLayer _editorLayer;
    private readonly DataGridHeaderRow _header;
    private readonly DataGridFilterRow _filterRow;
    private readonly DataGridTotalsRow _totals;
    private readonly DataGridEmptyState _empty;
    private EventHandler? _scrollInvalidated;
    private Vector _offset;
    private Size _extent;
    private Size _viewportSize;
    private MotionValue<double>? _filterOpen;
    private MotionValue<double>? _slide;
    private MotionValue<double>? _arrive;
    private Dictionary<object, double> _slideFrom = new();
    private HashSet<object> _arriving = new();
    private LiveLoop? _autoScroll;
    private DragState? _drag;
    private PendingTap? _tap;
    private Point _lastPointer;
    private int _hoverRow = -1;
    private bool _touchMode;
    private double _lastRowHeight;
    private bool _overHandle;
    private static Cursor? s_crossCursor;

    private enum DragMode
    {
        Select,
        Rows,
        Fill
    }

    private sealed class DragState
    {
        public required DragMode Mode { get; init; }

        public required DataGridCellRange Source { get; init; }

        public required IPointer Pointer { get; init; }

        public DataGridCellRange? Preview { get; set; }
    }

    private sealed record PendingTap(IPointer Pointer, DataGridHit Hit, Point Start);

    public DataGridViewport(DataGrid grid)
    {
        _grid = grid;
        Focusable = true;
        FocusAdorner = null;
        ClipToBounds = true;
        _scrollCells = new DataGridCellsLayer(grid, this, pinned: false);
        _pinnedCells = new DataGridCellsLayer(grid, this, pinned: true);
        _selection = new DataGridSelectionLayer(grid, this);
        _editorLayer = new DataGridEditorLayer(grid, this);
        _empty = new DataGridEmptyState(grid);
        _totals = new DataGridTotalsRow(grid, this);
        _filterRow = new DataGridFilterRow(grid, this);
        _header = new DataGridHeaderRow(grid, this);
        foreach (var child in new Control[] { _scrollCells, _pinnedCells, _selection, _editorLayer, _empty, _totals, _filterRow, _header })
        {
            LogicalChildren.Add(child);
            VisualChildren.Add(child);
        }
    }

    internal DataGridHeaderRow Header => _header;

    internal DataGridFilterRow FilterRow => _filterRow;

    internal DataGridTotalsRow TotalsRow => _totals;

    internal DataGridSelectionLayer SelectionLayer => _selection;

    internal DataGridEditorLayer EditorLayer => _editorLayer;

    internal DataGridCellsLayer PinnedCells => _pinnedCells;

    internal DataGridCellsLayer ScrollCells => _scrollCells;

    internal DataGridEmptyState EmptyState => _empty;

    /// <summary>Width the column geometry is computed for.</summary>
    internal double LayoutWidth { get; private set; } = double.NaN;

    internal double HeaderHeight => _grid.HeaderRowHeight;

    internal double FilterHeight => _grid.FilterRowHeight * FilterProgress;

    internal double FilterProgress => _filterOpen?.Value ?? (_grid.IsFilterRowOpen ? 1 : 0);

    internal double TotalsHeight => _grid.ShowTotals ? _grid.TotalsRowHeight : 0;

    internal double BodyTop => HeaderHeight + FilterHeight;

    internal double BodyHeight => Math.Max(0, Bounds.Height - BodyTop - TotalsHeight);

    internal Vector Offset => _offset;

    /// <summary>Whether the last pointer was a touch (checkboxes and column menu buttons stay visible).</summary>
    internal bool TouchMode => _touchMode;

    internal int HoverRow => _hoverRow;

    #region ILogicalScrollable

    bool ILogicalScrollable.CanHorizontallyScroll { get; set; }

    bool ILogicalScrollable.CanVerticallyScroll { get; set; }

    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    Size ILogicalScrollable.ScrollSize => new(16, Math.Max(1, _grid.RowHeight));

    Size ILogicalScrollable.PageScrollSize => new(Math.Max(16, _viewportSize.Width - _grid.Layout.PinEdge), Math.Max(_grid.RowHeight, BodyHeight - _grid.RowHeight));

    Size IScrollable.Extent => _extent;

    Size IScrollable.Viewport => _viewportSize;

    bool IScrollable.CanHorizontallyScroll => ((ILogicalScrollable)this).CanHorizontallyScroll;

    bool IScrollable.CanVerticallyScroll => ((ILogicalScrollable)this).CanVerticallyScroll;

    Vector IScrollable.Offset
    {
        get => _offset;
        set => SetOffset(value);
    }

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => _scrollInvalidated += value;
        remove => _scrollInvalidated -= value;
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect) => false;

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) => null;

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e) => _scrollInvalidated?.Invoke(this, e);

    private Vector ClampOffset(Vector value) => new(
        Math.Clamp(value.X, 0, Math.Max(0, _extent.Width - _viewportSize.Width)),
        Math.Clamp(value.Y, 0, Math.Max(0, _extent.Height - _viewportSize.Height)));

    private void SetOffset(Vector value)
    {
        var clamped = ClampOffset(value);
        if (clamped == _offset)
        {
            return;
        }
        var horizontal = Math.Abs(clamped.X - _offset.X) > 0.001;
        _offset = clamped;
        OnScrolled(horizontal);
    }

    private void OnScrolled(bool horizontal)
    {
        _header.OnScrolled();
        _filterRow.OnScrolled();
        _totals.OnScrolled();
        _pinnedCells.InvalidateVisual();
        _scrollCells.InvalidateVisual();
        _selection.InvalidateVisual();
        _editorLayer.InvalidateArrange();
        _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        RequestRows();
        if (horizontal)
        {
            _grid.SetScrolledX(_offset.X > 0.5);
        }
    }

    /// <summary>Scrolls by a delta (auto-scroll while dragging).</summary>
    private void ScrollBy(double dx, double dy) => SetOffset(_offset + new Vector(dx, dy));

    #endregion

    #region Layout

    /// <summary>Height of the body content (rows, placeholders or the empty state).</summary>
    private double BodyContentHeight()
    {
        var rowHeight = _grid.RowHeight;
        if (_grid.IsShowingSkeleton)
        {
            return 6 * rowHeight;
        }
        var lines = _grid.LineCount;
        return lines > 0 ? lines * rowHeight : 176;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _grid.EnsureView();
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : _grid.LayoutFor(double.PositiveInfinity).Total;
        var layout = _grid.LayoutFor(width);
        var content = HeaderHeight + FilterHeight + BodyContentHeight() + TotalsHeight;
        var height = Math.Min(content, double.IsFinite(availableSize.Height) ? availableSize.Height : content);
        _header.Measure(new Size(width, HeaderHeight));
        _filterRow.Measure(new Size(width, _grid.FilterRowHeight));
        _totals.Measure(new Size(width, _grid.TotalsRowHeight));
        _empty.Measure(new Size(width, 176));
        _editorLayer.Measure(new Size(width, height));
        foreach (var layer in new Control[] { _scrollCells, _pinnedCells, _selection })
        {
            layer.Measure(new Size(width, height));
        }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : layout.Total, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        LayoutWidth = finalSize.Width;
        var layout = _grid.LayoutFor(finalSize.Width);
        var rowHeight = _grid.RowHeight;
        if (_lastRowHeight > 0 && Math.Abs(_lastRowHeight - rowHeight) > 0.01)
        {
            _offset = new Vector(_offset.X, Math.Round(_offset.Y * rowHeight / _lastRowHeight));
        }
        _lastRowHeight = rowHeight;
        var extent = new Size(Math.Max(layout.Total, finalSize.Width),
            HeaderHeight + FilterHeight + BodyContentHeight() + TotalsHeight);
        var changed = extent != _extent || finalSize != _viewportSize;
        _extent = extent;
        _viewportSize = finalSize;
        var clamped = ClampOffset(_offset);
        var moved = clamped != _offset;
        _offset = clamped;
        var width = finalSize.Width;
        var bodyTop = BodyTop;
        var bodyHeight = Math.Max(0, finalSize.Height - bodyTop - TotalsHeight);
        var pinEdge = Math.Min(layout.PinEdge, width);
        _pinnedCells.Arrange(new Rect(0, bodyTop, pinEdge, bodyHeight));
        _scrollCells.Arrange(new Rect(pinEdge, bodyTop, Math.Max(0, width - pinEdge), bodyHeight));
        _selection.Arrange(new Rect(0, bodyTop, width, bodyHeight));
        _editorLayer.Arrange(new Rect(0, bodyTop, width, bodyHeight));
        _empty.Arrange(new Rect(0, bodyTop, width, bodyHeight));
        _header.Arrange(new Rect(0, 0, width, HeaderHeight));
        _filterRow.Arrange(new Rect(0, HeaderHeight, width, _grid.FilterRowHeight));
        _filterRow.Clip = FilterProgress >= 0.999 ? null : new RectangleGeometry(new Rect(0, 0, width, FilterHeight));
        _filterRow.IsVisible = FilterProgress > 0.001;
        _totals.IsVisible = _grid.ShowTotals;
        _totals.Arrange(new Rect(0, Math.Max(bodyTop, finalSize.Height - TotalsHeight), width, _grid.TotalsRowHeight));
        if (changed || moved)
        {
            _scrollInvalidated?.Invoke(this, EventArgs.Empty);
            _header.OnScrolled();
            _filterRow.OnScrolled();
            _totals.OnScrolled();
        }
        RequestRows();
        return finalSize;
    }

    internal void InvalidateLayers()
    {
        _pinnedCells.InvalidateVisual();
        _scrollCells.InvalidateVisual();
        _selection.InvalidateVisual();
    }

    internal void InvalidateText()
    {
        _pinnedCells.ClearText();
        _scrollCells.ClearText();
        InvalidateLayers();
        _header.Refresh();
        _filterRow.Refresh();
        _totals.Refresh();
    }

    internal void UpdateHeaders() => _header.Refresh();

    internal void UpdateTotals() => _totals.Refresh();

    internal void UpdateSelectAll() => _header.UpdateSelectAll();

    internal void UpdateEmptyState() => _empty.Update();

    internal void UpdateTexts()
    {
        _header.Refresh();
        _filterRow.Refresh();
        _totals.Refresh();
        _empty.Update();
        _editorLayer.UpdateTexts();
    }

    internal void OnDensityChanged()
    {
        InvalidateMeasure();
        InvalidateText();
        SnapSelection();
    }

    internal void OnFilterRowToggled(bool animate)
    {
        var target = _grid.IsFilterRowOpen ? 1.0 : 0.0;
        _filterOpen ??= CreateValue(target == 1 ? 0 : 1, () =>
        {
            InvalidateMeasure();
            InvalidateArrange();
        });
        _filterRow.Refresh();
        if (!animate || !_grid.IsMotionFull)
        {
            _filterOpen.Jump(target);
        }
        else
        {
            _ = _filterOpen.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
        InvalidateMeasure();
    }

    private MotionValue<double> CreateValue(double initial, Action changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => changed();
        return value;
    }

    #endregion

    #region Lines and rows in view

    /// <summary>First and last line to draw (the visible ones plus a few above and below).</summary>
    internal (int First, int Last) LineWindow()
    {
        var rowHeight = Math.Max(1, _grid.RowHeight);
        var count = _grid.LineCount;
        if (count == 0)
        {
            return (0, -1);
        }
        var first = (int)Math.Floor(_offset.Y / rowHeight) - OverscanLines;
        var last = (int)Math.Ceiling((_offset.Y + BodyHeight) / rowHeight) + OverscanLines;
        return (Math.Max(0, first), Math.Min(count - 1, last));
    }

    private void RequestRows()
    {
        if (!_grid.HasDataSource)
        {
            return;
        }
        var (first, last) = LineWindow();
        _grid.RequestRows(first, Math.Max(first, last));
    }

    /// <summary>Vertical offset of a line from its slot while rows slide after a sort.</summary>
    internal double SlideOffset(object? key)
    {
        if (key is null || _slide is not { } slide || slide.Value <= 0.0001)
        {
            return 0;
        }
        return _slideFrom.TryGetValue(key, out var from) ? from * slide.Value : 0;
    }

    /// <summary>Opacity of a row arriving from outside the view after a sort.</summary>
    internal double ArriveOpacity(object? key) =>
        key is not null && _arrive is { } arrive && _arriving.Contains(key) ? Math.Clamp(arrive.Value, 0, 1) : 1;

    internal bool IsSliding => _slide is { IsAnimating: true } || _arrive is { IsAnimating: true };

    /// <summary>Where every row in view is drawn now (content y), before the order changes.</summary>
    internal Dictionary<object, double>? CaptureRowPositions()
    {
        if (!IsAttachedToTree || _grid.HasDataSource || !_grid.IsMotionFull || _grid.LineCount == 0)
        {
            return null;
        }
        var positions = new Dictionary<object, double>();
        var (first, last) = LineWindow();
        var rowHeight = _grid.RowHeight;
        for (var line = first; line <= last; line++)
        {
            if (_grid.GroupAtLine(line) is not null)
            {
                continue;
            }
            var row = _grid.RowOfLine(line);
            if (_grid.KeyOfRow(row) is { } key)
            {
                positions[key] = line * rowHeight + SlideOffset(key);
            }
        }
        return positions;
    }

    private bool IsAttachedToTree => TopLevel.GetTopLevel(this) is not null;

    /// <summary>Slides rows from where they were drawn to their new slots; rows new to the view fade in.</summary>
    internal void StartSlide(Dictionary<object, double> before)
    {
        _slideFrom = new Dictionary<object, double>();
        _arriving = new HashSet<object>();
        var (first, last) = LineWindow();
        var rowHeight = _grid.RowHeight;
        for (var line = first; line <= last; line++)
        {
            if (_grid.GroupAtLine(line) is not null)
            {
                continue;
            }
            var row = _grid.RowOfLine(line);
            if (_grid.KeyOfRow(row) is not { } key)
            {
                continue;
            }
            if (before.TryGetValue(key, out var y))
            {
                var delta = y - line * rowHeight;
                if (Math.Abs(delta) > 0.5)
                {
                    _slideFrom[key] = delta;
                }
            }
            else
            {
                _arriving.Add(key);
            }
        }
        _slide ??= CreateValue(0, InvalidateLayers);
        _arrive ??= CreateValue(1, InvalidateLayers);
        _slide.Jump(1);
        _ = _slide.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        if (_arriving.Count > 0)
        {
            _arrive.Jump(0);
            _ = _arrive.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
    }

    #endregion

    #region Geometry helpers

    /// <summary>The body rectangle of a range in content coordinates, with whether its left and right columns are pinned.</summary>
    internal (Rect Rect, bool LeftPinned, bool RightPinned) ContentRect(DataGridCellRange range)
    {
        var layout = _grid.Layout;
        var rowHeight = _grid.RowHeight;
        if (layout.Count == 0)
        {
            return (default, false, false);
        }
        var c0 = Math.Clamp(range.Column0, 0, layout.Count - 1);
        var c1 = Math.Clamp(range.Column1, 0, layout.Count - 1);
        var top = _grid.LineOfRow(range.Row0) * rowHeight;
        var bottom = (_grid.LineOfRow(range.Row1) + 1) * rowHeight;
        var left = layout.Lefts[c0];
        var right = layout.Right(c1);
        return (new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)), c0 < layout.PinCount, c1 < layout.PinCount);
    }

    /// <summary>Maps a content rectangle into body coordinates (pinned edges stay, scrolling edges move with the offset).</summary>
    internal Rect BodyRect(Rect content, bool leftPinned, bool rightPinned)
    {
        var pinEdge = _grid.Layout.PinEdge;
        var left = leftPinned ? content.Left : content.Left - _offset.X;
        var right = rightPinned ? content.Right : content.Right - _offset.X;
        if (leftPinned && !rightPinned)
        {
            right = Math.Max(right, pinEdge);
        }
        return new Rect(left, content.Top - _offset.Y, Math.Max(0, right - left), content.Height);
    }

    /// <summary>The body rectangle of one cell (not animated), for the editor and its popups.</summary>
    internal Rect CellBodyRect(DataGridCellAddress cell)
    {
        var (rect, left, right) = ContentRect(new DataGridCellRange(cell.Row, cell.Column, cell.Row, cell.Column));
        return BodyRect(rect, left, right);
    }

    /// <summary>The body point under a viewport point.</summary>
    internal DataGridHit? HitTest(Point point, bool clamp)
    {
        var layout = _grid.Layout;
        var lines = _grid.LineCount;
        if (lines == 0 || layout.Count == 0)
        {
            return null;
        }
        var bodyTop = BodyTop;
        var bodyBottom = bodyTop + BodyHeight;
        if (!clamp && (point.Y < bodyTop || point.Y >= bodyBottom || point.X < 0 || point.X >= Bounds.Width))
        {
            return null;
        }
        var rowHeight = Math.Max(1, _grid.RowHeight);
        var line = (int)Math.Floor((Math.Clamp(point.Y, bodyTop, bodyBottom - 0.01) - bodyTop + _offset.Y) / rowHeight);
        line = Math.Clamp(line, 0, lines - 1);
        var row = _grid.RowOfLine(line);
        var group = _grid.GroupAtLine(line);
        var x = point.X;
        if (x < layout.Gutter && !clamp)
        {
            return new DataGridHit(line, row, 0, true, group, false);
        }
        int column;
        if (x < layout.PinEdge && layout.PinCount > 0)
        {
            column = Math.Max(0, layout.ColumnAtContent(Math.Max(x, layout.Gutter)));
            column = Math.Min(column, layout.PinCount - 1);
        }
        else
        {
            var content = Math.Max(x, layout.PinEdge) + _offset.X;
            column = layout.ColumnAtContent(content);
            column = Math.Clamp(column, Math.Min(layout.PinCount, layout.Count - 1), layout.Count - 1);
            if (x < layout.PinEdge)
            {
                column = Math.Min(layout.PinCount, layout.Count - 1);
            }
        }
        var fill = !clamp && _selection.IsOverFillHandle(point - new Point(0, bodyTop));
        return new DataGridHit(line, row, column, x < layout.Gutter, group, fill);
    }

    /// <summary>Scrolls a cell into view (pinned columns never scroll horizontally).</summary>
    internal void ScrollToCell(DataGridCellAddress cell)
    {
        var layout = _grid.Layout;
        if (layout.Count == 0 || _grid.LineCount == 0)
        {
            return;
        }
        var rowHeight = _grid.RowHeight;
        var top = _grid.LineOfRow(cell.Row) * rowHeight;
        var bottom = top + rowHeight;
        var y = _offset.Y;
        var visible = BodyHeight;
        if (top < y)
        {
            y = top;
        }
        else if (bottom > y + visible)
        {
            y = bottom - visible;
        }
        var x = _offset.X;
        var column = Math.Clamp(cell.Column, 0, layout.Count - 1);
        if (column >= layout.PinCount)
        {
            var left = layout.Lefts[column];
            var right = layout.Right(column);
            if (left - layout.PinEdge < x)
            {
                x = left - layout.PinEdge;
            }
            else if (right > x + Bounds.Width)
            {
                x = right - Bounds.Width;
            }
        }
        SetOffset(new Vector(x, y));
    }

    #endregion

    #region Selection, editor, flash

    internal void OnSelectionChanged()
    {
        _selection.OnSelectionChanged(animate: _drag is null || _drag.Mode == DragMode.Select);
        _header.UpdateRangeHighlight();
        _editorLayer.InvalidateArrange();
        _pinnedCells.InvalidateVisual();
        _peer?.NotifyActiveCellChanged();
    }

    /// <summary>Jumps the selection to its target (scrolling, resizing a column).</summary>
    internal void SnapSelection() => _selection.Snap();

    internal void Flash(DataGridCellRange range) => _selection.Flash(range);

    internal void AnimateChevron(string groupId, bool expanded)
    {
        _pinnedCells.AnimateChevron(groupId, expanded);
        _scrollCells.AnimateChevron(groupId, expanded);
    }

    internal string EditorText => _editorLayer.Text;

    internal bool IsEditorFocused => _editorLayer.IsEditorFocused;

    internal void OpenEditor(DataGridEditState edit, DataGridColumn column, string draft, bool openList, IReadOnlyList<string> options, string label)
    {
        _editorLayer.Open(edit, column, draft, openList, options, label);
        _selection.InvalidateVisual();
    }

    internal void CloseEditor()
    {
        _editorLayer.Close();
        _selection.InvalidateVisual();
    }

    internal void MarkEditorInvalid() => _editorLayer.MarkInvalid();

    internal void FocusGrid() => Focus(NavigationMethod.Pointer);

    #endregion

    #region Input

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && ReferenceEquals(e.Source, this))
        {
            _grid.HandleGridKey(e);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!e.Handled && ReferenceEquals(e.Source, this))
        {
            _grid.HandleGridText(e);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _selection.InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _selection.InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled)
        {
            return;
        }
        var point = e.GetCurrentPoint(this);
        _touchMode = e.Pointer.Type == PointerType.Touch;
        if (!point.Properties.IsLeftButtonPressed || point.Position.Y < BodyTop || point.Position.Y >= BodyTop + BodyHeight)
        {
            return;
        }
        if (HitTest(point.Position, clamp: false) is not { } hit)
        {
            return;
        }
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (e.Pointer.Type == PointerType.Touch && !hit.FillHandle)
        {
            _tap = new PendingTap(e.Pointer, hit, point.Position);
            return;
        }
        if (_grid.Edit is not null && !_grid.CommitEdit(null, null))
        {
            e.Handled = true;
            return;
        }
        e.Handled = true;
        FocusGrid();
        if (hit.Group is { } group && !hit.Gutter)
        {
            if (hit.Column == 0 || point.Position.X < _grid.Layout.PinEdge && hit.Column < _grid.Layout.PinCount)
            {
                _grid.ToggleGroup(group);
            }
            return;
        }
        if (hit.Gutter)
        {
            if (_grid.IsRowCheckEnabled && _pinnedCells.IsOverCheckbox(point.Position - new Point(0, BodyTop), hit.Row))
            {
                _grid.ToggleRowChecked(hit.Row, shift);
                return;
            }
            var lastColumn = _grid.Layout.Count - 1;
            if (shift)
            {
                _grid.Select(new DataGridCellAddress(_grid.Anchor.Row, 0), new DataGridCellAddress(hit.Row, lastColumn), scroll: false);
            }
            else
            {
                _grid.Select(new DataGridCellAddress(hit.Row, 0), new DataGridCellAddress(hit.Row, lastColumn), scroll: false);
            }
            BeginDrag(e, DragMode.Rows);
            return;
        }
        if (hit.FillHandle && !_grid.HasDataSource)
        {
            BeginDrag(e, DragMode.Fill);
            return;
        }
        var cell = new DataGridCellAddress(hit.Row, hit.Column);
        if (shift)
        {
            _grid.Select(_grid.Anchor, cell, cell);
        }
        else
        {
            _grid.Select(cell);
        }
        BeginDrag(e, DragMode.Select);
    }

    private void BeginDrag(PointerPressedEventArgs e, DragMode mode)
    {
        _drag = new DragState { Mode = mode, Source = _grid.Range, Pointer = e.Pointer };
        _lastPointer = e.GetPosition(this);
        e.Pointer.Capture(this);
        _autoScroll ??= new LiveLoop(this, AutoScrollStep) { MinimumLevel = ArcMotionLevel.Off };
        _autoScroll.Start();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _touchMode = false;
        }
        if (_tap is { } tap && Distance(position, tap.Start) > 8)
        {
            _tap = null;
        }
        if (_drag is { } drag && ReferenceEquals(drag.Pointer, e.Pointer))
        {
            _lastPointer = position;
            DragTo(position);
            return;
        }
        UpdateHover(position);
    }

    private void UpdateHover(Point position)
    {
        var row = -1;
        if (position.X < _grid.Layout.Gutter && position.Y >= BodyTop && position.Y < BodyTop + BodyHeight &&
            HitTest(position, clamp: false) is { Group: null } hit)
        {
            row = hit.Row;
        }
        if (row != _hoverRow)
        {
            _hoverRow = row;
            _pinnedCells.InvalidateVisual();
        }
        var overHandle = _selection.IsOverFillHandle(position - new Point(0, BodyTop));
        if (overHandle != _overHandle)
        {
            _overHandle = overHandle;
            Cursor = overHandle ? s_crossCursor ??= new Cursor(StandardCursorType.Cross) : null;
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverRow != -1)
        {
            _hoverRow = -1;
            _pinnedCells.InvalidateVisual();
        }
    }

    private void DragTo(Point position)
    {
        if (_drag is not { } drag || HitTest(position, clamp: true) is not { } hit)
        {
            return;
        }
        switch (drag.Mode)
        {
            case DragMode.Rows:
                if (_grid.Corner.Row != hit.Row)
                {
                    _grid.Select(new DataGridCellAddress(_grid.Anchor.Row, 0), new DataGridCellAddress(hit.Row, _grid.Layout.Count - 1), scroll: false);
                }
                break;
            case DragMode.Select:
                var corner = new DataGridCellAddress(hit.Row, hit.Column);
                if (_grid.Corner != corner)
                {
                    _grid.Select(_grid.Anchor, corner, scroll: false);
                }
                break;
            case DragMode.Fill:
                var source = drag.Source;
                var dr = hit.Row > source.Row1 ? hit.Row - source.Row1 : hit.Row < source.Row0 ? hit.Row - source.Row0 : 0;
                var dc = hit.Column > source.Column1 ? hit.Column - source.Column1 : hit.Column < source.Column0 ? hit.Column - source.Column0 : 0;
                DataGridCellRange? preview = null;
                if (dr != 0 || dc != 0)
                {
                    preview = Math.Abs(dr) >= Math.Abs(dc)
                        ? source with { Row0 = Math.Min(source.Row0, hit.Row), Row1 = Math.Max(source.Row1, hit.Row) }
                        : source with { Column0 = Math.Min(source.Column0, hit.Column), Column1 = Math.Max(source.Column1, hit.Column) };
                }
                if (preview != drag.Preview)
                {
                    drag.Preview = preview;
                    _selection.SetFillPreview(preview);
                }
                break;
        }
    }

    private bool AutoScrollStep(double dt)
    {
        if (_drag is null)
        {
            return false;
        }
        var bounds = Bounds;
        var point = _lastPointer;
        static double Speed(double over) => Math.Sign(over) * Math.Min(28, 0.35 * Math.Abs(over));
        var top = BodyTop;
        var bottom = top + BodyHeight;
        var dy = point.Y > bottom - 8 ? Speed(point.Y - bottom + 8) : point.Y < top ? Speed(point.Y - top) : 0;
        var pinEdge = _grid.Layout.PinEdge;
        var dx = point.X > bounds.Width - 8
            ? Speed(point.X - bounds.Width + 8)
            : point.X < pinEdge && _offset.X > 0 && _drag.Mode != DragMode.Rows ? Speed(point.X - pinEdge) : 0;
        if (dx != 0 || dy != 0)
        {
            ScrollBy(dx, dy);
            DragTo(point);
        }
        return true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_tap is { } tap && ReferenceEquals(tap.Pointer, e.Pointer))
        {
            _tap = null;
            CompleteTap(tap.Hit, e.GetPosition(this));
            e.Handled = true;
            return;
        }
        EndDrag();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag();
    }

    private void EndDrag()
    {
        if (_drag is not { } drag)
        {
            return;
        }
        _drag = null;
        _autoScroll?.Stop();
        drag.Pointer.Capture(null);
        if (drag.Mode == DragMode.Fill)
        {
            _selection.SetFillPreview(null);
            if (drag.Preview is { } target)
            {
                _grid.CompleteFill(drag.Source, target);
            }
        }
    }

    /// <summary>Touch: a tap selects on release; a second tap on the active single cell edits it.</summary>
    private void CompleteTap(DataGridHit hit, Point position)
    {
        FocusGrid();
        if (hit.Group is { } group && !hit.Gutter)
        {
            _grid.ToggleGroup(group);
            return;
        }
        if (hit.Gutter)
        {
            if (_grid.IsRowCheckEnabled && _pinnedCells.IsOverCheckbox(position - new Point(0, BodyTop), hit.Row))
            {
                _grid.ToggleRowChecked(hit.Row, extend: false);
                return;
            }
            _grid.Select(new DataGridCellAddress(hit.Row, 0), new DataGridCellAddress(hit.Row, _grid.Layout.Count - 1), scroll: false);
            return;
        }
        var cell = new DataGridCellAddress(hit.Row, hit.Column);
        if (_grid.Anchor == cell && _grid.Range.IsSingleCell && _grid.Edit is null)
        {
            _grid.BeginEdit(cell, null, openList: false);
            return;
        }
        if (_grid.Edit is not null && !_grid.CommitEdit(null, null))
        {
            return;
        }
        _grid.Select(cell);
    }

    /// <summary>Double click (or double tap) on a cell edits it; select and date cells open their list.</summary>
    internal void OnDoubleTapped(Point position)
    {
        if (HitTest(position, clamp: false) is not { Gutter: false, Group: null } hit)
        {
            return;
        }
        _grid.BeginEdit(new DataGridCellAddress(hit.Row, hit.Column), null, openList: true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DoubleTapped += OnDoubleTappedEvent;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DoubleTapped -= OnDoubleTappedEvent;
        _autoScroll?.Stop();
        _drag = null;
    }

    private void OnDoubleTappedEvent(object? sender, TappedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        var position = e.GetPosition(this);
        if (position.Y < BodyTop || position.Y >= BodyTop + BodyHeight)
        {
            return;
        }
        e.Handled = true;
        OnDoubleTapped(position);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    #endregion

    private DataGridBodyAutomationPeer? _peer;

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => _peer = new DataGridBodyAutomationPeer(_grid, this);
}
