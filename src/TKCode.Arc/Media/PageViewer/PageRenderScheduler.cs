using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>What the viewer shows at the moment, in the units <see cref="PageRenderScheduler"/> plans with.</summary>
/// <param name="First">First visible page.</param>
/// <param name="Last">Last visible page (below <paramref name="First"/> when none).</param>
/// <param name="Zoom">The zoom on screen.</param>
/// <param name="Scale">Device pixels per DIP.</param>
/// <param name="Viewport">The viewport in content coordinates.</param>
internal readonly record struct PageViewState(int First, int Last, double Zoom, double Scale, Rect Viewport);

/// <summary>A rendered bitmap and what it was rendered for.</summary>
internal sealed class PageBitmapEntry
{
    public PageBitmapEntry(int page, PixelSize targetSize, PixelRect? region, Bitmap bitmap, bool ownsBitmap)
    {
        Page = page;
        TargetSize = targetSize;
        Region = region;
        Bitmap = bitmap;
        OwnsBitmap = ownsBitmap;
        // A bitmap the viewer does not own costs nothing to keep and frees nothing when dropped.
        Bytes = ownsBitmap ? (long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4 : 0;
    }

    public int Page { get; }

    public PixelSize TargetSize { get; }

    public PixelRect? Region { get; }

    public Bitmap Bitmap { get; }

    public bool OwnsBitmap { get; }

    public long Bytes { get; }

    public LinkedListNode<PageBitmapEntry>? Node { get; set; }
}

/// <summary>
/// Turns what is on screen into <see cref="IPageSource.RenderAsync"/> calls and keeps the results (spec section 4): the
/// visible pages ± 1 are the target, nearest to the viewport center first; nothing is requested while the zoom moves (the
/// bitmaps at hand stretch) until it rests for <see cref="SettleDelay"/>; a page over <see cref="MaxBasePixels"/> gets a
/// capped base bitmap plus a viewport region once scrolling rests as well; requests that leave the target are cancelled;
/// a page has one request running at a time; the bitmaps are an LRU within a byte budget that never evicts what a visible
/// page draws, and are disposed after the next frame. Used on the UI thread only.
/// </summary>
internal sealed class PageRenderScheduler
{
    /// <summary>A whole-page bitmap is capped at this many pixels.</summary>
    public const long MaxBasePixels = 16_000_000;

    /// <summary>Requests running at once, over all pages.</summary>
    private const int MaxRunning = 4;

    /// <summary>Queue order: details after every base, auxiliary requests after everything on screen.</summary>
    private const double DetailRank = 1e9;

    private const double AuxiliaryRank = 1e12;

    /// <summary>The detail region grows by this share of the viewport on each side.</summary>
    private const double RegionMargin = 0.25;

    private const int RegionGrid = 64;

    private readonly record struct Key(int Page, PixelSize Size, PixelRect? Region);

    private sealed class Request
    {
        public required Key Key { get; init; }

        public required bool IsAuxiliary { get; init; }

        public double Rank { get; set; }

        public int Generation { get; init; }

        public CancellationTokenSource? Cancellation { get; set; }
    }

    private sealed class PageState
    {
        public readonly List<PageBitmapEntry> Bases = new(2);
        public PageBitmapEntry? Detail;
        public PageBitmapEntry? BestBase;
        public PageBitmapEntry? BestDetail;
        public Request? Running;
    }

    private readonly PageLayoutEngine _layout;
    private readonly Dictionary<int, PageState> _pages = new();
    private readonly Dictionary<Key, PageBitmapEntry> _cache = new();
    private readonly LinkedList<PageBitmapEntry> _lru = new();
    private readonly HashSet<Key> _finishedEmpty = new();
    private readonly List<Request> _queue = new();
    private readonly List<Request> _running = new();
    private readonly HashSet<Key> _wanted = new();
    private readonly List<Bitmap> _toDispose = new();
    private IPageSource? _source;
    private DispatcherTimer? _settleTimer;
    private PageViewState _view;
    private bool _hasView;
    private bool _zoomSettling;
    private bool _scrollSettling;
    private bool _pumping;
    private bool _disposePosted;
    private bool _isLoading;
    private int _generation;
    private long _bytes;
    private long _budget = 192L * 1024 * 1024;

