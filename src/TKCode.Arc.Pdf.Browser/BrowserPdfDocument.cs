using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// A document open in pdf.js (design/components/pdf/pdf.md section 5.9). <see cref="PageCount"/>, <see cref="Info"/> and
/// <see cref="GetPageSize"/> are plain managed data, so they stay valid after <see cref="Dispose"/>. The other calls end with
/// <see cref="OperationCanceledException"/> after it (<see cref="ObjectDisposedException"/> for <see cref="GetPageLabel"/>).
/// A page that cannot be read gives no picture, no text and no links, as in the PDFium backend; the document itself goes on.
/// </summary>
internal sealed class BrowserPdfDocument : PdfDocument
{
    /// <summary>
    /// pdf.js has no way to ask for all page sizes, and asking for a page is one message to the worker. So this many come with the
    /// document, and in a longer one the others are read in the background, in the meantime taking the size of the last page known:
    /// most long documents have one page size, and the rest get one <see cref="PdfDocument.PagesChanged"/> at the end.
    /// </summary>
    private const int FirstSizes = 512;
    private const int SizeBatch = 256;

    /// <summary>A page count no real document reaches, which only a damaged or hostile file would claim.</summary>
    private const int MaxPages = 1 << 22;

    /// <summary>Browsers limit a canvas to about this much; beyond it a canvas is silently blank. The viewer asks for far less.</summary>
    private const long MaxBitmapBytes = 268_435_456L * 4;

    /// <summary>Painting takes the main thread, so one page at a time across all documents.</summary>
    private static readonly SemaphoreSlim s_renderGate = new(1, 1);

    private readonly CancellationTokenSource _lifetime = new();
    private readonly JSObject _handle;
    private readonly Size[] _pageSizes;
    private string?[]? _pageLabels;

    private BrowserPdfDocument(JSObject handle, Size[] pageSizes, PdfDocumentInfo info)
    {
        _handle = handle;
        _pageSizes = pageSizes;
        Info = info;
    }

    public override int PageCount => _pageSizes.Length;

    public override PdfDocumentInfo Info { get; }

    /// <summary>Makes the document of a handle that opened; it owns the handle from the moment this returns. Throws what the file gets wrong.</summary>
    public static async Task<BrowserPdfDocument> CreateAsync(JSObject handle, CancellationToken cancellationToken)
    {
        var count = handle.GetPropertyAsInt32("pageCount");
        if (count is <= 0 or > MaxPages)
        {
            throw new PdfException(PdfErrorKind.Corrupt, "The PDF has no readable pages.");
        }
        var sizes = new Size[count];
        var firstCount = Math.Min(count, FirstSizes);
        using (var result = await BrowserPdfInterop.ReadSizes(handle, 0, firstCount).WaitAsync(cancellationToken))
        {
            PdfJsGeometry.FillPageSizes(sizes, 0, BrowserPdfInterop.Numbers(result, "sizes"), new Size(816, 1056));
        }
        // Until they are read, later pages look like the last one that was.
        Array.Fill(sizes, sizes[firstCount - 1], firstCount, count - firstCount);
        PdfDocumentInfo info;
        using (var result = await BrowserPdfInterop.ReadInfo(handle).WaitAsync(cancellationToken))
        {
            info = new PdfDocumentInfo
            {
                Title = result.GetPropertyAsString("title"),
                Author = result.GetPropertyAsString("author"),
                Subject = result.GetPropertyAsString("subject"),
                Keywords = result.GetPropertyAsString("keywords"),
                Creator = result.GetPropertyAsString("creator"),
                Producer = result.GetPropertyAsString("producer"),
                Created = PdfDateText.Parse(result.GetPropertyAsString("created")),
                Modified = PdfDateText.Parse(result.GetPropertyAsString("modified")),
            };
        }
        var document = new BrowserPdfDocument(handle, sizes, info);
        _ = document.ReadLabelsAsync();
        if (count > firstCount)
        {
            _ = document.ReadOtherSizesAsync(firstCount);
        }
        return document;
    }

    public override Size GetPageSize(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return _pageSizes[pageIndex];
    }

    /// <summary>What was read so far: pdf.js gives all labels at once, some time after the document opens. Never waits.</summary>
    public override string? GetPageLabel(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        ObjectDisposedException.ThrowIf(_lifetime.IsCancellationRequested, this);
        return _pageLabels?[pageIndex];
    }

