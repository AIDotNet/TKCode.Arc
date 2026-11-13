namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="WindowRange"/>. The frame, line, text and focus ring take their colors from ArcDesign tokens in
/// the theme (frame: <c>ControlOn</c>, grips: <c>ControlThumbOn</c>, line and area: <c>Series1</c>).
/// </summary>
[ControlDesignToken]
internal sealed class WindowRangeToken : AbstractControlDesignToken
{
    /// <summary>Width of each edge handle; the chart is inset by it on both sides.</summary>
    public double EdgeWidth { get; set; }

    /// <summary>Radius of the outer corners of the edge handles.</summary>
    public double EdgeRadius { get; set; }

    /// <summary>Thickness of the frame's top and bottom lines.</summary>
    public double BandThickness { get; set; }

    /// <summary>Width of the grip pill inside an edge.</summary>
    public double GripWidth { get; set; }

    /// <summary>Height of the grip pill inside an edge.</summary>
    public double GripHeight { get; set; }

    /// <summary>Opacity of a grip at rest (1 while the pointer is over its edge).</summary>
    public double GripOpacity { get; set; }

    /// <summary>Space between the top of the plot and the top of the chart.</summary>
    public double ChartTopInset { get; set; }

    /// <summary>Space between the bottom of the chart and the bottom of the plot.</summary>
    public double ChartBottomInset { get; set; }

    /// <summary>Stroke width of the line.</summary>
    public double LineThickness { get; set; }

    /// <summary>Opacity of the faded chart outside the window.</summary>
    public double MutedOpacity { get; set; }

    /// <summary>Opacity of the area fill inside the window.</summary>
    public double AreaOpacity { get; set; }

    /// <summary>Opacity of the area fill of the faded chart.</summary>
    public double MutedAreaOpacity { get; set; }

    /// <summary>Opacity of the frame-colored tint inside the window while it is held.</summary>
    public double TintOpacity { get; set; }

    /// <summary>Diameter of the live dot on the latest point.</summary>
    public double DotDiameter { get; set; }

    /// <summary>Font size of the start, length and end readouts.</summary>
    public double ReadoutFontSize { get; set; }

    /// <summary>Line height of the readouts.</summary>
    public double ReadoutLineHeight { get; set; }

    /// <summary>Space between the three readouts.</summary>
    public double ReadoutGap { get; set; }

    /// <summary>Line height of the tick labels.</summary>
    public double TickLineHeight { get; set; }

    /// <summary>Space between the readouts, the plot and the tick labels.</summary>
    public double RowGap { get; set; }

    /// <summary>Opacity of the whole control when it is disabled.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        EdgeWidth = 12;
        EdgeRadius = 9;
        BandThickness = 2;
        GripWidth = 2.5;
        GripHeight = 16;
        GripOpacity = 0.85;
        ChartTopInset = 10;
        ChartBottomInset = 2;
        LineThickness = 1.75;
        MutedOpacity = 0.32;
        AreaOpacity = 0.14;
        MutedAreaOpacity = 0.10;
        TintOpacity = 0.06;
        DotDiameter = 8;
        ReadoutFontSize = 13;
        ReadoutLineHeight = 18.2;
        ReadoutGap = 12;
        TickLineHeight = 16;
        RowGap = 8;
        DisabledOpacity = 0.5;
    }
}
