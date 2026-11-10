using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A routine of an <see cref="AgentRoutines"/> list: the header (name, agent, schedule and next run) that opens the
/// detail, the recent outcomes, Run now, and the switch. The detail lists the instructions and the recent runs.
/// </summary>
[TemplatePart(HeaderPart, typeof(AgentRoutinesHeader))]
[TemplatePart(SwitchPart, typeof(ToggleButton))]
[TemplatePart(RunPart, typeof(Button))]
[TemplatePart(HistoryPart, typeof(AgentRoutinesHistory))]
[TemplatePart(DetailPart, typeof(AiCollapsible))]
[PseudoClasses(PausedPseudoClass, ExpandedPseudoClass, CompactPseudoClass)]
internal sealed class AgentRoutinesItem : TemplatedControl
{
    public const string HeaderPart = "PART_Header";
    public const string SwitchPart = "PART_Switch";
    public const string RunPart = "PART_Run";
    public const string HistoryPart = "PART_History";
    public const string DetailPart = "PART_Detail";

    public const string PausedPseudoClass = ":paused";
    public const string ExpandedPseudoClass = ":expanded";
    public const string CompactPseudoClass = ":compact";

    private AgentRoutinesHeader? _header;
    private ToggleButton? _switch;
    private Button? _run;
    private AgentRoutinesHistory? _history;
    private AiCollapsible? _detail;
    private bool _expanded;
    private bool _refreshing;

    public AgentRoutine? Routine { get; set; }

    public AgentRoutines? Owner { get; set; }

    public AgentRoutinesHeader? Header => _header;

    public ToggleButton? EnableSwitch => _switch;

    public Button? RunButton => _run;

    public AgentRoutinesHistory? History => _history;

