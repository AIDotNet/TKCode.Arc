using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CustomerGalaxy"/> (Arc Customer galaxy, design reference, measured at 664 / 1312 px): the
/// frame, header, toolbar (tabs and legend), stage overlays, totals panel and list. Colors, shadows and type come from
/// ArcDesign tokens in the theme; the dot colors are the health states' tones.
/// </summary>
[ControlDesignToken]
internal sealed class CustomerGalaxyToken : AbstractControlDesignToken
{
    /// <summary>Corner radius of the frame (16).</summary>
    public CornerRadius FrameRadius { get; set; }

    /// <summary>Inset of the title and subtitle (20 at the sides, 16 on top).</summary>
    public Thickness HeaderInset { get; set; }

    /// <summary>Space between the title and the subtitle (2).</summary>
    public double HeaderGap { get; set; }

    /// <summary>Letter spacing of the 16 px title (−0.48).</summary>
    public double TitleTracking { get; set; }

    /// <summary>Line height of the title (17.6).</summary>
    public double TitleLeading { get; set; }

    /// <summary>Inset of the toolbar (12 on top, 20 at the sides, 16 below; a 1 px rule under it).</summary>
    public Thickness ToolbarInset { get; set; }

    /// <summary>Gap between the tabs and the legend on one row (20) and between rows (12).</summary>
    public double ToolbarColumnGap { get; set; }

    public double ToolbarRowGap { get; set; }

    /// <summary>The tab track: 2 px inset, 2 px gaps, radius 10 (34 high with its border).</summary>
    public Thickness TabTrackInset { get; set; }

    public double TabGap { get; set; }

    public CornerRadius TabTrackRadius { get; set; }

    /// <summary>A tab: 28 high, 10 px side inset, radius 8.</summary>
    public double TabExtent { get; set; }

    public Thickness TabInset { get; set; }

    public CornerRadius TabRadius { get; set; }

    /// <summary>Legend: 16 px between entries, 6 px between dot and text, 8 px dots, 6 px between wrapped lines.</summary>
    public double LegendGap { get; set; }

    public double LegendItemGap { get; set; }

    public double LegendDot { get; set; }

    public double LegendLineGap { get; set; }

    /// <summary>The size entry's small and large dots (4 and 10, 3 apart).</summary>
    public double SizeDotSmall { get; set; }

    public double SizeDotLarge { get; set; }

    public double SizeDotGap { get; set; }

    /// <summary>Width of the side panel with its divider (288).</summary>
    public double SidePanelWidth { get; set; }

    /// <summary>Shortest stage beside the panel (360).</summary>
    public double StageMinHeight { get; set; }

    /// <summary>Inset of the totals panel (16 on top, 20 at the sides and below).</summary>
    public Thickness TotalsInset { get; set; }

    /// <summary>Totals panel: column gap when split (32), gap between stacked blocks (20), width from which it splits (600).</summary>
    public double TotalsColumnGap { get; set; }

    public double TotalsRowGap { get; set; }

    public double TotalsSplitWidth { get; set; }

    /// <summary>The caption sits close under the scope name (−14 + the 20 px block gap).</summary>
    public Thickness MrrCaptionMargin { get; set; }

    /// <summary>The subtotals: 12 px column gap, 16 px under them before their rule, 2 px between label and value.</summary>
    public double SubtotalGap { get; set; }

    public Thickness SubtotalInset { get; set; }

    public double SubtotalLabelGap { get; set; }

    /// <summary>Color dots before the growth and risk subtotals (8, 6 before the label).</summary>
    public double SubtotalDot { get; set; }

    /// <summary>Letter spacing of the list title (−0.42).</summary>
    public double ListTitleTracking { get; set; }

    /// <summary>Space between the list title row and the rows (10).</summary>
    public double ListGap { get; set; }

    /// <summary>The rows bleed 8 px left and right so their hover fill reaches past the text.</summary>
    public Thickness ListBleed { get; set; }

    /// <summary>A row: 8 px inset, radius 8, 6 px between its lines.</summary>
    public Thickness RowInset { get; set; }

    public CornerRadius RowRadius { get; set; }

    public double RowLineGap { get; set; }

    /// <summary>A row's bar: 4 high, segments 2 apart.</summary>
    public double BarExtent { get; set; }

    public double BarSegmentGap { get; set; }

    /// <summary>The MRR / Accounts switch: 2 px inset and gap, radius 8 (30 high); buttons 24 high, 8 px inset, radius 6.</summary>
    public Thickness SortTrackInset { get; set; }

    public CornerRadius SortTrackRadius { get; set; }

    public double SortButtonExtent { get; set; }

