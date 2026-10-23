using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One drawn prompt bar of a <see cref="PromptCache"/>: the prefix segments on a square root scale with 2px gaps, and in
/// a request row a slot for the new message after one more gap. Every segment has a muted track; over it a processed
/// (solid) fill grows from the left, a cached fill (light tint with a 1px inner outline) fades in, a band passes over the
/// cached prefix, and a partial hit raises a 2×20 warning line in the gap before the first changed segment. The owner
/// writes the state for the current story time and calls <see cref="Update"/>; the bar draws it and redraws only when
/// something changed. The same layout feeds the flight overlay, so the row, the flight and the cache panel line up.
/// </summary>
internal sealed class PromptCacheBar : Control
{
    /// <summary>Segment and gap geometry (px).</summary>
    public const double Gap = 2;

    public const double Radius = 3;

    public const double MarkWidth = 2;

    public const double MarkOverhang = 5;

    private Rect[] _segments = [];
    private Rect _message;
    private double _signature = double.NaN;

    public PromptCacheBar()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        Focusable = false;
    }

    /// <summary>The bar height (10).</summary>
    public double BarHeight { get; set; } = 10;

    /// <summary>The square root growth of each prefix segment.</summary>
    public double[] Grows { get; private set; } = [];

    /// <summary>The growth of the message slot (0 in the cache panel: no slot).</summary>
    public double MessageGrow { get; private set; }

    /// <summary>The palette (set by the owner; null draws nothing).</summary>
    public PromptCachePalette? Palette { get; set; }

    /// <summary>Per segment: the processed fill's progress (0..1, from the left).</summary>
    public double[] Processed { get; private set; } = [];

    /// <summary>Per segment: the cached fill's opacity (0..1).</summary>
    public double[] Cached { get; private set; } = [];

    /// <summary>The message fill's progress (0..1).</summary>
    public double Message { get; set; }

    /// <summary>The band's left edge as a share of the prefix width (from −<see cref="PromptCacheTimeline.BandWidth"/> to 1), NaN for none.</summary>
    public double Band { get; set; } = double.NaN;

    /// <summary>The band shows on segments before this index.</summary>
    public int BandTo { get; set; }

    /// <summary>The break marker's progress (0..1), NaN for none; it stands before segment <see cref="MarkIndex"/>.</summary>
    public double Mark { get; set; } = double.NaN;

    public int MarkIndex { get; set; }

    /// <summary>The opacity of everything over the tracks (a new round fades the old fills).</summary>
    public double ContentOpacity { get; set; } = 1;

    /// <summary>The laid out segments (local coordinates).</summary>
    public ReadOnlySpan<Rect> Segments => _segments;

    /// <summary>Sets the shape: the segments' growth and the message slot's (0 for none). Clears the state.</summary>
    public void SetShape(double[] grows, double messageGrow)
    {
        Grows = grows;
        MessageGrow = messageGrow;
        _segments = new Rect[grows.Length];
        Processed = new double[grows.Length];
        Cached = new double[grows.Length];
        Message = 0;
        Band = double.NaN;
        Mark = double.NaN;
        ContentOpacity = 1;
        _signature = double.NaN;
        InvalidateArrange();
        InvalidateVisual();
    }

    /// <summary>Clears every fill (an empty track).</summary>
    public void Clear()
    {
        Array.Clear(Processed);
        Array.Clear(Cached);
        Message = 0;
        Band = double.NaN;
        Mark = double.NaN;
        ContentOpacity = 1;
    }

    /// <summary>Redraws when the state changed since the last call.</summary>
    public void Update()
    {
        var s = Message * 3.1 + ContentOpacity * 7.7 + (double.IsNaN(Band) ? -11 : Band * 13.3 + BandTo) +
                (double.IsNaN(Mark) ? -17 : Mark * 19.1 + MarkIndex);
        for (var i = 0; i < Processed.Length; i++)
        {
            s += Processed[i] * (23.3 + i) + Cached[i] * (29.9 + 2 * i);
        }
        if (s.Equals(_signature))
        {
            return;
        }
        _signature = s;
        InvalidateVisual();
    }

    /// <summary>Forces a redraw (palette or layout changed).</summary>
    public void Refresh()
    {
        _signature = double.NaN;
        InvalidateVisual();
    }

    /// <summary>The rectangle of segments <paramref name="from"/>..<paramref name="to"/> (exclusive) in local coordinates.</summary>
    public Rect Span(int from, int to)
    {
        if (_segments.Length == 0 || from >= to || from >= _segments.Length)
        {
            return default;
        }
        var a = _segments[Math.Clamp(from, 0, _segments.Length - 1)];
        var b = _segments[Math.Clamp(to - 1, 0, _segments.Length - 1)];
        return new Rect(a.X, a.Y, Math.Max(0, b.Right - a.X), a.Height);
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, BarHeight);

    protected override Size ArrangeOverride(Size finalSize)
    {
        _message = PromptCacheTimeline.Layout(Grows, 0, Grows.Length, MessageGrow, 0, 0, finalSize.Width, BarHeight, Gap, _segments);
        _signature = double.NaN;
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var palette = Palette;
        if (palette is null || _segments.Length == 0 && MessageGrow <= 0)
        {
            return;
        }
        for (var i = 0; i < _segments.Length; i++)
        {
            var r = _segments[i];
            if (r.Width <= 0)
            {
                continue;
            }
            context.DrawRectangle(palette.Track, null, r, Radius, Radius);
        }
        if (MessageGrow > 0 && _message.Width > 0)
        {
            context.DrawRectangle(palette.Track, null, _message, Radius, Radius);
        }
        using (context.PushOpacity(Math.Clamp(ContentOpacity, 0, 1)))
        {
            DrawFills(context, palette);
        }
        DrawMark(context, palette);
    }

    private void DrawFills(DrawingContext context, PromptCachePalette palette)
    {
        var prefix = Span(0, _segments.Length);
        var bandWidth = prefix.Width * PromptCacheTimeline.BandWidth;
        for (var i = 0; i < _segments.Length; i++)
        {
            var r = _segments[i];
            if (r.Width <= 0)
            {
                continue;
            }
            var cached = Math.Clamp(Cached[i], 0, 1);
            if (cached > 0.001)
            {
                using (context.PushOpacity(cached))
                {
                    context.DrawRectangle(palette.CachedOf(i), null, r, Radius, Radius);
                    if (palette.OutlineOf(i) is { } pen && r.Width > 1)
                    {
                        context.DrawRectangle(null, pen, r.Deflate(0.5), Radius - 0.5, Radius - 0.5);
                    }
                }
            }
            var processed = Math.Clamp(Processed[i], 0, 1);
            if (processed > 0.001)
            {
                var w = r.Width * processed;
                var radius = Math.Min(Radius, w / 2);
                context.DrawRectangle(palette.SolidOf(i), null, new Rect(r.X, r.Y, w, r.Height), radius, radius);
            }
            if (!double.IsNaN(Band) && i < BandTo && bandWidth > 0)
            {
                var left = prefix.X + Band * prefix.Width;
                var band = new Rect(left, r.Y, bandWidth, r.Height);
                if (band.Intersects(r))
                {
                    using (context.PushClip(new RoundedRect(r, Radius)))
                    {
                        context.DrawRectangle(palette.BandOf(i), null, band);
                    }
                }
            }
        }
        if (MessageGrow > 0 && _message.Width > 0 && Message > 0.001)
        {
            var w = _message.Width * Math.Clamp(Message, 0, 1);
            var radius = Math.Min(Radius, w / 2);
            context.DrawRectangle(palette.Message, null, new Rect(_message.X, _message.Y, w, _message.Height), radius, radius);
        }
    }

    private void DrawMark(DrawingContext context, PromptCachePalette palette)
    {
        if (double.IsNaN(Mark) || MarkIndex <= 0 || MarkIndex >= _segments.Length)
        {
            return;
        }
        var p = Math.Clamp(Mark, 0, 1);
        if (p <= 0.001)
        {
            return;
        }
        var x = _segments[MarkIndex].X - MarkWidth;
        var full = BarHeight + 2 * MarkOverhang;
        var h = full * p;
        var y = -MarkOverhang + (full - h) / 2;
        using (context.PushOpacity(p))
        {
            context.DrawRectangle(palette.Warning, null, new Rect(x, y, MarkWidth, h), 1, 1);
        }
    }
}
