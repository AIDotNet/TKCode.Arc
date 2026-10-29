using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FloatingButtonGroupToken : AbstractControlDesignToken
{
    /// <summary>Tray padding (Arc <c>--fbg-pad</c>: 4 / 3px).</summary>
    public Thickness TrayInsetMD { get; set; }
    public Thickness TrayInsetSM { get; set; }

    /// <summary>Gap between buttons (Arc <c>--fbg-gap</c>: 4 / 3px).</summary>
    public double ItemGapMD { get; set; }
    public double ItemGapSM { get; set; }

    /// <summary>Tray corner radius (radius-control, minus 3px when small).</summary>
    public CornerRadius TrayRadiusMD { get; set; }
    public CornerRadius TrayRadiusSM { get; set; }

    /// <summary>Highlight radius, concentric with the tray (outer − pad − 1).</summary>
    public CornerRadius HighlightRadiusMD { get; set; }
    public CornerRadius HighlightRadiusSM { get; set; }

    /// <summary>Muted tray: foreground at 4.5% (6% dark), a tint that still reads on muted surfaces.</summary>
    public Color TrayColor { get; set; }

    /// <summary>Muted tray edge: foreground at 3% (5% dark).</summary>
    public Color TrayEdgeColor { get; set; }

    /// <summary>Shared highlight: foreground at 7% (9% dark).</summary>
    public Color HighlightColor { get; set; }

    /// <summary>Highlight while pressing: foreground at 12% (15% dark).</summary>
    public Color HighlightPressColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        TrayInsetMD = new Thickness(4);
        TrayInsetSM = new Thickness(3);
        ItemGapMD = 4;
        ItemGapSM = 3;
        TrayRadiusMD = new CornerRadius(18);
        TrayRadiusSM = new CornerRadius(15);
        HighlightRadiusMD = new CornerRadius(13);
        HighlightRadiusSM = new CornerRadius(11);
        TrayColor = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.06 : 0.045);
        TrayEdgeColor = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.05 : 0.03);
        HighlightColor = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.09 : 0.07);
        HighlightPressColor = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.15 : 0.12);
    }
}
