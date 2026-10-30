namespace TKCode.Arc;

/// <summary>
/// How an <see cref="AgentPlanSimulation"/> runs one step: the activity lines it reports, the time it took, and optionally
/// a failure (after the last line) with the lines and time of the retry, and a step the agent inserts after one of the
/// lines. Your app writes it; nothing ships with the package.
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

/// <summary>A step with its simulation script: what a simulation's draft, its suggestions and the steps it inserts are made of.</summary>
/// <param name="Id">The step's id (the simulation finds the script by it).</param>
/// <param name="Title">The step's title.</param>
/// <param name="Script">How the simulation runs it.</param>
public sealed record AgentPlanScriptedStep(string Id, string Title, AgentPlanStepScript Script)
{
    /// <summary>A new pending step with this id and title.</summary>
    public AgentPlanStep CreateStep(AgentPlanNoteKind noteKind = AgentPlanNoteKind.None) => new(Id, Title) { NoteKind = noteKind };
}
