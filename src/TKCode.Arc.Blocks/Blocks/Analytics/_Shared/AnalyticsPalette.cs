using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// Colour math of the Analytics blocks, all in OKLab like Arc's <c>color-mix(in oklab, …)</c>: weighted mixes, stepped
/// ramps (cohort heat steps, choropleth bins, uptime status fills) and the horizon band alphas. Inputs are token colours
/// read for the current theme, so every ramp follows light / dark and the accent.
/// </summary>
internal static class AnalyticsColors
{
    /// <summary>Cohort retention's six heat steps: Accent mixed into Surface at 8 / 18 / 32 / 48 / 66 / 86 %.</summary>
    public static readonly double[] CohortSteps = [0.08, 0.18, 0.32, 0.48, 0.66, 0.86];

    /// <summary>
    /// <c>color-mix(in oklab, <paramref name="color"/> <paramref name="weight"/>, <paramref name="into"/>)</c>: weight 0 is
    /// <paramref name="into"/>, 1 is <paramref name="color"/> (Metrics dashboard bars: Mix(Accent, 0.08, Surface)).
    /// </summary>
    public static Color Mix(Color color, double weight, Color into) => Oklch.MixOklab(into, color, Math.Clamp(weight, 0, 1));

    /// <summary>Writes <c>Mix(color, weights[i], into)</c> for each weight: a stepped sequential ramp.</summary>
    public static void Ramp(Color color, Color into, ReadOnlySpan<double> weights, Span<Color> destination)
    {
        for (var i = 0; i < weights.Length && i < destination.Length; i++)
        {
            destination[i] = Mix(color, weights[i], into);
        }
    }

    /// <summary>A stepped ramp as a new array (see <see cref="Ramp(Color, Color, ReadOnlySpan{double}, Span{Color})"/>).</summary>
    public static Color[] Ramp(Color color, Color into, ReadOnlySpan<double> weights)
    {
        var colors = new Color[weights.Length];
        Ramp(color, into, weights, colors);
        return colors;
    }

    /// <summary><paramref name="steps"/> weights spread evenly from <paramref name="from"/> to <paramref name="to"/> inclusive.</summary>
    public static double[] EvenWeights(int steps, double from, double to)
    {
        var weights = new double[Math.Max(0, steps)];
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] = steps == 1 ? to : from + (to - from) * i / (steps - 1);
        }
        return weights;
    }

    /// <summary>The colour with its alpha multiplied by <paramref name="alpha"/> (0–1).</summary>
    public static Color Fade(Color color, double alpha)
        => Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(alpha, 0, 1)), color.R, color.G, color.B);

    /// <summary>Horizon chart: the alpha of one band layer, <c>α = 1 − 0.08^(1 / bands)</c> (bands stacked reach 0.92).</summary>
    public static double HorizonLayerAlpha(double bands) => 1 - Math.Pow(0.08, 1 / Math.Max(1e-6, bands));

    /// <summary>Horizon chart: alpha after <paramref name="layers"/> layers of one side are stacked, <c>1 − (1 − α)^k</c>.</summary>
    public static double HorizonStackedAlpha(int layers, double bands) => 1 - Math.Pow(1 - HorizonLayerAlpha(bands), layers);

    /// <summary>Linear interpolation of two colours in OKLab (colour transitions drawn in Render).</summary>
    public static Color Lerp(Color a, Color b, double t) => Oklch.MixOklab(a, b, Math.Clamp(t, 0, 1));
}

/// <summary>
/// The theme colours, shadows and fonts a self-drawn Analytics plot needs, read once per theme from the owner's tokens,
/// plus immutable brush and pen caches keyed by colour (no per-frame allocation). Call <see cref="Refresh"/> while the
/// owner is attached (first render, and on <c>ActualThemeVariantChanged</c> / <c>ResourcesChanged</c>);
/// <see cref="Version"/> bumps each time so dependent caches (text colours, ramps) know to rebuild.
/// </summary>
internal sealed class AnalyticsPalette
{
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = new();
    private readonly Dictionary<PenKey, ImmutablePen> _pens = new();

    /// <summary>Bumped by every <see cref="Refresh"/>.</summary>
    public int Version { get; private set; }

    /// <summary>True after the first <see cref="Refresh"/>.</summary>
    public bool IsReady { get; private set; }

#pragma warning disable CS1591 // Token colours, named as the tokens.
    public Color Background { get; private set; }
    public Color Foreground { get; private set; }
    public Color TextSecondary { get; private set; }
    public Color TextMuted { get; private set; }
    public Color Border { get; private set; }
    public Color BorderSubtle { get; private set; }
    public Color BorderStrong { get; private set; }
    public Color Surface { get; private set; }
    public Color SurfaceRaised { get; private set; }
    public Color SurfaceMuted { get; private set; }
    public Color Accent { get; private set; }
    public Color AccentStrong { get; private set; }
    public Color AccentSubtle { get; private set; }
    public Color Success { get; private set; }
    public Color Warning { get; private set; }
    public Color Danger { get; private set; }
    public Color Series1 { get; private set; }
    public Color Series2 { get; private set; }
    public Color Series3 { get; private set; }
    public Color Series4 { get; private set; }
    public Color FocusRing { get; private set; }
    public BoxShadows ShadowResting { get; private set; }
    public BoxShadows ShadowRaised { get; private set; }
    public BoxShadows ShadowFloating { get; private set; }
    public FontFamily BodyFont { get; private set; } = FontFamily.Default;
    public FontFamily DisplayFont { get; private set; } = FontFamily.Default;
    public FontWeight Medium { get; private set; } = FontWeight.Medium;
#pragma warning restore CS1591

