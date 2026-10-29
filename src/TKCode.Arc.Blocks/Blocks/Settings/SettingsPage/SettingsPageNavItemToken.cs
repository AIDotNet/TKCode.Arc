using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the hover tint of <see cref="SettingsPageNavItem"/>.</summary>
[ControlDesignToken]
internal sealed class SettingsPageNavItemToken : AbstractControlDesignToken
{
    public double ItemHeight { get; set; }

    /// <summary>Space inside an item around its icon, header, badge and dot.</summary>
    public Thickness ItemInset { get; set; }

    public CornerRadius ItemRadius { get; set; }

    public double GlyphSize { get; set; }

    /// <summary>Gap after the icon.</summary>
    public Thickness IconMargin { get; set; }

    /// <summary>Gap before the badge and the unsaved-changes dot.</summary>
    public Thickness TrailingMargin { get; set; }

    public double DirtyDotSize { get; set; }

    /// <summary>An unselected item under the pointer: the foreground at 4%.</summary>
    public Color HoverBackground { get; set; }

    /// <summary>An unselected item while pressed: the foreground at 7%.</summary>
    public Color PressedBackground { get; set; }

    /// <summary>The focus ring sits this far outside the item.</summary>
    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ItemHeight = 40;
        ItemInset = new Thickness(12, 0);
        ItemRadius = new CornerRadius(12);
        GlyphSize = 18;
        IconMargin = new Thickness(0, 0, 12, 0);
        TrailingMargin = new Thickness(8, 0, 0, 0);
        DirtyDotSize = 6;
        HoverBackground = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.06 : 0.04);
        PressedBackground = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.09 : 0.07);
        FocusRingMargin = new Thickness(-3);
        FocusRingRadius = new CornerRadius(15);
    }
}
