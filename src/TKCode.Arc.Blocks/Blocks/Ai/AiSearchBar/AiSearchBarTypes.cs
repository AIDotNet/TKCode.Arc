using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where an <see cref="AiSearchBar"/> stands.</summary>
public enum AiSearchBarState
{
    /// <summary>Nothing asked yet (or cleared): the suggested questions show.</summary>
    Idle,

    /// <summary>A question is being answered.</summary>
    Searching,

    /// <summary>The answer, its sources and the results show.</summary>
    Answered,

    /// <summary>The last search failed; the error and a retry button show.</summary>
    Failed
}

/// <summary>A source an answer cites; numbered by its place in <see cref="AiSearchAnswer.Sources"/>, from 1.</summary>
public sealed record AiSearchSource
{
    /// <summary>The source's title ("Leave policy 2026").</summary>
    public required string Title { get; init; }

    /// <summary>Where it lives ("Handbook › People"); null leaves it out.</summary>
    public string? Location { get; init; }

    /// <summary>Your link or identifier, handed back by <see cref="AiSearchBar.SourceInvoked"/>.</summary>
    public string? Link { get; init; }
}

/// <summary>A classic search result under the answer.</summary>
public sealed record AiSearchResult
{
    /// <summary>The result's title.</summary>
    public required string Title { get; init; }

    /// <summary>A line or two of matching text; null leaves it out.</summary>
    public string? Snippet { get; init; }

    /// <summary>Where it lives ("Docs › Finance"); null leaves it out.</summary>
    public string? Location { get; init; }

    /// <summary>Your link or identifier, handed back by <see cref="AiSearchBar.ResultInvoked"/>.</summary>
    public string? Link { get; init; }
}

/// <summary>
/// An answer to a question: the text, with citations written as <c>[1]</c>, <c>[2]</c> pointing into
/// <see cref="Sources"/>, the classic results, and follow-up questions.
/// </summary>
public sealed record AiSearchAnswer
{
    /// <summary>The answer in plain text; <c>[n]</c> becomes a citation button for source n.</summary>
    public required string Text { get; init; }

    /// <summary>The sources the answer cites, in citation order.</summary>
    public IReadOnlyList<AiSearchSource> Sources { get; init; } = [];

    /// <summary>The classic results, best first.</summary>
    public IReadOnlyList<AiSearchResult> Results { get; init; } = [];

    /// <summary>Questions to ask next; empty hides them.</summary>
    public IReadOnlyList<string> FollowUps { get; init; } = [];
}

/// <summary>Arguments of <see cref="AiSearchBar.QuerySubmitted"/>.</summary>
public sealed class AiSearchQueryEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiSearchQueryEventArgs(RoutedEvent routedEvent, string query) : base(routedEvent) => Query = query;

    /// <summary>The question asked.</summary>
    public string Query { get; }
}

/// <summary>Arguments of <see cref="AiSearchBar.SourceInvoked"/>.</summary>
public sealed class AiSearchSourceEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiSearchSourceEventArgs(RoutedEvent routedEvent, AiSearchSource source, int number) : base(routedEvent)
    {
        SearchSource = source;
        Number = number;
    }

    /// <summary>The source that was opened.</summary>
    public AiSearchSource SearchSource { get; }

    /// <summary>Its citation number, from 1.</summary>
    public int Number { get; }
}

/// <summary>Arguments of <see cref="AiSearchBar.ResultInvoked"/>.</summary>
public sealed class AiSearchResultEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiSearchResultEventArgs(RoutedEvent routedEvent, AiSearchResult result) : base(routedEvent) => Result = result;

    /// <summary>The result that was opened.</summary>
    public AiSearchResult Result { get; }
}

/// <summary>
/// Every word an <see cref="AiSearchBar"/> shows or reads out, with English defaults. Assign a new instance to
/// <see cref="AiSearchBar.Labels"/>.
/// </summary>
public sealed record AiSearchBarLabels
{
    /// <summary>The field's placeholder ("Ask a question or search…").</summary>
    public string Placeholder { get; init; } = "Ask a question or search…";

    /// <summary>The field's accessible name ("Search").</summary>
    public string Field { get; init; } = "Search";

    /// <summary>The submit button ("Ask").</summary>
    public string Submit { get; init; } = "Ask";

    /// <summary>Accessible name of the clear button ("Clear search").</summary>
    public string Clear { get; init; } = "Clear search";

    /// <summary>The heading over the suggested questions ("Try asking").</summary>
    public string Suggestions { get; init; } = "Try asking";

    /// <summary>The line shown while searching ("Reading your workspace…").</summary>
    public string Searching { get; init; } = "Reading your workspace…";

    /// <summary>The label over the answer ("AI answer").</summary>
    public string AnswerHeading { get; init; } = "AI answer";

    /// <summary>The heading over the sources ("Sources").</summary>
    public string Sources { get; init; } = "Sources";

    /// <summary>The heading over the follow-up questions ("Ask next").</summary>
    public string FollowUps { get; init; } = "Ask next";

    /// <summary>The heading over the results ("Results").</summary>
    public string Results { get; init; } = "Results";

    /// <summary>Shown when the answer has no results ("No other results").</summary>
    public string NoResults { get; init; } = "No other results";

