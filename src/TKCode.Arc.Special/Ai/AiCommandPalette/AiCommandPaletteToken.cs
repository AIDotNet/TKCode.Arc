using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AiCommandPalette"/>: the widest it grows, the input row, the 40px rows and their headings, the key
/// caps, the footer, the answer's type and spacing, the source chips and the inline citations. Colors, fonts, the panel's
/// radius and its shadow come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AiCommandPaletteToken : AbstractControlDesignToken
{
    /// <summary>The palette fills its container up to this width.</summary>
    public double PaletteMaxWidth { get; set; }

    public double InputRowHeight { get; set; }

    public Thickness InputRowPadding { get; set; }

    public double InputGlyphSize { get; set; }

    public Thickness InputGlyphMargin { get; set; }

    /// <summary>The mode toggle: the track's padding, a button's height and padding, its glyph.</summary>
    public Thickness ModeTrackPadding { get; set; }

    public double ModeButtonHeight { get; set; }

    public Thickness ModeButtonPadding { get; set; }

    public double ModeGlyphSize { get; set; }

    /// <summary>The padding around the lists in the results area.</summary>
    public Thickness ResultsPadding { get; set; }

    /// <summary>A row: 40px high, its padding, its icon and the space after it, the gap between label and meta, its radius.</summary>
    public double RowHeight { get; set; }

    public Thickness RowPadding { get; set; }

    public double RowIconSize { get; set; }

    public Thickness RowIconMargin { get; set; }

    public double RowMetaGap { get; set; }

    public CornerRadius RowCornerRadius { get; set; }

    /// <summary>A group heading: its height and padding.</summary>
    public double GroupHeadingHeight { get; set; }

    public Thickness GroupHeadingPadding { get; set; }

    /// <summary>A key cap: height, minimum width, padding, type size and radius.</summary>
    public double KeycapHeight { get; set; }

    public double KeycapMinWidth { get; set; }

    public Thickness KeycapPadding { get; set; }

    public double KeycapFontSize { get; set; }

    public CornerRadius KeycapCornerRadius { get; set; }

    /// <summary>The footer: height, padding, the space between hints and inside one, the model mark.</summary>
    public double FooterHeight { get; set; }

    public Thickness FooterPadding { get; set; }

    public double FooterHintGap { get; set; }

    public double HintGap { get; set; }

    public double FooterMarkSize { get; set; }

    /// <summary>The answer view: padding, the question's type, the meta row, the answer's type and spacing, section gaps.</summary>
    public Thickness AnswerPadding { get; set; }

    public double QuestionFontSize { get; set; }

    public double QuestionLineHeight { get; set; }

    public double AnswerFontSize { get; set; }

    public double AnswerLineHeight { get; set; }

    public double AnswerWordGap { get; set; }

    public double AnswerParagraphGap { get; set; }

    public double SectionGap { get; set; }

    /// <summary>A source chip: height, padding, the gap between chips, the mark, its type and the number badge.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipGap { get; set; }

    public double ChipMarkSize { get; set; }

    public double ChipFontSize { get; set; }

    public double ChipNumberSize { get; set; }

    /// <summary>An inline citation: height, minimum width, padding, type size and the space before it.</summary>
    public double CitationHeight { get; set; }

    public double CitationMinWidth { get; set; }

    public Thickness CitationPadding { get; set; }

    public double CitationFontSize { get; set; }

    public Thickness CitationMargin { get; set; }

    /// <summary>The empty-search line: its padding.</summary>
    public Thickness EmptyPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PaletteMaxWidth = 680;
        InputRowHeight = 56;
        InputRowPadding = new Thickness(18, 0, 12, 0);
        InputGlyphSize = 18;
        InputGlyphMargin = new Thickness(0, 0, 12, 0);
        ModeTrackPadding = new Thickness(2);
        ModeButtonHeight = 26;
        ModeButtonPadding = new Thickness(9, 0);
        ModeGlyphSize = 13;
        ResultsPadding = new Thickness(8, 6, 8, 8);
        RowHeight = 40;
        RowPadding = new Thickness(10, 0);
        RowIconSize = 16;
        RowIconMargin = new Thickness(0, 0, 10, 0);
        RowMetaGap = 8;
        RowCornerRadius = new CornerRadius(10);
        GroupHeadingHeight = 30;
        GroupHeadingPadding = new Thickness(10, 10, 10, 4);
        KeycapHeight = 20;
        KeycapMinWidth = 20;
        KeycapPadding = new Thickness(5, 0);
        KeycapFontSize = 11;
        KeycapCornerRadius = new CornerRadius(5);
        FooterHeight = 40;
        FooterPadding = new Thickness(16, 0, 12, 0);
        FooterHintGap = 14;
        HintGap = 6;
        FooterMarkSize = 14;
        AnswerPadding = new Thickness(18, 16, 18, 8);
        QuestionFontSize = 15;
        QuestionLineHeight = 22;
        AnswerFontSize = 14;
        AnswerLineHeight = 22;
        AnswerWordGap = 4;
        AnswerParagraphGap = 10;
        SectionGap = 14;
        ChipHeight = 26;
        ChipPadding = new Thickness(4, 0, 10, 0);
        ChipGap = 6;
        ChipMarkSize = 13;
        ChipFontSize = 12;
        ChipNumberSize = 18;
        CitationHeight = 17;
        CitationMinWidth = 17;
        CitationPadding = new Thickness(4, 0);
        CitationFontSize = 11;
        CitationMargin = new Thickness(2, 0, 0, 0);
        EmptyPadding = new Thickness(10, 14, 10, 6);
    }
}

/// <summary>Geometry of <see cref="AiCommandPaletteDialog"/>: the palette's width and its distance from the top of the window.</summary>
[ControlDesignToken]
internal sealed class AiCommandPaletteDialogToken : AbstractControlDesignToken
{
    /// <summary>The widest the palette is in the dialog.</summary>
    public double PanelWidth { get; set; }

    /// <summary>The palette's top as a fraction of the window height.</summary>
    public double TopRatio { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelWidth = 680;
        TopRatio = 0.12;
    }
}
