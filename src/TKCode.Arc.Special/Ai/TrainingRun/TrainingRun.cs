using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A fine-tuning job card (Arc Training run, design reference): the base model with its mark and the job's name, a live
/// train and validation loss curve drawn across a fixed step axis that doubles as the epoch progress bar, the step,
/// learning rate, elapsed time and ETA, the newest three events (evals, checkpoints, warnings), Pause and Cancel while it
/// runs, and the results with a Deploy button once it completes.
/// <para>
/// Drive it from your job: set <see cref="Status"/>, <see cref="TotalSteps"/> and <see cref="Epochs"/> from the job
/// config so the axis is right from the first sample, then keep <see cref="Step"/>, <see cref="LearningRate"/>,
/// <see cref="Elapsed"/> and <see cref="Eta"/> current and append to <see cref="Loss"/> and <see cref="Events"/> as they
/// stream (no need to throttle: the card eases toward each sample), or hand it updates with <see cref="Apply"/> or
/// <see cref="FollowAsync"/>. The buttons raise events; Deploy calls <see cref="DeployHandler"/>. With no
/// <see cref="Status"/> it plays its <see cref="Simulation"/>, a job you describe, as <see cref="Scenario"/> says, freezing
/// while the card is off screen; with neither it shows a queued job with no data yet.
/// </para>
/// </summary>
[TemplatePart(ChartPart, typeof(TrainingRunChart))]
[TemplatePart(EventsPart, typeof(TrainingRunEventList))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(QueuedPseudoClass, RunningPseudoClass, PausedPseudoClass, CompletedPseudoClass, FailedPseudoClass,
    CancelledPseudoClass, NarrowPseudoClass, InspectingPseudoClass)]
public sealed partial class TrainingRun : TemplatedControl
{
    internal const string QueuedPseudoClass = ":queued";
    internal const string RunningPseudoClass = ":running";
    internal const string PausedPseudoClass = ":paused";
    internal const string CompletedPseudoClass = ":completed";
    internal const string FailedPseudoClass = ":failed";
    internal const string CancelledPseudoClass = ":cancelled";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string InspectingPseudoClass = ":inspecting";

    /// <summary>Under this width (px) the card goes narrow: 16px padding, two stat columns, "Val", a stacked footer.</summary>
    internal const double NarrowBreakpoint = 440;

    /// <summary>How long Confirm cancel waits for the second press.</summary>
    internal static readonly TimeSpan ConfirmCancelWindow = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Where the job is. Leave it null to play the <see cref="Simulation"/> (as <see cref="Scenario"/> says), or to show a
    /// queued job with no data without one.
    /// </summary>
    public static readonly StyledProperty<TrainingRunStatus?> StatusProperty =
        AvaloniaProperty.Register<TrainingRun, TrainingRunStatus?>(nameof(Status));

    /// <summary>The job or output model name; null shows the simulation's name while it plays, else "Fine-tuning job".</summary>
    public static readonly StyledProperty<string?> JobNameProperty =
        AvaloniaProperty.Register<TrainingRun, string?>(nameof(JobName));

    /// <summary>The base model and its 24px mark (a monogram when the model has no mark); null shows the simulation's while it plays.</summary>
    public static readonly StyledProperty<AgentModel?> BaseModelProperty =
        AvaloniaProperty.Register<TrainingRun, AgentModel?>(nameof(BaseModel));

    /// <summary>Short facts after the model name, such as "LoRA r16" and "8× H200".</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> DetailsProperty =
        AvaloniaProperty.Register<TrainingRun, IReadOnlyList<string>?>(nameof(Details));

    /// <summary>The training data, shown in the footer with its 14px mark and size.</summary>
    public static readonly StyledProperty<TrainingRunDataset?> DatasetProperty =
        AvaloniaProperty.Register<TrainingRun, TrainingRunDataset?>(nameof(Dataset));

    /// <summary>Optimizer steps done.</summary>
    public static readonly StyledProperty<int> StepProperty =
        AvaloniaProperty.Register<TrainingRun, int>(nameof(Step), coerce: static (_, v) => Math.Max(0, v));

    /// <summary>Steps in the whole run. It fixes the x axis so the curve never rescales mid-run.</summary>
    public static readonly StyledProperty<int> TotalStepsProperty =
        AvaloniaProperty.Register<TrainingRun, int>(nameof(TotalSteps), 1, coerce: static (_, v) => Math.Max(1, v));

