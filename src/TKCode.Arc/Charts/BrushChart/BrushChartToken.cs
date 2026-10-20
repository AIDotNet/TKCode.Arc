using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>BrushChart geometry (Arc: 44px value gutter, 28px date axis, the overview 8px under the axis and as wide as the plot).</summary>
[ControlDesignToken]
internal sealed class BrushChartToken : AbstractControlDesignToken
{
    /// <summary>Width of the right-hand value axis.</summary>
    public double GutterWidth { get; set; }

    /// <summary>Height of the date axis row.</summary>
    public double AxisRowHeight { get; set; }

    /// <summary>The overview sits 8px under the date axis and stops at the value gutter.</summary>
    public Thickness OverviewMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GutterWidth = 44;
        AxisRowHeight = 28;
        OverviewMargin = new Thickness(0, 8, 44, 0);
    }
}
