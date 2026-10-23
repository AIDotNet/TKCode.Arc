using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Text layouts of the speculative decoding stage, keyed by text, size, weight, color and width limit. They are
/// <see cref="TextLayout"/>s rather than <c>FormattedText</c>, because drawing a cached <c>FormattedText</c> shapes its
/// lines again on every frame while a <see cref="TextLayout"/> keeps them (see the implementation guide, section 7).
/// Opacity is applied by the caller with <c>PushOpacity</c>, so a fade reuses one layout. Widths are cached separately
/// for layout. A font or theme change calls <see cref="Clear"/>.
/// </summary>
/// <remarks>
/// Shared-code request: <c>ExplainerTextCache</c> hands out <c>FormattedText</c>; a <c>TextLayout</c> variant there would
/// let every explainer draw text per frame without re-shaping.
/// </remarks>
internal sealed class SpeculativeDecodingTextCache
{
    private readonly record struct Key(string Text, double Size, FontWeight Weight, Color Color, int MaxWidth);

    private readonly record struct WidthKey(string Text, double Size, FontWeight Weight);

    private readonly Dictionary<Key, TextLayout> _layouts = new();
    private readonly Dictionary<WidthKey, double> _widths = new();
    private readonly Dictionary<Color, IImmutableSolidColorBrush> _brushes = new();
    private readonly List<TextLayout> _retired = new();

    public SpeculativeDecodingTextCache(int capacity = 768)
    {
        Capacity = capacity;
    }

    /// <summary>Most layouts kept before the cache starts over.</summary>
    public int Capacity { get; }

    /// <summary>The font family the layouts use.</summary>
    public FontFamily FontFamily { get; private set; } = FontFamily.Default;

    /// <summary>Layouts built so far (tests: a steady scene stops growing this).</summary>
    public int Misses { get; private set; }

    /// <summary>Cached layouts.</summary>
    public int Count => _layouts.Count;

    /// <summary>Uses <paramref name="family"/> from now on (drops everything when it changes).</summary>
    public void SetFontFamily(FontFamily family)
    {
        if (!Equals(family, FontFamily))
        {
            FontFamily = family;
            Clear();
        }
    }

    /// <summary>A one-line layout of <paramref name="text"/>, cut with an ellipsis at <paramref name="maxWidth"/> when given.</summary>
    public TextLayout Get(string text, double size, FontWeight weight, Color color, double maxWidth = double.PositiveInfinity)
    {
        var width = double.IsFinite(maxWidth) ? Math.Max(1, (int)Math.Ceiling(maxWidth - 0.01)) : -1;
        var key = new Key(text, size, weight, color, width);
        if (_layouts.TryGetValue(key, out var layout))
        {
            return layout;
        }
        if (_layouts.Count >= Capacity)
        {
            DisposeLayouts();
        }
        layout = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, weight), size, Brush(color),
            textAlignment: TextAlignment.Left, textWrapping: TextWrapping.NoWrap,
            textTrimming: width > 0 ? TextTrimming.CharacterEllipsis : null,
            maxWidth: width > 0 ? width : double.PositiveInfinity, maxLines: 1);
        _layouts[key] = layout;
        Misses++;
        return layout;
    }

    /// <summary>The advance width of <paramref name="text"/> on one line (trailing spaces included).</summary>
    public double Width(string text, double size, FontWeight weight)
    {
        if (text.Length == 0)
        {
            return 0;
        }
        var key = new WidthKey(text, size, weight);
        if (_widths.TryGetValue(key, out var width))
        {
            return width;
        }
        using var layout = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, weight), size, null);
        width = layout.WidthIncludingTrailingWhitespace;
        _widths[key] = width;
        return width;
    }

    /// <summary>A cached solid brush of <paramref name="color"/>.</summary>
    public IImmutableSolidColorBrush Brush(Color color)
    {
        if (!_brushes.TryGetValue(color, out var brush))
        {
            brush = new ImmutableSolidColorBrush(color);
            _brushes[color] = brush;
        }
        return brush;
    }

    /// <summary>Drops every layout, width and brush (font or theme change).</summary>
    public void Clear()
    {
        DisposeLayouts();
        _widths.Clear();
        _brushes.Clear();
    }

    // A layout may still be referenced by the frame last sent to the compositor, so dropped layouts are disposed one
    // clear later, not at once.
    private void DisposeLayouts()
    {
        foreach (var layout in _retired)
        {
            layout.Dispose();
        }
        _retired.Clear();
        _retired.AddRange(_layouts.Values);
        _layouts.Clear();
    }
}
