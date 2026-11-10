namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AgentTasks"/> board shows or reads out, with English defaults. Words built from values are
/// functions, so an app can apply its own grammar. Assign a new instance to <see cref="AgentTasks.Labels"/> to change them.
/// </summary>
public sealed class AgentTasksLabels
{
    /// <summary>The board's title ("Delegated tasks"), also its accessible name.</summary>
    public string Title { get; init; } = "Delegated tasks";

    /// <summary>The column titles: Queued, Running, Needs you, Done (Failed shares Needs you).</summary>
    public Func<AgentTaskStatus, string> Column { get; init; } = static status => status switch
    {
        AgentTaskStatus.Running => "Running",
        AgentTaskStatus.NeedsInput or AgentTaskStatus.Failed => "Needs you",
        AgentTaskStatus.Done => "Done",
        _ => "Queued"
    };

    /// <summary>The status word on a card ("Queued", "Running", "Needs input", "Failed", "Done").</summary>
    public Func<AgentTaskStatus, string> Status { get; init; } = static status => status switch
    {
        AgentTaskStatus.Running => "Running",
        AgentTaskStatus.NeedsInput => "Needs input",
        AgentTaskStatus.Failed => "Failed",
        AgentTaskStatus.Done => "Done",
        _ => "Queued"
    };

    /// <summary>The summary under the title, from running and waiting counts ("3 running · 2 need you").</summary>
    public Func<int, int, string> Summary { get; init; } = static (running, waiting) =>
        waiting == 0 ? $"{running} running" : $"{running} running · {waiting} {(waiting == 1 ? "needs" : "need")} you";

    /// <summary>The filter chip that shows every agent ("All agents").</summary>
    public string AllAgents { get; init; } = "All agents";

    /// <summary>The accessible name of the agent filter ("Filter by agent").</summary>
    public string Filter { get; init; } = "Filter by agent";

    /// <summary>A column with no task ("Nothing here").</summary>
    public string EmptyColumn { get; init; } = "Nothing here";

    /// <summary>A progress as shown ("64%").</summary>
    public Func<double, string> Percent { get; init; } = static value => $"{Math.Round(value * 100):0}%";

    /// <summary>A relative time from the time and now ("5m ago").</summary>
    public Func<DateTimeOffset, DateTimeOffset, string> Updated { get; init; } = AiAgentFormat.Relative;

    /// <summary>The hint read with every card ("Press Enter to open the run").</summary>
    public string OpenHint { get; init; } = "Press Enter to open the run";

    /// <summary>The accessible name of a card, from title, agent, status and progress or note.</summary>
    public Func<string, string?, string, string?, string> CardName { get; init; } =
        static (title, agent, status, detail) => string.Join(", ", new[] { title, agent, status, detail }.Where(s => !string.IsNullOrEmpty(s)));

    /// <summary>What screen readers hear when a task changes column ("Release notes: Done").</summary>
    public Func<string, string, string> Moved { get; init; } = static (title, status) => $"{title}: {status}";
}
