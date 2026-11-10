namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AgentRoutines"/> list shows or reads out, with English defaults. Words built from values are
/// functions, so an app can apply its own grammar. Assign a new instance to <see cref="AgentRoutines.Labels"/> to change them.
/// </summary>
public sealed class AgentRoutinesLabels
{
    /// <summary>The list's title ("Routines"), also its accessible name.</summary>
    public string Title { get; init; } = "Routines";

    /// <summary>The summary under the title, from active and total counts ("3 of 4 active").</summary>
    public Func<int, int, string> Summary { get; init; } = static (active, total) => $"{active} of {total} active";

    /// <summary>A schedule in words (defaults to <see cref="AgentRoutineSchedule.Describe"/>).</summary>
    public Func<string?, string> Schedule { get; init; } = AgentRoutineSchedule.Describe;

    /// <summary>The next run, from its relative time ("Next in 3h").</summary>
    public Func<string, string> Next { get; init; } = static time => $"Next {time}";

    /// <summary>The line of a paused routine ("Paused").</summary>
    public string Paused { get; init; } = "Paused";

    /// <summary>The line while a run started with Run now is going ("Running now").</summary>
    public string Running { get; init; } = "Running now";

    /// <summary>A relative time from the time and now ("5m ago", "in 3h").</summary>
    public Func<DateTimeOffset, DateTimeOffset, string> Time { get; init; } = AiAgentFormat.Relative;

    /// <summary>A run's duration ("42s").</summary>
    public Func<TimeSpan, string> Duration { get; init; } = AiAgentFormat.Elapsed;

    /// <summary>The outcome words ("Succeeded", "Failed", "Skipped").</summary>
    public Func<AgentRoutineOutcome, string> Outcome { get; init; } = static outcome => outcome switch
    {
        AgentRoutineOutcome.Failed => "Failed",
        AgentRoutineOutcome.Skipped => "Skipped",
        _ => "Succeeded"
    };

    /// <summary>The history strip's spoken summary, from succeeded and total runs ("8 of 10 recent runs succeeded").</summary>
    public Func<int, int, string> History { get; init; } = static (ok, total) => $"{ok} of {total} recent runs succeeded";

    /// <summary>The headings of an open routine: "Instructions", "Recent runs".</summary>
    public string Instructions { get; init; } = "Instructions";

    /// <inheritdoc cref="Instructions"/>
    public string RecentRuns { get; init; } = "Recent runs";

    /// <summary>An open routine without runs ("No runs yet").</summary>
    public string NoRuns { get; init; } = "No runs yet";

    /// <summary>The Run now button ("Run now").</summary>
    public string RunNow { get; init; } = "Run now";

    /// <summary>The accessible names of a routine's switch and its expand button, from its name.</summary>
    public Func<string, string> EnableName { get; init; } = static name => $"Run {name} on schedule";

    /// <inheritdoc cref="EnableName"/>
    public Func<string, string> DetailsName { get; init; } = static name => $"Details of {name}";

    /// <summary>What screen readers hear when a run ends, from the name and the outcome ("Morning digest: Succeeded").</summary>
    public Func<string, string, string> RunEnded { get; init; } = static (name, outcome) => $"{name}: {outcome}";

    /// <summary>The summary of a run started with Run now when the built-in stand-in runs it ("Run by hand").</summary>
    public string ManualRun { get; init; } = "Run by hand";
}
