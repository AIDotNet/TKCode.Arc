using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;

namespace TKCode.Arc;

/// <summary>
/// The measured look of each material (Arc Wallet stack, design reference; literal OKLCH values per theme): ink colors,
/// emboss, edge lips, the texture and its blend, the sweep bands and the pointer light's blend and stops.
/// </summary>
internal sealed class WalletStackMaterialStyle
{
    private Color _tint;

    public WalletStackMaterial Kind { get; private init; }

    public bool IsDark { get; private init; }

    public Color Ink { get; private init; }

    public double ProductAlpha { get; private init; }

    public Color NumberInk { get; private init; }

    public Color EmbossLight { get; private init; }

    public Color EmbossDark { get; private init; }

    /// <summary>Edge: inner hairline (A), top lip (B), bottom lip (C).</summary>
    public Color EdgeLine { get; private init; }

    public Color EdgeTop { get; private init; }

    public Color EdgeBottom { get; private init; }

    public bool GoldChip { get; private init; }

    public WalletStackNoise NoiseKind { get; private init; }

    public double NoiseFrequency { get; private init; }

    public double NoiseOpacity { get; private init; }

    public int NoiseSeed { get; private init; }

    public BitmapBlendingMode NoiseBlend { get; private init; }

    /// <summary>How the pointer light mixes with the face: overlay (metal), screen (black), soft light (glass, color).</summary>
    public BitmapBlendingMode LightBlend { get; private init; }

    /// <summary>White alpha of the light at its centre and at 18% (it is clear from 36%).</summary>
    public (double Center, double Mid) LightStops { get; private init; }

    /// <summary>A flat stand-in for the face where offscreen rendering is unavailable.</summary>
    public Color Fallback { get; private init; }

    public static WalletStackMaterialStyle For(WalletStackCard card, bool dark) => card.Material switch
    {
        WalletStackMaterial.Black => new WalletStackMaterialStyle
        {
            Kind = WalletStackMaterial.Black, IsDark = dark,
            Ink = Oklch.ToColor(0.9, 0.006, 260), ProductAlpha = 0.62, NumberInk = Oklch.ToColor(0.86, 0.008, 260),
            EmbossLight = Oklch.ToColor(1, 0, 0, 0.14), EmbossDark = Oklch.ToColor(0, 0, 0, 0.7),
            EdgeLine = Oklch.ToColor(1, 0, 0, 0.08), EdgeTop = Oklch.ToColor(1, 0, 0, 0.2), EdgeBottom = Oklch.ToColor(0, 0, 0, 0.5),
            GoldChip = false,
            NoiseKind = WalletStackNoise.Grain, NoiseFrequency = 0.9, NoiseOpacity = 0.22, NoiseSeed = 29,
            NoiseBlend = BitmapBlendingMode.SourceOver,
            LightBlend = BitmapBlendingMode.Screen, LightStops = (0.16, 0.04),
            Fallback = Oklch.ToColor(0.19, 0.024, 265),
        },
        WalletStackMaterial.Glass => new WalletStackMaterialStyle
        {
            Kind = WalletStackMaterial.Glass, IsDark = dark,
            Ink = dark ? Oklch.ToColor(0.96, 0.01, 230) : Oklch.ToColor(0.27, 0.03, 240), ProductAlpha = 0.7,
            NumberInk = dark ? Oklch.ToColor(0.96, 0.01, 230) : Oklch.ToColor(0.27, 0.03, 240),
            EmbossLight = Oklch.ToColor(1, 0, 0, 0.45), EmbossDark = Oklch.ToColor(0, 0, 0, 0.12),
            EdgeLine = Oklch.ToColor(1, 0, 0, dark ? 0.32 : 0.8), EdgeTop = Oklch.ToColor(1, 0, 0, dark ? 0.32 : 0.8),
            EdgeBottom = Oklch.ToColor(0, 0, 0, 0.08),
            GoldChip = false,
            NoiseKind = WalletStackNoise.Grain, NoiseFrequency = 0.8, NoiseOpacity = 0.14, NoiseSeed = 41,
            NoiseBlend = BitmapBlendingMode.Overlay,
            LightBlend = BitmapBlendingMode.SoftLight, LightStops = (0.42, 0.12),
            Fallback = dark ? Oklch.ToColor(0.6, 0.035, 230, 0.5) : Oklch.ToColor(0.93, 0.03, 220, 0.8),
        },
        WalletStackMaterial.Color => ColorStyle(card.Tint ?? WalletStackSamples.BrickTint, dark),
        _ => new WalletStackMaterialStyle
        {
            Kind = WalletStackMaterial.Metal, IsDark = dark,
            Ink = Oklch.ToColor(0.3, 0.01, 260), ProductAlpha = 0.72, NumberInk = Oklch.ToColor(0.3, 0.01, 260),
            EmbossLight = Oklch.ToColor(1, 0, 0, 0.75), EmbossDark = Oklch.ToColor(0, 0, 0, 0.22),
            EdgeLine = Oklch.ToColor(0, 0, 0, 0.1), EdgeTop = Oklch.ToColor(1, 0, 0, 0.85), EdgeBottom = Oklch.ToColor(0, 0, 0, 0.18),
            GoldChip = true,
            NoiseKind = WalletStackNoise.Brushed, NoiseFrequency = 0.9, NoiseOpacity = 0.5, NoiseSeed = 7,
            NoiseBlend = BitmapBlendingMode.Overlay,
            LightBlend = BitmapBlendingMode.Overlay, LightStops = (0.42, 0.12),
            Fallback = Oklch.ToColor(0.88, 0.004, 260),
        },
    };

