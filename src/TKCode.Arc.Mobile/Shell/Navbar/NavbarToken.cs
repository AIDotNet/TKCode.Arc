using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Navbar"/>: a 44px bar, 8px side padding, 44px minimum leading and trailing slots.</summary>
[ControlDesignToken]
internal sealed class NavbarToken : AbstractControlDesignToken
{
    /// <summary>The bar's height: 44.</summary>
    public double BarHeight { get; set; }

    /// <summary>Padding at the bar's sides: 8.</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Smallest width of the leading and trailing slots, so their buttons stay touchable: 44.</summary>
    public double SlotMinWidth { get; set; }

    /// <summary>Hairline under the bar: 1.</summary>
    public Thickness BarBorderThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarHeight = 44;
        BarPadding = new Thickness(8, 0);
        SlotMinWidth = 44;
        BarBorderThickness = new Thickness(0, 0, 0, 1);
    }
}
