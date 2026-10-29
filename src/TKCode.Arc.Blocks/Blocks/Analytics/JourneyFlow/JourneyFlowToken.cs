using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and type sizes of <see cref="JourneyFlow"/> (Arc Journey flow, design reference). Colours, shadows and fonts
/// come from ArcDesign tokens in the theme; the chart's own geometry (8px bars, 44px node gap, 404 / 372 / 340 heights)
/// is fixed in <c>JourneyFlowEngine</c>.
/// </summary>
[ControlDesignToken]
internal sealed class JourneyFlowToken : AbstractControlDesignToken
{
    /// <summary>Header padding: 16 above and below, 20 at the sides (16 all round when compact).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Header padding in compact blocks (559 wide or less).</summary>
    public Thickness CompactHeaderPadding { get; set; }

    /// <summary>Chart area padding: 20 / 20 / 16 (16 / 16 / 12 when compact).</summary>
    public Thickness ChartPadding { get; set; }

    /// <summary>Chart area padding in compact blocks.</summary>
    public Thickness CompactChartPadding { get; set; }

    /// <summary>Side panel padding: 16 / 20 / 20 (16 when compact).</summary>
    public Thickness PanelPadding { get; set; }

    /// <summary>Side panel padding in compact blocks.</summary>
    public Thickness CompactPanelPadding { get; set; }

    /// <summary>Footer padding: 12 / 20 (12 / 16 when compact).</summary>
    public Thickness FooterPadding { get; set; }

    /// <summary>Footer padding in compact blocks.</summary>
    public Thickness CompactFooterPadding { get; set; }

    /// <summary>Width of the side panel beside the chart (blocks 860 wide or more).</summary>
    public double PanelWidth { get; set; }

    /// <summary>Title: Geist 15/16.5, tracking −0.45.</summary>
    public double TitleSize { get; set; }

    /// <summary>Title line height.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Title tracking.</summary>
    public double TitleTracking { get; set; }

    /// <summary>Body text: 13/18.2.</summary>
    public double BodySize { get; set; }

    /// <summary>Body line height.</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Panel title: Geist 14/15.4, tracking −0.42.</summary>
    public double PanelTitleSize { get; set; }

    /// <summary>Panel title tracking.</summary>
    public double PanelTitleTracking { get; set; }

    /// <summary>Panel big number: 28/30.8 Medium.</summary>
    public double BigNumberSize { get; set; }

    /// <summary>Small text (group headers, table captions and headers): 12/16.8.</summary>
    public double CaptionSize { get; set; }

    /// <summary>Small text line height.</summary>
    public double CaptionLineHeight { get; set; }

    /// <summary>Gap between the panel's groups (20) and the two columns of groups when side by side (28).</summary>
    public double GroupGap { get; set; }

    /// <summary>Column gap of side-by-side groups.</summary>
    public double GroupColumnGap { get; set; }

    /// <summary>"Flow table" button: 32 high, padding 0 8 0 12, radius 10.</summary>
    public double TableButtonHeight { get; set; }

    /// <summary>"Flow table" button padding.</summary>
    public Thickness TableButtonPadding { get; set; }

    /// <summary>"Flow table" button radius.</summary>
    public CornerRadius TableButtonRadius { get; set; }

    /// <summary>"Clear" button: 28 high, padding 0 10 0 8, radius 8.</summary>
    public double ClearButtonHeight { get; set; }

    /// <summary>"Clear" button padding.</summary>
    public Thickness ClearButtonPadding { get; set; }

    /// <summary>"Clear" button radius.</summary>
    public CornerRadius ClearButtonRadius { get; set; }

    /// <summary>Flow table margin: 0 20 20 (0 16 16 when compact).</summary>
    public Thickness TableMargin { get; set; }

    /// <summary>Flow table margin in compact blocks.</summary>
    public Thickness CompactTableMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderPadding = new Thickness(20, 16);
        CompactHeaderPadding = new Thickness(16);
        ChartPadding = new Thickness(20, 20, 20, 16);
        CompactChartPadding = new Thickness(16, 16, 16, 12);
        PanelPadding = new Thickness(20, 16, 20, 20);
        CompactPanelPadding = new Thickness(16);
        FooterPadding = new Thickness(20, 12);
        CompactFooterPadding = new Thickness(16, 12);
        PanelWidth = 280;
        TitleSize = 15;
        TitleLineHeight = 16.5;
        TitleTracking = -0.45;
        BodySize = 13;
        BodyLineHeight = 18.2;
        PanelTitleSize = 14;
        PanelTitleTracking = -0.42;
        BigNumberSize = 28;
        CaptionSize = 12;
        CaptionLineHeight = 16.8;
        GroupGap = 20;
        GroupColumnGap = 28;
        TableButtonHeight = 32;
        TableButtonPadding = new Thickness(12, 0, 8, 0);
        TableButtonRadius = new CornerRadius(10);
        ClearButtonHeight = 28;
        ClearButtonPadding = new Thickness(8, 0, 10, 0);
        ClearButtonRadius = new CornerRadius(8);
        TableMargin = new Thickness(20, 0, 20, 20);
        CompactTableMargin = new Thickness(16, 0, 16, 16);
    }
}