    public PageRenderScheduler(PageLayoutEngine layout)
    {
        _layout = layout;
    }

    /// <summary>How long the zoom and the scroll must rest before sharper bitmaps are asked for (spec: 120 ms).</summary>
    public TimeSpan SettleDelay { get; set; } = TimeSpan.FromMilliseconds(120);

    /// <summary>A bitmap arrived or was dropped: redraw.</summary>
    public event Action? Changed;

    /// <summary><see cref="IsLoading"/> changed.</summary>
    public event Action? LoadingChanged;

    /// <summary>A bitmap asked for with <see cref="GetAuxiliaryBitmap"/> finished (page, target size).</summary>
    public event Action<int, PixelSize>? AuxiliaryReady;

    /// <summary>A visible page has neither a bitmap nor a finished first request.</summary>
    public bool IsLoading => _isLoading;

    /// <summary>Bytes of owned bitmaps in the cache.</summary>
    public long CachedBytes => _bytes;

    /// <summary>Bitmaps in the cache.</summary>
    public int CachedCount => _cache.Count;

    /// <summary>Requests the source is working on.</summary>
    public int RunningCount => _running.Count;

    /// <summary>Requests waiting to start.</summary>
    public int QueuedCount => _queue.Count;

    /// <summary>The byte budget (<see cref="PageViewer.RenderCacheBytes"/>).</summary>
    public long Budget
    {
        get => _budget;
        set
        {
            _budget = Math.Max(0, value);
            Trim();
        }
    }

    /// <summary>The whole-page pixel size at a zoom: page DIPs × zoom × scale.</summary>
    public static PixelSize FullSize(Size page, double zoom, double scale) => new(
        Math.Max(1, (int)Math.Round(page.Width * zoom * scale)),
        Math.Max(1, (int)Math.Round(page.Height * zoom * scale)));

    /// <summary>The base bitmap's size: <paramref name="full"/>, scaled to <see cref="MaxBasePixels"/> when it is larger.</summary>
    public static PixelSize CapSize(PixelSize full)
    {
        var pixels = (double)full.Width * full.Height;
        if (pixels <= MaxBasePixels)
        {
            return full;
        }
        var k = Math.Sqrt(MaxBasePixels / pixels);
        return new PixelSize(Math.Max(1, (int)(full.Width * k)), Math.Max(1, (int)(full.Height * k)));
    }

    /// <summary>Replaces the source; everything from the old one is cancelled and dropped.</summary>
    public void SetSource(IPageSource? source)
    {
        _source = source;
        Reset();
    }

    /// <summary>Cancels all work and drops every bitmap (the pages changed, or the viewer left the tree).</summary>
    public void Reset()
    {
        _generation++;
        _settleTimer?.Stop();
        _zoomSettling = false;
        _scrollSettling = false;
        _hasView = false;
        _queue.Clear();
        CancelRunning(static _ => true);
        foreach (var entry in _cache.Values)
        {
            ScheduleDispose(entry);
        }
        _cache.Clear();
        _lru.Clear();
        _bytes = 0;
        _finishedEmpty.Clear();
        foreach (var page in _pages.Values)
        {
            page.Bases.Clear();
            page.Detail = null;
            page.BestBase = null;
            page.BestDetail = null;
        }
        SetLoading(false);
    }

    /// <summary>Takes the new view and starts or cancels requests accordingly.</summary>
    public void Update(in PageViewState view)
    {
        var zoomMoved = _hasView && (view.Zoom != _view.Zoom || view.Scale != _view.Scale);
        var scrolled = _hasView && view.Viewport != _view.Viewport;
        _view = view;
        _hasView = true;
        if (zoomMoved)
        {
            _zoomSettling = true;
            RestartSettle();
        }
        else if (scrolled && NeedsDetail())
        {
            _scrollSettling = true;
            RestartSettle();
        }
        Refresh();
    }

    /// <summary>The bitmaps to draw for a page. Nothing here starts work; <see cref="Update"/> did that.</summary>
    public void GetBitmaps(int page, out PageBitmapEntry? baseEntry, out PageBitmapEntry? detail)
    {
        if (_pages.TryGetValue(page, out var state))
        {
            baseEntry = state.BestBase;
            detail = state.BestDetail;
        }
        else
        {
            baseEntry = null;
            detail = null;
        }
    }

