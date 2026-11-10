using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Places the rings and the legend of an <see cref="ActivityRings"/>: side by side when the width allows
/// (<see cref="SideMinWidth"/>), the legend under the rings when it does not. The first child is the rings, the second
/// the legend.
/// </summary>
internal sealed class ActivityRingsLayout : Panel
{
    /// <summary>Space between the rings and the legend.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ActivityRingsLayout, double>(nameof(Spacing), 24);

    /// <summary>Narrower than this, the legend goes under the rings.</summary>
    public static readonly StyledProperty<double> SideMinWidthProperty =
        AvaloniaProperty.Register<ActivityRingsLayout, double>(nameof(SideMinWidth), 340);

    static ActivityRingsLayout()
    {
        AffectsMeasure<ActivityRingsLayout>(SpacingProperty, SideMinWidthProperty);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double SideMinWidth
    {
        get => GetValue(SideMinWidthProperty);
        set => SetValue(SideMinWidthProperty, value);
    }

    /// <summary>Whether the last measure put the legend beside the rings.</summary>
    internal bool IsSideBySide { get; private set; }

    private Control? Rings => Children.Count > 0 ? Children[0] : null;

    private Control? Legend => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var rings = Rings;
        if (rings is null)
        {
            return default;
        }
        var legend = Legend;
        var spacing = Spacing;
        IsSideBySide = legend is not null && (double.IsInfinity(availableSize.Width) || availableSize.Width >= SideMinWidth);
        if (legend is null)
        {
            rings.Measure(availableSize);
            return rings.DesiredSize;
        }
        if (IsSideBySide)
        {
            rings.Measure(availableSize);
            var rest = double.IsInfinity(availableSize.Width)
                ? double.PositiveInfinity
                : Math.Max(0, availableSize.Width - rings.DesiredSize.Width - spacing);
            legend.Measure(new Size(rest, availableSize.Height));
            return new Size(rings.DesiredSize.Width + spacing + legend.DesiredSize.Width,
                Math.Max(rings.DesiredSize.Height, legend.DesiredSize.Height));
        }
        rings.Measure(availableSize);
        legend.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return new Size(Math.Max(rings.DesiredSize.Width, legend.DesiredSize.Width),
            rings.DesiredSize.Height + spacing + legend.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rings = Rings;
        if (rings is null)
        {
            return finalSize;
        }
        var legend = Legend;
        var ringsSize = rings.DesiredSize;
        if (legend is null)
        {
            rings.Arrange(new Rect(new Point((finalSize.Width - ringsSize.Width) / 2, (finalSize.Height - ringsSize.Height) / 2), ringsSize));
            return finalSize;
        }
        var spacing = Spacing;
        if (IsSideBySide)
        {
            rings.Arrange(new Rect(new Point(0, (finalSize.Height - ringsSize.Height) / 2), ringsSize));
            var x = ringsSize.Width + spacing;
            var height = legend.DesiredSize.Height;
            legend.Arrange(new Rect(x, (finalSize.Height - height) / 2, Math.Max(0, finalSize.Width - x), height));
            return finalSize;
        }
        rings.Arrange(new Rect(new Point((finalSize.Width - ringsSize.Width) / 2, 0), ringsSize));
        legend.Arrange(new Rect(0, ringsSize.Height + spacing, finalSize.Width, legend.DesiredSize.Height));
        return finalSize;
    }
}