    public bool IsCompact
    {
        get => PseudoClasses.Contains(CompactPseudoClass);
        set => PseudoClasses.Set(CompactPseudoClass, value);
    }

    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            _expanded = value;
            PseudoClasses.Set(ExpandedPseudoClass, value);
            Refresh();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_header is not null)
        {
            _header.Click -= OnHeaderClick;
        }
        if (_switch is not null)
        {
            _switch.IsCheckedChanged -= OnSwitchChanged;
        }
        if (_run is not null)
        {
            _run.Click -= OnRunClick;
        }
        base.OnApplyTemplate(e);
        _header = e.NameScope.Find<AgentRoutinesHeader>(HeaderPart);
        _switch = e.NameScope.Find<ToggleButton>(SwitchPart);
        _run = e.NameScope.Find<Button>(RunPart);
        _history = e.NameScope.Find<AgentRoutinesHistory>(HistoryPart);
        _detail = e.NameScope.Find<AiCollapsible>(DetailPart);
        if (_header is not null)
        {
            _header.Click += OnHeaderClick;
        }
        if (_switch is not null)
        {
            _switch.IsCheckedChanged += OnSwitchChanged;
        }
        if (_run is not null)
        {
            _run.Click += OnRunClick;
        }
        Refresh();
    }

    private void OnHeaderClick(object? sender, RoutedEventArgs e) => IsExpanded = !_expanded;

    private void OnSwitchChanged(object? sender, RoutedEventArgs e)
    {
        if (!_refreshing && Routine is { } routine && Owner is { } owner && _switch is not null)
        {
            owner.SetEnabled(routine, _switch.IsChecked == true);
        }
    }

    private void OnRunClick(object? sender, RoutedEventArgs e)
    {
        if (Routine is { } routine && Owner is { } owner)
        {
            _ = owner.RunNowAsync(routine);
        }
    }

    public void Refresh()
    {
        if (Routine is not { } routine)
        {
            return;
        }
        var owner = Owner;
        var labels = owner?.Labels ?? new AgentRoutinesLabels();
        var now = owner?.Clock() ?? DateTimeOffset.Now;
        var running = owner?.IsRunning(routine) == true;
        PseudoClasses.Set(PausedPseudoClass, !routine.IsEnabled);
        _refreshing = true;
        try
        {
            if (_switch is not null)
            {
                _switch.IsChecked = routine.IsEnabled;
                AutomationProperties.SetName(_switch, labels.EnableName(routine.Name));
            }
        }
        finally
        {
            _refreshing = false;
        }
        string status;
        if (running)
        {
            status = labels.Running;
        }
        else if (!routine.IsEnabled)
        {
            status = labels.Paused;
        }
        else
        {
            status = AgentRoutineSchedule.Next(routine.Schedule, now) is { } next ? labels.Next(labels.Time(next, now)) : string.Empty;
        }
        var runs = routine.Runs ?? [];
        var count = Math.Min(Math.Max(0, owner?.HistoryLength ?? 10), runs.Count);
        var ok = 0;
        for (var i = 0; i < count; i++)
        {
            if (runs[i].Outcome == AgentRoutineOutcome.Succeeded)
            {
                ok++;
            }
        }
        if (_header is not null)
        {
            _header.Title = routine.Name;
            _header.Agent = routine.Agent;
            _header.ScheduleText = labels.Schedule(routine.Schedule);
            _header.StatusText = status;
            _header.IsExpanded = _expanded;
            AutomationProperties.SetName(_header, $"{routine.Name}, {_header.ScheduleText}, {status}");
            AutomationProperties.SetHelpText(_header, labels.DetailsName(routine.Name));
        }
        if (_history is not null)
        {
            _history.SetRuns(runs, owner?.HistoryLength ?? 10);
            AutomationProperties.SetName(_history, count == 0 ? labels.NoRuns : labels.History(ok, count));
            ToolTip.SetTip(_history, count == 0 ? labels.NoRuns : labels.History(ok, count));
        }
        if (_run is not null)
        {
            _run.Content = labels.RunNow;
            _run.IsLoading = running;
            AutomationProperties.SetName(_run, $"{labels.RunNow}: {routine.Name}");
        }
        if (_detail is not null)
        {
            if (_expanded)
            {
                _detail.Content = BuildDetail(routine, labels, now);
            }
            _detail.IsExpanded = _expanded;
        }
    }

    private static Control BuildDetail(AgentRoutine routine, AgentRoutinesLabels labels, DateTimeOffset now)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(Heading(labels.Instructions));
        var instructions = new SelectableTextBlock { Text = routine.Instructions, TextWrapping = TextWrapping.Wrap, LineHeight = 21 };
        ArcTokens.Bind(instructions, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(instructions, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var quote = new Border { Child = instructions, Padding = new Thickness(12, 10) }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(quote, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        panel.Children.Add(quote);
        panel.Children.Add(Heading(labels.RecentRuns));
        var runs = routine.Runs ?? [];
        if (runs.Count == 0)
        {
            panel.Children.Add(Small(labels.NoRuns, ArcDesignTokenKind.TextMuted));
        }
        var list = new StackPanel { Spacing = 6 };
        for (var i = 0; i < runs.Count && i < 6; i++)
        {
            var run = runs[i];
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
            var dot = new Avalonia.Controls.Shapes.Ellipse { Width = 7, Height = 7, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(dot, Avalonia.Controls.Shapes.Shape.FillProperty, OutcomeColor(run.Outcome));
            var outcome = Small(labels.Outcome(run.Outcome), ArcDesignTokenKind.Foreground);
            outcome.Margin = new Thickness(0, 0, 10, 0);
            outcome.MinWidth = 70;
            var summary = Small(run.Summary, ArcDesignTokenKind.TextSecondary);
            summary.TextTrimming = TextTrimming.CharacterEllipsis;
            var when = Small($"{labels.Time(run.Started, now)} · {labels.Duration(run.Duration)}", ArcDesignTokenKind.TextMuted);
            when.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(outcome, 1);
            Grid.SetColumn(summary, 2);
            Grid.SetColumn(when, 3);
            row.Children.Add(dot);
            row.Children.Add(outcome);
            row.Children.Add(summary);
            row.Children.Add(when);
            AutomationProperties.SetName(row, $"{labels.Outcome(run.Outcome)}, {run.Summary}, {when.Text}");
            list.Children.Add(row);
        }
        panel.Children.Add(list);
        return panel;
    }

    internal static ArcDesignTokenKind OutcomeColor(AgentRoutineOutcome outcome) => outcome switch
    {
        AgentRoutineOutcome.Failed => ArcDesignTokenKind.Danger,
        AgentRoutineOutcome.Skipped => ArcDesignTokenKind.BorderStrong,
        _ => ArcDesignTokenKind.Success
    };

    private static TextBlock Heading(string text)
    {
        var block = new TextBlock { Text = text };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(block, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        return block;
    }

    private static TextBlock Small(string? text, ArcDesignTokenKind color)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, FontFeatures = FontFeatureCollection.Parse("tnum") };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        return block;
    }
}

/// <summary>The pressable header of a routine: name and agent over the schedule and the next run, with a chevron.</summary>
[PseudoClasses(ExpandedPseudoClass)]
internal sealed class AgentRoutinesHeader : AiAgentButton
{
    public const string ExpandedPseudoClass = ":expanded";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentRoutinesHeader, string?>(nameof(Title));

    public static readonly StyledProperty<string?> AgentProperty =
        AvaloniaProperty.Register<AgentRoutinesHeader, string?>(nameof(Agent));

    public static readonly StyledProperty<string?> ScheduleTextProperty =
        AvaloniaProperty.Register<AgentRoutinesHeader, string?>(nameof(ScheduleText));

    public static readonly StyledProperty<string?> StatusTextProperty =
        AvaloniaProperty.Register<AgentRoutinesHeader, string?>(nameof(StatusText));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AgentRoutinesHeader, bool>(nameof(IsExpanded));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Agent { get => GetValue(AgentProperty); set => SetValue(AgentProperty, value); }

    public string? ScheduleText { get => GetValue(ScheduleTextProperty); set => SetValue(ScheduleTextProperty, value); }

    public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HeaderPeer(this);

    private sealed class HeaderPeer(AgentRoutinesHeader owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private AgentRoutinesHeader Header => (AgentRoutinesHeader)Owner;

        public ExpandCollapseState ExpandCollapseState => Header.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Header.IsExpanded)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (Header.IsExpanded)
            {
                Invoke();
            }
        }
    }
}

