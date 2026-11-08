using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// A document open in PDFium (design/components/pdf/pdf.md section 5.8). Every native call runs on <see cref="PdfiumThread"/>;
/// the fields marked as such are only touched there. <see cref="PageCount"/>, <see cref="Info"/> and <see cref="GetPageSize"/> are
/// plain managed data read at open, so they stay valid after <see cref="Dispose"/> and never cause native access. The other calls
/// end with <see cref="OperationCanceledException"/> after it (<see cref="ObjectDisposedException"/> for <see cref="GetPageLabel"/>).
/// </summary>
internal sealed class PdfiumDocument : PdfDocument
{
    private const int PageCacheSize = 16;
    private const int MaxOutlineNodes = 10_000;
    private const int MaxOutlineDepth = 64;

    /// <summary>How many pages one call may load only to convert the points of destinations: loading a page parses its content.</summary>
    private const int MaxPagesLoadedForDestinations = 64;

    /// <summary>
    /// Page labels read in one go on the PDFium thread: the first batch while opening, so a normal document has all of them from the
    /// start; a longer one goes on in the background, batch after batch, with the queue's other work between them.
    /// </summary>
    private const int PageLabelBatch = 256;

    /// <summary>Beyond this a page bitmap is refused (it would not fit an array); the viewer caps its requests far below.</summary>
    private const long MaxBitmapBytes = int.MaxValue / 2;

    private const double DipPerPoint = 96.0 / 72.0;

    private readonly CancellationTokenSource _lifetime = new();
    private readonly Size[] _pageSizes;
    // Written on the PDFium thread, read anywhere: a reference is written whole, and a label not there yet reads as none.
    private readonly string?[]? _pageLabels;

    // On the PDFium thread only.
    private nint _document;
    private IDisposable? _resources;
    private readonly List<(int Index, nint Handle)> _openPages = new(PageCacheSize);

    private PdfiumDocument(nint document, IDisposable? resources, Size[] pageSizes, PdfDocumentInfo info, bool hasPageLabels)
    {
        _document = document;
        _resources = resources;
        _pageSizes = pageSizes;
        Info = info;
        if (hasPageLabels)
        {
            _pageLabels = new string?[pageSizes.Length];
            ReadPageLabelsOnThread(0);
        }
    }

    public override int PageCount => _pageSizes.Length;

    public override PdfDocumentInfo Info { get; }

    /// <summary>
    /// Makes the document of a handle from <c>FPDF_Load*</c>; a null handle throws the reason. Takes over
    /// <paramref name="resources"/> (what the handle reads from) in every case, closing them with the document or at once on failure.
    /// </summary>
    public static PdfiumDocument Create(nint document, IDisposable? resources, CancellationToken cancellationToken)
    {
        if (document == 0)
        {
            var error = (int)PdfiumInterop.FPDF_GetLastError().Value;
            resources?.Dispose();
            throw new PdfException(error switch
            {
                PdfiumInterop.ErrorFile => PdfErrorKind.NotFound,
                PdfiumInterop.ErrorPassword => PdfErrorKind.PasswordRequired,
                PdfiumInterop.ErrorSecurity => PdfErrorKind.Unsupported,
                // ErrorFormat, ErrorUnknown and ErrorPage: what cannot be read is damaged as far as anyone can tell.
                _ => PdfErrorKind.Corrupt
            });
        }
        try
        {
            var count = PdfiumInterop.FPDF_GetPageCount(document);
            if (count <= 0)
            {
                throw new PdfException(PdfErrorKind.Corrupt, "The PDF has no readable pages.");
            }
            var sizes = ReadPageSizes(document, count, cancellationToken);
            return new PdfiumDocument(document, resources, sizes, ReadInfo(document), ReadPageLabel(document, 0) is not null);
        }
        catch (Exception)
        {
            PdfiumInterop.FPDF_CloseDocument(document);
            resources?.Dispose();
            throw;
        }
    }

    public override Size GetPageSize(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return _pageSizes[pageIndex];
    }

