using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A sticky row of per-column controls (header, filter or totals): an optional gutter control, the pinned columns that
/// never scroll, and the scrolling columns inside a clip that moves with the horizontal offset. Only the columns in
/// view (plus 240px either side) have controls; they are recycled as the window moves in 64px steps.
/// </summary>
internal abstract class DataGridColumnRow<TCell> : Control where TCell : Control
{
    private const double WindowMargin = 240;
    private const double WindowStep = 64;

    private readonly Dictionary<int, TCell> _cells = new();
    private readonly Stack<TCell> _pool = new();
    private readonly Canvas _pinned = new();
    private readonly Canvas _scroll = new();
    private readonly Border _scrollClip;
    private readonly TranslateTransform _shift = new();
    private int _windowStart = -1;
    private int _windowEnd = -1;
    private int _windowStep = int.MinValue;
    private int _layoutVersion = -1;

    protected DataGridColumnRow(DataGrid grid, DataGridViewport viewport)
    {
        Grid = grid;
        Viewport = viewport;
        _scroll.RenderTransform = _shift;
        _scrollClip = new Border { ClipToBounds = true, Child = _scroll };
        AddChild(_scrollClip);
        AddChild(_pinned);
    }

    protected DataGrid Grid { get; }

    protected DataGridViewport Viewport { get; }

    protected Control? Gutter { get; private set; }

    protected IReadOnlyDictionary<int, TCell> Cells => _cells;

    protected void SetGutter(Control gutter)
    {
        Gutter = gutter;
        AddChild(gutter);
    }

    private void AddChild(Control child)
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
    }

    protected abstract TCell CreateCell();

    /// <summary>Points a cell at a visible column.</summary>
    protected abstract void BindCell(TCell cell, int visibleIndex, int accessor);

    /// <summary>Re-binds every realized cell (texts, sort state, values).</summary>
    public virtual void Refresh()
    {
        _layoutVersion = -1;
        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <summary>Called when the horizontal offset changed.</summary>
    public void OnScrolled()
    {
        var layout = Grid.Layout;
        _shift.X = -Viewport.Offset.X;
        var step = (int)Math.Floor(Viewport.Offset.X / WindowStep);
        if (step != _windowStep)
        {
            InvalidateArrange();
        }
        UpdatePinnedEdge(layout);
    }

    protected virtual void UpdatePinnedEdge(DataGridLayout layout)
    {
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : 40;
        Gutter?.Measure(new Size(Grid.Layout.Gutter, height));
        _pinned.Measure(availableSize);
        _scrollClip.Measure(availableSize);
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : Grid.Layout.Total, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = Grid.Layout;
        Realize(layout, finalSize.Width, finalSize.Height);
        var pinEdge = Math.Min(layout.PinEdge, finalSize.Width);
        Gutter?.Arrange(new Rect(0, 0, layout.Gutter, finalSize.Height));
        if (Gutter is not null)
        {
            Gutter.IsVisible = layout.Gutter > 0;
        }
        _pinned.Arrange(new Rect(0, 0, pinEdge, finalSize.Height));
        _scrollClip.Arrange(new Rect(pinEdge, 0, Math.Max(0, finalSize.Width - pinEdge), finalSize.Height));
        _scroll.Arrange(new Rect(0, 0, Math.Max(0, layout.Total - layout.PinEdge), finalSize.Height));
        _shift.X = -Viewport.Offset.X;
        UpdatePinnedEdge(layout);
        return finalSize;
    }

    private void Realize(DataGridLayout layout, double width, double height)
    {
        var offset = Viewport.Offset.X;
        var step = (int)Math.Floor(offset / WindowStep);
        var left = step * WindowStep + layout.PinEdge - WindowMargin;
        var right = (step + 1) * WindowStep + width + WindowMargin;
        var start = layout.PinCount;
        while (start < layout.Count && layout.Right(start) < left)
        {
            start++;
        }
        var end = start;
        while (end < layout.Count && layout.Lefts[end] < right)
        {
            end++;
        }
        var version = Grid.ColumnsVersion;
        var same = start == _windowStart && end == _windowEnd && version == _layoutVersion;
        _windowStep = step;
        _windowStart = start;
        _windowEnd = end;
        _layoutVersion = version;
        var wanted = new HashSet<int>();
        for (var i = 0; i < layout.PinCount; i++)
        {
            wanted.Add(layout.Accessors[i]);
        }
        for (var i = start; i < end; i++)
        {
            wanted.Add(layout.Accessors[i]);
        }
        if (!same)
        {
            foreach (var accessor in _cells.Keys.Where(a => !wanted.Contains(a)).ToList())
            {
                var cell = _cells[accessor];
                _cells.Remove(accessor);
                (cell.Parent as Canvas)?.Children.Remove(cell);
                _pool.Push(cell);
            }
        }
        for (var i = 0; i < layout.Count; i++)
        {
            var accessor = layout.Accessors[i];
            if (!wanted.Contains(accessor))
            {
                continue;
            }
            var pinned = i < layout.PinCount;
            if (!_cells.TryGetValue(accessor, out var cell))
            {
                cell = _pool.Count > 0 ? _pool.Pop() : CreateCell();
                _cells[accessor] = cell;
            }
            var host = pinned ? _pinned : _scroll;
            if (!ReferenceEquals(cell.Parent, host))
            {
                (cell.Parent as Canvas)?.Children.Remove(cell);
                host.Children.Add(cell);
            }
            if (!same)
            {
                BindCell(cell, i, accessor);
            }
            var x = pinned ? layout.Lefts[i] : layout.Lefts[i] - layout.PinEdge;
            Canvas.SetLeft(cell, x);
            Canvas.SetTop(cell, 0);
            cell.Width = layout.Widths[i];
            cell.Height = height;
        }
    }
}

