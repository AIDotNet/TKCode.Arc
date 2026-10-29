
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ActivityHeatmapToken : AbstractControlDesignToken
{
    /// <summary>Gap between day squares.</summary>
    public double CellGap { get; set; }

    /// <summary>Smallest square; below it the grid scrolls sideways from the newest week.</summary>
    public double CellMin { get; set; }

    /// <summary>Largest square.</summary>
    public double CellMax { get; set; }

    /// <summary>Width of the weekday label column.</summary>
    public double WeekdayColumn { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CellGap = 3;
        CellMin = 9;
        CellMax = 15;
        WeekdayColumn = 30;
    }
}
