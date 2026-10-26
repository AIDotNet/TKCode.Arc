using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Toolbar"/>: a 44px bar with a hairline above it and 8px side padding.</summary>
[ControlDesignToken]
internal sealed class ToolbarToken : AbstractControlDesignToken
{
    /// <summary>The bar's height, hairline included: 44.</summary>
    public double BarHeight { get; set; }

    /// <summary>Hairline above the bar: 1.</summary>
    public Thickness BarBorderThickness { get; set; }

    /// <summary>Padding at the bar's sides: 8.</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Height of the bar's content row, the bar's height less its hairline: 43.</summary>
    public double BarContentHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarHeight = 44;
        BarContentHeight = BarHeight - 1;
        BarPadding = new Thickness(8, 0);
        BarBorderThickness = new Thickness(0, 1, 0, 0);
    }
}
