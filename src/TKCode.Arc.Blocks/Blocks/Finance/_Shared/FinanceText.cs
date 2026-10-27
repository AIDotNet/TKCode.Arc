using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// A per-control cache of laid out text for the Finance blocks' self-drawn parts (axis labels, readouts, counting numbers):
/// a layout is built once per text, size, weight and brush and reused on every frame until <see cref="Clear"/> (theme or
/// font changes). Entries not drawn since the previous <see cref="Sweep"/> are disposed, so a live chart whose labels keep
/// changing does not grow the cache.
/// </summary>
internal sealed class FinanceText
{
    public static readonly FontFeatureCollection Tabular = new() { FontFeature.Parse("tnum") };

    private readonly record struct Key(string Text, double Size, FontWeight Weight, IBrush Brush);

    private Dictionary<Key, TextLayout> _current = new();
    private Dictionary<Key, TextLayout> _previous = new();

    public FontFamily FontFamily { get; set; } = FontFamily.Default;

    /// <summary>The layout of <paramref name="text"/>; reused while the same text, size, weight and brush are asked for.</summary>
    public TextLayout Get(string text, double size, FontWeight weight, IBrush brush)
    {
        var key = new Key(text, size, weight, brush);
        if (_current.TryGetValue(key, out var layout))
        {
            return layout;
        }
        if (_previous.Remove(key, out layout))
        {
            _current[key] = layout;
            return layout;
        }
        layout = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, weight), size, brush, TextAlignment.Left,
            TextWrapping.NoWrap, TextTrimming.None, textDecorations: null, FlowDirection.LeftToRight, double.PositiveInfinity,
            double.PositiveInfinity, double.NaN, 0, 1, Tabular);
        _current[key] = layout;
        return layout;
    }

    /// <summary>Draws <paramref name="text"/> with its left edge (or right edge when <paramref name="alignRight"/>) at x.</summary>
    public Size Draw(DrawingContext context, string text, double size, FontWeight weight, IBrush brush, double x, double y,
        bool alignRight = false)
    {
        var layout = Get(text, size, weight, brush);
        var width = layout.WidthIncludingTrailingWhitespace;
        layout.Draw(context, new Point(alignRight ? x - width : x, y));
        return new Size(width, layout.Height);
    }

    /// <summary>Call once per frame after drawing: layouts not used since the previous sweep are disposed.</summary>
    public void Sweep()
    {
        foreach (var layout in _previous.Values)
        {
            layout.Dispose();
        }
        _previous.Clear();
        (_previous, _current) = (_current, _previous);
    }

    /// <summary>Drops every layout (the theme, font or scaling changed).</summary>
    public void Clear()
    {
        foreach (var layout in _current.Values)
        {
            layout.Dispose();
        }
        foreach (var layout in _previous.Values)
        {
            layout.Dispose();
        }
        _current.Clear();
        _previous.Clear();
    }
}
