using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AgentCursor"/>: the card's width, header and document padding (and the tighter padding under
/// 480px), block type metrics and spacing, the selection and change tints, the cursors and their tags, and the review
/// bar. Two accent-independent colors (the light and dark text on a cursor tag, picked by the tag's color) come from the
/// neutral palette; every other color and the type come from the ArcDesign tokens in the theme or follow the agent's color.
/// </summary>
[ControlDesignToken]
internal sealed class AgentCursorToken : AbstractControlDesignToken
{
    /// <summary>The card is fluid up to this width.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Under this width the document padding tightens and the review bar drops "by" and the agent's name.</summary>
    public double CompactWidth { get; set; }

    /// <summary>The header row: its padding, the space between its parts and the 28px avatars (the teammate overlaps by 6).</summary>
    public Thickness HeaderPadding { get; set; }

    public double HeaderGap { get; set; }

    public double AvatarSize { get; set; }

    public double AgentMarkSize { get; set; }

    public Thickness TeammateOverlap { get; set; }

    /// <summary>The status line: its glyph and the space after it.</summary>
    public double StatusGlyphSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>The document's padding; the bottom keeps room for the floating review bar so the card never grows.</summary>
    public Thickness DocumentPadding { get; set; }

    public Thickness CompactDocumentPadding { get; set; }

    /// <summary>Type: the title 30px lines, headings 24, paragraphs 22, the meta line 18.</summary>
    public double TitleLineHeight { get; set; }

    public double HeadingLineHeight { get; set; }

    public double ParagraphLineHeight { get; set; }

    public double MetaLineHeight { get; set; }

    /// <summary>Space above each block (inside its height spring, so an inserted block grows with it).</summary>
    public Thickness TitleMargin { get; set; }

    public Thickness MetaMargin { get; set; }

    public Thickness HeadingMargin { get; set; }

    public Thickness ParagraphMargin { get; set; }

    /// <summary>The selection fill (the agent's color at 22%) and the change tint (12%), rounded and grown past the glyphs.</summary>
    public double SelectionOpacity { get; set; }

    public double TintOpacity { get; set; }

    public CornerRadius TintCornerRadius { get; set; }

    public Thickness TintOutset { get; set; }

    /// <summary>The wavy underline that flags a typo: its stroke, height and wavelength.</summary>
    public double FlagStrokeWidth { get; set; }

    public double FlagAmplitude { get; set; }

    public double FlagWavelength { get; set; }

    /// <summary>Cursor tags: 20px pills of 12px medium text, 7 in from the sides, with a 14px initials disc for a person.</summary>
    public double TagHeight { get; set; }

    public double TagPadding { get; set; }

    public CornerRadius TagCornerRadius { get; set; }

    public double TagFontSize { get; set; }

    public double TagPortraitSize { get; set; }

    /// <summary>The cursor arrow's outline.</summary>
    public double CursorOutlineWidth { get; set; }

    /// <summary>The text on a light tag and on a dark tag.</summary>
    public Color TagDarkText { get; set; }

    public Color TagLightText { get; set; }

    /// <summary>The review bar: 44px high, 16 above the card's bottom, rising 8px as it fades in.</summary>
    public double ReviewBarHeight { get; set; }

    public Thickness ReviewBarPadding { get; set; }

    public Thickness ReviewBarMargin { get; set; }

    public double ReviewBarGap { get; set; }

    public double ReviewBarRise { get; set; }

    /// <summary>The header and review buttons: 30px high.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 680;
        CompactWidth = 480;
        HeaderPadding = new Thickness(16, 12, 12, 12);
        HeaderGap = 10;
        AvatarSize = 28;
        AgentMarkSize = 15;
        TeammateOverlap = new Thickness(-6, 0, 0, 0);
        StatusGlyphSize = 14;
        StatusGap = 6;
        DocumentPadding = new Thickness(32, 24, 32, 80);
        CompactDocumentPadding = new Thickness(20, 20, 20, 76);
        TitleLineHeight = 30;
        HeadingLineHeight = 24;
        ParagraphLineHeight = 22;
        MetaLineHeight = 18;
        TitleMargin = new Thickness(0);
        MetaMargin = new Thickness(0, 4, 0, 6);
        HeadingMargin = new Thickness(0, 18, 0, 0);
        ParagraphMargin = new Thickness(0, 8, 0, 0);
        SelectionOpacity = 0.22;
        TintOpacity = 0.12;
        TintCornerRadius = new CornerRadius(4);
        TintOutset = new Thickness(2, 1);
        FlagStrokeWidth = 1.25;
        FlagAmplitude = 1.5;
        FlagWavelength = 4;
        TagHeight = 20;
        TagPadding = 7;
        TagCornerRadius = new CornerRadius(6);
        TagFontSize = 12;
        TagPortraitSize = 14;
        CursorOutlineWidth = 1.5;
        var light = ArcPalette.Neutrals(false);
        TagDarkText = light.Foreground;
        TagLightText = light.Surface;
        ReviewBarHeight = 44;
        ReviewBarPadding = new Thickness(14, 0, 6, 0);
        ReviewBarMargin = new Thickness(16, 0, 16, 16);
        ReviewBarGap = 6;
        ReviewBarRise = 8;
        ActionHeight = 30;
        ActionPadding = new Thickness(12, 0);
    }
}
