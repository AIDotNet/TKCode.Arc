using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ModelTraining"/> (Arc Model training, design reference): the gaps between the diagram, the step
/// bar and the caption, the readout panel beside the diagram (232 wide, 32 apart, from 720 px) or under it, the stats,
/// the chart, the evaluation row and the step bar. Colours, radii and type come from the ArcDesign tokens in the theme;
/// the diagram is laid out from its measured width.
/// </summary>
[ControlDesignToken]
internal sealed class ModelTrainingToken : AbstractControlDesignToken
{
    /// <summary>No longer used (the illustration has no title line); kept for theme overrides.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>No longer used (the illustration has no header); kept for theme overrides.</summary>
    public double HeaderRowHeight { get; set; }

    /// <summary>No longer used (the caption sits under the step bar, <see cref="SectionGap"/> apart); kept for theme overrides.</summary>
    public Thickness CaptionMargin { get; set; }

    /// <summary>No longer used (the caption takes the lines it needs); kept for theme overrides.</summary>
    public double CaptionMinHeight { get; set; }

    /// <summary>The space between the diagram, the step bar and the caption (20).</summary>
    public double SectionGap { get; set; }

    /// <summary>No longer used (the step bar has one play button); kept for theme overrides.</summary>
    public double ButtonGap { get; set; }

    /// <summary>No longer used (the illustration has no speed control); kept for theme overrides.</summary>
    public double SpeedGap { get; set; }

    /// <summary>From this width the readout panel stands beside the diagram (720).</summary>
    public double WideWidth { get; set; }

    /// <summary>Under this width the step bar shows numbers only and the caption names the step (600).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The readout panel's width beside the diagram (232) and the gap to the diagram (32).</summary>
    public double PanelWidth { get; set; }

    public double PanelGap { get; set; }

    /// <summary>The gap between the diagram and the panel under it (24; 20 on compact cards).</summary>
    public double StackedGap { get; set; }

    public double CompactStackedGap { get; set; }

    /// <summary>The space between the panel's rows and between the two stats (16).</summary>
    public double PanelSpacing { get; set; }

    /// <summary>The stat values' size (22; 20 on compact cards) and the space in a stat (2).</summary>
    public double StatValueSize { get; set; }

    public double CompactStatValueSize { get; set; }

    public double StatSpacing { get; set; }

    /// <summary>The loss chart's height (112).</summary>
    public double ChartHeight { get; set; }

    /// <summary>The evaluation row: 16 above it under its rule, 12 between its columns, 8 between the score and the tag.</summary>
    public Thickness EvalPadding { get; set; }

    public double EvalColumnGap { get; set; }

    public double EvalTagGap { get; set; }

    /// <summary>The evaluation tag's padding (7 × 1) and the score's least width (41).</summary>
    public Thickness TagPadding { get; set; }

    public double ScoreMinWidth { get; set; }

    /// <summary>The step bar: 44 high, 16 after the play button (32), steps 8 apart, the label 7 above the 2 px track, 6 between number and name.</summary>
    public double StepBarHeight { get; set; }

    public double ControlsGap { get; set; }

    public double PlayButtonSize { get; set; }

    public double StepGap { get; set; }

    public double StepLabelGap { get; set; }

    public double StepNumberGap { get; set; }

    public double TrackHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TitleLineHeight = 17.6;
        HeaderRowHeight = 22.4;
        CaptionMargin = new Thickness(0, 4, 0, 0);
        CaptionMinHeight = 39.2;
        SectionGap = 20;
        ButtonGap = 6;
        SpeedGap = 12;
        WideWidth = 720;
        CompactWidth = 600;
        PanelWidth = 232;
        PanelGap = 32;
        StackedGap = 24;
        CompactStackedGap = 20;
        PanelSpacing = 16;
        StatValueSize = 22;
        CompactStatValueSize = 20;
        StatSpacing = 2;
        ChartHeight = 112;
        EvalPadding = new Thickness(0, 16, 0, 0);
        EvalColumnGap = 12;
        EvalTagGap = 8;
        TagPadding = new Thickness(7, 1);
        ScoreMinWidth = 41;
        StepBarHeight = 44;
        ControlsGap = 16;
        PlayButtonSize = 32;
        StepGap = 8;
        StepLabelGap = 7;
        StepNumberGap = 6;
        TrackHeight = 2;
    }
}
