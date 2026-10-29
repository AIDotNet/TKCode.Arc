using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ChoroplethExplorer"/>, measured from the Analytics group's choropleth explorer (design reference
/// only, ADR 0024; notes sections 3 and 8). Colours that follow the accent (the map ramp, bars) are derived in code from
/// ArcDesign tokens; the type ramp's colours and the fonts come from ArcDesign tokens in the theme. The compact values apply
/// below a block width of 560.
/// </summary>
[ControlDesignToken]
internal sealed class ChoroplethExplorerToken : AbstractControlDesignToken
{
    /// <summary>The card look's corner radius (16).</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>Block padding (20, 16, 20, 20) and its compact form (16).</summary>
    public Thickness BlockPadding { get; set; }

    public Thickness CompactBlockPadding { get; set; }

    /// <summary>Between the header and the body (20; compact 16).</summary>
    public double SectionGap { get; set; }

    public double CompactSectionGap { get; set; }

    /// <summary>The title: 16 on a 17.6 line, letter spacing −0.48; the description 12 on 16.8, 2 under the title.</summary>
    public double TitleSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double TitleTracking { get; set; }

    public double DescriptionLineHeight { get; set; }

    /// <summary>Between the header's title and its controls when they wrap (12), and side by side (24); 8 between the controls.</summary>
    public double HeaderRowGap { get; set; }

    public double HeaderColumnGap { get; set; }

    public double ControlsGap { get; set; }

    /// <summary>In two columns (from a block width of 820) the list column is 300 wide, 32 from the map.</summary>
    public double SideWidth { get; set; }

    public double ColumnGap { get; set; }

    /// <summary>Between the map column and the list when they stack (24).</summary>
    public double StackGap { get; set; }

    /// <summary>Between the summary, the map and the legend (12).</summary>
    public double MainGap { get; set; }

    /// <summary>The summary's height (64; compact 60) and the headline number's size (28; compact 22).</summary>
    public double SummaryHeight { get; set; }

    public double CompactSummaryHeight { get; set; }

    public double HeadlineSize { get; set; }

    public double CompactHeadlineSize { get; set; }

    /// <summary>The legend's ramp width (420; compact: the column's width) and its step's height (28) and tick row (16).</summary>
    public double RampWidth { get; set; }

    /// <summary>The stacked list's height (400; compact 360).</summary>
    public double ListHeight { get; set; }

    public double CompactListHeight { get; set; }

    /// <summary>A list row: 44 high, radius 10, 2 between rows.</summary>
    public double RowHeight { get; set; }

    public double RowGap { get; set; }

    public CornerRadius RowRadius { get; set; }

    /// <summary>The back button: 36 high, padding (10, 0, 14, 0), 6 between its arrow and its text.</summary>
    public double BackHeight { get; set; }

    public Thickness BackPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardRadius = new CornerRadius(16);
        BlockPadding = new Thickness(20, 16, 20, 20);
        CompactBlockPadding = new Thickness(16);
        SectionGap = 20;
        CompactSectionGap = 16;
        TitleSize = 16;
        TitleLineHeight = 17.6;
        TitleTracking = -0.48;
        DescriptionLineHeight = 16.8;
        HeaderRowGap = 12;
        HeaderColumnGap = 24;
        ControlsGap = 8;
        SideWidth = 300;
        ColumnGap = 32;
        StackGap = 24;
        MainGap = 12;
        SummaryHeight = 64;
        CompactSummaryHeight = 60;
        HeadlineSize = 28;
        CompactHeadlineSize = 22;
        RampWidth = 420;
        ListHeight = 400;
        CompactListHeight = 360;
        RowHeight = 44;
        RowGap = 2;
        RowRadius = new CornerRadius(10);
        BackHeight = 36;
        BackPadding = new Thickness(10, 0, 14, 0);
    }
}
