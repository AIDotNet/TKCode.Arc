using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MetricsDashboard"/>, measured from Arc's Metrics dashboard (design reference only, ADR 0024).
/// Colors, the type ramp and the fonts come from ArcDesign tokens in the theme. Paddings are Avalonia thicknesses (left,
/// top, right, bottom); the medium layout applies under <see cref="MediumWidth"/>, the small one under <see cref="SmallWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class MetricsDashboardToken : AbstractControlDesignToken
{
    /// <summary>Under this width of the block the medium layout applies (640).</summary>
    public double MediumWidth { get; set; }

    /// <summary>Under this width of the block the small layout applies (460).</summary>
    public double SmallWidth { get; set; }

    /// <summary>Header padding (20, 16) and its small form (16 all round); 16 between the titles and the actions, 12 between wrapped rows, 8 between the actions.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness SmallHeaderPadding { get; set; }

    public double HeaderColumnGap { get; set; }

    public double HeaderRowGap { get; set; }

    public double ActionsGap { get; set; }

    /// <summary>The title: 16 on a 17.6 line, letter spacing −0.16; the subtitle 2 below it on a 19.6 line.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double SubtitleGap { get; set; }

    public double CopyLineHeight { get; set; }

    /// <summary>The Export button: 32 high, padding 10 / 12, radius 10, 14px glyph, 6 between glyph and text, pressed scale .97.</summary>
    public double ExportHeight { get; set; }

    public Thickness ExportPadding { get; set; }

    public CornerRadius ExportRadius { get; set; }

    public double ExportGlyphSize { get; set; }

    public double ExportGap { get; set; }

    /// <summary>A metric tab's padding: large (16, 14), the first one (20, 14) so it lines up with the page edge; medium and small (16, 12).</summary>
    public Thickness TabPadding { get; set; }

    public Thickness FirstTabPadding { get; set; }

    public Thickness CompactTabPadding { get; set; }

    /// <summary>The tab value: 22 (18 in the small layout); the underline under the selected tab is 2 thick.</summary>
    public double TabValueSize { get; set; }

    public double SmallTabValueSize { get; set; }

    public double UnderlineThickness { get; set; }

    /// <summary>The chart section's padding (20, 16, 20, 12) and its small form (16, 16, 16, 12); the legend row is 24 high, 12 above the plot.</summary>
    public Thickness ChartPadding { get; set; }

    public Thickness SmallChartPadding { get; set; }

    public double ChartBarHeight { get; set; }

    public double PlotGap { get; set; }

    /// <summary>The plot is 200 high (168 in the small layout), with a 24px x axis row under it and the y labels 10 to its left.</summary>
    public double PlotHeight { get; set; }

    public double SmallPlotHeight { get; set; }

    public double XAxisHeight { get; set; }

    public double YAxisGap { get; set; }

    /// <summary>Stroke widths: the line 1.75, the previous period 1.25 dashed 3 / 3; the area is the accent at 7%.</summary>
    public double LineThickness { get; set; }

    public double PreviousLineThickness { get; set; }

    public double PreviousDash { get; set; }

    public double AreaOpacity { get; set; }

    /// <summary>The inspection dot: 9 across with a 2px ring.</summary>
    public double DotDiameter { get; set; }

    /// <summary>Legend swatches are 10 × 2, 6 before their text; items are 14 apart.</summary>
    public double SwatchWidth { get; set; }

    public double LegendGap { get; set; }

    /// <summary>The Compare box: 14 square, radius 4, an 11px check of stroke 2.5, 6 before its text.</summary>
    public double CheckBoxSize { get; set; }

    public CornerRadius CheckBoxRadius { get; set; }

    public double CheckGlyphSize { get; set; }

    public double CheckGlyphStroke { get; set; }

    /// <summary>A list section's padding (20, 14, 20, 16) and its small form (12, 14, 12, 16); the heading row is inset 8 and 6 above the rows, rows are 2 apart.</summary>
    public Thickness ListPadding { get; set; }

    public Thickness SmallListPadding { get; set; }

    public Thickness ListHeaderMargin { get; set; }

    public double ListRowGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MediumWidth = 640;
        SmallWidth = 460;
        HeaderPadding = new Thickness(20, 16);
        SmallHeaderPadding = new Thickness(16);
        HeaderColumnGap = 16;
        HeaderRowGap = 12;
        ActionsGap = 8;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.16;
        SubtitleGap = 2;
        CopyLineHeight = 19.6;
        ExportHeight = 32;
        ExportPadding = new Thickness(10, 0, 12, 0);
        ExportRadius = new CornerRadius(10);
        ExportGlyphSize = 14;
        ExportGap = 6;
        TabPadding = new Thickness(16, 14);
        FirstTabPadding = new Thickness(20, 14, 16, 14);
        CompactTabPadding = new Thickness(16, 12);
        TabValueSize = 22;
        SmallTabValueSize = 18;
        UnderlineThickness = 2;
        ChartPadding = new Thickness(20, 16, 20, 12);
        SmallChartPadding = new Thickness(16, 16, 16, 12);
        ChartBarHeight = 24;
        PlotGap = 12;
        PlotHeight = 200;
        SmallPlotHeight = 168;
        XAxisHeight = 24;
        YAxisGap = 10;
        LineThickness = 1.75;
        PreviousLineThickness = 1.25;
        PreviousDash = 3;
        AreaOpacity = 0.07;
        DotDiameter = 9;
        SwatchWidth = 10;
        LegendGap = 14;
        CheckBoxSize = 14;
        CheckBoxRadius = new CornerRadius(4);
        CheckGlyphSize = 11;
        CheckGlyphStroke = 2.5;
        ListPadding = new Thickness(20, 14, 20, 16);
        SmallListPadding = new Thickness(12, 14, 12, 16);
        ListHeaderMargin = new Thickness(8, 0, 8, 6);
        ListRowGap = 2;
    }
}
