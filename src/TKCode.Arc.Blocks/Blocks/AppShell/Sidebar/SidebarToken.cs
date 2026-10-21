using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry of the <see cref="Sidebar"/> block (an original TKCode design built from Arc's tokens).</summary>
[ControlDesignToken]
internal sealed class SidebarToken : AbstractControlDesignToken
{
    /// <summary>Width of the open sidebar.</summary>
    public double ExpandedWidth { get; set; }

    /// <summary>Width of the icon rail (64: the 40px rows plus the 12px inset on each side).</summary>
    public double RailWidth { get; set; }

    /// <summary>Padding of the pane around its rows; with the rows' own margins every edge ends up 12 from the content.</summary>
    public Thickness PanePadding { get; set; }

    /// <summary>Header row (the header content and the collapse button); every pane row is inset 12 from the edges.</summary>
    public Thickness HeaderMargin { get; set; }

    public Thickness FilterMargin { get; set; }

    public Thickness FooterMargin { get; set; }

    /// <summary>Room above the first row and below the last one, so focus rings are never cut off.</summary>
    public Thickness ListMargin { get; set; }

    /// <summary>Width of the collapse and rail search buttons: the rail's row width, so they line up with the rows.</summary>
    public double IconButtonWidth { get; set; }

    /// <summary>Row height (34).</summary>
    public double RowHeight { get; set; }

    /// <summary>Space between two rows.</summary>
    public double RowGap { get; set; }

    public CornerRadius RowRadius { get; set; }

    /// <summary>
    /// Row inline padding. 12 rather than 10 so the 16px icon sits exactly in the middle of the 40px rail row and never
    /// moves when the sidebar collapses.
    /// </summary>
    public Thickness RowInlinePadding { get; set; }

    public double RowGlyphSize { get; set; }

    public double RowGlyphStroke { get; set; }

    /// <summary>Gap between the icon and the label.</summary>
    public Thickness RowGlyphMargin { get; set; }

    public Thickness BadgeMargin { get; set; }

    public double ChevronSize { get; set; }

    public double ChevronStroke { get; set; }

    public Thickness ChevronMargin { get; set; }

    /// <summary>The 4.5% foreground wash behind a hovered row.</summary>
    public Color RowHoverFill { get; set; }

    /// <summary>Indent of child pages (26).</summary>
    public Thickness ChildrenMargin { get; set; }

    /// <summary>Position of the 1px guide the child pages hang from (under the parent's icon).</summary>
    public Thickness GuideMargin { get; set; }

    /// <summary>Space above a section (16), and the height of a section heading row.</summary>
    public double SectionGap { get; set; }

    /// <summary>The section gap above a heading's title row.</summary>
    public Thickness SectionTitleMargin { get; set; }

    /// <summary>The 1px section break on the rail, inset from the row edges.</summary>
    public Thickness SeparatorMargin { get; set; }

    public double SectionHeaderHeight { get; set; }

    public double SectionChevronSize { get; set; }

    /// <summary>Selection marker accent bar: 3 by 16, 4px in from the row's left edge.</summary>
    public double MarkerBarWidth { get; set; }

    public double MarkerBarHeight { get; set; }

    public double MarkerBarInset { get; set; }

    public CornerRadius MarkerBarRadius { get; set; }

    /// <summary>The rail count dot: 6px, at the top right of the icon.</summary>
    public double BadgeDotSize { get; set; }

    public Thickness BadgeDotMargin { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ExpandedWidth = 260;
        RailWidth = 64;
        PanePadding = new Thickness(0, 8, 0, 4);
        HeaderMargin = new Thickness(12, 4, 12, 4);
        FilterMargin = new Thickness(12, 4, 12, 6);
        FooterMargin = new Thickness(12, 8, 12, 8);
        ListMargin = new Thickness(12, 4, 12, 8);
        IconButtonWidth = 40;
        RowHeight = 34;
        RowGap = 2;
        RowRadius = new CornerRadius(10);
        RowInlinePadding = new Thickness(12, 0);
        RowGlyphSize = 16;
        RowGlyphStroke = 1.75;
        RowGlyphMargin = new Thickness(0, 0, 10, 0);
        BadgeMargin = new Thickness(8, 0, 0, 0);
        ChevronSize = 14;
        ChevronStroke = 2;
        ChevronMargin = new Thickness(6, 0, 0, 0);
        RowHoverFill = Oklch.WithAlpha(n.Foreground, 0.045);
        ChildrenMargin = new Thickness(26, 0, 0, 0);
        GuideMargin = new Thickness(20, 2, 0, 2);
        SectionGap = 16;
        SectionTitleMargin = new Thickness(0, 16, 0, 0);
        SeparatorMargin = new Thickness(8, 0);
        SectionHeaderHeight = 28;
        SectionChevronSize = 12;
        MarkerBarWidth = 3;
        MarkerBarHeight = 16;
        MarkerBarInset = 4;
        MarkerBarRadius = new CornerRadius(1.5);
        BadgeDotSize = 6;
        // Row padding 12 + icon 16 - 4: the dot overlaps the icon's top right corner.
        BadgeDotMargin = new Thickness(24, 6, 0, 0);
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(14);
        DisabledOpacity = 0.45;
    }
}
