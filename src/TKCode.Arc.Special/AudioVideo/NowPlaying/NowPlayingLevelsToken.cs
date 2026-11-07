using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="NowPlayingLevels"/> (Arc Now playing's level bars, design reference): three 3 × 14 bars 2
/// apart with radius 1, resting at 30%, 60% and 42% of their height at 55% opacity.
/// </summary>
[ControlDesignToken]
internal sealed class NowPlayingLevelsToken : AbstractControlDesignToken
{
    /// <summary>A bar's width (3).</summary>
    public double BarWidth { get; set; }

    /// <summary>A bar's full height (14).</summary>
    public double BarHeight { get; set; }

    /// <summary>The space between bars (2).</summary>
    public double BarGap { get; set; }

    /// <summary>A bar's corner radius (1).</summary>
    public double BarRadius { get; set; }

    /// <summary>The opacity of the bars at rest (0.55).</summary>
    public double RestOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarWidth = 3;
        BarHeight = 14;
        BarGap = 2;
        BarRadius = 1;
        RestOpacity = 0.55;
    }
}
