using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ToneTuner"/>: the card, its sections and gaps, the voice chips, the message area and its type,
/// the change highlights, the footer and the breakpoints. Colors, fonts and the focus ring come from the ArcDesign tokens
/// in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ToneTunerToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius and padding.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    public Thickness CardPadding { get; set; }

    /// <summary>The space between the card's sections (header, chips, sliders, message, footer).</summary>
    public double SectionGap { get; set; }

    /// <summary>The header: the space between the avatar and the names, the name and subject type.</summary>
    public double HeaderGap { get; set; }

    public double NameLineHeight { get; set; }

    public double SubjectFontSize { get; set; }

    /// <summary>The changed words pill in the header.</summary>
    public double CountFontSize { get; set; }

    public double CountHeight { get; set; }

    public Thickness CountPadding { get; set; }

    /// <summary>A voice chip: 32px high, pill shaped, 12px in from the sides, a 14px icon 6px from its label.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipGap { get; set; }

    public double ChipIconSize { get; set; }

    public double ChipIconGap { get; set; }

    /// <summary>The space between the two sliders side by side, and between them stacked.</summary>
    public double SliderColumnGap { get; set; }

    public double SliderRowGap { get; set; }

    /// <summary>The message area: its corner radius, padding, type and the space between paragraphs.</summary>
    public CornerRadius MessageCornerRadius { get; set; }

    public Thickness MessagePadding { get; set; }

    public double MessageFontSize { get; set; }

    public double MessageLineHeight { get; set; }

    public double ParagraphGap { get; set; }

    /// <summary>A highlighted word: its rounded background (the corner is read from the top left) and how far it reaches past the glyphs.</summary>
    public CornerRadius HighlightCornerRadius { get; set; }

    public Thickness HighlightOutset { get; set; }

    /// <summary>The footer: its height, the status glyph and text, and the space between its controls.</summary>
    public double FooterHeight { get; set; }

    public double StatusGlyphSize { get; set; }

    public double StatusFontSize { get; set; }

    public double StatusGap { get; set; }

    public double FooterGap { get; set; }

    /// <summary>Retry beside a failed status: 28px high, 10px in from the sides.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    /// <summary>Below this card width the sliders stack, the chips share one row and the status hides its settings.</summary>
    public double StackWidth { get; set; }

    /// <summary>Below this card width the chip icons, the changed count and Copy's label drop.</summary>
    public double CompactWidth { get; set; }

    /// <summary>The widest the card grows (Arc: fluid up to 640px).</summary>
    public double MaxCardWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(20, 18, 20, 16);
        SectionGap = 16;
        HeaderGap = 10;
        NameLineHeight = 20;
        SubjectFontSize = 13;
        CountFontSize = 12;
        CountHeight = 24;
        CountPadding = new Thickness(9, 0);
        ChipHeight = 32;
        ChipPadding = new Thickness(12, 0);
        ChipGap = 8;
        ChipIconSize = 14;
        ChipIconGap = 6;
        SliderColumnGap = 24;
        SliderRowGap = 12;
        MessageCornerRadius = new CornerRadius(14);
        MessagePadding = new Thickness(16, 14);
        MessageFontSize = 14;
        MessageLineHeight = 22;
        ParagraphGap = 10;
        HighlightCornerRadius = new CornerRadius(4);
        HighlightOutset = new Thickness(2, 1);
        FooterHeight = 32;
        StatusGlyphSize = 14;
        StatusFontSize = 13;
        StatusGap = 6;
        FooterGap = 12;
        ActionHeight = 28;
        ActionPadding = new Thickness(10, 0);
        StackWidth = 520;
        CompactWidth = 400;
        MaxCardWidth = 640;
    }
}
