namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="SourceCards"/> and <see cref="SourceCitation"/> show or say, in English by default. Replace the
/// whole object to localize or reword; counts and names are format functions.
/// </summary>
public sealed class SourceCardsLabels
{
    /// <summary>The English defaults.</summary>
    public static SourceCardsLabels Default { get; } = new();

    /// <summary>The accessible name of the list of cards.</summary>
    public string List { get; init; } = "Sources";

    /// <summary>The status word while the search runs.</summary>
    public string Searching { get; init; } = "Searching";

    /// <summary>The count while the search runs, from the sources so far and the expected total: "3 of 7"; "3 found" when
    /// the total is not known; empty before the first.</summary>
    public Func<int, int?, string> Progress { get; init; } = (count, total) => total is { } t
        ? $"{count} of {t}"
        : count > 0 ? $"{count} found" : string.Empty;

    /// <summary>The count once the search is done: "1 source", "7 sources".</summary>
    public Func<int, string> Count { get; init; } = count => count == 1 ? "1 source" : $"{count} sources";

    /// <summary>The time after the count when done: "2.1s", "1m 5s".</summary>
    public Func<TimeSpan, string> Duration { get; init; } = SourceCardsFormat.Duration;

    /// <summary>The status word of a failed search.</summary>
    public string Failed { get; init; } = "Search failed";

    /// <summary>The status word of a search that found nothing.</summary>
    public string Empty { get; init; } = "No sources found";

    /// <summary>The button after a failed search.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The button after a search that found nothing.</summary>
    public string SearchAgain { get; init; } = "Search again";

    /// <summary>The toggle that opens the stack.</summary>
    public string ShowAll { get; init; } = "Show all";

    /// <summary>The toggle that closes it again.</summary>
    public string ShowLess { get; init; } = "Show less";

    /// <summary>The stack's count: "+4 more".</summary>
    public Func<int, string> More { get; init; } = count => $"+{count} more";

    /// <summary>A card's name from its number, title and domain, and whether it opens a link: "2: Request: text() method,
    /// webref.example, opens in your browser".</summary>
    public Func<int, string, string, bool, string> CardName { get; init; } = (number, title, domain, opensLink) =>
        opensLink ? $"{number}: {title}, {domain}, opens in your browser" : $"{number}: {title}, {domain}";

    /// <summary>The stack's name from the hidden count and their sites: "4 more sources: WebRef, DevAnswers. Show all".</summary>
    public Func<int, string, string> StackName { get; init; } = (count, sites) =>
        $"{count} more {(count == 1 ? "source" : "sources")}: {sites}. Show all";

    /// <summary>A citation's name from its number and its source's title and domain (null before the source arrived):
    /// "Source 2: Request: text() method, webref.example".</summary>
    public Func<int, string?, string?, string> CitationName { get; init; } = (number, title, domain) =>
        title is null ? $"Source {number}" : $"Source {number}: {title}, {domain}";

    /// <summary>What the list reports while it fills (its busy state).</summary>
    public string Busy { get; init; } = "Loading sources";

    /// <summary>Said once when a search starts: "Searching for webhook signature verification".</summary>
    public Func<string?, string> AnnounceSearching { get; init; } = query =>
        string.IsNullOrWhiteSpace(query) ? "Searching" : $"Searching for {query}";

    /// <summary>Said once when the search is done: "7 sources found".</summary>
    public Func<int, string> AnnounceFound { get; init; } = count => count == 1 ? "1 source found" : $"{count} sources found";

    /// <summary>Said once when the search failed, with its detail: "Search failed. The provider timed out."</summary>
    public Func<string?, string> AnnounceFailed { get; init; } = error =>
        string.IsNullOrWhiteSpace(error) ? "Search failed" : $"Search failed. {error}";

    /// <summary>Said when the self-playing sample starts.</summary>
    public string AnnounceSimulated { get; init; } = "This search is simulated; card clicks open nothing.";
}