    private static WalletStackMaterialStyle ColorStyle(Color tint, bool dark)
    {
        var (l, _, _) = Oklch.FromColor(tint);
        return new WalletStackMaterialStyle
        {
            Kind = WalletStackMaterial.Color, IsDark = dark, _tint = tint,
            Ink = Oklch.ToColor(0.98, 0.01, 260), ProductAlpha = 0.72, NumberInk = Oklch.ToColor(0.98, 0.01, 260),
            EmbossLight = Oklch.ToColor(1, 0, 0, 0.2), EmbossDark = Oklch.ToColor(0, 0, 0, 0.4),
            EdgeLine = Oklch.ToColor(1, 0, 0, 0.12), EdgeTop = Oklch.ToColor(1, 0, 0, 0.35), EdgeBottom = Oklch.ToColor(0, 0, 0, 0.35),
            GoldChip = true,
            NoiseKind = WalletStackNoise.Grain, NoiseFrequency = 0.85, NoiseOpacity = 0.16, NoiseSeed = 53,
            NoiseBlend = BitmapBlendingMode.Overlay,
            LightBlend = BitmapBlendingMode.SoftLight, LightStops = (0.42, 0.12),
            Fallback = Oklch.ToColor(Math.Clamp(l, 0, 1), 0.1, 28),
        };
    }

    /// <summary>The material's base layers, bottom first.</summary>
    public IEnumerable<IBrush> Material(Size size)
    {
        switch (Kind)
        {
            case WalletStackMaterial.Black:
                yield return Radial(1.1, 1.3, 0.08, 0, (0, Oklch.ToColor(0.29, 0.03, 265)), (0.55, Oklch.ToColor(0.17, 0.022, 265)),
                    (1, Oklch.ToColor(0.12, 0.016, 265)));
                break;
            case WalletStackMaterial.Glass:
            {
                var line = WalletStackCardArt.CssLine(135, size);
                yield return Linear(line, IsDark
                    ? new[] { (0.0, Oklch.ToColor(0.72, 0.03, 220, 0.42)), (0.55, Oklch.ToColor(0.52, 0.04, 235, 0.34)), (1.0, Oklch.ToColor(0.64, 0.04, 260, 0.4)) }
                    : new[] { (0.0, Oklch.ToColor(0.97, 0.02, 215, 0.8)), (0.55, Oklch.ToColor(0.91, 0.035, 225, 0.64)), (1.0, Oklch.ToColor(0.94, 0.03, 250, 0.74)) });
                break;
            }
            case WalletStackMaterial.Color:
            {
                var (l, c, h) = Oklch.FromColor(_tint);
                yield return Radial(1.3, 1.2, 0.12, 0, (0, Oklch.ToColor(Math.Min(1, l + 0.12), c, h)), (0.48, Oklch.ToColor(l, c, h)),
                    (1, Oklch.ToColor(Math.Max(0, l - 0.14), c * 0.85, h)));
                break;
            }
            default:
            {
                // Turned metal: seven alternating light and dark spokes around a point right of centre.
                ReadOnlySpan<double> lightness = stackalloc double[] { 0.86, 0.96, 0.79, 0.93, 0.83, 0.97, 0.86 };
                ReadOnlySpan<double> chroma = stackalloc double[] { 0.004, 0.002, 0.006, 0.003, 0.005, 0.002, 0.004 };
                var stops = new List<ImmutableGradientStop>(7);
                for (var i = 0; i < 7; i++)
                {
                    stops.Add(new ImmutableGradientStop(i / 6.0, Oklch.ToColor(lightness[i], chroma[i], 260)));
                }
                yield return new ConicGradientBrush
                {
                    GradientStops = ToStops(stops), Angle = 215,
                    Center = new RelativePoint(0.58, 0.46, RelativeUnit.Relative),
                }.ToImmutable();
                yield return Radial(1.2, 0.9, 0.22, 0.12, (0, Oklch.ToColor(1, 0, 0, 0.8)), (0.5, Oklch.ToColor(1, 0, 0, 0)));
                break;
            }
        }
    }

