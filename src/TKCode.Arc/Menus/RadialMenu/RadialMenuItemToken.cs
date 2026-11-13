using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Content and resting fill of a <see cref="RadialMenuItem"/> sector.</summary>
[ControlDesignToken]
internal sealed class RadialMenuItemToken : AbstractControlDesignToken
{
    /// <summary>Size of a Lucide icon in a sector: 20.</summary>
    public double IconSize { get; set; }

    /// <summary>Space between the icon and the label: 3.</summary>
    public double ContentSpacing { get; set; }

    /// <summary>The least distance between the content and the edges of its sector: 6.</summary>
    public Thickness ContentPadding { get; set; }

    /// <summary>Opacity of a disabled sector's content: 0.4.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Fill of a resting sector on the plate: the foreground at 5% (6% in dark mode).</summary>
    public Color SectorFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        IconSize = 20;
        ContentSpacing = 3;
        ContentPadding = new Thickness(6);
        DisabledOpacity = 0.4;
        SectorFill = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.06 : 0.05);
    }
}
