using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// A document without a file, for tests and design time: every page is grey bars where words would be, drawn into real
/// bitmaps; <see cref="GetPageTextAsync"/> returns real words with the boxes of those bars, so search and selection can be
/// tested against it. The same arguments always give the same pages. It has a fixed outline, and page 0 has one internal and
/// one external link. Never used unless created explicitly (a real document is never shown as a fake one).
/// </summary>
public sealed class SimulatedPdfDocument : PdfDocument
{
    /// <summary>US Letter in page DIPs (8.5 × 11 in at 96 DIP per inch).</summary>
    private static readonly Size s_defaultPageSize = new(816, 1056);

    private const int Paper = unchecked((int)0xFFFFFFFF);
    private const int WordInk = unchecked((int)0xFFC4C4C4);
    private const int TitleInk = unchecked((int)0xFF8C8C8C);
    private const int LinkInk = unchecked((int)0xFF7AA7F0);
    private const int FigureInk = unchecked((int)0xFFECECEC);

    private static readonly string[] s_words =
    [
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed", "do", "eiusmod", "tempor",
        "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua", "enim", "ad", "minim", "veniam", "quis"
    ];

    private readonly Size _pageSize;
    private readonly Dictionary<int, SimulatedPage> _pages = new();
    private readonly CancellationTokenSource _disposed = new();

    /// <summary>Creates the document.</summary>
    /// <param name="pageCount">How many pages (at least 1).</param>
    /// <param name="pageSize">The size of every page in page DIPs; null is US Letter.</param>
    public SimulatedPdfDocument(int pageCount = 12, Size? pageSize = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 1);
        PageCount = pageCount;
        _pageSize = pageSize ?? s_defaultPageSize;
        Info = new PdfDocumentInfo
        {
            Title = "Simulated document",
            Author = "TKCode.Arc",
            Subject = "A generated document for tests and design time",
            Creator = nameof(SimulatedPdfDocument),
            Producer = nameof(SimulatedPdfDocument),
            Created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Modified = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
    }

    /// <inheritdoc />
    public override int PageCount { get; }

    /// <inheritdoc />
    public override PdfDocumentInfo Info { get; }

    /// <summary>True once <see cref="Dispose"/> was called.</summary>
    public bool IsDisposed => _disposed.IsCancellationRequested;

    /// <inheritdoc />
    public override Size GetPageSize(int pageIndex) => _pageSize;

