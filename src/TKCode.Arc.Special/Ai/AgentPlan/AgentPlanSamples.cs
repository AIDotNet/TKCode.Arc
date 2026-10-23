using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// How the built-in simulation runs one step: the activity lines it reports, the time it took, and optionally a failure
/// (after the last line) with the lines and time of the retry, and a step the agent inserts after one of the lines.
/// </summary>
public sealed class AgentPlanStepScript
{
    /// <summary>Creates a script that reports <paramref name="activities"/> and finishes after <paramref name="duration"/>.</summary>
    public AgentPlanStepScript(IReadOnlyList<string> activities, TimeSpan duration)
    {
        Activities = activities;
        Duration = duration;
    }

    /// <summary>The activity lines, in order ("Read 5 files", "Ran 9 tests").</summary>
    public IReadOnlyList<string> Activities { get; }

    /// <summary>The time shown once the step is done (the retry's time when it failed first).</summary>
    public TimeSpan Duration { get; }

    /// <summary>When set, the step fails once after its last line with this reason and waits for Retry or Skip.</summary>
    public string? FailureReason { get; init; }

    /// <summary>The lines a retry adds under the ones already shown.</summary>
    public IReadOnlyList<string> RetryActivities { get; init; } = [];

    /// <summary>The time shown when the step is done after a retry (null: <see cref="Duration"/>).</summary>
    public TimeSpan? RetryDuration { get; init; }

    /// <summary>A step the agent inserts after this one while it runs (null: none).</summary>
    public AgentPlanScriptedStep? Inserts { get; init; }

    /// <summary>The 0-based activity line after which <see cref="Inserts"/> is inserted.</summary>
    public int InsertAfterActivity { get; init; }
}

/// <summary>A step with its simulation script: what the sample draft, the suggestions and the inserted step are made of.</summary>
/// <param name="Id">The step's id (the simulation finds the script by it).</param>
/// <param name="Title">The step's title.</param>
/// <param name="Script">How the simulation runs it.</param>
public sealed record AgentPlanScriptedStep(string Id, string Title, AgentPlanStepScript Script)
{
    /// <summary>A new pending step with this id and title.</summary>
    public AgentPlanStep CreateStep(AgentPlanNoteKind noteKind = AgentPlanNoteKind.None) => new(Id, Title) { NoteKind = noteKind };
}

/// <summary>
/// The invented sample an <see cref="AgentPlan"/> shows and simulates when it is given no steps and no status: a coding
/// agent adding full-text search to a help center. Six drafted steps, three suggestions for Keep planning, a step the
/// agent inserts while running, a test step that fails once, and a summary. Every name and number is made up.
/// </summary>
public static class AgentPlanSamples
{
    /// <summary>The sample goal.</summary>
    public const string Goal = "Add full-text search to the help center";

    /// <summary>The sample summary shown after "Done in …".</summary>
    public const string Summary = "Search now covers all 31 article fields, with a disk fallback and 14 passing tests.";

    /// <summary>The sample model's name (invented).</summary>
    public const string ModelName = "Lumen 2 Pro";

    private static readonly IBrush s_markBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xE0, 0x7A, 0x45));

    /// <summary>A new instance of the sample model: an invented name and a sparkle mark in a fixed warm color.</summary>
    public static AgentModel CreateModel() => new(ModelName, ArcIcons.Sparkles, s_markBrush);

    internal static AgentModel SampleModel { get; } = CreateModel();

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

    /// <summary>New pending instances of the six drafted steps.</summary>
    public static List<AgentPlanStep> CreateSteps() => Steps.Select(s => s.CreateStep()).ToList();
}
