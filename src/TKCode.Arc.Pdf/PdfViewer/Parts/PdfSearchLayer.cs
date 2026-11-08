using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Search highlights: every result in one color, the current one in another. Draws from the results, which keep their own rectangles, so a frame allocates nothing.</summary>
internal sealed class PdfSearchLayer : PageLayer
{
    private readonly PdfViewer _viewer;

    public PdfSearchLayer(PdfViewer viewer)
    {
        _viewer = viewer;
    }

    protected override void Render(DrawingContext context, in PageLayerContext page)
    {
        var results = _viewer.SearchResults;
        if (results.Count == 0 || !results.TryFindPage(page.PageIndex, out var list, out var first))
        {
            return;
        }
        var current = _viewer.CurrentMatch;
        var bounds = page.PageBounds;
        var scale = page.Scale;
        for (var i = first; i < list.Count && list[i].PageIndex == page.PageIndex; i++)
        {
            var match = list[i];
            var brush = ReferenceEquals(match, current) ? _viewer.SearchHighlightCurrentBrush : _viewer.SearchHighlightBrush;
            if (brush is null)
            {
                continue;
            }
            foreach (var rect in match.Rects)
            {
                context.FillRectangle(brush, new Rect(bounds.X + rect.X * scale, bounds.Y + rect.Y * scale, rect.Width * scale, rect.Height * scale));
            }
        }
    }
}
