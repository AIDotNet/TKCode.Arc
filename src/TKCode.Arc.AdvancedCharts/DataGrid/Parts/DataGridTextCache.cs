using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Formatted text of the cells drawn in one frame, kept for the next frame: entries used in a frame survive, the rest are
/// dropped at the start of the frame after (two generations, no allocation once the view is steady).
/// </summary>
internal sealed class DataGridTextCache
{
    private readonly record struct Key(object Owner, int Slot);

    internal sealed class Entry
    {
        public int Version;
        public string Text = string.Empty;
        public FormattedText? Layout;
        public double MaxWidth = -1;
        public int Style;
    }

    private Dictionary<Key, Entry> _current = new();
    private Dictionary<Key, Entry> _previous = new();

    public int Count => _current.Count;

    public void BeginFrame()
    {
        (_previous, _current) = (_current, _previous);
        _current.Clear();
    }

    public void Clear()
    {
        _current.Clear();
        _previous.Clear();
    }

    /// <summary>
    /// The text of a cell: reused while <paramref name="version"/> and <paramref name="style"/> are unchanged, else rebuilt
    /// from <paramref name="text"/> (a static function of <paramref name="state"/>, so drawing does not allocate).
    /// </summary>
    public FormattedText? Get<TState>(object owner, int slot, int version, int style, TState state, Func<TState, string> text,
                                      Typeface typeface, double size, IBrush brush, bool tabular, double maxWidth)
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
        if (entry.Version != version || entry.Style != style || entry.Layout is null)
        {
            entry.Version = version;
            entry.Style = style;
            entry.Text = text(state);
            entry.MaxWidth = -1;
            if (entry.Text.Length == 0)
            {
                entry.Layout = null;
                return null;
            }
            var layout = new FormattedText(entry.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush)
            {
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis
            };
            if (tabular)
            {
                layout.SetFontFeatures(DataGridPalette.TabularNumbers);
            }
            entry.Layout = layout;
        }
        if (entry.Layout is { } formatted)
        {
            var width = Math.Max(1, maxWidth);
            if (Math.Abs(entry.MaxWidth - width) > 0.5)
            {
                entry.MaxWidth = width;
                formatted.MaxTextWidth = width;
            }
        }
        return entry.Layout;
    }

    /// <summary>The text last built for a cell this frame (accessible names, truncation checks).</summary>
    public string? TextOf(object owner, int slot) => _current.TryGetValue(new Key(owner, slot), out var entry) ? entry.Text : null;
}
