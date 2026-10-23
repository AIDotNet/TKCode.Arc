using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SemanticZoom"/> (Arc Semantic zoom, design reference, measured at 663 / 1312 px and in a 390 px
/// window): the frame, header, map overlays (breadcrumbs, tour button, minimap, hint, zoom bar) and hover card. Colors,
/// shadows and type come from ArcDesign tokens in the theme; the map itself is self-drawn from the same tokens.
/// </summary>
[ControlDesignToken]
internal sealed class SemanticZoomToken : AbstractControlDesignToken
{
    /// <summary>Corner radius of the frame (16).</summary>
    public CornerRadius FrameRadius { get; set; }

    /// <summary>Inset of the header (16 above and below, 20 at the sides) and the gap between its lines (2).</summary>
    public Thickness HeaderInset { get; set; }

    public double HeaderGap { get; set; }

    /// <summary>The heading: letter spacing −0.48, line height 17.6.</summary>
    public double TitleTracking { get; set; }

    public double TitleLeading { get; set; }

    /// <summary>Body text of the map's interface (13 px; Arc's interface type is between TypeXS and TypeSM).</summary>
    public double MapTextSize { get; set; }

    /// <summary>Margin of the overlays from the map's edges (12).</summary>
    public Thickness OverlayMargin { get; set; }

    /// <summary>Overlay surfaces: radius 10, 32 high.</summary>
    public CornerRadius OverlayRadius { get; set; }

    public double OverlayExtent { get; set; }

    /// <summary>Breadcrumb track inset (0 4), crumb inset (0 8), crumb height 24, crumb radius 6, chevron 14.</summary>
    public Thickness CrumbTrackInset { get; set; }

    public Thickness CrumbInset { get; set; }

    public double CrumbExtent { get; set; }

    public CornerRadius CrumbRadius { get; set; }

    public double CrumbGlyphSize { get; set; }

    public Thickness CrumbSeparatorMargin { get; set; }

    /// <summary>Gap between the breadcrumbs and the tour button.</summary>
    public double TopBarGap { get; set; }

    /// <summary>Tour button inset (0 12 0 10), glyph 16, glyph gap 6, label rise 5.</summary>
    public Thickness TourInset { get; set; }

    public double TourGlyphSize { get; set; }

    public double TourGlyphGap { get; set; }

    public double TourLabelRise { get; set; }

    /// <summary>Minimap frame 172 × 88 (128 × 66 compact) and its rise while appearing (6).</summary>
    public double MinimapWidth { get; set; }

    public double MinimapHeight { get; set; }

    public double CompactMinimapWidth { get; set; }

    public double CompactMinimapHeight { get; set; }

    public double MinimapRise { get; set; }

    /// <summary>Hint margin from the bottom-left corner (20; compact: above the zoom bar).</summary>
    public Thickness HintMargin { get; set; }

    public Thickness CompactHintMargin { get; set; }

    /// <summary>Hover card width (252), inset (12 14), radius 12, rule spacing 10, row gap 4, avatar 18, dot 6.</summary>
    public double CardWidth { get; set; }

    public Thickness CardInset { get; set; }

    public CornerRadius CardRadius { get; set; }

    public Thickness CardRuleMargin { get; set; }

    public double CardRowGap { get; set; }

    public double CardAvatarSize { get; set; }

    public double CardDot { get; set; }

    /// <summary>Keyboard focus rings (2 px, 2 px outside) of the crumbs and the tour button.</summary>
    public Thickness FocusRingMargin { get; set; }

    public CornerRadius CrumbFocusRadius { get; set; }

    public CornerRadius TourFocusRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FrameRadius = new CornerRadius(16);
        HeaderInset = new Thickness(20, 16);
        HeaderGap = 2;
        TitleTracking = -0.48;
        TitleLeading = 17.6;
        MapTextSize = 13;
        OverlayMargin = new Thickness(12);
        OverlayRadius = new CornerRadius(10);
        OverlayExtent = 32;
        CrumbTrackInset = new Thickness(4, 0);
        CrumbInset = new Thickness(8, 0);
        CrumbExtent = 24;
        CrumbRadius = new CornerRadius(6);
        CrumbGlyphSize = 14;
        CrumbSeparatorMargin = new Thickness(1, 0);
        TopBarGap = 12;
        TourInset = new Thickness(10, 0, 12, 0);
        TourGlyphSize = 16;
        TourGlyphGap = 6;
        TourLabelRise = 5;
        MinimapWidth = 172;
        MinimapHeight = 88;
        CompactMinimapWidth = 128;
        CompactMinimapHeight = 66;
        MinimapRise = 6;
        HintMargin = new Thickness(20, 0, 0, 20);
        CompactHintMargin = new Thickness(20, 0, 0, 58);
        CardWidth = 252;
        CardInset = new Thickness(14, 12);
        CardRadius = new CornerRadius(12);
        CardRuleMargin = new Thickness(0, 10, 0, 0);
        CardRowGap = 4;
        CardAvatarSize = 18;
        CardDot = 6;
        FocusRingMargin = new Thickness(-4);
        CrumbFocusRadius = new CornerRadius(10);
        TourFocusRadius = new CornerRadius(14);
    }
}

/// <summary>
/// Geometry of <see cref="SemanticZoomPanel"/> (Arc Semantic zoom, design reference): insets, the two-column grid (at most
/// 768 px, columns 5 : 6, 32 apart, rows 20 apart), the numbers row, the progress bar and the avatar stack.
/// </summary>
[ControlDesignToken]
internal sealed class SemanticZoomPanelToken : AbstractControlDesignToken
{
    /// <summary>Inset of the panel (16 above, 20 at the sides and below).</summary>
    public Thickness PanelInset { get; set; }

    public double PanelColumnGap { get; set; }

    public double PanelRowGap { get; set; }

    public double GridMaxWidth { get; set; }

    /// <summary>Gap between the three numbers (12) and between a label and its value (2).</summary>
    public double NumberGap { get; set; }

    public double NumberLabelGap { get; set; }

    /// <summary>Line heights: labels 16.8, numbers 22.4, the large percentage 33.6 (letter spacing −0.56).</summary>
    public double LabelLeading { get; set; }

    public double NumberLeading { get; set; }

    public double ProgressLeading { get; set; }

    public double ProgressTracking { get; set; }

    /// <summary>Progress bar height (4) and its gap to the percentage (4).</summary>
    public double BarExtent { get; set; }

    public double BarGap { get; set; }

    /// <summary>People: rule spacing (18 above the block), label column 156, 16 to the stack, 8 between rows.</summary>
    public double PeopleRuleSpacing { get; set; }

    public double PeopleLabelWidth { get; set; }

    public double PeopleColumnGap { get; set; }

    public double PeopleRowGap { get; set; }

    /// <summary>Avatars: 28 px circles, 6 px overlap, 2 px surface ring.</summary>
    public double AvatarSize { get; set; }

    public double AvatarOverlap { get; set; }

    public double AvatarRing { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelInset = new Thickness(20, 16, 20, 20);
        PanelColumnGap = 32;
        PanelRowGap = 20;
        GridMaxWidth = 768;
        NumberGap = 12;
        NumberLabelGap = 2;
        LabelLeading = 16.8;
        NumberLeading = 22.4;
        ProgressLeading = 33.6;
        ProgressTracking = -0.56;
        BarExtent = 4;
        BarGap = 4;
        PeopleRuleSpacing = 18;
        PeopleLabelWidth = 156;
        PeopleColumnGap = 16;
        PeopleRowGap = 8;
        AvatarSize = 28;
        AvatarOverlap = -6;
        AvatarRing = 2;
    }
}
