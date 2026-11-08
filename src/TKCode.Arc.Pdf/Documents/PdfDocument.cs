using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// An open PDF document; also the <see cref="IPageSource"/> of a <see cref="PageViewer"/>. Every method may be called from the
/// UI thread (a backend moves the work to its own thread). After <see cref="Dispose"/>, calls still running end with
/// <see cref="OperationCanceledException"/>.
/// </summary>
public abstract class PdfDocument : IPageSource, IDisposable
{
    /// <inheritdoc />
    public abstract int PageCount { get; }

    /// <summary>The document's metadata.</summary>
    public abstract PdfDocumentInfo Info { get; }

    /// <inheritdoc />
    public abstract Size GetPageSize(int pageIndex);

    /// <inheritdoc />
    public abstract ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The label a page has in the document ("iv", "A-3"), or null when it has none or it is not known yet. This never waits: the UI
    /// thread asks it for every thumbnail, and a backend may be busy with a page. A backend that reads labels in the background
    /// raises <see cref="PageLabelsChanged"/> as more become known.
    /// </summary>
    public abstract string? GetPageLabel(int pageIndex);

    /// <summary>The table of contents; empty when the document has none.</summary>
    public abstract Task<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync(CancellationToken cancellationToken);

    /// <summary>The text of a page with the box of every character.</summary>
    public abstract Task<PdfPageText> GetPageTextAsync(int pageIndex, CancellationToken cancellationToken);

    /// <summary>The links on a page.</summary>
    public abstract Task<IReadOnlyList<PdfLink>> GetLinksAsync(int pageIndex, CancellationToken cancellationToken);

    /// <summary>Closes the document.</summary>
    public abstract void Dispose();

    /// <inheritdoc />
    public event EventHandler? PagesChanged;

    /// <summary>More of <see cref="GetPageLabel"/> became known. May be raised on any thread.</summary>
    public event EventHandler? PageLabelsChanged;

    /// <summary>Raises <see cref="PagesChanged"/> on the UI thread.</summary>
    protected void RaisePagesChanged() => PagesChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises <see cref="PageLabelsChanged"/>.</summary>
    protected void RaisePageLabelsChanged() => PageLabelsChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>The metadata of a document; what a document does not say is null.</summary>
public sealed record PdfDocumentInfo
{
    /// <summary>The title.</summary>
    public string? Title { get; init; }

    /// <summary>The author.</summary>
    public string? Author { get; init; }

    /// <summary>The subject.</summary>
    public string? Subject { get; init; }

    /// <summary>The keywords.</summary>
    public string? Keywords { get; init; }

    /// <summary>The application that made the original.</summary>
    public string? Creator { get; init; }

    /// <summary>The application that made the PDF.</summary>
    public string? Producer { get; init; }

    /// <summary>When it was created.</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>When it was last changed.</summary>
    public DateTimeOffset? Modified { get; init; }
}

/// <summary>One entry of a document's table of contents.</summary>
public sealed class PdfOutlineItem
{
    /// <summary>Creates an entry.</summary>
    public PdfOutlineItem(string title, PdfDestination? destination = null, IReadOnlyList<PdfOutlineItem>? children = null)
    {
        Title = title;
        Destination = destination;
        Children = children ?? [];
    }

    /// <summary>The text of the entry.</summary>
    public string Title { get; }

    /// <summary>Where it leads; null when it only groups its children.</summary>
    public PdfDestination? Destination { get; }

    /// <summary>The entries under it.</summary>
    public IReadOnlyList<PdfOutlineItem> Children { get; }
}

/// <summary>A place in a document.</summary>
/// <param name="PageIndex">The page, from 0.</param>
/// <param name="PointInPage">A point of the page in page coordinates; null means the whole page.</param>
public sealed record PdfDestination(int PageIndex, Point? PointInPage = null);

/// <summary>A link on a page.</summary>
/// <param name="Bounds">Its area in page coordinates.</param>
/// <param name="Destination">Where an internal link leads.</param>
/// <param name="Uri">Where an external link leads. A document is untrusted input: check the scheme before opening it.</param>
public sealed record PdfLink(Rect Bounds, PdfDestination? Destination, Uri? Uri);
