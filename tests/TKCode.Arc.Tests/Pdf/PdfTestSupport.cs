using Avalonia;

namespace TKCode.Arc.Tests.Pdf;

/// <summary>Sets <see cref="ArcPdf.Backend"/> for one test and puts the old one back.</summary>
internal sealed class BackendScope : IDisposable
{
    private readonly IPdfBackend _previous = ArcPdf.Backend;

    public BackendScope(IPdfBackend backend)
    {
        ArcPdf.Backend = backend;
    }

    public void Dispose() => ArcPdf.Backend = _previous;
}

/// <summary>One call to <see cref="FakePdfBackend.OpenAsync"/>.</summary>
internal sealed class OpenCall
{
    public required PdfSource Source { get; init; }

    public required string? Password { get; init; }

    public required CancellationToken Token { get; init; }

    public TaskCompletionSource<PdfDocument> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// A backend under the test's control. With <see cref="Hold"/> the opens wait for the test; otherwise they finish at once,
/// and <see cref="RequiredPassword"/> makes the document a locked one.
/// </summary>
internal sealed class FakePdfBackend : IPdfBackend
{
    public List<OpenCall> Calls { get; } = new();

    public bool Hold { get; set; }

    public string? RequiredPassword { get; set; }

    public Exception? Failure { get; set; }

    public int PageCount { get; set; } = 12;

    public List<SimulatedPdfDocument> Documents { get; } = new();

    public string Name => "fake";

    public bool IsReal => true;

    public SimulatedPdfDocument NewDocument()
    {
        var document = new SimulatedPdfDocument(PageCount);
        Documents.Add(document);
        return document;
    }

    public Task<PdfDocument> OpenAsync(PdfSource source, string? password, CancellationToken cancellationToken)
    {
        var call = new OpenCall { Source = source, Password = password, Token = cancellationToken };
        Calls.Add(call);
        if (Hold)
        {
            return call.Result.Task;
        }
        if (Failure is { } failure)
        {
            return Task.FromException<PdfDocument>(failure);
        }
        if (RequiredPassword is { } required && password != required)
        {
            return Task.FromException<PdfDocument>(new PdfException(PdfErrorKind.PasswordRequired));
        }
        return Task.FromResult<PdfDocument>(NewDocument());
    }
}

/// <summary>A document whose text and outline wait for nothing and that counts its renders.</summary>
internal sealed class CountingPdfDocument : PdfDocument
{
    private readonly SimulatedPdfDocument _inner;

    public CountingPdfDocument(int pageCount, bool hasOutline = false)
    {
        _inner = new SimulatedPdfDocument(pageCount);
        HasOutline = hasOutline;
    }

    public bool HasOutline { get; }

    public List<PageRenderRequest> Requests { get; } = new();

    public bool IsDisposed { get; private set; }

    public override int PageCount => _inner.PageCount;

    public override PdfDocumentInfo Info { get; } = new() { Title = null };

    public override Size GetPageSize(int pageIndex) => _inner.GetPageSize(pageIndex);

    public override ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return _inner.RenderAsync(request, cancellationToken);
    }

    public override string? GetPageLabel(int pageIndex) => null;

    public override Task<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync(CancellationToken cancellationToken) =>
        HasOutline ? _inner.GetOutlineAsync(cancellationToken) : Task.FromResult<IReadOnlyList<PdfOutlineItem>>([]);

    public override Task<PdfPageText> GetPageTextAsync(int pageIndex, CancellationToken cancellationToken) => _inner.GetPageTextAsync(pageIndex, cancellationToken);

    public override Task<IReadOnlyList<PdfLink>> GetLinksAsync(int pageIndex, CancellationToken cancellationToken) => _inner.GetLinksAsync(pageIndex, cancellationToken);

    public override void Dispose()
    {
        IsDisposed = true;
        _inner.Dispose();
    }
}

/// <summary>
/// A document with text the test chooses, laid out on a grid: a line per '\n' from (72, 100), 7 × 11 per UTF-16 unit, 20 between
/// lines, so a test can work out where any character is. Reads can be held page by page, and page labels published later.
/// </summary>
internal sealed class ScriptedPdfDocument : PdfDocument
{
    public const double Left = 72;
    public const double Top = 100;
    public const double CharWidth = 7;
    public const double LineHeight = 20;
    public const double BoxHeight = 11;

