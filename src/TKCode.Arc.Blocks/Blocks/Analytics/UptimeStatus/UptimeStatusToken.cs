using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="UptimeStatus"/>, measured from the Analytics group's Uptime status (design reference only, ADR 0024;
/// notes section 3). Colours come from ArcDesign tokens in the theme or are derived from them in code (the status colours
/// are mixed from Success / Warning / Danger and the surface).
/// </summary>
[ControlDesignToken]
internal sealed class UptimeStatusToken : AbstractControlDesignToken
{
    /// <summary>The header: padding (20, 16), compact (16, 14); the status icon is 28.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness CompactHeaderPadding { get; set; }

    public double StatusIconSize { get; set; }

    /// <summary>A section's padding (20, 16, 20, 20); compact (16, 14, 16, 16).</summary>
    public Thickness SectionPadding { get; set; }

    public Thickness CompactSectionPadding { get; set; }

    /// <summary>The bar band's height (34), the bar radius (1.5), the heights of degraded and outage bars (65%, 35%) and the dim of the other bars while one is inspected (0.38).</summary>
    public double StripHeight { get; set; }

    public double BarRadius { get; set; }

    public double DegradedBarFraction { get; set; }

    public double OutageBarFraction { get; set; }

    public double DimmedBarOpacity { get; set; }

    /// <summary>A service row: radius 10, padding (10, 8, 10, 10), 6 between the name line and the bars.</summary>
    public CornerRadius RowRadius { get; set; }

    public Thickness RowPadding { get; set; }

    public double RowGap { get; set; }

    /// <summary>The latency chart's height (216).</summary>
    public double ChartHeight { get; set; }

    /// <summary>The ratio of the lower sections' columns (response time : incidents = 1.2 : 1).</summary>
    public double ResponseColumnWeight { get; set; }

    /// <summary>The block's own width below which the lower sections stack (860) and the layout is compact (560).</summary>
    public double StackedBreakpoint { get; set; }

    public double CompactBreakpoint { get; set; }

    /// <summary>The status badge: 22 high, padding (8, 0), icon 12, 4 between icon and text.</summary>
    public double BadgeHeight { get; set; }

    public Thickness BadgePadding { get; set; }

    /// <summary>An incident's header margin (10 above and below) and the timeline dot (8).</summary>
    public Thickness IncidentHeaderMargin { get; set; }

    public double TimelineDotSize { get; set; }

    /// <summary>The lead's avatar (28).</summary>
    public double AvatarSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderPadding = new Thickness(20, 16);
        CompactHeaderPadding = new Thickness(16, 14);
        StatusIconSize = 28;
        SectionPadding = new Thickness(20, 16, 20, 20);
        CompactSectionPadding = new Thickness(16, 14, 16, 16);
        StripHeight = 34;
        BarRadius = 1.5;
        DegradedBarFraction = 0.65;
        OutageBarFraction = 0.35;
        DimmedBarOpacity = 0.38;
        RowRadius = new CornerRadius(10);
        RowPadding = new Thickness(10, 8, 10, 10);
        RowGap = 6;
        ChartHeight = 216;
        ResponseColumnWeight = 1.2;
        StackedBreakpoint = 860;
        CompactBreakpoint = 560;
        BadgeHeight = 22;
        BadgePadding = new Thickness(8, 0);
        IncidentHeaderMargin = new Thickness(0, 10);
        TimelineDotSize = 8;
        AvatarSize = 28;
    }
}
