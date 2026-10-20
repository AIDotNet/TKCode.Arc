using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Neutral steps a chart falls back to after the four series colors.</summary>
internal enum ChartNeutralRamp
{
    /// <summary>DonutChart: foreground mixed into the surface at 56, 42, 32%.</summary>
    Donut,

    /// <summary>Streamgraph: 30, 18, 24, 14% so stacked neighbours stay apart.</summary>
    Stream
}

/// <summary>
/// Colors, pens, brushes and fonts captured from the chart's theme (ArcDesign tokens). Rebuilt only when a
/// token changes (theme or accent switch); brushes and pens are cached by color so frames allocate nothing.
/// </summary>
internal sealed class ChartPalette
{
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = new();
    private readonly Dictionary<(uint Color, double Thickness, int Dash), ImmutablePen> _pens = new();

    public int Version { get; private set; }

    public bool IsDark { get; private set; }

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
    public Color Success { get; private set; }
    public Color Warning { get; private set; }
    public Color Danger { get; private set; }
    public Color FocusRing { get; private set; }
    public Color[] Series { get; } = new Color[4];
    public BoxShadows FloatingShadow { get; private set; }
    public BoxShadows RestingShadow { get; private set; }
    public Typeface Body { get; private set; } = Typeface.Default;
    public Typeface BodyMedium { get; private set; } = Typeface.Default;
    public Typeface Display { get; private set; } = Typeface.Default;
    public double SizeXS { get; private set; } = 12;
    public double SizeSM { get; private set; } = 14;
    public double SizeBase { get; private set; } = 16;
    public double SizeLG { get; private set; } = 18;
    public double Size2XL { get; private set; } = 28;

    public void Capture(ChartBase chart)
    {
        Foreground = ColorOf(chart.Foreground);
        TextSecondary = ColorOf(chart.SecondaryBrush);
        TextMuted = ColorOf(chart.MutedBrush);
        Border = ColorOf(chart.GridBrush);
        BorderSubtle = ColorOf(chart.SubtleBrush);
        BorderStrong = ColorOf(chart.StrongBrush);
        Surface = ColorOf(chart.SurfaceBrush);
        SurfaceRaised = ColorOf(chart.RaisedBrush);
        SurfaceMuted = ColorOf(chart.SurfaceMutedBrush);
        Accent = ColorOf(chart.AccentBrush);
        AccentStrong = ColorOf(chart.AccentStrongBrush);
        Success = ColorOf(chart.SuccessBrush);
        Warning = ColorOf(chart.WarningBrush);
        Danger = ColorOf(chart.DangerBrush);
        FocusRing = ColorOf(chart.FocusRingBrush);
        Series[0] = ColorOf(chart.Series1Brush);
        Series[1] = ColorOf(chart.Series2Brush);
        Series[2] = ColorOf(chart.Series3Brush);
        Series[3] = ColorOf(chart.Series4Brush);
        FloatingShadow = chart.FloatingShadow;
        RestingShadow = chart.RestingShadow;
        IsDark = Oklch.FromColor(Surface.A == 0 ? Colors.White : Surface).L < 0.5;
        var medium = chart.MediumWeight;
        Body = new Typeface(chart.FontFamily, FontStyle.Normal, FontWeight.Normal);
        BodyMedium = new Typeface(chart.FontFamily, FontStyle.Normal, medium);
        Display = new Typeface(chart.DisplayFontFamily ?? chart.FontFamily, FontStyle.Normal, medium);
        SizeXS = chart.TextSizeXS;
        SizeSM = chart.TextSizeSM;
        SizeBase = chart.TextSizeBase;
        SizeLG = chart.TextSizeLG;
        Size2XL = chart.TextSize2XL;
        _brushes.Clear();
        _pens.Clear();
        Version++;
    }

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;

    /// <summary>CSS <c>color-mix(in oklab, a p%, b)</c>.</summary>
    public static Color Mix(Color a, double share, Color b) => Oklch.MixOklab(b, a, Math.Clamp(share, 0, 1));

    /// <summary>
    /// Default color of the series at <paramref name="index"/>: Series1–4, then neutral steps of the foreground
    /// in the surface (Arc's <c>--series-*</c> rule).
    /// </summary>
    public Color SeriesColor(int index, ChartNeutralRamp ramp = ChartNeutralRamp.Donut)
    {
        if (index < 4)
        {
            return Series[Math.Max(0, index)];
        }
        var steps = ramp == ChartNeutralRamp.Stream ? s_streamSteps : s_donutSteps;
        return Mix(Foreground, steps[(index - 4) % steps.Length], Surface);
    }

