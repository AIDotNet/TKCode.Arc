using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="UsageForecast"/>, measured from the Analytics group's usage forecast block (design reference
/// only, ADR 0024). Colours, radii, shadows and fonts come from ArcDesign tokens in the theme; the self-drawn chart reads
/// the same tokens through the palette. "Compact" values apply while the block is narrower than <see cref="CompactWidth"/>,
/// "narrow" ones under <see cref="NarrowWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class UsageForecastToken : AbstractControlDesignToken
{
    /// <summary>Block padding (16, 20, 20 with the header 16 from the top) and its compact form (16).</summary>
    public Thickness BlockPadding { get; set; }

    public Thickness CompactBlockPadding { get; set; }

    /// <summary>Space between the header, the headline, the tiles, the chart and the footer (20; compact 16).</summary>
    public double SectionGap { get; set; }

    public double CompactSectionGap { get; set; }

    /// <summary>Below this width the switch goes under the titles, the tiles tighten and the chart is 232 high (560).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Below this width the tiles stack and the chips fill their rows (440).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Frame radius of the card version (16).</summary>
    public CornerRadius FrameRadius { get; set; }

    /// <summary>Title: 16 on a 17.6 line, letter spacing -0.48; 2 between it and the subtitle (14 on 19.6).</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double SubtitleGap { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>Header gaps: 24 between the titles and the switch, 12 between wrapped lines.</summary>
    public double HeaderColumnGap { get; set; }

    public double HeaderRowGap { get; set; }

    /// <summary>Headline: 22 on 28.6, 500, letter spacing -0.22, at most 672 wide; 18 at the smallest; 4 above the detail line.</summary>
    public double HeadlineSize { get; set; }

    public double HeadlineMinSize { get; set; }

    public double HeadlineScale { get; set; }

    public double HeadlineLineHeight { get; set; }

    public double HeadlineMaxWidth { get; set; }

    public double HeadlineGap { get; set; }

    /// <summary>Tile value size (22; compact 18), unit size (16), small text size (12 on 16.8).</summary>
    public double TileValueSize { get; set; }

    public double CompactTileValueSize { get; set; }

    public double TileUnitSize { get; set; }

    public double TileSmallLineHeight { get; set; }

    /// <summary>Tile inline padding (20; compact 10) and the stacked tiles' vertical padding (10).</summary>
    public double TilePadding { get; set; }

    public double CompactTilePadding { get; set; }

    public double StackedTilePadding { get; set; }

    /// <summary>Chart height (264; compact 232) and its plot insets: left 52, right 10, top 30, bottom 30.</summary>
    public double ChartHeight { get; set; }

    public double CompactChartHeight { get; set; }

    public Thickness PlotInsets { get; set; }

    /// <summary>Axis label column (42), the budget band's height (32), the handle capsule (26 high, 8 from the left).</summary>
    public double AxisLabelWidth { get; set; }

    public double HandleBandHeight { get; set; }

    /// <summary>The handle band's margin: it spans the plot (52 left, 10 right).</summary>
    public Thickness HandleMargin { get; set; }

    public double HandleCapsuleHeight { get; set; }

    public double HandleCapsuleInset { get; set; }

    /// <summary>The inspect card: 176 wide, 14 from the crosshair, radius 14.</summary>
    public double PanelWidth { get; set; }

    public double PanelOffset { get; set; }

    public double PanelRadius { get; set; }

    /// <summary>Marker sizes: crossing circle radius 5, today dot radius 4, threshold tick length 14.</summary>
    public double CrossingRadius { get; set; }

    public double TodayDotRadius { get; set; }

    public double ThresholdTickLength { get; set; }

    /// <summary>The chip row: chip height 32, gaps 8, legend gaps 6 and 16 (compact 12); footer gap 12.</summary>
    public double ChipHeight { get; set; }

    public double ChipGap { get; set; }

    public double LegendRowGap { get; set; }

    public double LegendColumnGap { get; set; }

    public double CompactLegendColumnGap { get; set; }

    public double FooterGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockPadding = new Thickness(20, 16, 20, 20);
        CompactBlockPadding = new Thickness(16);
        SectionGap = 20;
        CompactSectionGap = 16;
        CompactWidth = 560;
        NarrowWidth = 440;
        FrameRadius = new CornerRadius(16);
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.48;
        SubtitleGap = 2;
        BodyLineHeight = 19.6;
        HeaderColumnGap = 24;
        HeaderRowGap = 12;
        HeadlineSize = 22;
        HeadlineMinSize = 18;
        HeadlineScale = 0.03;
        HeadlineLineHeight = 1.3;
        HeadlineMaxWidth = 672;
        HeadlineGap = 4;
        TileValueSize = 22;
        CompactTileValueSize = 18;
        TileUnitSize = 16;
        TileSmallLineHeight = 16.8;
        TilePadding = 20;
        CompactTilePadding = 10;
        StackedTilePadding = 10;
        ChartHeight = 264;
        CompactChartHeight = 232;
        PlotInsets = new Thickness(52, 30, 10, 30);
        AxisLabelWidth = 42;
        HandleBandHeight = 32;
        HandleMargin = new Thickness(52, 0, 10, 0);
        HandleCapsuleHeight = 26;
        HandleCapsuleInset = 8;
        PanelWidth = 176;
        PanelOffset = 14;
        PanelRadius = 14;
        CrossingRadius = 5;
        TodayDotRadius = 4;
        ThresholdTickLength = 14;
        ChipHeight = 32;
        ChipGap = 8;
        LegendRowGap = 6;
        LegendColumnGap = 16;
        CompactLegendColumnGap = 12;
        FooterGap = 12;
    }
}
