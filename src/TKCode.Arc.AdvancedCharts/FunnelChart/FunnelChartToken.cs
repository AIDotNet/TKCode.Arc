
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FunnelChartToken : AbstractControlDesignToken
{
    /// <summary>Space between the bands of the split view.</summary>
    public double SplitGap { get; set; }

    /// <summary>Height of one step when the funnel runs top to bottom.</summary>
    public double VerticalRowHeight { get; set; }

    /// <summary>Space between the stage labels and the band.</summary>
    public double HeaderGap { get; set; }

    /// <summary>Radius of the flowing dots.</summary>
    public double DotRadius { get; set; }

    /// <summary>Corner radius of the column highlight.</summary>
    public double HighlightRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SplitGap = 6;
        VerticalRowHeight = 100;
        HeaderGap = 10;
        DotRadius = 2.5;
        HighlightRadius = 12;
    }
}
