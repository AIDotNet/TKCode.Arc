using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class AgentOrchestrator
{
    private Panel? _host;
    private AgentOrchestratorGraph? _graph;
    private TextBlock? _title;
    private AiStatusPill? _runPill;
    private TextBlock? _goal;
    private TextBlock? _progress;
    private Button? _run;
    private TextBlock? _runText;
    private ArcGlyph? _runGlyph;
    private Button? _reset;
    private TextBlock? _logHeading;
    private TextBlock? _logEmpty;
    private ScrollViewer? _logScroll;
    private ItemsControl? _log;
    private Control? _detail;
    private TextBlock? _detailName;
    private TextBlock? _detailRole;
    private TextBlock? _detailStatus;
    private TextBlock? _detailModel;
    private TextBlock? _detailTraffic;
    private TextBlock? _taskHeading;
    private TextBlock? _detailTask;
    private TextBlock? _activityHeading;
    private TextBlock? _detailActivity;
    private TextBlock? _resultHeading;
    private TextBlock? _detailResult;
    private bool _followLog = true;
    private bool _scrollQueued;

    /// <summary>The graph (null until the template is applied).</summary>
    internal AgentOrchestratorGraph? Graph => _graph;

    /// <summary>The log's items control (null until the template is applied).</summary>
    internal ItemsControl? Log => _log;

    internal Button? RunButton => _run;

    internal Button? ResetButton => _reset;

    internal TextBlock? ProgressText => _progress;

    internal TextBlock? DetailNameText => _detailName;

    internal TextBlock? DetailTaskText => _detailTask;

    internal TextBlock? DetailResultText => _detailResult;

    internal TextBlock? DetailTrafficText => _detailTraffic;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_graph is not null)
        {
            _graph.NodeInvoked -= OnNodeInvoked;
            _graph.SetAgents([], Labels);
        }
        if (_run is not null)
        {
            _run.Click -= OnRunClick;
        }
        if (_reset is not null)
        {
            _reset.Click -= OnResetClick;
        }
        if (_logScroll is not null)
        {
            _logScroll.ScrollChanged -= OnLogScrollChanged;
        }
        _host?.Children.Remove(_announcer);

        var scope = e.NameScope;
        _host = scope.Find<Panel>(HostPart);
        _graph = scope.Find<AgentOrchestratorGraph>(GraphPart);
        _title = scope.Find<TextBlock>(TitlePart);
        _runPill = scope.Find<AiStatusPill>(RunPillPart);
        _goal = scope.Find<TextBlock>(GoalPart);
        _progress = scope.Find<TextBlock>(ProgressPart);
        _run = scope.Find<Button>(RunPart);
        _runText = scope.Find<TextBlock>(RunTextPart);
        _runGlyph = scope.Find<ArcGlyph>(RunGlyphPart);
        _reset = scope.Find<Button>(ResetPart);
        _logHeading = scope.Find<TextBlock>(LogHeadingPart);
        _logEmpty = scope.Find<TextBlock>(LogEmptyPart);
        _logScroll = scope.Find<ScrollViewer>(LogScrollPart);
        _log = scope.Find<ItemsControl>(LogPart);
        _detail = scope.Find<Control>(DetailPart);
        _detailName = scope.Find<TextBlock>(DetailNamePart);
        _detailRole = scope.Find<TextBlock>(DetailRolePart);
        _detailStatus = scope.Find<TextBlock>(DetailStatusPart);
        _detailModel = scope.Find<TextBlock>(DetailModelPart);
        _detailTraffic = scope.Find<TextBlock>(DetailTrafficPart);
        _taskHeading = scope.Find<TextBlock>(TaskHeadingPart);
        _detailTask = scope.Find<TextBlock>(DetailTaskPart);
        _activityHeading = scope.Find<TextBlock>(ActivityHeadingPart);
        _detailActivity = scope.Find<TextBlock>(DetailActivityPart);
        _resultHeading = scope.Find<TextBlock>(ResultHeadingPart);
        _detailResult = scope.Find<TextBlock>(DetailResultPart);

        if (_graph is not null)
        {
            _graph.NodeInvoked += OnNodeInvoked;
            _graph.SetAgents(_ordered, Labels);
        }
        if (_run is not null)
        {
            _run.Click += OnRunClick;
        }
        if (_reset is not null)
        {
            _reset.Click += OnResetClick;
        }
        if (_logScroll is not null)
        {
            _logScroll.ScrollChanged += OnLogScrollChanged;
        }
        if (_log is not null)
        {
            _log.ItemTemplate = new FuncDataTemplate<OrchestratorMessage>((_, _) => new AgentOrchestratorLogRow(this), supportsRecycling: true);
            _log.ItemsSource = _messages;
        }
        _host?.Children.Add(_announcer);
        UpdateSelection();
        UpdateTexts();
        UpdateRunParts();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_pausedByDetach)
        {
            _pausedByDetach = false;
            if (RunState == OrchestratorRunState.Paused)
            {
                Resume();
            }
        }
        else if (IsAutoStart && RunState == OrchestratorRunState.Idle && EffectiveScript is not null)
        {
            Start();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Leaving the screen pauses the script; coming back resumes it from the same step.
        if (RunState == OrchestratorRunState.Running && EffectiveScript is not null)
        {
            Pause();
            _pausedByDetach = true;
        }
    }

    private bool _pausedByDetach;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PseudoClasses.Set(CompactPseudoClass, e.NewSize.Width <= CompactWidth);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == LeadIdProperty)
        {
            RebuildAgents();
        }
        else if (property == SelectedAgentIdProperty)
        {
            UpdateSelection();
        }
        else if (property == LabelsProperty)
        {
            _graph?.SetLabels(Labels);
            UpdateTexts();
            UpdateRunParts();
            RefreshLogRows();
        }
        else if (property == TitleProperty || property == GoalProperty)
        {
            UpdateTexts();
        }
        else if (property == ScriptProperty)
        {
            if (RunState is OrchestratorRunState.Running or OrchestratorRunState.Paused)
            {
                Reset();
            }
            UpdateRunParts();
        }
        else if (property == CompactWidthProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, Bounds.Width > 0 && Bounds.Width <= CompactWidth);
        }
    }

    private void OnRunClick(object? sender, RoutedEventArgs e) => Toggle();

    private void OnResetClick(object? sender, RoutedEventArgs e) => Reset();

    private void OnNodeInvoked(int index)
    {
        if (index >= 0 && index < _ordered.Length)
        {
            SetCurrentValue(SelectedAgentIdProperty, _ordered[index].Id);
        }
    }

    private void UpdateSelection()
    {
        var selected = SelectedAgent;
        _graph?.Select(selected is null ? -1 : Array.IndexOf(_ordered, selected));
        UpdateDetail();
    }

    /// <summary>The title, the goal and the progress line.</summary>
    private void UpdateTexts()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = Title ?? labels.Title;
        }
        if (_goal is not null)
        {
            var goal = EffectiveGoal;
            _goal.Text = goal;
            _goal.IsVisible = !string.IsNullOrWhiteSpace(goal);
        }
        if (_progress is not null)
        {
            var done = 0;
            foreach (var agent in _ordered)
            {
                if (agent.Status == OrchestratorAgentStatus.Done)
                {
                    done++;
                }
            }
            _progress.Text = labels.Progress(done, _ordered.Length, _messages.Count);
        }
        if (_logHeading is not null)
        {
            _logHeading.Text = labels.MessagesHeading;
        }
        if (_logEmpty is not null)
        {
            _logEmpty.Text = labels.NoMessages;
        }
        if (_log is not null)
        {
            AutomationProperties.SetName(_log, labels.LogName);
        }
        if (_graph is not null)
        {
            AutomationProperties.SetName(_graph, labels.GraphName);
        }
        if (_taskHeading is not null)
        {
            _taskHeading.Text = labels.TaskHeading;
        }
        if (_activityHeading is not null)
        {
            _activityHeading.Text = labels.ActivityHeading;
        }
        if (_resultHeading is not null)
        {
            _resultHeading.Text = labels.ResultHeading;
        }
        PseudoClasses.Set(EmptyLogPseudoClass, _messages.Count == 0);
        UpdateDetail();
    }

    /// <summary>The detail panel for the selected agent (or the lead).</summary>
    private void UpdateDetail()
    {
        var agent = SelectedAgent;
        var labels = Labels;
        if (_detailName is not null)
        {
            _detailName.Text = agent?.Name;
        }
        if (_detailRole is not null)
        {
            var role = agent is null ? null : ReferenceEquals(agent, Lead) ? Join(labels.Lead, agent.Role) : agent.Role;
            _detailRole.Text = role;
            _detailRole.IsVisible = !string.IsNullOrEmpty(role);
        }
        if (_detailStatus is not null)
        {
            var status = agent?.Status ?? OrchestratorAgentStatus.Idle;
            _detailStatus.Text = labels.StatusName(status);
            _detailStatus.Classes.Set("done", status == OrchestratorAgentStatus.Done);
            _detailStatus.Classes.Set("failed", status == OrchestratorAgentStatus.Failed);
            _detailStatus.Classes.Set("active", status is OrchestratorAgentStatus.Thinking or OrchestratorAgentStatus.Working
                or OrchestratorAgentStatus.Waiting);
        }
        if (_detailModel is not null)
        {
            _detailModel.Text = agent?.Model is { Length: > 0 } model ? $"{labels.ModelHeading}: {model}" : null;
            _detailModel.IsVisible = !string.IsNullOrEmpty(agent?.Model);
        }
        if (_detailTraffic is not null)
        {
            int sent = 0, received = 0;
            if (agent is not null)
            {
                foreach (var message in _messages)
                {
                    if (message.From == agent.Id)
                    {
                        sent++;
                    }
                    if (message.To == agent.Id)
                    {
                        received++;
                    }
                }
            }
            _detailTraffic.Text = labels.Traffic(sent, received);
        }
        SetValueText(_detailTask, agent?.Task, labels.Nothing);
        SetValueText(_detailActivity, agent?.Activity, labels.Nothing);
        SetValueText(_detailResult, agent?.Result, labels.Nothing);
        if (_detail is not null && agent is not null)
        {
            AutomationProperties.SetName(_detail, labels.DetailName(agent.Name));
        }
    }

    private static string? Join(string a, string? b) => string.IsNullOrEmpty(b) ? a : $"{a} · {b}";

    private static void SetValueText(TextBlock? block, string? value, string nothing)
    {
        if (block is null)
        {
            return;
        }
        var empty = string.IsNullOrWhiteSpace(value);
        block.Text = empty ? nothing : value;
        block.Classes.Set("empty", empty);
    }

    /// <summary>The main button's word and icon, the reset button's name and the status pill.</summary>
    private void UpdateRunParts()
    {
        var labels = Labels;
        var state = RunState;
        PseudoClasses.Set(IdlePseudoClass, state == OrchestratorRunState.Idle);
        PseudoClasses.Set(RunningPseudoClass, state == OrchestratorRunState.Running);
        PseudoClasses.Set(PausedPseudoClass, state == OrchestratorRunState.Paused);
        PseudoClasses.Set(CompletedPseudoClass, state == OrchestratorRunState.Completed);
        PseudoClasses.Set(FailedPseudoClass, state == OrchestratorRunState.Failed);
        var word = state switch
        {
            OrchestratorRunState.Running => labels.Pause,
            OrchestratorRunState.Paused => labels.Resume,
            OrchestratorRunState.Completed or OrchestratorRunState.Failed => labels.Replay,
            _ => labels.Start,
        };
        if (_runText is not null)
        {
            _runText.Text = word;
        }
        if (_runGlyph is not null)
        {
            _runGlyph.Data = state switch
            {
                OrchestratorRunState.Running => ArcIcons.Pause,
                OrchestratorRunState.Completed or OrchestratorRunState.Failed => ArcIcons.RotateCw,
                _ => ArcIcons.Play,
            };
        }
        if (_run is not null)
        {
            AutomationProperties.SetName(_run, word);
        }
        if (_reset is not null)
        {
            AutomationProperties.SetName(_reset, labels.Reset);
            Tooltip.SetTip(_reset, labels.Reset);
            _reset.IsEnabled = state != OrchestratorRunState.Idle || _messages.Count > 0;
        }
        if (_runPill is not null)
        {
            _runPill.ThinkingText = labels.RunStateName(OrchestratorRunState.Running);
            _runPill.StoppedText = labels.RunStateName(OrchestratorRunState.Paused);
            _runPill.DoneText = labels.RunStateName(OrchestratorRunState.Completed);
            _runPill.FailedText = labels.RunStateName(OrchestratorRunState.Failed);
            _runPill.Status = state switch
            {
                OrchestratorRunState.Running => ArenaPaneStatus.Thinking,
                OrchestratorRunState.Paused => ArenaPaneStatus.Stopped,
                OrchestratorRunState.Completed => ArenaPaneStatus.Done,
                OrchestratorRunState.Failed => ArenaPaneStatus.Failed,
                _ => ArenaPaneStatus.Idle,
            };
        }
    }

    #region Log

    private void OnMessageAdded(OrchestratorMessage message)
    {
        UpdateTexts();
        if (_reset is not null)
        {
            _reset.IsEnabled = true;
        }
        if (_followLog && !_scrollQueued && _logScroll is not null)
        {
            _scrollQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _scrollQueued = false;
                _logScroll?.ScrollToEnd();
            }, DispatcherPriority.Loaded);
        }
    }

    /// <summary>The log follows new messages until the person scrolls up, and again once they are back at the end.</summary>
    private void OnLogScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_logScroll is not { } scroll || e.OffsetDelta.Y == 0)
        {
            return;
        }
        _followLog = scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - 8;
    }

    private void RefreshLogRows()
    {
        if (_log is null)
        {
            return;
        }
        foreach (var row in _log.GetRealizedContainers())
        {
            if (row is ContentPresenter { Child: AgentOrchestratorLogRow logRow })
            {
                logRow.Refresh();
            }
        }
    }

    #endregion
}
