using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A cell of an <see cref="AiDiffTable"/>: plain text, or text on the inserted tint (rounded 4, reaching 2px past the words
/// on each line it wraps over, like the card's inserted words). The tint is drawn under the glyphs without relayout.
/// </summary>
internal sealed class AiDiffTableCell : TextBlock
{
    /// <summary>Horizontal padding of an inserted cell's text (Arc <c>ins { padding: 0 2px }</c>).</summary>
    internal const double InsertPad = 2;

    private readonly TextRangeDecoration[] _tint = [NewTint(null)];

    protected override Type StyleKeyOverride => typeof(AiDiffTableCell);

    /// <summary>The tint behind the text; null for a plain cell.</summary>
    public IBrush? Tint
    {
        get => _tint[0].Background;
        set
        {
            _tint[0].Background = value;
            Padding = value is null ? default : new Thickness(InsertPad, 0, InsertPad, 0);
            InvalidateVisual();
        }
    }

    /// <summary>Whether this is a header cell.</summary>
    public bool IsHeader { get; init; }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        if (_tint[0].Background is not null && !string.IsNullOrEmpty(Text))
        {
            TextRangeRenderer.DrawBackgrounds(context, TextLayout, _tint, origin);
        }
        base.RenderTextLayout(context, origin);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The base disposes its layout: a fresh decoration does not keep it alive through its cache.
        _tint[0] = NewTint(_tint[0].Background);
    }

    /// <summary>The whole text on the tint: rounded 4, 2px past the words, the font's text box high.</summary>
    private static TextRangeDecoration NewTint(IBrush? background) => new(0, int.MaxValue)
    {
        Extent = TextRangeExtent.Text,
        Outset = new Thickness(InsertPad, 0, InsertPad, 0),
        CornerRadius = AiDiffDocument.TintRadius,
        Background = background
    };
}

/// <summary>
/// Lays out the cells of an <see cref="AiDiffTable"/> like an HTML table at <c>width: 100%</c> with automatic layout: each
/// column takes its widest cell, the space left over is shared in proportion to those widths, and when the cells do not
/// fit the columns shrink in proportion and their text wraps. Cells are padded 3 8 3 0 (the last column 0 on the right),
/// with no row lines, and centered vertically in their row. Children are the cells in row order, header row first.
/// </summary>
internal sealed class AiDiffTableGrid : Panel
{
    /// <summary>Padding above and below a cell's text.</summary>
    internal const double CellPadY = 3;

    /// <summary>Space after a column (none after the last).</summary>
    internal const double CellPadRight = 8;

    /// <summary>The number of columns.</summary>
    public static readonly StyledProperty<int> ColumnCountProperty =
        AvaloniaProperty.Register<AiDiffTableGrid, int>(nameof(ColumnCount));

    private double[] _widths = [];
    private double[] _heights = [];

    static AiDiffTableGrid()
    {
        AffectsMeasure<AiDiffTableGrid>(ColumnCountProperty);
    }

    /// <inheritdoc cref="ColumnCountProperty"/>
    public int ColumnCount { get => GetValue(ColumnCountProperty); set => SetValue(ColumnCountProperty, value); }

    /// <summary>The column widths of the last measure (text only, without the right padding).</summary>
    internal IReadOnlyList<double> ColumnWidths => _widths;

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = ColumnCount;
        var children = Children;
        if (columns <= 0 || children.Count == 0)
        {
            _widths = [];
            _heights = [];
            return default;
        }
        var rows = (children.Count + columns - 1) / columns;
        var natural = new double[columns];
        for (var i = 0; i < children.Count; i++)
        {
            children[i].Measure(Size.Infinity);
            natural[i % columns] = Math.Max(natural[i % columns], children[i].DesiredSize.Width);
        }
        var gaps = CellPadRight * (columns - 1);
        var content = natural.Sum();
        var widths = new double[columns];
        var available = availableSize.Width;
        if (double.IsInfinity(available) || double.IsNaN(available))
        {
            Array.Copy(natural, widths, columns);
        }
        else
        {
            var room = Math.Max(0, available - gaps);
            for (var c = 0; c < columns; c++)
            {
                // Grow or shrink each column in proportion to its widest cell (equal shares when all are empty).
                widths[c] = content > 0 ? room * natural[c] / content : room / columns;
            }
        }
        var heights = new double[rows];
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            child.Measure(new Size(widths[i % columns], double.PositiveInfinity));
            heights[i / columns] = Math.Max(heights[i / columns], child.DesiredSize.Height + CellPadY * 2);
        }
        _widths = widths;
        _heights = heights;
        return new Size(widths.Sum() + gaps, heights.Sum());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnCount;
        var children = Children;
        if (columns <= 0 || _widths.Length != columns)
        {
            return finalSize;
        }
        var y = 0.0;
        for (var row = 0; row < _heights.Length; row++)
        {
            var x = 0.0;
            for (var c = 0; c < columns; c++)
            {
                var index = row * columns + c;
                if (index >= children.Count)
                {
                    break;
                }
                var child = children[index];
                var height = Math.Min(child.DesiredSize.Height, _heights[row] - CellPadY * 2);
                var top = y + (_heights[row] - height) / 2;
                child.Arrange(new Rect(x, top, _widths[c], height));
                x += _widths[c] + CellPadRight;
            }
            y += _heights[row];
        }
        return finalSize;
    }
}
