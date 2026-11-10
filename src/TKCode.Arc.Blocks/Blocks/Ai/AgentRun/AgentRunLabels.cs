namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AgentRun"/> shows or reads out, with English defaults. Words built from values are functions,
/// so an app can apply its own grammar. Assign a new instance to <see cref="AgentRun.Labels"/> to change them.
/// </summary>
public sealed class AgentRunLabels
{
    /// <summary>Accessible name of the run ("Agent run").</summary>
    public string Region { get; init; } = "Agent run";

    /// <summary>The small heading over the goal ("Goal").</summary>
    public string Goal { get; init; } = "Goal";

    /// <summary>The state words: Idle, Running, Completed, Stopped, Failed.</summary>
    public Func<AgentRunState, string> State { get; init; } = static state => state switch
    {
        AgentRunState.Running => "Running",
        AgentRunState.Completed => "Completed",
        AgentRunState.Stopped => "Stopped",
        AgentRunState.Failed => "Failed",
        _ => "Not started"
    };

    /// <summary>The kind of a step, read before its title ("Thinking", "Tool call", "Files", "Message").</summary>
    public Func<AgentRunStepKind, string> Kind { get; init; } = static kind => kind switch
    {
        AgentRunStepKind.Thinking => "Thinking",
        AgentRunStepKind.ToolCall => "Tool call",
        AgentRunStepKind.FileEdit => "Files",
        _ => "Message"
    };

    /// <summary>The status of a step for screen readers ("waiting", "running", "done", "failed", "stopped").</summary>
    public Func<AgentRunStepStatus, string> StepStatus { get; init; } = static status => status switch
    {
        AgentRunStepStatus.Running => "running",
        AgentRunStepStatus.Done => "done",
        AgentRunStepStatus.Failed => "failed",
        AgentRunStepStatus.Stopped => "stopped",
        _ => "waiting"
    };

    /// <summary>The time the run has taken ("1m 05s").</summary>
    public Func<TimeSpan, string> Elapsed { get; init; } = AiAgentFormat.Elapsed;

    /// <summary>The time a step took ("2.1s").</summary>
    public Func<TimeSpan, string> Duration { get; init; } = AiAgentFormat.Duration;

    /// <summary>The cost so far from the amount and the currency symbol ("$0.042").</summary>
    public Func<decimal, string, string> Cost { get; init; } = AiAgentFormat.Cost;

    /// <summary>The tokens used so far ("12,480 tokens").</summary>
    public Func<long, string> Tokens { get; init; } = static count => $"{AiAgentFormat.Count(count)} tokens";

    /// <summary>The progress line from the finished and total step counts ("4 of 6 steps").</summary>
    public Func<int, int, string> Progress { get; init; } = static (done, total) => $"{done} of {total} steps";

    /// <summary>Accessible names of the facts: "Time", "Cost", "Tokens".</summary>
    public string TimeName { get; init; } = "Time";

    /// <inheritdoc cref="TimeName"/>
    public string CostName { get; init; } = "Cost";

    /// <inheritdoc cref="TimeName"/>
    public string TokensName { get; init; } = "Tokens";

    /// <summary>The section headings of a tool call's detail: "Input" and "Result".</summary>
    public string Input { get; init; } = "Input";

    /// <inheritdoc cref="Input"/>
    public string Output { get; init; } = "Result";

    /// <summary>A file's change word ("added", "changed", "deleted"), read after its path.</summary>
    public Func<AgentRunFileChange, string> FileChange { get; init; } = static change => change switch
    {
        AgentRunFileChange.Created => "added",
        AgentRunFileChange.Deleted => "deleted",
        _ => "changed"
    };

    /// <summary>The file count of a file edit step ("2 files").</summary>
    public Func<int, string> FileCount { get; init; } = static count => count == 1 ? "1 file" : $"{count} files";

    /// <summary>The Stop button ("Stop").</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The button after a stopped or failed run ("Retry").</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The button after a finished run ("Run again").</summary>
    public string RunAgain { get; init; } = "Run again";

    /// <summary>The empty timeline before the first step ("Waiting for the agent to start").</summary>
    public string Waiting { get; init; } = "Waiting for the agent to start";

    /// <summary>The note of a stopped run ("Stopped. Nothing after this point ran.").</summary>
    public string StoppedNote { get; init; } = "Stopped. Nothing after this point ran.";

    /// <summary>The heading of a failed run's note ("Run failed.").</summary>
    public string FailedNote { get; init; } = "Run failed.";

    /// <summary>The accessible name of a step header, from kind, title, status and time ("Tool call: Search the code, done, 1.2s").</summary>
    public Func<string, string, string, string?, string> StepName { get; init; } =
        static (kind, title, status, time) => time is null ? $"{kind}: {title}, {status}" : $"{kind}: {title}, {status}, {time}";

    /// <summary>What screen readers hear when a step starts ("Running: Search the code").</summary>
    public Func<string, string> StepStarted { get; init; } = static title => $"Running: {title}";

    /// <summary>What screen readers hear when the run ends, from the state word and the time ("Completed in 1m 05s").</summary>
    public Func<string, string, string> RunEnded { get; init; } = static (state, time) => $"{state} in {time}";
}
