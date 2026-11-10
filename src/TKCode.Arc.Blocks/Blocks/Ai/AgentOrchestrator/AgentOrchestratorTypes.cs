using Avalonia;

namespace TKCode.Arc;

/// <summary>Where one agent of an <see cref="AgentOrchestrator"/> run stands.</summary>
public enum OrchestratorAgentStatus
{
    /// <summary>Has no task yet, or the run has not started.</summary>
    Idle,

    /// <summary>Planning: reading its task or splitting it up.</summary>
    Thinking,

    /// <summary>Doing its task.</summary>
    Working,

    /// <summary>Blocked on an answer or on the agents it delegated to.</summary>
    Waiting,

    /// <summary>Finished and handed its result back.</summary>
    Done,

    /// <summary>Gave up with an error.</summary>
    Failed,
}

/// <summary>What a message between two agents carries; it decides the dot's color and what the message changes.</summary>
public enum OrchestratorMessageKind
{
    /// <summary>A task handed down: the receiver starts working on the text.</summary>
    Delegate,

    /// <summary>A progress note: the sender's current activity becomes the text.</summary>
    Progress,

    /// <summary>A question: the sender waits for an answer.</summary>
    Question,

    /// <summary>An answer: the receiver (the one who asked) goes back to work.</summary>
    Answer,

    /// <summary>A result handed back: the sender is done and its result is the text.</summary>
    Result,

    /// <summary>An error reported: the sender has failed and its result is the text.</summary>
    Error,
}

/// <summary>Where the run of an <see cref="AgentOrchestrator"/> stands.</summary>
public enum OrchestratorRunState
{
    /// <summary>Not started, or reset.</summary>
    Idle,

    /// <summary>Running: the script plays, or your code posts messages.</summary>
    Running,

    /// <summary>Paused: the script waits until it is resumed.</summary>
    Paused,

    /// <summary>The run finished.</summary>
    Completed,

    /// <summary>The run stopped with an error.</summary>
    Failed,
}

/// <summary>
/// One agent of an <see cref="AgentOrchestrator"/> run: the lead or a sub-agent. Data only: an <see cref="AvaloniaObject"/>,
/// so a change made by your code or by a message reaches the graph, the log and the detail panel.
/// </summary>
public class OrchestratorAgent : AvaloniaObject
{
    /// <summary>Stable id used by messages and steps (<see cref="OrchestratorMessage.From"/>, <see cref="OrchestratorMessage.To"/>).</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<OrchestratorAgent, string>(nameof(Id), string.Empty);

    /// <summary>The name on the node, such as "Scout".</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<OrchestratorAgent, string>(nameof(Name), string.Empty);

    /// <summary>What the agent is for, one short phrase under the name ("Market research").</summary>
    public static readonly StyledProperty<string?> RoleProperty =
        AvaloniaProperty.Register<OrchestratorAgent, string?>(nameof(Role));

    /// <summary>The model the agent runs on, shown in the detail panel; null shows nothing.</summary>
    public static readonly StyledProperty<string?> ModelProperty =
        AvaloniaProperty.Register<OrchestratorAgent, string?>(nameof(Model));

    /// <summary>Where the agent stands; messages change it, and so can <see cref="AgentOrchestrator.SetStatus"/>.</summary>
    public static readonly StyledProperty<OrchestratorAgentStatus> StatusProperty =
        AvaloniaProperty.Register<OrchestratorAgent, OrchestratorAgentStatus>(nameof(Status));

    /// <summary>The task it was given (the text of the last <see cref="OrchestratorMessageKind.Delegate"/> it received).</summary>
    public static readonly StyledProperty<string?> TaskProperty =
        AvaloniaProperty.Register<OrchestratorAgent, string?>(nameof(Task));

    /// <summary>What it is doing right now (the last progress note); the node shows it in place of the task while set.</summary>
    public static readonly StyledProperty<string?> ActivityProperty =
        AvaloniaProperty.Register<OrchestratorAgent, string?>(nameof(Activity));

