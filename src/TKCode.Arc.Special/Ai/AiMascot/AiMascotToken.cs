using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry, timing and the accent-independent derived colors of <see cref="AiMascot"/> (Arc AI mascot, design
/// reference). The body, eye and dot colors follow the accent and come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AiMascotToken : AbstractControlDesignToken
{
    /// <summary>Below this size (px) the compact drawing takes over: tighter framing, bolder eyes and dot, no ground.</summary>
    public double CompactBelow { get; set; }

    /// <summary>How long a status change blends the old pose into the new one (and the first frames ease out of the still pose).</summary>
    public TimeSpan BlendDuration { get; set; }

    /// <summary>The quiet form shade on the body's lower right: black at 14% (24% in dark).</summary>
    public Color ShadeColor { get; set; }

    /// <summary>The soft contact shadow under the body: the foreground at 10% (16% in dark).</summary>
    public Color GroundColor { get; set; }

    /// <summary>The line the dot writes while writing: the strong border.</summary>
    public Color LineColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CompactBelow = 56;
        BlendDuration = TimeSpan.FromMilliseconds(420);
        ShadeColor = Oklch.WithAlpha(n.Scrim, isDarkMode ? 0.24 : 0.14);
        GroundColor = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.16 : 0.10);
        LineColor = n.BorderStrong;
    }
}
