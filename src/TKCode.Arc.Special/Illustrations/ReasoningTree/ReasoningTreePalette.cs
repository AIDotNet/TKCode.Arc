using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The colors, brushes, pens and text layouts the reasoning tree draws with, rebuilt from the stage's ink properties
/// (theme tokens) when the theme or accent changes. Derived colors follow the measured mixes: the path border is 60%
/// accent into the border, the answer fill 7% success into the surface, the answer border 55% success into the border,
/// and the sheen 28% (light) or 30% (dark) accent into white or the foreground. Mixed colors are quantized to 16 steps,
/// so a fade allocates a handful of brushes once and then reuses them.
/// </summary>
internal sealed class ReasoningTreePalette
{
    private const int MixSteps = 15;
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = new();
    private readonly Dictionary<uint, ImmutablePen> _borderPens = new();

    public Color Surface;
    public Color SurfaceMuted;
    public Color Border;
    public Color BorderStrong;
    public Color Foreground;
    public Color TextSecondary;
    public Color TextMuted;
    public Color Success;
    public Color Accent;
    public Color FocusRing;
    public Color Background;
    public Color PathBorder;
    public Color AnswerFill;
    public Color AnswerBorder;
    public Color Sheen;
    public bool IsDark;
    public FontFamily FontFamily = FontFamily.Default;
    public Typeface Medium;
    public Typeface Regular;

    /// <summary>Strong-border pen for the edges (1.25px, round caps).</summary>
    public ImmutablePen EdgePen = new(Brushes.Gray, 1.25);

    /// <summary>The keyboard ring (2px).</summary>
    public ImmutablePen FocusPen = new(Brushes.Gray, 2);

    /// <summary>The answer's check (1.75 in the 24px glyph box).</summary>
    public ImmutablePen CheckPen = new(Brushes.Green, 1.75);

    public ReasoningTreeTextCache Texts { get; } = new();

    /// <summary>Takes the ink colors and derives the rest.</summary>
    public void Capture(Color surface, Color surfaceMuted, Color border, Color borderStrong, Color foreground, Color secondary,
        Color muted, Color success, Color accent, Color focus, Color background, FontFamily fontFamily)
    {
        Surface = surface;
        SurfaceMuted = surfaceMuted;
        Border = border;
        BorderStrong = borderStrong;
        Foreground = foreground;
        TextSecondary = secondary;
        TextMuted = muted;
        Success = success;
        Accent = accent;
        FocusRing = focus;
        Background = background;
        IsDark = Luminance(surface) < Luminance(foreground);
        PathBorder = Oklch.MixOklab(border, accent, 0.6);
        AnswerFill = Oklch.MixOklab(surface, success, 0.07);
        AnswerBorder = Oklch.MixOklab(border, success, 0.55);
        Sheen = IsDark ? Oklch.MixOklab(foreground, accent, 0.3) : Oklch.MixOklab(Colors.White, accent, 0.28);
        FontFamily = fontFamily;
        Medium = new Typeface(fontFamily, FontStyle.Normal, FontWeight.Medium);
        Regular = new Typeface(fontFamily, FontStyle.Normal, FontWeight.Normal);
        _brushes.Clear();
        _borderPens.Clear();
        Texts.Clear();
        EdgePen = new ImmutablePen(Brush(borderStrong), 1.25, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        FocusPen = new ImmutablePen(Brush(focus), 2);
        CheckPen = new ImmutablePen(Brush(success), 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    }

    /// <summary>A cached brush of <paramref name="color"/>.</summary>
    public IImmutableSolidColorBrush Brush(Color color)
    {
        var key = Key(color);
        if (!_brushes.TryGetValue(key, out var brush))
        {
            brush = new ImmutableSolidColorBrush(color);
            _brushes[key] = brush;
        }
        return brush;
    }

    /// <summary>A cached 1px pen of <paramref name="color"/> (card borders).</summary>
    public ImmutablePen BorderPen(Color color)
    {
        var key = Key(color);
        if (!_borderPens.TryGetValue(key, out var pen))
        {
            pen = new ImmutablePen(Brush(color), 1);
            _borderPens[key] = pen;
        }
        return pen;
    }

    /// <summary>A mix of two colors at <paramref name="t"/> rounded to one of 16 steps (so brushes stay few).</summary>
    public static Color Mix(Color a, Color b, double t)
    {
        var step = Math.Round(Math.Clamp(double.IsNaN(t) ? 0 : t, 0, 1) * MixSteps) / MixSteps;
        if (step <= 0)
        {
            return a;
        }
        if (step >= 1)
        {
            return b;
        }
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * step),
            (byte)Math.Round(a.R + (b.R - a.R) * step),
            (byte)Math.Round(a.G + (b.G - a.G) * step),
            (byte)Math.Round(a.B + (b.B - a.B) * step));
    }

    private static uint Key(Color color) => ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    private static double Luminance(Color c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
}

/// <summary>
/// Cached <see cref="TextLayout"/>s for the reasoning tree. A layout keeps its shaped lines, so drawing it every frame
/// (the stage and the cards redraw while the search animates) costs only the glyph runs; a FormattedText would be
/// shaped again on every draw. Keyed by text, typeface, size, color, width, line count and alignment; the cache starts
/// over (disposing its layouts) past <see cref="Capacity"/> entries and on theme changes.
/// </summary>
internal sealed class ReasoningTreeTextCache
{
    public const int Capacity = 768;

    /// <summary>Digits of equal width (the score digits never shift as they count).</summary>
    public static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    private readonly Dictionary<Key, TextLayout> _layouts = new();

    /// <summary>Layouts built so far (tests: a steady scene stops growing this).</summary>
    public int Misses { get; private set; }

    /// <summary>
    /// A layout of <paramref name="text"/>: one line cut with an ellipsis at <paramref name="maxWidth"/> when
    /// <paramref name="maxLines"/> is 1, or wrapped to at most that many lines (the last one cut).
    /// </summary>
    public TextLayout Get(string text, Typeface typeface, double size, IImmutableSolidColorBrush brush, double maxWidth,
        int maxLines = 1, double lineHeight = double.NaN, TextAlignment alignment = TextAlignment.Left, double letterSpacing = 0,
        bool tabular = false)
    {
        var width = double.IsFinite(maxWidth) ? Math.Max(1, Math.Floor(maxWidth)) : double.PositiveInfinity;
        var key = new Key(text, typeface, size, brush.Color, width, maxLines, lineHeight, alignment, letterSpacing, tabular);
        if (_layouts.TryGetValue(key, out var cached))
        {
            return cached;
        }
        if (_layouts.Count >= Capacity)
        {
            Clear();
        }
        var layout = new TextLayout(text, typeface, size, brush, alignment,
            maxLines == 1 ? TextWrapping.NoWrap : TextWrapping.Wrap,
            TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, width, double.PositiveInfinity,
            lineHeight, letterSpacing, maxLines, tabular ? TabularNumbers : null);
        _layouts[key] = layout;
        Misses++;
        return layout;
    }

    /// <summary>Disposes every layout.</summary>
    public void Clear()
    {
        foreach (var layout in _layouts.Values)
        {
            layout.Dispose();
        }
        _layouts.Clear();
    }

    private readonly record struct Key(string Text, Typeface Typeface, double Size, Color Color, double Width, int Lines,
        double LineHeight, TextAlignment Alignment, double LetterSpacing, bool Tabular);
}