/// <summary>The sticky header row: the select-all box in the gutter and one header cell per column.</summary>
internal sealed class DataGridHeaderRow : DataGridColumnRow<DataGridHeaderCell>
{
    private readonly DataGridSelectAllBox _selectAll;

    public DataGridHeaderRow(DataGrid grid, DataGridViewport viewport) : base(grid, viewport)
    {
        _selectAll = new DataGridSelectAllBox(grid);
        SetGutter(_selectAll);
        ClipToBounds = true;
    }

    protected override DataGridHeaderCell CreateCell() => new(Grid);

    protected override void BindCell(DataGridHeaderCell cell, int visibleIndex, int accessor) => cell.Bind(visibleIndex, accessor);

    public override void Refresh()
    {
        base.Refresh();
        foreach (var cell in Cells.Values)
        {
            cell.Update();
        }
        UpdateSelectAll();
    }

    public void UpdateSelectAll() => _selectAll.Update();

    /// <summary>Focuses the sort button of the header of the column at a visible index (header cells are kept per column, so a moved column keeps its cell).</summary>
    public void FocusColumn(int visibleIndex)
    {
        UpdateLayout();
        var visible = Grid.VisibleAccessors;
        if (visibleIndex >= 0 && visibleIndex < visible.Length && Cells.TryGetValue(visible[visibleIndex], out var cell))
        {
            cell.FocusSort();
        }
    }

    /// <summary>Highlights the headers of the selected columns.</summary>
    public void UpdateRangeHighlight()
    {
        foreach (var cell in Cells.Values)
        {
            cell.UpdateRange();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var palette = Grid.Palette;
        context.FillRectangle(palette.SurfaceBrush, new Rect(Bounds.Size));
    }
}

/// <summary>The filter row: a clear-all button in the gutter and one filter cell per column.</summary>
internal sealed class DataGridFilterRow : DataGridColumnRow<DataGridFilterCell>
{
    private readonly DataGridClearFiltersButton _clear;

    public DataGridFilterRow(DataGrid grid, DataGridViewport viewport) : base(grid, viewport)
    {
        _clear = new DataGridClearFiltersButton(grid);
        SetGutter(_clear);
        ClipToBounds = true;
    }

    protected override DataGridFilterCell CreateCell() => new(Grid);

    protected override void BindCell(DataGridFilterCell cell, int visibleIndex, int accessor) => cell.Bind(visibleIndex, accessor);

    public override void Refresh()
    {
        base.Refresh();
        foreach (var cell in Cells.Values)
        {
            cell.Update();
        }
        _clear.Update();
    }

    /// <summary>Moves focus to the filter input of a column (column menu: Filter).</summary>
    public void FocusColumn(int accessor)
    {
        InvalidateArrange();
        UpdateLayout();
        if (Cells.TryGetValue(accessor, out var cell))
        {
            cell.FocusInput();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Grid.Palette.SurfaceBrush, new Rect(Bounds.Size));
    }
}

/// <summary>The sticky totals row: the row count under the first column and one total per column.</summary>
internal sealed class DataGridTotalsRow : DataGridColumnRow<DataGridTotalCell>
{
    private readonly Border _gutter = new();

    public DataGridTotalsRow(DataGrid grid, DataGridViewport viewport) : base(grid, viewport)
    {
        SetGutter(_gutter);
        ClipToBounds = true;
    }

    protected override DataGridTotalCell CreateCell() => new(Grid);

    protected override void BindCell(DataGridTotalCell cell, int visibleIndex, int accessor) => cell.Bind(visibleIndex, accessor);

    public override void Refresh()
    {
        base.Refresh();
        if (!IsVisible)
        {
            return;
        }
        foreach (var cell in Cells.Values)
        {
            cell.Update();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var palette = Grid.Palette;
        context.FillRectangle(palette.SurfaceBrush, new Rect(Bounds.Size));
        context.DrawLine(palette.BorderPen, new Point(0, 0.5), new Point(Bounds.Width, 0.5));
    }
}