    /// <summary>The static reflection bands over the texture, bottom first.</summary>
    public IEnumerable<IBrush> Sweep(Size size)
    {
        var white = Oklch.ToColor(1, 0, 0);
        switch (Kind)
        {
            case WalletStackMaterial.Black:
                yield return Linear(WalletStackCardArt.CssLine(118, size),
                    new[] { (0.30, Oklch.WithAlpha(white, 0)), (0.44, Oklch.WithAlpha(white, 0.07)), (0.58, Oklch.WithAlpha(white, 0)) });
                break;
            case WalletStackMaterial.Glass:
            {
                yield return Linear(WalletStackCardArt.CssLine(160, size),
                    new[] { (0.0, Oklch.WithAlpha(white, 0.35)), (0.38, Oklch.WithAlpha(white, 0)) });
                var cyan = Oklch.ToColor(0.8, 0.08, 200, 0.28);
                yield return Radial(0.9, 0.7, 1, 1, (0, cyan), (0.7, Oklch.WithAlpha(cyan, 0)));
                break;
            }
            case WalletStackMaterial.Color:
            {
                yield return Linear(WalletStackCardArt.CssLine(112, size), new[]
                {
                    (0.22, Oklch.WithAlpha(white, 0)), (0.38, Oklch.WithAlpha(white, 0.16)), (0.52, Oklch.WithAlpha(white, 0.03)),
                    (0.64, Oklch.WithAlpha(white, 0)),
                });
                var (l, c, h) = Oklch.FromColor(_tint);
                var glow = Oklch.ToColor(Math.Min(1, l + 0.16), c, h + 24, 0.3);
                yield return Radial(0.7, 0.6, 1, 1.1, (0, glow), (0.7, Oklch.WithAlpha(glow, 0)));
                break;
            }
            default:
                yield return Linear(WalletStackCardArt.CssLine(104, size),
                    new[] { (0.38, Oklch.WithAlpha(white, 0)), (0.47, Oklch.WithAlpha(white, 0.34)), (0.58, Oklch.WithAlpha(white, 0)) });
                break;
        }
    }

    private static GradientStops ToStops(IEnumerable<ImmutableGradientStop> stops)
    {
        var result = new GradientStops();
        foreach (var stop in stops)
        {
            result.Add(new GradientStop(stop.Color, stop.Offset));
        }
        return result;
    }

    // CSS radial-gradient(rx% ry% at x% y%, ...): an ellipse with radii relative to the box.
    private static IBrush Radial(double radiusX, double radiusY, double x, double y, params (double Offset, Color Color)[] stops)
    {
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(x, y, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(x, y, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(radiusX, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(radiusY, RelativeUnit.Relative),
        };
        foreach (var (offset, color) in stops)
        {
            brush.GradientStops.Add(new GradientStop(color, offset));
        }
        return brush.ToImmutable();
    }

    private static IBrush Linear((RelativePoint Start, RelativePoint End) line, (double Offset, Color Color)[] stops)
    {
        var list = new List<ImmutableGradientStop>(stops.Length);
        foreach (var (offset, color) in stops)
        {
            list.Add(new ImmutableGradientStop(offset, color));
        }
        return new ImmutableLinearGradientBrush(list, 1, null, null, GradientSpreadMethod.Pad, line.Start, line.End);
    }
}
