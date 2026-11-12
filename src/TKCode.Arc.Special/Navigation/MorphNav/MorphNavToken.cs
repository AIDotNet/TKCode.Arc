using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MorphNav"/>: the bar and its compact form, the panels, links, search and the feature card, plus
/// the accent-independent tints of the surface. Colors that follow the accent, radii, shadows and fonts come from the
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class MorphNavToken : AbstractControlDesignToken
{
    /// <summary>Height of the bar.</summary>
    public double BarHeight { get; set; }

    /// <summary>Height of the bar in compact mode.</summary>
    public double CompactBarHeight { get; set; }

    /// <summary>Space between the bar's edge and its first and last parts.</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Gap between the brand, the items and the tools.</summary>
    public double BarSpacing { get; set; }

    /// <summary>Height of the items, the brand, the search button and the action.</summary>
    public double ItemHeight { get; set; }

    /// <summary>Height of the items, the brand, the search button and the action in compact mode.</summary>
    public double CompactItemHeight { get; set; }

    /// <summary>Space inside an item.</summary>
    public Thickness ItemPadding { get; set; }

    /// <summary>Space inside an item in compact mode.</summary>
    public Thickness CompactItemPadding { get; set; }

    /// <summary>Gap between an item's label and its chevron.</summary>
    public double ItemSpacing { get; set; }

    /// <summary>Size of the chevron of items with a panel.</summary>
    public double ChevronSize { get; set; }

    /// <summary>Space inside the brand button.</summary>
    public Thickness BrandPadding { get; set; }

    /// <summary>Size of the brand mark.</summary>
    public double BrandMarkSize { get; set; }

    /// <summary>Space before the brand name (after the mark).</summary>
    public Thickness BrandNameMargin { get; set; }

    /// <summary>Extra space before the tools (search and action).</summary>
    public Thickness ToolsMargin { get; set; }

    /// <summary>Gap between the search button and the action.</summary>
    public double ToolsSpacing { get; set; }

    /// <summary>Size of the search glyph and of the close-search glyph.</summary>
    public double ToolGlyphSize { get; set; }

    /// <summary>Space inside the action button.</summary>
    public Thickness ActionPadding { get; set; }

    /// <summary>Space inside the action button in compact mode.</summary>
    public Thickness CompactActionPadding { get; set; }

    /// <summary>Gap between the action's glyph and label.</summary>
    public double ActionSpacing { get; set; }

    /// <summary>Space inside an open panel, the menu and the results.</summary>
    public Thickness FacePadding { get; set; }

    /// <summary>A panel is never wider than this.</summary>
    public double FaceMaxWidth { get; set; }

    /// <summary>Panels and search keep this much less than the nav's own width.</summary>
    public double FaceGutter { get; set; }

    /// <summary>How far the hairline between the bar and a panel stays from the surface's sides.</summary>
    public double DividerInset { get; set; }

    /// <summary>Space above the links of a panel.</summary>
    public double BodyTopPadding { get; set; }

    /// <summary>Gap between the links and the feature.</summary>
    public double FeatureGap { get; set; }

    /// <summary>Width of the feature column.</summary>
    public double FeatureWidth { get; set; }

    /// <summary>Width of a link column.</summary>
    public double LinkColumnWidth { get; set; }

    /// <summary>Gap between links, in both directions.</summary>
    public double LinkGap { get; set; }

    /// <summary>Space inside a link.</summary>
    public Thickness LinkPadding { get; set; }

    /// <summary>Gap between a link's glyph and its text.</summary>
    public double LinkSpacing { get; set; }

    /// <summary>Box of a link's glyph.</summary>
    public double LinkIconSize { get; set; }

    /// <summary>Size of a link's glyph inside its box.</summary>
    public double LinkGlyphSize { get; set; }

    /// <summary>Line height of 14 px labels.</summary>
    public double LabelLineHeight { get; set; }

    /// <summary>Line height of 12 px descriptions, headings and footers.</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Gap between a link's label and its description.</summary>
    public double LinkTextGap { get; set; }

    /// <summary>Space inside a link of the folded menu.</summary>
    public Thickness DenseLinkPadding { get; set; }

    /// <summary>Space inside a link of the folded menu on narrow windows (no glyphs).</summary>
    public Thickness TinyLinkPadding { get; set; }

    /// <summary>Gap between a menu link's glyph and its label.</summary>
    public double DenseLinkSpacing { get; set; }

    /// <summary>Box of a menu link's glyph.</summary>
    public double DenseIconSize { get; set; }

    /// <summary>Space around a group heading of the folded menu.</summary>
    public Thickness MenuHeadingPadding { get; set; }

    /// <summary>Space around a group heading of the folded menu on narrow windows.</summary>
    public Thickness TinyMenuHeadingPadding { get; set; }

    /// <summary>Gap between the two group columns of the folded menu.</summary>
    public double MenuColumnGap { get; set; }

    /// <summary>The folded menu shows one group column when its content is narrower than this.</summary>
    public double MenuTwoColumnWidth { get; set; }

    /// <summary>Space around a panel's footer.</summary>
    public Thickness FooterMargin { get; set; }

    /// <summary>Space inside a panel's footer, under its hairline.</summary>
    public Thickness FooterPadding { get; set; }

    /// <summary>Gap between a footer link's text and arrow.</summary>
    public double FooterLinkSpacing { get; set; }

    /// <summary>Size of the footer link's arrow.</summary>
    public double FooterGlyphSize { get; set; }

    /// <summary>Space inside the feature card.</summary>
    public Thickness StoryPadding { get; set; }

    /// <summary>Gap between the feature card's title and description.</summary>
    public double StorySpacing { get; set; }

    /// <summary>Width-to-height ratio of the feature card's picture.</summary>
    public double StoryImageAspect { get; set; }

    /// <summary>Space under the feature card's picture.</summary>
    public Thickness StoryImageMargin { get; set; }

    /// <summary>Indent of the feature card's text.</summary>
    public Thickness StoryTextMargin { get; set; }

    /// <summary>Width of the search surface.</summary>
    public double SearchWidth { get; set; }

    /// <summary>Space inside the search bar.</summary>
    public Thickness SearchBarPadding { get; set; }

    /// <summary>Gap between the search glyph, the input and the close button.</summary>
    public double SearchBarSpacing { get; set; }

    /// <summary>Size of the search bar's glyph.</summary>
    public double SearchIconSize { get; set; }

    /// <summary>Space around the results heading.</summary>
    public Thickness ResultsHeadingPadding { get; set; }

    /// <summary>Space inside a search result.</summary>
    public Thickness ResultPadding { get; set; }

    /// <summary>Gap between a result's glyph, text, group and enter glyph.</summary>
    public double ResultSpacing { get; set; }

    /// <summary>Size of the enter glyph on the active result.</summary>
    public double EnterGlyphSize { get; set; }

    /// <summary>Below this window width link panels use one column and the folded menu drops its glyphs.</summary>
    public double TinyViewportWidth { get; set; }

    /// <summary>Margin of the keyboard focus ring (outside the part).</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>
    /// The surface's fill: the raised surface, slightly translucent. (Arc MorphNav（设计参考） uses 0.88 over a 20 px backdrop
    /// blur; without a blur behind it the surface stays nearly opaque so the page does not show through.)
    /// </summary>
    public Color SurfaceColor { get; set; }

    /// <summary>The surface's hairline edge.</summary>
    public Color EdgeColor { get; set; }

    /// <summary>The hairline between the bar and a panel.</summary>
    public Color DividerColor { get; set; }

    /// <summary>The highlight that glides between items, the hovered search button and the active result.</summary>
    public Color HighlightColor { get; set; }

    /// <summary>A link or feature card under the pointer.</summary>
    public Color LinkHoverColor { get; set; }

    /// <summary>The action under the pointer.</summary>
    public Color ActionHoverColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        BarHeight = 56;
        CompactBarHeight = 46;
        BarPadding = new Thickness(10, 0, 8, 0);
        BarSpacing = 6;
        ItemHeight = 36;
        CompactItemHeight = 32;
        ItemPadding = new Thickness(14, 0);
        CompactItemPadding = new Thickness(11, 0);
        ItemSpacing = 4;
        ChevronSize = 14;
        BrandPadding = new Thickness(4, 0, 8, 0);
        BrandMarkSize = 26;
        BrandNameMargin = new Thickness(9, 0, 0, 0);
        ToolsMargin = new Thickness(2, 0, 0, 0);
        ToolsSpacing = 4;
        ToolGlyphSize = 17;
        ActionPadding = new Thickness(16, 0);
        CompactActionPadding = new Thickness(14, 0);
        ActionSpacing = 6;
        FacePadding = new Thickness(8);
        FaceMaxWidth = 700;
        FaceGutter = 16;
        DividerInset = 20;
        BodyTopPadding = 4;
        FeatureGap = 8;
        FeatureWidth = 216;
        LinkColumnWidth = 248;
        LinkGap = 2;
        LinkPadding = new Thickness(12, 10);
        LinkSpacing = 12;
        LinkIconSize = 20;
        LinkGlyphSize = 17;
        LabelLineHeight = 19.6;
        SmallLineHeight = 16.8;
        LinkTextGap = 1;
        DenseLinkPadding = new Thickness(8, 7);
        TinyLinkPadding = new Thickness(10, 7);
        DenseLinkSpacing = 8;
        DenseIconSize = 18;
        MenuHeadingPadding = new Thickness(8, 8, 8, 2);
        TinyMenuHeadingPadding = new Thickness(10, 8, 10, 2);
        MenuColumnGap = 4;
        MenuTwoColumnWidth = 260;
        FooterMargin = new Thickness(4, 6, 4, 0);
        FooterPadding = new Thickness(8, 10, 8, 2);
        FooterLinkSpacing = 6;
        FooterGlyphSize = 14;
        StoryPadding = new Thickness(6);
        StorySpacing = 4;
        StoryImageAspect = 1.6;
        StoryImageMargin = new Thickness(0, 0, 0, 8);
        StoryTextMargin = new Thickness(6, 0);
        SearchWidth = 544;
        SearchBarPadding = new Thickness(18, 0, 8, 0);
        SearchBarSpacing = 10;
        SearchIconSize = 18;
        ResultsHeadingPadding = new Thickness(12, 10, 12, 6);
        ResultPadding = new Thickness(12, 9);
        ResultSpacing = 12;
        EnterGlyphSize = 14;
        TinyViewportWidth = 420;
        FocusRingMargin = new Thickness(-4);
        SurfaceColor = Oklch.WithAlpha(n.SurfaceRaised, 0.94);
        EdgeColor = Oklch.WithAlpha(n.Border, 0.8);
        DividerColor = Oklch.WithAlpha(n.BorderSubtle, 0.7);
        HighlightColor = Oklch.WithAlpha(n.Foreground, 0.045);
        LinkHoverColor = Oklch.WithAlpha(n.Foreground, 0.035);
        ActionHoverColor = Oklch.MixOklab(n.Foreground, n.Background, 0.14);
    }
}
