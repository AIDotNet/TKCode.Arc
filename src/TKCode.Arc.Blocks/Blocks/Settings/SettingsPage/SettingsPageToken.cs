using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the navigation tint of <see cref="SettingsPage"/>.</summary>
[ControlDesignToken]
internal sealed class SettingsPageToken : AbstractControlDesignToken
{
    /// <summary>Width of the navigation column.</summary>
    public double NavWidth { get; set; }

    /// <summary>The navigation column: the surface with 2.5% of the foreground mixed in, a step below the content.</summary>
    public Color NavBackground { get; set; }

    /// <summary>Space around the page title in the navigation column.</summary>
    public Thickness TitleInset { get; set; }

    /// <summary>Space around the navigation items (room for their focus rings).</summary>
    public Thickness NavListInset { get; set; }

    /// <summary>Gap between navigation items.</summary>
    public double NavItemGap { get; set; }

    /// <summary>Corner radius of the selection pill (the items' radius).</summary>
    public CornerRadius NavPillRadius { get; set; }

    /// <summary>Space around the footer under its hairline.</summary>
    public Thickness FooterInset { get; set; }

    /// <summary>Space around the section content.</summary>
    public Thickness ContentInset { get; set; }

    /// <summary>The compact page's title, list and content insets.</summary>
    public Thickness CompactTitleInset { get; set; }

    public Thickness CompactNavListInset { get; set; }

    public Thickness CompactContentInset { get; set; }

    /// <summary>Room kept under the content while the save bar shows, so the bar never hides the last row.</summary>
    public double SaveBarRoom { get; set; }

    /// <summary>The save bar's distance from the bottom edge.</summary>
    public Thickness SaveBarMargin { get; set; }

    /// <summary>Space inside the save bar pill (its buttons sit close to the round ends).</summary>
    public Thickness SaveBarInset { get; set; }

    /// <summary>Gap between the note and the buttons in the save bar.</summary>
    public double SaveBarGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        NavWidth = 248;
        NavBackground = Oklch.MixOklab(n.Surface, n.Foreground, isDarkMode ? 0.02 : 0.025);
        TitleInset = new Thickness(24, 24, 24, 14);
        NavListInset = new Thickness(12, 2, 12, 12);
        NavItemGap = 2;
        NavPillRadius = new CornerRadius(12);
        FooterInset = new Thickness(16, 14);
        ContentInset = new Thickness(36, 30, 36, 36);
        CompactTitleInset = new Thickness(20, 18, 20, 10);
        CompactNavListInset = new Thickness(14, 2, 14, 12);
        CompactContentInset = new Thickness(20, 22, 20, 24);
        SaveBarRoom = 72;
        SaveBarMargin = new Thickness(0, 0, 0, 20);
        SaveBarInset = new Thickness(18, 5, 5, 5);
        SaveBarGap = 6;
    }
}
