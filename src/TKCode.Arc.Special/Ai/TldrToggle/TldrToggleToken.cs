using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TldrToggle"/>: the card (radius, widest width, padding at full and compact widths), the header
/// type, the controls row, the body's type and spacing, the key point rows, the highlight of a jumped-to paragraph and
/// the footer with its 32px actions. Colors and fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TldrToggleToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The widest the card grows; it fills narrower containers.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The card's padding: 24px, and 16px below 440px.</summary>
    public Thickness CardPadding { get; set; }

    public Thickness CompactCardPadding { get; set; }

    /// <summary>The header: the source line, the title (20/28, wraps) and the author row, with the space between them.</summary>
    public double HeaderGap { get; set; }

    public double MetaFontSize { get; set; }

    public double MonogramSize { get; set; }

    public CornerRadius MonogramCornerRadius { get; set; }

    public double MonogramFontSize { get; set; }

    public double SourceMarkSize { get; set; }

    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double AuthorGap { get; set; }

    /// <summary>The controls row under the header: its top margin, the gap beside or under the switch and the slider's widest.</summary>
    public Thickness ControlsMargin { get; set; }

    public double ControlsGap { get; set; }

    public double SliderMaxWidth { get; set; }

    /// <summary>The body: its margin from the controls and the footer, its type and the space between paragraphs.</summary>
    public Thickness BodyMargin { get; set; }

    public double BodyFontSize { get; set; }

    public double BodyLineHeight { get; set; }

    public double ParagraphGap { get; set; }

    /// <summary>How far the focus ring and the highlight may reach past the body's sides while its height moves.</summary>
    public double BodyCrossOverflow { get; set; }

    /// <summary>The thinking line's height and its type.</summary>
    public double ThinkingHeight { get; set; }

    /// <summary>Key points: the space between rows, the status glyph, the gap to the text and the jump link.</summary>
    public double PointGap { get; set; }

    public double PointGlyphSize { get; set; }

    public Thickness PointGlyphMargin { get; set; }

    public Thickness PointTextMargin { get; set; }

    public double LinkSize { get; set; }

    public double LinkGlyphSize { get; set; }

    public double LinkFontSize { get; set; }

    /// <summary>A jumped-to paragraph's highlight: its radius and how far it reaches past the text.</summary>
    public CornerRadius HighlightCornerRadius { get; set; }

    public Thickness HighlightOutset { get; set; }

    /// <summary>The error alert: its padding, radius and the gap between its icon, message and button.</summary>
    public Thickness AlertPadding { get; set; }

    public CornerRadius AlertCornerRadius { get; set; }

    public double AlertGap { get; set; }

    /// <summary>The footer: its height, type, status glyph, the model's 14px mark and the gaps.</summary>
    public double FooterHeight { get; set; }

    public Thickness FooterMargin { get; set; }

    public double FooterFontSize { get; set; }

    public double StatusGlyphSize { get; set; }

    public double FooterGap { get; set; }

    public double ModelMarkSize { get; set; }

    public Thickness ModelMargin { get; set; }

    /// <summary>The footer's actions: 32px targets with a 16px glyph.</summary>
    public double ActionSize { get; set; }

    public double ActionGlyphSize { get; set; }

    public Thickness ActionInlinePadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        CardMaxWidth = 680;
        CardPadding = new Thickness(24);
        CompactCardPadding = new Thickness(16);
        HeaderGap = 10;
        MetaFontSize = 13;
        MonogramSize = 20;
        MonogramCornerRadius = new CornerRadius(6);
        MonogramFontSize = 11;
        SourceMarkSize = 14;
        TitleFontSize = 20;
        TitleLineHeight = 28;
        AuthorGap = 8;
        ControlsMargin = new Thickness(0, 18, 0, 0);
        ControlsGap = 12;
        SliderMaxWidth = 240;
        BodyMargin = new Thickness(0, 18, 0, 18);
        BodyFontSize = 15;
        BodyLineHeight = 24;
        ParagraphGap = 14;
        BodyCrossOverflow = 8;
        ThinkingHeight = 24;
        PointGap = 10;
        PointGlyphSize = 16;
        PointGlyphMargin = new Thickness(0, 4, 0, 0);
        PointTextMargin = new Thickness(10, 0, 0, 0);
        LinkSize = 28;
        LinkGlyphSize = 14;
        LinkFontSize = 12;
        HighlightCornerRadius = new CornerRadius(4);
        HighlightOutset = new Thickness(4, 2);
        AlertPadding = new Thickness(14, 12);
        AlertCornerRadius = new CornerRadius(12);
        AlertGap = 10;
        FooterHeight = 48;
        FooterMargin = new Thickness(0, 0, 0, -8);
        FooterFontSize = 13;
        StatusGlyphSize = 14;
        FooterGap = 6;
        ModelMarkSize = 14;
        ModelMargin = new Thickness(12, 0);
        ActionSize = 32;
        ActionGlyphSize = 16;
        ActionInlinePadding = new Thickness(10, 0);
    }
}
