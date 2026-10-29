using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="KpiDrilldown"/>, measured from the Analytics group's KPI drilldown (design reference only,
/// ADR 0024; notes sections 3 and 8). Colours follow ArcDesign tokens in the theme or are derived from them in code
/// (the Compare toggle's edge, the chart's accent fills); the compact values apply below a block width of 520.
/// </summary>
[ControlDesignToken]
internal sealed class KpiDrilldownToken : AbstractControlDesignToken
{
    /// <summary>The header: padding (16, 12) and compact 12; 8 between its controls.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness CompactHeaderPadding { get; set; }

    public double HeaderControlsGap { get; set; }

    /// <summary>The card area's padding (16; compact 12) and the gap between cards (12; compact 10).</summary>
    public double StagePadding { get; set; }

    public double CompactStagePadding { get; set; }

    public double CardGap { get; set; }

    public double CompactCardGap { get; set; }

    /// <summary>The smallest height of the card rows when there are several columns (548: two rows of 268).</summary>
    public double CardRowsMinHeight { get; set; }

    /// <summary>A card: radius 14, padding 16, 6 between label, value row and sparkline; the sparkline at least 44 high.</summary>
    public CornerRadius KpiCardRadius { get; set; }

    public Thickness KpiCardPadding { get; set; }

    public double KpiCardRowGap { get; set; }

    public double SparklineMinHeight { get; set; }

    /// <summary>The value's size (28 on a 30.8 line; compact 22 on 24.2) and letter spacing (−0.03em).</summary>
    public double KpiValueSize { get; set; }

    public double CompactKpiValueSize { get; set; }

    /// <summary>The detail panel: inset 8 inside the card area, padding (16, 15, 16, 16).</summary>
    public double DetailInset { get; set; }

    public Thickness DetailPadding { get; set; }

    /// <summary>The detail chart's height (220).</summary>
    public double TrendHeight { get; set; }

    /// <summary>The Compare toggle and the switches: 34 high; the toggle's radius 9 and padding (10, 0); icon-only 36 wide.</summary>
    public double ToolbarItemHeight { get; set; }

    public CornerRadius CompareRadius { get; set; }

    public Thickness ComparePadding { get; set; }

    public double CompareIconOnlyWidth { get; set; }

    /// <summary>The close button: 30 square, radius 9.</summary>
    public double CloseSize { get; set; }

    public CornerRadius CloseRadius { get; set; }

    /// <summary>Breakdown columns: Value 96 (compact 76), Change 88 (compact 68), the percent 44; the name takes 30%.</summary>
    public double BreakdownValueWidth { get; set; }

    public double CompactBreakdownValueWidth { get; set; }

    public double BreakdownChangeWidth { get; set; }

    public double CompactBreakdownChangeWidth { get; set; }

    public double BreakdownPercentWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderPadding = new Thickness(16, 12);
        CompactHeaderPadding = new Thickness(12);
        HeaderControlsGap = 8;
        StagePadding = 16;
        CompactStagePadding = 12;
        CardGap = 12;
        CompactCardGap = 10;
        CardRowsMinHeight = 548;
        KpiCardRadius = new CornerRadius(14);
        KpiCardPadding = new Thickness(16);
        KpiCardRowGap = 6;
        SparklineMinHeight = 44;
        KpiValueSize = 28;
        CompactKpiValueSize = 22;
        DetailInset = 8;
        DetailPadding = new Thickness(16, 15, 16, 16);
        TrendHeight = 220;
        ToolbarItemHeight = 34;
        CompareRadius = new CornerRadius(9);
        ComparePadding = new Thickness(10, 0);
        CompareIconOnlyWidth = 36;
        CloseSize = 30;
        CloseRadius = new CornerRadius(9);
        BreakdownValueWidth = 96;
        CompactBreakdownValueWidth = 76;
        BreakdownChangeWidth = 88;
        CompactBreakdownChangeWidth = 68;
        BreakdownPercentWidth = 44;
    }
}
