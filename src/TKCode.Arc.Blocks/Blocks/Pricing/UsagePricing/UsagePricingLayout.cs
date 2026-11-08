using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The page grid of <see cref="UsagePricing"/> (notes 3.1, 8): from <see cref="Threshold"/> wide, the heading, the
/// controls and the breakdown stack in a flexible left column and the card takes a fixed column on the right, top
/// aligned; narrower, everything stacks as heading, controls, card, breakdown. Children are taken in that template
/// order: heading, controls, breakdown, card (further children, such as the live region, are measured and laid over
/// the top left without taking space).
/// </summary>
internal sealed class UsagePricingLayout : Panel
{
    public static readonly StyledProperty<double> ThresholdProperty =
        AvaloniaProperty.Register<UsagePricingLayout, double>(nameof(Threshold), 780);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<UsagePricingLayout, double>(nameof(ColumnGap), 40);

    public static readonly StyledProperty<double> CardWidthProperty =
        AvaloniaProperty.Register<UsagePricingLayout, double>(nameof(CardWidth), 392);

    public static readonly StyledProperty<double> StackedCardGapProperty =
        AvaloniaProperty.Register<UsagePricingLayout, double>(nameof(StackedCardGap), 28);

    public double Threshold { get => GetValue(ThresholdProperty); set => SetValue(ThresholdProperty, value); }
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    public double CardWidth { get => GetValue(CardWidthProperty); set => SetValue(CardWidthProperty, value); }
    public double StackedCardGap { get => GetValue(StackedCardGapProperty); set => SetValue(StackedCardGapProperty, value); }

    static UsagePricingLayout()
    {
        AffectsMeasure<UsagePricingLayout>(ThresholdProperty, ColumnGapProperty, CardWidthProperty, StackedCardGapProperty);
    }

    /// <summary>Raised when the layout switches between two columns and the stack.</summary>
    public event EventHandler<bool>? StackedChanged;

    /// <summary>True while everything stacks in one column.</summary>
    public bool IsStacked { get; private set; }

    /// <summary>Whether a width stacks the layout (narrower than the threshold).</summary>
    internal static bool Stacks(double width, double threshold) => width < threshold;

    private Control? Part(int index) => index < Children.Count ? Children[index] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? Threshold : availableSize.Width;
        SetStacked(Stacks(width, Threshold));
        var head = Part(0);
        var controls = Part(1);
        var breakdown = Part(2);
        var card = Part(3);
        for (var i = 4; i < Children.Count; i++)
        {
            Children[i].Measure(availableSize);
        }
        if (IsStacked)
        {
            var size = new Size(width, double.PositiveInfinity);
            var height = 0.0;
            foreach (var part in new[] { head, controls, card, breakdown })
            {
                if (part is null)
                {
                    continue;
                }
                part.Measure(size);
                height += part.DesiredSize.Height;
            }
            if (card is { IsVisible: true })
            {
                height += StackedCardGap;
            }
            return new Size(width, height);
        }
        var cardWidth = Math.Min(CardWidth, width);
        var left = Math.Max(0, width - cardWidth - ColumnGap);
        var leftSize = new Size(left, double.PositiveInfinity);
        var leftHeight = 0.0;
        foreach (var part in new[] { head, controls, breakdown })
        {
            if (part is null)
            {
                continue;
            }
            part.Measure(leftSize);
            leftHeight += part.DesiredSize.Height;
        }
        card?.Measure(new Size(cardWidth, double.PositiveInfinity));
        return new Size(width, Math.Max(leftHeight, card?.DesiredSize.Height ?? 0));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var head = Part(0);
        var controls = Part(1);
        var breakdown = Part(2);
        var card = Part(3);
        for (var i = 4; i < Children.Count; i++)
        {
            Children[i].Arrange(new Rect(Children[i].DesiredSize));
        }
        var y = 0.0;
        if (IsStacked)
        {
            foreach (var part in new[] { head, controls, card, breakdown })
            {
                if (part is null)
                {
                    continue;
                }
                if (ReferenceEquals(part, card) && part.IsVisible)
                {
                    y += StackedCardGap;
                }
                part.Arrange(new Rect(0, y, width, part.DesiredSize.Height));
                y += part.DesiredSize.Height;
            }
            return finalSize;
        }
        var cardWidth = Math.Min(CardWidth, width);
        var left = Math.Max(0, width - cardWidth - ColumnGap);
        foreach (var part in new[] { head, controls, breakdown })
        {
            if (part is null)
            {
                continue;
            }
            part.Arrange(new Rect(0, y, left, part.DesiredSize.Height));
            y += part.DesiredSize.Height;
        }
        card?.Arrange(new Rect(width - cardWidth, 0, cardWidth, card.DesiredSize.Height));
        return finalSize;
    }

    private void SetStacked(bool stacked)
    {
        if (stacked == IsStacked)
        {
            return;
        }
        IsStacked = stacked;
        StackedChanged?.Invoke(this, stacked);
    }
}
