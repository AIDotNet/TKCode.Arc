using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ModelMenu"/>, measured from the model picker's menu. Colors, radii, shadows and fonts come
/// from ArcDesign tokens in the theme (surface: Border, SurfaceRaised, RadiusPanel, ShadowFloating; rows and highlight:
/// SurfaceMuted, RadiusControl).
/// </summary>
[ControlDesignToken]
internal sealed class ModelMenuToken : AbstractControlDesignToken
{
    /// <summary>Width of the main menu (304), at most the window width minus <see cref="ViewportInset"/>.</summary>
    public double SurfaceWidth { get; set; }

    /// <summary>Width of the effort submenu (264).</summary>
    public double EffortMenuWidth { get; set; }

    /// <summary>Width of the "More models" submenu (272).</summary>
    public double MoreMenuWidth { get; set; }

    /// <summary>Space the main menu keeps from the window's left and right edges together (2 × 12).</summary>
    public double ViewportInset { get; set; }

    /// <summary>Padding of every list, inside the scroll area (6).</summary>
    public Thickness ListPadding { get; set; }

    /// <summary>Tallest a list gets before it scrolls (420: the "More models" list and every page in a narrow window).</summary>
    public double ListMaxHeight { get; set; }

    /// <summary>A featured model: mark and two lines (54).</summary>
    public double FeaturedRowHeight { get; set; }

    /// <summary>The Effort and More models rows and the back row of a page (40).</summary>
    public double ActionRowHeight { get; set; }

    /// <summary>An effort level: name and description (50).</summary>
    public double EffortRowHeight { get; set; }

    /// <summary>A model in the "More models" list: one line (38).</summary>
    public double CompactRowHeight { get; set; }

    /// <summary>Inline padding of a row (0 10).</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>An effort level has no mark, so its text starts 2px further in (0 10 0 12).</summary>
    public Thickness EffortRowPadding { get; set; }

    /// <summary>Space after a row's leading part (mark or icon): 12. A margin, so a missing part leaves no gap.</summary>
    public Thickness LeadingGap { get; set; }

    /// <summary>Space before a row's trailing parts (value, check): 12. Unselected rows have no check and no gap.</summary>
    public Thickness TrailingGap { get; set; }

    /// <summary>The same gaps in a compact row (10).</summary>
    public Thickness CompactLeadingGap { get; set; }

    public Thickness CompactTrailingGap { get; set; }

    public double FeaturedMarkSize { get; set; }

    public double CompactMarkSize { get; set; }

    /// <summary>The check of the selected row: Lucide Check 16 at stroke 2.</summary>
    public double CheckGlyphSize { get; set; }

    public double CheckStroke { get; set; }

    /// <summary>The Gauge / Layers3 icon of an action row and the back chevron (18).</summary>
    public double ActionGlyphSize { get; set; }

    /// <summary>The chevron at the end of an action row (15).</summary>
    public double ChevronGlyphSize { get; set; }

    /// <summary>Stroke of the row icons and chevrons (1.75).</summary>
    public double GlyphStroke { get; set; }

    /// <summary>The trailing chevron: 12 after the value, 2px past the row padding.</summary>
    public Thickness ChevronMargin { get; set; }

    /// <summary>Widest the value of an action row gets before it ends in an ellipsis (132).</summary>
    public double ValueMaxWidth { get; set; }

    /// <summary>Space around the separator line (6 on every side).</summary>
    public Thickness SeparatorMargin { get; set; }

    /// <summary>Padding of the first provider heading in "More models" (6 10 4).</summary>
    public Thickness GroupHeaderPadding { get; set; }

    /// <summary>Padding of every later provider heading: 4px more above (10 10 4).</summary>
    public Thickness GroupHeaderPaddingNext { get; set; }

    /// <summary>Line height of a 14px name in a two-line row (18.2).</summary>
    public double NameLineHeight { get; set; }

    /// <summary>Line height of a 12px description (16.2).</summary>
    public double BlurbLineHeight { get; set; }

    /// <summary>Line height of a 14px one-line label (19.6).</summary>
    public double LabelLineHeight { get; set; }

    /// <summary>Line height of the 12px "Default" note and the provider headings (15.6).</summary>
    public double NoteLineHeight { get; set; }

    /// <summary>Line height of the 12px tag in a compact row (16.8).</summary>
    public double TagLineHeight { get; set; }

    /// <summary>Tracking of 14px text (−0.01em).</summary>
    public double NameLetterSpacing { get; set; }

    /// <summary>The "Default" note: 8 after the level's name, lifted 0.6 so the 12px and 14px baselines line up.</summary>
    public Thickness DefaultNoteMargin { get; set; }

    /// <summary>A submenu's first row lines up with the row that opened it: list padding 6 + border 1 above it (−7).</summary>
    public double SubmenuOffset { get; set; }

    /// <summary>Gap between the menu and a submenu (1).</summary>
    public double SubmenuGap { get; set; }

    /// <summary>Distance a new page travels in while it fades in (12).</summary>
    public double PageTravel { get; set; }

    /// <summary>The inline (AI composer) layout's menu width (296).</summary>
    public double InlineSurfaceWidth { get; set; }

    /// <summary>Its list padding (5) and the highlight radius concentric with the 26px panel (26 − 5 − 1 = 20).</summary>
    public Thickness InlineListPadding { get; set; }

    public CornerRadius InlineHighlightRadius { get; set; }

    /// <summary>A model row of the inline layout: 48 high, padding 6 10 6 11, 10 between its parts, a 16px mark.</summary>
    public double InlineRowHeight { get; set; }

