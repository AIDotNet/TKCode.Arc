using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TKCode.Arc.Mobile;

/// <summary>
/// The items panel of <see cref="RadioCards"/>: the group's <see cref="RadioCards.Columns"/> equal-width columns, each row
/// as tall as its tallest card, with the group's column and row gaps between them. Hidden cards take no room. The group
/// remeasures the panel when its values change.
/// </summary>
internal sealed class RadioCardsPanel : Panel
{
    private readonly List<double> _rowHeights = [];
    private RadioCards? _owner;

    private int Columns => Math.Max(1, _owner?.Columns ?? 2);

    private double ColumnGap => Math.Max(0, _owner?.ColumnGap ?? 0);

    private double RowGap => Math.Max(0, _owner?.RowGap ?? 0);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = this.FindAncestorOfType<RadioCards>();
        InvalidateMeasure();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _owner = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Columns;
        var gap = ColumnGap;
        var unbounded = double.IsInfinity(availableSize.Width);
        var columnWidth = unbounded ? double.PositiveInfinity : Math.Max(0, (availableSize.Width - gap * (columns - 1)) / columns);
        var widest = 0.0;
        var height = 0.0;
        var rowHeight = 0.0;
        var position = 0;
        var rows = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            widest = Math.Max(widest, child.DesiredSize.Width);
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            position++;
            if (position % columns == 0)
            {
                height += rowHeight;
                rowHeight = 0;
                rows++;
            }
        }
        if (position % columns != 0)
        {
            height += rowHeight;
            rows++;
        }
        height += RowGap * Math.Max(0, rows - 1);
        var width = unbounded ? widest * columns + gap * (columns - 1) : availableSize.Width;
        return new Size(position == 0 ? 0 : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Columns;
        var gap = ColumnGap;
        var rowGap = RowGap;
        var columnWidth = Math.Max(0, (finalSize.Width - gap * (columns - 1)) / columns);

        _rowHeights.Clear();
        var position = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var row = position / columns;
            if (row == _rowHeights.Count)
            {
                _rowHeights.Add(0);
            }
            _rowHeights[row] = Math.Max(_rowHeights[row], child.DesiredSize.Height);
            position++;
        }

        position = 0;
        var y = 0.0;
        var currentRow = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var row = position / columns;
            while (currentRow < row)
            {
                y += _rowHeights[currentRow] + rowGap;
                currentRow++;
            }
            var column = position % columns;
            child.Arrange(new Rect(column * (columnWidth + gap), y, columnWidth, _rowHeights[row]));
            position++;
        }
        return finalSize;
    }
}
