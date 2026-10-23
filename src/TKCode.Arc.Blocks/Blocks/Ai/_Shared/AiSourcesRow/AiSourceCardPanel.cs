using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out the source cards in Arc's two arrangements. Grid (wide): two equal columns 8px apart, rows 8px apart, the cards
/// of a row as high as the taller one (CSS grid stretch). Row (narrow): one line of cards <see cref="CardWidth"/> wide and
/// 8px apart, all as high as the tallest (flex stretch), inside a horizontal scroller. <see cref="Padding"/> is room inside
/// the scroller: the focus rings in both modes and, in the row, the bleed to the edges of the page.
/// </summary>
internal sealed class AiSourceCardPanel : Panel
{
    /// <summary>The gap between cards, both ways.</summary>
    public const double Gap = 8;

    /// <summary>Whether the cards form one scrolling line (narrow) instead of the two-column grid.</summary>
    public static readonly StyledProperty<bool> IsRowProperty =
        AvaloniaProperty.Register<AiSourceCardPanel, bool>(nameof(IsRow));

    /// <summary>The width of each card in the row.</summary>
    public static readonly StyledProperty<double> CardWidthProperty =
        AvaloniaProperty.Register<AiSourceCardPanel, double>(nameof(CardWidth), AiSourceSnap.MaxCardWidth);

    /// <summary>Room around the cards.</summary>
    public static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<AiSourceCardPanel, Thickness>(nameof(Padding));

    static AiSourceCardPanel()
    {
        AffectsMeasure<AiSourceCardPanel>(IsRowProperty, CardWidthProperty, PaddingProperty);
    }

    /// <inheritdoc cref="IsRowProperty"/>
    public bool IsRow { get => GetValue(IsRowProperty); set => SetValue(IsRowProperty, value); }

    /// <inheritdoc cref="CardWidthProperty"/>
    public double CardWidth { get => GetValue(CardWidthProperty); set => SetValue(CardWidthProperty, value); }

    /// <inheritdoc cref="PaddingProperty"/>
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>The column width of the grid for a panel <paramref name="width"/> wide (padding included).</summary>
    internal double ColumnWidth(double width) => Math.Max(0, (width - Padding.Left - Padding.Right - Gap) / 2);

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children;
        var padding = Padding;
        var count = children.Count;
        if (count == 0)
        {
            return new Size(padding.Left + padding.Right, padding.Top + padding.Bottom);
        }
        if (IsRow)
        {
            var width = CardWidth;
            var tallest = 0.0;
            foreach (var child in children)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                tallest = Math.Max(tallest, child.DesiredSize.Height);
            }
            return new Size(padding.Left + padding.Right + count * width + (count - 1) * Gap, padding.Top + padding.Bottom + tallest);
        }
        var available = double.IsFinite(availableSize.Width) ? availableSize.Width : 2 * AiSourceSnap.MaxCardWidth + Gap;
        var column = ColumnWidth(available);
        var height = 0.0;
        for (var i = 0; i < count; i += 2)
        {
            children[i].Measure(new Size(column, double.PositiveInfinity));
            var row = children[i].DesiredSize.Height;
            if (i + 1 < count)
            {
                children[i + 1].Measure(new Size(column, double.PositiveInfinity));
                row = Math.Max(row, children[i + 1].DesiredSize.Height);
            }
            height += row + (i > 0 ? Gap : 0);
        }
        return new Size(available, padding.Top + padding.Bottom + height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children;
        var padding = Padding;
        var count = children.Count;
        if (IsRow)
        {
            var width = CardWidth;
            var height = Math.Max(0, finalSize.Height - padding.Top - padding.Bottom);
            for (var i = 0; i < count; i++)
            {
                children[i].Arrange(new Rect(padding.Left + i * (width + Gap), padding.Top, width, height));
            }
            return finalSize;
        }
        var column = ColumnWidth(finalSize.Width);
        var y = padding.Top;
        for (var i = 0; i < count; i += 2)
        {
            var row = children[i].DesiredSize.Height;
            if (i + 1 < count)
            {
                row = Math.Max(row, children[i + 1].DesiredSize.Height);
            }
            children[i].Arrange(new Rect(padding.Left, y, column, row));
            if (i + 1 < count)
            {
                children[i + 1].Arrange(new Rect(padding.Left + column + Gap, y, column, row));
            }
            y += row + Gap;
        }
        return finalSize;
    }
}
