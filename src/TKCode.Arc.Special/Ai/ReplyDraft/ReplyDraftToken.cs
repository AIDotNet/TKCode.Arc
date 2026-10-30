using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ReplyDraft"/>: the card (600px at most, 20px padding, 16px below 480px), the email header and
/// body, the reply area with its quick reply chips, the draft box, the tone and length groups, and the buttons. Colors,
/// fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ReplyDraftToken : AbstractControlDesignToken
{
    /// <summary>The card's widest width; it fills narrower containers.</summary>
    public double CardMaxWidth { get; set; }

    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The card's padding, and the padding below 480px.</summary>
    public Thickness CardPadding { get; set; }

    public Thickness CompactCardPadding { get; set; }

    /// <summary>The space between the subject line, the sender row and the body.</summary>
    public double HeaderGap { get; set; }

    /// <summary>The mail glyph before the subject, and the subject's type.</summary>
    public double MailMarkSize { get; set; }

    public double SubjectFontSize { get; set; }

    public double SubjectLineHeight { get; set; }

    /// <summary>The time and address text.</summary>
    public double MetaFontSize { get; set; }

    /// <summary>The space between the avatar and the sender's name.</summary>
    public double SenderGap { get; set; }

    /// <summary>The body's indent under the avatar (dropped below 480px).</summary>
    public Thickness BodyIndent { get; set; }

    public double BodyFontSize { get; set; }

    public double BodyLineHeight { get; set; }

    public double ParagraphGap { get; set; }

    /// <summary>The reply area: its distance from the email, its padding under the hairline and the gap between its rows.</summary>
    public Thickness ReplyMargin { get; set; }

    public Thickness ReplyPadding { get; set; }

    public double ReplyGap { get; set; }

    /// <summary>The model's mark and the reply heading's type.</summary>
    public double ModelMarkSize { get; set; }

    public double ReplyHeadingFontSize { get; set; }

    /// <summary>The status line: glyph, text and the gap between them.</summary>
    public double StatusGlyphSize { get; set; }

    public double StatusFontSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>A quick reply chip: its height (36px on touch), padding, type and the gap between chips.</summary>
    public double ChipHeight { get; set; }

    public double ChipHeightCoarse { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipFontSize { get; set; }

    public double ChipGap { get; set; }

    /// <summary>The scale a pressed chip or option shrinks to.</summary>
    public double PressScale { get; set; }

    /// <summary>The draft box: its radius, padding, the type of the draft and its smallest height.</summary>
    public CornerRadius DraftCornerRadius { get; set; }

    public Thickness DraftPadding { get; set; }

    public double DraftFontSize { get; set; }

    public double DraftLineHeight { get; set; }

    public double DraftMinHeight { get; set; }

    /// <summary>How far the old draft dims while a rewrite runs.</summary>
    public double DimOpacity { get; set; }

    /// <summary>A tone or length option: its height (32px on touch), padding, type, the track's inset and the label's gap.</summary>
    public double OptionHeight { get; set; }

    public double OptionHeightCoarse { get; set; }

    public Thickness OptionPadding { get; set; }

    public double OptionFontSize { get; set; }

    public Thickness TrackPadding { get; set; }

    public double GroupLabelGap { get; set; }

    /// <summary>The space between the tone and length groups, and between rows when they wrap.</summary>
    public double ControlsGap { get; set; }

    public double ControlsRowGap { get; set; }

    /// <summary>The hairline between the groups.</summary>
    public double DividerHeight { get; set; }

    /// <summary>Regenerate and Insert: their height, padding, type and the gap between them.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    public double ActionFontSize { get; set; }

    public double ActionGap { get; set; }

    public double ActionGlyphSize { get; set; }

    /// <summary>The failure alert inside the draft box.</summary>
    public double AlertGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 600;
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(20);
        CompactCardPadding = new Thickness(16);
        HeaderGap = 12;
        MailMarkSize = 16;
        SubjectFontSize = 16;
        SubjectLineHeight = 22;
        MetaFontSize = 12;
        SenderGap = 12;
        BodyIndent = new Thickness(44, 0, 0, 0);
        BodyFontSize = 14;
        BodyLineHeight = 22;
        ParagraphGap = 10;
        ReplyMargin = new Thickness(0, 18, 0, 0);
        ReplyPadding = new Thickness(0, 16, 0, 0);
        ReplyGap = 12;
        ModelMarkSize = 16;
        ReplyHeadingFontSize = 13;
        StatusGlyphSize = 14;
        StatusFontSize = 12;
        StatusGap = 6;
        ChipHeight = 32;
        ChipHeightCoarse = 36;
        ChipPadding = new Thickness(12, 0);
        ChipFontSize = 13;
        ChipGap = 8;
        PressScale = 0.97;
        DraftCornerRadius = new CornerRadius(14);
        DraftPadding = new Thickness(14, 12);
        DraftFontSize = 14;
        DraftLineHeight = 22;
        DraftMinHeight = 22;
        DimOpacity = 0.5;
        OptionHeight = 28;
        OptionHeightCoarse = 32;
        OptionPadding = new Thickness(10, 0);
        OptionFontSize = 12;
        TrackPadding = new Thickness(2);
        GroupLabelGap = 8;
        ControlsGap = 16;
        ControlsRowGap = 10;
        DividerHeight = 20;
        ActionHeight = 32;
        ActionPadding = new Thickness(12, 0);
        ActionFontSize = 13;
        ActionGap = 8;
        ActionGlyphSize = 14;
        AlertGap = 8;
    }
}
