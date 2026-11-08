namespace TKCode.Arc;

/// <summary>
/// The text of the most recently used pages of one document (64, spec section 5.5), shared by search and selection: one read per
/// page, whoever asks first. A page that cannot be read counts as a page without text, so it neither ends a search nor comes
/// back on every pointer move. Reading is only ever awaited, never waited for: a backend may be busy rendering a page.
/// </summary>
internal sealed class PdfPageTextCache : IDisposable
{
    /// <summary>How many pages are kept.</summary>
    public const int Capacity = 64;

    private static readonly PdfPageText s_noText = new(string.Empty, []);

    private readonly PdfDocument _document;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private readonly Dictionary<int, LinkedListNode<(int Page, Task<PdfPageText> Text)>> _pages = new();
    private readonly LinkedList<(int Page, Task<PdfPageText> Text)> _recent = new();

    public PdfPageTextCache(PdfDocument document)
    {
        _document = document;
    }

    /// <summary>How many pages are held (tests).</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _pages.Count;
            }
        }
    }

    /// <summary>The text of a page if it has arrived; never asks for anything.</summary>
    public PdfPageText? TryGet(int pageIndex)
    {
        lock (_gate)
        {
            if (_pages.TryGetValue(pageIndex, out var node) && node.Value.Text.IsCompletedSuccessfully)
            {
                Touch(node);
                return node.Value.Text.Result;
            }
            return null;
        }
    }

    /// <summary>
    /// The text of a page. Cancelling ends this wait only: the read stays in the cache for whoever asks next. Ends with
    /// <see cref="OperationCanceledException"/> when the document is gone.
    /// </summary>
    public Task<PdfPageText> GetAsync(int pageIndex, CancellationToken cancellationToken)
    {
        Task<PdfPageText> text;
        lock (_gate)
        {
            if (_pages.TryGetValue(pageIndex, out var node) && !node.Value.Text.IsCanceled)
            {
                Touch(node);
                text = node.Value.Text;
            }
            else
            {
                if (node is not null)
                {
                    _recent.Remove(node);
                    _pages.Remove(pageIndex);
                }
                text = ReadAsync(pageIndex);
                _pages[pageIndex] = _recent.AddFirst((pageIndex, text));
                while (_recent.Count > Capacity)
                {
                    _pages.Remove(_recent.Last!.Value.Page);
                    _recent.RemoveLast();
                }
            }
        }
        return cancellationToken.CanBeCanceled ? text.WaitAsync(cancellationToken) : text;
    }

    private void Touch(LinkedListNode<(int Page, Task<PdfPageText> Text)> node)
    {
        if (!ReferenceEquals(_recent.First, node))
        {
            _recent.Remove(node);
            _recent.AddFirst(node);
        }
    }

    private async Task<PdfPageText> ReadAsync(int pageIndex)
    {
        try
        {
            return await _document.GetPageTextAsync(pageIndex, _lifetime.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException and not OutOfMemoryException)
        {
            return s_noText;
        }
    }

    public void Dispose() => _lifetime.Cancel();
}