    private static readonly double[] s_donutSteps = [0.56, 0.42, 0.32];
    private static readonly double[] s_streamSteps = [0.30, 0.18, 0.24, 0.14];

    /// <summary>The quietest neutral, used for "Other" (26%).</summary>
    public Color OtherColor => Mix(Foreground, 0.26, Surface);

    public Color ToneColor(ChartTone tone) => tone switch
    {
        ChartTone.Success => Success,
        ChartTone.Warning => Warning,
        ChartTone.Danger => Danger,
        _ => Accent
    };

    public Color ToneTextColor(ChartTone tone) => tone == ChartTone.Accent ? AccentStrong : ToneColor(tone);

    /// <summary>A cached immutable brush for <paramref name="color"/>.</summary>
    public IImmutableSolidColorBrush Brush(Color color)
    {
        var key = color.ToUInt32();
        if (!_brushes.TryGetValue(key, out var brush))
        {
            if (_brushes.Count > 512)
            {
                _brushes.Clear();
            }
            brush = new ImmutableSolidColorBrush(color);
            _brushes[key] = brush;
        }
        return brush;
    }

    /// <summary>
    /// A cached pen. <paramref name="dash"/>: 0 solid, 1 Arc's line dash (4 5), 2 the bar chart mean (3 3),
    /// 3 the legend/tooltip swatch dash (4 3).
    /// </summary>
    public ImmutablePen Pen(Color color, double thickness, int dash = 0, PenLineCap cap = PenLineCap.Flat)
    {
        var key = (color.ToUInt32() ^ ((uint)cap << 30), thickness, dash);
        if (!_pens.TryGetValue(key, out var pen))
        {
            if (_pens.Count > 256)
            {
                _pens.Clear();
            }
            ImmutableDashStyle? style = dash switch
            {
                // Avalonia dash lengths are multiples of the stroke thickness.
                1 => new ImmutableDashStyle([4 / thickness, 5 / thickness], 0),
                2 => new ImmutableDashStyle([3 / thickness, 3 / thickness], 0),
                3 => new ImmutableDashStyle([4 / thickness, 3 / thickness], 0),
                _ => null
            };
            pen = new ImmutablePen(Brush(color), thickness, style, cap, PenLineJoin.Round);
            _pens[key] = pen;
        }
        return pen;
    }

    /// <summary>Interpolates two colors in sRGB (used for short state fades between cached steps).</summary>
    public static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t),
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    /// <summary>Quantizes a 0..1 fade so color fades reuse a bounded set of cached brushes.</summary>
    public static double Step(double t) => Math.Round(Math.Clamp(t, 0, 1) * 24) / 24;
}

/// <summary>
/// Formatted text cache keyed by text and style. Charts draw many labels every frame while scrubbing; each
/// distinct label is shaped once. Cleared when the palette changes.
/// </summary>
internal sealed class ChartTextCache
{
    private readonly record struct Key(string Text, double Size, ushort Weight, uint Color, int MaxWidth, bool Display, bool Wrap);

    private readonly Dictionary<Key, FormattedText> _texts = new();
    private int _version = -1;

    public FormattedText Get(ChartPalette palette, string text, double size, Color color, bool medium = false,
                             double maxWidth = 0, bool display = false, bool wrap = false)
    {
        if (_version != palette.Version)
        {
            _texts.Clear();
            _version = palette.Version;
        }
        var key = new Key(text, size, medium ? (ushort)500 : (ushort)400, color.ToUInt32(),
            maxWidth > 0 ? (int)Math.Floor(maxWidth) : 0, display, wrap);
        if (_texts.TryGetValue(key, out var formatted))
        {
            return formatted;
        }
        if (_texts.Count > 1024)
        {
            _texts.Clear();
        }
        var typeface = display ? palette.Display : medium ? palette.BodyMedium : palette.Body;
        formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, palette.Brush(color));
        if (maxWidth > 0)
        {
            formatted.MaxTextWidth = Math.Max(1, key.MaxWidth);
            if (!wrap)
            {
                formatted.MaxLineCount = 1;
                formatted.Trimming = TextTrimming.CharacterEllipsis;
            }
        }
        _texts[key] = formatted;
        return formatted;
    }

    public void Clear() => _texts.Clear();
}