    /// <summary>What it handed back (the text of its result or error).</summary>
    public static readonly StyledProperty<string?> ResultProperty =
        AvaloniaProperty.Register<OrchestratorAgent, string?>(nameof(Result));

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="RoleProperty"/>
    public string? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public string? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public OrchestratorAgentStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="TaskProperty"/>
    public string? Task { get => GetValue(TaskProperty); set => SetValue(TaskProperty, value); }

    /// <inheritdoc cref="ActivityProperty"/>
    public string? Activity { get => GetValue(ActivityProperty); set => SetValue(ActivityProperty, value); }

    /// <inheritdoc cref="ResultProperty"/>
    public string? Result { get => GetValue(ResultProperty); set => SetValue(ResultProperty, value); }

    /// <summary>Puts the agent back to the start of a run: idle, with no task, activity or result.</summary>
    public void ResetRun()
    {
        Status = OrchestratorAgentStatus.Idle;
        Task = null;
        Activity = null;
        Result = null;
    }
}

/// <summary>
/// A message from one agent to another. Post it with <see cref="AgentOrchestrator.Post"/>: the graph sends a dot along
/// the line between the two agents, the log lists it, and its <see cref="Kind"/> updates the agents.
/// </summary>
public sealed class OrchestratorMessage
{
    /// <summary>The id of the agent that sends it.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>The id of the agent that receives it.</summary>
    public string To { get; init; } = string.Empty;

    /// <summary>What it carries (a task, a note, a question, an answer, a result or an error).</summary>
    public OrchestratorMessageKind Kind { get; init; }

    /// <summary>The text, one sentence.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Its place in the run, from 1; set when it is posted.</summary>
    public int Sequence { get; internal set; }

    /// <summary>The run time when it was posted (paused time left out); set when it is posted.</summary>
    public TimeSpan Elapsed { get; internal set; }

    /// <summary>A fresh copy that has not been posted (a script step can play again after a reset).</summary>
    public OrchestratorMessage Copy() => new() { From = From, To = To, Kind = Kind, Text = Text };
}

/// <summary>
/// One step of a scripted run (<see cref="AgentOrchestrator.Script"/>): after <see cref="Delay"/>, post
/// <see cref="Message"/> and/or set the status of <see cref="AgentId"/>.
/// </summary>
public sealed class OrchestratorStep
{
    /// <summary>How long to wait after the previous step, at speed 1.</summary>
    public TimeSpan Delay { get; init; }

    /// <summary>The message to post, or null.</summary>
    public OrchestratorMessage? Message { get; init; }

    /// <summary>The agent whose status changes, or null.</summary>
    public string? AgentId { get; init; }

    /// <summary>The new status of <see cref="AgentId"/>.</summary>
    public OrchestratorAgentStatus? Status { get; init; }

    /// <summary>The new activity of <see cref="AgentId"/>; null keeps it.</summary>
    public string? Activity { get; init; }

    /// <summary>The new result of <see cref="AgentId"/> (the lead's final answer); null keeps it.</summary>
    public string? Result { get; init; }

    /// <summary>A step that posts a message.</summary>
    /// <param name="delayMs">Milliseconds after the previous step.</param>
    /// <param name="from">The sender's id.</param>
    /// <param name="to">The receiver's id.</param>
    /// <param name="kind">What the message carries.</param>
    /// <param name="text">The message.</param>
    public static OrchestratorStep Send(int delayMs, string from, string to, OrchestratorMessageKind kind, string text) =>
        new() { Delay = TimeSpan.FromMilliseconds(delayMs), Message = new OrchestratorMessage { From = from, To = to, Kind = kind, Text = text } };

    /// <summary>A step that sets an agent's status and, optionally, its activity.</summary>
    /// <param name="delayMs">Milliseconds after the previous step.</param>
    /// <param name="agentId">The agent.</param>
    /// <param name="status">Its new status.</param>
    /// <param name="activity">Its new activity, or null to keep it.</param>
    /// <param name="result">Its new result, or null to keep it.</param>
    public static OrchestratorStep Set(int delayMs, string agentId, OrchestratorAgentStatus status, string? activity = null, string? result = null) =>
        new() { Delay = TimeSpan.FromMilliseconds(delayMs), AgentId = agentId, Status = status, Activity = activity, Result = result };
}
