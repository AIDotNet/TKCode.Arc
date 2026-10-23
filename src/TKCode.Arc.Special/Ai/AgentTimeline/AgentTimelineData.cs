using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What an agent does during an <see cref="AgentTimelineSpan"/>.</summary>
public enum AgentTimelineSpanKind
{
    /// <summary>The model is reasoning or writing: a tint of the agent's color.</summary>
    Thinking,

    /// <summary>A tool call: the agent's solid color.</summary>
    Tool,

    /// <summary>Blocked on other agents: hatched, and left out of the speedup.</summary>
    Waiting
}

/// <summary>How an <see cref="AgentTimelineSpan"/> ended.</summary>
public enum AgentTimelineSpanStatus
{
    /// <summary>It ended normally.</summary>
    Ok,

    /// <summary>It failed (a tool call that errored or timed out): a cross and a Failed note once it ends.</summary>
    Failed
}

/// <summary>The state of an <see cref="AgentTimeline"/>'s replay, shown as the word in its status line.</summary>
public enum AgentTimelinePhase
{
    /// <summary>Paused at the start of the run.</summary>
    Ready,

    /// <summary>The replay is playing (also while the timeline waits off screen).</summary>
    Running,

    /// <summary>Paused part way through the run.</summary>
    Paused,

    /// <summary>At the end of the run: the status line shows the summary.</summary>
    Finished,

    /// <summary>Following a run in progress (<see cref="AgentTimeline.IsLive"/>).</summary>
    Live
}

/// <summary>A playback action a person took on an <see cref="AgentTimeline"/>.</summary>
public enum AgentTimelinePlaybackAction
{
    /// <summary>Play (or resume, or replay from the end).</summary>
    Play,

    /// <summary>Pause.</summary>
    Pause,

    /// <summary>Start over from zero and play.</summary>
    Restart,

    /// <summary>Move to a time on the axis (dragging, clicking or a scrubber key); the replay pauses.</summary>
    Seek,

    /// <summary>A new playback speed.</summary>
    Speed
}

/// <summary>
/// One agent of a run, shown as a row of an <see cref="AgentTimeline"/>: a color dot, its name and its model. The first
/// agent of a run is the orchestrator. Data only; set it before handing the run to the timeline.
/// </summary>
public sealed class AgentTimelineAgent
{
    /// <summary>Creates an empty agent; set <see cref="Id"/> and <see cref="Name"/>.</summary>
    public AgentTimelineAgent()
    {
    }

    /// <summary>Creates an agent.</summary>
    /// <param name="id">The id spans and handoffs use to name this agent.</param>
    /// <param name="name">The display name.</param>
    /// <param name="model">The model it runs on, or null.</param>
    /// <param name="price">A blended price in US dollars per million tokens, or null to leave it out of the cost.</param>
    public AgentTimelineAgent(string id, string name, AgentModel? model = null, double? price = null)
    {
        Id = id;
        Name = name;
        Model = model;
        Price = price;
    }

    /// <summary>The id that spans (<see cref="AgentTimelineSpan.AgentId"/>) and handoffs use for this agent.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The display name ("Coordinator", "Log reader").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The model the agent runs on: its name and mark show under the agent's name and in the span card.</summary>
    public AgentModel? Model { get; set; }

    /// <summary>What the agent is for, in a few words; read by screen readers with the row's spans. Optional.</summary>
    public string? Role { get; set; }

    /// <summary>
    /// The agent's color. Null uses the default: a neutral for the first agent (the orchestrator) and the chart series
    /// colors 1 to 4 after it, in turn.
    /// </summary>
    public IBrush? Color { get; set; }