    /// <summary>Number of epochs: the axis splits into one band and one progress segment per epoch.</summary>
    public static readonly StyledProperty<int> EpochsProperty =
        AvaloniaProperty.Register<TrainingRun, int>(nameof(Epochs), 1, coerce: static (_, v) => Math.Max(1, v));

    /// <summary>The current learning rate, shown as 1.84e-4.</summary>
    public static readonly StyledProperty<double?> LearningRateProperty =
        AvaloniaProperty.Register<TrainingRun, double?>(nameof(LearningRate));

    /// <summary>Time spent training so far, excluding pauses.</summary>
    public static readonly StyledProperty<TimeSpan?> ElapsedProperty =
        AvaloniaProperty.Register<TrainingRun, TimeSpan?>(nameof(Elapsed));

    /// <summary>Estimated time left. While queued it reads as "About 18m 40s".</summary>
    public static readonly StyledProperty<TimeSpan?> EtaProperty =
        AvaloniaProperty.Register<TrainingRun, TimeSpan?>(nameof(Eta));

    /// <summary>Up to four results that replace the live stats on completion; null shows the final train and validation loss, the steps and the duration.</summary>
    public static readonly StyledProperty<IReadOnlyList<TrainingRunMetric>?> MetricsProperty =
        AvaloniaProperty.Register<TrainingRun, IReadOnlyList<TrainingRunMetric>?>(nameof(Metrics));

    /// <summary>A line over the empty chart while queued or starting, such as "Waiting for 8× H200 · 2nd in queue".</summary>
    public static readonly StyledProperty<string?> ActivityProperty =
        AvaloniaProperty.Register<TrainingRun, string?>(nameof(Activity));

    /// <summary>Why the run failed, shown in the footer in place of the dataset.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<TrainingRun, string?>(nameof(ErrorText));

    /// <summary>A one-off message for screen readers, such as the result of an action.</summary>
    public static readonly StyledProperty<string?> NoticeProperty =
        AvaloniaProperty.Register<TrainingRun, string?>(nameof(Notice));

    /// <summary>Which action buttons may show (all by default; None for a read-only monitor). A button also needs the right status.</summary>
    public static readonly StyledProperty<TrainingRunActions> ActionsProperty =
        AvaloniaProperty.Register<TrainingRun, TrainingRunActions>(nameof(Actions), TrainingRunActions.All);

    /// <summary>
    /// Deploys the completed model: called when Deploy is pressed, with a token that is cancelled when the card unloads or
    /// the run leaves Completed. Return the endpoint (or null). The button loads while it runs; a thrown exception shows its
    /// message and Retry deploy. Deploy shows only when this is set (or while simulating).
    /// </summary>
    public static readonly StyledProperty<Func<CancellationToken, Task<string?>>?> DeployHandlerProperty =
        AvaloniaProperty.Register<TrainingRun, Func<CancellationToken, Task<string?>>?>(nameof(DeployHandler));

    /// <summary>How the <see cref="Simulation"/> plays while <see cref="Status"/> is null: Healthy, or Oom (fails once at step 940).</summary>
    public static readonly StyledProperty<TrainingRunScenario> ScenarioProperty =
        AvaloniaProperty.Register<TrainingRun, TrainingRunScenario>(nameof(Scenario));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<TrainingRunLabels> LabelsProperty =
        AvaloniaProperty.Register<TrainingRun, TrainingRunLabels>(nameof(Labels), TrainingRunLabels.Default);

