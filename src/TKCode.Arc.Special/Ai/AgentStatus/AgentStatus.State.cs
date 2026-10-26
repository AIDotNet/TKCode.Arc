using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public sealed partial class AgentStatus
{
    private readonly record struct LabelInfo(string Key, string Text, string? Count, Geometry? Icon, AgentStatusLabelTone Tone,
        string Spoken, string FullText);

    private readonly AgentStatusRunClock _clock = new();
    private AgentRunSimulation? _sim;
    private AgentRunSimulation? _autoSim;
    private AgentStatusRunState _lastStatus;
    private bool _statusSeen;
    private bool _settled;
    private ITimer? _settleTimer;
    private ITimer? _tickTimer;
    private AgentStatusRunState _display;
    private string? _lastKey;
    private string? _lastNotice;
    private int _lastNoticeVersion;
    private bool _compact;
    private double _available = double.NaN;
    private ExplainerAnnouncer? _announcer;
    private bool _attached;
    private readonly HashSet<AgentStatusStep> _watchedSteps = new();
    private bool _built;
    private AgentModel? _markModel;
    private bool _refreshing;

    private AgentStatusRunState EffStatus => _sim?.Status ?? Status;

    private AgentStatusActivity? EffActivity => _sim is not null ? _sim.Activity : Activity;

    private IReadOnlyList<AgentStatusStep> EffSteps => _sim is not null ? _sim.Steps : _steps;

    private AgentModel EffAgent => _sim?.Agent ?? Agent ?? s_defaultAgent;

    private DateTimeOffset? EffStartedAt => _sim is not null ? _sim.StartedAt : StartedAt;

    private DateTimeOffset? EffEndedAt => _sim is not null ? _sim.EndedAt : EndedAt;

    private static readonly AgentModel s_defaultAgent = new();

    /// <summary>The state the pill shows: the run's, or Idle once a finished run has settled back to Ready.</summary>
    internal AgentStatusRunState DisplayState => _display;

    /// <summary>The name shown and spoken for the agent.</summary>
    private string AgentName => EffAgent.Name is { Length: > 0 } name ? name : Labels.DefaultAgentName;

    private bool IsLiveState(AgentStatusRunState state) => state is AgentStatusRunState.Working or AgentStatusRunState.NeedsInput or AgentStatusRunState.Paused;

    private void OnSimulationReplaced(AgentRunSimulation? old, AgentRunSimulation? sim)
    {
        if (old is not null)
        {
            old.Changed -= OnSimulationChanged;
        }
        _sim = sim;
        // A host's simulation (and its steps) can outlive the pill: the pill listens only while attached.
        if (sim is not null && _attached)
        {
            sim.Changed += OnSimulationChanged;
            sim.Start();
        }
        Refresh();
    }

    /// <summary>Listens to the simulation and the steps while attached, so data that outlives the pill does not keep it.</summary>
    private void WatchData(bool watch)
    {
        if (_sim is not null)
        {
            _sim.Changed -= OnSimulationChanged;
            if (watch)
            {
                _sim.Changed += OnSimulationChanged;
            }
        }
        // The watched set, not the list: a Clear reports no old items.
        foreach (var step in _watchedSteps)
        {
            step.PropertyChanged -= OnStepChanged;
        }
        _watchedSteps.Clear();
        if (watch)
        {
            foreach (var step in _steps)
            {
                if (_watchedSteps.Add(step))
                {
                    step.PropertyChanged += OnStepChanged;
                }
            }
        }
    }

    private void OnSimulationChanged(object? sender, EventArgs e) => Refresh();

    private void OnStepsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // While detached nothing is watched; attaching watches every step.
        if (_attached)
        {
            WatchData(true);
        }
        Refresh();
    }

    private void OnStepChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();

    private bool HasHostData() =>
        IsSet(StatusProperty) || IsSet(ActivityProperty) || IsSet(AgentProperty) || IsSet(StartedAtProperty) || _steps.Count > 0;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ResourcesChanged += OnResourcesChanged;
        RefreshRadii();
        if (Simulation is null && !HasHostData())
        {
            _autoSim = new AgentRunSimulation(AgentStatusSamples.Coding, TimeProvider);
            SetCurrentValue(SimulationProperty, _autoSim);
        }
        WatchData(true);
        _sim?.Start();
        ApplyLevel();
        Refresh();
        if (IsExpanded)
        {
            // Attached with the panel already open: open it without a motion.
            OnExpandedChanged(true);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        ResourcesChanged -= OnResourcesChanged;
        WatchData(false);
        StopTick();
        StopSettle();
        ReleasePanel();
        if (_autoSim is not null)
        {
            _autoSim.Dispose();
            if (ReferenceEquals(Simulation, _autoSim))
            {
                ClearValue(SimulationProperty);
            }
            _autoSim = null;
        }
    }

    /// <summary>Brings every part in line with the inputs: face, buttons, body, sizes and announcements.</summary>
    private void Refresh()
    {
        if (!_built || _refreshing)
        {
            return;
        }
        _refreshing = true;
        try
        {
            RefreshCore();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshCore()
    {
        var labels = Labels;
        var now = TimeProvider.GetUtcNow();
        var status = EffStatus;
        if (!_statusSeen || status != _lastStatus)
        {
            _statusSeen = true;
            _lastStatus = status;
            _settled = false;
            StopSettle();
        }
        _clock.SetStartedAt(EffStartedAt);
        _clock.EndedAt = EffEndedAt;
        _clock.Observe(status, now);
        var finished = status is AgentStatusRunState.Done or AgentStatusRunState.Stopped;
        if (finished && !_settled && _settleTimer is null && _attached && SettleAfter is { } after)
        {
            StartSettle(after);
        }
        _display = _settled && finished ? AgentStatusRunState.Idle : status;
        if (_attached && _display is AgentStatusRunState.Working or AgentStatusRunState.NeedsInput)
        {
            StartTick();
        }
        else
        {
            StopTick();
        }
        UpdatePseudoClasses();

        var agent = EffAgent;
        if (!ReferenceEquals(agent, _markModel))
        {
            _markModel = agent;
            _face.SetMark(agent.Mark, agent.MarkBrush);
        }
        var elapsed = _clock.Elapsed(now);
        var info = ComputeLabel(labels, elapsed);
        var inline = InlineVisible();
        var timerText = labels.FormatTimer(elapsed);
        var showsElapsed = IsLiveState(_display);
        _face.SetState(_display);
        _face.SetLabel(info.Key, info.Text, info.Count, info.Icon, info.Tone);
        _face.SetTimer(timerText, showsElapsed && !(_compact && inline));
        AutomationProperties.SetName(_head, labels.PillName(AgentName, info.FullText, showsElapsed ? timerText : null));
        _head.IsExpandedState = IsExpanded;
        Announce(labels, info);

        UpdateButtons();
        if (IsExpanded || _bodyVisible)
        {
            UpdateBody(now);
        }
        UpdateSizes();
    }

    private LabelInfo ComputeLabel(AgentStatusLabels labels, TimeSpan elapsed)
    {
        var activity = EffActivity;
        switch (_display)
        {
            case AgentStatusRunState.Working:
            {
                var text = activity?.Label is { Length: > 0 } label ? label : labels.Working;
                var count = activity is { HasProgress: true } ? labels.Count(activity.ProgressValue!.Value, activity.ProgressTotal!.Value) : null;
                return new LabelInfo("working:" + text, text, count, activity?.Icon, AgentStatusLabelTone.Live, text, count is null ? text : text + " " + count);
            }
            case AgentStatusRunState.NeedsInput:
            {
                var text = activity?.Label is { Length: > 0 } label ? label : labels.NeedsInput;
                return new LabelInfo("needs:" + text, text, null, activity?.Icon, AgentStatusLabelTone.Plain, text, text);
            }
            case AgentStatusRunState.Paused:
                return new LabelInfo("paused", labels.Paused, null, ArcIcons.Pause, AgentStatusLabelTone.Quiet, labels.Paused, labels.Paused);
            case AgentStatusRunState.Done:
            {
                var outcome = activity?.Label;
                var text = labels.DoneIn(labels.FormatTotal(elapsed), outcome);
                var spoken = labels.DoneInSpoken(labels.FormatTotalSpoken(elapsed), outcome);
                return new LabelInfo("done", text, null, null, AgentStatusLabelTone.Plain, spoken, text);
            }
            case AgentStatusRunState.Failed:
            {
                var text = activity?.Label is { Length: > 0 } label ? label : labels.Failed;
                return new LabelInfo("failed:" + text, text, null, null, AgentStatusLabelTone.Plain, text, text);
            }
            case AgentStatusRunState.Stopped:
            {
                var text = labels.StoppedAfter(labels.FormatTotal(elapsed));
                return new LabelInfo("stopped", text, null, null, AgentStatusLabelTone.Quiet, labels.StoppedAfterSpoken(labels.FormatTotalSpoken(elapsed)), text);
            }
            default:
                return new LabelInfo("ready", labels.Ready, null, null, AgentStatusLabelTone.Quiet, labels.Ready, labels.Ready);
        }
    }

    private string? EffNotice => _sim is not null ? _sim.Notice : Notice;

    /// <summary>Says a new activity or outcome once, and an action's notice with it; never the count or the timer.</summary>
    private void Announce(AgentStatusLabels labels, LabelInfo info)
    {
        var notice = EffNotice;
        var version = _sim?.NoticeVersion ?? 0;
        var noticeChanged = !string.IsNullOrEmpty(notice) && (notice != _lastNotice || version != _lastNoticeVersion);
        var keyChanged = _lastKey is not null && info.Key != _lastKey;
        if (_lastKey is not null && (keyChanged || noticeChanged))
        {
            _announcer?.Announce(labels.Announcement(AgentName, info.Spoken, noticeChanged ? notice : null));
        }
        _lastKey = info.Key;
        _lastNotice = notice;
        _lastNoticeVersion = version;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(IdlePseudoClass, _display == AgentStatusRunState.Idle);
        PseudoClasses.Set(WorkingPseudoClass, _display == AgentStatusRunState.Working);
        PseudoClasses.Set(NeedsInputPseudoClass, _display == AgentStatusRunState.NeedsInput);
        PseudoClasses.Set(PausedPseudoClass, _display == AgentStatusRunState.Paused);
        PseudoClasses.Set(DonePseudoClass, _display == AgentStatusRunState.Done);
        PseudoClasses.Set(FailedPseudoClass, _display == AgentStatusRunState.Failed);
        PseudoClasses.Set(StoppedPseudoClass, _display == AgentStatusRunState.Stopped);
        PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
        PseudoClasses.Set(CompactPseudoClass, _compact);
    }

    /// <summary>True while Deny, Approve or Retry show inline in the pill (the panel is closed and the state asks for them).</summary>
    private bool InlineVisible()
    {
        if (IsExpanded)
        {
            return false;
        }
        var actions = Actions;
        return _display switch
        {
            AgentStatusRunState.NeedsInput => (actions & (AgentStatusActions.Approve | AgentStatusActions.Deny)) != 0,
            AgentStatusRunState.Failed => (actions & AgentStatusActions.Retry) != 0,
            _ => false
        };
    }

    // The timer ticks four times a second while the run is live; the callback arrives on a pool thread.
    private void StartTick()
    {
        if (_tickTimer is not null)
        {
            return;
        }
        _tickTimer = TimeProvider.CreateTimer(static state => Dispatcher.UIThread.Post(((AgentStatus)state!).Tick), this,
            TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
    }

    private void StopTick()
    {
        _tickTimer?.Dispose();
        _tickTimer = null;
    }

    private void StartSettle(TimeSpan after)
    {
        _settleTimer = TimeProvider.CreateTimer(static state => Dispatcher.UIThread.Post(((AgentStatus)state!).Settle), this,
            after, Timeout.InfiniteTimeSpan);
    }

    private void StopSettle()
    {
        _settleTimer?.Dispose();
        _settleTimer = null;
    }

    /// <summary>Updates the timer, the live step times and the pill's name (the timer's tick; tests call it directly).</summary>
    internal void Tick()
    {
        if (!_built || !_attached)
        {
            return;
        }
        var labels = Labels;
        var now = TimeProvider.GetUtcNow();
        var elapsed = _clock.Elapsed(now);
        var timerText = labels.FormatTimer(elapsed);
        var showsElapsed = IsLiveState(_display);
        _face.SetTimer(timerText, showsElapsed && !(_compact && InlineVisible()));
        var info = ComputeLabel(labels, elapsed);
        AutomationProperties.SetName(_head, labels.PillName(AgentName, info.FullText, showsElapsed ? timerText : null));
        if (IsExpanded || _bodyVisible)
        {
            _stepList.RefreshTimes();
        }
    }

    /// <summary>Settles a finished or stopped run back to Ready (the settle timer's callback; tests call it directly).</summary>
    internal void Settle()
    {
        if (!_attached || _settled || EffStatus is not (AgentStatusRunState.Done or AgentStatusRunState.Stopped))
        {
            return;
        }
        _settled = true;
        StopSettle();
        Refresh();
        RaiseEvent(new RoutedEventArgs(SettledEvent));
    }

    private string? StepTime(AgentStatusStep step)
    {
        var labels = Labels;
        switch (step.State)
        {
            case AgentStatusStepState.Done:
                return step.Duration is { } duration ? labels.FormatStepDuration(duration) : null;
            case AgentStatusStepState.Active:
            case AgentStatusStepState.Waiting:
                if (step.StartedAt is not { } started)
                {
                    return null;
                }
                var now = TimeProvider.GetUtcNow();
                var end = _clock.End is { } e && e < now ? e : now;
                return labels.FormatLiveStepDuration(_clock.ActiveBetween(started, end));
            default:
                return null;
        }
    }

    private void ApplyLevel()
    {
        if (!_built)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        _face.Level = level;
        _segments.Level = level;
        _stepList.Level = level;
        // Parts in the overlay layer do not inherit the level set on this control: hand it over explicitly.
        ArcMotion.SetLevel(_surface, GetValue(ArcMotion.LevelProperty));
    }
}
