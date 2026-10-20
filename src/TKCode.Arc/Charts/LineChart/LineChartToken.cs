using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>LineChart geometry (Arc: 48px value gutter, 26px date axis, 2px lines, legend 16px above the plot).</summary>
[ControlDesignToken]
internal sealed class LineChartToken : AbstractControlDesignToken
{
    /// <summary>Width of the right-hand value axis.</summary>
    public double GutterWidth { get; set; }

    /// <summary>Height of the date axis row under the plot.</summary>
    public double AxisRowHeight { get; set; }

    /// <summary>Stroke width of solid lines (dashed lines are 0.25px thinner).</summary>
    public double StrokeWidth { get; set; }

    /// <summary>Space between the legend and the plot.</summary>
    public double LegendGap { get; set; }

    /// <summary>The legend reaches 8px into the margins so pill hovers align with the plot edge.</summary>
    public Thickness LegendMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GutterWidth = 48;
        AxisRowHeight = 26;
        StrokeWidth = 2;
        LegendGap = 16;
        LegendMargin = new Thickness(-8, 0);
    }
}
