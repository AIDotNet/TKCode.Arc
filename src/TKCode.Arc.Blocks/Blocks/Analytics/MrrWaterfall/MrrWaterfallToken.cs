using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MrrWaterfall"/>, measured from the Analytics group's MRR waterfall block (design reference only,
/// ADR 0024). Colors, the type ramp, the radius and the fonts come from ArcDesign tokens in the theme. The chart's own
/// numbers (plot padding 48 / 30 / 34, bar width min(56, 56% of the band), the 2px marker) are rules of
/// <see cref="MrrWaterfallEngine"/>.
/// </summary>
[ControlDesignToken]
internal sealed class MrrWaterfallToken : AbstractControlDesignToken
{
    /// <summary>Block padding (20) and the gap between the header, the summary and the body (20).</summary>
    public Thickness BlockPadding { get; set; }

    public double SectionGap { get; set; }

    /// <summary>Below this content width the panel goes under the chart (800).</summary>
    public double StackedWidth { get; set; }

    /// <summary>Below this content width the headline and statistics shrink and the statistics share the row (560).</summary>
    public double CompactSummaryWidth { get; set; }

    /// <summary>Body gap between the chart and the panel (24) and the panel's width (264).</summary>
    public double BodyGap { get; set; }

    public double PanelWidth { get; set; }

    /// <summary>The panel's start padding beside its line (20) and its top padding when stacked (16).</summary>
    public double PanelInset { get; set; }

    public double StackedPanelInset { get; set; }

    /// <summary>Title: 16 on a 17.6 line, letter spacing −0.48; 2 between it and the subtitle (14 on 19.6).</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double SubtitleGap { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>Header gaps: 24 between the titles and the switches, 12 between wrapped lines; 8 between the switches.</summary>
    public double HeaderColumnGap { get; set; }

    public double HeaderRowGap { get; set; }

    public double SwitchGap { get; set; }

    /// <summary>Summary gaps: 32 between the headline and the statistics, 16 between wrapped lines.</summary>
    public double SummaryColumnGap { get; set; }

    public double SummaryRowGap { get; set; }

    /// <summary>Headline value size (28; compact 22) and statistic value size (18; compact 16).</summary>
    public double HeadlineValueSize { get; set; }

    public double CompactHeadlineValueSize { get; set; }

    public double StatValueSize { get; set; }

    public double CompactStatValueSize { get; set; }

    /// <summary>Padding beside a statistic's divider line (20).</summary>
    public double StatPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockPadding = new Thickness(20);
        SectionGap = 20;
        StackedWidth = 800;
        CompactSummaryWidth = 560;
        BodyGap = 24;
        PanelWidth = 264;
        PanelInset = 20;
        StackedPanelInset = 16;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.48;
        SubtitleGap = 2;
        BodyLineHeight = 19.6;
        HeaderColumnGap = 24;
        HeaderRowGap = 12;
        SwitchGap = 8;
        SummaryColumnGap = 32;
        SummaryRowGap = 16;
        HeadlineValueSize = 28;
        CompactHeadlineValueSize = 22;
        StatValueSize = 18;
        CompactStatValueSize = 16;
        StatPadding = 20;
    }
}
