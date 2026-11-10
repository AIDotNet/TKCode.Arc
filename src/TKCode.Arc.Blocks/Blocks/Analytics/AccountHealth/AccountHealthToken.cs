using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="AccountHealth"/>. Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class AccountHealthToken : AbstractControlDesignToken
{
    /// <summary>At or below this width the tiles stack and the lists move under each other (720).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The block's padding (24) and its compact form (16).</summary>
    public Thickness Padding { get; set; }

    public Thickness CompactPadding { get; set; }

    /// <summary>A tile's padding (16).</summary>
    public Thickness TilePadding { get; set; }

    /// <summary>The health trend's height (40) and the usage chart's plot height (160).</summary>
    public double TrendHeight { get; set; }

    public double UsagePlotHeight { get; set; }

    /// <summary>The healthy band starts at this score (75) and the watch band at this one (50).</summary>
    public double HealthyFrom { get; set; }

    public double WatchFrom { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CompactWidth = 720;
        Padding = new Thickness(24);
        CompactPadding = new Thickness(16);
        TilePadding = new Thickness(16);
        TrendHeight = 40;
        UsagePlotHeight = 160;
        HealthyFrom = 75;
        WatchFrom = 50;
    }
}
