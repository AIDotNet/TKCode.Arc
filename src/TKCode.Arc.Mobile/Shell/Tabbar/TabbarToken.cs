using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Tabbar"/>: a 49px bar with a hairline above it.</summary>
[ControlDesignToken]
internal sealed class TabbarToken : AbstractControlDesignToken
{
    /// <summary>The bar's height: 49.</summary>
    public double BarHeight { get; set; }

    /// <summary>Hairline above the bar: 1.</summary>
    public Thickness BarBorderThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarHeight = 49;
        BarBorderThickness = new Thickness(0, 1, 0, 0);
    }
}
