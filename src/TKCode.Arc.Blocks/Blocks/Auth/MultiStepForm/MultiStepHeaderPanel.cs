using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out the progress header: the step list (first child) and the count (second child). Side by side, the list takes
/// what is left beside the count (and scrolls when its steps do not fit) with <see cref="Gap"/> between them, both centered
/// on one row; stacked (narrow cards), the count sits above the list.
/// </summary>
internal sealed class MultiStepHeaderPanel : Panel
{
    /// <summary>Defines the <see cref="IsStacked"/> property.</summary>
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<MultiStepHeaderPanel, bool>(nameof(IsStacked));

    /// <summary>Defines the <see cref="Gap"/> property.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<MultiStepHeaderPanel, double>(nameof(Gap), 16);

    static MultiStepHeaderPanel()
    {
        AffectsMeasure<MultiStepHeaderPanel>(IsStackedProperty, GapProperty);
    }

    /// <summary>True puts the count above the list.</summary>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    /// <summary>Space between the list and the count.</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private Control? List => Children.Count > 0 ? Children[0] : null;

    private Control? Count => Children.Count > 1 ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var list = List;
        var count = Count;
        count?.Measure(availableSize);
        var countSize = count?.IsVisible == true ? count.DesiredSize : default;
        if (IsStacked)
        {
            list?.Measure(availableSize);
            var listSize = list?.DesiredSize ?? default;
            var gap = countSize.Height > 0 && listSize.Height > 0 ? Gap : 0;
            return new Size(Math.Max(listSize.Width, countSize.Width), countSize.Height + gap + listSize.Height);
        }
        var sideGap = countSize.Width > 0 ? Gap : 0;
        var listAvailable = Math.Max(0, availableSize.Width - countSize.Width - sideGap);
        list?.Measure(availableSize.WithWidth(listAvailable));
        var listDesired = list?.DesiredSize ?? default;
        return new Size(listDesired.Width + sideGap + countSize.Width, Math.Max(listDesired.Height, countSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var list = List;
        var count = Count;
        var countSize = count?.IsVisible == true ? count.DesiredSize : default;
        if (IsStacked)
        {
            count?.Arrange(new Rect(0, 0, countSize.Width, countSize.Height));
            var listSize = list?.DesiredSize ?? default;
            var gap = countSize.Height > 0 && listSize.Height > 0 ? Gap : 0;
            list?.Arrange(new Rect(0, countSize.Height + gap, finalSize.Width, listSize.Height));
            return finalSize;
        }
        var sideGap = countSize.Width > 0 ? Gap : 0;
        var listWidth = Math.Max(0, finalSize.Width - countSize.Width - sideGap);
        var listHeight = list?.DesiredSize.Height ?? 0;
        list?.Arrange(new Rect(0, (finalSize.Height - listHeight) / 2, Math.Min(listWidth, list.DesiredSize.Width), listHeight));
        count?.Arrange(new Rect(finalSize.Width - countSize.Width, (finalSize.Height - countSize.Height) / 2, countSize.Width, countSize.Height));
        return finalSize;
    }
}
