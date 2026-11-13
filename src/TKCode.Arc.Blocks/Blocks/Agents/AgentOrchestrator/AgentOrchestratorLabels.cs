namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AgentOrchestrator"/> shows or reads out, with English defaults. Replace the whole object to
/// reword or translate; texts with values are functions so the word order can change.
/// </summary>
public sealed class AgentOrchestratorLabels
{
    /// <summary>The shared English labels.</summary>
    public static AgentOrchestratorLabels Default { get; } = new();

    /// <summary>The card's title ("Orchestration"); <see cref="AgentOrchestrator.Title"/> wins when set.</summary>
    public string Title { get; init; } = "Orchestration";

    /// <summary>The Run button before the first start ("Run").</summary>
    public string Start { get; init; } = "Run";

    /// <summary>The button while the run plays ("Pause").</summary>
    public string Pause { get; init; } = "Pause";

    /// <summary>The button while the run is paused ("Resume").</summary>
    public string Resume { get; init; } = "Resume";

    /// <summary>The button after the run ended ("Run again").</summary>
    public string Replay { get; init; } = "Run again";

    /// <summary>The accessible name and tooltip of the reset button ("Reset run").</summary>
    public string Reset { get; init; } = "Reset run";

    /// <summary>The run state words of the status pill (Idle shows none).</summary>
    public Func<OrchestratorRunState, string> RunStateName { get; init; } = static state => state switch
    {
        OrchestratorRunState.Running => "Running",
        OrchestratorRunState.Paused => "Paused",
        OrchestratorRunState.Completed => "Complete",
        OrchestratorRunState.Failed => "Failed",
        _ => "Ready",
    };

    /// <summary>The line under the title: done agents, agents and messages ("2 of 4 agents done · 9 messages").</summary>
    public Func<int, int, int, string> Progress { get; init; } = static (done, total, messages) =>
        $"{done} of {total} agents done · {messages} {(messages == 1 ? "message" : "messages")}";

    /// <summary>The word of a status on a node and in the detail panel.</summary>
    public Func<OrchestratorAgentStatus, string> StatusName { get; init; } = static status => status switch
    {
        OrchestratorAgentStatus.Thinking => "Thinking",
        OrchestratorAgentStatus.Working => "Working",
        OrchestratorAgentStatus.Waiting => "Waiting",
        OrchestratorAgentStatus.Done => "Done",
        OrchestratorAgentStatus.Failed => "Failed",
        _ => "Idle",
    };

    /// <summary>The verb of a message kind in the log ("delegated", "replied").</summary>
    public Func<OrchestratorMessageKind, string> KindName { get; init; } = static kind => kind switch
    {
        OrchestratorMessageKind.Delegate => "Task",
        OrchestratorMessageKind.Progress => "Update",
        OrchestratorMessageKind.Question => "Question",
        OrchestratorMessageKind.Answer => "Answer",
        OrchestratorMessageKind.Result => "Result",
        OrchestratorMessageKind.Error => "Error",
        _ => kind.ToString(),
    };

    /// <summary>The tag on the lead's node ("Lead").</summary>
    public string Lead { get; init; } = "Lead";

    /// <summary>A node's line while the agent has no task ("No task yet").</summary>
    public string NoTask { get; init; } = "No task yet";

    /// <summary>The heading of the message log ("Messages").</summary>
    public string MessagesHeading { get; init; } = "Messages";

    /// <summary>The log before the first message ("Messages between agents show up here once the run starts").</summary>
    public string NoMessages { get; init; } = "Messages between agents show up here once the run starts";

    /// <summary>The detail panel's label for the task ("Task").</summary>
    public string TaskHeading { get; init; } = "Task";

    /// <summary>The detail panel's label for the current activity ("Now").</summary>
    public string ActivityHeading { get; init; } = "Now";

    /// <summary>The detail panel's label for the result ("Result").</summary>
    public string ResultHeading { get; init; } = "Result";

    /// <summary>The detail panel's label for the model ("Model").</summary>
    public string ModelHeading { get; init; } = "Model";

    /// <summary>The detail panel's message counts: sent and received ("4 sent · 3 received").</summary>
    public Func<int, int, string> Traffic { get; init; } = static (sent, received) => $"{sent} sent · {received} received";

    /// <summary>The detail panel's text for a missing value ("Nothing yet").</summary>
    public string Nothing { get; init; } = "Nothing yet";

    /// <summary>The graph's accessible name ("Agents").</summary>
    public string GraphName { get; init; } = "Agents";

    /// <summary>The log's accessible name ("Message log").</summary>
    public string LogName { get; init; } = "Message log";

    /// <summary>The detail panel's accessible name; the argument is the agent's name ("Details for Scout").</summary>
    public Func<string, string> DetailName { get; init; } = static name => $"Details for {name}";

    /// <summary>A node's accessible name: name, role, status word and what it does ("Scout, Market research, Working: Compare prices").</summary>
    public Func<string, string?, string, string?, string> NodeName { get; init; } = static (name, role, status, doing) =>
        $"{name}{(string.IsNullOrEmpty(role) ? "" : ", " + role)}, {status}{(string.IsNullOrEmpty(doing) ? "" : ": " + doing)}";

    /// <summary>A log row's accessible text: sender, receiver, kind word and text ("Atlas to Scout, Task: Compare prices").</summary>
    public Func<string, string, string, string, string> MessageName { get; init; } = static (from, to, kind, text) =>
        $"{from} to {to}, {kind}: {text}";

    /// <summary>Said when an agent's status changes ("Scout is done").</summary>
    public Func<string, OrchestratorAgentStatus, string> StatusAnnouncement { get; init; } = static (name, status) => status switch
    {
        OrchestratorAgentStatus.Thinking => $"{name} is thinking",
        OrchestratorAgentStatus.Working => $"{name} is working",
        OrchestratorAgentStatus.Waiting => $"{name} is waiting",
        OrchestratorAgentStatus.Done => $"{name} is done",
        OrchestratorAgentStatus.Failed => $"{name} failed",
        _ => $"{name} is idle",
    };

    /// <summary>Said when the run starts, pauses, ends or fails ("Run complete").</summary>
    public Func<OrchestratorRunState, string> RunAnnouncement { get; init; } = static state => state switch
    {
        OrchestratorRunState.Running => "Run started",
        OrchestratorRunState.Paused => "Run paused",
        OrchestratorRunState.Completed => "Run complete",
        OrchestratorRunState.Failed => "Run failed",
        _ => "Run reset",
    };
}
