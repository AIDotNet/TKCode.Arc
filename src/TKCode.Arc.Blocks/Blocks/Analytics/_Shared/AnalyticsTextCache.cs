using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Text and brush cache for self-drawn Analytics plots, so frames allocate next to nothing: laid-out texts
/// (<see cref="AnalyticsText"/>) keyed by text, typeface, font size quantized to <see cref="FontSizeStep"/> (0.5px) and
/// layout (plain, one line cut with an ellipsis at a width, or wrapped to at most N lines with an ellipsis; widths in
/// whole px), and immutable brushes keyed by color with alpha quantized to <see cref="AlphaLevels"/> (16) levels. Colors
/// are not part of a text's key: <see cref="AnalyticsText.Draw"/> takes the brush, so one layout serves every color and
/// fade level (or vary opacity with <c>DrawingContext.PushOpacity</c>). Theme changes must <see cref="Clear"/> it
/// (do it on <c>ActualThemeVariantChanged</c> / <c>ResourcesChanged</c>). When <see cref="Capacity"/> texts are cached the text cache starts over.
/// </summary>
/// <remarks>
/// Text is laid out like a <c>TextBlock</c> (no culture of its own: Avalonia shapes with the current culture and picks
/// fallback fonts for the current UI culture), left to right. A dropped text stays valid until the cache drops texts
/// once more (its layout is disposed then), so a text fetched earlier in a frame can still be drawn.
/// </remarks>
internal sealed class AnalyticsTextCache
{
    /// <summary>Font sizes are rounded to multiples of this (px).</summary>
    public const double FontSizeStep = 0.5;

    /// <summary>Alpha multipliers are rounded to one of this many levels (0, 1/15, …, 1).</summary>
    public const int AlphaLevels = 16;

    private readonly Dictionary<TextKey, AnalyticsText> _texts = new();
    private readonly List<AnalyticsText> _dropped = new();
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = new();

    public AnalyticsTextCache(int capacity = 2048)
    {
        Capacity = Math.Max(16, capacity);
    }

    /// <summary>Most texts kept before the cache starts over.</summary>
    public int Capacity { get; }

    /// <summary>Cached texts.</summary>
    public int Count => _texts.Count;

    /// <summary>Cached brushes.</summary>
    public int BrushCount => _brushes.Count;

    /// <summary>Times a text was laid out (tests and tuning: a steady scene stops growing this).</summary>
    public int Misses { get; private set; }

    /// <summary>A font size rounded to the nearest 0.5px (at least 0.5).</summary>
    public static double QuantizeFontSize(double size) =>
        Math.Max(FontSizeStep, Math.Round(size / FontSizeStep, MidpointRounding.AwayFromZero) * FontSizeStep);

    /// <summary>The alpha level 0…15 of a multiplier in [0, 1].</summary>
    public static int AlphaLevel(double alpha) =>
        (int)Math.Round(Math.Clamp(double.IsNaN(alpha) ? 0 : alpha, 0, 1) * (AlphaLevels - 1), MidpointRounding.AwayFromZero);

    /// <summary>An alpha multiplier rounded to its level.</summary>
    public static double QuantizeAlpha(double alpha) => AlphaLevel(alpha) / (double)(AlphaLevels - 1);

