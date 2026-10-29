using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class UsageMeterToken : AbstractControlDesignToken
{
    /// <summary>Track and free swatch: 7% foreground into surface (accent independent).</summary>
    public Color TrackColor { get; set; }

    /// <summary>Badge backgrounds: 14% warning / 12% danger into surface.</summary>
    public Color NearBadgeBackground { get; set; }

    public Color OverBadgeBackground { get; set; }

    public double BadgeHeight { get; set; }
    public Thickness BadgePadding { get; set; }
    public double TopMinHeight { get; set; }
    public Thickness LegendMargin { get; set; }
    public Thickness LegendItemPadding { get; set; }
    public CornerRadius LegendItemRadius { get; set; }
    public Thickness LegendValueMargin { get; set; }
    public double LegendDimOpacity { get; set; }
    public Thickness CaptionMargin { get; set; }

    public CornerRadius LegendFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LegendFocusRingRadius = new CornerRadius(16);
        var n = ArcPalette.Neutrals(isDarkMode);
        TrackColor = Oklch.MixOklab(n.Surface, n.Foreground, 0.07);
        NearBadgeBackground = Oklch.MixOklab(n.Surface, n.Warning, 0.14);
        OverBadgeBackground = Oklch.MixOklab(n.Surface, n.Danger, 0.12);
        BadgeHeight = 28;
        BadgePadding = new Thickness(12, 0);
        TopMinHeight = 28;
        LegendMargin = new Thickness(-8, 0);
        LegendItemPadding = new Thickness(8);
        LegendItemRadius = new CornerRadius(12);
        LegendValueMargin = new Thickness(20, 0, 0, 0);
        LegendDimOpacity = 0.45;
        CaptionMargin = new Thickness(0, 0, 0, 5);
    }
}