    /// <summary>The colour of a tone: Success, Danger, or TextMuted for flat.</summary>
    public Color ToneColor(AnalyticsTone tone) => tone switch
    {
        AnalyticsTone.Good => Success,
        AnalyticsTone.Bad => Danger,
        _ => TextMuted
    };

    /// <summary>Series colour 1–4 (other indexes wrap).</summary>
    public Color Series(int index) => (((index % 4) + 4) % 4) switch
    {
        0 => Series1,
        1 => Series2,
        2 => Series3,
        _ => Series4
    };

    /// <summary>Reads every token for <paramref name="owner"/>'s theme and clears the brush and pen caches.</summary>
    public void Refresh(Control owner)
    {
        Background = Read(owner, ArcDesignTokenKind.Background);
        Foreground = Read(owner, ArcDesignTokenKind.Foreground);
        TextSecondary = Read(owner, ArcDesignTokenKind.TextSecondary);
        TextMuted = Read(owner, ArcDesignTokenKind.TextMuted);
        Border = Read(owner, ArcDesignTokenKind.Border);
        BorderSubtle = Read(owner, ArcDesignTokenKind.BorderSubtle);
        BorderStrong = Read(owner, ArcDesignTokenKind.BorderStrong);
        Surface = Read(owner, ArcDesignTokenKind.Surface);
        SurfaceRaised = Read(owner, ArcDesignTokenKind.SurfaceRaised);
        SurfaceMuted = Read(owner, ArcDesignTokenKind.SurfaceMuted);
        Accent = Read(owner, ArcDesignTokenKind.Accent);
        AccentStrong = Read(owner, ArcDesignTokenKind.AccentStrong);
        AccentSubtle = Read(owner, ArcDesignTokenKind.AccentSubtle);
        Success = Read(owner, ArcDesignTokenKind.Success);
        Warning = Read(owner, ArcDesignTokenKind.Warning);
        Danger = Read(owner, ArcDesignTokenKind.Danger);
        Series1 = Read(owner, ArcDesignTokenKind.Series1);
        Series2 = Read(owner, ArcDesignTokenKind.Series2);
        Series3 = Read(owner, ArcDesignTokenKind.Series3);
        Series4 = Read(owner, ArcDesignTokenKind.Series4);
        FocusRing = Read(owner, ArcDesignTokenKind.FocusRing);
        ShadowResting = Shadow(owner, ArcDesignTokenKind.ShadowResting);
        ShadowRaised = Shadow(owner, ArcDesignTokenKind.ShadowRaised);
        ShadowFloating = Shadow(owner, ArcDesignTokenKind.ShadowFloating);
        BodyFont = Resource(owner, ArcDesignTokenKind.BodyFontFamily, FontFamily.Default);
        DisplayFont = Resource(owner, ArcDesignTokenKind.DisplayFontFamily, BodyFont);
        Medium = Resource(owner, ArcDesignTokenKind.WeightMedium, FontWeight.Medium);
        _brushes.Clear();
        _pens.Clear();
        IsReady = true;
        Version++;
    }

    /// <summary>A cached immutable brush of <paramref name="color"/> with its alpha multiplied by <paramref name="alpha"/>.</summary>
    public IImmutableSolidColorBrush Brush(Color color, double alpha = 1)
    {
        var a = alpha >= 1 ? color.A : (byte)Math.Round(color.A * Math.Clamp(alpha, 0, 1));
        var key = ((uint)a << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (!_brushes.TryGetValue(key, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromArgb(a, color.R, color.G, color.B));
            _brushes[key] = brush;
        }
        return brush;
    }

    /// <summary>
    /// A cached immutable pen. <paramref name="dash"/> is in pixels like SVG <c>stroke-dasharray</c> (e.g. 3, 3 for
    /// Metrics dashboard's previous period); round caps and joins by default (Arc's lines).
    /// </summary>
    public ImmutablePen Pen(Color color, double thickness, double alpha = 1, double dash = 0, double gap = 0,
        PenLineCap cap = PenLineCap.Round, PenLineJoin join = PenLineJoin.Round)
    {
        var brush = Brush(color, alpha);
        var key = new PenKey(brush.Color, thickness, dash, gap, cap, join);
        if (!_pens.TryGetValue(key, out var pen))
        {
            // Avalonia dashes are in multiples of the thickness; Arc's are in pixels.
            var dashStyle = dash > 0 && thickness > 0
                ? new ImmutableDashStyle(new[] { dash / thickness, (gap > 0 ? gap : dash) / thickness }, 0)
                : null;
            pen = new ImmutablePen(brush, thickness, dashStyle, cap, join);
            _pens[key] = pen;
        }
        return pen;
    }

    private static Color Read(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    private static BoxShadows Shadow(Control owner, ArcDesignTokenKind kind)
        => owner.TryFindResource(kind, owner.ActualThemeVariant, out var value) && value is BoxShadows shadows ? shadows : default;

    private static T Resource<T>(Control owner, ArcDesignTokenKind kind, T fallback)
        => owner.TryFindResource(kind, owner.ActualThemeVariant, out var value) && value is T typed ? typed : fallback;

    private readonly record struct PenKey(Color Color, double Thickness, double Dash, double Gap, PenLineCap Cap, PenLineJoin Join);
}