    /// <summary>A cached brush of <paramref name="color"/> with its alpha multiplied by the quantized <paramref name="alpha"/>.</summary>
    public IImmutableSolidColorBrush Brush(Color color, double alpha = 1)
    {
        var a = (byte)Math.Round(color.A * QuantizeAlpha(alpha));
        var key = ((uint)a << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (!_brushes.TryGetValue(key, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromArgb(a, color.R, color.G, color.B));
            _brushes[key] = brush;
        }
        return brush;
    }

    /// <summary>
    /// Tabular figures (OpenType <c>tnum</c>): every digit has the width of "0", like Arc's <c>font-variant-numeric:
    /// tabular-nums</c> on axis labels, readout values and table numbers (a counting value does not jitter).
    /// </summary>
    public static readonly FontFeatureCollection TabularFigures = new() { FontFeature.Parse("tnum") };

    /// <summary>One line of text, unconstrained; <paramref name="tabular"/> lays digits out with tabular figures.</summary>
    public AnalyticsText Get(string text, Typeface typeface, double fontSize, bool tabular = false)
        => GetCore(text, typeface, fontSize, -1, 0, tabular);

    /// <summary>One line cut with an ellipsis to fit <paramref name="maxWidth"/> px (rounded down to whole px).</summary>
    public AnalyticsText GetTrimmed(string text, Typeface typeface, double fontSize, double maxWidth, bool tabular = false)
        => GetCore(text, typeface, fontSize, WidthBucket(maxWidth), -1, tabular);

    /// <summary>
    /// Text wrapped at <paramref name="maxWidth"/> px to at most <paramref name="maxLines"/> lines (default 2). When more
    /// would be needed, the last line holds the rest of the text cut with an ellipsis at the width (as much as fits); a
    /// word wider than a line is cut with an ellipsis on its own line.
    /// </summary>
    public AnalyticsText GetWrapped(string text, Typeface typeface, double fontSize, double maxWidth, int maxLines = 2)
        => GetCore(text, typeface, fontSize, WidthBucket(maxWidth), Math.Max(1, maxLines), false);

    /// <summary>
    /// Text wrapped at <paramref name="maxWidth"/> px to at most <paramref name="maxLines"/> lines, like a line clamp:
    /// when more would be needed, the last line keeps its break and ends in an ellipsis; a word wider than a line breaks.
    /// </summary>
    public AnalyticsText GetClamped(string text, Typeface typeface, double fontSize, double maxWidth, int maxLines)
        => GetCore(text, typeface, fontSize, WidthBucket(maxWidth), -1 - Math.Max(1, maxLines), false);

    private static int WidthBucket(double width) => Math.Max(1, (int)Math.Floor(double.IsFinite(width) ? width : 100000));

    // mode: 0 one line, −1 one line cut at maxWidth, N wrapped to N lines, −1 − N clamped to N lines.
    private AnalyticsText GetCore(string text, Typeface typeface, double fontSize, int maxWidth, int mode, bool tabular)
    {
        if (mode != 0)
        {
            // A text that fits on one line is that line at every width (no cut, no wrap): a caption on a card that
            // grows or shrinks keeps one layout instead of one per width.
            var plain = GetCore(text, typeface, fontSize, -1, 0, tabular);
            if (plain.Lines.Count == 1 && plain.Width <= maxWidth)
            {
                return plain;
            }
        }
        var size = QuantizeFontSize(fontSize);
        var key = new TextKey(text, typeface, size, maxWidth, mode, tabular);
        if (_texts.TryGetValue(key, out var cached))
        {
            return cached;
        }
        if (_texts.Count >= Capacity)
        {
            DropTexts();
        }
        var result = mode switch
        {
            0 => new AnalyticsText(new TextLayout(text, typeface, size, Brushes.Black, fontFeatures: tabular ? TabularFigures : null)),
            -1 => new AnalyticsText(Layout(text, typeface, size, TextWrapping.NoWrap, maxWidth, 1, tabular)),
            < -1 => new AnalyticsText(Layout(text, typeface, size, TextWrapping.Wrap, maxWidth, -1 - mode)),
            _ => Wrapped(text, typeface, size, maxWidth, mode)
        };
        _texts[key] = result;
        Misses++;
        return result;
    }

    private static TextLayout Layout(string text, Typeface typeface, double size, TextWrapping wrapping, int maxWidth, int maxLines, bool tabular = false) =>
        new(text, typeface, size, Brushes.Black, TextAlignment.Left, wrapping, TextTrimming.CharacterEllipsis, null,
            FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity, double.NaN, 0, maxLines, fontFeatures: tabular ? TabularFigures : null);

    /// <summary>
    /// Lines as <c>FormattedText</c> breaks them with a width, a line count and a character ellipsis (what this cache kept
    /// before it kept layouts): wrapped lines, each cut at the width if one word overflows it, and when text is left over,
    /// the last line laid out again from its start without wrapping and cut at the width.
    /// </summary>
    private static AnalyticsText Wrapped(string text, Typeface typeface, double size, int maxWidth, int maxLines)
    {
        var layout = Layout(text, typeface, size, TextWrapping.WrapWithOverflow, maxWidth, maxLines);
        var lines = layout.TextLines;
        var end = 0;
        foreach (var line in lines)
        {
            end += line.Length;
        }
        if (end >= text.Length || lines.Count < maxLines)
        {
            return new AnalyticsText(layout);
        }
        var start = lines[maxLines - 1].FirstTextSourceIndex;
        var tail = Layout(text[start..], typeface, size, TextWrapping.NoWrap, maxWidth, 0);
        return new AnalyticsText(layout, maxLines - 1, tail);
    }

    /// <summary>Drops every cached text and brush (theme or font change).</summary>
    public void Clear()
    {
        DropTexts();
        _brushes.Clear();
    }

    private void DropTexts()
    {
        // Texts dropped last time are disposed now; this time's stay drawable until then.
        foreach (var text in _dropped)
        {
            text.Dispose();
        }
        _dropped.Clear();
        _dropped.AddRange(_texts.Values);
        _texts.Clear();
    }

    private readonly record struct TextKey(string Text, Typeface Typeface, double Size, int MaxWidth, int Mode, bool Tabular);
}
