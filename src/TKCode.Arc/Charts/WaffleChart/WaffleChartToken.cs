using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class WaffleChartToken : AbstractControlDesignToken
{
    /// <summary>Gap between cells.</summary>
    public double CellGap { get; set; }

    /// <summary>Largest grid width (the grid stays square for 10 × 10).</summary>
    public double GridMaxSize { get; set; }

    /// <summary>Chart width from which the legend sits beside the grid.</summary>
    public double SideLegendBreakpoint { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CellGap = 3;
        GridMaxSize = 300;
        SideLegendBreakpoint = 420;
    }
}
