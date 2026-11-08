using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FluidTabControlToken : AbstractControlDesignToken
{
    /// <summary>Width of the tab list beside the sections.</summary>
    public double TabListWidth { get; set; }

    /// <summary>Padding of the tab list inside its column.</summary>
    public Thickness TabListPadding { get; set; }

    /// <summary>Space between two tabs in the list.</summary>
    public double ItemSpacing { get; set; }

    /// <summary>Space between two sections in the scroll area.</summary>
    public double SectionSpacing { get; set; }

    /// <summary>Padding around the stacked sections.</summary>
    public Thickness ContentPadding { get; set; }

    /// <summary>Divider between a left tab list and the sections (its right edge).</summary>
    public Thickness DividerLeftPlacement { get; set; }

    /// <summary>Divider between a right tab list and the sections (its left edge).</summary>
    public Thickness DividerRightPlacement { get; set; }

    /// <summary>Radius of the selection highlight behind the selected tab.</summary>
    public CornerRadius HighlightRadius { get; set; }

    /// <summary>Thickness of the side indicator on the selected tab.</summary>
    public double IndicatorThickness { get; set; }

    /// <summary>Space kept above and below the side indicator inside the selected tab.</summary>
    public double IndicatorInset { get; set; }

    public CornerRadius IndicatorRadius { get; set; }

    /// <summary>Length of the smooth scroll to a section (Arc considered duration).</summary>
    public TimeSpan ScrollDuration { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TabListWidth = 200;
        TabListPadding = new Thickness(8);
        ItemSpacing = 2;
        SectionSpacing = 40;
        ContentPadding = new Thickness(24);
        DividerLeftPlacement = new Thickness(0, 0, 1, 0);
        DividerRightPlacement = new Thickness(1, 0, 0, 0);
        HighlightRadius = new CornerRadius(12);
        IndicatorThickness = 3;
        IndicatorInset = 9;
        IndicatorRadius = new CornerRadius(1.5);
        ScrollDuration = TimeSpan.FromMilliseconds(480);
    }
}
