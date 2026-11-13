using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Scheduled agent routines: each row shows the routine's name and agent, its schedule in words with the next run time,
/// a strip of its recent outcomes, a switch that pauses or resumes it, and Run now. Opening a row shows the agent's
/// instructions and the recent runs with their results. Give it your routines through <see cref="Routines"/> and a
/// <see cref="RunHandler"/> that starts a run on your backend; without them it shows sample routines and Run now plays a
/// short stand-in run. Schedules are five-field cron expressions (<see cref="AgentRoutineSchedule"/>).
/// </summary>
/// <example><code>
/// &lt;arc:AgentRoutines Routines="{Binding Routines}" EnabledChanged="OnRoutineToggled" /&gt;
/// </code></example>
[TemplatePart(ListPart, typeof(StackPanel))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(CompactPseudoClass)]
public class AgentRoutines : TemplatedControl
{
    public const string ListPart = "PART_List";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the list is at most 560 wide: the history strip and Run now move under the schedule.</summary>
    public const string CompactPseudoClass = ":compact";

    #region Properties

    /// <summary>
    /// The routines. A collection that raises change notifications is followed; each routine's own changes are followed
    /// too. Null (the default) shows sample routines.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<AgentRoutine>?> RoutinesProperty =
        AvaloniaProperty.Register<AgentRoutines, IReadOnlyList<AgentRoutine>?>(nameof(Routines));

    /// <summary>How many recent runs the strip of a row shows (10).</summary>
    public static readonly StyledProperty<int> HistoryLengthProperty =
        AvaloniaProperty.Register<AgentRoutines, int>(nameof(HistoryLength), 10);

    /// <summary>Every word the list shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<AgentRoutinesLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentRoutines, AgentRoutinesLabels>(nameof(Labels), new AgentRoutinesLabels());

    /// <summary>True while the list is at most 560 wide.</summary>
    public static readonly DirectProperty<AgentRoutines, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<AgentRoutines, bool>(nameof(IsCompact), o => o.IsCompact);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AgentRoutines, double>(nameof(CompactWidth), 560);

