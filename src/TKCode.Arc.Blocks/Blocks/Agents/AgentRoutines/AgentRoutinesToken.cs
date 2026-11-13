using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="AgentRoutines"/>. Colors, radii and fonts come from ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class AgentRoutinesToken : AbstractControlDesignToken
{
    /// <summary>The widest the list grows (760).</summary>
    public double MaxWidth { get; set; }

    /// <summary>Padding of the head and of each routine (20 on the sides).</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>At or below this width the strip and Run now move under the schedule (560).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The height of the history strip (16).</summary>
    public double HistoryHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxWidth = 760;
        RowPadding = new Thickness(20, 14);
        CompactWidth = 560;
        HistoryHeight = 16;
    }
}
