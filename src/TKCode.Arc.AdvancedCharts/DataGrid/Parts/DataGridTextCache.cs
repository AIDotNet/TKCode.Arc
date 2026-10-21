using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Text layouts of the cells drawn in one frame, kept for the next frame: entries used in a frame survive, the rest are
/// disposed at the start of the frame after (two generations, no allocation once the view is steady).
/// </summary>
/// <remarks>
/// Layouts are <see cref="TextLayout"/>, drawn with <see cref="TextLayout.Draw"/>. Drawing a cached <c>FormattedText</c>
/// instead rebuilds its lines on every call: about 3 KB and 0.3 ms per Latin cell and 22 KB per cell with Chinese text
/// (font fallback), against about 0.1 KB for a cached <c>TextLayout</c> (R11 in the roadmap).
/// </remarks>
internal sealed class DataGridTextCache
{
    private readonly record struct Key(object Owner, int Slot);

    internal sealed class Entry
    {
        public int Version;
        public string Text = string.Empty;
        public TextLayout? Layout;
        public Typeface Typeface;
        public double Size;
        public IBrush? Brush;
        public bool Tabular;
        public double MaxWidth = -1;
        public int Style;
    }

    private Dictionary<Key, Entry> _current = new();
    private Dictionary<Key, Entry> _previous = new();

    public int Count => _current.Count;

    public void BeginFrame()
    {
        (_previous, _current) = (_current, _previous);
        Release(_current);
        _current.Clear();
    }

    public void Clear()
    {
        Release(_current);
        Release(_previous);
        _current.Clear();
        _previous.Clear();
    }

    private static void Release(Dictionary<Key, Entry> entries)
    {
        foreach (var entry in entries.Values)
        {
            entry.Layout?.Dispose();
            entry.Layout = null;
        }
    }

    /// <summary>
    /// The text of a cell: reused while <paramref name="version"/> and <paramref name="style"/> are unchanged, else rebuilt
    /// from <paramref name="text"/> (a static function of <paramref name="state"/>, so drawing does not allocate). The
    /// layout is one line, trimmed with an ellipsis at <paramref name="maxWidth"/>, and is only valid until the frame after
    /// the next one. With <paramref name="anyBrush"/> an existing layout is returned whatever its brush (for measuring).
    /// </summary>
    public TextLayout? Get<TState>(object owner, int slot, int version, int style, TState state, Func<TState, string> text,
                                   Typeface typeface, double size, IBrush brush, bool tabular, double maxWidth, bool anyBrush = false)
    {
        var key = new Key(owner, slot);
        if (!_current.TryGetValue(key, out var entry))
        {
            if (_previous.Remove(key, out entry))
            {
                _current[key] = entry;
            }
            else
            {
                entry = new Entry { Version = int.MinValue };
                _current[key] = entry;
            }
        }
        var width = Math.Max(1, maxWidth);
        var stale = entry.Version != version || entry.Style != style;
        if (stale)
        {
            entry.Version = version;
            entry.Style = style;
            entry.Text = text(state);
        }
        if (entry.Text.Length == 0)
        {
            entry.Layout?.Dispose();
            entry.Layout = null;
            return null;
        }
        if (stale || entry.Layout is null || Math.Abs(entry.MaxWidth - width) > 0.5 || entry.Size != size || !anyBrush && !ReferenceEquals(entry.Brush, brush) ||
            entry.Typeface != typeface || entry.Tabular != tabular)
        {
            entry.Layout?.Dispose();
            entry.Typeface = typeface;
            entry.Size = size;
            entry.Brush = brush;
            entry.Tabular = tabular;
            entry.MaxWidth = width;
            entry.Layout = new TextLayout(entry.Text, typeface, size, brush, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis,
                textDecorations: null, FlowDirection.LeftToRight, width, double.PositiveInfinity, double.NaN, 0, 1,
                tabular ? DataGridPalette.TabularNumbers : null);
        }
        return entry.Layout;
    }

    /// <summary>
    /// The text of a cell without a layout (for cells a renderer draws): reused while <paramref name="version"/> is unchanged,
    /// else rebuilt from <paramref name="text"/>.
    /// </summary>
    public string StringOf<TState>(object owner, int slot, int version, TState state, Func<TState, string> text)
    {
        var key = new Key(owner, slot);
        if (!_current.TryGetValue(key, out var entry))
        {
            if (_previous.Remove(key, out entry))
            {
                _current[key] = entry;
            }
            else
            {
                entry = new Entry { Version = int.MinValue };
                _current[key] = entry;
            }
        }
        if (entry.Version != version)
        {
            entry.Version = version;
            entry.Text = text(state);
            entry.Layout?.Dispose();
            entry.Layout = null;
        }
        return entry.Text;
    }

    /// <summary>The text last built for a cell this frame (accessible names, truncation checks).</summary>
    public string? TextOf(object owner, int slot) => _current.TryGetValue(new Key(owner, slot), out var entry) ? entry.Text : null;
}
