using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

// Geometry from Arc's free site-header block (site-header.module.css; MIT, Copyright (c) 2026 Elia Kuratli).
[ControlDesignToken]
internal sealed class SiteHeaderToken : AbstractControlDesignToken
{
    /// <summary>The bar is 64 high; its content is at most 1200 wide with 24 (16 when narrow) on each side.</summary>
    public double BarHeight { get; set; }

    public double InnerMaxWidth { get; set; }

    public Thickness InnerPadding { get; set; }

    public Thickness InnerPaddingNarrow { get; set; }

    /// <summary>Gap between brand, navigation and actions (8).</summary>
    public double BarGap { get; set; }

    /// <summary>The brand: radius 12, padding 8 8 8 0, mark 22, 8 between mark and name.</summary>
    public CornerRadius BrandRadius { get; set; }

    public Thickness BrandPadding { get; set; }

    public double BrandMarkSize { get; set; }

    public double BrandGap { get; set; }

    /// <summary>Navigation: 16 from the brand (not in the centered layout), 2 between items.</summary>
    public double NavInset { get; set; }

    public double NavItemGap { get; set; }

    /// <summary>The centered layout's capsule: padding 3 inside a 1px border.</summary>
    public Thickness CapsulePadding { get; set; }

    /// <summary>A navigation item: 36 high, radius 12, padding 0 12, 4 between label and chevron.</summary>
    public double NavItemHeight { get; set; }

    public CornerRadius NavItemRadius { get; set; }

    public Thickness NavItemPadding { get; set; }

    public double NavItemContentGap { get; set; }

    /// <summary>The centered layout's item: 32 high, pill, padding 0 14.</summary>
    public double NavItemHeightCentered { get; set; }

    public Thickness NavItemPaddingCentered { get; set; }

    /// <summary>The trigger's chevron (14, stroke 2).</summary>
    public double ChevronSize { get; set; }

    public double ChevronStroke { get; set; }

    /// <summary>The current-link underline: 2 high, 12 from the left, 12 (30 with a chevron) from the right, 4 above the bottom.</summary>
    public double UnderlineHeight { get; set; }

    public double UnderlineInset { get; set; }

    public double UnderlineChevronInset { get; set; }

    public double UnderlineBottom { get; set; }

    /// <summary>Actions: 36 high, padding 0 14, 8 apart; pressed scale .97.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    public double ActionGap { get; set; }

    public double PressScale { get; set; }

    /// <summary>The menu button (narrow): 40 square, radius 12, 20px icon with stroke 1.75.</summary>
    public double MenuButtonSize { get; set; }

    public CornerRadius MenuButtonRadius { get; set; }

    public double MenuGlyphSize { get; set; }

    public double MenuGlyphStroke { get; set; }

    /// <summary>The mega panel: its top is 4 above the bar bottom (60 from the inner top), 24 from each inner edge.</summary>
    public double PanelTop { get; set; }

    public double PanelInset { get; set; }

    /// <summary>A face: padding 12; 8 between links and feature; the feature column is 200 to 260 wide; links 2 apart.</summary>
    public Thickness FacePadding { get; set; }

    public double FaceGap { get; set; }

    public double FeatureMinWidth { get; set; }

    public double FeatureMaxWidth { get; set; }

    public double LinkGridGap { get; set; }

    /// <summary>A panel link: radius 16, padding 12, 12 between icon and text, 2 between title and description, icon 16 lowered by -2.</summary>
    public CornerRadius PanelLinkRadius { get; set; }

    public Thickness PanelLinkPadding { get; set; }

    public double PanelLinkGap { get; set; }

    public double PanelLinkTextGap { get; set; }

    public double LinkIconSize { get; set; }

    public double LinkIconStroke { get; set; }

    public double LinkIconOffset { get; set; }

    /// <summary>The feature card: radius 18, padding 8 8 12, 6 between rows, picture radius 12 at 16:10 with 4 below, text inset 4.</summary>
    public CornerRadius FeatureRadius { get; set; }

    public Thickness FeaturePadding { get; set; }

    public double FeatureGap { get; set; }

    public CornerRadius FeatureImageRadius { get; set; }

    public double FeatureImageRatio { get; set; }

    public double FeatureImageSpace { get; set; }

    public Thickness FeatureTextPadding { get; set; }

    public double FeatureArrowGap { get; set; }

    /// <summary>The picture grows to 1.03 and the arrow moves 2 right when the card is hovered.</summary>
    public double FeatureImageHoverScale { get; set; }

    public double FeatureArrowShift { get; set; }

