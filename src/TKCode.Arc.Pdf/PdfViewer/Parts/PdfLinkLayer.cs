using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The links of the pages: read when a page is drawn, hand cursor and tint under the pointer, and a click that did not drag
/// activates. It never handles the pointer events: a press on a link may still become a text selection, which is the next
/// layer's to decide, and that layer must see the release too.
/// </summary>
internal sealed class PdfLinkLayer : PageLayer
{
    /// <summary>How far the pointer may travel, in viewer DIPs, for a press and release to remain a click.</summary>
    internal const double ClickSlop = 4;

    /// <summary>Requests waiting at most; while scrolling fast, pages that went by are forgotten.</summary>
    private const int MaxPending = 16;

    /// <summary>Pages whose links are remembered.</summary>
    private const int MaxKnownPages = 128;

    private static Cursor? s_hand;

    private readonly PdfViewer _viewer;
    private readonly Dictionary<int, IReadOnlyList<PdfLink>> _known = new();
    private readonly Queue<int> _knownOrder = new();
    private readonly List<int> _pending = new();
    private PdfDocument? _document;
    private bool _loading;
    private bool _loadPosted;
    private PdfLink? _pressed;
    private int _pressedPage;
    private Point _pressedPoint;
    private PdfLink? _hovered;
    private int _hoveredPage;

    public PdfLinkLayer(PdfViewer viewer)
    {
        _viewer = viewer;
    }

    /// <summary>A press is on a link and may still turn out a click.</summary>
    public bool IsPressingLink => _pressed is not null;

    /// <summary>The link under the pointer.</summary>
    public PdfLink? Hovered => _hovered;

    /// <summary>The pages changed: what is known about them is gone.</summary>
    public void Reset(PdfDocument? document)
    {
        _document = document;
        _known.Clear();
        _knownOrder.Clear();
        _pending.Clear();
        _pressed = null;
        ClearHover();
    }

    /// <summary>The links of a page if they have arrived.</summary>
    public IReadOnlyList<PdfLink>? TryGetLinks(int pageIndex) => _known.GetValueOrDefault(pageIndex);

    /// <summary>The pointer went away, or the page moved under it.</summary>
    public void ClearHover()
    {
        if (_hovered is not null)
        {
            _hovered = null;
            _viewer.OnLinkHoverChanged(null, 0, default);
            Invalidate();
        }
    }

    protected override void Render(DrawingContext context, in PageLayerContext page)
    {
        if (_document is not null && !_known.ContainsKey(page.PageIndex))
        {
            Want(page.PageIndex);
        }
        if (_hovered is { } link && _hoveredPage == page.PageIndex && _viewer.LinkHoverBrush is { } brush)
        {
            var (bounds, scale) = (page.PageBounds, page.Scale);
            var area = link.Bounds;
            context.FillRectangle(brush, new Rect(bounds.X + area.X * scale, bounds.Y + area.Y * scale, area.Width * scale, area.Height * scale));
        }
    }

    protected override void OnPointerPressed(PageLayerPointerArgs e)
    {
        _pressed = null;
        // A second click of a double click selects a word; the first one has already done its work. Shift extends a selection.
        if (_viewer.InteractionMode == PageInteractionMode.Pan || e.ClickCount > 1 || (e.Modifiers & KeyModifiers.Shift) != 0
            || LinkAt(e.PageIndex, e.PointInPage) is not { } link)
        {
            return;
        }
        _pressed = link;
        _pressedPage = e.PageIndex;
        _pressedPoint = e.PointInPage;
    }

    protected override void OnPointerMoved(PageLayerPointerArgs e)
    {
        if (_pressed is not null && (e.PageIndex != _pressedPage || Distance(e.PointInPage, _pressedPoint) * _viewer.Zoom > ClickSlop))
        {
            _pressed = null;
        }
        if (_viewer.InteractionMode == PageInteractionMode.Pan || _viewer.SelectionLayer.IsDragging)
        {
            ClearHover();
            return;
        }
        var link = LinkAt(e.PageIndex, e.PointInPage);
        if (!ReferenceEquals(link, _hovered))
        {
            _hovered = link;
            _hoveredPage = e.PageIndex;
            _viewer.OnLinkHoverChanged(link, e.PageIndex, e.PointInPage);
            Invalidate();
        }
    }

    protected override void OnPointerReleased(PageLayerPointerArgs e)
    {
        var link = _pressed;
        _pressed = null;
        if (link is not null && e.PageIndex == _pressedPage && link.Bounds.Contains(e.PointInPage))
        {
            _viewer.ActivateLink(link);
        }
    }

    protected override void OnPointerDragCancelled() => _pressed = null;

    protected override Cursor? GetCursor(int pageIndex, Point pointInPage)
    {
        if (_viewer.SelectionLayer.IsDragging || LinkAt(pageIndex, pointInPage) is null)
        {
            return null;
        }
        return s_hand ??= new Cursor(StandardCursorType.Hand);
    }

    private PdfLink? LinkAt(int pageIndex, Point point)
    {
        if (!_known.TryGetValue(pageIndex, out var links))
        {
            return null;
        }
        // The later one is on top.
        for (var i = links.Count - 1; i >= 0; i--)
        {
            if (links[i].Bounds.Contains(point))
            {
                return links[i];
            }
        }
        return null;
    }

    private static double Distance(Point a, Point b)
    {
        var delta = a - b;
        return Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
    }

    /// <summary>Called from a render pass: only notes the wish; asking may finish inline and invalidate.</summary>
    private void Want(int pageIndex)
    {
        if (_pending.Contains(pageIndex))
        {
            return;
        }
        if (_pending.Count >= MaxPending)
        {
            _pending.RemoveAt(0);
        }
        _pending.Add(pageIndex);
        if (!_loadPosted && !_loading)
        {
            _loadPosted = true;
            Dispatcher.UIThread.Post(LoadNext, DispatcherPriority.Background);
        }
    }

    /// <summary>One page at a time, the page drawn last first: that is the one on screen now.</summary>
    private async void LoadNext()
    {
        _loadPosted = false;
        if (_loading || _pending.Count == 0 || _document is not { } document)
        {
            return;
        }
        var page = _pending[^1];
        _pending.RemoveAt(_pending.Count - 1);
        _loading = true;
        IReadOnlyList<PdfLink>? links;
        try
        {
            links = await document.GetLinksAsync(page, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            links = null;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A page whose links cannot be read has none.
            links = [];
        }
        _loading = false;
        if (ReferenceEquals(document, _document) && links is not null)
        {
            Remember(page, links);
            if (_pending.Count > 0)
            {
                LoadNext();
            }
        }
        else if (_pending.Count > 0)
        {
            LoadNext();
        }
    }

    private void Remember(int page, IReadOnlyList<PdfLink> links)
    {
        _known[page] = links;
        _knownOrder.Enqueue(page);
        while (_knownOrder.Count > MaxKnownPages)
        {
            _known.Remove(_knownOrder.Dequeue());
        }
    }
}