    private readonly string[] _pages;
    private readonly Dictionary<int, TaskCompletionSource<PdfPageText>> _held = new();
    private readonly CancellationTokenSource _disposed = new();
    private readonly SimulatedPdfDocument _paper;

    public ScriptedPdfDocument(params string[] pages)
    {
        _pages = pages;
        _paper = new SimulatedPdfDocument(pages.Length);
    }

    public override int PageCount => _pages.Length;

    public override PdfDocumentInfo Info { get; } = new();

    /// <summary>While true, text reads wait for <see cref="Release"/>.</summary>
    public bool HoldText { get; set; }

    /// <summary>Every page whose text was asked for, in order.</summary>
    public List<int> TextReads { get; } = new();

    /// <summary>Threads text was read on.</summary>
    public int TextCallsOnThreadPool;

    /// <summary>Pages whose text read throws.</summary>
    public HashSet<int> Unreadable { get; } = new();

    /// <summary>The links of each page.</summary>
    public Dictionary<int, IReadOnlyList<PdfLink>> Links { get; } = new();

    /// <summary>The page labels; empty until published.</summary>
    public Dictionary<int, string> Labels { get; } = new();

    public bool IsDisposed => _disposed.IsCancellationRequested;

    public static PdfPageText Layout(string text)
    {
        var boxes = new Rect[text.Length];
        var (line, column) = (0, 0);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                boxes[i] = default;
                line++;
                column = 0;
                continue;
            }
            if (text[i] == ' ')
            {
                boxes[i] = default;
                column++;
                continue;
            }
            var box = new Rect(Left + column * CharWidth, Top + line * LineHeight, CharWidth, BoxHeight);
            boxes[i] = box;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length)
            {
                boxes[++i] = box;
            }
            column++;
        }
        return new PdfPageText(text, boxes);
    }

    /// <summary>The middle of a character's box on a page.</summary>
    public Point PointOf(int page, int index) => Layout(_pages[page]).CharBoxes[index].Center;

    /// <summary>The middle of the first occurrence of a text's first character.</summary>
    public Point PointOf(int page, string text) => PointOf(page, _pages[page].IndexOf(text, StringComparison.Ordinal));

    public void Release(int page)
    {
        if (_held.Remove(page, out var gate))
        {
            gate.TrySetResult(Layout(_pages[page]));
        }
    }

    public void ReleaseAll()
    {
        HoldText = false;
        foreach (var page in _held.Keys.ToArray())
        {
            Release(page);
        }
    }

    public void PublishLabels() => RaisePageLabelsChanged();

    public override Size GetPageSize(int pageIndex) => _paper.GetPageSize(pageIndex);

    public override ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken) => _paper.RenderAsync(request, cancellationToken);

    public override string? GetPageLabel(int pageIndex) => Labels.GetValueOrDefault(pageIndex);

    public override Task<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PdfOutlineItem>>([]);

    public override Task<PdfPageText> GetPageTextAsync(int pageIndex, CancellationToken cancellationToken)
    {
        lock (TextReads)
        {
            TextReads.Add(pageIndex);
            if (Thread.CurrentThread.IsThreadPoolThread)
            {
                TextCallsOnThreadPool++;
            }
            if (Unreadable.Contains(pageIndex))
            {
                return Task.FromException<PdfPageText>(new InvalidOperationException("unreadable"));
            }
            if (_disposed.IsCancellationRequested)
            {
                return Task.FromCanceled<PdfPageText>(_disposed.Token);
            }
            if (!HoldText)
            {
                return Task.FromResult(Layout(_pages[pageIndex]));
            }
            if (!_held.TryGetValue(pageIndex, out var gate))
            {
                gate = new TaskCompletionSource<PdfPageText>(TaskCreationOptions.RunContinuationsAsynchronously);
                _held[pageIndex] = gate;
                _disposed.Token.Register(() => gate.TrySetCanceled());
            }
            return gate.Task;
        }
    }

    public override Task<IReadOnlyList<PdfLink>> GetLinksAsync(int pageIndex, CancellationToken cancellationToken) =>
        Task.FromResult(Links.GetValueOrDefault(pageIndex) ?? []);

    public override void Dispose()
    {
        _disposed.Cancel();
        _paper.Dispose();
    }
}
