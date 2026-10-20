using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Resolved geometry of one table layout pass (shared by the header, the rows and the presenter).</summary>
internal sealed class SortableDataTableLayout
{
    private double[] _x = [];
    private double[] _w = [];

    public double Width { get; set; }
    public double RowHeight { get; set; } = 52;
    public double HeaderHeight { get; set; } = 44;
    public bool Compact { get; set; }
    public double SelectWidth { get; set; }

    public double ColumnX(int index) => index >= 0 && index < _x.Length ? _x[index] : 0;

    public double ColumnWidth(int index) => index >= 0 && index < _w.Length ? _w[index] : 0;

    public void SetColumns(double[] x, double[] w)
    {
        _x = x;
        _w = w;
    }
}

/// <summary>
/// The header row: one cell per column (and Select all), with the bottom rule. It lives inside the scrolled
/// content so it follows horizontal scrolling, and the presenter offsets it with a render transform to keep it
/// stuck to the top of the visible area. Below the compact breakpoint it becomes Arc's strip of sort pills that
/// scrolls sideways under a pinned Select all.
/// </summary>
internal sealed class SortableDataTableHeaderRow : Control
{
    private const double PillHeight = 32;
    private const double PillGap = 4;
    private const double StripInset = 8;
    private const double PinnedWidth = 40;

    private readonly SortableDataTable _table;
    private readonly List<SortableDataTableHeaderCell> _cells = new();
    private readonly TranslateTransform _stick = new();
    private double _stripOffset;
    private double _stripExtent;
    private LinearGradientBrush? _fade;
    private Color _fadeColor;

    private readonly HeaderFade _fadeLayer;

    public SortableDataTableHeaderRow(SortableDataTable table)
    {
        _table = table;
        RenderTransform = _stick;
        ZIndex = 1;
        ClipToBounds = false;
        _fadeLayer = new HeaderFade(this) { ZIndex = 1, IsHitTestVisible = false };
        VisualChildren.Add(_fadeLayer);
    }

    public IReadOnlyList<SortableDataTableHeaderCell> Cells => _cells;

    public double StickOffset
    {
        get => _stick.Y;
        set => _stick.Y = value;
    }

    public void Rebuild()
    {
        foreach (var cell in _cells)
        {
            VisualChildren.Remove(cell);
            LogicalChildren.Remove(cell);
        }
        _cells.Clear();
        if (_table.IsSelectable)
        {
            _cells.Add(new SortableDataTableHeaderCell(_table, -1) { Focusable = true });
        }
        for (var i = 0; i < _table.ColumnCount; i++)
        {
            var column = _table.ColumnAt(i)!;
            _cells.Add(new SortableDataTableHeaderCell(_table, i)
            {
                Focusable = column.IsSortable,
                Cursor = column.IsSortable ? new Cursor(StandardCursorType.Hand) : null
            });
        }
        foreach (var cell in _cells)
        {
            LogicalChildren.Add(cell);
            VisualChildren.Add(cell);
            cell.UpdateAutomation();
        }
        _stripOffset = 0;
        InvalidateMeasure();
    }