    public Thickness InlineRowPadding { get; set; }

    public Thickness InlineLeadingGap { get; set; }

    public Thickness InlineTrailingGap { get; set; }

    public double InlineMarkSize { get; set; }

    /// <summary>Line height of the inline layout's 12px text: the model line, the effort header and the level cells (16.8).</summary>
    public double InlineCaptionLineHeight { get; set; }

    /// <summary>The inline layout's separator reaches the panel's edges: 5 above and below, −5 at the sides.</summary>
    public Thickness InlineSeparatorMargin { get; set; }

    /// <summary>The "Effort" header row: padding 6 11, 12px between the title and the description.</summary>
    public Thickness EffortHeaderPadding { get; set; }

    public Thickness EffortHeaderGap { get; set; }

    /// <summary>The segmented row's track: margin 0 2 2, padding 3, radius 17 (1px border, muted fill).</summary>
    public Thickness EffortTrackMargin { get; set; }

    public Thickness EffortTrackPadding { get; set; }

    public CornerRadius EffortTrackRadius { get; set; }

    /// <summary>The level cells: 30 high, radius 13, 2 apart.</summary>
    public double EffortCellHeight { get; set; }

    public CornerRadius EffortCellRadius { get; set; }

    public double EffortCellGap { get; set; }

    /// <summary>The thumb's radius: its 1px border is drawn half a pixel inside the cell (13 − 0.5).</summary>
    public CornerRadius EffortThumbRadius { get; set; }

    /// <summary>The keyboard ring around the selected level: a 2px band just outside the cell (13 + 1 at its center line).</summary>
    public CornerRadius EffortRingRadius { get; set; }

    /// <summary>The thumb's spring, measured from Arc's composer: visual duration 0.35s, bounce 0.1.</summary>
    public double EffortThumbSpringDuration { get; set; }

    public double EffortThumbSpringBounce { get; set; }

    /// <summary>The simple layout (the chat's menu): at least 240 wide, as wide as its rows.</summary>
    public double SimpleMinWidth { get; set; }

    /// <summary>Its rows: 36px.</summary>
    public double SimpleRowHeight { get; set; }

    /// <summary>Its highlight: radius 12.</summary>
    public CornerRadius SimpleHighlightRadius { get; set; }

    /// <summary>Its check: 15.</summary>
    public double SimpleCheckSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SurfaceWidth = 304;
        SimpleMinWidth = 240;
        SimpleRowHeight = 36;
        SimpleHighlightRadius = new CornerRadius(12);
        SimpleCheckSize = 15;
        EffortMenuWidth = 264;
        MoreMenuWidth = 272;
        ViewportInset = 24;
        ListPadding = new Thickness(6);
        ListMaxHeight = 420;
        FeaturedRowHeight = 54;
        ActionRowHeight = 40;
        EffortRowHeight = 50;
        CompactRowHeight = 38;
        RowPadding = new Thickness(10, 0);
        EffortRowPadding = new Thickness(12, 0, 10, 0);
        LeadingGap = new Thickness(0, 0, 12, 0);
        TrailingGap = new Thickness(12, 0, 0, 0);
        CompactLeadingGap = new Thickness(0, 0, 10, 0);
        CompactTrailingGap = new Thickness(10, 0, 0, 0);
        FeaturedMarkSize = 18;
        CompactMarkSize = 16;
        CheckGlyphSize = 16;
        CheckStroke = 2;
        ActionGlyphSize = 18;
        ChevronGlyphSize = 15;
        GlyphStroke = 1.75;
        ChevronMargin = new Thickness(12, 0, -2, 0);
        ValueMaxWidth = 132;
        SeparatorMargin = new Thickness(6);
        GroupHeaderPadding = new Thickness(10, 6, 10, 4);
        GroupHeaderPaddingNext = new Thickness(10, 10, 10, 4);
        NameLineHeight = 18.2;
        BlurbLineHeight = 16.2;
        LabelLineHeight = 19.6;
        NoteLineHeight = 15.6;
        TagLineHeight = 16.8;
        NameLetterSpacing = -0.14;
        DefaultNoteMargin = new Thickness(8, 0, 0, 0.6);
        SubmenuOffset = -7;
        SubmenuGap = 1;
        PageTravel = 12;
        InlineSurfaceWidth = 296;
        InlineListPadding = new Thickness(5);
        InlineHighlightRadius = new CornerRadius(20);
        InlineRowHeight = 48;
        InlineRowPadding = new Thickness(11, 6, 10, 6);
        InlineLeadingGap = new Thickness(0, 0, 10, 0);
        InlineTrailingGap = new Thickness(10, 0, 0, 0);
        InlineMarkSize = 16;
        InlineCaptionLineHeight = 16.8;
        InlineSeparatorMargin = new Thickness(-5, 5, -5, 5);
        EffortHeaderPadding = new Thickness(11, 6, 11, 6);
        EffortHeaderGap = new Thickness(12, 0, 0, 0);
        EffortTrackMargin = new Thickness(2, 0, 2, 2);
        EffortTrackPadding = new Thickness(3);
        EffortTrackRadius = new CornerRadius(17);
        EffortCellHeight = 30;
        EffortCellRadius = new CornerRadius(13);
        EffortCellGap = 2;
        EffortThumbRadius = new CornerRadius(12.5);
        EffortRingRadius = new CornerRadius(14);
        EffortThumbSpringDuration = 0.35;
        EffortThumbSpringBounce = 0.1;
    }
}