    /// <summary>The page's number: known from the start, so <see cref="PdfDocument.PageLabelsChanged"/> never fires.</summary>
    public override string? GetPageLabel(int pageIndex) => (pageIndex + 1).ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken)
    {
        ThrowIfStopped(cancellationToken);
        var bitmap = Draw(PageAt(request.PageIndex), request);
        return new ValueTask<PageImage?>(new PageImage(bitmap));
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync(CancellationToken cancellationToken)
    {
        ThrowIfStopped(cancellationToken);
        PdfDestination To(int page, double? y = null) =>
            new(Math.Min(page, PageCount - 1), y is { } top ? new Point(0, top * _pageSize.Height) : null);
        IReadOnlyList<PdfOutlineItem> outline =
        [
            new PdfOutlineItem("Introduction", To(0)),
            new PdfOutlineItem("Getting started", To(1),
            [
                new PdfOutlineItem("Installation", To(1, 0.25)),
                new PdfOutlineItem("First steps", To(2))
            ]),
            new PdfOutlineItem("Reference", null,
            [
                new PdfOutlineItem("Pages", To(PageCount / 2)),
                new PdfOutlineItem("Index", To(PageCount - 1))
            ])
        ];
        return Task.FromResult(outline);
    }

    /// <inheritdoc />
    public override Task<PdfPageText> GetPageTextAsync(int pageIndex, CancellationToken cancellationToken)
    {
        ThrowIfStopped(cancellationToken);
        return Task.FromResult(PageAt(pageIndex).Text);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<PdfLink>> GetLinksAsync(int pageIndex, CancellationToken cancellationToken)
    {
        ThrowIfStopped(cancellationToken);
        return Task.FromResult(PageAt(pageIndex).Links);
    }

    /// <inheritdoc />
    public override void Dispose() => _disposed.Cancel();

    private void ThrowIfStopped(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed.IsCancellationRequested)
        {
            throw new OperationCanceledException(_disposed.Token);
        }
    }

    private SimulatedPage PageAt(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        lock (_pages)
        {
            if (!_pages.TryGetValue(pageIndex, out var page))
            {
                page = BuildPage(pageIndex);
                _pages.Add(pageIndex, page);
            }
            return page;
        }
    }

    /// <summary>Lays one page out on a character grid: a title, then paragraphs of words of a repeatable random length.</summary>
    private SimulatedPage BuildPage(int pageIndex)
    {
        var unit = _pageSize.Width / s_defaultPageSize.Width;
        var margin = 72 * unit;
        var charWidth = 7 * unit;
        var lineHeight = 18 * unit;
        var boxHeight = 11 * unit;
        var writer = new PageWriter();
        var random = new Repeatable(pageIndex);

        var y = margin;
        writer.Line(string.Create(CultureInfo.InvariantCulture, $"Page {pageIndex + 1}"), margin, y, charWidth * 2, boxHeight * 2, TitleInk);
        y += lineHeight * 3;

        var links = new List<PdfLink>();
        if (pageIndex == 0)
        {
            var target = new PdfDestination(Math.Min(2, PageCount - 1), new Point(0, margin));
            links.Add(new PdfLink(writer.Line("Go to chapter two", margin, y, charWidth, boxHeight, LinkInk), target, null));
            y += lineHeight;
            links.Add(new PdfLink(writer.Line("Visit example.com", margin, y, charWidth, boxHeight, LinkInk), null, new Uri("https://example.com/")));
            y += lineHeight * 2;
        }

        // Every third page has a figure, so the pages differ at a glance.
        Rect? figure = null;
        if (pageIndex % 3 == 2)
        {
            figure = new Rect(margin, y, _pageSize.Width - margin * 2, _pageSize.Height * 0.22);
            y = figure.Value.Bottom + lineHeight;
        }

        var bottom = _pageSize.Height - margin - lineHeight;
        var perLine = Math.Max(1, (int)((_pageSize.Width - margin * 2) / charWidth));
        while (y < bottom)
        {
            var lines = 2 + random.Next(5);
            for (var i = 0; i < lines && y < bottom; i++)
            {
                // The last line of a paragraph stops short.
                var room = i == lines - 1 ? perLine / 2 + random.Next(perLine / 2) : perLine;
                writer.Line(random.Words(room), margin, y, charWidth, boxHeight, WordInk);
                y += lineHeight;
            }
            y += lineHeight;
        }
        return new SimulatedPage(writer.Build(), writer.Bars, figure, links);
    }

    private Bitmap Draw(SimulatedPage page, in PageRenderRequest request)
    {
        var target = request.TargetSize;
        var area = request.Region ?? new PixelRect(target);
        var width = Math.Max(1, area.Width);
        var height = Math.Max(1, area.Height);
        var scaleX = target.Width / _pageSize.Width;
        var scaleY = target.Height / _pageSize.Height;
        var pixels = ArrayPool<int>.Shared.Rent(width * height);
        try
        {
            pixels.AsSpan(0, width * height).Fill(Paper);
            if (page.Figure is { } figure)
            {
                Fill(pixels, width, height, area, figure, scaleX, scaleY, FigureInk);
            }
            foreach (var bar in page.Bars)
            {
                Fill(pixels, width, height, area, bar.Bounds, scaleX, scaleY, bar.Ink);
            }
            var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var buffer = bitmap.Lock())
            {
                for (var row = 0; row < height; row++)
                {
                    Marshal.Copy(pixels, row * width, buffer.Address + row * buffer.RowBytes, width);
                }
            }
            return bitmap;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(pixels);
        }
    }

    private static void Fill(int[] pixels, int width, int height, PixelRect area, Rect bounds, double scaleX, double scaleY, int ink)
    {
        var left = Math.Clamp((int)Math.Round(bounds.Left * scaleX) - area.X, 0, width);
        var right = Math.Clamp((int)Math.Round(bounds.Right * scaleX) - area.X, 0, width);
        var top = Math.Clamp((int)Math.Round(bounds.Top * scaleY) - area.Y, 0, height);
        var bottom = Math.Clamp((int)Math.Round(bounds.Bottom * scaleY) - area.Y, 0, height);
        if (right <= left)
        {
            return;
        }
        for (var row = top; row < bottom; row++)
        {
            pixels.AsSpan(row * width + left, right - left).Fill(ink);
        }
    }

    private readonly record struct Bar(Rect Bounds, int Ink);

    private sealed record SimulatedPage(PdfPageText Text, IReadOnlyList<Bar> Bars, Rect? Figure, IReadOnlyList<PdfLink> Links);

    /// <summary>Collects the text with its boxes and turns each word into a bar.</summary>
    private sealed class PageWriter
    {
        private readonly StringBuilder _text = new();
        private readonly List<Rect> _boxes = new();

        public List<Bar> Bars { get; } = new();

        /// <summary>Writes one line and returns its area.</summary>
        public Rect Line(string line, double x, double y, double charWidth, double height, int ink)
        {
            if (_text.Length > 0)
            {
                _text.Append('\n');
                _boxes.Add(default);
            }
            var wordStart = -1;
            for (var i = 0; i <= line.Length; i++)
            {
                if (i == line.Length || line[i] == ' ')
                {
                    if (wordStart >= 0)
                    {
                        Bars.Add(new Bar(new Rect(x + wordStart * charWidth, y, (i - wordStart) * charWidth, height), ink));
                        wordStart = -1;
                    }
                    if (i < line.Length)
                    {
                        _text.Append(' ');
                        _boxes.Add(default);
                    }
                    continue;
                }
                if (wordStart < 0)
                {
                    wordStart = i;
                }
                _text.Append(line[i]);
                _boxes.Add(new Rect(x + i * charWidth, y, charWidth, height));
            }
            return new Rect(x, y, line.Length * charWidth, height);
        }

        public PdfPageText Build() => new(_text.ToString(), _boxes.ToArray());
    }

    /// <summary>A small generator of its own: <see cref="Random"/> does not promise the same numbers on every runtime.</summary>
    private struct Repeatable
    {
        private uint _state;

        public Repeatable(int seed)
        {
            _state = (uint)seed * 2654435761u + 12345u;
        }

        public int Next(int limit)
        {
            _state = _state * 1664525u + 1013904223u;
            return limit <= 0 ? 0 : (int)((_state >> 8) % (uint)limit);
        }

        public string Words(int room)
        {
            var line = new StringBuilder();
            while (true)
            {
                var word = s_words[Next(s_words.Length)];
                var needed = line.Length == 0 ? word.Length : line.Length + 1 + word.Length;
                if (needed > room)
                {
                    return line.Length == 0 ? word[..Math.Min(word.Length, Math.Max(1, room))] : line.ToString();
                }
                if (line.Length > 0)
                {
                    line.Append(' ');
                }
                line.Append(word);
            }
        }
    }
}
