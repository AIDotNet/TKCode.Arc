using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// An animated "AI is building your app" visual (Arc Building blocks, design reference). Rounded blocks for a nav bar, a
/// hero card, a chart, an avatar row, a settings row and a button fly in and snap into a tiny app layout with no bounce,
/// land as tinted placeholders, fill with real-looking content and pick up the tint in a styling pass, while the status
/// line steps through Laying out, Adding components, Styling and Ready.
/// <para>
/// Map your request to <see cref="Status"/>: <see cref="BuildingBlocksStatus.Building"/> while it runs,
/// <see cref="BuildingBlocksStatus.Done"/> when the result arrives (the rest fast forwards), <see cref="BuildingBlocksStatus.Idle"/>
/// before the first prompt. Leave <see cref="Status"/> unset to play a simulated build. The frame grows to
/// <see cref="Size"/> (or <see cref="StageWidth"/>) and never past its container; everything inside scales with the width.
/// </para>
/// </summary>
[TemplatePart(StagePart, typeof(BuildingBlocksStage))]
[TemplatePart(MarkPart, typeof(BuildingBlocksMark))]
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[TemplatePart(ReplayHostPart, typeof(PresenceHost))]
[TemplatePart(ReplayPart, typeof(AvaloniaButton))]
[TemplatePart(ReplayTextPart, typeof(TextBlock))]
[PseudoClasses(ReadyPseudoClass, NarrowPseudoClass)]
public class BuildingBlocks : TemplatedControl
{
    internal const string StagePart = "PART_Stage";
    internal const string MarkPart = "PART_Mark";
    internal const string LabelPart = "PART_Label";
    internal const string ReplayHostPart = "PART_ReplayHost";
    internal const string ReplayPart = "PART_Replay";
    internal const string ReplayTextPart = "PART_ReplayText";

    internal const string ReadyPseudoClass = ":ready";
    internal const string NarrowPseudoClass = ":narrow";

    /// <summary>
    /// What the app says about its generation: idle clears the frame to dashed slots, building runs the phases (and
    /// re-assembles while <see cref="IsLooping"/>), done fast forwards and settles on Ready. Null plays the built-in
    /// simulated build.
    /// </summary>
    public static readonly StyledProperty<BuildingBlocksStatus?> StatusProperty =
        AvaloniaProperty.Register<BuildingBlocks, BuildingBlocksStatus?>(nameof(Status));

    /// <summary>One build, from the first block to the end of styling, split evenly over the three phases; never under 2.4 seconds.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<BuildingBlocks, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(BuildingBlocksSchedule.DefaultDuration));

    /// <summary>
    /// Without a status: replay the build after a short hold on Ready (off plays once and shows Replay). Building:
    /// re-assemble until the status turns done (off holds on Styling). Reduced motion always plays once.
    /// </summary>
    public static readonly StyledProperty<bool> IsLoopingProperty =
        AvaloniaProperty.Register<BuildingBlocks, bool>(nameof(IsLooping), true);

    /// <summary>The width the frame grows to: Small 280, Medium 400, Large 560 (never past the container).</summary>
    public static readonly StyledProperty<BuildingBlocksSize> SizeProperty =
        AvaloniaProperty.Register<BuildingBlocks, BuildingBlocksSize>(nameof(Size), BuildingBlocksSize.Medium);

    /// <summary>A width in pixels that takes the place of <see cref="Size"/>; NaN (the default) uses <see cref="Size"/>.</summary>
    public static readonly StyledProperty<double> StageWidthProperty =
        AvaloniaProperty.Register<BuildingBlocks, double>(nameof(StageWidth), double.NaN);

    /// <summary>The tint of the placeholders, the button, the chart, the switch and the status mark: the accent or the foreground.</summary>
    public static readonly StyledProperty<BuildingBlocksTone> ToneProperty =
        AvaloniaProperty.Register<BuildingBlocks, BuildingBlocksTone>(nameof(Tone));

    /// <summary>Any color, taking the place of <see cref="Tone"/>, for a brand or per-product tint.</summary>
    public static readonly StyledProperty<Color?> TintProperty =
        AvaloniaProperty.Register<BuildingBlocks, Color?>(nameof(Tint));

