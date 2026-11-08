using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An <see cref="IPageSource"/> over a collection of pictures: <see cref="Bitmap"/>, <see cref="ImageSource"/>, or a
/// <see cref="string"/> or <see cref="Uri"/> address, read through <see cref="ArcImage.DefaultLoader"/> and shared with
/// <see cref="ArcImageCache"/>. An <see cref="INotifyCollectionChanged"/> collection is followed while the source has a
/// <see cref="PagesChanged"/> subscriber. A bitmap's page size is its <see cref="Bitmap.Size"/>; an address has the size of
/// the first known page until its picture arrives, and then <see cref="PagesChanged"/> fires. Other elements are empty
/// pages. Pictures stay with their owners (the viewer does not dispose them) and have no detail layer: at high zoom the
/// whole picture is stretched.
/// </summary>
public sealed class ImagePageSource : IPageSource
{
    private static readonly Size s_defaultPageSize = new(816, 1056);

    private readonly IEnumerable _images;
    private readonly Dictionary<object, Size> _loadedSizes = new();
    private object[] _items = [];
    private Size? _firstKnownSize;
    private EventHandler? _pagesChanged;

    /// <summary>Wraps a collection of pictures.</summary>
    public ImagePageSource(IEnumerable images)
    {
        ArgumentNullException.ThrowIfNull(images);
        _images = images;
        Snapshot();
    }

    /// <inheritdoc />
    public int PageCount => _items.Length;

    /// <inheritdoc />
    public event EventHandler? PagesChanged
    {
        add
        {
            var first = _pagesChanged is null;
            _pagesChanged += value;
            if (first && _images is INotifyCollectionChanged collection)
            {
                // The collection reached this source only through a subscriber, so an idle source leaves no reference behind.
                collection.CollectionChanged += OnCollectionChanged;
                Snapshot();
            }
        }
        remove
        {
            _pagesChanged -= value;
            if (_pagesChanged is null && _images is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged -= OnCollectionChanged;
            }
        }
    }

    /// <inheritdoc />
    public Size GetPageSize(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _items.Length)
        {
            return s_defaultPageSize;
        }
        return KnownSize(_items[pageIndex]) ?? _firstKnownSize ?? s_defaultPageSize;
    }

    /// <inheritdoc />
    public async ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken)
    {
        if (request.Region is not null || request.PageIndex < 0 || request.PageIndex >= _items.Length)
        {
            return null;
        }
        var item = _items[request.PageIndex];
        if (item is Bitmap bitmap)
        {
            return new PageImage(bitmap, ownsBitmap: false);
        }
        if (ToImageSource(item) is not { } source)
        {
            return null;
        }
        if (source is BitmapImageSource direct)
        {
            return new PageImage(direct.Bitmap, ownsBitmap: false);
        }
        if (!ArcImageCache.TryGet(source, out var frame) || frame.IsReduced)
        {
            // The cache does not dispose what it drops, and neither does the viewer: the bitmap belongs to no one and the GC frees it.
            frame = await ArcImageCache.LoadAsync(source, ArcImage.DefaultLoader, null, Stretch.Uniform, cancellationToken);
        }
        if (!_loadedSizes.TryGetValue(item, out var size) || size != frame.NaturalSize)
        {
            _loadedSizes[item] = frame.NaturalSize;
            UpdateFirstKnownSize();
            Dispatcher.UIThread.Post(RaisePagesChanged);
        }
        return new PageImage(frame.Bitmap, ownsBitmap: false);
    }

    private Size? KnownSize(object item)
    {
        if (item is Bitmap bitmap)
        {
            return bitmap.Size;
        }
        if (item is BitmapImageSource source)
        {
            return source.Bitmap.Size;
        }
        return _loadedSizes.TryGetValue(item, out var size) ? size : null;
    }

    private static ImageSource? ToImageSource(object item) => item switch
    {
        ImageSource source => source,
        string text when ImageSource.TryParse(text, out var parsed) => parsed,
        Uri uri when uri.IsAbsoluteUri && UriImageSource.IsSupported(uri) => new UriImageSource(uri),
        _ => null
    };

    private void Snapshot()
    {
        var items = new List<object>();
        foreach (var item in _images)
        {
            if (item is not null)
            {
                items.Add(item);
            }
        }
        _items = items.ToArray();
        UpdateFirstKnownSize();
    }

    /// <summary>Kept, not searched for per page: a long list of addresses would make reading the page sizes quadratic.</summary>
    private void UpdateFirstKnownSize()
    {
        _firstKnownSize = null;
        foreach (var item in _items)
        {
            if (KnownSize(item) is { } size)
            {
                _firstKnownSize = size;
                return;
            }
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Snapshot();
        RaisePagesChanged();
    }

    private void RaisePagesChanged() => _pagesChanged?.Invoke(this, EventArgs.Empty);
}
