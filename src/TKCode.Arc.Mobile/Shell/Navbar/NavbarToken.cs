using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Navbar"/>: a 44px bar, a 30px large title, 8px side padding, 44px minimum leading and trailing slots.</summary>
[ControlDesignToken]
internal sealed class NavbarToken : AbstractControlDesignToken
{
    /// <summary>The bar's height, hairline included: 44.</summary>
    public double BarHeight { get; set; }

    /// <summary>Padding at the bar's sides: 8.</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Smallest width of the leading and trailing slots, so their buttons stay touchable: 44.</summary>
    public double SlotMinWidth { get; set; }

    /// <summary>Hairline under the bar: 1.</summary>
    public Thickness BarBorderThickness { get; set; }

    /// <summary>Size of the back button's chevron: 22.</summary>
    public double BackIconExtent { get; set; }

    /// <summary>Height of the bar's content row, the bar's height less its hairline: 43.</summary>
    public double BarContentHeight { get; set; }

    /// <summary>Font size of the large title: 30.</summary>
    public double LargeTitleFontSize { get; set; }

    /// <summary>Margin around the large title, which lines it up with the back button's text: 8 at the sides.</summary>
    public Thickness LargeTitleMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarHeight = 44;
        BarPadding = new Thickness(8, 0);
        SlotMinWidth = 44;
        BarContentHeight = BarHeight - 1;
        BackIconExtent = 22;
        LargeTitleFontSize = 30;
        LargeTitleMargin = new Thickness(8, 0);
        BarBorderThickness = new Thickness(0, 0, 0, 1);
    }
}