    public override async ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(request.PageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(request.PageIndex, PageCount);
        if (PdfJsGeometry.RenderArea(request, MaxBitmapBytes) is not { } area)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _lifetime.Token.ThrowIfCancellationRequested();
            return null;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var token = linked.Token;
        await s_renderGate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            using var render = BrowserPdfInterop.BeginRender(_handle, request.PageIndex, request.TargetSize.Width, request.TargetSize.Height, area.X, area.Y, area.Width, area.Height);
            int status;
            using (token.Register(static state => BrowserPdfInterop.CancelRender((JSObject)state!), render))
            {
                status = await BrowserPdfInterop.WaitRender(render);
            }
            if (token.IsCancellationRequested || status < 0)
            {
                BrowserPdfInterop.ReleaseRender(render);
                throw new OperationCanceledException(token);
            }
            if (status == 0)
            {
                BrowserPdfInterop.ReleaseRender(render);
                return null;
            }
            return new PageImage(CopyToBitmap(render, area));
        }
        finally
        {
            s_renderGate.Release();
        }
    }

    public override Task<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync(CancellationToken cancellationToken) =>
        ReadAsync<IReadOnlyList<PdfOutlineItem>>(
            () => BrowserPdfInterop.ReadOutline(_handle, PdfJsOutline.MaxNodes, PdfJsOutline.MaxDepth),
            result => PdfJsOutline.Build(BrowserPdfInterop.Strings(result, "titles"), BrowserPdfInterop.Numbers(result, "values"), PageCount),
            [],
            cancellationToken);

    public override Task<PdfPageText> GetPageTextAsync(int pageIndex, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return ReadAsync(
            () => BrowserPdfInterop.ReadText(_handle, pageIndex),
            result => PdfJsText.Build(result.GetPropertyAsString("text") ?? string.Empty, BrowserPdfInterop.Numbers(result, "values"), BrowserPdfInterop.Numbers(result, "view")),
            new PdfPageText(string.Empty, []),
            cancellationToken);
    }

    public override Task<IReadOnlyList<PdfLink>> GetLinksAsync(int pageIndex, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return ReadAsync<IReadOnlyList<PdfLink>>(
            () => BrowserPdfInterop.ReadLinks(_handle, pageIndex),
            result => PdfJsLinks.Build(BrowserPdfInterop.Strings(result, "uris"), BrowserPdfInterop.Numbers(result, "values"), BrowserPdfInterop.Numbers(result, "view"), PageCount),
            [],
            cancellationToken);
    }

    /// <summary>Ends the waits at once and destroys the pdf.js document with its worker; bitmaps handed out are copies that do not depend on it.</summary>
    public override void Dispose()
    {
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }
        _lifetime.Cancel();
        BrowserPdfInterop.Close(_handle);
        _handle.Dispose();
    }

    private static unsafe Bitmap CopyToBitmap(JSObject render, PixelRect area)
    {
        var stride = area.Width * 4;
        var bytes = (nuint)((long)stride * area.Height);
        // Native memory, not a pooled array: a page is many megabytes, and this way they go back at once instead of staying with the collector.
        var pixels = NativeMemory.Alloc(bytes);
        try
        {
            BrowserPdfInterop.TakePixels(render, new Span<byte>(pixels, (int)bytes));
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, (nint)pixels, new PixelSize(area.Width, area.Height), new Vector(96, 96), stride);
        }
        finally
        {
            NativeMemory.Free(pixels);
        }
    }

    /// <summary>Runs a read in the worker. The caller's token stops the wait; the worker's reply is then dropped.</summary>
    private async Task<T> ReadAsync<T>(Func<Task<JSObject>> start, Func<JSObject, T> convert, T whenUnreadable, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var token = linked.Token;
        token.ThrowIfCancellationRequested();
        try
        {
            using var result = await start().WaitAsync(token);
            return convert(result);
        }
        catch (JSException)
        {
            // The worker going away with the document looks like this too.
            token.ThrowIfCancellationRequested();
            return whenUnreadable;
        }
    }

    private async Task ReadLabelsAsync()
    {
        try
        {
            using var result = await BrowserPdfInterop.ReadLabels(_handle).WaitAsync(_lifetime.Token);
            if (result.HasProperty("labels"))
            {
                _pageLabels = PdfJsGeometry.PageLabels(BrowserPdfInterop.Strings(result, "labels"), PageCount);
                RaisePageLabelsChanged();
            }
        }
        catch (Exception exception) when (exception is JSException or OperationCanceledException)
        {
            // No labels, which is what most documents have anyway.
        }
    }

    private async Task ReadOtherSizesAsync(int first)
    {
        var changed = false;
        try
        {
            for (var start = first; start < _pageSizes.Length; start += SizeBatch)
            {
                using var result = await BrowserPdfInterop.ReadSizes(_handle, start, Math.Min(SizeBatch, _pageSizes.Length - start)).WaitAsync(_lifetime.Token);
                changed |= PdfJsGeometry.FillPageSizes(_pageSizes, start, BrowserPdfInterop.Numbers(result, "sizes"), _pageSizes[start - 1]);
            }
        }
        catch (Exception exception) when (exception is JSException or OperationCanceledException)
        {
            // Closed, or the worker is gone: what is known stays.
            return;
        }
        if (changed)
        {
            RaisePagesChanged();
        }
    }
}