    public SortableDataTableHeaderCell? CellFor(int columnIndex)
    {
        foreach (var cell in _cells)
        {
            if (cell.ColumnIndex == columnIndex)
            {
                return cell;
            }
        }
        return null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = _table.Layout;
        foreach (var cell in _cells)
        {
            cell.Measure(new Size(layout.Width, layout.HeaderHeight));
        }
        _fadeLayer.Measure(new Size(36, layout.HeaderHeight));
        return new Size(layout.Width, layout.HeaderHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = _table.Layout;
        if (!layout.Compact)
        {
            foreach (var cell in _cells)
            {
                var rect = cell.IsSelectAll
                    ? new Rect(0, 0, layout.SelectWidth, finalSize.Height - 1)
                    : new Rect(layout.ColumnX(cell.ColumnIndex), 0, layout.ColumnWidth(cell.ColumnIndex), finalSize.Height - 1);
                cell.ZIndex = 0;
                cell.Arrange(rect);
            }
            _stripExtent = 0;
            _fadeLayer.Arrange(default);
            return finalSize;
        }
        var x = StripInset;
        var top = (finalSize.Height - 1 - PillHeight) / 2;
        var start = x;
        foreach (var cell in _cells)
        {
            if (cell.IsSelectAll)
            {
                cell.ZIndex = 2;
                cell.Arrange(new Rect(0, top, PinnedWidth, PillHeight));
                start = PinnedWidth;
                continue;
            }
            x = Math.Max(x, start);
            var width = cell.MeasureNaturalWidth() - _table.CellInlinePadding * 2 + 20;
            cell.ZIndex = 0;
            cell.Arrange(new Rect(x - _stripOffset, top, width, PillHeight));
            x += width + PillGap;
        }
        _stripExtent = x - PillGap + StripInset;
        ClampStrip(finalSize.Width);
        _fadeLayer.Arrange(new Rect(PinnedWidth, 0, 36, Math.Max(0, finalSize.Height - 1)));
        _fadeLayer.InvalidateVisual();
        return finalSize;
    }

    private void ClampStrip(double width)
    {
        var max = Math.Max(0, _stripExtent - width);
        _stripOffset = Math.Clamp(_stripOffset, 0, max);
    }

    /// <summary>Keeps a pill inside the strip (Arc scrolls the active sort control into view).</summary>
    public void RevealPill(SortableDataTableHeaderCell cell)
    {
        if (!_table.Layout.Compact || cell.IsSelectAll)
        {
            return;
        }
        var bounds = cell.Bounds;
        var left = (_table.IsSelectable ? PinnedWidth : 0) + StripInset;
        var right = Bounds.Width - StripInset;
        var shift = bounds.Right > right ? bounds.Right - right : bounds.X < left ? bounds.X - left : 0;
        if (Math.Abs(shift) > 0.5)
        {
            _stripOffset += shift;
            InvalidateArrange();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!_table.Layout.Compact || _stripExtent <= Bounds.Width)
        {
            return;
        }
        var delta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? e.Delta.X : e.Delta.Y;
        var before = _stripOffset;
        _stripOffset -= delta * 40;
        ClampStrip(Bounds.Width);
        if (Math.Abs(before - _stripOffset) > 0.01)
        {
            e.Handled = true;
            InvalidateArrange();
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var palette = _table.Palette;
        var size = Bounds.Size;
        context.FillRectangle(palette.SurfaceBrush, new Rect(size));
        context.DrawLine(palette.RulePen, new Point(0, size.Height - 0.5), new Point(size.Width, size.Height - 0.5));
    }

    /// <summary>Once the strip scrolls, a fade off the pinned cell hides the word sliding under it.</summary>
    internal void RenderFade(DrawingContext context)
    {
        if (!_table.Layout.Compact || !_table.IsSelectable || _stripOffset <= 0)
        {
            return;
        }
        var palette = _table.Palette;
        if (_fade is null || _fadeColor != palette.Surface)
        {
            _fadeColor = palette.Surface;
            _fade = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(palette.Surface, 0.45),
                    new GradientStop(Color.FromArgb(0, palette.Surface.R, palette.Surface.G, palette.Surface.B), 1)
                }
            };
        }
        using (context.PushOpacity(Math.Clamp(_stripOffset / 24, 0, 1)))
        {
            context.FillRectangle(_fade, new Rect(0, 0, 36, Math.Max(0, Bounds.Height - 1)));
        }
    }

    private sealed class HeaderFade : Control
    {
        private readonly SortableDataTableHeaderRow _row;

        public HeaderFade(SortableDataTableHeaderRow row) => _row = row;

        public override void Render(DrawingContext context) => _row.RenderFade(context);
    }
}
