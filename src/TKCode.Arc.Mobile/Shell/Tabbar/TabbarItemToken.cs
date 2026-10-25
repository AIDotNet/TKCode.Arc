using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="TabbarItem"/>: a 44px minimum touch target, a 24px icon above a small label.</summary>
[ControlDesignToken]
internal sealed class TabbarItemToken : AbstractControlDesignToken
{
    /// <summary>Smallest height of an item: 44.</summary>
    public double TouchTarget { get; set; }

    /// <summary>The icon's size: 24.</summary>
    public double IconExtent { get; set; }

    /// <summary>Gap between icon and label: 2.</summary>
    public double IconGap { get; set; }

    /// <summary>Opacity while a finger is on the item: 0.6.</summary>
    public double PressedOpacity { get; set; }

    /// <summary>Padding inside an item: 4.</summary>
    public Thickness ItemPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TouchTarget = 44;
        IconExtent = 24;
        IconGap = 2;
        PressedOpacity = 0.6;
        ItemPadding = new Thickness(4);
    }
}
