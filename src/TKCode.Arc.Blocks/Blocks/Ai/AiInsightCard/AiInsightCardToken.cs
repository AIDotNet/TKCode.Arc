using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AiInsightCard"/> (an original TKCode design). Colors, radii, type sizes and fonts come from
/// ArcDesign tokens in the theme; paddings are Avalonia thicknesses (left, top, right, bottom).
/// </summary>
[ControlDesignToken]
internal sealed class AiInsightCardToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows (640).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Padding inside the card (20) and its compact form (16).</summary>
    public Thickness CardPadding { get; set; }

    public Thickness CompactCardPadding { get; set; }

    /// <summary>At or below this width the paddings tighten and the value and its change stack (420).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Space between the card's sections (18).</summary>
    public double SectionGap { get; set; }

    /// <summary>Space between a section's heading and its body (6).</summary>
    public double HeadingGap { get; set; }

    /// <summary>The chart's height (148).</summary>
    public double ChartHeight { get; set; }

    /// <summary>The chart line's width (2) and the dots' radius (4).</summary>
    public double LineThickness { get; set; }

    public double DotRadius { get; set; }

    /// <summary>How strongly the area under the line is filled (0.55 of the soft accent).</summary>
    public double AreaOpacity { get; set; }

    /// <summary>Line height of the running text (14 / 21) and of small text (12 / 16.8).</summary>
    public double BodyLineHeight { get; set; }

    public double SmallLineHeight { get; set; }

    /// <summary>Space between the eyebrow glyph and its label (6), and the glyph's size (14).</summary>
    public double EyebrowGap { get; set; }

    public double EyebrowGlyphSize { get; set; }

    /// <summary>Padding of the eyebrow pill (8, 3) and of the change pill (8, 2).</summary>
    public Thickness EyebrowPadding { get; set; }

    public Thickness ChangePadding { get; set; }

    /// <summary>A cause row: padding (12, 10), the share bar's height (4) and width (72), space between rows (4).</summary>
    public Thickness RowPadding { get; set; }

    public double ShareBarHeight { get; set; }

    public double ShareBarWidth { get; set; }

    public double RowGap { get; set; }

    /// <summary>The cause row's focus ring sits 2 outside it.</summary>
    public Thickness RowFocusMargin { get; set; }

    /// <summary>The suggestion box's padding (14).</summary>
    public Thickness NextStepPadding { get; set; }

    /// <summary>The square icon buttons (32) and their glyphs (16).</summary>
    public double IconButtonSize { get; set; }

    public double GlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 640;
        CardPadding = new Thickness(20);
        CompactCardPadding = new Thickness(16);
        CompactWidth = 420;
        SectionGap = 18;
        HeadingGap = 6;
        ChartHeight = 148;
        LineThickness = 2;
        DotRadius = 4;
        AreaOpacity = 0.55;
        BodyLineHeight = 21;
        SmallLineHeight = 16.8;
        EyebrowGap = 6;
        EyebrowGlyphSize = 14;
        EyebrowPadding = new Thickness(8, 3);
        ChangePadding = new Thickness(8, 2);
        RowPadding = new Thickness(12, 10);
        ShareBarHeight = 4;
        ShareBarWidth = 72;
        RowGap = 4;
        RowFocusMargin = new Thickness(-2);
        NextStepPadding = new Thickness(14);
        IconButtonSize = 32;
        GlyphSize = 16;
    }
}