    /// <summary>
    /// A bitmap of a page at any size for other views of the document (thumbnails): the cached one, or null after queueing
    /// it behind everything on screen. <see cref="AuxiliaryReady"/> says when it arrived; it shares the cache and its budget.
    /// </summary>
    public Bitmap? GetAuxiliaryBitmap(int page, PixelSize size)
    {
        var key = new Key(page, size, null);
        if (_cache.TryGetValue(key, out var entry))
        {
            Touch(entry);
            return entry.Bitmap;
        }
        if (_source is null || page < 0 || page >= _source.PageCount || _finishedEmpty.Contains(key) || IsScheduled(key))
        {
            return null;
        }
        _queue.Add(new Request { Key = key, IsAuxiliary = true, Rank = AuxiliaryRank, Generation = _generation });
        Pump();
        return null;
    }

    /// <summary>A bitmap of the shared cache, if it is there. Asks for nothing, so it is safe in a render pass.</summary>
    public Bitmap? PeekAuxiliaryBitmap(int page, PixelSize size)
    {
        if (_cache.TryGetValue(new Key(page, size, null), out var entry))
        {
            Touch(entry);
            return entry.Bitmap;
        }
        return null;
    }

    /// <summary>Takes one auxiliary request off the queue if it has not started (its thumbnail scrolled away). A running one finishes into the cache.</summary>
    public void DropQueuedAuxiliary(int page, PixelSize size)
    {
        var key = new Key(page, size, null);
        _queue.RemoveAll(request => request.IsAuxiliary && request.Key == key);
    }

    /// <summary>Forgets the queued and running auxiliary requests (the thumbnails were hidden).</summary>
    public void CancelAuxiliary()
    {
        _queue.RemoveAll(static request => request.IsAuxiliary);
        CancelRunning(static request => request.IsAuxiliary);
    }

    /// <summary>Over a copy: cancelling can finish a request inline, which takes it off the list.</summary>
    private void CancelRunning(Func<Request, bool> predicate)
    {
        if (_running.Count == 0)
        {
            return;
        }
        foreach (var request in _running.ToArray())
        {
            if (predicate(request))
            {
                request.Cancellation?.Cancel();
            }
        }
    }

    private void Refresh()
    {
        if (_source is null || !_hasView)
        {
            return;
        }
        CollectWanted();
        _queue.RemoveAll(request => !request.IsAuxiliary && !_wanted.Contains(request.Key));
        CancelRunning(request => !request.IsAuxiliary && !_wanted.Contains(request.Key));
        for (var page = _view.First; page <= _view.Last; page++)
        {
            ChooseBest(page);
        }
        Pump();
        Trim();
        UpdateLoading();
    }

    /// <summary>The requests the view wants that are not cached or finished, queued if they are new.</summary>
    private void CollectWanted()
    {
        _wanted.Clear();
        if (_zoomSettling || _layout.PageCount == 0 || _view.Last < _view.First)
        {
            return;
        }
        var first = Math.Max(0, _view.First - 1);
        var last = Math.Min(_layout.PageCount - 1, _view.Last + 1);
        // The pages on screen stay whatever the budget says; a neighbour is only worth asking for if it would stay too.
        var keptBytes = 0L;
        for (var page = _view.First; page <= _view.Last; page++)
        {
            var bytes = CapSize(FullSize(_layout.GetPageSize(page), _view.Zoom, _view.Scale));
            keptBytes += (long)bytes.Width * bytes.Height * 4;
        }
        for (var page = first; page <= last; page++)
        {
            var visible = page >= _view.First && page <= _view.Last;
            var full = FullSize(_layout.GetPageSize(page), _view.Zoom, _view.Scale);
            var capped = CapSize(full);
            if (!visible)
            {
                var bytes = (long)capped.Width * capped.Height * 4;
                if (keptBytes + bytes > _budget)
                {
                    continue;
                }
                keptBytes += bytes;
            }
            var key = new Key(page, capped, null);
            Want(key, Rank(page));
            // The detail goes on top of a base, and a base that already holds every pixel needs none.
            if (visible && capped != full && !_scrollSettling && _cache.ContainsKey(key) && !BaseCovers(key, full))
            {
                WantDetail(page, full);
            }
        }
    }

