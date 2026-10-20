using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Shared blur effects for animated blurs drawn in <c>Render</c> or set as <c>Effect</c> every frame (blur swaps, text
/// reveals, focus pulls). Radii are quantized to 0.25px and capped at <see cref="MaxRadius"/>, and every effect is
/// created once and reused, so a running blur allocates nothing per frame.
/// </summary>
public static class ArcBlur
{
    /// <summary>Quantization step in px.</summary>
    public const double Step = 0.25;

    /// <summary>The largest radius handed out (Arc's largest motion blur is BlurText, 8px).</summary>
    public const double MaxRadius = 10;

    private static readonly ImmutableBlurEffect?[] s_effects = new ImmutableBlurEffect?[(int)(MaxRadius / Step) + 1];

    /// <summary>A cached blur of about <paramref name="radius"/> px, or null when it rounds to zero (nothing to blur).</summary>
    public static IEffect? Get(double radius)
    {
        if (!(radius > 0))
        {
            return null;
        }
        var index = (int)Math.Round(Math.Min(radius, MaxRadius) / Step);
        if (index <= 0)
        {
            return null;
        }
        return s_effects[index] ??= new ImmutableBlurEffect(index * Step);
    }
}
