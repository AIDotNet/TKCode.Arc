using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An animated explainer of the agent loop: a bright head with a gradient trail
/// circles a ring past three stations (Gather context, Take action, Verify) around the model. At Gather a context block
/// joins the stack beside the ring and the token total counts up; at Take action one to three tool calls drop out of the
/// ring on a pin and resolve; at Verify a check passes or sends the loop round again. The run ends done (the ring draws
/// in, a check at the center, the summary as the caption) or stopped when the last check fails. Play, pause, step,
/// restart and a 0.5× / 1× / 2× speed control drive it; under reduced motion it opens on its end. The run is sample data:
/// nothing calls a real service.
/// </summary>
[TemplatePart(StagePart, typeof(AgentLoopStage))]
[TemplatePart(StatusPart, typeof(ExplainerStatus))]
[TemplatePart(CaptionPart, typeof(ArcContentSwap))]
[TemplatePart(SpeedPart, typeof(SegmentedControl))]
[TemplatePart(SpeedLabelPart, typeof(TextBlock))]
[TemplatePart(ControlsPart, typeof(Control))]
[PseudoClasses(StoppedPseudoClass, EmptyPseudoClass)]
public class AgentLoop : Explainer
{
    /// <summary>The ring, its labels, lanes and the context stack.</summary>
    public const string StagePart = "PART_Stage";

    /// <summary>The status line in the header.</summary>
    public const string StatusPart = "PART_Status";

    /// <summary>The caption under the title (task, then summary).</summary>
    public const string CaptionPart = "PART_Caption";

    /// <summary>The speed control.</summary>
    public const string SpeedPart = "PART_Speed";

    /// <summary>The "Speed" words before the speed control.</summary>
    public const string SpeedLabelPart = "PART_SpeedLabel";

    /// <summary>The row of playback controls (turned off while there is no run).</summary>
    public const string ControlsPart = "PART_Controls";

    /// <summary>Set once the run ended with a failed check.</summary>
    public const string StoppedPseudoClass = ":stopped";

    /// <summary>Set while there are no iterations (the ring rests, the controls are off).</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Defines the <see cref="IterationsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<AgentLoopIteration>?> IterationsSourceProperty =
        AvaloniaProperty.Register<AgentLoop, IEnumerable<AgentLoopIteration>?>(nameof(IterationsSource));

    /// <summary>Defines the <see cref="Task"/> property.</summary>
    public static readonly StyledProperty<string?> TaskProperty =
        AvaloniaProperty.Register<AgentLoop, string?>(nameof(Task));

    /// <summary>Defines the <see cref="Summary"/> property.</summary>
    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<AgentLoop, string?>(nameof(Summary));

    /// <summary>Defines the <see cref="Labels"/> property.</summary>
    public static readonly StyledProperty<AgentLoopLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentLoop, AgentLoopLabels>(nameof(Labels), new AgentLoopLabels());

    /// <summary>Defines the <see cref="Phase"/> property.</summary>
    public static readonly DirectProperty<AgentLoop, AgentLoopPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<AgentLoop, AgentLoopPhase>(nameof(Phase), o => o.Phase);

