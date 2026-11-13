using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="BranchGraph"/>: the card's padding and header, the rows and lanes of the stage, the commit dots,
/// the branch pills, the row columns and the detail card. Colors, radii, shadows and type come from the ArcDesign tokens
/// in the theme. (Arc Branch graph, design reference: rows 44, lanes 18 apart from x 22, an 80 px graph gutter.)
/// </summary>
[ControlDesignToken]
internal sealed class BranchGraphToken : AbstractControlDesignToken
{
    /// <summary>No longer used (the card has no caption title line); kept for theme overrides.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>The header's height (46: 4 + the 38 strategy control + 4).</summary>
    public double HeaderRowHeight { get; set; }

    /// <summary>No longer used (the card has no caption); kept for theme overrides.</summary>
    public Thickness CaptionMargin { get; set; }

    /// <summary>No longer used (the card has no caption); kept for theme overrides.</summary>
    public double CaptionMinHeight { get; set; }

    /// <summary>The gap between the header, the stage and the footer (8).</summary>
    public double SectionGap { get; set; }

    /// <summary>No longer used (the card has no playback buttons); kept for theme overrides.</summary>
    public double ButtonGap { get; set; }

    /// <summary>No longer used (the card has no speed control); kept for theme overrides.</summary>
    public double SpeedGap { get; set; }

    /// <summary>A commit's row (44).</summary>
    public double RowHeight { get; set; }

    /// <summary>The space between two lanes (18).</summary>
    public double LaneGap { get; set; }

    /// <summary>The card's padding (8 on every side).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The header's padding (4 4 4 16; 12 on the left on narrow cards is set in code).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>The space between the header's icon, title and number (8).</summary>
    public double HeadingGap { get; set; }

    /// <summary>The footer's padding (0 16 4) and least height (32).</summary>
    public Thickness FooterPadding { get; set; }

    public double FooterMinHeight { get; set; }

    /// <summary>The strategy control: track inset 3, segments 32 high and at least 64 wide with 12 on each side.</summary>
    public Thickness StrategyTrackInset { get; set; }

    public double StrategySegmentHeight { get; set; }

    public double StrategySegmentMinWidth { get; set; }

    public Thickness StrategySegmentPadding { get; set; }

    /// <summary>The x of the first lane's dots (22) and the least width of the graph gutter (80).</summary>
    public double LaneStart { get; set; }

    public double GutterWidth { get; set; }

    /// <summary>The space above the first row (6) and below the last (4), and beside the rows (4).</summary>
    public double RowsTopInset { get; set; }

    public double RowsBottomInset { get; set; }

    public double RowsSideInset { get; set; }

    /// <summary>A commit dot's radius (5), the stage-colored halo around it (8) and a merge ring's radius (4.25).</summary>
    public double DotRadius { get; set; }

    public double HaloRadius { get; set; }

    public double MergeRingRadius { get; set; }

    /// <summary>The width of the lines and of the merge ring (2).</summary>
    public double LineWidth { get; set; }

    /// <summary>The columns at the right of a row: avatar 20, hash 64, date 88, 12 apart (8 on narrow cards), 12 from the edge.</summary>
    public double AvatarSize { get; set; }

    public double HashColumnWidth { get; set; }

    public double DateColumnWidth { get; set; }

    public double ColumnGap { get; set; }

    public double RowEndPadding { get; set; }

    /// <summary>A branch pill: 22 high, 8 inside each side, a 6 px dot 6 before the name, 4 between pills.</summary>
    public double PillHeight { get; set; }

    public double PillPadding { get; set; }

    public double PillDotSize { get; set; }

    public double PillGap { get; set; }

    /// <summary>The detail card: 340 wide, 72 from the stage's left, 4 from its row, padding 12 16, 8 between its lines.</summary>
    public double DetailWidth { get; set; }

    public double DetailLeft { get; set; }

    public double DetailOffset { get; set; }

    public Thickness DetailPadding { get; set; }

    public double DetailGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TitleLineHeight = 21;
        HeaderRowHeight = 46;
        CaptionMargin = new Thickness(0);
        CaptionMinHeight = 0;
        SectionGap = 8;
        ButtonGap = 6;
        SpeedGap = 12;
        RowHeight = 44;
        LaneGap = 18;
        CardPadding = new Thickness(8);
        HeaderPadding = new Thickness(16, 4, 4, 4);
        HeadingGap = 8;
        FooterPadding = new Thickness(16, 0, 16, 4);
        FooterMinHeight = 32;
        StrategyTrackInset = new Thickness(3);
        StrategySegmentHeight = 32;
        StrategySegmentMinWidth = 64;
        StrategySegmentPadding = new Thickness(12, 0);
        LaneStart = 22;
        GutterWidth = 80;
        RowsTopInset = 6;
        RowsBottomInset = 4;
        RowsSideInset = 4;
        DotRadius = 5;
        HaloRadius = 8;
        MergeRingRadius = 4.25;
        LineWidth = 2;
        AvatarSize = 20;
        HashColumnWidth = 64;
        DateColumnWidth = 88;
        ColumnGap = 12;
        RowEndPadding = 12;
        PillHeight = 22;
        PillPadding = 8;
        PillDotSize = 6;
        PillGap = 4;
        DetailWidth = 340;
        DetailLeft = 72;
        DetailOffset = 4;
        DetailPadding = new Thickness(16, 12);
        DetailGap = 8;
    }
}
