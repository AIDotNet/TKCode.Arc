using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Streamgraph geometry (Arc: 26px time axis, legend 16px above the plot, 2px surface gap between layers).</summary>
[ControlDesignToken]
internal sealed class StreamgraphToken : AbstractControlDesignToken
{
    /// <summary>Height of the time axis row under the stream.</summary>
    public double AxisRowHeight { get; set; }

    /// <summary>Space between the legend and the plot.</summary>
    public double LegendGap { get; set; }

    /// <summary>The legend reaches 8px into the margins so pill hovers align with the plot edge.</summary>
    public Thickness LegendMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        AxisRowHeight = 26;
        LegendGap = 16;
        LegendMargin = new Thickness(-8, 0);
    }
}
