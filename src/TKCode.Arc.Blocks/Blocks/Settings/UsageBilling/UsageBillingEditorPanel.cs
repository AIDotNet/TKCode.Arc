using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Holds the limit field of <see cref="UsageBilling"/>: left aligned and at most <see cref="MaxChildWidth"/> wide, but
/// never wider than the card, so the field shrinks on narrow cards instead of running past them.
/// </summary>
internal sealed class UsageBillingEditorPanel : Panel
{
    public static readonly StyledProperty<double> MaxChildWidthProperty =
        AvaloniaProperty.Register<UsageBillingEditorPanel, double>(nameof(MaxChildWidth), double.PositiveInfinity);

    static UsageBillingEditorPanel()
    {
        AffectsMeasure<UsageBillingEditorPanel>(MaxChildWidthProperty);
    }

    public double MaxChildWidth
    {
        get => GetValue(MaxChildWidthProperty);
        set => SetValue(MaxChildWidthProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Min(availableSize.Width, MaxChildWidth);
        double desiredWidth = 0, desiredHeight = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, availableSize.Height));
            desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
            desiredHeight = Math.Max(desiredHeight, child.DesiredSize.Height);
        }
        return new Size(double.IsInfinity(width) ? desiredWidth : width, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Min(finalSize.Width, MaxChildWidth);
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, width, finalSize.Height));
        }
        return finalSize;
    }
}
