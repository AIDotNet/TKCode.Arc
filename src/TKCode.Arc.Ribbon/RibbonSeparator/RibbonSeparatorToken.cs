using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of a <see cref="RibbonSeparator"/>: a 1px line with 4px on each side, inset 6px from the top and bottom.</summary>
[ControlDesignToken]
internal sealed class RibbonSeparatorToken : AbstractControlDesignToken
{
    /// <summary>Width of the line: 1.</summary>
    public double LineWidth { get; set; }

    /// <summary>Space around the line: 4 at the sides, 6 at the top and bottom.</summary>
    public Thickness LineMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LineWidth = 1;
        LineMargin = new Thickness(4, 6);
    }
}