    /// <summary>A blended price in US dollars per million tokens, used for the cost; null leaves its tokens out of the cost.</summary>
    public double? Price { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// One stretch of an agent's work on the time axis of an <see cref="AgentTimeline"/>: thinking, a tool call or waiting,
/// from <see cref="Start"/> to <see cref="End"/> in seconds from the start of the run. An open span (no
/// <see cref="End"/>) grows to the current time in a live run. Changes to its properties reach the timeline at once.
/// </summary>
public sealed class AgentTimelineSpan : AvaloniaObject
{
    /// <summary>Defines the <see cref="Id"/> property.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, string?>(nameof(Id));

    /// <summary>Defines the <see cref="AgentId"/> property.</summary>
    public static readonly StyledProperty<string> AgentIdProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, string>(nameof(AgentId), string.Empty);

    /// <summary>Defines the <see cref="Kind"/> property.</summary>
    public static readonly StyledProperty<AgentTimelineSpanKind> KindProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, AgentTimelineSpanKind>(nameof(Kind));

    /// <summary>Defines the <see cref="Start"/> property.</summary>
    public static readonly StyledProperty<double> StartProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, double>(nameof(Start));

    /// <summary>Defines the <see cref="End"/> property.</summary>
    public static readonly StyledProperty<double?> EndProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, double?>(nameof(End));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, string>(nameof(Label), string.Empty);

    /// <summary>Defines the <see cref="Detail"/> property.</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, string?>(nameof(Detail));

    /// <summary>Defines the <see cref="Tool"/> property.</summary>
    public static readonly StyledProperty<string?> ToolProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, string?>(nameof(Tool));

    /// <summary>Defines the <see cref="ToolMark"/> property.</summary>
    public static readonly StyledProperty<Geometry?> ToolMarkProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, Geometry?>(nameof(ToolMark));

    /// <summary>Defines the <see cref="Tokens"/> property.</summary>
    public static readonly StyledProperty<int?> TokensProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, int?>(nameof(Tokens));

    /// <summary>Defines the <see cref="Cost"/> property.</summary>
    public static readonly StyledProperty<double?> CostProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, double?>(nameof(Cost));

    /// <summary>Defines the <see cref="Status"/> property.</summary>
    public static readonly StyledProperty<AgentTimelineSpanStatus> StatusProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, AgentTimelineSpanStatus>(nameof(Status));

    /// <summary>Defines the <see cref="IsRetry"/> property.</summary>
    public static readonly StyledProperty<bool> IsRetryProperty =
        AvaloniaProperty.Register<AgentTimelineSpan, bool>(nameof(IsRetry));

    /// <summary>Creates an empty span; set at least <see cref="AgentId"/>, <see cref="Start"/> and <see cref="Label"/>.</summary>
    public AgentTimelineSpan()
    {
    }

    /// <summary>Creates a span.</summary>
    /// <param name="agentId">The <see cref="AgentTimelineAgent.Id"/> of the agent doing the work.</param>
    /// <param name="kind">Thinking, a tool call or waiting.</param>
    /// <param name="start">Seconds from the start of the run.</param>
    /// <param name="end">Seconds from the start of the run, or null while the span is still open.</param>
    /// <param name="label">What the agent did, in a few words.</param>
    public AgentTimelineSpan(string agentId, AgentTimelineSpanKind kind, double start, double? end, string label)
    {
        AgentId = agentId;
        Kind = kind;
        Start = start;
        End = end;
        Label = label;
    }

    /// <summary>An optional id of your own (handy in <see cref="AgentTimeline.SpanSelected"/>).</summary>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The <see cref="AgentTimelineAgent.Id"/> of the agent doing the work; spans of unknown agents are not shown.</summary>
    public string AgentId { get => GetValue(AgentIdProperty); set => SetValue(AgentIdProperty, value); }

    /// <summary>Thinking, a tool call or waiting.</summary>
    public AgentTimelineSpanKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <summary>When the span starts, in seconds from the start of the run.</summary>
    public double Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <summary>When the span ends, in seconds from the start of the run; null while it is open (it grows to now in a live run).</summary>
    public double? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>What the agent did, in a few words; the first line of the span card and part of the span's accessible name.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>
    /// More detail for the card: the reason of a failed call ("Failed: …") or the note of a retry. Optional.
    /// </summary>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    /// <summary>The tool's name for a tool call ("Web search"); shown with <see cref="ToolMark"/> in the card.</summary>
    public string? Tool { get => GetValue(ToolProperty); set => SetValue(ToolProperty, value); }

    /// <summary>The tool's mark: a 24 × 24 stroked glyph such as a Lucide icon. Null shows a wrench.</summary>
    public Geometry? ToolMark { get => GetValue(ToolMarkProperty); set => SetValue(ToolMarkProperty, value); }

    /// <summary>Tokens the span used; they count up evenly over the span while the run replays. Null counts none.</summary>
    public int? Tokens { get => GetValue(TokensProperty); set => SetValue(TokensProperty, value); }

    /// <summary>
    /// An explicit cost in US dollars for the span; null works it out from <see cref="Tokens"/> and the agent's
    /// <see cref="AgentTimelineAgent.Price"/>.
    /// </summary>
    public double? Cost { get => GetValue(CostProperty); set => SetValue(CostProperty, value); }

    /// <summary>Failed shows a cross and a Failed note once the span ends.</summary>
    public AgentTimelineSpanStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>True for the call that retried a failed one: it shows a retry icon once it ends.</summary>
    public bool IsRetry { get => GetValue(IsRetryProperty); set => SetValue(IsRetryProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>
/// Work handed from one agent to another at a moment of the run: drawn as a thin elbow connector with a dot where it
/// meets the upper row. Handing work down a row (orchestrator to subagent) is a dispatch: it drops at
/// <see cref="At"/> and enters the target's next span. Handing it up (subagent to orchestrator) is a result: it leaves
/// the end of the source's last span and rises to the target at <see cref="At"/>.
/// </summary>
public sealed class AgentTimelineHandoff
{
    /// <summary>Creates an empty handoff; set <see cref="From"/>, <see cref="To"/> and <see cref="At"/>.</summary>
    public AgentTimelineHandoff()
    {
    }

    /// <summary>Creates a handoff.</summary>
    /// <param name="from">The agent id handing the work over.</param>
    /// <param name="to">The agent id receiving it.</param>
    /// <param name="at">Seconds from the start of the run.</param>
    /// <param name="label">What was handed over, in a few words; shown in the span cards at both ends.</param>
    public AgentTimelineHandoff(string from, string to, double at, string? label = null)
    {
        From = from;
        To = to;
        At = at;
        Label = label;
    }

    /// <summary>The <see cref="AgentTimelineAgent.Id"/> handing the work over.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>The <see cref="AgentTimelineAgent.Id"/> receiving it.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>When it happens, in seconds from the start of the run.</summary>
    public double At { get; set; }

    /// <summary>What was handed over ("Mail config changes since Mar 1"). Optional.</summary>
    public string? Label { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{From} → {To} @ {At}";
}

/// <summary>
/// One orchestrated run for an <see cref="AgentTimeline"/>: its agents (rows, the orchestrator first), their spans and the
/// handoffs between them. The three lists are observed: append spans and handoffs as events arrive in a live run, and
/// set a span's <see cref="AgentTimelineSpan.End"/> when it closes.
/// </summary>
public sealed class AgentTimelineRun
{
    /// <summary>The heading; the timeline's <see cref="AgentTimeline.Title"/> wins when set.</summary>
    public string? Title { get; set; }

    /// <summary>The agents, one row each, in order; the first one is the orchestrator.</summary>
    public AvaloniaList<AgentTimelineAgent> Agents { get; } = new();

    /// <summary>What each agent did and when. Order does not matter.</summary>
    public AvaloniaList<AgentTimelineSpan> Spans { get; } = new();

    /// <summary>Work handed between agents.</summary>
    public AvaloniaList<AgentTimelineHandoff> Handoffs { get; } = new();

    /// <summary>The length of the run in seconds; null uses the last span end or handoff.</summary>
    public double? Duration { get; set; }

    /// <summary>When the run started (wall clock); a live timeline counts the time from it. Null counts from when live mode starts.</summary>
    public DateTimeOffset? StartedAt { get; set; }
}

/// <summary>The totals of a run, as the finished status line and the closing announcement show them.</summary>
/// <param name="AgentCount">The number of agents (rows).</param>
/// <param name="Duration">The wall time of the run in seconds.</param>
/// <param name="Speedup">The time the agents spent thinking and calling tools, divided by the wall time (waiting does not count).</param>
/// <param name="Tokens">All tokens of the run.</param>
/// <param name="Cost">The cost in US dollars.</param>
public readonly record struct AgentTimelineSummary(int AgentCount, double Duration, double Speedup, long Tokens, double Cost);

/// <summary>Arguments of <see cref="AgentTimeline.SpanSelected"/>: the span that was pinned, or null when it was unpinned.</summary>
public sealed class AgentTimelineSpanSelectedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentTimelineSpanSelectedEventArgs(RoutedEvent routedEvent, AgentTimelineSpan? span)
        : base(routedEvent)
    {
        Span = span;
    }

    /// <summary>The pinned span, or null when the pin was removed.</summary>
    public AgentTimelineSpan? Span { get; }
}

/// <summary>Arguments of <see cref="AgentTimeline.PlaybackChanged"/>: what the person did, and the time and speed after it.</summary>
public sealed class AgentTimelinePlaybackChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentTimelinePlaybackChangedEventArgs(RoutedEvent routedEvent, AgentTimelinePlaybackAction action, double time, double speed)
        : base(routedEvent)
    {
        Action = action;
        Time = time;
        Speed = speed;
    }

    /// <summary>The action.</summary>
    public AgentTimelinePlaybackAction Action { get; }

    /// <summary>The replay time in seconds after the action.</summary>
    public double Time { get; }

    /// <summary>The playback speed after the action.</summary>
    public double Speed { get; }
}
