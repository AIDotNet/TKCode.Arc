using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Token of <see cref="LiquidTabBar"/> and its tabs. The track, lens, glyph, label and badge colors, the fonts and the
/// radii come from the ArcDesign tokens in the theme; the tab geometry is in <c>LiquidTabBarMetrics</c>.
/// </summary>
[ControlDesignToken]
internal sealed class LiquidTabBarToken : AbstractControlDesignToken
{
    /// <summary>Margin of a tab's keyboard focus ring: 2px outside the tab, 2px wide.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Font size of the badge digits.</summary>
    public double BadgeFontSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FocusRingMargin = new Thickness(-4);
        BadgeFontSize = 11;
    }
}
