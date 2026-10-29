
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TreemapToken : AbstractControlDesignToken
{
    /// <summary>Corner radius of the tile stage (tiles are clipped to it).</summary>
    public double StageRadius { get; set; }

    /// <summary>Space between breadcrumb bar, tiles and color scale.</summary>
    public double FigureGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        StageRadius = 12;
        FigureGap = 12;
    }
}