    /// <summary>Raised when Pause is pressed. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> PauseRequestedEvent =
        RoutedEvent.Register<TrainingRun, RoutedEventArgs>(nameof(PauseRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Resume is pressed. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ResumeRequestedEvent =
        RoutedEvent.Register<TrainingRun, RoutedEventArgs>(nameof(ResumeRequested), RoutingStrategies.Bubble);

    /// <summary>Raised on the second press, on Confirm cancel, within 3 seconds of the first. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CancelRequestedEvent =
        RoutedEvent.Register<TrainingRun, RoutedEventArgs>(nameof(CancelRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Retry from checkpoint is pressed after a failure. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<TrainingRun, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Run again is pressed after the run completed, failed or was cancelled. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RestartRequestedEvent =
        RoutedEvent.Register<TrainingRun, RoutedEventArgs>(nameof(RestartRequested), RoutingStrategies.Bubble);

    private readonly AvaloniaList<TrainingRunLossPoint> _loss = [];
    private readonly AvaloniaList<TrainingRunEvent> _events = [];
    private TrainingRunPlayer? _player;
    private LiveLoop? _simulationLoop;
    private int _lossVersion;
    private int _eventsVersion;

    /// <summary>Creates the card.</summary>
    public TrainingRun()
    {
        SpecialFocusModality.Watch();
        _loss.CollectionChanged += OnLossChanged;
        _events.CollectionChanged += OnEventsChanged;
    }

    /// <inheritdoc cref="StatusProperty"/>
    public TrainingRunStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="JobNameProperty"/>
    public string? JobName { get => GetValue(JobNameProperty); set => SetValue(JobNameProperty, value); }

    /// <inheritdoc cref="BaseModelProperty"/>
    public AgentModel? BaseModel { get => GetValue(BaseModelProperty); set => SetValue(BaseModelProperty, value); }

    /// <inheritdoc cref="DetailsProperty"/>
    public IReadOnlyList<string>? Details { get => GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }

    /// <inheritdoc cref="DatasetProperty"/>
    public TrainingRunDataset? Dataset { get => GetValue(DatasetProperty); set => SetValue(DatasetProperty, value); }

    /// <inheritdoc cref="StepProperty"/>
    public int Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <inheritdoc cref="TotalStepsProperty"/>
    public int TotalSteps { get => GetValue(TotalStepsProperty); set => SetValue(TotalStepsProperty, value); }

    /// <inheritdoc cref="EpochsProperty"/>
    public int Epochs { get => GetValue(EpochsProperty); set => SetValue(EpochsProperty, value); }

    /// <inheritdoc cref="LearningRateProperty"/>
    public double? LearningRate { get => GetValue(LearningRateProperty); set => SetValue(LearningRateProperty, value); }

    /// <inheritdoc cref="ElapsedProperty"/>
    public TimeSpan? Elapsed { get => GetValue(ElapsedProperty); set => SetValue(ElapsedProperty, value); }

    /// <inheritdoc cref="EtaProperty"/>
    public TimeSpan? Eta { get => GetValue(EtaProperty); set => SetValue(EtaProperty, value); }

    /// <inheritdoc cref="MetricsProperty"/>
    public IReadOnlyList<TrainingRunMetric>? Metrics { get => GetValue(MetricsProperty); set => SetValue(MetricsProperty, value); }

    /// <inheritdoc cref="ActivityProperty"/>
    public string? Activity { get => GetValue(ActivityProperty); set => SetValue(ActivityProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="NoticeProperty"/>
    public string? Notice { get => GetValue(NoticeProperty); set => SetValue(NoticeProperty, value); }

    /// <inheritdoc cref="ActionsProperty"/>
    public TrainingRunActions Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <inheritdoc cref="DeployHandlerProperty"/>
    public Func<CancellationToken, Task<string?>>? DeployHandler { get => GetValue(DeployHandlerProperty); set => SetValue(DeployHandlerProperty, value); }

    /// <inheritdoc cref="ScenarioProperty"/>
    public TrainingRunScenario Scenario { get => GetValue(ScenarioProperty); set => SetValue(ScenarioProperty, value); }

    /// <summary>
    /// The job the card plays on its own while <see cref="Status"/> is null: its name, base model, details, dataset,
    /// hardware and endpoint, played as <see cref="Scenario"/> says. Null shows a queued job with no data instead.
    /// </summary>
    public static readonly StyledProperty<TrainingRunSimulation?> SimulationProperty =
        AvaloniaProperty.Register<TrainingRun, TrainingRunSimulation?>(nameof(Simulation));

    /// <inheritdoc cref="SimulationProperty"/>
    public TrainingRunSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public TrainingRunLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The loss samples, oldest first. Append as they stream; clear it for a reset, remove the newest for a rollback.</summary>
    public AvaloniaList<TrainingRunLossPoint> Loss => _loss;

    /// <summary>The event log, oldest first; the newest three show.</summary>
    public AvaloniaList<TrainingRunEvent> Events => _events;

    /// <inheritdoc cref="PauseRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? PauseRequested
    {
        add => AddHandler(PauseRequestedEvent, value);
        remove => RemoveHandler(PauseRequestedEvent, value);
    }

    /// <inheritdoc cref="ResumeRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ResumeRequested
    {
        add => AddHandler(ResumeRequestedEvent, value);
        remove => RemoveHandler(ResumeRequestedEvent, value);
    }

    /// <inheritdoc cref="CancelRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? CancelRequested
    {
        add => AddHandler(CancelRequestedEvent, value);
        remove => RemoveHandler(CancelRequestedEvent, value);
    }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <inheritdoc cref="RestartRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RestartRequested
    {
        add => AddHandler(RestartRequestedEvent, value);
        remove => RemoveHandler(RestartRequestedEvent, value);
    }

    /// <summary>
    /// Applies one update of a running job: every value it sets replaces the card's, its loss sample and event are
    /// appended. Call it on the UI thread.
    /// </summary>
    /// <param name="progress">The update.</param>
    public void Apply(TrainingRunProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.Step is { } step)
        {
            Step = step;
        }
        if (progress.LearningRate is { } rate)
        {
            LearningRate = rate;
        }
        if (progress.Elapsed is { } elapsed)
        {
            Elapsed = elapsed;
        }
        if (progress.Eta is { } eta)
        {
            Eta = eta;
        }
        if (progress.Activity is { } activity)
        {
            Activity = activity;
        }
        if (progress.ErrorText is { } error)
        {
            ErrorText = error;
        }
        if (progress.Loss is { } loss)
        {
            _loss.Add(loss);
        }
        if (progress.Event is { } item)
        {
            _events.Add(item);
        }
        if (progress.Status is { } status)
        {
            Status = status;
        }
    }

    /// <summary>
    /// Follows a job's updates as they arrive (for example from a server stream) and applies each one on the UI thread,
    /// until the source ends or <paramref name="cancellation"/> is cancelled.
    /// </summary>
    /// <param name="updates">The job's updates, in order.</param>
    /// <param name="cancellation">Stops following.</param>
    public async Task FollowAsync(IAsyncEnumerable<TrainingRunProgress> updates, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        await foreach (var update in updates.WithCancellation(cancellation))
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                Apply(update);
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() => Apply(update));
            }
        }
    }

    /// <summary>True while the simulation drives the card (no <see cref="Status"/>, a <see cref="Simulation"/>).</summary>
    internal bool IsSimulated => Status is null && Simulation is not null;

    /// <summary>The simulation's player (created on first use while simulating).</summary>
    internal TrainingRunPlayer Player => _player ??= new TrainingRunPlayer(Scenario, Simulation);

    /// <summary>The clock of the Confirm cancel window (tests pass a manual one).</summary>
    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    internal TrainingRunStatus EffectiveStatus => Status ?? (IsSimulated ? Player.Status : TrainingRunStatus.Queued);

    internal int EffectiveStep => IsSimulated ? Player.Step : Step;

    internal int EffectiveTotalSteps => IsSimulated ? TrainingRunPlayer.TotalSteps : TotalSteps;

    internal int EffectiveEpochs => IsSimulated ? TrainingRunPlayer.Epochs : Epochs;

    internal IReadOnlyList<TrainingRunLossPoint> EffectiveLoss => IsSimulated ? Player.Loss : _loss;

    internal IReadOnlyList<TrainingRunEvent> EffectiveEvents => IsSimulated ? Player.Events : _events;

    internal double? EffectiveLearningRate => IsSimulated ? Player.LearningRate : LearningRate;

    internal TimeSpan? EffectiveElapsed => IsSimulated ? Player.Elapsed : Elapsed;

    internal TimeSpan? EffectiveEta => IsSimulated ? Player.Eta : Eta;

    internal string? EffectiveActivity => IsSimulated ? Player.Activity : Activity;

    internal string? EffectiveError => IsSimulated ? Player.ErrorText : ErrorText;

    internal string DisplayName => JobName ?? (IsSimulated ? Simulation!.JobName : null) ?? Labels.DefaultName;

    /// <summary>The base model: yours, else the simulation's, else an unnamed one with the sparkle.</summary>
    internal AgentModel DisplayModel => BaseModel ?? (IsSimulated ? Simulation!.BaseModel : null) ?? s_noModel;

    internal IReadOnlyList<string> DisplayDetails => Details ?? (IsSimulated ? Simulation!.Details : null) ?? [];

    internal TrainingRunDataset? DisplayDataset => Dataset ?? (IsSimulated ? Simulation!.Dataset : null);

    private static readonly AgentModel s_noModel = new(string.Empty, ArcIcons.Sparkles);

    /// <summary>True while running with no loss samples yet ("Starting").</summary>
    internal bool IsStartingUp => EffectiveStatus == TrainingRunStatus.Running && EffectiveLoss.Count == 0;

    /// <summary>The status word the head shows.</summary>
    internal string StatusText => EffectiveStatus switch
    {
        TrainingRunStatus.Queued => Labels.Queued,
        TrainingRunStatus.Running => IsStartingUp ? Labels.Starting : Labels.Training,
        TrainingRunStatus.Paused => Labels.Paused,
        TrainingRunStatus.Completed => Labels.Completed,
        TrainingRunStatus.Failed => Labels.Failed,
        _ => Labels.Cancelled,
    };

    /// <summary>A version of the loss samples on screen: it changes whenever they do.</summary>
    private int LossVersion => IsSimulated ? Player.LossVersion : _lossVersion;

    private int EventsVersion => IsSimulated ? Player.EventsVersion : _eventsVersion;

    /// <summary>Moves the simulation on by <paramref name="seconds"/> (the loop does this per frame; tests call it).</summary>
    internal void AdvanceSimulation(double seconds)
    {
        if (!IsSimulated)
        {
            return;
        }
        // Long steps are split so every sample and event in between is produced.
        while (seconds > 0)
        {
            var slice = Math.Min(0.25, seconds);
            seconds -= slice;
            Player.Advance(slice);
        }
        Refresh();
        UpdateSimulationLoop();
    }

    private void OnLossChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _lossVersion++;
        if (!IsSimulated)
        {
            Refresh();
        }
    }

    private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _eventsVersion++;
        if (!IsSimulated)
        {
            Refresh();
        }
    }

