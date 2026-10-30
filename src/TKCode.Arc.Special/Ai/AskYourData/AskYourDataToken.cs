using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AskYourData"/>: the card, the Generated SQL panel and its code, the stage with its header, the
/// chart area, the legend, the insight, the running count and the suggestion chips. Colors, fonts and the focus ring
/// come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AskYourDataToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius and padding, and the space between its sections.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    public Thickness CardPadding { get; set; }

    public double SectionGap { get; set; }

    /// <summary>The source chip at the start of the field: its glyph and the gap to the divider.</summary>
    public double SourceGlyphSize { get; set; }

    /// <summary>The Ask button inside the field: square, its glyph.</summary>
    public double AskSize { get; set; }

    public double AskGlyphSize { get; set; }

    /// <summary>The Generated SQL panel: its radius, the row's height, the code's well and type.</summary>
    public CornerRadius SqlCornerRadius { get; set; }

    public double SqlRowHeight { get; set; }

    public Thickness SqlRowPadding { get; set; }

    public CornerRadius SqlWellCornerRadius { get; set; }

    public Thickness SqlWellMargin { get; set; }

    public Thickness SqlPadding { get; set; }

    public double SqlFontSize { get; set; }

    public double SqlLineHeight { get; set; }

    /// <summary>The SQL row's fill under the pointer: SurfaceMuted at this opacity.</summary>
    public double HoverOpacity { get; set; }

    /// <summary>The stage: its least height (so states trade places without the card jumping) and its header.</summary>
    public double StageMinHeight { get; set; }

    public double HeaderMinHeight { get; set; }

    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double SubtitleFontSize { get; set; }

    /// <summary>The fixed area the bars, lines and table share.</summary>
    public double ChartHeight { get; set; }

    /// <summary>The legend: the swatch and the space between entries.</summary>
    public double SwatchSize { get; set; }

    public double LegendGap { get; set; }

    /// <summary>The insight: its type and the two lines it reserves.</summary>
    public double InsightLineHeight { get; set; }

    public double InsightHeight { get; set; }

    /// <summary>The rows scanned while the query runs.</summary>
    public double CountFontSize { get; set; }

    /// <summary>The suggestion chips: height and side padding.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    /// <summary>The error well's padding.</summary>
    public Thickness ErrorPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(16);
        SectionGap = 12;
        SourceGlyphSize = 15;
        AskSize = 32;
        AskGlyphSize = 16;
        SqlCornerRadius = new CornerRadius(14);
        SqlRowHeight = 40;
        SqlRowPadding = new Thickness(12, 0, 6, 0);
        SqlWellCornerRadius = new CornerRadius(10);
        SqlWellMargin = new Thickness(6, 0, 6, 6);
        SqlPadding = new Thickness(14, 10);
        SqlFontSize = 12.5;
        SqlLineHeight = 20;
        HoverOpacity = 0.6;
        StageMinHeight = 340;
        HeaderMinHeight = 40;
        TitleFontSize = 15;
        TitleLineHeight = 22;
        SubtitleFontSize = 12;
        ChartHeight = 200;
        SwatchSize = 8;
        LegendGap = 14;
        InsightLineHeight = 20;
        InsightHeight = 2 * InsightLineHeight;
        CountFontSize = 13;
        ChipHeight = 30;
        ChipPadding = new Thickness(12, 0);
        ErrorPadding = new Thickness(12, 10);
    }
}
