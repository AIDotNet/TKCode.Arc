using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the map colors of <see cref="DeliveryTracker"/>.</summary>
[ControlDesignToken]
internal sealed class DeliveryTrackerToken : AbstractControlDesignToken
{
    /// <summary>Space inside the block's card.</summary>
    public Thickness BlockPadding { get; set; }

    /// <summary>Gap between the block's sections.</summary>
    public double SectionGap { get; set; }

    /// <summary>Height of the route drawing.</summary>
    public double RouteHeight { get; set; }

    /// <summary>Width of the route beside the history.</summary>
    public double RouteWidth { get; set; }

    /// <summary>Space around the route beside the history (the gap is on its right).</summary>
    public Thickness RouteMargin { get; set; }

    /// <summary>Space around the route when it sits above the history.</summary>
    public Thickness RouteStackedMargin { get; set; }

    /// <summary>Block width below which the route sits above the history.</summary>
    public double CompactThreshold { get; set; }

    /// <summary>The map's ground (a muted tint of the page, accent independent).</summary>
    public Color GroundColor { get; set; }

    /// <summary>The map's streets.</summary>
    public Color StreetColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        BlockPadding = new Thickness(24);
        SectionGap = 22;
        RouteHeight = 220;
        RouteWidth = 380;
        RouteMargin = new Thickness(0, 0, 24, 0);
        RouteStackedMargin = new Thickness(0, 0, 0, 20);
        CompactThreshold = 720;
        GroundColor = Oklch.MixOklab(n.Background, n.Foreground, isDarkMode ? 0.06 : 0.04);
        StreetColor = Oklch.MixOklab(n.Background, n.Foreground, isDarkMode ? 0.13 : 0.09);
    }
}