    /// <summary>Every word the component shows or says.</summary>
    public static readonly StyledProperty<BuildingBlocksLabels> LabelsProperty =
        AvaloniaProperty.Register<BuildingBlocks, BuildingBlocksLabels>(nameof(Labels), BuildingBlocksLabels.Default);

    /// <summary>Shows the status line under the frame: the mark, the phase label and Replay. Turn it off when the surrounding UI already says what happens.</summary>
    public static readonly StyledProperty<bool> IsLabelVisibleProperty =
        AvaloniaProperty.Register<BuildingBlocks, bool>(nameof(IsLabelVisible), true);

    /// <summary>Shows Replay on Ready; it raises <see cref="ReplayRequested"/>. Start the generation again and set <see cref="Status"/> back to building.</summary>
    public static readonly StyledProperty<bool> AllowsReplayProperty =
        AvaloniaProperty.Register<BuildingBlocks, bool>(nameof(AllowsReplay));

    /// <summary>The phase the status line shows.</summary>
    public static readonly DirectProperty<BuildingBlocks, BuildingBlocksPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<BuildingBlocks, BuildingBlocksPhase>(nameof(Phase), o => o.Phase);

    /// <summary>Raised when the visible phase changes, for analytics or to keep other UI in step. Bubbles.</summary>
    public static readonly RoutedEvent<BuildingBlocksPhaseChangedEventArgs> PhaseChangedEvent =
        RoutedEvent.Register<BuildingBlocks, BuildingBlocksPhaseChangedEventArgs>(nameof(PhaseChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Replay. Bubbles. Without a status the simulated build also starts again.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ReplayRequestedEvent =
        RoutedEvent.Register<BuildingBlocks, RoutedEventArgs>(nameof(ReplayRequested), RoutingStrategies.Bubble);

    private readonly BuildingBlocksSchedule _schedule = new();
    private readonly LiveLoop _clock;
    private BuildingBlocksPhase _phase = BuildingBlocksPhase.Idle;
    private BuildingBlocksStage? _stage;
    private BuildingBlocksMark? _mark;
    private ArcContentSwap? _label;
    private PresenceHost? _replayHost;
    private AvaloniaButton? _replay;
    private TextBlock? _replayText;
    private string? _shownLabel;

    static BuildingBlocks()
    {
        AffectsMeasure<BuildingBlocks>(SizeProperty, StageWidthProperty);
    }

    /// <summary>Creates the component.</summary>
    public BuildingBlocks()
    {
        // One clock for the schedule, the blocks' transitions and the status mark. It runs at every motion level (the phases
        // are information) but only while the frame is attached, visible and on screen, so a scrolled-away or hidden build
        // pauses where it is and resumes from there.
        _clock = new LiveLoop(this, OnFrame) { MinimumLevel = ArcMotionLevel.Off };
        _schedule.Duration = BuildingBlocksSchedule.DefaultDuration;
    }

    /// <inheritdoc cref="StatusProperty"/>
    public BuildingBlocksStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="IsLoopingProperty"/>
    public bool IsLooping { get => GetValue(IsLoopingProperty); set => SetValue(IsLoopingProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public BuildingBlocksSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="StageWidthProperty"/>
    public double StageWidth { get => GetValue(StageWidthProperty); set => SetValue(StageWidthProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public BuildingBlocksTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="TintProperty"/>
    public Color? Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public BuildingBlocksLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsLabelVisibleProperty"/>
    public bool IsLabelVisible { get => GetValue(IsLabelVisibleProperty); set => SetValue(IsLabelVisibleProperty, value); }

    /// <inheritdoc cref="AllowsReplayProperty"/>
    public bool AllowsReplay { get => GetValue(AllowsReplayProperty); set => SetValue(AllowsReplayProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public BuildingBlocksPhase Phase => _phase;

    /// <inheritdoc cref="PhaseChangedEvent"/>
    public event EventHandler<BuildingBlocksPhaseChangedEventArgs>? PhaseChanged
    {
        add => AddHandler(PhaseChangedEvent, value);
        remove => RemoveHandler(PhaseChangedEvent, value);
    }

    /// <inheritdoc cref="ReplayRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ReplayRequested
    {
        add => AddHandler(ReplayRequestedEvent, value);
        remove => RemoveHandler(ReplayRequestedEvent, value);
    }

    /// <summary>
    /// Builds again from the first block: the simulated build, or an app build while <see cref="Status"/> is building.
    /// Blocks still on screen fade out first. Idle and done are the app's to change.
    /// </summary>
    public void Replay()
    {
        if (Status is not null and not BuildingBlocksStatus.Building)
        {
            return;
        }
        _schedule.Restart(_stage?.HasVisibleBlocks ?? false);
        ApplyStep();
    }

    internal BuildingBlocksSchedule Schedule => _schedule;

    internal BuildingBlocksStage? Stage => _stage;

    internal BuildingBlocksMark? Mark => _mark;

    /// <summary>The label the status line shows.</summary>
    internal string DisplayedLabel => Labels.Of(_phase);

    /// <summary>Replay shows: on Ready, when the app asked for it or the simulated build does not loop.</summary>
    internal bool ShowsReplay => _phase == BuildingBlocksPhase.Ready && (AllowsReplay || (Status is null && !_schedule.Loops));

    /// <summary>The width the frame grows to before the container limits it.</summary>
    internal double TargetWidth
    {
        get
        {
            var width = StageWidth;
            if (double.IsFinite(width) && width > 0)
            {
                return width;
            }
            return _stage?.WidthFor(Size) ?? Size switch
            {
                BuildingBlocksSize.Small => 280,
                BuildingBlocksSize.Large => 560,
                _ => 400
            };
        }
    }

    /// <summary>Moves the clock on by <paramref name="seconds"/> without frames (tests).</summary>
    internal void Advance(double seconds) => Tick(seconds);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _stage = e.NameScope.Find<BuildingBlocksStage>(StagePart);
        _mark = e.NameScope.Find<BuildingBlocksMark>(MarkPart);
        _label = e.NameScope.Find<ArcContentSwap>(LabelPart);
        _replayHost = e.NameScope.Find<PresenceHost>(ReplayHostPart);
        _replay = e.NameScope.Find<AvaloniaButton>(ReplayPart);
        _replayText = e.NameScope.Find<TextBlock>(ReplayTextPart);
        if (_replay is not null)
        {
            _replay.Click += OnReplayClick;
        }
        if (_stage is not null)
        {
            _stage.PropertyChanged += OnStagePropertyChanged;
        }
        if (_label is not null)
        {
            AutomationProperties.SetLiveSetting(_label, AutomationLiveSetting.Polite);
        }
        _shownLabel = null;
        UpdateLooping();
        StartFromStatus(initial: true);
    }

    private void Unhook()
    {
        if (_replay is not null)
        {
            _replay.Click -= OnReplayClick;
        }
        if (_stage is not null)
        {
            _stage.PropertyChanged -= OnStagePropertyChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == StatusProperty)
        {
            StartFromStatus(initial: false);
        }
        else if (p == DurationProperty)
        {
            _schedule.Duration = Duration.TotalSeconds;
            ApplyStep();
        }
        else if (p == IsLoopingProperty || p == ArcMotion.LevelProperty)
        {
            if (p == ArcMotion.LevelProperty && !ArcMotion.IsFull(this))
            {
                _stage?.Settle();
            }
            UpdateLooping();
            ApplyStep();
        }
        else if (p == LabelsProperty || p == AllowsReplayProperty || p == IsLabelVisibleProperty)
        {
            UpdateStatusLine();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLooping();
        UpdateClock();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // The frame grows to its size and never past the container; its width never follows its content.
        var target = TargetWidth;
        var width = double.IsFinite(availableSize.Width) ? Math.Min(availableSize.Width, target) : target;
        var desired = base.MeasureOverride(availableSize.WithWidth(width));
        return new Size(width, desired.Height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Min(finalSize.Width, TargetWidth);
        base.ArrangeOverride(finalSize.WithWidth(width));
        PseudoClasses.Set(NarrowPseudoClass, width < (_stage?.NarrowWidth ?? 320));
        return finalSize;
    }

    private void OnStagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BuildingBlocksStage.WidthSMProperty || e.Property == BuildingBlocksStage.WidthMDProperty
            || e.Property == BuildingBlocksStage.WidthLGProperty || e.Property == BuildingBlocksStage.NarrowWidthProperty)
        {
            InvalidateMeasure();
        }
    }

    private void OnReplayClick(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(ReplayRequestedEvent));
        if (Status is null)
        {
            Replay();
        }
    }

    /// <summary>Hands the schedule to the app's status, or to the simulated build when there is none.</summary>
    private void StartFromStatus(bool initial)
    {
        var mode = Status switch
        {
            null => BuildingBlocksMode.Sample,
            BuildingBlocksStatus.Idle => BuildingBlocksMode.Idle,
            BuildingBlocksStatus.Building => BuildingBlocksMode.Building,
            _ => BuildingBlocksMode.Done
        };
        if (!initial && mode == _schedule.Mode && mode == BuildingBlocksMode.Building)
        {
            return;
        }
        _schedule.Duration = Duration.TotalSeconds;
        _schedule.SetMode(mode, !initial && (_stage?.HasVisibleBlocks ?? false));
        ApplyStep();
        if (initial && mode is BuildingBlocksMode.Idle or BuildingBlocksMode.Done)
        {
            // A frame that opens idle or done shows its state at once.
            _stage?.Settle();
        }
    }

    private void UpdateLooping() =>
        _schedule.Loops = IsLooping && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    private bool OnFrame(double seconds)
    {
        Tick(seconds);
        return NeedsFrames;
    }

    private bool NeedsFrames =>
        _schedule.IsRunning || (_stage?.IsMoving ?? false) || (_mark is { } mark && (mark.IsLightRunning || mark.IsMoving));

    private void Tick(double seconds)
    {
        _stage?.Advance(seconds);
        _mark?.Advance(seconds);
        if (_schedule.Advance(seconds))
        {
            ApplyStep();
        }
    }

    /// <summary>Brings the stage and the status line in line with the schedule.</summary>
    private void ApplyStep()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        _stage?.Show(_schedule.Step, level);
        var phase = _schedule.Phase;
        if (phase != _phase)
        {
            var old = _phase;
            SetAndRaise(PhaseProperty, ref _phase, phase);
            PseudoClasses.Set(ReadyPseudoClass, phase == BuildingBlocksPhase.Ready);
            UpdateStatusLine();
            RaiseEvent(new BuildingBlocksPhaseChangedEventArgs(PhaseChangedEvent, old, phase));
        }
        else
        {
            UpdateStatusLine();
        }
        UpdateClock();
    }

    private void UpdateClock()
    {
        if (NeedsFrames)
        {
            _clock.Start();
        }
        else
        {
            _clock.Stop();
        }
    }

    private void UpdateStatusLine()
    {
        var labels = Labels;
        var text = labels.Of(_phase);
        if (_label is not null && _shownLabel != text)
        {
            _shownLabel = text;
            _label.Content = text;
            AutomationProperties.SetName(_label, text);
        }
        if (_label is not null)
        {
            // A looping sample is not announced on every loop.
            AutomationProperties.SetLiveSetting(_label,
                Status is null && _schedule.Loops ? AutomationLiveSetting.Off : AutomationLiveSetting.Polite);
        }
        AutomationProperties.SetHelpText(this, Status is null ? labels.Simulated : null);
        _mark?.Show(_phase, ArcMotion.GetEffectiveLevel(this));
        if (_replayText is not null)
        {
            _replayText.Text = labels.Replay;
        }
        if (_replay is not null)
        {
            AutomationProperties.SetName(_replay, labels.Replay);
        }
        if (_replayHost is not null)
        {
            _replayHost.IsPresent = ShowsReplay;
        }
    }
}
