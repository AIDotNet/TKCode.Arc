using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="GlassTabBar"/>: the inset between the floating glass bar and its tabs. The material comes from
/// <see cref="GlassPaneToken"/>; corners, shadows and colors from the ArcDesign tokens.
/// </summary>
[ControlDesignToken]
internal sealed class GlassTabBarToken : AbstractControlDesignToken
{
    /// <summary>Space between the bar's edge and the tabs (the lens sits in it).</summary>
    public Thickness BarInset { get; set; }

    /// <summary>How far the lens swells on every side while pressed or dragged.</summary>
    public double LensSwell { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarInset = new Thickness(5);
        LensSwell = 3;
    }
}

/// <summary>Geometry of one <see cref="GlassTab"/>: its size, icon, label and badge, and the keyboard focus ring.</summary>
[ControlDesignToken]
internal sealed class GlassTabToken : AbstractControlDesignToken
{
    /// <summary>Narrowest a tab gets.</summary>
    public double TabMinWidth { get; set; }

    /// <summary>Height of a tab with its label.</summary>
    public double TabHeight { get; set; }

    /// <summary>Height of a tab without labels (icons only).</summary>
    public double CompactTabHeight { get; set; }

    /// <summary>Space inside a tab.</summary>
    public Thickness TabPadding { get; set; }

    /// <summary>Size of a tab's icon.</summary>
    public double IconSize { get; set; }

    /// <summary>Gap between the icon and the label.</summary>
    public double LabelGap { get; set; }

    /// <summary>Font size of the label.</summary>
    public double LabelFontSize { get; set; }

    /// <summary>Height of a badge with a count; a badge without text is a dot of <see cref="BadgeDotSize"/>.</summary>
    public double BadgeHeight { get; set; }

    /// <summary>Diameter of a badge without text.</summary>
    public double BadgeDotSize { get; set; }

    /// <summary>Space inside a badge with a count.</summary>
    public Thickness BadgePadding { get; set; }

    /// <summary>Font size of a badge's count.</summary>
    public double BadgeFontSize { get; set; }

    /// <summary>Where the badge sits relative to the icon's top right corner.</summary>
    public Thickness BadgeMargin { get; set; }

    /// <summary>The focus ring sits this far outside the tab.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TabMinWidth = 64;
        TabHeight = 52;
        CompactTabHeight = 44;
        TabPadding = new Thickness(12, 0);
        IconSize = 22;
        LabelGap = 2;
        LabelFontSize = 11;
        BadgeHeight = 16;
        BadgeDotSize = 8;
        BadgePadding = new Thickness(4, 0);
        BadgeFontSize = 10;
        BadgeMargin = new Thickness(0, -4, -10, 0);
        FocusRingMargin = new Thickness(-2);
    }
}