    /// <summary>The error line when the search throws without a message.</summary>
    public string Failed { get; init; } = "Something went wrong while answering. Try again.";

    /// <summary>The retry button ("Try again").</summary>
    public string Retry { get; init; } = "Try again";

    /// <summary>A citation's accessible name from its number and the source's title ("Source 2: Travel policy").</summary>
    public Func<int, string, string> Citation { get; init; } = static (number, title) => $"Source {number}: {title}";

    /// <summary>Announced when an answer arrives, from the number of results ("Answer ready, 4 results").</summary>
    public Func<int, string> Answered { get; init; } = static count => count == 1 ? "Answer ready, 1 result" : $"Answer ready, {count} results";
}

/// <summary>Invented sample data for <see cref="AiSearchBar"/>: its suggestions and the simulated answers it gives when no <see cref="AiSearchBar.AnswerProvider"/> is set.</summary>
public static class AiSearchBarSamples
{
    /// <summary>How long a simulated answer takes (0.9 s).</summary>
    public static readonly TimeSpan SimulatedDelay = TimeSpan.FromMilliseconds(900);

    /// <summary>Three questions people often ask an internal handbook.</summary>
    public static IReadOnlyList<string> Questions() =>
    [
        "How many vacation days do new hires get?",
        "What can I expense on a work trip?",
        "Who approves a new laptop?",
    ];

    private static readonly AiSearchResult[] s_documents =
    [
        new() { Title = "Leave policy 2026", Location = "Handbook › People", Link = "doc:leave",
            Snippet = "Full-time staff earn 25 days of paid leave a year, accrued monthly from the first day." },
        new() { Title = "Public holidays by office", Location = "Handbook › People", Link = "doc:holidays",
            Snippet = "Each office observes its local public holidays on top of paid leave." },
        new() { Title = "Travel and expenses", Location = "Handbook › Finance", Link = "doc:travel",
            Snippet = "Book economy for flights under six hours. Meals are covered up to the daily rate for the city." },
        new() { Title = "Daily meal rates", Location = "Finance › Tables", Link = "doc:rates",
            Snippet = "Rates range from 45 to 90 a day depending on the city; receipts are needed above 25." },
        new() { Title = "Equipment requests", Location = "Handbook › IT", Link = "doc:equipment",
            Snippet = "Laptops are replaced every three years. Your manager approves, IT orders." },
        new() { Title = "Onboarding checklist", Location = "Handbook › People", Link = "doc:onboarding",
            Snippet = "Week one: accounts, laptop, buddy lunch and a walk through the leave and expense policies." },
    ];

    /// <summary>The simulated answer to <paramref name="query"/>: one of three prepared answers, or a short general one.</summary>
    public static AiSearchAnswer Answer(string query)
    {
        var q = query.ToLowerInvariant();
        if (q.Contains("vacation") || q.Contains("leave") || q.Contains("holiday") || q.Contains("days off"))
        {
            return new AiSearchAnswer
            {
                Text = "New hires get 25 days of paid leave a year, earned month by month from their first day [1]. Local public holidays come on top and depend on the office [2]. Leave can be booked once the first month is done.",
                Sources = [Source(0), Source(1)],
                Results = Rank(q, 0, 1, 5),
                FollowUps = ["Can unused leave carry over?", "How do I book time off?"],
            };
        }
        if (q.Contains("expense") || q.Contains("trip") || q.Contains("travel") || q.Contains("meal"))
        {
            return new AiSearchAnswer
            {
                Text = "On a work trip you can expense economy flights under six hours, hotels within the city cap and meals up to the daily rate [1]. Daily rates run from 45 to 90 depending on the city, and receipts are needed for anything above 25 [2].",
                Sources = [Source(2), Source(3)],
                Results = Rank(q, 2, 3),
                FollowUps = ["What is the hotel cap in Lisbon?", "How fast are expenses paid back?"],
            };
        }
        if (q.Contains("laptop") || q.Contains("equipment") || q.Contains("monitor") || q.Contains("approve"))
        {
            return new AiSearchAnswer
            {
                Text = "Your manager approves a new laptop, then IT orders it [1]. Laptops are replaced every three years, or sooner when one breaks.",
                Sources = [Source(4)],
                Results = Rank(q, 4, 5),
                FollowUps = ["Can I pick a different model?"],
            };
        }
        return new AiSearchAnswer
        {
            Text = $"I could not find a direct answer to \"{query.Trim()}\". The onboarding checklist is the closest match and links to the main policies [1].",
            Sources = [Source(5)],
            Results = Rank(q, 5, 0, 2, 4),
            FollowUps = Questions(),
        };
    }

    private static AiSearchSource Source(int index)
    {
        var doc = s_documents[index];
        return new AiSearchSource { Title = doc.Title, Location = doc.Location, Link = doc.Link };
    }

    private static IReadOnlyList<AiSearchResult> Rank(string query, params int[] indices)
    {
        // The cited documents first, then any other document sharing a word with the question.
        var results = indices.Select(i => s_documents[i]).ToList();
        var words = query.Split([' ', '?', ',', '.'], StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToArray();
        foreach (var doc in s_documents)
        {
            if (!results.Contains(doc) && words.Any(w => (doc.Snippet ?? "").Contains(w, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(doc);
            }
        }
        return results;
    }
}
