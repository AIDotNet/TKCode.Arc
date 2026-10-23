using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The colors of the bars, the band and the flight of a <see cref="PromptCache"/>, cached as immutable brushes and pens
/// so frames allocate nothing. Each segment has a solid (processed) fill, a cached fill (its color mixed 28% into the
/// surface) with a 1px inner outline at 55% alpha, and a band gradient (transparent, 80% of its color, transparent). It is
/// rebuilt when the theme, the accent or a segment's color changes.
/// </summary>
internal sealed class PromptCachePalette
{
    /// <summary>The segment colors used when a segment has none of its own: series 3, 2, 4, 1 (amber, purple, teal, blue).</summary>
    public static readonly int[] SeriesOrder = [2, 1, 3, 0];

    private static readonly ImmutableSolidColorBrush s_transparent = new(Colors.Transparent);

    public PromptCachePalette(Color surface, Color track, Color message, Color warning, Color keyInk, IReadOnlyList<Color> segments)
    {
        Surface = surface;
        Track = new ImmutableSolidColorBrush(track);
        Message = new ImmutableSolidColorBrush(message);
        Warning = new ImmutableSolidColorBrush(warning);
        KeySolid = new ImmutableSolidColorBrush(keyInk);
        KeyCached = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, keyInk, 0.2));
        KeyOutline = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(keyInk, 0.5)), 1);
        var count = segments.Count;
        SegmentColors = segments.ToArray();
        Solid = new IImmutableBrush[count];
        CachedFill = new IImmutableBrush[count];
        CachedOutline = new ImmutablePen[count];
        Band = new IImmutableBrush[count];
        for (var i = 0; i < count; i++)
        {
            var c = segments[i];
            Solid[i] = new ImmutableSolidColorBrush(c);
            CachedFill[i] = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, c, 0.28));
            CachedOutline[i] = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(c, 0.55)), 1);
            var band = Oklch.MixOklab(surface, c, 0.8);
            Band[i] = new ImmutableLinearGradientBrush(
                [
                    new ImmutableGradientStop(0, Oklch.WithAlpha(band, 0)),
                    new ImmutableGradientStop(0.5, band),
                    new ImmutableGradientStop(1, Oklch.WithAlpha(band, 0))
                ],
                startPoint: new RelativePoint(0, 0.5, RelativeUnit.Relative),
                endPoint: new RelativePoint(1, 0.5, RelativeUnit.Relative));
        }
    }

    public Color Surface { get; }

    /// <summary>The colors of the segments, in order.</summary>
    public Color[] SegmentColors { get; }

    public IImmutableBrush Track { get; }

    public IImmutableBrush Message { get; }

    public IImmutableBrush Warning { get; }

    /// <summary>The column keys: the solid one and the cached one with its outline.</summary>
    public IImmutableBrush KeySolid { get; }

    public IImmutableBrush KeyCached { get; }

    public ImmutablePen KeyOutline { get; }

    public IImmutableBrush[] Solid { get; }

    public IImmutableBrush[] CachedFill { get; }

    public ImmutablePen[] CachedOutline { get; }

    public IImmutableBrush[] Band { get; }

    public static IImmutableBrush Transparent => s_transparent;

    /// <summary>The solid fill of segment <paramref name="i"/> (wraps around).</summary>
    public IImmutableBrush SolidOf(int i) => Solid.Length == 0 ? Track : Solid[i % Solid.Length];

    public IImmutableBrush CachedOf(int i) => CachedFill.Length == 0 ? Track : CachedFill[i % CachedFill.Length];

    public ImmutablePen? OutlineOf(int i) => CachedOutline.Length == 0 ? null : CachedOutline[i % CachedOutline.Length];

    public IImmutableBrush BandOf(int i) => Band.Length == 0 ? Track : Band[i % Band.Length];
}