    /// <summary>Defines the <see cref="StationChanged"/> routed event.</summary>
    public static readonly RoutedEvent<AgentLoopStationChangedEventArgs> StationChangedEvent =
        RoutedEvent.Register<AgentLoop, AgentLoopStationChangedEventArgs>(nameof(StationChanged), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<Thickness> BodyPaddingProperty =
        AvaloniaProperty.Register<AgentLoop, Thickness>(nameof(BodyPadding), new Thickness(24, 24, 24, 0));

    internal static readonly StyledProperty<Thickness> FooterPaddingProperty =
        AvaloniaProperty.Register<AgentLoop, Thickness>(nameof(FooterPadding), new Thickness(24, 16, 24, 24));

    private static readonly ExplainerModel s_sampleModel = AgentLoopDefaults.CreateModel();
    private static readonly IReadOnlyList<AgentLoopIteration> s_sampleRun = AgentLoopDefaults.CreateIterations();
    private static readonly SegmentedOption[] s_speeds =
    [
        new SegmentedOption(0.5, "0.5×"),
        new SegmentedOption(1.0, "1×"),
        new SegmentedOption(2.0, "2×")
    ];

    private readonly AgentLoopScene _scene = new();
    private IReadOnlyList<AgentLoopIteration> _run = Array.Empty<AgentLoopIteration>();
    private bool _runRead;
    private AgentLoopTimeline _timeline = AgentLoopTimeline.Build(Array.Empty<AgentLoopShape>());
    private bool _isSample;
    private AgentLoopStage? _stage;
    private ExplainerStatus? _status;
    private ArcContentSwap? _caption;
    private string? _captionText;
    private SegmentedControl? _speed;
    private TextBlock? _speedLabel;
    private Control? _controls;
    private INotifyCollectionChanged? _observedSource;
    private AgentLoopPhase _phase;
    private string? _sentence;
    private bool _rebuildPending;
    private bool _syncingSpeed;
    private bool _pauseCheckPending;

    static AgentLoop()
    {
        TitleProperty.OverrideDefaultValue<AgentLoop>("Agent loop");
        ModelProperty.OverrideDefaultValue<AgentLoop>(s_sampleModel);
        IsPlayingProperty.OverrideMetadata<AgentLoop>(new StyledPropertyMetadata<bool>(coerce: CoerceIsPlaying));
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<AgentLoop>(AutomationControlType.Group);
    }

    /// <summary>Creates an agent loop that plays the sample run until it is given iterations.</summary>
    public AgentLoop()
    {
        Iterations.CollectionChanged += OnIterationsChanged;
        AutomationProperties.SetName(this, Title);
        UpdatePseudoClasses();
    }

    /// <summary>
    /// The loops, in order (the XAML content). Two to four read best; each reserves a slot in the context stack. While
    /// it is empty and <see cref="IterationsSource"/> is null, the sample run plays.
    /// </summary>
    [Content]
    public AvaloniaList<AgentLoopIteration> Iterations { get; } = new();

    /// <summary>
    /// The loops from a view model; when set it replaces <see cref="Iterations"/>. An empty collection shows the ring at
    /// rest with the controls off.
    /// </summary>
    public IEnumerable<AgentLoopIteration>? IterationsSource
    {
        get => GetValue(IterationsSourceProperty);
        set => SetValue(IterationsSourceProperty, value);
    }

    /// <summary>The caption while the run is ready or running (one line reads best); null shows the sample task for the sample run.</summary>
    public string? Task { get => GetValue(TaskProperty); set => SetValue(TaskProperty, value); }

    /// <summary>
    /// The caption once the run ends (up to two lines); null shows a count of loops and tool calls, or a stopped note
    /// when the last check failed (the sample run has its own summary).
    /// </summary>
    public string? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <summary>Every word the card shows or announces (station names, status, lanes, context, announcements).</summary>
    public AgentLoopLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>Where the run stands: ready, running, paused, done or stopped.</summary>
    public AgentLoopPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <summary>Raised when the head reaches a station, with the station and the zero-based loop (once per beat).</summary>
    public event EventHandler<AgentLoopStationChangedEventArgs>? StationChanged
    {
        add => AddHandler(StationChangedEvent, value);
        remove => RemoveHandler(StationChangedEvent, value);
    }

    internal Thickness BodyPadding { get => GetValue(BodyPaddingProperty); set => SetValue(BodyPaddingProperty, value); }

    internal Thickness FooterPadding { get => GetValue(FooterPaddingProperty); set => SetValue(FooterPaddingProperty, value); }

    /// <summary>What the story has shown so far (tests read it).</summary>
    internal AgentLoopScene Scene => _scene;

    /// <summary>The timeline of the current run (tests read it).</summary>
    internal AgentLoopTimeline Timeline => _timeline;

    /// <summary>The loops being played (the sample run when none are given).</summary>
    internal IReadOnlyList<AgentLoopIteration> Run => _run;

    /// <summary>The story time in seconds (tests read it).</summary>
    internal double StoryTime => Now;

    /// <summary>Seconds of frames, for the view's one-shot transitions.</summary>
    internal double Wall => WallTime;

    /// <summary>True when changes animate (full motion, on screen); otherwise they apply in place.</summary>
    internal bool Animates => IsTravelEnabled && _stage is not null && _stage.IsLoaded;

    /// <summary>The caption text shown now.</summary>
    internal string? CaptionText => _captionText;

    /// <summary>The words the card shows for the run's end (the summary).</summary>
    internal string SummaryText
    {
        get
        {
            if (Summary is { } summary)
            {
                return summary;
            }
            if (_isSample)
            {
                return AgentLoopDefaults.Summary;
            }
            var calls = 0;
            foreach (var iteration in _run)
            {
                calls += Math.Min(iteration.Calls.Count, AgentLoopTimeline.MaxCalls);
            }
            return _timeline.IsStopped ? Labels.StoppedSummary(_run.Count, calls) : Labels.DoneSummary(_run.Count, calls);
        }
    }

    /// <summary>The caption while the run is ready or running.</summary>
    internal string TaskText => Task ?? (_isSample ? AgentLoopDefaults.Task : string.Empty);

    private protected override double StepSpeed => 2.5 * Math.Max(1, Speed);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_speed is not null)
        {
            _speed.SelectionChanged -= OnSpeedSelectionChanged;
        }
        base.OnApplyTemplate(e);
        _stage = e.NameScope.Find<AgentLoopStage>(StagePart);
        _status = e.NameScope.Find<ExplainerStatus>(StatusPart);
        _caption = e.NameScope.Find<ArcContentSwap>(CaptionPart);
        _speed = e.NameScope.Find<SegmentedControl>(SpeedPart);
        _speedLabel = e.NameScope.Find<TextBlock>(SpeedLabelPart);
        _controls = e.NameScope.Find<Control>(ControlsPart);
        _captionText = null;
        if (_speed is not null)
        {
            _speed.ItemsSource = s_speeds;
            _speed.SelectionChanged += OnSpeedSelectionChanged;
        }
        _stage?.Attach(this);
        UpdateSpeedControl();
        UpdateLabels();
        UpdatePhase();
        UpdateCaption();
        UpdateControlsEnabled();
        UpdateSizing(Bounds.Width);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IterationsSourceProperty)
        {
            ObserveSource(IsAttached ? change.GetNewValue<IEnumerable<AgentLoopIteration>?>() : null);
            ScheduleRebuild();
        }
        else if (change.Property == TaskProperty || change.Property == SummaryProperty)
        {
            UpdateCaption();
        }
        else if (change.Property == LabelsProperty)
        {
            UpdateLabels();
            UpdatePhase(force: true);
            UpdateCaption();
        }
        else if (change.Property == ModelProperty)
        {
            _stage?.UpdateModel();
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
        else if (change.Property == SpeedProperty)
        {
            UpdateSpeedControl();
        }
        else if (change.Property == IsSteppingProperty || change.Property == IsFinishedProperty)
        {
            UpdatePhase();
        }
        else if (change.Property == AreControlsVisibleProperty)
        {
            UpdateSizing(Bounds.Width);
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateSizing(change.GetNewValue<Rect>().Width);
        }
    }

    private protected override void BuildStory(ExplainerScript script)
    {
        _run = SnapshotRun(out _isSample);
        _runRead = true;
        var shapes = new AgentLoopShape[_run.Count];
        for (var i = 0; i < _run.Count; i++)
        {
            shapes[i] = new AgentLoopShape(_run[i].Calls.Count, _run[i].Verify.Passed);
        }
        _timeline = AgentLoopTimeline.Build(shapes);
        _stage?.SetRun(_run, _timeline);
        UpdatePseudoClasses();
        UpdateControlsEnabled();
        CoerceValue(IsPlayingProperty);
        for (var i = 0; i < _timeline.Count; i++)
        {
            var loop = i;
            var times = _timeline.Loops[i];
            script.At(times.GatherAt, () => ArriveAtGather(loop));
            script.At(times.BlockAt, () => AddBlock(loop));
            script.At(times.GatherStop, AnnounceSentence, isStop: true);
            script.At(times.LeaveGather, () => Leave(AgentLoopStation.Gather));
            script.At(times.ActAt, () => Arrive(AgentLoopStation.Act, loop));
            for (var k = 0; k < times.CallIn.Length; k++)
            {
                var call = k;
                script.At(times.CallIn[k], () => DropCall(loop, call));
                script.At(times.Result[k], () => ResolveCall(loop, call));
                script.At(times.ResultStop(k), AnnounceSentence, isStop: true);
            }
            script.At(times.LeaveAct, () => Leave(AgentLoopStation.Act));
            script.At(times.VerifyAt, () => Arrive(AgentLoopStation.Verify, loop));
            script.At(times.CheckIn, () => DropCheck(loop));
            script.At(times.VerifyStop, AnnounceSentence, isStop: true);
            script.At(times.LeaveVerify, () => Leave(AgentLoopStation.Verify));
        }
        if (_timeline.Count > 0)
        {
            script.At(_timeline.End, Finish, isStop: true);
            script.Length = _timeline.End;
        }
    }

    private protected override void ResetScene()
    {
        var hadContent = _scene.HasStarted || _scene.Ending != AgentLoopEnding.None;
        _scene.Reset();
        _sentence = null;
        _stage?.ResetScene(Animates && hadContent);
        UpdatePseudoClasses();
        UpdatePhase();
        UpdateCaption();
        if (hadContent)
        {
            RequestFrames(0.6);
        }
    }

    private protected override void OnTimeChanged()
    {
        _stage?.Tick(Now, WallTime);
        UpdatePhase();
    }

    private protected override void OnFinished()
    {
        UpdatePhase();
        if (_timeline.Count > 0)
        {
            Announce(SummaryText);
        }
    }

    private protected override void OnPlaybackChanged()
    {
        UpdatePhase();
        if (!IsPlaying && !_pauseCheckPending)
        {
            // A step also turns playing off before it starts stepping: wait for the dust to settle, then speak only
            // for a real pause (the reader asked where the run is).
            _pauseCheckPending = true;
            Dispatcher.UIThread.Post(AnnouncePause, DispatcherPriority.Background);
        }
    }

    private void AnnouncePause()
    {
        _pauseCheckPending = false;
        if (!IsPlaying && !IsStepping && !IsFinished && _scene.HasStarted && IsReaderDriven)
        {
            Announce(_sentence);
        }
    }

    private static bool CoerceIsPlaying(AvaloniaObject owner, bool value)
        => value && owner is AgentLoop loop && loop.IsEmptyRun() ? false : value;

    private bool IsEmptyRun()
    {
        if (IterationsSource is { } source)
        {
            return !source.Any();
        }
        return false;
    }

    private IReadOnlyList<AgentLoopIteration> SnapshotRun(out bool isSample)
    {
        isSample = false;
        if (IterationsSource is { } source)
        {
            return source.ToArray();
        }
        if (Iterations.Count > 0)
        {
            return Iterations.ToArray();
        }
        isSample = true;
        return s_sampleRun;
    }

    // ---- beats --------------------------------------------------------------------------------------------------

    private void ArriveAtGather(int loop)
    {
        _scene.Loop = loop;
        _scene[AgentLoopStation.Gather] = AgentLoopNodeState.Active;
        if (loop > 0)
        {
            _scene[AgentLoopStation.Act] = AgentLoopNodeState.Idle;
            _scene[AgentLoopStation.Verify] = AgentLoopNodeState.Idle;
        }
        _scene.HeadVisible = true;
        _stage?.OnStations(Animates);
        _stage?.OnLoop(Animates);
        _stage?.OnHead(Animates);
        Beat();
        UpdatePhase();
        RaiseEvent(new AgentLoopStationChangedEventArgs(StationChangedEvent, this, AgentLoopStation.Gather, loop));
    }

    private void AddBlock(int loop)
    {
        var context = _run[loop].Gather;
        _scene.Blocks = loop + 1;
        _scene.FreshBlock = loop;
        _scene.Tokens += Math.Max(0, context.Tokens);
        _sentence = Labels.GatheredAnnouncement(loop + 1, context.Label);
        _stage?.OnBlock(loop, Animates);
        Beat();
    }

    private void Arrive(AgentLoopStation station, int loop)
    {
        _scene[station] = AgentLoopNodeState.Active;
        _stage?.OnStations(Animates);
        Beat();
        RaiseEvent(new AgentLoopStationChangedEventArgs(StationChangedEvent, this, station, loop));
    }

    private void Leave(AgentLoopStation station)
    {
        _scene[station] = AgentLoopNodeState.Visited;
        if (station == AgentLoopStation.Gather)
        {
            _scene.FreshBlock = -1;
        }
        _stage?.OnStations(Animates);
        Beat();
    }

    private void DropCall(int loop, int index)
    {
        var call = _run[loop].Calls[index];
        var chip = AgentLoopChipState.ForCall(call, _scene.NextSerial());
        _scene.CallChip = chip;
        _scene.LatestChip = chip;
        _sentence = Labels.CallingAnnouncement(call.Tool, call.Action);
        _stage?.OnChip(chip, Animates);
        Beat();
    }

    private void ResolveCall(int loop, int index)
    {
        var call = _run[loop].Calls[index];
        if (_scene.CallChip is { } chip && ReferenceEquals(chip.Call, call))
        {
            chip.IsResolved = true;
            _stage?.OnResolved(chip, Animates);
        }
        _sentence = call.Status == AgentLoopCallStatus.Error
            ? Labels.FailedAnnouncement(call.Tool, call.Result)
            : Labels.ReturnedAnnouncement(call.Tool, call.Result);
        Beat();
    }

    private void DropCheck(int loop)
    {
        var check = _run[loop].Verify;
        var chip = AgentLoopChipState.ForCheck(check, _scene.NextSerial());
        _scene.CheckChip = chip;
        _scene.LatestChip = chip;
        _sentence = check.Passed ? Labels.PassedAnnouncement(check.Note) : Labels.RetryAnnouncement(check.Note);
        _stage?.OnChip(chip, Animates);
        Beat();
    }

    private void Finish()
    {
        _scene.Ending = _timeline.IsStopped ? AgentLoopEnding.Stopped : AgentLoopEnding.Done;
        _scene.HeadVisible = false;
        _scene.FreshBlock = -1;
        _stage?.OnStations(Animates);
        _stage?.OnHead(Animates);
        _stage?.OnEnding(Animates);
        UpdatePseudoClasses();
        UpdateCaption();
        Beat();
    }

    private void AnnounceSentence() => AnnounceBeat(_sentence);

    /// <summary>After a beat: keep frames coming for its transitions, even while paused.</summary>
    private void Beat()
    {
        if (Animates)
        {
            RequestFrames(1.0);
        }
    }

    // ---- data changes ---------------------------------------------------------------------------------------------

    private void OnIterationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IterationsSource is null)
        {
            ScheduleRebuild();
        }
    }

    private protected override void WatchData(bool watch)
    {
        // A view model's list can outlive the card: it is observed only while the card is attached.
        ObserveSource(watch ? IterationsSource : null);
        if (watch && _runRead && !_run.SequenceEqual(SnapshotRun(out _)))
        {
            // Changed while nothing listened: rebuild as a change seen live would.
            ScheduleRebuild();
        }
    }

    private void ObserveSource(IEnumerable<AgentLoopIteration>? source)
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
        }
        _observedSource = source as INotifyCollectionChanged;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRebuild();

    private void ScheduleRebuild()
    {
        CoerceValue(IsPlayingProperty);
        if (_rebuildPending)
        {
            return;
        }
        // Coalesce: XAML and view models add loops one at a time.
        _rebuildPending = true;
        Dispatcher.UIThread.Post(RebuildNow, DispatcherPriority.Background);
    }

    private void RebuildNow()
    {
        _rebuildPending = false;
        // Before the card is first seen this rebuilds quietly, so autoplay still starts; afterwards it starts over.
        Rebuild();
    }

    // ---- view state -----------------------------------------------------------------------------------------------

    private void UpdatePhase(bool force = false)
    {
        AgentLoopPhase phase;
        if (_scene.Ending == AgentLoopEnding.Done)
        {
            phase = AgentLoopPhase.Done;
        }
        else if (_scene.Ending == AgentLoopEnding.Stopped)
        {
            phase = AgentLoopPhase.Stopped;
        }
        else if (_timeline.Count > 0 && (IsPlaying || IsStepping))
        {
            phase = AgentLoopPhase.Running;
        }
        else if (!_scene.HasStarted)
        {
            phase = AgentLoopPhase.Ready;
        }
        else
        {
            phase = AgentLoopPhase.Paused;
        }
        if (phase == Phase && !force && _status?.Text is not null)
        {
            return;
        }
        Phase = phase;
        if (_status is null)
        {
            return;
        }
        var labels = Labels;
        var (text, icon, tone, busy) = phase switch
        {
            AgentLoopPhase.Running => (labels.Running, (Geometry?)null, ExplainerTone.Accent, true),
            AgentLoopPhase.Paused => (labels.Paused, ArcIcons.Pause, ExplainerTone.Muted, false),
            AgentLoopPhase.Done => (labels.Done, ArcIcons.CircleCheck, ExplainerTone.Success, false),
            AgentLoopPhase.Stopped => (labels.Stopped, AgentLoopIcons.CircleStop, ExplainerTone.Warning, false),
            _ => (labels.Ready, ExplainerIcons.CircleDashed, ExplainerTone.Muted, false)
        };
        _status.Text = text;
        _status.Icon = icon;
        _status.Tone = tone;
        _status.IsBusy = busy;
    }

    private void UpdateCaption()
    {
        var text = _scene.Ending != AgentLoopEnding.None ? SummaryText : TaskText;
        if (_caption is null || text == _captionText)
        {
            _captionText = _caption is null ? null : text;
            return;
        }
        _captionText = text;
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 19.6
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextSecondary);
        if (IsTravelEnabled)
        {
            _caption.Content = block;
            return;
        }
        // Reduced motion: the caption changes in place.
        var mode = _caption.Mode;
        _caption.Mode = ContentSwapMode.None;
        _caption.Content = block;
        _caption.Mode = mode;
    }

    private void UpdateLabels()
    {
        if (_speedLabel is not null)
        {
            _speedLabel.Text = Labels.Speed;
        }
        if (_speed is not null)
        {
            AutomationProperties.SetName(_speed, Labels.Speed);
        }
        _stage?.UpdateLabels();
    }

    private void UpdateSpeedControl()
    {
        if (_speed is null)
        {
            return;
        }
        var index = -1;
        for (var i = 0; i < s_speeds.Length; i++)
        {
            if (s_speeds[i].Value is double value && Math.Abs(value - Speed) < 0.001)
            {
                index = i;
            }
        }
        _syncingSpeed = true;
        try
        {
            _speed.SelectedIndex = index;
        }
        finally
        {
            _syncingSpeed = false;
        }
    }

    private void OnSpeedSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSpeed || _speed?.SelectedItem is not SegmentedOption { Value: double value })
        {
            return;
        }
        SetCurrentValue(SpeedProperty, value);
    }

    private void UpdateControlsEnabled()
    {
        if (_controls is not null)
        {
            _controls.IsEnabled = _timeline.Count > 0;
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(StoppedPseudoClass, _scene.Ending == AgentLoopEnding.Stopped);
        PseudoClasses.Set(EmptyPseudoClass, _timeline.Count == 0);
    }

    private void UpdateSizing(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var padding = AgentLoopGeometry.CardPadding(width);
        var bottom = AreControlsVisible ? 0 : padding;
        var body = new Thickness(padding, padding, padding, bottom);
        if (body != BodyPadding)
        {
            SetCurrentValue(BodyPaddingProperty, body);
        }
        var footer = new Thickness(padding, 16, padding, padding);
        if (footer != FooterPadding)
        {
            SetCurrentValue(FooterPaddingProperty, footer);
        }
        if (_speedLabel is not null)
        {
            // The card's border is 1px each side.
            _speedLabel.IsVisible = AgentLoopGeometry.ShowsSpeedLabel(width - 2 * padding - 2);
        }
    }
}