    public Thickness SortButtonInset { get; set; }

    public CornerRadius SortButtonRadius { get; set; }

    /// <summary>The gesture hint: 16 from the left, 20 from the bottom, clear of the zoom bar.</summary>
    public Thickness HintMargin { get; set; }

    /// <summary>"Show all": 12 from the corner, 32 high, inset 10 / 12, radius 10, 6 px after its 16 px arrow.</summary>
    public Thickness ChipMargin { get; set; }

    public double ChipExtent { get; set; }

    public Thickness ChipInset { get; set; }

    public CornerRadius ChipRadius { get; set; }

    public double ChipGlyphGap { get; set; }

    public double ChipGlyphSize { get; set; }

    /// <summary>The zoom bar sits 12 from the stage's bottom right corner.</summary>
    public Thickness ZoomMargin { get; set; }

    /// <summary>The card: at least 236 wide, a 1 px rule 10 below the subtitle and 10 above the rows, 4 between rows, 8 px dot.</summary>
    public double CardMinWidth { get; set; }

    public Thickness CardRuleMargin { get; set; }

    public double CardRowGap { get; set; }

    public double CardDot { get; set; }

    public Thickness CardNoteMargin { get; set; }

    /// <summary>A cluster label's two lines are 1 px apart; dimmed labels fade to 30%.</summary>
    public double LabelLineGap { get; set; }

    public double DimmedLabelOpacity { get; set; }

    /// <summary>Keyboard focus rings (2 px, 2 px outside) of the tabs, chip, switch and rows.</summary>
    public Thickness FocusRingMargin { get; set; }

    public CornerRadius TabFocusRadius { get; set; }

    public CornerRadius ChipFocusRadius { get; set; }

    public CornerRadius RowFocusRadius { get; set; }

    public CornerRadius SortFocusRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FrameRadius = new CornerRadius(16);
        HeaderInset = new Thickness(20, 16, 20, 0);
        HeaderGap = 2;
        TitleTracking = -0.48;
        TitleLeading = 17.6;
        ToolbarInset = new Thickness(20, 12, 20, 16);
        ToolbarColumnGap = 20;
        ToolbarRowGap = 12;
        TabTrackInset = new Thickness(2);
        TabGap = 2;
        TabTrackRadius = new CornerRadius(10);
        TabExtent = 28;
        TabInset = new Thickness(10, 0);
        TabRadius = new CornerRadius(8);
        LegendGap = 16;
        LegendItemGap = 6;
        LegendDot = 8;
        LegendLineGap = 6;
        SizeDotSmall = 4;
        SizeDotLarge = 10;
        SizeDotGap = 3;
        SidePanelWidth = 288;
        StageMinHeight = 360;
        TotalsInset = new Thickness(20, 16, 20, 20);
        TotalsColumnGap = 32;
        TotalsRowGap = 20;
        TotalsSplitWidth = 600;
        MrrCaptionMargin = new Thickness(0, -14, 0, 0);
        SubtotalGap = 12;
        SubtotalInset = new Thickness(0, 0, 0, 16);
        SubtotalLabelGap = 2;
        SubtotalDot = 8;
        ListTitleTracking = -0.42;
        ListGap = 10;
        ListBleed = new Thickness(-8, 0);
        RowInset = new Thickness(8);
        RowRadius = new CornerRadius(8);
        RowLineGap = 6;
        BarExtent = 4;
        BarSegmentGap = 2;
        SortTrackInset = new Thickness(2);
        SortTrackRadius = new CornerRadius(8);
        SortButtonExtent = 24;
        SortButtonInset = new Thickness(8, 0);
        SortButtonRadius = new CornerRadius(6);
        HintMargin = new Thickness(16, 0, 130, 20);
        ChipMargin = new Thickness(12);
        ChipExtent = 32;
        ChipInset = new Thickness(10, 0, 12, 0);
        ChipRadius = new CornerRadius(10);
        ChipGlyphGap = 6;
        ChipGlyphSize = 16;
        ZoomMargin = new Thickness(12);
        CardMinWidth = 236;
        CardRuleMargin = new Thickness(0, 10);
        CardRowGap = 4;
        CardDot = 8;
        CardNoteMargin = new Thickness(0, 10, 0, 0);
        LabelLineGap = 1;
        DimmedLabelOpacity = 0.3;
        FocusRingMargin = new Thickness(-4);
        TabFocusRadius = new CornerRadius(12);
        ChipFocusRadius = new CornerRadius(14);
        RowFocusRadius = new CornerRadius(12);
        SortFocusRadius = new CornerRadius(10);
    }
}
