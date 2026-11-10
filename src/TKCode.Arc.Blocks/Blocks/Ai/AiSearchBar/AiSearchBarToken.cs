using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AiSearchBar"/> (an original TKCode design). Colors, radii, type sizes and fonts come from
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AiSearchBarToken : AbstractControlDesignToken
{
    /// <summary>The widest the bar grows (720).</summary>
    public double BarMaxWidth { get; set; }

    /// <summary>Padding of the panel under the field (20) and its compact form (16).</summary>
    public Thickness PanelPadding { get; set; }

    public Thickness CompactPanelPadding { get; set; }

    /// <summary>At or below this width the paddings tighten (440).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Space between the panel's sections (20) and between a heading and its content (8).</summary>
    public double SectionGap { get; set; }

    public double HeadingGap { get; set; }

    /// <summary>Line height of the answer (24).</summary>
    public double AnswerLineHeight { get; set; }

    /// <summary>Line height of small text (16.8).</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Padding of a question chip (12, 6), a source card (10, 8) and a result row (12, 10).</summary>
    public Thickness ChipPadding { get; set; }

    public Thickness SourcePadding { get; set; }

    public Thickness ResultPadding { get; set; }

    /// <summary>Space between chips and cards in a row (8).</summary>
    public double ItemGap { get; set; }

    /// <summary>A source card is at most 260 wide.</summary>
    public double SourceMaxWidth { get; set; }

    /// <summary>The citation pill: 18 high with 6 on each side and 10px numbers.</summary>
    public double CitationHeight { get; set; }

    public Thickness CitationPadding { get; set; }

    public double CitationFontSize { get; set; }

    /// <summary>The number disc of a source card (20).</summary>
    public double NumberSize { get; set; }

    /// <summary>Focus rings sit 2 outside the item.</summary>
    public Thickness FocusMargin { get; set; }

    /// <summary>Glyph size (16) and the small glyph of a chip (14).</summary>
    public double GlyphSize { get; set; }

    public double SmallGlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarMaxWidth = 720;
        PanelPadding = new Thickness(20);
        CompactPanelPadding = new Thickness(16);
        CompactWidth = 440;
        SectionGap = 20;
        HeadingGap = 8;
        AnswerLineHeight = 24;
        SmallLineHeight = 16.8;
        ChipPadding = new Thickness(12, 6);
        SourcePadding = new Thickness(10, 8);
        ResultPadding = new Thickness(12, 10);
        ItemGap = 8;
        SourceMaxWidth = 260;
        CitationHeight = 18;
        CitationPadding = new Thickness(6, 0);
        CitationFontSize = 10;
        NumberSize = 20;
        FocusMargin = new Thickness(-2);
        GlyphSize = 16;
        SmallGlyphSize = 14;
    }
}
