using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The page's article column (Arc's <c>Article</c>): <see cref="Padding"/> inside its own width (the 680 maximum includes
/// it), the head (title and meta line, the children that are not blocks, in order, then <see cref="HeadSpacing"/>), the
/// blocks one under another (each block's <c>Margin.Top</c> is its gap above), and the floating
/// <see cref="AiPanelAskButton"/>s at their <see cref="AiPanelAskButton.Position"/>, which do not change the size. The
/// whole column takes the pointer, so a press beside the text still reaches the page.
/// </summary>
internal sealed class AiPanelArticle : Panel
{
    /// <summary>The space inside the column.</summary>
    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<AiPanelArticle>();

    /// <summary>The space between the head and the first block.</summary>
    public static readonly StyledProperty<double> HeadSpacingProperty =
        AvaloniaProperty.Register<AiPanelArticle, double>(nameof(HeadSpacing), 18);

    static AiPanelArticle()
    {
        AffectsMeasure<AiPanelArticle>(PaddingProperty, HeadSpacingProperty);
        BackgroundProperty.OverrideDefaultValue<AiPanelArticle>(Brushes.Transparent);
    }

    /// <inheritdoc cref="PaddingProperty"/>
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <inheritdoc cref="HeadSpacingProperty"/>
    public double HeadSpacing { get => GetValue(HeadSpacingProperty); set => SetValue(HeadSpacingProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var inner = new Size(Math.Max(0, width - padding.Left - padding.Right), double.PositiveInfinity);
        var y = padding.Top;
        var head = false;
        var first = true;
        var widest = 0.0;
        foreach (var child in Children)
        {
            if (child is AiPanelAskButton)
            {
                child.Measure(Size.Infinity);
                continue;
            }
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(inner);
            widest = Math.Max(widest, child.DesiredSize.Width);
            if (child is AiPanelBlockView)
            {
                if (first && head)
                {
                    y += HeadSpacing;
                }
                // A block's own top margin is its gap above; the first block has none.
                y += first ? child.DesiredSize.Height - child.Margin.Top : child.DesiredSize.Height;
                first = false;
            }
            else
            {
                y += child.DesiredSize.Height;
                head = true;
            }
        }
        y += padding.Bottom;
        var desiredWidth = double.IsFinite(availableSize.Width) ? width : widest + padding.Left + padding.Right;
        return new Size(desiredWidth, y);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        var innerWidth = Math.Max(0, finalSize.Width - padding.Left - padding.Right);
        var y = padding.Top;
        var head = false;
        var first = true;
        foreach (var child in Children)
        {
            if (child is AiPanelAskButton ask)
            {
                child.Arrange(new Rect(ask.Position, child.DesiredSize));
                continue;
            }
            if (!child.IsVisible)
            {
                continue;
            }
            // Desired sizes include the margins, and Arrange insets them again.
            var height = child.DesiredSize.Height;
            if (child is AiPanelBlockView)
            {
                if (first && head)
                {
                    y += HeadSpacing;
                }
                // The first block's gap above is dropped: it starts right under the head.
                var skipped = first ? child.Margin.Top : 0;
                child.Arrange(new Rect(padding.Left, y - skipped, innerWidth, height));
                y += height - skipped;
                first = false;
            }
            else
            {
                child.Arrange(new Rect(padding.Left, y, innerWidth, height));
                y += height;
                head = true;
            }
        }
        return finalSize;
    }

    /// <summary>The width inside the padding.</summary>
    public double InnerWidth => Math.Max(0, Bounds.Width - Padding.Left - Padding.Right);
}
