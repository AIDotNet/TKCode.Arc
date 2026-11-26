using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry and backdrop of <see cref="SideDrawer"/>: default width, max width ratio, content padding and scrim.</summary>
[ControlDesignToken]
internal sealed class SideDrawerToken : AbstractControlDesignToken
{
    /// <summary>Scrim color behind the drawer panel.</summary>
    public Color ScrimColor { get; set; }

    /// <summary>Default drawer width in px: 280.</summary>
    public double DefaultWidth { get; set; }

    /// <summary>Maximum fraction of the viewport width the drawer may occupy: 0.85.</summary>
    public double MaxWidthRatio { get; set; }

    /// <summary>Padding around the drawer's body content: 0.</summary>
    public Thickness ContentInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        ScrimColor = isDarkMode ? Color.FromArgb(148, 0, 0, 0) : Color.FromArgb(96, 0, 0, 0);
        DefaultWidth = 280;
        MaxWidthRatio = 0.85;
        ContentInset = default;
    }
}
