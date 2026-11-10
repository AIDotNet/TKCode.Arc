using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A lead agent delegating to sub-agents, shown live: a graph with the lead on top and a node per sub-agent, a dot on the
/// line for every message (down for a task, back up for a result, across for a question between two sub-agents), a
/// message log, and a detail panel for the selected agent with its task, what it is doing now and what it handed back.
/// <para>
/// With no <see cref="Agents"/> the card plays an invented sample run (a lead and four sub-agents writing a launch brief).
/// Give it your agents and either a <see cref="Script"/> to play, or drive the run from your code: <see cref="Start"/>,
/// <see cref="Post"/> for each message, <see cref="SetStatus"/> and <see cref="Complete"/>. A message's
/// <see cref="OrchestratorMessage.Kind"/> updates the agents: a task sets the receiver to working, a question sets the
/// sender to waiting, a result sets it to done.
/// </para>
/// <code language="xml">
/// &lt;arc:AgentOrchestrator Goal="Plan the offsite" IsAutoStart="False"&gt;
///     &lt;arc:OrchestratorAgent Id="lead" Name="Atlas" Role="Planner" /&gt;
///     &lt;arc:OrchestratorAgent Id="venues" Name="Scout" Role="Venues" /&gt;
/// &lt;/arc:AgentOrchestrator&gt;
/// </code>
/// </summary>
[TemplatePart(HostPart, typeof(Panel))]
[TemplatePart(GraphPart, typeof(AgentOrchestratorGraph))]
[TemplatePart(RunPart, typeof(Button))]
[TemplatePart(ResetPart, typeof(Button))]
[TemplatePart(LogPart, typeof(ItemsControl))]
[TemplatePart(LogScrollPart, typeof(ScrollViewer))]
[PseudoClasses(CompactPseudoClass, IdlePseudoClass, RunningPseudoClass, PausedPseudoClass, CompletedPseudoClass, FailedPseudoClass,
    EmptyLogPseudoClass)]
public partial class AgentOrchestrator : TemplatedControl
{
    public const string HostPart = "PART_Host";
    public const string TitlePart = "PART_Title";
    public const string RunPillPart = "PART_RunPill";
    public const string GoalPart = "PART_Goal";
    public const string ProgressPart = "PART_Progress";
    public const string RunPart = "PART_Run";
    public const string RunTextPart = "PART_RunText";
    public const string RunGlyphPart = "PART_RunGlyph";
    public const string ResetPart = "PART_Reset";
    public const string GraphPart = "PART_Graph";
    public const string LogHeadingPart = "PART_LogHeading";
    public const string LogEmptyPart = "PART_LogEmpty";
    public const string LogScrollPart = "PART_LogScroll";
    public const string LogPart = "PART_Log";
    public const string DetailPart = "PART_Detail";
    public const string DetailNamePart = "PART_DetailName";
    public const string DetailRolePart = "PART_DetailRole";
    public const string DetailStatusPart = "PART_DetailStatus";
    public const string DetailModelPart = "PART_DetailModel";
    public const string DetailTrafficPart = "PART_DetailTraffic";
    public const string TaskHeadingPart = "PART_TaskHeading";
    public const string DetailTaskPart = "PART_DetailTask";
    public const string ActivityHeadingPart = "PART_ActivityHeading";
    public const string DetailActivityPart = "PART_DetailActivity";
    public const string ResultHeadingPart = "PART_ResultHeading";
    public const string DetailResultPart = "PART_DetailResult";

    /// <summary>Set while the card is at most <c>CompactWidth</c> (640) wide: the detail panel moves above the log.</summary>
    public const string CompactPseudoClass = ":compact";
    public const string IdlePseudoClass = ":idle";
    public const string RunningPseudoClass = ":running";
    public const string PausedPseudoClass = ":paused";
    public const string CompletedPseudoClass = ":completed";
    public const string FailedPseudoClass = ":failed";

    /// <summary>Set while the log has no message.</summary>
    public const string EmptyLogPseudoClass = ":empty-log";

    #region Properties

    /// <summary>The card's title; null uses the title of the labels ("Orchestration").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentOrchestrator, string?>(nameof(Title));

    /// <summary>The goal given to the lead, shown under the title and given to the lead as its task at the start; null with the sample agents uses the sample goal.</summary>
    public static readonly StyledProperty<string?> GoalProperty =
        AvaloniaProperty.Register<AgentOrchestrator, string?>(nameof(Goal));

    /// <summary>The id of the lead; null makes the first agent the lead.</summary>
    public static readonly StyledProperty<string?> LeadIdProperty =
        AvaloniaProperty.Register<AgentOrchestrator, string?>(nameof(LeadId));

    /// <summary>
    /// The run to play: steps that post messages and set statuses after a delay. Null with the sample agents plays the
    /// sample run; null with your own agents means your code drives the run.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<OrchestratorStep>?> ScriptProperty =
        AvaloniaProperty.Register<AgentOrchestrator, IReadOnlyList<OrchestratorStep>?>(nameof(Script));

