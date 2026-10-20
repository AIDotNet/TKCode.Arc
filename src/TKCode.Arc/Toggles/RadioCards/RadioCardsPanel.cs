using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Lays the cards out like Arc's CSS grid: <c>repeat(auto-fill, minmax(min(100%, MinColumnWidth), 1fr))</c> with a 10px
/// gap, or one full-width column with an 8px gap in list layout. Rows take the tallest card, and every card in a row is
/// stretched to it, so prices sit on one line across a row.
/// </summary>
internal sealed class RadioCardsPanel : Panel
{
    private double[] _rowHeights = [];

    private RadioCards? Owner => TemplatedParent as RadioCards ?? this.FindAncestorOfType<RadioCards>();

    private (int Columns, double ColumnWidth, double Gap) Grid(double width)
    {
        var owner = Owner;
        var list = owner?.Layout == RadioCardsLayout.List;
        var gap = list ? owner?.ListGap ?? 8 : owner?.GridGap ?? 10;
        if (list)
        {
            return (1, width, gap);
        }
        var min = Math.Max(1, owner?.MinColumnWidth ?? 180);
        if (double.IsInfinity(width))
        {
            return (Math.Max(1, Children.Count), min, gap);
        }
        var columns = Math.Max(1, (int)Math.Floor((width + gap) / (Math.Min(min, width) + gap)));
        var columnWidth = Math.Max(0, (width - gap * (columns - 1)) / columns);
        return (columns, columnWidth, gap);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (columns, columnWidth, gap) = Grid(availableSize.Width);
        var rows = (Children.Count + columns - 1) / columns;
        if (_rowHeights.Length != rows)
        {
            _rowHeights = new double[rows];
        }
        Array.Clear(_rowHeights);
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            var row = i / columns;
            _rowHeights[row] = Math.Max(_rowHeights[row], child.DesiredSize.Height);
        }
        var height = rows == 0 ? 0 : _rowHeights.Sum() + gap * (rows - 1);
        var width = double.IsInfinity(availableSize.Width) ? columns * columnWidth + gap * (columns - 1) : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, columnWidth, gap) = Grid(finalSize.Width);
        var y = 0.0;
        for (var row = 0; row * columns < Children.Count; row++)
        {
            var height = row < _rowHeights.Length ? _rowHeights[row] : 0;
            for (var column = 0; column < columns; column++)
            {
                var index = row * columns + column;
                if (index >= Children.Count)
                {
                    break;
                }
                Children[index].Arrange(new Rect(column * (columnWidth + gap), y, columnWidth, height));
            }
            y += height + gap;
        }
        return finalSize;
    }
}
