using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CardToArticle"/> (Arc Card to article, design reference) and the colors that sit on the
/// picture, which stay the same in light and dark (white pills and text over a dark scrim). Surfaces, text, accent,
/// shadows, radii and fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class CardToArticleToken : AbstractControlDesignToken
{
    /// <summary>The largest width the grid takes (Arc: 680, centred).</summary>
    public double GridMaxWidth { get; set; }

    /// <summary>The 2px keyboard ring 2px outside a card (4px outside its edge).</summary>
    public Thickness CardFocusRingMargin { get; set; }

    /// <summary>The card focus ring's outer radius (the card's 26 + 4).</summary>
    public CornerRadius CardFocusRingRadius { get; set; }

    /// <summary>The article text: 28 above, 56 below (the sides follow the window width).</summary>
    public double BodyPaddingTop { get; set; }

    public double BodyPaddingBottom { get; set; }

    /// <summary>Space between the byline, paragraphs and quotes of the article.</summary>
    public double ArticleGap { get; set; }

    /// <summary>Paragraph line height (16px text at 1.6).</summary>
    public double ParagraphLineHeight { get; set; }

    /// <summary>Pull quote line height (22px text at 1.4).</summary>
    public double QuoteLineHeight { get; set; }

    /// <summary>The pull quote's accent rule and the gap after it.</summary>
    public Thickness QuoteRuleThickness { get; set; }

    public Thickness QuotePadding { get; set; }

    /// <summary>Extra space above and below a pull quote (24 with the article gap).</summary>
    public Thickness QuoteMargin { get; set; }

    /// <summary>The byline: a 28px avatar 8px before the author.</summary>
    public double AvatarDiameter { get; set; }

    public double BylineGap { get; set; }

    /// <summary>The reading progress bar along the top of the article.</summary>
    public double ProgressThickness { get; set; }

    /// <summary>The close button: a 36px circle 14px inside the article's top-right corner, with an 18px glyph.</summary>
    public double CloseDiameter { get; set; }

    public Thickness CloseMargin { get; set; }

    public double CloseGlyphSize { get; set; }

    public double CloseGlyphStroke { get; set; }

    /// <summary>The close button's focus ring (2px, 2px outside the circle).</summary>
    public Thickness CloseFocusRingMargin { get; set; }

    /// <summary>The frosted white of the close button over the picture.</summary>
    public Color CloseFillColor { get; set; }

    /// <summary>The dark ink of the close glyph and of the tag text.</summary>
    public Color OnPictureInkColor { get; set; }

    /// <summary>The tag pill over the picture (white at 82%).</summary>
    public Color TagFillColor { get; set; }

    /// <summary>The title over the picture (near white).</summary>
    public Color TitleInkColor { get; set; }

    /// <summary>The meta line over the picture (white at 80%).</summary>
    public Color MetaInkColor { get; set; }

    /// <summary>The bottom of the picture's scrim (black at 62%, fading to clear 60% up).</summary>
    public Color ScrimShadeColor { get; set; }

    /// <summary>How opaque the page backdrop is behind an open article (the page background at 55%).</summary>
    public double BackdropOpacity { get; set; }

    /// <summary>The blur of the page behind an open article.</summary>
    public double BackdropBlurRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GridMaxWidth = 680;
        CardFocusRingMargin = new Thickness(-4);
        CardFocusRingRadius = new CornerRadius(30);
        BodyPaddingTop = 28;
        BodyPaddingBottom = 56;
        ArticleGap = 18;
        ParagraphLineHeight = 25.6;
        QuoteLineHeight = 30.8;
        QuoteRuleThickness = new Thickness(2, 0, 0, 0);
        QuotePadding = new Thickness(16, 0, 0, 0);
        QuoteMargin = new Thickness(0, 6);
        AvatarDiameter = 28;
        BylineGap = 8;
        ProgressThickness = 3;
        CloseDiameter = 36;
        CloseMargin = new Thickness(0, 14, 14, 0);
        CloseGlyphSize = 18;
        CloseGlyphStroke = 1.75;
        CloseFocusRingMargin = new Thickness(-4);
        CloseFillColor = Color.FromArgb(204, 255, 255, 255);
        OnPictureInkColor = Oklch.ToColor(0.2, 0, 0);
        TagFillColor = Color.FromArgb(209, 255, 255, 255);
        TitleInkColor = Oklch.ToColor(0.99, 0, 0);
        MetaInkColor = Color.FromArgb(204, 255, 255, 255);
        ScrimShadeColor = Color.FromArgb(158, 0, 0, 0);
        BackdropOpacity = 0.55;
        BackdropBlurRadius = 10;
    }
}