/// <summary>
/// A routine's recent outcomes as a row of small rounded bars, oldest on the left: succeeded in the success color, failed
/// in the danger color and taller, skipped as a short muted bar. Drawn in one pass.
/// </summary>
internal sealed class AgentRoutinesHistory : Control
{
    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<AgentRoutinesHistory, IBrush?>(nameof(SuccessBrush));

    public static readonly StyledProperty<IBrush?> FailureBrushProperty =
        AvaloniaProperty.Register<AgentRoutinesHistory, IBrush?>(nameof(FailureBrush));

    public static readonly StyledProperty<IBrush?> SkipBrushProperty =
        AvaloniaProperty.Register<AgentRoutinesHistory, IBrush?>(nameof(SkipBrush));

    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }

    public IBrush? FailureBrush { get => GetValue(FailureBrushProperty); set => SetValue(FailureBrushProperty, value); }

    public IBrush? SkipBrush { get => GetValue(SkipBrushProperty); set => SetValue(SkipBrushProperty, value); }

    private const double BarWidth = 4;
    private const double Gap = 3;
    private AgentRoutineOutcome[] _outcomes = [];
    private int _slots = 10;

    static AgentRoutinesHistory()
    {
        AffectsRender<AgentRoutinesHistory>(SuccessBrushProperty, FailureBrushProperty, SkipBrushProperty);
    }

    /// <summary>The outcomes drawn, oldest first (tests read them).</summary>
    public IReadOnlyList<AgentRoutineOutcome> Outcomes => _outcomes;

    public void SetRuns(IReadOnlyList<AgentRoutineRun> runs, int slots)
    {
        slots = Math.Max(0, slots);
        var count = Math.Min(slots, runs.Count);
        var outcomes = new AgentRoutineOutcome[count];
        for (var i = 0; i < count; i++)
        {
            outcomes[count - 1 - i] = runs[i].Outcome;
        }
        if (slots != _slots)
        {
            _slots = slots;
            InvalidateMeasure();
        }
        if (!outcomes.AsSpan().SequenceEqual(_outcomes))
        {
            _outcomes = outcomes;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(_slots == 0 ? 0 : _slots * BarWidth + (_slots - 1) * Gap, 16);

    public override void Render(DrawingContext context)
    {
        var height = Bounds.Height;
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var start = (_slots - _outcomes.Length) * (BarWidth + Gap);
        for (var i = 0; i < _outcomes.Length; i++)
        {
            var outcome = _outcomes[i];
            var (brush, barHeight) = outcome switch
            {
                AgentRoutineOutcome.Failed => (FailureBrush, height),
                AgentRoutineOutcome.Skipped => (SkipBrush, height * 0.4),
                _ => (SuccessBrush, height * 0.75)
            };
            if (brush is null)
            {
                continue;
            }
            var rect = new Rect(start + i * (BarWidth + Gap), height - barHeight, BarWidth, barHeight);
            context.DrawRectangle(brush, null, rect, BarWidth / 2, BarWidth / 2);
        }
    }
}
