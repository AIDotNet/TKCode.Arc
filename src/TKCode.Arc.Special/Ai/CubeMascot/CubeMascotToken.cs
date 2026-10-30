using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and shading of <see cref="CubeMascot"/>: below 56px the compact drawing takes over; the top face is the
/// body mixed toward white and the shaded side toward black by accent-independent amounts; the contact shadow and the
/// printed lines are drawn at fixed opacities. The colors themselves come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class CubeMascotToken : AbstractControlDesignToken
{
    /// <summary>Below this size (px) the compact drawing is used: a larger cube, bolder eyes, no mouth, shadow or lines (56).</summary>
    public double CompactBelow { get; set; }

    /// <summary>How far the top face is mixed from the body toward white (0.2 light, 0.14 dark).</summary>
    public double TopLift { get; set; }

    /// <summary>How far the front face is mixed from the body toward black (0 light; 0.1 dark, where a light body leaves no room to lift the top).</summary>
    public double FrontShade { get; set; }

    /// <summary>How far the shaded side is mixed from the body toward black (0.26 light, 0.4 dark).</summary>
    public double SideShade { get; set; }

    /// <summary>The contact shadow's color: the text color in light mode, a near black below the background in dark mode.</summary>
    public Color ShadowColor { get; set; }

    /// <summary>The contact shadow's opacity at rest (0.12 light, 0.55 dark).</summary>
    public double ShadowOpacity { get; set; }

    /// <summary>The printed lines' opacity at their fullest (0.7).</summary>
    public double LineOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CompactBelow = 56;
        TopLift = isDarkMode ? 0.14 : 0.2;
        FrontShade = isDarkMode ? 0.1 : 0;
        SideShade = isDarkMode ? 0.4 : 0.26;
        var n = ArcPalette.Neutrals(isDarkMode);
        ShadowColor = isDarkMode ? Oklch.MixOklab(n.Background, Colors.Black, 0.7) : n.Foreground;
        ShadowOpacity = isDarkMode ? 0.55 : 0.12;
        LineOpacity = 0.7;
    }
}
