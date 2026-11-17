using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Color blending for controls that draw themselves (#148): one place for what used to be a private <c>Lerp</c> / <c>Mix</c>
/// in many controls. <see cref="Lerp"/> mixes the sRGB channels (what CSS transitions do), <see cref="LerpOklab"/> mixes in
/// OKLab (what Arc's <c>color-mix(in oklab, …)</c> tokens do).
/// </summary>
public static class ArcColor
{
    /// <summary>
    /// Mixes <paramref name="from"/> towards <paramref name="to"/> channel by channel (alpha included) in sRGB;
    /// <paramref name="t"/> is clamped to 0..1 and each channel is rounded.
    /// </summary>
    /// <param name="from">The color at 0.</param>
    /// <param name="to">The color at 1.</param>
    /// <param name="t">How far towards <paramref name="to"/>.</param>
    public static Color Lerp(Color from, Color to, double t)
    {
        t = double.IsNaN(t) ? 0 : Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            Channel(from.A, to.A, t),
            Channel(from.R, to.R, t),
            Channel(from.G, to.G, t),
            Channel(from.B, to.B, t));
    }

    /// <summary>Mixes in OKLab (<see cref="Oklch.MixOklab"/>), <paramref name="t"/> clamped to 0..1.</summary>
    /// <param name="from">The color at 0.</param>
    /// <param name="to">The color at 1.</param>
    /// <param name="t">How far towards <paramref name="to"/>.</param>
    public static Color LerpOklab(Color from, Color to, double t)
        => Oklch.MixOklab(from, to, double.IsNaN(t) ? 0 : Math.Clamp(t, 0, 1));

    /// <summary><paramref name="color"/> with its alpha multiplied by <paramref name="opacity"/> (clamped to 0..1).</summary>
    /// <param name="color">The color.</param>
    /// <param name="opacity">The factor for its alpha.</param>
    public static Color WithOpacity(Color color, double opacity)
        => Color.FromArgb((byte)Math.Round(color.A * (double.IsNaN(opacity) ? 0 : Math.Clamp(opacity, 0, 1))), color.R, color.G, color.B);

    private static byte Channel(byte from, byte to, double t) => (byte)Math.Round(from + (to - from) * t);
}