    /// <summary>The simulation runs on a Core LiveLoop at every motion level (it is content), paused off screen.</summary>
    private void UpdateSimulationLoop()
    {
        if (IsSimulated && Player.IsMoving)
        {
            _simulationLoop ??= new LiveLoop(this, dt =>
            {
                if (!IsSimulated)
                {
                    return false;
                }
                if (Player.Advance(dt))
                {
                    Refresh();
                }
                if (!Player.IsMoving)
                {
                    Refresh();
                    return false;
                }
                return true;
            })
            { MinimumLevel = ArcMotionLevel.Off };
            _simulationLoop.Start();
        }
        else
        {
            _simulationLoop?.Stop();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == StatusProperty)
        {
            var old = change.GetOldValue<TrainingRunStatus?>();
            if (old is null && Status is not null || Status is null && old is not null)
            {
                ResetDeploy();
            }
            UpdateSimulationLoop();
            Refresh();
        }
        else if (p == ScenarioProperty)
        {
            if (_player is not null)
            {
                _player.Play(Scenario);
                ResetDeploy();
            }
            UpdateSimulationLoop();
            Refresh();
        }
        else if (p == SimulationProperty)
        {
            // Another job (or none): the player starts over with its data, or stops.
            _player = null;
            ResetDeploy();
            UpdateSimulationLoop();
            Refresh();
        }
        else if (p == NoticeProperty)
        {
            if (!string.IsNullOrEmpty(Notice))
            {
                _announcer?.Announce(Notice);
            }
        }
        else if (p == StepProperty || p == TotalStepsProperty || p == EpochsProperty || p == LearningRateProperty
                 || p == ElapsedProperty || p == EtaProperty || p == MetricsProperty || p == ActivityProperty
                 || p == ErrorTextProperty || p == ActionsProperty || p == DeployHandlerProperty || p == LabelsProperty
                 || p == JobNameProperty || p == BaseModelProperty || p == DetailsProperty || p == DatasetProperty)
        {
            Refresh();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSimulationLoop();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A deploy in flight is aborted when the card leaves.
        ResetDeploy();
        EndConfirmCancel();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width > 0 && e.NewSize.Width < NarrowBreakpoint;
        if (PseudoClasses.Contains(NarrowPseudoClass) != narrow)
        {
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            Refresh();
        }
    }
}
