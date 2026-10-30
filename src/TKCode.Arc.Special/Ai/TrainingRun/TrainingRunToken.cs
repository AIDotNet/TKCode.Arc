using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TrainingRun"/>: the card (radius, padding, the 640px cap), the head,
/// the chart (height, gutters, strokes, the epoch progress bar), the stats, the event log and the footer. Colors, fonts and
/// the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TrainingRunToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The card's padding: 20px, 16px under the breakpoint.</summary>
    public Thickness CardPadding { get; set; }

    public Thickness CardPaddingNarrow { get; set; }

    /// <summary>The widest the card grows (it fills its container up to here); under 440px it goes narrow.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The space between the card's sections.</summary>
    public double SectionGap { get; set; }

    /// <summary>The head: the model mark (24px), the gap after it, the title and the line under it.</summary>
    public double MarkSize { get; set; }

    public CornerRadius MarkCornerRadius { get; set; }

    public double HeadGap { get; set; }

    public double TitleFontSize { get; set; }

    public double SubtitleFontSize { get; set; }

    /// <summary>The status at the right of the head: icon size and gap.</summary>
    public double StatusGlyphSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>The legend row over the chart: its type and the swatch.</summary>
    public double LegendFontSize { get; set; }

    public double LegendSwatchSize { get; set; }

    /// <summary>The chart: its whole height (plot, progress bar and epoch labels), the y gutter and the axis labels.</summary>
    public double ChartHeight { get; set; }

    public double ChartGutter { get; set; }

    public double ChartLabelFontSize { get; set; }

    /// <summary>The smoothed train and the validation stroke; the raw train stroke and its opacity.</summary>
    public double CurveStrokeWidth { get; set; }

    public double RawStrokeWidth { get; set; }

    public double RawOpacity { get; set; }

    /// <summary>The live head dot and the validation sample dots.</summary>
    public double HeadDotRadius { get; set; }

    public double SampleDotRadius { get; set; }

    /// <summary>The epoch bands behind the plot (every other epoch, SurfaceMuted at this opacity).</summary>
    public double BandOpacity { get; set; }

    /// <summary>The epoch progress bar under the plot: its height, the gap between epoch segments and its distance from the plot.</summary>
    public double ProgressHeight { get; set; }

    public double EpochGap { get; set; }

    public double ProgressMargin { get; set; }

    /// <summary>The stats: label and value type, the gaps of the grid.</summary>
    public double StatLabelFontSize { get; set; }

    public double StatValueFontSize { get; set; }

    public double StatHintFontSize { get; set; }

    /// <summary>The space around a stat (right and below), so wrapped rows of two stay apart.</summary>
    public Thickness StatMargin { get; set; }

    /// <summary>The hint line under a stat, reserved even when empty.</summary>
    public double StatHintHeight { get; set; }

    /// <summary>Where the activity line sits over the empty chart (over the plot, clear of the y gutter and the axis).</summary>
    public Thickness ActivityMargin { get; set; }

    /// <summary>The event log: row height (three rows show), glyph size and type.</summary>
    public double EventRowHeight { get; set; }

    public double EventGlyphSize { get; set; }

    public double EventFontSize { get; set; }

    /// <summary>The footer: the dataset mark (14px), its type and the action buttons' height.</summary>
    public double DatasetMarkSize { get; set; }

    public double FooterFontSize { get; set; }

    /// <summary>The footer's hairline gap above, and the gap between context and actions when it stacks.</summary>
    public Thickness FooterPadding { get; set; }

    public Thickness FooterStackGap { get; set; }

    public double ActionHeight { get; set; }

    public Thickness ActionInlinePadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(20);
        CardPaddingNarrow = new Thickness(16);
        CardMaxWidth = 640;
        SectionGap = 16;
        MarkSize = 24;
        MarkCornerRadius = new CornerRadius(7);
        HeadGap = 12;
        TitleFontSize = 15;
        SubtitleFontSize = 13;
        StatusGlyphSize = 14;
        StatusGap = 6;
        LegendFontSize = 12;
        LegendSwatchSize = 8;
        ChartHeight = 176;
        ChartGutter = 30;
        ChartLabelFontSize = 11;
        CurveStrokeWidth = 2;
        RawStrokeWidth = 1;
        RawOpacity = 0.3;
        HeadDotRadius = 3.5;
        SampleDotRadius = 2.5;
        BandOpacity = 0.55;
        ProgressHeight = 4;
        EpochGap = 3;
        ProgressMargin = 8;
        StatLabelFontSize = 12;
        StatValueFontSize = 15;
        StatHintFontSize = 12;
        StatMargin = new Thickness(0, 0, 12, 4);
        StatHintHeight = 16;
        ActivityMargin = new Thickness(30, 0, 0, 30);
        EventRowHeight = 28;
        EventGlyphSize = 14;
        EventFontSize = 13;
        DatasetMarkSize = 14;
        FooterFontSize = 13;
        FooterPadding = new Thickness(0, 14, 0, 0);
        FooterStackGap = new Thickness(0, 10, 0, 0);
        ActionHeight = 32;
        ActionInlinePadding = new Thickness(12, 0);
    }
}
