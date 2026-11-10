using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="PrizeWheel"/>. The slice, rim, hub, pointer and highlight colors, the font and the focus ring
/// come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class PrizeWheelToken : AbstractControlDesignToken
{
    /// <summary>Width of the rim that carries the pegs.</summary>
    public double RimWidth { get; set; }

    /// <summary>Length of the pointer's flapper, from its pivot to its tip.</summary>
    public double PointerLength { get; set; }

    /// <summary>The keyboard focus ring sits 6px outside the wheel's box.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RimWidth = 12;
        PointerLength = 32;
        FocusRingMargin = new Thickness(-6);
    }
}
