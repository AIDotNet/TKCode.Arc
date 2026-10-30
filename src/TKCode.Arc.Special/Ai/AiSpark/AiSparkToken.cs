using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AiSpark"/> (Arc AI spark, design reference): the pill is a 28px capsule with the 16px mark 7px
/// from its left edge, 6px before the label and 11px after it; the avatar's mark is 56% of its tile; the diagonals and the
/// core take a lighter tone of the mark's color (mixed 42% toward the surface), and the error tone is the danger color
/// mixed 35% toward the muted text color. The colors themselves come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AiSparkToken : AbstractControlDesignToken
{
    /// <summary>The pill's height (28) and its capsule corner radius (14).</summary>
    public double PillHeight { get; set; }

    public CornerRadius PillCornerRadius { get; set; }

    /// <summary>The pill's padding: 7 before the mark, 11 after the label.</summary>
    public Thickness PillPadding { get; set; }

    /// <summary>The gap between the mark and the label in the pill (6).</summary>
    public double PillGap { get; set; }

    /// <summary>The mark's share of the avatar tile (0.56).</summary>
    public double AvatarMarkRatio { get; set; }

    /// <summary>The avatar tile's tint: the mark's color at this opacity (0.13).</summary>
    public double AvatarTint { get; set; }

    /// <summary>How far the lighter tone of the diagonals and core is mixed toward the surface (0.42).</summary>
    public double SoftToneMix { get; set; }

    /// <summary>How far the error tone is mixed from the danger color toward the muted text color (0.35).</summary>
    public double ErrorMuteMix { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PillHeight = 28;
        PillCornerRadius = new CornerRadius(14);
        PillPadding = new Thickness(7, 0, 11, 0);
        PillGap = 6;
        AvatarMarkRatio = 0.56;
        AvatarTint = isDarkMode ? 0.18 : 0.13;
        SoftToneMix = isDarkMode ? 0.38 : 0.42;
        ErrorMuteMix = 0.35;
    }
}