    /// <summary>Raised after the person paused or resumed a routine (its <see cref="AgentRoutine.IsEnabled"/> already changed).</summary>
    public static readonly RoutedEvent<AgentRoutineEventArgs> EnabledChangedEvent =
        RoutedEvent.Register<AgentRoutines, AgentRoutineEventArgs>(nameof(EnabledChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Run now, before the run starts.</summary>
    public static readonly RoutedEvent<AgentRoutineEventArgs> RunRequestedEvent =
        RoutedEvent.Register<AgentRoutines, AgentRoutineEventArgs>(nameof(RunRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when a run started with Run now finished; the run was added to the routine's <see cref="AgentRoutine.Runs"/>.</summary>
    public static readonly RoutedEvent<AgentRoutineEventArgs> RunCompletedEvent =
        RoutedEvent.Register<AgentRoutines, AgentRoutineEventArgs>(nameof(RunCompleted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="RoutinesProperty"/>
    public IReadOnlyList<AgentRoutine>? Routines { get => GetValue(RoutinesProperty); set => SetValue(RoutinesProperty, value); }

    /// <inheritdoc cref="HistoryLengthProperty"/>
    public int HistoryLength { get => GetValue(HistoryLengthProperty); set => SetValue(HistoryLengthProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentRoutinesLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => _isCompact; private set => SetAndRaise(IsCompactProperty, ref _isCompact, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <summary>
    /// Runs a routine now on your backend and returns the finished run; throw to record a failed run with the message. The
    /// token is cancelled when the list leaves the window. Null (the default) waits
    /// <see cref="AgentRoutinesSamples.SimulatedRunTime"/> and records a successful run.
    /// </summary>
    public Func<AgentRoutine, CancellationToken, Task<AgentRoutineRun>>? RunHandler { get; set; }

    /// <inheritdoc cref="EnabledChangedEvent"/>
    public event EventHandler<AgentRoutineEventArgs>? EnabledChanged
    {
        add => AddHandler(EnabledChangedEvent, value);
        remove => RemoveHandler(EnabledChangedEvent, value);
    }

    /// <inheritdoc cref="RunRequestedEvent"/>
    public event EventHandler<AgentRoutineEventArgs>? RunRequested
    {
        add => AddHandler(RunRequestedEvent, value);
        remove => RemoveHandler(RunRequestedEvent, value);
    }

    /// <inheritdoc cref="RunCompletedEvent"/>
    public event EventHandler<AgentRoutineEventArgs>? RunCompleted
    {
        add => AddHandler(RunCompletedEvent, value);
        remove => RemoveHandler(RunCompletedEvent, value);
    }

    #endregion

    private readonly Dictionary<AgentRoutine, AgentRoutinesItem> _items = new();
    private readonly HashSet<AgentRoutine> _running = new();
    private readonly List<AgentRoutine> _observed = new();
    private IReadOnlyList<AgentRoutine>? _sample;
    private INotifyCollectionChanged? _observedCollection;
    private StackPanel? _list;
    private AiAnnouncer? _announcer;
    private TextBlock? _title;
    private TextBlock? _summary;
    private DispatcherTimer? _clock;
    private CancellationTokenSource? _lifetime;
    private bool _isCompact;
    private bool _attached;

    static AgentRoutines()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<AgentRoutines>(AutomationLandmarkType.Region);
    }

    /// <summary>The current time for next runs and relative times; tests replace it.</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    /// <summary>The waits of the stand-in run; tests replace it.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => Task.Delay(time, token);

    internal IReadOnlyList<AgentRoutine> ShownRoutines => Routines ?? (_sample ??= AgentRoutinesSamples.Routines(Clock()));

    internal AgentRoutinesItem? ItemOf(AgentRoutine routine) => _items.GetValueOrDefault(routine);

    internal AiAnnouncer? Announcer => _announcer;

    internal bool IsRunning(AgentRoutine routine) => _running.Contains(routine);

    #region Public actions

    /// <summary>
    /// Runs <paramref name="routine"/> now: raises <see cref="RunRequested"/>, runs it through <see cref="RunHandler"/>,
    /// prepends the run to its <see cref="AgentRoutine.Runs"/> and raises <see cref="RunCompleted"/>. Does nothing while
    /// that routine is already running.
    /// </summary>
    public async Task RunNowAsync(AgentRoutine routine)
    {
        if (!_running.Add(routine))
        {
            return;
        }
        RaiseEvent(new AgentRoutineEventArgs(RunRequestedEvent, routine));
        ItemOf(routine)?.Refresh();
        var token = (_lifetime ??= new CancellationTokenSource()).Token;
        var started = Clock();
        AgentRoutineRun run;
        try
        {
            if (RunHandler is { } handler)
            {
                run = await handler(routine, token);
            }
            else
            {
                await Delay(AgentRoutinesSamples.SimulatedRunTime, token);
                run = new AgentRoutineRun(started, AgentRoutineOutcome.Succeeded, Clock() - started, Labels.ManualRun);
            }
        }
        catch (OperationCanceledException)
        {
            _running.Remove(routine);
            ItemOf(routine)?.Refresh();
            return;
        }
        catch (Exception exception)
        {
            run = new AgentRoutineRun(started, AgentRoutineOutcome.Failed, Clock() - started, exception.Message);
        }
        _running.Remove(routine);
        var runs = new List<AgentRoutineRun>(1 + (routine.Runs?.Count ?? 0)) { run };
        if (routine.Runs is { } old)
        {
            runs.AddRange(old);
        }
        routine.Runs = runs;
        ItemOf(routine)?.Refresh();
        _announcer?.Announce(Labels.RunEnded(routine.Name, Labels.Outcome(run.Outcome)));
        RaiseEvent(new AgentRoutineEventArgs(RunCompletedEvent, routine, run));
    }

    /// <summary>Pauses or resumes <paramref name="routine"/> and raises <see cref="EnabledChanged"/>.</summary>
    public void SetEnabled(AgentRoutine routine, bool enabled)
    {
        if (routine.IsEnabled == enabled)
        {
            return;
        }
        routine.IsEnabled = enabled;
        Rebuild();
        RaiseEvent(new AgentRoutineEventArgs(EnabledChangedEvent, routine));
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _list?.Children.Clear();
        base.OnApplyTemplate(e);
        _list = e.NameScope.Find<StackPanel>(ListPart);
        _announcer = e.NameScope.Find<AiAnnouncer>(AnnouncerPart);
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        _summary = e.NameScope.Find<TextBlock>("PART_Summary");
        Rebuild();
    }

    private void Rebuild()
    {
        var routines = ShownRoutines;
        var keep = new HashSet<AgentRoutine>(routines);
        foreach (var routine in _items.Keys.ToArray())
        {
            if (!keep.Contains(routine))
            {
                _items.Remove(routine);
            }
        }
        var active = 0;
        _list?.Children.Clear();
        foreach (var routine in routines)
        {
            if (routine.IsEnabled)
            {
                active++;
            }
            if (!_items.TryGetValue(routine, out var item))
            {
                item = new AgentRoutinesItem { Routine = routine, Owner = this };
                _items[routine] = item;
            }
            item.Refresh();
            _list?.Children.Add(item);
        }
        if (_title is not null)
        {
            _title.Text = Labels.Title;
        }
        if (_summary is not null)
        {
            _summary.Text = Labels.Summary(active, routines.Count);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RoutinesProperty)
        {
            Observe();
            Rebuild();
        }
        else if (change.Property == LabelsProperty || change.Property == HistoryLengthProperty)
        {
            Rebuild();
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    private void Observe()
    {
        if (_observedCollection is not null)
        {
            _observedCollection.CollectionChanged -= OnRoutinesChanged;
            _observedCollection = null;
        }
        foreach (var routine in _observed)
        {
            routine.PropertyChanged -= OnRoutinePropertyChanged;
        }
        _observed.Clear();
        if (!_attached)
        {
            return;
        }
        if (ShownRoutines is INotifyCollectionChanged notify)
        {
            _observedCollection = notify;
            notify.CollectionChanged += OnRoutinesChanged;
        }
        foreach (var routine in ShownRoutines)
        {
            routine.PropertyChanged += OnRoutinePropertyChanged;
            _observed.Add(routine);
        }
    }

    private void OnRoutinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Observe();
        Rebuild();
    }

    private void OnRoutinePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is AgentRoutine routine)
        {
            if (e.Property == AgentRoutine.IsEnabledProperty)
            {
                Rebuild();
            }
            else
            {
                ItemOf(routine)?.Refresh();
            }
        }
    }

    private void UpdateCompact(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var compact = width <= CompactWidth + 0.01;
        IsCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        foreach (var item in _items.Values)
        {
            item.IsCompact = compact;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateCompact(e.NewSize.Width);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _lifetime = new CancellationTokenSource();
        Observe();
        Rebuild();
        // Next-run and relative times are minute-grained: refresh them twice a minute.
        _clock = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) =>
        {
            foreach (var item in _items.Values)
            {
                item.Refresh();
            }
        });
        _clock.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _clock?.Stop();
        _clock = null;
        _lifetime?.Cancel();
        _lifetime = null;
        Observe();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AgentRoutinesAutomationPeer(this);

    private sealed class AgentRoutinesAutomationPeer(AgentRoutines owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : ((AgentRoutines)Owner).Labels.Title;
    }
}
