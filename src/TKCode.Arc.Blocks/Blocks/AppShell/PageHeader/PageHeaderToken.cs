using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

// Geometry from Arc's free page-header block (page-header.module.css; MIT, Copyright (c) 2026 Elia Kuratli).
[ControlDesignToken]
internal sealed class PageHeaderToken : AbstractControlDesignToken
{
    /// <summary>Arc: .frame border-radius 16px.</summary>
    public CornerRadius FrameRadius { get; set; }

    /// <summary>Header padding: 16px var(--pad) 0, with --pad 28px (18px below 560px).</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness HeaderPaddingNarrow { get; set; }

    /// <summary>Scroller padding: 28px var(--pad) 84px.</summary>
    public Thickness BodyPadding { get; set; }

    public Thickness BodyPaddingNarrow { get; set; }

    /// <summary>Gap between the lead cell (breadcrumbs or compact title) and the actions (Arc: 16px).</summary>
    public double BarGap { get; set; }

    /// <summary>Gap between header buttons (Arc: 8px), also kept after the secondary actions.</summary>
    public double ActionGap { get; set; }

    public Thickness SecondaryTrailing { get; set; }

    /// <summary>Lucide glyph in an action button (16px, stroke 1.75) and the overflow Ellipsis (17px).</summary>
    public double ActionGlyphSize { get; set; }

    public double OverflowGlyphSize { get; set; }

    /// <summary>Gap between the icon and the label of an action button (Arc button: space-2).</summary>
    public double ActionContentGap { get; set; }

    /// <summary>Intro padding: 18px 0 20px (16px on top below 560px), 10px between rows.</summary>
    public Thickness IntroPadding { get; set; }

    public Thickness IntroPaddingNarrow { get; set; }

    public double IntroGap { get; set; }

    /// <summary>Title row gap: 8px between lines, 14px between title and status.</summary>
    public double TitleRowItemGap { get; set; }

    public double TitleRowLineGap { get; set; }

    /// <summary>Description and meta: 14px apart.</summary>
    public double DetailsGap { get; set; }

    /// <summary>Description max-width 60ch (60 "0" glyphs of the 14px body face).</summary>
    public double DescriptionMaxWidth { get; set; }

    /// <summary>text-3xl / text-2xl × leading-display, tracking-display.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLineHeightNarrow { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double TitleLetterSpacingNarrow { get; set; }

    /// <summary>text-sm × leading-body.</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Tabs bleed to the frame edges (margin 2px −pad 0) and pad to pad − 10px so the first label lines up with the title.</summary>
    public Thickness TabsMargin { get; set; }

    public Thickness TabsMarginNarrow { get; set; }

    public Thickness TabsPadding { get; set; }

    /// <summary>Below 560px the far side keeps pad + 14px free for the fading edge.</summary>
    public Thickness TabsPaddingNarrow { get; set; }

    public double TabGap { get; set; }

    /// <summary>The fading right edge of the tab row below 560px (28px).</summary>
    public double TabsFadeWidth { get; set; }

    /// <summary>Indicator: 2px high, 10px in from each side of the tab, rounded on top.</summary>
    public double IndicatorHeight { get; set; }

    public double IndicatorInset { get; set; }

    public CornerRadius IndicatorRadius { get; set; }

    /// <summary>Compact title button: padding 5px 8px, margin-left −8px, radius 8px, 10px before the badge.</summary>
    public Thickness CompactPadding { get; set; }

    public Thickness CompactMargin { get; set; }

    public CornerRadius CompactRadius { get; set; }

    public Thickness CompactBadgeMargin { get; set; }

    /// <summary>Hover fill of the compact title: color-mix(foreground 4.5%, transparent).</summary>
    public Color CompactHoverFill { get; set; }

    /// <summary>Crumb buttons: padding 4px 2px, radius 8px; 6px between a crumb and its chevron; 14px chevrons.</summary>
    public Thickness CrumbPadding { get; set; }

    public CornerRadius CrumbRadius { get; set; }

    public double CrumbGap { get; set; }

    public double CrumbChevronSize { get; set; }

    /// <summary>Status dot: 6px circle in the badge tone.</summary>
    public double StatusDotSize { get; set; }

    /// <summary>Focus ring of the crumbs and the compact title: 2px outside their 8px corners.</summary>
    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    /// <summary>Focus ring around the body panel (4px out).</summary>
    public CornerRadius BodyFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        FrameRadius = new CornerRadius(16);
        HeaderPadding = new Thickness(28, 16, 28, 0);
        HeaderPaddingNarrow = new Thickness(18, 16, 18, 0);
        BodyPadding = new Thickness(28, 28, 28, 84);
        BodyPaddingNarrow = new Thickness(18, 28, 18, 84);
        BarGap = 16;
        ActionGap = 8;
        SecondaryTrailing = new Thickness(0, 0, 8, 0);
        ActionGlyphSize = 16;
        OverflowGlyphSize = 17;
        ActionContentGap = 8;
        IntroPadding = new Thickness(0, 18, 0, 20);
        IntroPaddingNarrow = new Thickness(0, 16, 0, 20);
        IntroGap = 10;
        TitleRowItemGap = 14;
        TitleRowLineGap = 8;
        DetailsGap = 14;
        DescriptionMaxWidth = 528;
        TitleLineHeight = 36 * 1.1;
        TitleLineHeightNarrow = 28 * 1.1;
        TitleLetterSpacing = 36 * -0.03;
        TitleLetterSpacingNarrow = 28 * -0.03;
        BodyLineHeight = 14 * 1.4;
        TabsMargin = new Thickness(-28, 2, -28, 0);
        TabsMarginNarrow = new Thickness(-18, 2, -18, 0);
        TabsPadding = new Thickness(18, 0, 18, 0);
        TabsPaddingNarrow = new Thickness(8, 0, 32, 0);
        TabGap = 2;
        TabsFadeWidth = 28;
        IndicatorHeight = 2;
        IndicatorInset = 10;
        IndicatorRadius = new CornerRadius(2, 2, 0, 0);
        CompactPadding = new Thickness(8, 5);
        CompactMargin = new Thickness(-8, 0, 0, 0);
        CompactRadius = new CornerRadius(8);
        CompactBadgeMargin = new Thickness(10, 0, 0, 0);
        CompactHoverFill = Oklch.WithAlpha(neutrals.Foreground, 0.045);
        CrumbPadding = new Thickness(2, 4);
        CrumbRadius = new CornerRadius(8);
        CrumbGap = 6;
        CrumbChevronSize = 14;
        StatusDotSize = 6;
        FocusRingMargin = new Thickness(-2);
        FocusRingRadius = new CornerRadius(10);
        BodyFocusRingRadius = new CornerRadius(12);
    }
}
