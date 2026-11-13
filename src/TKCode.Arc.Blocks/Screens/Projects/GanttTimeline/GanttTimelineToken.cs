using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="GanttTimeline"/> (Arc Gantt timeline, design reference, measured at 1280): the toolbar, the
/// zoom switch, the chart frame and the footer. Row, bar and day sizes are in <c>GanttModel</c> and the chart's
/// properties; colors and type come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class GanttTimelineToken : AbstractControlDesignToken
{
    /// <summary>Padding of the whole block (20 on the sides; smaller below 480).</summary>
    public Thickness RootPadding { get; set; }

    public Thickness NarrowRootPadding { get; set; }

    /// <summary>The toolbar sits 12 above the chart, the footer 12 below it.</summary>
    public double SectionGap { get; set; }

    public double ToolbarGap { get; set; }

    /// <summary>
    /// The zoom switch track (padding 4) and its buttons (height 32). Radii come from ArcDesign's radius steps (frame 20,
    /// track 16, buttons 12, pills 18), so the Corners presets reach them.
    /// </summary>
    public Thickness SwitchPadding { get; set; }

    public double SwitchButtonHeight { get; set; }

    public Thickness SwitchButtonPadding { get; set; }

    /// <summary>Pill buttons (Today, Undo, Reset): height 36, 14 of padding, 6 between glyph and label.</summary>
    public double PillHeight { get; set; }

    public Thickness PillPadding { get; set; }

    public double PillGap { get; set; }

    /// <summary>Opacity of a disabled pill.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Header, bar and row sizes handed to the chart.</summary>
    public double HeaderHeight { get; set; }

    public double BarHeight { get; set; }

    public double BarRadius { get; set; }

    public double TaskColumnWidth { get; set; }

    public double NarrowTaskColumnWidth { get; set; }

    /// <summary>Height of the chart when the parent gives none.</summary>
    public double DefaultChartHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RootPadding = new Thickness(20, 16, 20, 12);
        NarrowRootPadding = new Thickness(12, 12, 12, 8);
        SectionGap = 12;
        ToolbarGap = 12;
        SwitchPadding = new Thickness(4);
        SwitchButtonHeight = 32;
        SwitchButtonPadding = new Thickness(14, 0);
        PillHeight = 36;
        PillPadding = new Thickness(14, 0);
        PillGap = 6;
        DisabledOpacity = 0.4;
        HeaderHeight = 56;
        BarHeight = 24;
        BarRadius = 8;
        TaskColumnWidth = 248;
        NarrowTaskColumnWidth = 132;
        DefaultChartHeight = 480;
    }
}