    /// <summary>The sheet's content: padding 8 16 20; it is at most the scroll viewport's height minus 65.</summary>
    public Thickness SheetPadding { get; set; }

    public double SheetChrome { get; set; }

    /// <summary>A sheet row: at least 52 high, padding 0 4, chevron 18, current dot 6.</summary>
    public double SheetRowMinHeight { get; set; }

    public Thickness SheetRowPadding { get; set; }

    public double SheetChevronSize { get; set; }

    public double SheetDotSize { get; set; }

    /// <summary>A sheet link: at least 44 high, 12 between icon and text, radius 12, padding 0 8; 12 below a group.</summary>
    public double SheetLinkMinHeight { get; set; }

    public double SheetLinkGap { get; set; }

    public CornerRadius SheetLinkRadius { get; set; }

    public Thickness SheetLinkPadding { get; set; }

    public double SheetGroupSpace { get; set; }

    /// <summary>The action row of the sheet: two equal columns 8 apart, 20 from the list.</summary>
    public double SheetActionsGap { get; set; }

    public double SheetActionsTop { get; set; }

    /// <summary>The scrim over the page behind the sheet: black at 16%.</summary>
    public Color ScrimColor { get; set; }

    /// <summary>Label text letter spacing (-0.01em of 14 / 16px).</summary>
    public double LabelLetterSpacing { get; set; }

    public double BrandLetterSpacing { get; set; }

    /// <summary>Line height of the 14px body lines (text-sm × leading-body 1.4).</summary>
    public double BodyLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarHeight = 64;
        InnerMaxWidth = 1200;
        InnerPadding = new Thickness(24, 0);
        InnerPaddingNarrow = new Thickness(16, 0);
        BarGap = 8;
        BrandRadius = new CornerRadius(12);
        BrandPadding = new Thickness(0, 8, 8, 8);
        BrandMarkSize = 22;
        BrandGap = 8;
        NavInset = 16;
        NavItemGap = 2;
        CapsulePadding = new Thickness(3);
        NavItemHeight = 36;
        NavItemRadius = new CornerRadius(12);
        NavItemPadding = new Thickness(12, 0);
        NavItemContentGap = 4;
        NavItemHeightCentered = 32;
        NavItemPaddingCentered = new Thickness(14, 0);
        ChevronSize = 14;
        ChevronStroke = 2;
        UnderlineHeight = 2;
        UnderlineInset = 12;
        UnderlineChevronInset = 30;
        UnderlineBottom = 4;
        ActionHeight = 36;
        ActionPadding = new Thickness(14, 0);
        ActionGap = 8;
        PressScale = 0.97;
        MenuButtonSize = 40;
        MenuButtonRadius = new CornerRadius(12);
        MenuGlyphSize = 20;
        MenuGlyphStroke = 1.75;
        PanelTop = 60;
        PanelInset = 24;
        FacePadding = new Thickness(12);
        FaceGap = 8;
        FeatureMinWidth = 200;
        FeatureMaxWidth = 260;
        LinkGridGap = 2;
        PanelLinkRadius = new CornerRadius(16);
        PanelLinkPadding = new Thickness(12);
        PanelLinkGap = 12;
        PanelLinkTextGap = 2;
        LinkIconSize = 16;
        LinkIconStroke = 1.75;
        LinkIconOffset = 2;
        FeatureRadius = new CornerRadius(18);
        FeaturePadding = new Thickness(8, 8, 8, 12);
        FeatureGap = 6;
        FeatureImageRadius = new CornerRadius(12);
        FeatureImageRatio = 1.6;
        FeatureImageSpace = 4;
        FeatureTextPadding = new Thickness(4, 0);
        FeatureArrowGap = 6;
        FeatureImageHoverScale = 1.03;
        FeatureArrowShift = 2;
        SheetPadding = new Thickness(16, 8, 16, 20);
        SheetChrome = 65;
        SheetRowMinHeight = 52;
        SheetRowPadding = new Thickness(4, 0);
        SheetChevronSize = 18;
        SheetDotSize = 6;
        SheetLinkMinHeight = 44;
        SheetLinkGap = 12;
        SheetLinkRadius = new CornerRadius(12);
        SheetLinkPadding = new Thickness(8, 0);
        SheetGroupSpace = 12;
        SheetActionsGap = 8;
        SheetActionsTop = 20;
        ScrimColor = Color.FromArgb(0x29, 0, 0, 0);
        LabelLetterSpacing = -0.14;
        BrandLetterSpacing = -0.16;
        BodyLineHeight = 19.6;
    }
}
