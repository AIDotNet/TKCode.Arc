using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A scripted agent run for <see cref="AgentRunSimulation"/>: the agent, its steps with their timing, the outcome and the
/// notices the simulation announces for each action. <see cref="AgentStatusSamples"/> has two invented runs. Data only.
/// </summary>
public sealed class AgentRunScenario
{
    /// <summary>A short id ("coding", "research").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The agent that runs.</summary>
    public AgentModel Agent { get; init; } = new();

    /// <summary>The steps, in order.</summary>
    public IReadOnlyList<AgentRunScenarioStep> Steps { get; init; } = [];

    /// <summary>The outcome shown after "Done in …" ("5 files touched").</summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>The outcome when an approval was denied ("5 files touched, not deployed"); null uses <see cref="Outcome"/>.</summary>
    public string? DeniedOutcome { get; init; }

    /// <summary>The label the running step takes when the run is stopped.</summary>
    public string StoppedStepLabel { get; init; } = "Cancelled at your request";

    /// <summary>Announced after an approval.</summary>
    public string ApprovedNotice { get; init; } = "Approved. The action is only simulated here.";

    /// <summary>Announced after a denial.</summary>
    public string DeniedNotice { get; init; } = "Denied. Nothing else was run.";

    /// <summary>Announced after a pause.</summary>
    public string PausedNotice { get; init; } = "Paused. The run holds where it is.";

    /// <summary>Announced after a resume.</summary>
    public string ResumedNotice { get; init; } = "Resumed.";

    /// <summary>Announced after a retry.</summary>
    public string RetryNotice { get; init; } = "Trying the failed step again.";

    /// <summary>Announced after a stop.</summary>
    public string StoppedNotice { get; init; } = "Run stopped. Nothing else happens in this preview.";

    /// <summary>Announced after Open run.</summary>
    public string OpenRunNotice { get; init; } = "The run log would open here; this preview only simulates it.";

    /// <inheritdoc />
    public override string ToString() => Id;
}

/// <summary>
/// One step of an <see cref="AgentRunScenario"/>: its present- and past-tense labels, icon and active duration, an optional
/// count that ticks in place, an optional approval gate before it runs and an optional failure on its first attempt.
/// </summary>
public sealed class AgentRunScenarioStep
{
    /// <summary>A stable id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The label while it runs, in the present tense ("Scanning 9 modules"); the count follows it.</summary>
    public string ActiveLabel { get; init; } = string.Empty;

    /// <summary>The label once done, in the past tense ("Scanned 9 modules").</summary>
    public string DoneLabel { get; init; } = string.Empty;

    /// <summary>A 24 × 24 stroked glyph.</summary>
    public Geometry? Icon { get; init; }

    /// <summary>How long it runs (active time, pauses left out); after an approval, how long it runs once approved.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>A count shown as "k of total" that starts at 1 and ticks up every <see cref="ProgressInterval"/>; null for none.</summary>
    public int? ProgressTotal { get; init; }

    /// <summary>The time between two ticks of the count.</summary>
    public TimeSpan ProgressInterval { get; init; }

    /// <summary>
    /// The pill's request when the step waits for an approval before it runs ("Waiting on you to deploy to staging");
    /// null for a step without a gate. The run waits for as long as it takes.
    /// </summary>
    public string? Request { get; init; }

    /// <summary>The step's label while it waits for the approval.</summary>
    public string? WaitingLabel { get; init; }

    /// <summary>The step's label when the approval was denied.</summary>
    public string? SkippedLabel { get; init; }

    /// <summary>
    /// The count at which the first attempt fails: the failure comes when the next tick would; null for a step that never
    /// fails. The run waits for Retry for as long as it takes, then counts on from here.
    /// </summary>
    public int? FailAtProgress { get; init; }

    /// <summary>The pill's label after the failure ("Fetch failed · 2 pages did not load").</summary>
    public string? Failure { get; init; }

    /// <summary>The step's label after the failure ("2 pages did not load").</summary>
    public string? FailedLabel { get; init; }

    /// <summary>How long the retried attempt runs.</summary>
    public TimeSpan RetryDuration { get; init; }

    /// <inheritdoc />
    public override string ToString() => Id;
}