    private void Want(Key key, double rank)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            Touch(entry);
            return;
        }
        if (_finishedEmpty.Contains(key))
        {
            return;
        }
        _wanted.Add(key);
        foreach (var request in _queue)
        {
            if (request.Key == key)
            {
                request.Rank = rank;
                return;
            }
        }
        foreach (var request in _running)
        {
            if (request.Key == key)
            {
                return;
            }
        }
        _queue.Add(new Request { Key = key, IsAuxiliary = false, Rank = rank, Generation = _generation });
    }

    private void WantDetail(int page, PixelSize full)
    {
        if (ViewportRegion(page, full, RegionMargin) is not { } region || ViewportRegion(page, full, 0) is not { } needed)
        {
            return;
        }
        if (_pages.TryGetValue(page, out var state))
        {
            if (state.Detail is { Region: { } held } detail && detail.TargetSize == full && held.Contains(needed))
            {
                Touch(detail);
                return;
            }
            if (state.Running is { Key.Region: { } running } current && current.Key.Size == full && running.Contains(needed))
            {
                // Still the right part of the page: let it finish.
                _wanted.Add(current.Key);
                return;
            }
        }
        Want(new Key(page, full, region), DetailRank + Rank(page));
    }

    /// <summary>Whether the base bitmap already has every pixel of the full size (a large picture from an image source).</summary>
    private bool BaseCovers(Key key, PixelSize full) =>
        _cache.TryGetValue(key, out var entry)
        && entry.Bitmap.PixelSize.Width >= full.Width - 1
        && entry.Bitmap.PixelSize.Height >= full.Height - 1;

    /// <summary>The part of a page inside the viewport, grown by <paramref name="margin"/>, in the pixels of <paramref name="full"/>.</summary>
    private PixelRect? ViewportRegion(int page, PixelSize full, double margin)
    {
        var rect = _layout.GetPageRect(page, _view.Viewport.Size);
        var visible = rect.Intersect(_view.Viewport);
        if (visible.Width <= 0 || visible.Height <= 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }
        var kx = full.Width / rect.Width;
        var ky = full.Height / rect.Height;
        var left = (visible.X - rect.X - visible.Width * margin) * kx;
        var top = (visible.Y - rect.Y - visible.Height * margin) * ky;
        var right = (visible.Right - rect.X + visible.Width * margin) * kx;
        var bottom = (visible.Bottom - rect.Y + visible.Height * margin) * ky;
        // On a coarse grid, so that a region asked for twice is the same cache key.
        var x0 = margin > 0 ? (int)Math.Floor(left / RegionGrid) * RegionGrid : (int)Math.Floor(left);
        var y0 = margin > 0 ? (int)Math.Floor(top / RegionGrid) * RegionGrid : (int)Math.Floor(top);
        var x1 = margin > 0 ? (int)Math.Ceiling(right / RegionGrid) * RegionGrid : (int)Math.Ceiling(right);
        var y1 = margin > 0 ? (int)Math.Ceiling(bottom / RegionGrid) * RegionGrid : (int)Math.Ceiling(bottom);
        x0 = Math.Clamp(x0, 0, full.Width);
        y0 = Math.Clamp(y0, 0, full.Height);
        x1 = Math.Clamp(x1, x0, full.Width);
        y1 = Math.Clamp(y1, y0, full.Height);
        return x1 > x0 && y1 > y0 ? new PixelRect(x0, y0, x1 - x0, y1 - y0) : null;
    }

    private double Rank(int page)
    {
        if (_layout.Mode == PageLayoutMode.Single)
        {
            return Math.Abs(page - _layout.SingleIndex);
        }
        return Math.Abs(_layout.GetPageRect(page, _view.Viewport.Size).Center.Y - _view.Viewport.Center.Y);
    }

    private bool NeedsDetail()
    {
        for (var page = _view.First; page <= _view.Last; page++)
        {
            var full = FullSize(_layout.GetPageSize(page), _view.Zoom, _view.Scale);
            if ((long)full.Width * full.Height > MaxBasePixels)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsScheduled(Key key)
    {
        foreach (var request in _queue)
        {
            if (request.Key == key)
            {
                return true;
            }
        }
        foreach (var request in _running)
        {
            if (request.Key == key)
            {
                return true;
            }
        }
        return false;
    }

    private void Pump()
    {
        if (_pumping)
        {
            return;
        }
        _pumping = true;
        try
        {
            while (_running.Count < MaxRunning)
            {
                Request? next = null;
                foreach (var request in _queue)
                {
                    if ((next is null || request.Rank < next.Rank) && !IsPageBusy(request.Key.Page))
                    {
                        next = request;
                    }
                }
                if (next is null)
                {
                    break;
                }
                _queue.Remove(next);
                Start(next);
            }
        }
        finally
        {
            _pumping = false;
        }
    }

    private bool IsPageBusy(int page) => _pages.TryGetValue(page, out var state) && state.Running is not null;

    private PageState StateOf(int page)
    {
        if (!_pages.TryGetValue(page, out var state))
        {
            state = new PageState();
            _pages[page] = state;
        }
        return state;
    }

    private void Start(Request request)
    {
        request.Cancellation = new CancellationTokenSource();
        StateOf(request.Key.Page).Running = request;
        _running.Add(request);
        _ = RunAsync(request, _source!, request.Cancellation.Token);
    }

    private async Task RunAsync(Request request, IPageSource source, CancellationToken cancellationToken)
    {
        PageImage? image = null;
        var failed = false;
        try
        {
            image = await source.RenderAsync(new PageRenderRequest(request.Key.Page, request.Key.Size, request.Key.Region), cancellationToken);
        }
        catch (Exception)
        {
            // Cancelled, or a page that cannot be rendered: it keeps its placeholder, and asking again would only repeat the failure.
            if (!cancellationToken.IsCancellationRequested && request.Generation == _generation)
            {
                _finishedEmpty.Add(request.Key);
            }
            failed = true;
        }
        Finish(request, image, failed, cancellationToken.IsCancellationRequested);
    }

    private void Finish(Request request, PageImage? image, bool failed, bool cancelled)
    {
        _running.Remove(request);
        var state = StateOf(request.Key.Page);
        if (ReferenceEquals(state.Running, request))
        {
            state.Running = null;
        }
        // Not disposed: cancelling can run this code inline, and a source without a timer holds nothing to free.
        request.Cancellation = null;
        var current = request.Generation == _generation;
        if (!current || cancelled)
        {
            if (image is { OwnsBitmap: true })
            {
                DisposeLater(image.Bitmap);
            }
        }
        else if (!failed)
        {
            if (image is null)
            {
                _finishedEmpty.Add(request.Key);
            }
            else
            {
                Store(request, image);
            }
        }
        if (current && request.IsAuxiliary && !cancelled)
        {
            AuxiliaryReady?.Invoke(request.Key.Page, request.Key.Size);
        }
        Pump();
        if (current)
        {
            ChooseBest(request.Key.Page);
            // A base that just arrived may call for a detail, and the queue may have moved on.
            Refresh();
            Trim();
            Changed?.Invoke();
        }
    }

    private void Store(Request request, PageImage image)
    {
        var state = StateOf(request.Key.Page);
        var entry = new PageBitmapEntry(request.Key.Page, request.Key.Size, request.Key.Region, image.Bitmap, image.OwnsBitmap);
        if (_cache.TryGetValue(request.Key, out var replaced))
        {
            Remove(replaced);
        }
        _cache[request.Key] = entry;
        entry.Node = _lru.AddFirst(entry);
        _bytes += entry.Bytes;
        if (request.Key.Region is null)
        {
            state.Bases.Add(entry);
        }
        else
        {
            // One detail per page: a new region replaces the old one.
            if (state.Detail is { } old)
            {
                Remove(old);
            }
            state.Detail = entry;
        }
    }

    private void ChooseBest(int page)
    {
        if (!_pages.TryGetValue(page, out var state))
        {
            return;
        }
        var full = FullSize(_layout.GetPageSize(page), _view.Zoom, _view.Scale);
        var capped = CapSize(full);
        PageBitmapEntry? best = null;
        foreach (var entry in state.Bases)
        {
            if (entry.TargetSize == capped)
            {
                best = entry;
                break;
            }
            if (best is null || (long)entry.Bitmap.PixelSize.Width * entry.Bitmap.PixelSize.Height
                > (long)best.Bitmap.PixelSize.Width * best.Bitmap.PixelSize.Height)
            {
                best = entry;
            }
        }
        var detailChanged = !ReferenceEquals(state.BestDetail, capped != full ? state.Detail : null);
        var baseChanged = !ReferenceEquals(state.BestBase, best);
        state.BestBase = best;
        state.BestDetail = capped != full ? state.Detail : null;
        if (baseChanged || detailChanged)
        {
            Changed?.Invoke();
        }
    }

    private void Touch(PageBitmapEntry entry)
    {
        if (entry.Node is { } node && !ReferenceEquals(_lru.First, node))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
        }
    }

    private void Trim()
    {
        var node = _lru.Last;
        while (_bytes > _budget && node is not null)
        {
            var previous = node.Previous;
            if (!IsDrawn(node.Value))
            {
                Remove(node.Value);
            }
            node = previous;
        }
    }

    /// <summary>What a visible page draws right now is not evicted.</summary>
    private bool IsDrawn(PageBitmapEntry entry)
    {
        if (!_hasView || entry.Page < _view.First || entry.Page > _view.Last || !_pages.TryGetValue(entry.Page, out var state))
        {
            return false;
        }
        return ReferenceEquals(state.BestBase, entry) || ReferenceEquals(state.BestDetail, entry);
    }

    private void Remove(PageBitmapEntry entry)
    {
        _cache.Remove(new Key(entry.Page, entry.TargetSize, entry.Region));
        if (entry.Node is { } node)
        {
            _lru.Remove(node);
            entry.Node = null;
        }
        _bytes -= entry.Bytes;
        if (_pages.TryGetValue(entry.Page, out var state))
        {
            state.Bases.Remove(entry);
            if (ReferenceEquals(state.Detail, entry))
            {
                state.Detail = null;
            }
            if (ReferenceEquals(state.BestDetail, entry))
            {
                state.BestDetail = null;
            }
            if (ReferenceEquals(state.BestBase, entry))
            {
                state.BestBase = null;
                ChooseBest(entry.Page);
            }
        }
        ScheduleDispose(entry);
    }

    private void ScheduleDispose(PageBitmapEntry entry)
    {
        if (entry.OwnsBitmap)
        {
            DisposeLater(entry.Bitmap);
        }
    }

    /// <summary>The compositor may still be drawing the bitmap in the current frame: it goes after the next one.</summary>
    private void DisposeLater(Bitmap bitmap)
    {
        _toDispose.Add(bitmap);
        if (!_disposePosted)
        {
            _disposePosted = true;
            Dispatcher.UIThread.Post(FlushDisposals, DispatcherPriority.Background);
        }
    }

    private void FlushDisposals()
    {
        _disposePosted = false;
        foreach (var bitmap in _toDispose)
        {
            bitmap.Dispose();
        }
        _toDispose.Clear();
    }

    private void RestartSettle()
    {
        if (_settleTimer is null)
        {
            _settleTimer = new DispatcherTimer();
            _settleTimer.Tick += OnSettled;
        }
        _settleTimer.Stop();
        _settleTimer.Interval = SettleDelay;
        _settleTimer.Start();
    }

    private void OnSettled(object? sender, EventArgs e)
    {
        _settleTimer?.Stop();
        _zoomSettling = false;
        _scrollSettling = false;
        Refresh();
    }

    private void UpdateLoading()
    {
        var loading = false;
        if (_source is not null && _hasView)
        {
            for (var page = _view.First; page <= _view.Last && !loading; page++)
            {
                if (_pages.TryGetValue(page, out var state) && (state.BestBase is not null || state.BestDetail is not null))
                {
                    continue;
                }
                var key = new Key(page, CapSize(FullSize(_layout.GetPageSize(page), _view.Zoom, _view.Scale)), null);
                loading = !_finishedEmpty.Contains(key);
            }
        }
        SetLoading(loading);
    }

    private void SetLoading(bool value)
    {
        if (_isLoading != value)
        {
            _isLoading = value;
            LoadingChanged?.Invoke();
        }
    }
}
