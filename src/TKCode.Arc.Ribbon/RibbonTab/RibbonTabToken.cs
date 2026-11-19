using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of a <see cref="RibbonTab"/>'s command area: groups sit edge to edge (each draws its own end line), 4px in from the sides.</summary>
[ControlDesignToken]
internal sealed class RibbonTabToken : AbstractControlDesignToken
{
    /// <summary>Space between groups: 0 (the line at the end of each group separates them).</summary>
    public double GroupSpacing { get; set; }

    /// <summary>Padding around the groups: 4 at the sides.</summary>
    public Thickness Padding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GroupSpacing = 0;
        Padding = new Thickness(4, 0);
    }
}
