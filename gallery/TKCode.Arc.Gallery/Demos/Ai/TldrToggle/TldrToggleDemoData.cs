namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented article the TL;DR toggle demos play: a Northwind engineering post about making a desktop app start
/// faster, a summary written for each of the five lengths and seven key points ranked by weight. The company, the people
/// and every number are invented; the model credited is a real one from <see cref="AiModelCatalog"/>. Demo content only.
/// </summary>
internal static class TldrToggleDemoData
{
    public static TldrArticle Article { get; } = new()
    {
        Title = "How we made our desktop app start four times faster",
        SourceName = "Northwind Engineering",
        AuthorName = "Priya Raman",
        Date = "Oct 21, 2026",
        Paragraphs =
        [
            new("intro",
                "Last spring our desktop app took 4.2 seconds to show its first workspace on a typical laptop. People opened it in the " +
                "morning, switched to email while it loaded and sometimes never came back. We set ourselves one goal for the quarter: " +
                "show something useful in under a second, on the same hardware, without dropping a single feature."),
            new("measure",
                "We started by measuring instead of guessing. A trace of a thousand cold starts showed that almost half the time went to " +
                "loading modules nobody needed yet: the editor, the calendar, the settings pages and three charting libraries. Another " +
                "quarter went to waiting on the network before drawing anything at all, even when the data on disk was only minutes old."),
            new("bootstrap",
                "Partial bootstrapping loads only what the first screen needs. The shell, the sidebar and the list view now ship in one " +
                "small bundle, and everything else is fetched in the background after the first paint, in the order people usually open " +
                "it. Opening the editor before it has arrived shows a short skeleton instead of blocking the whole window."),
            new("cache",
                "Next we drew from the local cache first. The app now renders the last known state of the workspace straight from disk " +
                "and marks anything older than a few minutes as stale. Fresh data streams in behind it, and rows update in place so " +
                "nothing jumps under the pointer."),
            new("sync",
                "Sync was the hardest part. The old client asked the server for everything since the last session and waited for the " +
                "full answer. The new one asks for a compact list of changed ids first, applies them in small batches and only then " +
                "fetches the full records for what is on screen."),
            new("results",
                "The results held up outside the lab. Median cold start dropped from 4.2 seconds to 0.9, and the slowest tenth of " +
                "machines went from 11 seconds to 2.6. Support tickets about a slow launch fell by two thirds in the first month, and " +
                "daily use rose on the older hardware we had worried about most."),
            new("next",
                "We are not done. Search still builds its index on the main thread, and large workspaces with thousands of projects take " +
                "longer to sync than we would like. Both are next on our list, and we will share what we learn."),
        ],
    };

    private const string Lead =
        "The desktop app took 4.2 seconds to show a workspace, so the team set out to show something useful in under a second " +
        "without dropping features. Traces showed half the time went to modules nobody needed yet and a quarter to waiting on the " +
        "network. Partial bootstrapping now loads only the first screen while the rest arrives in the background, and the app renders " +
        "the last known state from disk as fresh data streams in. A leaner sync applies changed ids first.";

    private const string Outcome =
        " The median cold start fell to 0.9 seconds, and support tickets about slow launches dropped by two thirds.";

    /// <summary>The summaries written for lengths 1 to 5 (about 35, 60, 85, 100 and 130 words).</summary>
    public static IReadOnlyList<string> Summaries { get; } =
    [
        "The team cut the app's median cold start from 4.2 seconds to 0.9 by loading only what the first screen needs, drawing the " +
        "last known state from disk and syncing changed ids before full records.",
        "Cold starts took 4.2 seconds, and traces showed most of that went to loading modules nobody needed yet and to waiting on the " +
        "network. Partial bootstrapping now ships only the shell, sidebar and list view first, the app draws its cached state straight " +
        "from disk, and sync fetches changed ids before full records. The median start is now 0.9 seconds.",
        Lead,
        Lead + Outcome,
        Lead + Outcome + " The slowest tenth of machines went from 11 seconds to 2.6, and daily use rose on the older hardware the " +
        "team had worried about most. Search indexing and very large workspaces are next.",
    ];

    /// <summary>Seven key points, most important first; a list of N takes the first N and shows them in article order.</summary>
    public static IReadOnlyList<TldrKeyPoint> KeyPoints { get; } =
    [
        new("bootstrap", "Partial bootstrapping loads only the shell, sidebar and list view first; everything else arrives after the first paint."),
        new("results", "Median cold start fell from 4.2 seconds to 0.9, and the slowest machines went from 11 seconds to 2.6."),
        new("cache", "The app draws the last known workspace from disk and updates stale rows in place."),
        new("measure", "Traces showed half of startup went to modules nobody needed yet and a quarter to the network."),
        new("sync", "Sync fetches a list of changed ids first, then full records only for what is on screen."),
        new("intro", "The goal was something useful on screen in under a second, with no features dropped."),
        new("next", "Search indexing on the main thread and very large workspaces are next."),
    ];

    /// <summary>The model credited in the footer (a neutral mark; no brand mark is drawn).</summary>
    public static AgentModel Model { get; } = new(AiModelCatalog.Gpt6Luna.Name, ArcIcons.Sparkles);

    /// <summary>A new simulation of the post (each card gets its own).</summary>
    public static TldrToggleSimulation Simulation() => new()
    {
        Article = Article,
        Model = Model,
        Summaries = Summaries,
        KeyPoints = KeyPoints,
    };
}
