namespace TKCode.Arc;

public partial class KnowledgeBase
{
    private CancellationTokenSource? _searchCancellation;
    private string? _searchError;
    private bool _searched;

    /// <summary>Runs the search preview now for <see cref="SearchText"/>, without the typing delay.</summary>
    public Task RefreshSearchAsync() => SearchAsync(SearchText, waitFirst: false);

    /// <summary>Cancels a pending search; with <paramref name="immediate"/> the next one starts without the typing delay.</summary>
    private void StartSearch(bool immediate) => _ = SearchAsync(SearchText, waitFirst: !immediate);

    private void CancelSearch()
    {
        if (_searchCancellation is { } previous)
        {
            previous.Cancel();
            previous.Dispose();
            _searchCancellation = null;
        }
        IsSearching = false;
    }

    private async Task SearchAsync(string? query, bool waitFirst)
    {
        CancelSearch();
        _searchError = null;
        if (string.IsNullOrWhiteSpace(query))
        {
            _searched = false;
            Passages = Array.Empty<KnowledgePassage>();
            RenderSearchStatus();
            RenderPassages();
            return;
        }
        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        var token = cancellation.Token;
        IsSearching = true;
        RenderSearchStatus();
        IReadOnlyList<KnowledgePassage> result;
        try
        {
            if (waitFirst && SearchDelay > TimeSpan.Zero)
            {
                await Delay(SearchDelay, token);
            }
            token.ThrowIfCancellationRequested();
            var trimmed = query.Trim();
            result = SearchHandler is { } handler
                ? await handler(trimmed, MaxPassages, token)
                : KnowledgeBaseText.Search(Items, trimmed, MaxPassages);
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            result = Array.Empty<KnowledgePassage>();
            _searchError = exception.Message;
        }
        if (!ReferenceEquals(_searchCancellation, cancellation))
        {
            return;
        }
        _searchCancellation = null;
        cancellation.Dispose();
        IsSearching = false;
        _searched = true;
        Passages = (result ?? Array.Empty<KnowledgePassage>()).Take(MaxPassages).ToArray();
        RenderSearchStatus();
        RenderPassages();
        Announce(SearchStatusSentence(query.Trim()));
    }

    private string SearchStatusSentence(string query)
    {
        var labels = Labels;
        if (_searchError is { } error)
        {
            return labels.SearchFailed(error);
        }
        return _passages.Count == 0 ? labels.NoPassages(query) : labels.PassageCount(_passages.Count);
    }

    private void RenderSearchStatus()
    {
        var query = SearchText?.Trim();
        string text;
        if (string.IsNullOrEmpty(query))
        {
            text = Labels.SearchIdle;
        }
        else if (IsSearching && !_searched)
        {
            text = Labels.Searching;
        }
        else if (IsSearching)
        {
            // A new query while results show: keep the old line until the new results land, so it does not flicker.
            return;
        }
        else
        {
            text = SearchStatusSentence(query);
        }
        if (_searchStatus is not null && !Equals(_searchStatus.Content, text))
        {
            _searchStatus.Content = text;
        }
    }
}
