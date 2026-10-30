using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="StarterPrompts"/>: the outer surface (and its narrow step), the greeting and description type,
/// the toolbar, the card grid and its breakpoint, the cards with their mark and text metrics, and the composer with its
/// footer and send button. Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class StarterPromptsToken : AbstractControlDesignToken
{
    /// <summary>The outer surface's corner radius, and the smaller one under 480px.</summary>
    public CornerRadius SurfaceCornerRadius { get; set; }

    public CornerRadius SurfaceCornerRadiusNarrow { get; set; }

    /// <summary>The outer surface's padding, and the smaller one under 480px.</summary>
    public Thickness SurfacePadding { get; set; }

    public Thickness SurfacePaddingNarrow { get; set; }

    /// <summary>The greeting: 24/30 semibold with tight letter spacing (20/26 under 480px).</summary>
    public double GreetingFontSize { get; set; }

    public double GreetingLineHeight { get; set; }

    public double GreetingFontSizeNarrow { get; set; }

    public double GreetingLineHeightNarrow { get; set; }

    public double GreetingLetterSpacing { get; set; }

    /// <summary>The description under the greeting: 14/20, 6px below it.</summary>
    public double DescriptionLineHeight { get; set; }

    public Thickness DescriptionMargin { get; set; }

    /// <summary>The row of tabs and Shuffle, and the space above it.</summary>
    public Thickness ToolbarMargin { get; set; }

    /// <summary>The space between the tabs and Shuffle.</summary>
    public Thickness ShuffleMargin { get; set; }

    /// <summary>The grid of cards: the space above it, the gap between cards and the width from which it shows two columns.</summary>
    public Thickness GridMargin { get; set; }

    public double GridGap { get; set; }

    public double TwoColumnMinWidth { get; set; }

    /// <summary>Under this width Shuffle shows only its icon.</summary>
    public double CompactShuffleWidth { get; set; }

    /// <summary>Under this width the surface's padding and radius step down.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>A card: its radius, padding and the keyboard ring around it.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    public Thickness CardPadding { get; set; }

    public CornerRadius CardFocusRingRadius { get; set; }

    public Thickness CardFocusRingMargin { get; set; }

    /// <summary>The mark square on a card, its radius and glyph.</summary>
    public double MarkSize { get; set; }

    public CornerRadius MarkCornerRadius { get; set; }

    public double MarkGlyphSize { get; set; }

    /// <summary>The app's name beside the mark: 12/16.</summary>
    public double AppFontSize { get; set; }

    public double AppLineHeight { get; set; }

    /// <summary>The card's title (14/20, medium, 10px under the mark) and description (13/18).</summary>
    public Thickness TitleMargin { get; set; }

    public double TitleLineHeight { get; set; }

    public double CardDescriptionFontSize { get; set; }

    public double CardDescriptionLineHeight { get; set; }

    /// <summary>The placeholder bars of a loading card: the title's and the description's height and width share.</summary>
    public double SkeletonTitleHeight { get; set; }

    public double SkeletonLineHeight { get; set; }

    public CornerRadius SkeletonCornerRadius { get; set; }

    /// <summary>The composer: the space above it, its radius, padding and the text's line height and height limits.</summary>
    public Thickness ComposerMargin { get; set; }

    public CornerRadius ComposerCornerRadius { get; set; }

    public Thickness ComposerPadding { get; set; }

    public double ComposerLineHeight { get; set; }

    public double ComposerMinHeight { get; set; }

    public double ComposerMaxHeight { get; set; }

    /// <summary>The composer's footer: its height, the model chip's mark and the status text.</summary>
    public double FooterHeight { get; set; }

    public Thickness FooterMargin { get; set; }

    public double ModelMarkSize { get; set; }

    public double StatusFontSize { get; set; }

    public double StatusGlyphSize { get; set; }

    /// <summary>The round send button and its glyph.</summary>
    public double SendSize { get; set; }

    public double SendGlyphSize { get; set; }

    public CornerRadius SendFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SurfaceCornerRadius = new CornerRadius(28);
        SurfaceCornerRadiusNarrow = new CornerRadius(20);
        SurfacePadding = new Thickness(28);
        SurfacePaddingNarrow = new Thickness(18);
        GreetingFontSize = 24;
        GreetingLineHeight = 30;
        GreetingFontSizeNarrow = 20;
        GreetingLineHeightNarrow = 26;
        GreetingLetterSpacing = -0.4;
        DescriptionLineHeight = 20;
        DescriptionMargin = new Thickness(0, 6, 0, 0);
        ToolbarMargin = new Thickness(0, 22, 0, 0);
        ShuffleMargin = new Thickness(8, 0, 0, 0);
        GridMargin = new Thickness(0, 12, 0, 0);
        GridGap = 10;
        TwoColumnMinWidth = 560;
        CompactShuffleWidth = 440;
        NarrowWidth = 480;
        CardCornerRadius = new CornerRadius(16);
        CardPadding = new Thickness(14, 13, 14, 13);
        CardFocusRingRadius = new CornerRadius(19);
        CardFocusRingMargin = new Thickness(-3);
        MarkSize = 26;
        MarkCornerRadius = new CornerRadius(8);
        MarkGlyphSize = 14;
        AppFontSize = 12;
        AppLineHeight = 16;
        TitleMargin = new Thickness(0, 10, 0, 0);
        TitleLineHeight = 20;
        CardDescriptionFontSize = 13;
        CardDescriptionLineHeight = 18;
        SkeletonTitleHeight = 12;
        SkeletonLineHeight = 10;
        SkeletonCornerRadius = new CornerRadius(5);
        ComposerMargin = new Thickness(0, 16, 0, 0);
        ComposerCornerRadius = new CornerRadius(20);
        ComposerPadding = new Thickness(16, 12, 10, 10);
        ComposerLineHeight = 22;
        ComposerMinHeight = 44;
        ComposerMaxHeight = 176;
        FooterHeight = 32;
        FooterMargin = new Thickness(0, 6, 0, 0);
        ModelMarkSize = 14;
        StatusFontSize = 12;
        StatusGlyphSize = 14;
        SendSize = 32;
        SendGlyphSize = 16;
        SendFocusRingRadius = new CornerRadius(20);
    }
}
