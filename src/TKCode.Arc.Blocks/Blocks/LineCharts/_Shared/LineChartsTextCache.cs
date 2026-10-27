using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// One-line texts laid out once and drawn with any brush, for the self-drawn parts of the Line charts blocks. Texts are
/// keyed by text, size and weight; the cache starts over when it holds <see cref="Capacity"/> texts or when
/// <see cref="Clear"/> is called (a theme or font change). Drawing pushes glyph runs directly, so redrawing a cached
/// text every frame allocates nothing.
/// </summary>
internal sealed class LineChartsTextCache
{
    private readonly Dictionary<(string Text, double Size, FontWeight Weight), Entry> _texts = new();
    private readonly List<Entry> _dropped = new();
    private FontFamily _family = FontFamily.Default;

    public LineChartsTextCache(int capacity = 512)
    {
        Capacity = Math.Max(16, capacity);
    }

    /// <summary>Most texts kept before the cache starts over.</summary>
    public int Capacity { get; }

    /// <summary>Cached texts (tests).</summary>
    public int Count => _texts.Count;

    /// <summary>The font family; changing it clears the cache.</summary>
    public FontFamily Family
    {
        get => _family;
        set
        {
            if (Equals(_family, value))
            {
                return;
            }
            _family = value;
            Clear();
        }
    }

    /// <summary>The laid-out text (an empty string gives an empty layout).</summary>
    public Entry Get(string text, double size, FontWeight weight = FontWeight.Normal)
    {
        var key = (text, size, weight);
        if (_texts.TryGetValue(key, out var entry))
        {
            return entry;
        }
        if (_texts.Count >= Capacity)
        {
            Drop();
        }
        entry = new Entry(new TextLayout(text, new Typeface(_family, FontStyle.Normal, weight), size, Brushes.Black));
        _texts[key] = entry;
        return entry;
    }

    /// <summary>Forgets every text (their layouts are disposed the next time the cache drops texts).</summary>
    public void Clear() => Drop();

    private void Drop()
    {
        foreach (var old in _dropped)
        {
            old.Layout.Dispose();
        }
        _dropped.Clear();
        // Texts fetched earlier in the current frame may still be drawn, so they are disposed one drop later.
        _dropped.AddRange(_texts.Values);
        _texts.Clear();
    }

    /// <summary>A cached one-line text.</summary>
    internal sealed class Entry
    {
        public Entry(TextLayout layout)
        {
            Layout = layout;
            var lines = layout.TextLines;
            Line = lines.Count > 0 ? lines[0] : null;
            Width = Line?.WidthIncludingTrailingWhitespace ?? 0;
            Height = layout.Height;
            Baseline = Line?.Baseline ?? layout.Height * 0.8;
        }

        public TextLayout Layout { get; }

        private TextLine? Line { get; }

        public double Width { get; }

        public double Height { get; }

        public double Baseline { get; }

        /// <summary>Draws the text with its top-left corner at <paramref name="origin"/>.</summary>
        public void Draw(DrawingContext context, IBrush brush, Point origin)
        {
            if (Line is not { } line)
            {
                return;
            }
            var x = origin.X + line.Start;
            var runs = line.TextRuns;
            for (var r = 0; r < runs.Count; r++)
            {
                if (runs[r] is not DrawableTextRun drawable)
                {
                    continue;
                }
                if (drawable is ShapedTextRun shaped && shaped.GlyphRun.GlyphInfos.Count > 0)
                {
                    using (context.PushTransform(Matrix.CreateTranslation(x, origin.Y + (line.Baseline - shaped.Baseline))))
                    {
                        context.DrawGlyphRun(brush, shaped.GlyphRun);
                    }
                }
                x += drawable.Size.Width;
            }
        }

        /// <summary>Draws the text vertically centered on <paramref name="centerY"/>, starting at <paramref name="x"/>.</summary>
        public void DrawCentered(DrawingContext context, IBrush brush, double x, double centerY) =>
            Draw(context, brush, new Point(x, centerY - Height / 2));
    }
}
