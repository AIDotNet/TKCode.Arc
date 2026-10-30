using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The plan the agent plan demos simulate: a coding agent adding full-text search to a help center, written by a real
/// model from <see cref="AiModelCatalog"/>. Six drafted steps, three suggestions for Keep planning, a step the agent inserts
/// while running, a test step that fails once, and a summary. The task, files and numbers are invented. Demo content only;
/// the mark is a Lucide sparkle, not the vendor's logo.
/// </summary>
internal static class AgentPlanDemoData
{
    /// <summary>The goal.</summary>
    public const string Goal = "Add full-text search to the help center";

    /// <summary>The summary shown after "Done in …".</summary>
    public const string Summary = "Search now covers all 31 article fields, with a disk fallback and 14 passing tests.";

    private static readonly IBrush s_markBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xE0, 0x7A, 0x45));

    /// <summary>The model that writes the preview's plan: GPT-6.1 Sol (catalog name) with a sparkle mark in a warm color.</summary>
    public static AgentModel Model { get; } = new(AiModelCatalog.Gpt61Sol.Name, ArcIcons.Sparkles, s_markBrush);

    /// <summary>The model of the smaller plans in the other demos: Mistral Medium 3.5 (catalog name).</summary>
    public static AgentModel SmallPlansModel { get; } = new(AiModelCatalog.MistralMedium35.Name, ArcIcons.Sparkles);

    /// <summary>The six drafted steps with their scripts, in order.</summary>
    public static IReadOnlyList<AgentPlanScriptedStep> Steps { get; } =
    [
        new("inventory", "Inventory the article fields the help center stores",
            new(["Read 5 files", "Searched for articleSchema and draftSchema", "Found 31 article fields"], TimeSpan.FromSeconds(23))),
        new("index", "Build a search index from every published article",
            new(["Read jobs/publish.ts", "Created search/build-index.ts", "Edited 3 files"], TimeSpan.FromSeconds(46))),
        new("cache", "Cache the index in object storage for ten minutes",
            new(["Read lib/storage.ts", "Edited search/build-index.ts", "Ran 7 tests"], TimeSpan.FromSeconds(35))
            {
                Inserts = new("fallback", "Rebuild the index from disk when the cache is empty",
                    new(["Edited lib/storage.ts", "Ran 5 tests"], TimeSpan.FromSeconds(31))),
                InsertAfterActivity = 1
            }),
        new("rank", "Rank results by title match, then by freshness",
            new(["Edited search/rank.ts", "Ran 9 tests"], TimeSpan.FromSeconds(26))),
        new("tests", "Write tests for typos, stop words and empty queries",
            new(["Created search.test.ts", "Ran 14 tests"], TimeSpan.FromSeconds(67))
            {
                FailureReason = "3 of 14 tests failed: typo matching skips two-letter words",
                RetryActivities = ["Edited search/rank.ts", "Ran 14 tests, all passed"],
                RetryDuration = TimeSpan.FromSeconds(67)
            }),
        new("docs", "Describe the search syntax in the help center guide",
            new(["Read guide/overview.md", "Edited guide/search.md"], TimeSpan.FromSeconds(18)))
    ];

    /// <summary>The three steps Keep planning suggests, in order; each goes in before the last step.</summary>
    public static IReadOnlyList<AgentPlanScriptedStep> Suggestions { get; } =
    [
        new("highlight", "Highlight matched words in the result snippets",
            new(["Read search/snippet.ts", "Edited search/snippet.ts", "Ran 4 tests"], TimeSpan.FromSeconds(33))),
        new("zero-results", "Log searches that return no results",
            new(["Read lib/telemetry.ts", "Edited search/handler.ts"], TimeSpan.FromSeconds(21))),
        new("benchmark", "Benchmark search at 500 queries per second",
            new(["Ran a load script against staging for 60 seconds", "Measured 38 ms p99 at 500 queries per second"], TimeSpan.FromSeconds(69)))
    ];

    /// <summary>How the simulation runs a step it has no script for (one you added or renamed into a new id).</summary>
    public static AgentPlanStepScript GenericScript { get; } =
        new(["Read 2 files", "Edited 3 files", "Ran 5 tests"], TimeSpan.FromSeconds(30));
}