    /// <summary>What was read so far; it never touches PDFium. A document without labels, or a page not reached yet, gives null.</summary>
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
        var pixels = await RunAsync(() => RenderOnThread(request), PdfiumPriority.Render, cancellationToken).ConfigureAwait(false);
        if (pixels is not { } rendered)
        {
            return null;
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new PageImage(CopyToBitmap(rendered));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rendered.Buffer);
        }
    }

    public override Task<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync(CancellationToken cancellationToken) =>
        RunAsync(ReadOutlineOnThread, PdfiumPriority.Background, cancellationToken);

    public override Task<PdfPageText> GetPageTextAsync(int pageIndex, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return RunAsync(() => ReadTextOnThread(pageIndex), PdfiumPriority.Background, cancellationToken);
    }

    public override Task<IReadOnlyList<PdfLink>> GetLinksAsync(int pageIndex, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        return RunAsync(() => ReadLinksOnThread(pageIndex), PdfiumPriority.Background, cancellationToken);
    }

    /// <summary>
    /// Ends the waits at once and queues the close. Work already on the thread finishes first and its result is dropped, so a
    /// page being rendered never meets a closed document, and bitmaps handed out are copies that do not depend on it.
    /// </summary>
    public override void Dispose()
    {
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }
        _lifetime.Cancel();
        _ = PdfiumThread.Instance.RunAsync(() =>
        {
            CloseOnThread();
            return true;
        }, PdfiumPriority.Lifetime, CancellationToken.None);
    }

    /// <summary>Closes pages, then the document, then what it reads from. Runs on the PDFium thread.</summary>
    internal void CloseOnThread()
    {
        _lifetime.Cancel();
        if (_document == 0)
        {
            return;
        }
        foreach (var (_, handle) in _openPages)
        {
            PdfiumInterop.FPDF_ClosePage(handle);
        }
        _openPages.Clear();
        PdfiumInterop.FPDF_CloseDocument(_document);
        _document = 0;
        _resources?.Dispose();
        _resources = null;
    }

    private async Task<T> RunAsync<T>(Func<T> work, PdfiumPriority priority, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var token = linked.Token;
        return await PdfiumThread.Instance.RunAsync(() =>
        {
            // Queued before Dispose and dequeued after it: the token says so, but the handle is the last word.
            if (_document == 0)
            {
                throw new OperationCanceledException(token);
            }
            return work();
        }, priority, token).ConfigureAwait(false);
    }

    private static unsafe Size[] ReadPageSizes(nint document, int count, CancellationToken cancellationToken)
    {
        var sizes = new Size[count];
        var last = new Size(816, 1056);
        for (var i = 0; i < count; i++)
        {
            if ((i & 1023) == 1023)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            PdfiumInterop.SizeF size;
            // A page tree that promises more than it has must not stop the rest: the neighbour's size keeps the layout going.
            if (PdfiumInterop.FPDF_GetPageSizeByIndexF(document, i, &size) != 0 && size.Width > 0 && size.Height > 0)
            {
                last = new Size(size.Width * DipPerPoint, size.Height * DipPerPoint);
            }
            sizes[i] = last;
        }
        return sizes;
    }

    private static PdfDocumentInfo ReadInfo(nint document) => new()
    {
        Title = ReadMeta(document, "Title\0"u8),
        Author = ReadMeta(document, "Author\0"u8),
        Subject = ReadMeta(document, "Subject\0"u8),
        Keywords = ReadMeta(document, "Keywords\0"u8),
        Creator = ReadMeta(document, "Creator\0"u8),
        Producer = ReadMeta(document, "Producer\0"u8),
        Created = ParsePdfDate(ReadMeta(document, "CreationDate\0"u8)),
        Modified = ParsePdfDate(ReadMeta(document, "ModDate\0"u8)),
    };

    private static unsafe string? ReadMeta(nint document, ReadOnlySpan<byte> tag)
    {
        fixed (byte* tagPointer = tag)
        {
            var length = PdfiumInterop.FPDF_GetMetaText(document, tagPointer, null, new CULong(0));
            if (length.Value <= 2)
            {
                return null;
            }
            var buffer = new byte[(int)length.Value];
            fixed (byte* bufferPointer = buffer)
            {
                PdfiumInterop.FPDF_GetMetaText(document, tagPointer, bufferPointer, length);
            }
            return DecodeUtf16(buffer);
        }
    }

    /// <inheritdoc cref="PdfDateText.Parse"/>
    internal static DateTimeOffset? ParsePdfDate(string? text) => PdfDateText.Parse(text);

    /// <summary>Reads one batch of labels and queues the next behind what is waiting, at the priority of other background reads.</summary>
    private void ReadPageLabelsOnThread(int first)
    {
        var labels = _pageLabels!;
        var end = Math.Min(first + PageLabelBatch, labels.Length);
        for (var i = first; i < end; i++)
        {
            labels[i] = ReadPageLabel(_document, i);
        }
        // The first batch comes with the document; nobody can be listening yet.
        if (first > 0)
        {
            RaisePageLabelsChanged();
        }
        if (end == labels.Length)
        {
            return;
        }
        // Ends by itself when the document closes (the token, or the handle check in RunAsync).
        _ = RunAsync(() =>
        {
            ReadPageLabelsOnThread(end);
            return true;
        }, PdfiumPriority.Background, _lifetime.Token);
    }

    private static unsafe string? ReadPageLabel(nint document, int pageIndex)
    {
        var length = PdfiumInterop.FPDF_GetPageLabel(document, pageIndex, null, new CULong(0));
        if (length.Value <= 2)
        {
            return null;
        }
        var buffer = new byte[(int)length.Value];
        fixed (byte* pointer = buffer)
        {
            PdfiumInterop.FPDF_GetPageLabel(document, pageIndex, pointer, length);
        }
        return DecodeUtf16(buffer);
    }

    private static string? DecodeUtf16(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        return text.Length > 0 ? text : null;
    }

    /// <summary>The page's handle from the short list of open ones; 0 when the page cannot be loaded. Closing the oldest may close a handle obtained earlier, so hold a handle only until the next call.</summary>
    private nint GetPage(int pageIndex)
    {
        for (var i = 0; i < _openPages.Count; i++)
        {
            if (_openPages[i].Index == pageIndex)
            {
                var hit = _openPages[i];
                _openPages.RemoveAt(i);
                _openPages.Add(hit);
                return hit.Handle;
            }
        }
        var handle = PdfiumInterop.FPDF_LoadPage(_document, pageIndex);
        if (handle == 0)
        {
            return 0;
        }
        if (_openPages.Count >= PageCacheSize)
        {
            PdfiumInterop.FPDF_ClosePage(_openPages[0].Handle);
            _openPages.RemoveAt(0);
        }
        _openPages.Add((pageIndex, handle));
        return handle;
    }

    private unsafe RenderedPixels? RenderOnThread(PageRenderRequest request)
    {
        var target = request.TargetSize;
        if (target.Width <= 0 || target.Height <= 0)
        {
            return null;
        }
        var full = new PixelRect(target);
        var area = request.Region is { } region ? region.Intersect(full) : full;
        var bytes = (long)area.Width * area.Height * 4;
        if (area.Width <= 0 || area.Height <= 0 || bytes > MaxBitmapBytes)
        {
            return null;
        }
        var page = GetPage(request.PageIndex);
        if (page == 0)
        {
            return null;
        }
        var stride = area.Width * 4;
        var buffer = ArrayPool<byte>.Shared.Rent((int)bytes);
        fixed (byte* first = buffer)
        {
            var bitmap = PdfiumInterop.FPDFBitmap_CreateEx(area.Width, area.Height, PdfiumInterop.BitmapBgra, first, stride);
            if (bitmap == 0)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                return null;
            }
            try
            {
                // Opaque white first, so BGRA comes out effectively premultiplied.
                PdfiumInterop.FPDFBitmap_FillRect(bitmap, 0, 0, area.Width, area.Height, new CULong(0xFFFFFFFF));
                // A region is the whole page drawn at its full size with the part before the region outside the bitmap.
                PdfiumInterop.FPDF_RenderPageBitmap(bitmap, page, -area.X, -area.Y, target.Width, target.Height, 0, PdfiumInterop.RenderAnnotations);
            }
            finally
            {
                PdfiumInterop.FPDFBitmap_Destroy(bitmap);
            }
        }
        return new RenderedPixels(buffer, area.Width, area.Height, stride);
    }

    private static unsafe Bitmap CopyToBitmap(RenderedPixels pixels)
    {
        fixed (byte* first = pixels.Buffer)
        {
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, (nint)first, new PixelSize(pixels.Width, pixels.Height), new Vector(96, 96), pixels.Stride);
        }
    }

    private unsafe PdfPageText ReadTextOnThread(int pageIndex)
    {
        var page = GetPage(pageIndex);
        if (page == 0)
        {
            return new PdfPageText(string.Empty, []);
        }
        var textPage = PdfiumInterop.FPDFText_LoadPage(page);
        if (textPage == 0)
        {
            return new PdfPageText(string.Empty, []);
        }
        try
        {
            var count = Math.Max(PdfiumInterop.FPDFText_CountChars(textPage), 0);
            var mapper = PdfiumPageMapper.Create(page, _pageSizes[pageIndex]);
            var text = new StringBuilder(count);
            var boxes = new List<Rect>(count);
            for (var i = 0; i < count; i++)
            {
                var unit = PdfiumInterop.FPDFText_GetUnicode(textPage, i);
                var box = ReadCharBox(textPage, i, unit, mapper);
                // Windows' PDFium holds characters as UTF-16 units, the others as code points; every text the rest of the code
                // sees must be valid UTF-16 (Normalize, in search, throws on a lone surrogate).
                if (unit is >= 0xD800 and <= 0xDBFF && i + 1 < count)
                {
                    var next = PdfiumInterop.FPDFText_GetUnicode(textPage, i + 1);
                    if (next is >= 0xDC00 and <= 0xDFFF)
                    {
                        unit = 0x10000 + ((unit - 0xD800) << 10) + (next - 0xDC00);
                        i++;
                    }
                }
                var rune = unit != 0 && Rune.IsValid(unit) ? new Rune(unit) : Rune.ReplacementChar;
                text.Append(rune.ToString());
                boxes.Add(box);
                if (rune.Utf16SequenceLength == 2)
                {
                    boxes.Add(box);
                }
            }
            return new PdfPageText(text.ToString(), boxes.ToArray());
        }
        finally
        {
            PdfiumInterop.FPDFText_ClosePage(textPage);
        }
    }

    private static unsafe Rect ReadCharBox(nint textPage, int index, uint unit, in PdfiumPageMapper mapper)
    {
        PdfiumInterop.RectF box;
        if (unit is '\r' or '\n' || PdfiumInterop.FPDFText_GetLooseCharBox(textPage, index, &box) == 0)
        {
            return default;
        }
        return mapper.Map(box.Left, box.Top, box.Right, box.Bottom);
    }

    private unsafe IReadOnlyList<PdfLink> ReadLinksOnThread(int pageIndex)
    {
        var page = GetPage(pageIndex);
        if (page == 0)
        {
            return [];
        }
        var mapper = PdfiumPageMapper.Create(page, _pageSizes[pageIndex]);

        // Everything is read from the page first: converting a destination may load pages, which may close this one.
        var annotations = new List<(Rect Bounds, RawDestination? Destination, Uri? Uri)>();
        var position = 0;
        nint link;
        while (PdfiumInterop.FPDFLink_Enumerate(page, &position, &link) != 0)
        {
            PdfiumInterop.RectF rect;
            if (PdfiumInterop.FPDFLink_GetAnnotRect(link, &rect) == 0)
            {
                continue;
            }
            var (destination, uri) = ReadLinkTarget(link);
            if (destination is not null || uri is not null)
            {
                annotations.Add((mapper.Map(rect.Left, rect.Top, rect.Right, rect.Bottom), destination, uri));
            }
        }
        var webLinks = ReadWebLinks(page, mapper);

        var links = new List<PdfLink>(annotations.Count + webLinks.Count);
        var converter = new DestinationConverter(this);
        foreach (var (bounds, destination, uri) in annotations)
        {
            links.Add(new PdfLink(bounds, destination is { } target ? converter.Convert(target) : null, uri));
        }
        foreach (var (bounds, uri) in webLinks)
        {
            // A web address that also has a link annotation is one link to the reader.
            if (!annotations.Exists(annotation => annotation.Bounds.Intersects(bounds)))
            {
                links.Add(new PdfLink(bounds, null, uri));
            }
        }
        return links;
    }

    private (RawDestination? Destination, Uri? Uri) ReadLinkTarget(nint link)
    {
        var dest = PdfiumInterop.FPDFLink_GetDest(_document, link);
        if (dest != 0)
        {
            return (ReadDestination(dest), null);
        }
        var action = PdfiumInterop.FPDFLink_GetAction(link);
        if (action == 0)
        {
            return (null, null);
        }
        return (int)PdfiumInterop.FPDFAction_GetType(action).Value switch
        {
            PdfiumInterop.ActionGoTo => (ReadActionDestination(action), null),
            PdfiumInterop.ActionUri => (null, ReadActionUri(action)),
            // Launching a program, other documents and anything else is not something a viewer does for an untrusted file.
            _ => (null, null)
        };
    }

    private RawDestination? ReadActionDestination(nint action)
    {
        var dest = PdfiumInterop.FPDFAction_GetDest(_document, action);
        return dest != 0 ? ReadDestination(dest) : null;
    }

    private unsafe Uri? ReadActionUri(nint action)
    {
        var length = PdfiumInterop.FPDFAction_GetURIPath(_document, action, null, new CULong(0));
        if (length.Value == 0 || length.Value > 1 << 20)
        {
            return null;
        }
        var buffer = new byte[(int)length.Value];
        fixed (byte* pointer = buffer)
        {
            PdfiumInterop.FPDFAction_GetURIPath(_document, action, pointer, length);
        }
        // The header warns that this may be badly encoded; replacement characters then make it fail the parse or look wrong, not crash.
        return ParseUri(Encoding.UTF8.GetString(buffer).TrimEnd('\0'));
    }

    private static Uri? ParseUri(string text) =>
        Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) ? uri : null;

    private unsafe RawDestination? ReadDestination(nint dest)
    {
        var page = PdfiumInterop.FPDFDest_GetDestPageIndex(_document, dest);
        if (page < 0 || page >= PageCount)
        {
            return null;
        }
        int hasX, hasY, hasZoom;
        float x, y, zoom;
        if (PdfiumInterop.FPDFDest_GetLocationInPage(dest, &hasX, &hasY, &hasZoom, &x, &y, &zoom) != 0)
        {
            return new RawDestination(page, hasX != 0 ? x : null, hasY != 0 ? y : null);
        }
        CULong parameterCount;
        var parameters = stackalloc float[4];
        var mode = (int)PdfiumInterop.FPDFDest_GetView(dest, &parameterCount, parameters).Value;
        var count = (int)parameterCount.Value;
        return mode switch
        {
            PdfiumInterop.ViewFitH or PdfiumInterop.ViewFitBH when count >= 1 => new RawDestination(page, null, parameters[0]),
            PdfiumInterop.ViewFitV or PdfiumInterop.ViewFitBV when count >= 1 => new RawDestination(page, parameters[0], null),
            PdfiumInterop.ViewFitR when count >= 4 => new RawDestination(page, parameters[0], parameters[3]),
            _ => new RawDestination(page, null, null)
        };
    }

    private unsafe List<(Rect Bounds, Uri Uri)> ReadWebLinks(nint page, in PdfiumPageMapper mapper)
    {
        var result = new List<(Rect, Uri)>();
        var textPage = PdfiumInterop.FPDFText_LoadPage(page);
        if (textPage == 0)
        {
            return result;
        }
        try
        {
            var linkPage = PdfiumInterop.FPDFLink_LoadWebLinks(textPage);
            if (linkPage == 0)
            {
                return result;
            }
            try
            {
                var count = PdfiumInterop.FPDFLink_CountWebLinks(linkPage);
                for (var i = 0; i < count; i++)
                {
                    var length = PdfiumInterop.FPDFLink_GetURL(linkPage, i, null, 0);
                    if (length <= 1 || length > 1 << 20)
                    {
                        continue;
                    }
                    var buffer = new char[length];
                    fixed (char* pointer = buffer)
                    {
                        PdfiumInterop.FPDFLink_GetURL(linkPage, i, (ushort*)pointer, length);
                    }
                    if (ParseUri(new string(buffer, 0, length).TrimEnd('\0')) is not { } uri)
                    {
                        continue;
                    }
                    // One link per line a wrapped address sits on.
                    var rects = PdfiumInterop.FPDFLink_CountRects(linkPage, i);
                    for (var r = 0; r < rects; r++)
                    {
                        double left, top, right, bottom;
                        if (PdfiumInterop.FPDFLink_GetRect(linkPage, i, r, &left, &top, &right, &bottom) != 0)
                        {
                            result.Add((mapper.Map(left, top, right, bottom), uri));
                        }
                    }
                }
            }
            finally
            {
                PdfiumInterop.FPDFLink_CloseWebLinks(linkPage);
            }
        }
        finally
        {
            PdfiumInterop.FPDFText_ClosePage(textPage);
        }
        return result;
    }

    /// <summary>
    /// A document is untrusted: its outline can be a cycle or absurdly large. The walk stops at <see cref="MaxOutlineNodes"/> nodes
    /// and <see cref="MaxOutlineDepth"/> levels, and never enters a handle twice (the header leaves circular references to the caller).
    /// </summary>
    private IReadOnlyList<PdfOutlineItem> ReadOutlineOnThread()
    {
        var visited = new HashSet<nint>();
        var converter = new DestinationConverter(this);
        return Walk(0, 0);

        List<PdfOutlineItem> Walk(nint parent, int depth)
        {
            var items = new List<PdfOutlineItem>();
            var current = PdfiumInterop.FPDFBookmark_GetFirstChild(_document, parent);
            while (current != 0 && visited.Count < MaxOutlineNodes && visited.Add(current))
            {
                var title = ReadBookmarkTitle(current);
                var destination = ReadBookmarkDestination(current);
                var children = depth + 1 < MaxOutlineDepth ? Walk(current, depth + 1) : [];
                items.Add(new PdfOutlineItem(title, destination is { } target ? converter.Convert(target) : null, children));
                current = PdfiumInterop.FPDFBookmark_GetNextSibling(_document, current);
            }
            return items;
        }
    }

    private static unsafe string ReadBookmarkTitle(nint bookmark)
    {
        var length = PdfiumInterop.FPDFBookmark_GetTitle(bookmark, null, new CULong(0));
        if (length.Value <= 2 || length.Value > 1 << 20)
        {
            return string.Empty;
        }
        var buffer = new byte[(int)length.Value];
        fixed (byte* pointer = buffer)
        {
            PdfiumInterop.FPDFBookmark_GetTitle(bookmark, pointer, length);
        }
        return DecodeUtf16(buffer) ?? string.Empty;
    }

    private RawDestination? ReadBookmarkDestination(nint bookmark)
    {
        var dest = PdfiumInterop.FPDFBookmark_GetDest(_document, bookmark);
        if (dest != 0)
        {
            return ReadDestination(dest);
        }
        var action = PdfiumInterop.FPDFBookmark_GetAction(bookmark);
        return action != 0 && (int)PdfiumInterop.FPDFAction_GetType(action).Value == PdfiumInterop.ActionGoTo
            ? ReadActionDestination(action)
            : null;
    }

    /// <summary>A destination as it is in the file: the point is still in the target page's page space.</summary>
    private readonly record struct RawDestination(int PageIndex, double? X, double? Y);

    private readonly record struct RenderedPixels(byte[] Buffer, int Width, int Height, int Stride);

    /// <summary>
    /// Converts destinations for one call. The transform of a target page needs that page loaded, and loading parses its content,
    /// which a long outline must not do for every chapter. So up to <see cref="MaxPagesLoadedForDestinations"/> pages are loaded; a later
    /// page borrows the transform of a page of the same size (right unless pages of one size differ in rotation or box origin, and
    /// then the jump still lands on the right page), and without one its point is left out, which means the top of the page.
    /// </summary>
    private sealed class DestinationConverter(PdfiumDocument document)
    {
        private readonly Dictionary<int, PdfiumPageMapper> _byPage = new();
        private readonly Dictionary<Size, PdfiumPageMapper> _bySize = new();

        public PdfDestination Convert(RawDestination destination)
        {
            if (destination.X is null && destination.Y is null)
            {
                return new PdfDestination(destination.PageIndex);
            }
            return MapperFor(destination.PageIndex) is { } mapper
                ? new PdfDestination(destination.PageIndex, mapper.MapDestination(destination.X, destination.Y))
                : new PdfDestination(destination.PageIndex);
        }

        private PdfiumPageMapper? MapperFor(int pageIndex)
        {
            if (_byPage.TryGetValue(pageIndex, out var known))
            {
                return known;
            }
            var size = document._pageSizes[pageIndex];
            if (_byPage.Count >= MaxPagesLoadedForDestinations)
            {
                return _bySize.TryGetValue(size, out var borrowed) ? borrowed : null;
            }
            var page = document.GetPage(pageIndex);
            if (page == 0)
            {
                return null;
            }
            var mapper = PdfiumPageMapper.Create(page, size);
            _byPage.Add(pageIndex, mapper);
            _bySize.TryAdd(size, mapper);
            return mapper;
        }
    }
}
