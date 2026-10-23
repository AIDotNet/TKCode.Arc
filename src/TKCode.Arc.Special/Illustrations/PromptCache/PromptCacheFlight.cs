using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The card-wide layer that draws the flight of a <see cref="PromptCache"/>: the written part of a prompt lifts out of its
/// row and travels into the cache panel's bar, its width easing from the row's segments to the panel's, its segments
/// keeping their square root proportions, and its solid processed color fading to the light cached tint on the way. It
/// takes no input and draws nothing between flights.
/// </summary>
internal sealed class PromptCacheFlight : Control
{
    private readonly Rect[] _scratch = new Rect[32];

    public PromptCacheFlight()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = false;
    }

    /// <summary>The row bar the flight leaves from and the panel bar it lands in.</summary>
    public PromptCacheBar? From { get; set; }

    public PromptCacheBar? To { get; set; }

    /// <summary>The first segment that flies (all segments after it fly too).</summary>
    public int FirstSegment { get; set; }

    /// <summary>The eased progress 0..1, or NaN when nothing flies.</summary>
    public double Progress { get; set; } = double.NaN;

    public PromptCachePalette? Palette { get; set; }

    public override void Render(DrawingContext context)
    {
        if (double.IsNaN(Progress) || From is not { } from || To is not { } to || Palette is not { } palette)
        {
            return;
        }
        var count = from.Grows.Length;
        var first = Math.Clamp(FirstSegment, 0, count);
        if (first >= count || count - first > _scratch.Length)
        {
            return;
        }
        var a = from.Span(first, count);
        var b = to.Span(first, count);
        var pa = from.TranslatePoint(a.TopLeft, this);
        var pb = to.TranslatePoint(b.TopLeft, this);
        if (pa is not { } start || pb is not { } end || a.Width <= 0)
        {
            return;
        }
        var e = Math.Clamp(Progress, 0, 1);
        var x = start.X + (end.X - start.X) * e;
        var y = start.Y + (end.Y - start.Y) * e;
        var w = a.Width + (b.Width - a.Width) * e;
        var h = a.Height;
        PromptCacheTimeline.Layout(from.Grows, first, count, 0, x, y, w, h, PromptCacheBar.Gap, _scratch);
        for (var i = first; i < count; i++)
        {
            var r = _scratch[i - first];
            if (r.Width <= 0)
            {
                continue;
            }
            context.DrawRectangle(palette.CachedOf(i), null, r, PromptCacheBar.Radius, PromptCacheBar.Radius);
            if (palette.OutlineOf(i) is { } pen && r.Width > 1)
            {
                context.DrawRectangle(null, pen, r.Deflate(0.5), PromptCacheBar.Radius - 0.5, PromptCacheBar.Radius - 0.5);
            }
            if (e < 0.999)
            {
                using (context.PushOpacity(1 - e))
                {
                    context.DrawRectangle(palette.SolidOf(i), null, r, PromptCacheBar.Radius, PromptCacheBar.Radius);
                }
            }
        }
    }
}
