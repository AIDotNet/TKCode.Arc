using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Drawing helpers shared by the pickers: blur effects cached in 0.25px steps (animated blurs never allocate per
/// frame), cached solid brushes for colors that change while animating, and color mixing.
/// </summary>
internal static class PickerDrawing
{
    private const int BlurSteps = 4;
    private static readonly IEffect?[] s_blurs = new IEffect?[8 * BlurSteps + 1];

    /// <summary>A cached blur for <paramref name="radius"/> px, or null when it rounds to zero.</summary>
    public static IEffect? Blur(double radius)
    {
        var index = (int)Math.Round(Math.Clamp(radius, 0, 8) * BlurSteps);
        if (index <= 0)
        {
            return null;
        }
        return s_blurs[index] ??= new ImmutableBlurEffect(index / (double)BlurSteps);
    }

    /// <summary>Linear RGB mix of two colors (alpha included).</summary>
    public static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t),
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    public static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;

    /// <summary>Returns <paramref name="cache"/> when it already paints <paramref name="color"/>, else a new immutable brush.</summary>
    public static ImmutableSolidColorBrush Brush(ref ImmutableSolidColorBrush? cache, Color color)
    {
        if (cache is null || cache.Color != color)
        {
            cache = new ImmutableSolidColorBrush(color);
        }
        return cache;
    }

    /// <summary>Piecewise-linear keyframes at even spacing (Motion's keyframe arrays).</summary>
    public static double Keyframes(ReadOnlySpan<double> frames, double progress)
    {
        if (frames.Length == 0)
        {
            return 0;
        }
        if (progress <= 0 || frames.Length == 1)
        {
            return frames[0];
        }
        if (progress >= 1)
        {
            return frames[^1];
        }
        var scaled = progress * (frames.Length - 1);
        var index = (int)scaled;
        var fraction = scaled - index;
        return frames[index] + (frames[index + 1] - frames[index]) * fraction;
    }
}
