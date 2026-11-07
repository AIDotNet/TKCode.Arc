using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The card grid: two columns 16 apart while the grid is wider than <see cref="Breakpoint"/>, one column 12 apart
/// otherwise (Arc: a container query on the component's own width); every slot is the column width at
/// <see cref="AspectRatio"/>.
/// </summary>
internal sealed class CardToArticleGrid : Panel
{
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<CardToArticleGrid, double>(nameof(AspectRatio), 4.0 / 3.0);

    public static readonly StyledProperty<double> BreakpointProperty =
        AvaloniaProperty.Register<CardToArticleGrid, double>(nameof(Breakpoint), CardToArticleLayout.ColumnBreakpoint);

    public double AspectRatio { get => GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }
    public double Breakpoint { get => GetValue(BreakpointProperty); set => SetValue(BreakpointProperty, value); }

    static CardToArticleGrid()
    {
        AffectsMeasure<CardToArticleGrid>(AspectRatioProperty, BreakpointProperty);
    }

    /// <summary>Columns at the last arrange (for tests and the narrow pseudo-class).</summary>
    public int ColumnCount { get; private set; } = 2;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : CardToArticleLayout.SheetMaxWidth;
        for (var i = 0; i < Children.Count; i++)
        {
            var slot = CardToArticleLayout.Slot(i, width, AspectRatio, Breakpoint);
            Children[i].Measure(slot.Size);
        }
        return new Size(width, CardToArticleLayout.GridHeight(Children.Count, width, AspectRatio, Breakpoint));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ColumnCount = CardToArticleLayout.Columns(finalSize.Width, Breakpoint);
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(CardToArticleLayout.Slot(i, finalSize.Width, AspectRatio, Breakpoint));
        }
        return finalSize;
    }
}
