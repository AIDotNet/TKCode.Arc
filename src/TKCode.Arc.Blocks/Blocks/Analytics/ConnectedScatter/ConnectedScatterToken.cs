using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ConnectedScatter"/>, measured from the Analytics group's connected scatter block (design
/// reference only, ADR 0024). Colours, radii, shadows and fonts come from ArcDesign tokens in the theme; the mark sizes
/// that never change with the theme (dot radii, stroke widths) are constants of the plot.
/// </summary>
[ControlDesignToken]
internal sealed class ConnectedScatterToken : AbstractControlDesignToken
{
    /// <summary>Block padding (24) and its narrow form (16, under <see cref="NarrowWidth"/>).</summary>
    public Thickness BlockPadding { get; set; }

    public Thickness NarrowBlockPadding { get; set; }

    /// <summary>Space between the header, readouts, chart and transport (24; narrow 20).</summary>
    public double SectionGap { get; set; }

    public double NarrowSectionGap { get; set; }

    /// <summary>Below this block width the paddings and gaps tighten (540 ≈ Arc's 600 viewport).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Header gaps: 24 between the titles and the switch, 16 between wrapped lines.</summary>
    public double HeaderColumnGap { get; set; }

    public double HeaderRowGap { get; set; }

    /// <summary>Title 22 on a 24.2 line, letter spacing −0.22; 4 between it and the description (14 on 19.6).</summary>
    public double TitleSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double DescriptionGap { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>Readout cell padding (14 / 16; 12 under 380), the value size (22; 18 under 380) and the gap between lines.</summary>
    public Thickness ReadoutPadding { get; set; }

    public Thickness TinyReadoutPadding { get; set; }

    public double ReadoutValueSize { get; set; }

    public double TinyReadoutValueSize { get; set; }

    /// <summary>Chart header: 8 above the plot; the x axis title sits 4 up into the plot's bottom padding; legend items are 16 apart.</summary>
    public Thickness ChartHeaderMargin { get; set; }

    public Thickness XTitleMargin { get; set; }

    public double LegendGap { get; set; }

    /// <summary>Plot padding: left 44, top 14, right 16, bottom 30.</summary>
    public Thickness PlotInset { get; set; }

    /// <summary>Plot height from 720 wide: 380; from 480: 330; under that 300.</summary>
    public double PlotHeightWide { get; set; }

    public double PlotHeightMedium { get; set; }

    public double PlotHeightCompact { get; set; }

    /// <summary>The hover card: 176 wide, 14 from the point.</summary>
    public double CardWidth { get; set; }

    public double CardGap { get; set; }

    /// <summary>Transport: 20 above, the button 104 wide, 20 between button and scrubber, scrubber band 44 high.</summary>
    public Thickness TransportMargin { get; set; }

    public double ButtonColumnWidth { get; set; }

    public double TransportGap { get; set; }

    public double ScrubberHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockPadding = new Thickness(24);
        NarrowBlockPadding = new Thickness(16);
        SectionGap = 24;
        NarrowSectionGap = 20;
        NarrowWidth = 540;
        HeaderColumnGap = 24;
        HeaderRowGap = 16;
        TitleSize = 22;
        TitleLineHeight = 24.2;
        TitleLetterSpacing = -0.22;
        DescriptionGap = 4;
        BodyLineHeight = 19.6;
        ReadoutPadding = new Thickness(16, 14);
        TinyReadoutPadding = new Thickness(12);
        ReadoutValueSize = 22;
        TinyReadoutValueSize = 18;
        ChartHeaderMargin = new Thickness(0, 0, 0, 8);
        XTitleMargin = new Thickness(0, -4, 0, 0);
        LegendGap = 16;
        PlotInset = new Thickness(44, 14, 16, 30);
        PlotHeightWide = 380;
        PlotHeightMedium = 330;
        PlotHeightCompact = 300;
        CardWidth = 176;
        CardGap = 14;
        TransportMargin = new Thickness(0, 20, 0, 0);
        ButtonColumnWidth = 104;
        TransportGap = 20;
        ScrubberHeight = 44;
    }
}