    /// <summary>How fast the script plays: 2 plays it twice as fast (default 1). Zero or less plays at speed 1.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<AgentOrchestrator, double>(nameof(Speed), 1);

    /// <summary>Starts the script when the card is shown (true). Has no effect without a script.</summary>
    public static readonly StyledProperty<bool> IsAutoStartProperty =
        AvaloniaProperty.Register<AgentOrchestrator, bool>(nameof(IsAutoStart), true);

    /// <summary>The id of the agent in the detail panel (two-way); null shows the lead.</summary>
    public static readonly StyledProperty<string?> SelectedAgentIdProperty =
        AvaloniaProperty.Register<AgentOrchestrator, string?>(nameof(SelectedAgentId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Every word the card shows or reads out (see <see cref="AgentOrchestratorLabels"/>).</summary>
    public static readonly StyledProperty<AgentOrchestratorLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentOrchestrator, AgentOrchestratorLabels>(nameof(Labels), AgentOrchestratorLabels.Default);

    /// <summary>Where the run stands.</summary>
    public static readonly DirectProperty<AgentOrchestrator, OrchestratorRunState> RunStateProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestrator, OrchestratorRunState>(nameof(RunState), o => o.RunState);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AgentOrchestrator, double>(nameof(CompactWidth), 640);

    /// <summary>Raised after a message was posted (by the script or by <see cref="Post"/>).</summary>
    public static readonly RoutedEvent<OrchestratorMessageEventArgs> MessagePostedEvent =
        RoutedEvent.Register<AgentOrchestrator, OrchestratorMessageEventArgs>(nameof(MessagePosted), RoutingStrategies.Bubble);

    /// <summary>Raised when an agent's status changes, whatever changed it.</summary>
    public static readonly RoutedEvent<OrchestratorAgentStatusEventArgs> AgentStatusChangedEvent =
        RoutedEvent.Register<AgentOrchestrator, OrchestratorAgentStatusEventArgs>(nameof(AgentStatusChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the run starts, pauses, resumes, completes, fails or is reset.</summary>
    public static readonly RoutedEvent<OrchestratorRunStateEventArgs> RunStateChangedEvent =
        RoutedEvent.Register<AgentOrchestrator, OrchestratorRunStateEventArgs>(nameof(RunStateChanged), RoutingStrategies.Bubble);

    /// <summary>The agents, declared inline or added in code; empty plays the sample run.</summary>
    [Content]
    public AvaloniaList<OrchestratorAgent> Agents { get; } = new();

    /// <summary>The messages of this run, oldest first (read it; post with <see cref="Post"/>).</summary>
    public IReadOnlyList<OrchestratorMessage> Messages => _messages;

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="GoalProperty"/>
    public string? Goal { get => GetValue(GoalProperty); set => SetValue(GoalProperty, value); }

    /// <inheritdoc cref="LeadIdProperty"/>
    public string? LeadId { get => GetValue(LeadIdProperty); set => SetValue(LeadIdProperty, value); }

    /// <inheritdoc cref="ScriptProperty"/>
    public IReadOnlyList<OrchestratorStep>? Script { get => GetValue(ScriptProperty); set => SetValue(ScriptProperty, value); }

    /// <inheritdoc cref="SpeedProperty"/>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <inheritdoc cref="IsAutoStartProperty"/>
    public bool IsAutoStart { get => GetValue(IsAutoStartProperty); set => SetValue(IsAutoStartProperty, value); }

    /// <inheritdoc cref="SelectedAgentIdProperty"/>
    public string? SelectedAgentId { get => GetValue(SelectedAgentIdProperty); set => SetValue(SelectedAgentIdProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentOrchestratorLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="RunStateProperty"/>
    public OrchestratorRunState RunState { get => _runState; private set => SetAndRaise(RunStateProperty, ref _runState, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="MessagePostedEvent"/>
    public event EventHandler<OrchestratorMessageEventArgs>? MessagePosted
    {
        add => AddHandler(MessagePostedEvent, value);
        remove => RemoveHandler(MessagePostedEvent, value);
    }

    /// <inheritdoc cref="AgentStatusChangedEvent"/>
    public event EventHandler<OrchestratorAgentStatusEventArgs>? AgentStatusChanged
    {
        add => AddHandler(AgentStatusChangedEvent, value);
        remove => RemoveHandler(AgentStatusChangedEvent, value);
    }

    /// <inheritdoc cref="RunStateChangedEvent"/>
    public event EventHandler<OrchestratorRunStateEventArgs>? RunStateChanged
    {
        add => AddHandler(RunStateChangedEvent, value);
        remove => RemoveHandler(RunStateChangedEvent, value);
    }

    #endregion

    private readonly AvaloniaList<OrchestratorMessage> _messages = new();
    private readonly AiAnnouncer _announcer = new();
    private OrchestratorRunState _runState;
    private IReadOnlyList<OrchestratorAgent>? _samples;
    private IReadOnlyList<OrchestratorStep>? _sampleScript;
    // The agents in graph order: the lead first.
    private OrchestratorAgent[] _ordered = [];
    private readonly HashSet<OrchestratorAgent> _watched = new();

    /// <summary>The waits of the script; tests replace it to step time by hand.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => System.Threading.Tasks.Task.Delay(time, token);

    /// <summary>The run clock's source; tests replace it.</summary>
    internal Func<TimeSpan> Clock { get; set; } = static () => Stopwatch.GetElapsedTime(s_origin);

    private static readonly long s_origin = Stopwatch.GetTimestamp();

    /// <summary>Creates a card that plays the sample run until it is given agents.</summary>
    public AgentOrchestrator()
    {
        Agents.CollectionChanged += OnAgentsChanged;
        RebuildAgents();
    }

    /// <summary>True while the card shows the built-in sample agents (it has no agents of its own).</summary>
    public bool IsUsingSamples => Agents.Count == 0;

    /// <summary>The agents shown: yours, or the samples when you gave none.</summary>
    public IReadOnlyList<OrchestratorAgent> EffectiveAgents => IsUsingSamples ? _samples ??= AgentOrchestratorSamples.Agents() : Agents;

    /// <summary>The lead agent (null without agents).</summary>
    public OrchestratorAgent? Lead => _ordered.Length > 0 ? _ordered[0] : null;

    /// <summary>The goal shown and given to the lead.</summary>
    public string? EffectiveGoal => Goal ?? (IsUsingSamples ? AgentOrchestratorSamples.Goal : null);

    /// <summary>The script that <see cref="Start"/> plays; null when your code drives the run.</summary>
    public IReadOnlyList<OrchestratorStep>? EffectiveScript =>
        Script ?? (IsUsingSamples ? _sampleScript ??= AgentOrchestratorSamples.Script() : null);

    /// <summary>The agent in the detail panel: the selected one, or the lead.</summary>
    public OrchestratorAgent? SelectedAgent => Find(SelectedAgentId) ?? Lead;

    /// <summary>The agent with <paramref name="id"/>, or null.</summary>
    /// <param name="id">The agent's id.</param>
    public OrchestratorAgent? Find(string? id)
    {
        if (id is null)
        {
            return null;
        }
        foreach (var agent in _ordered)
        {
            if (agent.Id == id)
            {
                return agent;
            }
        }
        return null;
    }

    /// <summary>The name of the agent with <paramref name="id"/>, or the id itself.</summary>
    internal string NameOf(string id) => Find(id)?.Name is { Length: > 0 } name ? name : id;

    internal int IndexOf(string id)
    {
        for (var i = 0; i < _ordered.Length; i++)
        {
            if (_ordered[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }

    private void OnAgentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        StopScript();
        _messages.Clear();
        SetRunState(OrchestratorRunState.Idle, announce: false);
        RebuildAgents();
    }

    /// <summary>Orders the agents (lead first), follows their changes and gives them to the graph.</summary>
    private void RebuildAgents()
    {
        foreach (var agent in _watched)
        {
            agent.PropertyChanged -= OnAgentPropertyChanged;
        }
        _watched.Clear();
        var agents = EffectiveAgents;
        var leadId = LeadId;
        var lead = agents.FirstOrDefault(a => leadId is not null && a.Id == leadId) ?? agents.FirstOrDefault();
        var ordered = new List<OrchestratorAgent>(agents.Count);
        if (lead is not null)
        {
            ordered.Add(lead);
        }
        foreach (var agent in agents)
        {
            if (!ReferenceEquals(agent, lead))
            {
                ordered.Add(agent);
            }
        }
        _ordered = ordered.ToArray();
        foreach (var agent in _ordered)
        {
            if (_watched.Add(agent))
            {
                agent.PropertyChanged += OnAgentPropertyChanged;
            }
        }
        _graph?.SetAgents(_ordered, Labels);
        UpdateSelection();
        UpdateTexts();
    }

    private void OnAgentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not OrchestratorAgent agent)
        {
            return;
        }
        if (e.Property == OrchestratorAgent.StatusProperty)
        {
            var oldStatus = e.GetOldValue<OrchestratorAgentStatus>();
            var newStatus = e.GetNewValue<OrchestratorAgentStatus>();
            _graph?.Refresh();
            RaiseEvent(new OrchestratorAgentStatusEventArgs(AgentStatusChangedEvent, this, agent, oldStatus, newStatus));
            if (!_resetting)
            {
                _announcer.Announce(Labels.StatusAnnouncement(agent.Name, newStatus));
            }
            UpdateTexts();
        }
        else if (e.Property == OrchestratorAgent.IdProperty)
        {
            UpdateSelection();
        }
        if (ReferenceEquals(agent, SelectedAgent))
        {
            UpdateDetail();
        }
    }

    /// <summary>The last polite announcement (tests and tools).</summary>
    internal string? LastAnnouncement => _announcer.Announcement;
}
