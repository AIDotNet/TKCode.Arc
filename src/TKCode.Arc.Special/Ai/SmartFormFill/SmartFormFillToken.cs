using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SmartFormFill"/>: the card, its header, the paste well and its text area, the form grid, a field's
/// label row, confidence dot, logo and note, the source highlight in the pasted text and the footer. Colors, fonts and the
/// focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SmartFormFillToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius and padding.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    public Thickness CardPadding { get; set; }

    /// <summary>The space between the header, the body and the footer, and between the well and the form.</summary>
    public double SectionGap { get; set; }

    /// <summary>The title (17/24 medium) and the description under it (13/20).</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double DescriptionFontSize { get; set; }

    public double DescriptionLineHeight { get; set; }

    /// <summary>The paste well: its radius and padding, the space between its rows and the model chip's text size.</summary>
    public CornerRadius WellCornerRadius { get; set; }

    public Thickness WellPadding { get; set; }

    public double WellGap { get; set; }

    public double WellHeadingFontSize { get; set; }

    /// <summary>The text area's height when the well stacks on top of the form, and its least height beside it.</summary>
    public double EditorHeight { get; set; }

    public double EditorMinHeight { get; set; }

    /// <summary>The pasted text view: its padding, text size and line height.</summary>
    public Thickness SourcePadding { get; set; }

    public double SourceFontSize { get; set; }

    public double SourceLineHeight { get; set; }

    /// <summary>The source highlight: its corners, how far it reaches past the text, and its opacity at rest and when it is the active field's.</summary>
    public CornerRadius HighlightCornerRadius { get; set; }

    public Thickness HighlightOutset { get; set; }

    public double HighlightRestOpacity { get; set; }

    /// <summary>The form grid's gaps.</summary>
    public double FieldColumnGap { get; set; }

    public double FieldRowGap { get; set; }

    /// <summary>A field: the label row (13/18 medium), the space under it, the confidence dot and badge text, the note.</summary>
    public double LabelFontSize { get; set; }

    public double LabelLineHeight { get; set; }

    public double LabelGap { get; set; }

    public double DotSize { get; set; }

    public double BadgeGap { get; set; }

    public double BadgeFontSize { get; set; }

    public double NoteFontSize { get; set; }

    public double NoteLineHeight { get; set; }

    /// <summary>The company logo slot (a 20px rounded square) and its radius.</summary>
    public double LogoSize { get; set; }

    public CornerRadius LogoCornerRadius { get; set; }

    /// <summary>The check button inside a field that needs review.</summary>
    public double CheckButtonSize { get; set; }

    public double CheckGlyphSize { get; set; }

    /// <summary>The accent tint an input breathes when its value arrives, at its peak.</summary>
    public double TintPeakOpacity { get; set; }

    /// <summary>The footer: the status glyph and text, and the gap between the actions.</summary>
    public double StatusGlyphSize { get; set; }

    public double StatusFontSize { get; set; }

    public double ActionGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(20);
        SectionGap = 20;
        TitleFontSize = 17;
        TitleLineHeight = 24;
        DescriptionFontSize = 13;
        DescriptionLineHeight = 20;
        WellCornerRadius = new CornerRadius(16);
        WellPadding = new Thickness(12);
        WellGap = 10;
        WellHeadingFontSize = 12;
        EditorHeight = 168;
        EditorMinHeight = 200;
        SourcePadding = new Thickness(12, 10);
        SourceFontSize = 13;
        SourceLineHeight = 21;
        HighlightCornerRadius = new CornerRadius(4);
        HighlightOutset = new Thickness(2, 1);
        HighlightRestOpacity = 0.6;
        FieldColumnGap = 12;
        FieldRowGap = 14;
        LabelFontSize = 13;
        LabelLineHeight = 18;
        LabelGap = 6;
        DotSize = 6;
        BadgeGap = 5;
        BadgeFontSize = 12;
        NoteFontSize = 12;
        NoteLineHeight = 16;
        LogoSize = 20;
        LogoCornerRadius = new CornerRadius(6);
        CheckButtonSize = 28;
        CheckGlyphSize = 15;
        TintPeakOpacity = 0.14;
        StatusGlyphSize = 15;
        StatusFontSize = 13;
        ActionGap = 8;
    }
}
