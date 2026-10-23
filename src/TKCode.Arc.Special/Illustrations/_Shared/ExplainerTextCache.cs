using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Text and brush cache for self-drawn explainer diagrams, so frames allocate nothing: <see cref="FormattedText"/> keyed
/// by text, typeface, font size quantized to <see cref="FontSizeStep"/> (0.5px), color and layout (plain, one line cut
/// with an ellipsis at a width, or wrapped to at most N lines with an ellipsis; widths in whole px), and immutable
/// brushes keyed by color with alpha quantized to <see cref="AlphaLevels"/> (16) levels. Vary text opacity with
/// <c>DrawingContext.PushOpacity</c> rather than new brushes. Theme changes must <see cref="Clear"/> it
/// (each explainer does in its theme-change handler). When <see cref="Capacity"/> texts are cached the text cache starts over.
/// </summary>
internal sealed class ExplainerTextCache
{
    /// <summary>Font sizes are rounded to multiples of this (px).</summary>
    public const double FontSizeStep = 0.5;

    /// <summary>Alpha multipliers are rounded to one of this many levels (0, 1/15, …, 1).</summary>
    public const int AlphaLevels = 16;

    private readonly Dictionary<TextKey, FormattedText> _texts = new();
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = new();

    public ExplainerTextCache(int capacity = 2048)
    {
        Capacity = Math.Max(16, capacity);
    }

    /// <summary>Most texts kept before the cache starts over.</summary>
    public int Capacity { get; }

    /// <summary>Culture used to shape text (default: the current culture).</summary>
    public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

    /// <summary>Cached texts.</summary>
    public int Count => _texts.Count;

    /// <summary>Cached brushes.</summary>
    public int BrushCount => _brushes.Count;

    /// <summary>Times a text was built (tests and tuning: a steady scene stops growing this).</summary>
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

    /// <summary>One line of text, unconstrained.</summary>
    public FormattedText Get(string text, Typeface typeface, double fontSize, Color color)
        => GetCore(text, typeface, fontSize, color, -1, 1);

    /// <summary>One line cut with an ellipsis to fit <paramref name="maxWidth"/> px (rounded down to whole px).</summary>
    public FormattedText GetTrimmed(string text, Typeface typeface, double fontSize, Color color, double maxWidth)
        => GetCore(text, typeface, fontSize, color, WidthBucket(maxWidth), 1);

    /// <summary>
    /// Text wrapped at <paramref name="maxWidth"/> px to at most <paramref name="maxLines"/> lines (default 2), the last
    /// one cut with an ellipsis.
    /// </summary>
    public FormattedText GetWrapped(string text, Typeface typeface, double fontSize, Color color, double maxWidth, int maxLines = 2)
        => GetCore(text, typeface, fontSize, color, WidthBucket(maxWidth), Math.Max(1, maxLines), wrap: true);

    private static int WidthBucket(double width) => Math.Max(1, (int)Math.Floor(double.IsFinite(width) ? width : 100000));

    private FormattedText GetCore(string text, Typeface typeface, double fontSize, Color color, int maxWidth, int lines, bool wrap = false)
    {
        var size = QuantizeFontSize(fontSize);
        var key = new TextKey(text, typeface, size, color, maxWidth, wrap ? lines : maxWidth >= 0 ? -1 : 0);
        if (_texts.TryGetValue(key, out var cached))
        {
            return cached;
        }
        if (_texts.Count >= Capacity)
        {
            _texts.Clear();
        }
        var formatted = new FormattedText(text, Culture, FlowDirection.LeftToRight, typeface, size, Brush(color));
        if (maxWidth >= 0)
        {
            formatted.MaxTextWidth = maxWidth;
            formatted.MaxLineCount = lines;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }
        _texts[key] = formatted;
        Misses++;
        return formatted;
    }

    /// <summary>Drops every cached text and brush (theme or font change).</summary>
    public void Clear()
    {
        _texts.Clear();
        _brushes.Clear();
    }

    private readonly record struct TextKey(string Text, Typeface Typeface, double Size, Color Color, int MaxWidth, int Mode);
}
