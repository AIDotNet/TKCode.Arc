// Ported from Arc's free notification-center block (MIT, Copyright (c) 2026 Elia Kuratli).
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="NotificationCenter"/> from notification-center.module.css. Colors, radii, shadows and fonts
/// come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class NotificationCenterToken : AbstractControlDesignToken
{
    /// <summary>The bell button (42px square, radius 12).</summary>
    public double TriggerSize { get; set; }

    public CornerRadius TriggerRadius { get; set; }

    /// <summary>Keyboard ring around the bell (2px outside the 2px offset, radius + 4).</summary>
    public CornerRadius TriggerRingRadius { get; set; }

    public double BellSize { get; set; }

    /// <summary>The unread badge: 20px pill, 0 5 padding, 2px surface ring, 6px past the top right corner.</summary>
    public double BadgeHeight { get; set; }

    public Thickness BadgePadding { get; set; }

    public Thickness BadgeRing { get; set; }

    public Thickness BadgeMargin { get; set; }

    /// <summary>Panel width (min(424px, 100vw − 24px)) and height cap (min(590px, 100vh − 24px)).</summary>
    public double PanelWidth { get; set; }

    public double PanelMaxHeight { get; set; }

    /// <summary>Space kept between the panel and the window edges (24 = 2 × collision padding).</summary>
    public double ViewportInset { get; set; }

    /// <summary>Gap between the bell and the panel (sideOffset 12).</summary>
    public double PanelGap { get; set; }

    /// <summary>Viewport width at or below which the panel switches to its narrow layout (380).</summary>
    public double NarrowBreakpoint { get; set; }

    public Thickness HeaderPadding { get; set; }

    public Thickness HeaderPaddingNarrow { get; set; }

    public double HeadingGap { get; set; }

    /// <summary>The heading's line height (1.2 × text-xl; 1.2 × text-lg in the narrow layout).</summary>
    public double HeadingLineHeight { get; set; }

    public double HeadingLineHeightNarrow { get; set; }

    /// <summary>The unread count chip beside the heading (21px, radius 7).</summary>
    public double CountSize { get; set; }

    public Thickness CountPadding { get; set; }

    public CornerRadius CountRadius { get; set; }

    public Thickness SummaryMargin { get; set; }

    public double BodyLineHeight { get; set; }

    public double CloseSize { get; set; }

    public CornerRadius CloseRadius { get; set; }

    public double CloseGlyphSize { get; set; }

    public double ToolbarMinHeight { get; set; }

    public Thickness ToolbarPadding { get; set; }

    public Thickness ToolbarPaddingNarrow { get; set; }

    /// <summary>All / Unread buttons: 31px high, at least 61 wide, radius 9, 3px apart.</summary>
    public double ViewButtonHeight { get; set; }

    public double ViewButtonMinWidth { get; set; }

    public Thickness ViewButtonPadding { get; set; }

    public CornerRadius ViewButtonRadius { get; set; }

    public double ViewButtonGap { get; set; }

    public Thickness MarkAllPadding { get; set; }

    public double MarkAllGap { get; set; }

    public double MarkAllGlyphSize { get; set; }

    public Thickness ListPadding { get; set; }

    public Thickness ListPaddingNarrow { get; set; }

    /// <summary>A row: 10px padding, radius panel − 12, 11px between its columns.</summary>
    public Thickness RowPadding { get; set; }

    public Thickness RowPaddingNarrow { get; set; }

    /// <summary>Half the 4px gap between rows, above and below each one; it closes with the row it belongs to.</summary>
    public Thickness RowGap { get; set; }

    public CornerRadius RowRadius { get; set; }

    /// <summary>The title column keeps 11px (narrow: 7px) from the visual and the time.</summary>
    public Thickness RowColumnGap { get; set; }

    public Thickness RowColumnGapNarrow { get; set; }

    /// <summary>The tone icon box (36px) and its 18px glyph at stroke 1.7.</summary>
    public double EventIconSize { get; set; }

    public double EventGlyphSize { get; set; }

    public double EventGlyphStroke { get; set; }

    /// <summary>The title button's top padding (2) and the gap between title and preview (4).</summary>
    public Thickness TogglePadding { get; set; }

    public double ToggleGap { get; set; }

    public double TitleMinHeight { get; set; }

    public double TitleGap { get; set; }

    public double TitleLineHeight { get; set; }

    public double UnreadDotSize { get; set; }

    public Thickness TimeMargin { get; set; }

    /// <summary>Details sit under the text, past the 36px visual and the 11px gap.</summary>
    public Thickness DetailsIndent { get; set; }

    public Thickness DetailsIndentNarrow { get; set; }

    public Thickness DetailsTextMargin { get; set; }

    public Thickness ActionsMargin { get; set; }

    public double ActionsGap { get; set; }

    public double ActionsGapNarrow { get; set; }

    /// <summary>Row actions: 30px high, 0 10 padding, radius 10, 5px between glyph and label.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    public Thickness ActionPaddingNarrow { get; set; }

    public CornerRadius ActionRadius { get; set; }

    public double ActionGap { get; set; }

    public double ActionGlyphSize { get; set; }

    public double EmptyMinHeight { get; set; }

    public Thickness EmptyPadding { get; set; }

    public Thickness EmptyIconMargin { get; set; }

    public double EmptyIconSize { get; set; }

    public Thickness EmptyDescriptionMargin { get; set; }

    public Thickness EmptyLinkMargin { get; set; }

    public double FooterMinHeight { get; set; }

    public Thickness FooterPadding { get; set; }

    public Thickness FooterPaddingNarrow { get; set; }

    public Thickness FooterButtonPadding { get; set; }

    /// <summary>Keyboard ring of the buttons inside the panel (2px, 4px outside the button).</summary>
    public Thickness RingMargin { get; set; }

    public CornerRadius CloseRingRadius { get; set; }

    public CornerRadius ViewRingRadius { get; set; }

    public CornerRadius ActionRingRadius { get; set; }

    /// <summary>Ring of the text buttons and the row title (they have no radius of their own).</summary>
    public CornerRadius TextRingRadius { get; set; }

    public Thickness ActionGlyphMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerSize = 42;
        TriggerRadius = new CornerRadius(12);
        TriggerRingRadius = new CornerRadius(16);
        BellSize = 19;
        BadgeHeight = 20;
        BadgePadding = new Thickness(5, 0);
        BadgeRing = new Thickness(2);
        // 6px past the corner of the padding box, inside the 1px border.
        BadgeMargin = new Thickness(0, -5, -5, 0);
        PanelWidth = 424;
        PanelMaxHeight = 590;
        ViewportInset = 24;
        PanelGap = 12;
        NarrowBreakpoint = 380;
        HeaderPadding = new Thickness(24, 23, 24, 18);
        HeaderPaddingNarrow = new Thickness(16, 20, 16, 16);
        HeadingGap = 8;
        HeadingLineHeight = 22 * 1.2;
        HeadingLineHeightNarrow = 18 * 1.2;
        CountSize = 21;
        CountPadding = new Thickness(5, 0);
        CountRadius = new CornerRadius(7);
        SummaryMargin = new Thickness(0, 4, 0, 0);
        BodyLineHeight = 12 * 1.4;
        CloseSize = 30;
        CloseRadius = new CornerRadius(8);
        CloseGlyphSize = 17;
        ToolbarMinHeight = 52;
        ToolbarPadding = new Thickness(20, 0, 17, 13);
        ToolbarPaddingNarrow = new Thickness(12, 0, 12, 12);
        ViewButtonHeight = 31;
        ViewButtonMinWidth = 61;
        ViewButtonPadding = new Thickness(10, 0);
        ViewButtonRadius = new CornerRadius(9);
        ViewButtonGap = 3;
        MarkAllPadding = new Thickness(0, 6);
        MarkAllGap = 6;
        MarkAllGlyphSize = 15;
        ListPadding = new Thickness(11, 7, 11, 8);
        ListPaddingNarrow = new Thickness(5, 6);
        RowPadding = new Thickness(10);
        RowPaddingNarrow = new Thickness(7, 10);
        RowGap = new Thickness(0, 2);
        RowRadius = new CornerRadius(14);
        RowColumnGap = new Thickness(11, 0);
        RowColumnGapNarrow = new Thickness(7, 0);
        EventIconSize = 36;
        EventGlyphSize = 18;
        EventGlyphStroke = 1.7;
        TogglePadding = new Thickness(0, 2, 0, 0);
        ToggleGap = 4;
        TitleMinHeight = 20;
        TitleGap = 6;
        TitleLineHeight = 14 * 1.4;
        UnreadDotSize = 6;
        TimeMargin = new Thickness(0, 4, 0, 0);
        DetailsIndent = new Thickness(47, 0, 0, 0);
        DetailsIndentNarrow = new Thickness(43, 0, 0, 0);
        DetailsTextMargin = new Thickness(0, 12);
        ActionsMargin = new Thickness(0, 0, 0, 5);
        ActionsGap = 6;
        ActionsGapNarrow = 5;
        ActionHeight = 30;
        ActionPadding = new Thickness(10, 0);
        ActionPaddingNarrow = new Thickness(7, 0);
        ActionRadius = new CornerRadius(10);
        ActionGap = 5;
        ActionGlyphSize = 14;
        EmptyMinHeight = 190;
        EmptyPadding = new Thickness(12, 22);
        EmptyIconMargin = new Thickness(0, 0, 0, 13);
        EmptyIconSize = 24;
        EmptyDescriptionMargin = new Thickness(0, 4, 0, 0);
        EmptyLinkMargin = new Thickness(0, 16, 0, 0);
        FooterMinHeight = 46;
        FooterPadding = new Thickness(22, 8);
        FooterPaddingNarrow = new Thickness(16, 8);
        FooterButtonPadding = new Thickness(0, 4);
        RingMargin = new Thickness(-4);
        CloseRingRadius = new CornerRadius(12);
        ViewRingRadius = new CornerRadius(13);
        ActionRingRadius = new CornerRadius(14);
        TextRingRadius = new CornerRadius(8);
        ActionGlyphMargin = new Thickness(0, 0, 5, 0);
    }
}
