using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class BreadcrumbToken : AbstractControlDesignToken
{
    /// <summary>Gap between crumbs (Arc: 6px, the same as inside one crumb).</summary>
    public double CrumbGap { get; set; }

    /// <summary>Gap between wrapped lines (Arc: 2px).</summary>
    public double LineGap { get; set; }

    /// <summary>Overflow menu padding (Arc dropdown menu: 5px).</summary>
    public Thickness MenuPadding { get; set; }

    /// <summary>Arc dropdown menu: min-width 12rem.</summary>
    public double MenuMinWidth { get; set; }

    /// <summary>Arc dropdown menu item: min-height 36px.</summary>
    public double MenuItemMinHeight { get; set; }

    public Thickness MenuItemPadding { get; set; }

    /// <summary>radius-panel − 6px.</summary>
    public CornerRadius MenuItemRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CrumbGap = 6;
        LineGap = 2;
        MenuPadding = new Thickness(5);
        MenuMinWidth = 192;
        MenuItemMinHeight = 36;
        MenuItemPadding = new Thickness(11, 0);
        MenuItemRadius = new CornerRadius(20);
    }
}
