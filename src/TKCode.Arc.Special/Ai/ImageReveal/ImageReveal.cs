using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// An image generation canvas (Arc Image reveal, design reference): the way current image models show a picture forming.
/// A soft, heavily blurred preview made from the image's own colors, with fine film grain, sharpens top to bottom behind a
/// soft-edged band while the rest gently comes into focus; a translucent pill shows a progress ring and the step count;
/// queued and failed jobs get a calm surface with one soft shimmer, or an alert, the message and Retry.
/// <para>
/// Drive it with <see cref="Status"/>, <see cref="Progress"/> and <see cref="Source"/> (coarse progress updates are smoothed
/// into one continuous reveal; without progress it estimates from <see cref="Duration"/>), or hand it a
/// <see cref="Generator"/> that reports progress and returns the image. A <see cref="Source"/> alone is shown finished. For
/// demos, a <see cref="Simulation"/> plays a looping, simulated generation over the pictures it is given; with nothing at
/// all the canvas rests on its plain surface. The canvas fills its container up to 560px wide and keeps
/// <see cref="AspectRatio"/>.
/// </para>
/// </summary>
[TemplatePart(CanvasPart, typeof(ImageRevealCanvas))]
[TemplatePart(PillHostPart, typeof(Control))]
[TemplatePart(PillPart, typeof(ImageRevealPill))]
[TemplatePart(SpinnerPart, typeof(ArcSpinner))]
[TemplatePart(RingPart, typeof(ArcRing))]
[TemplatePart(CheckPart, typeof(ArcGlyph))]
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[TemplatePart(CountPart, typeof(TextBlock))]
[TemplatePart(CountReservePart, typeof(TextBlock))]
[TemplatePart(ErrorPart, typeof(Control))]
[TemplatePart(ErrorTextPart, typeof(TextBlock))]
[TemplatePart(RetryPart, typeof(AvaloniaButton))]
[TemplatePart(LivePart, typeof(ExplainerAnnouncer))]
[PseudoClasses(SmallPseudoClass, LargePseudoClass, QueuedPseudoClass, GeneratingPseudoClass, DonePseudoClass, ErrorPseudoClass)]
public class ImageReveal : TemplatedControl
{
    internal const string CanvasPart = "PART_Canvas";
    internal const string PillHostPart = "PART_PillHost";
    internal const string PillPart = "PART_Pill";
    internal const string SpinnerPart = "PART_Spinner";
    internal const string RingPart = "PART_Ring";
    internal const string CheckPart = "PART_Check";
    internal const string LabelPart = "PART_Label";
    internal const string CountPart = "PART_Count";
    internal const string CountReservePart = "PART_CountReserve";
    internal const string ErrorPart = "PART_Error";
    internal const string ErrorTextPart = "PART_ErrorText";
    internal const string RetryPart = "PART_Retry";
    internal const string LivePart = "PART_Live";

    internal const string SmallPseudoClass = ":small";
    internal const string LargePseudoClass = ":large";
    internal const string QueuedPseudoClass = ":queued";
    internal const string GeneratingPseudoClass = ":generating";
    internal const string DonePseudoClass = ":done";
    internal const string ErrorPseudoClass = ":error";

    /// <summary>A photo swap crossfades from a snapshot of the old frame over this long (seconds).</summary>
    internal const double SwapFade = 0.45;

    /// <summary>The final image (any <see cref="IImage"/>, usually a decoded <see cref="Bitmap"/>); it is cover-cropped to the canvas. With no <see cref="Status"/> and no <see cref="Generator"/> it is simply shown, finished (a simulation is set aside).</summary>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<ImageReveal, IImage?>(nameof(Source));

    /// <summary>The image's name once it is done (the canvas's accessible name); "Generated image" by default.</summary>
    public static readonly StyledProperty<string?> AltTextProperty =
        AvaloniaProperty.Register<ImageReveal, string?>(nameof(AltText));

    /// <summary>The job's status when the app controls it. Null runs the generator, shows <see cref="Source"/>, or plays the <see cref="Simulation"/> (queued, generating over <see cref="Duration"/>, done). Every change transitions from the current pose.</summary>
    public static readonly StyledProperty<ImageRevealStatus?> StatusProperty =
        AvaloniaProperty.Register<ImageReveal, ImageRevealStatus?>(nameof(Status));

    /// <summary>Controlled progress from 0 to 1 while generating; coarse updates (one per model step) are smoothed into continuous motion. Null estimates it from <see cref="Duration"/>.</summary>
    public static readonly StyledProperty<double?> ProgressProperty =
        AvaloniaProperty.Register<ImageReveal, double?>(nameof(Progress));

    /// <summary>How long one generation takes when progress is not given (the simulation's length, or the estimate's).</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<ImageReveal, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(5200));

    /// <summary>The total steps the pill counts, as in 18 / 30 (tabular numerals, reserved width).</summary>
    public static readonly StyledProperty<int> StepsProperty =
        AvaloniaProperty.Register<ImageReveal, int>(nameof(Steps), 30);

    /// <summary>The canvas's width over its height (1.5, or 0.8 for 4 / 5); the image is cover-cropped to it.</summary>
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<ImageReveal, double>(nameof(AspectRatio), 1);

    /// <summary>The overlay's scale and the corner radius: Small (grids, thumbnails) shows only the ring and the count; Medium and Large add the Generating label.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<ImageReveal, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>The progress ring's color; the accent by default.</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<ImageReveal, IBrush?>(nameof(RingBrush));

    /// <summary>Shows the translucent progress pill. It fades out shortly after the image resolves.</summary>
    public static readonly StyledProperty<bool> ShowsProgressProperty =
        AvaloniaProperty.Register<ImageReveal, bool>(nameof(ShowsProgress), true);

    /// <summary>Plays one soft light sheen across the image when it resolves. Skipped with reduced motion.</summary>
    public static readonly StyledProperty<bool> ShowsSheenProperty =
        AvaloniaProperty.Register<ImageReveal, bool>(nameof(ShowsSheen), true);

    /// <summary>The chip while queued, such as "Queued, 2 ahead"; "Queued" by default.</summary>
    public static readonly StyledProperty<string?> QueueLabelProperty =
        AvaloniaProperty.Register<ImageReveal, string?>(nameof(QueueLabel));

    /// <summary>The message of the error state; "Generation failed" by default.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<ImageReveal, string?>(nameof(ErrorMessage));

    /// <summary>Shows a Retry button in the error state; it raises <see cref="RetryRequested"/> (and reruns the <see cref="Generator"/>).</summary>
    public static readonly StyledProperty<bool> AllowsRetryProperty =
        AvaloniaProperty.Register<ImageReveal, bool>(nameof(AllowsRetry), true);

    /// <summary>Simulation and generator: starts a run as soon as the control is shown. Otherwise it waits in the queued look for <see cref="Restart"/>.</summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<ImageReveal, bool>(nameof(AutoPlay), true);

    /// <summary>Simulation only: after <see cref="HoldDuration"/> the image blurs back out and generates again.</summary>
    public static readonly StyledProperty<bool> IsLoopingProperty =
        AvaloniaProperty.Register<ImageReveal, bool>(nameof(IsLooping), true);

    /// <summary>Simulation only: extra queue time before each run, for staggered grids.</summary>
    public static readonly StyledProperty<TimeSpan> StartDelayProperty =
        AvaloniaProperty.Register<ImageReveal, TimeSpan>(nameof(StartDelay));

    /// <summary>Simulation only: how long each run waits in the queue.</summary>
    public static readonly StyledProperty<TimeSpan> QueueDurationProperty =
        AvaloniaProperty.Register<ImageReveal, TimeSpan>(nameof(QueueDuration), TimeSpan.FromMilliseconds(900));

    /// <summary>Simulation only: how long the finished image holds before a loop restarts.</summary>
    public static readonly StyledProperty<TimeSpan> HoldDurationProperty =
        AvaloniaProperty.Register<ImageReveal, TimeSpan>(nameof(HoldDuration), TimeSpan.FromMilliseconds(2600));

    /// <summary>Simulation and generator: change it to blur back out and run again (a Generate again button can bind its count here).</summary>
    public static readonly StyledProperty<object?> RunKeyProperty =
        AvaloniaProperty.Register<ImageReveal, object?>(nameof(RunKey));

    /// <summary>Seeds the film grain and the drift of the soft preview (and which of the simulation's pictures comes first), so tiles in a grid differ.</summary>
    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<ImageReveal, int>(nameof(Seed), 1);

    /// <summary>Runs one image job: it reports progress (and the image as soon as it has one) and returns the final image. Used when <see cref="Status"/> is null.</summary>
    public static readonly StyledProperty<ImageRevealGenerator?> GeneratorProperty =
        AvaloniaProperty.Register<ImageReveal, ImageRevealGenerator?>(nameof(Generator));

    /// <summary>
    /// A simulated job to play while <see cref="Status"/>, <see cref="Generator"/> and <see cref="Source"/> are null: it
    /// reveals its pictures in turn on the timing of <see cref="Duration"/>, <see cref="QueueDuration"/> and
    /// <see cref="HoldDuration"/>. Null (the default) leaves the canvas on its plain surface. Give each control its own
    /// simulation.
    /// </summary>
    public static readonly StyledProperty<ImageRevealSimulation?> SimulationProperty =
        AvaloniaProperty.Register<ImageReveal, ImageRevealSimulation?>(nameof(Simulation));

    /// <summary>Every word the control shows or says.</summary>
    public static readonly StyledProperty<ImageRevealLabels> LabelsProperty =
        AvaloniaProperty.Register<ImageReveal, ImageRevealLabels>(nameof(Labels), ImageRevealLabels.Default);

    /// <summary>The status on screen, in every mode (the simulation's and the generator's too).</summary>
    public static readonly DirectProperty<ImageReveal, ImageRevealStatus> ActualStatusProperty =
        AvaloniaProperty.RegisterDirect<ImageReveal, ImageRevealStatus>(nameof(ActualStatus), o => o.ActualStatus);

    /// <summary>Raised when the status on screen changes, including the simulation's transitions. Bubbles.</summary>
    public static readonly RoutedEvent<ImageRevealStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<ImageReveal, ImageRevealStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    /// <summary>Raised once per run when the final image is fully resolved and crisp. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CompletedEvent =
        RoutedEvent.Register<ImageReveal, RoutedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Retry in the error state. Bubbles. Restart your job, then set <see cref="Status"/>.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<ImageReveal, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private readonly ImageRevealEngine _engine = new();
    private readonly LiveLoop _loop;
    private ImageRevealStatus _actualStatus = ImageRevealStatus.Queued;
    private ImageRevealCanvas? _canvas;
    private Control? _pillHost;
    private ImageRevealPill? _pill;
    private ArcSpinner? _spinner;
    private ArcRing? _ring;
    private ArcGlyph? _check;
    private ArcContentSwap? _label;
    private TextBlock? _count;
    private TextBlock? _countReserve;
    private Control? _error;
    private TextBlock? _errorText;
    private AvaloniaButton? _retry;
    private ExplainerAnnouncer? _live;
    private ImageRevealChain? _chain;
    private IImage? _image;
    private IImage? _jobImage;
    private CancellationTokenSource? _job;
    private Mode _mode;
    private bool _begun;
    private int _shownStep = -1;
    private RenderTargetBitmap? _snapshot;
    private double _snapshotOpacity;

    static ImageReveal()
    {
        FocusableProperty.OverrideDefaultValue<ImageReveal>(false);
    }

    /// <summary>Creates the control.</summary>
    public ImageReveal()
    {
        _engine.StatusChanged += OnEngineStatusChanged;
        _engine.Completed += OnEngineCompleted;
        _engine.RunStarting += OnRunStarting;
        _loop = new LiveLoop(this, Frame) { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <inheritdoc cref="SourceProperty"/>
    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="AltTextProperty"/>
    public string? AltText { get => GetValue(AltTextProperty); set => SetValue(AltTextProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public ImageRevealStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ProgressProperty"/>
    public double? Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="StepsProperty"/>
    public int Steps { get => GetValue(StepsProperty); set => SetValue(StepsProperty, value); }

    /// <inheritdoc cref="AspectRatioProperty"/>
    public double AspectRatio { get => GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="RingBrushProperty"/>
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    /// <inheritdoc cref="ShowsProgressProperty"/>
    public bool ShowsProgress { get => GetValue(ShowsProgressProperty); set => SetValue(ShowsProgressProperty, value); }

    /// <inheritdoc cref="ShowsSheenProperty"/>
    public bool ShowsSheen { get => GetValue(ShowsSheenProperty); set => SetValue(ShowsSheenProperty, value); }

    /// <inheritdoc cref="QueueLabelProperty"/>
    public string? QueueLabel { get => GetValue(QueueLabelProperty); set => SetValue(QueueLabelProperty, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <inheritdoc cref="AllowsRetryProperty"/>
    public bool AllowsRetry { get => GetValue(AllowsRetryProperty); set => SetValue(AllowsRetryProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="IsLoopingProperty"/>
    public bool IsLooping { get => GetValue(IsLoopingProperty); set => SetValue(IsLoopingProperty, value); }

    /// <inheritdoc cref="StartDelayProperty"/>
    public TimeSpan StartDelay { get => GetValue(StartDelayProperty); set => SetValue(StartDelayProperty, value); }

    /// <inheritdoc cref="QueueDurationProperty"/>
    public TimeSpan QueueDuration { get => GetValue(QueueDurationProperty); set => SetValue(QueueDurationProperty, value); }

    /// <inheritdoc cref="HoldDurationProperty"/>
    public TimeSpan HoldDuration { get => GetValue(HoldDurationProperty); set => SetValue(HoldDurationProperty, value); }

    /// <inheritdoc cref="RunKeyProperty"/>
    public object? RunKey { get => GetValue(RunKeyProperty); set => SetValue(RunKeyProperty, value); }

    /// <inheritdoc cref="SeedProperty"/>
    public int Seed { get => GetValue(SeedProperty); set => SetValue(SeedProperty, value); }

    /// <inheritdoc cref="GeneratorProperty"/>
    public ImageRevealGenerator? Generator { get => GetValue(GeneratorProperty); set => SetValue(GeneratorProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public ImageRevealSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ImageRevealLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ActualStatusProperty"/>
    public ImageRevealStatus ActualStatus
    {
        get => _actualStatus;
        private set => SetAndRaise(ActualStatusProperty, ref _actualStatus, value);
    }

    /// <inheritdoc cref="StatusChangedEvent"/>
    public event EventHandler<ImageRevealStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    /// <inheritdoc cref="CompletedEvent"/>
    public event EventHandler<RoutedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <summary>
    /// Simulation and generator: blurs whatever is shown back out and runs again (what a Generate again button does).
    /// Does nothing without either, or while the app controls <see cref="Status"/> or shows its own <see cref="Source"/>.
    /// </summary>
    public void Restart()
    {
        switch (CurrentMode)
        {
            case Mode.Auto:
                _engine.Restart();
                break;
            case Mode.Job:
                StartJob();
                break;
            default:
                return;
        }
        _begun = true;
        Wake();
    }

    private enum Mode
    {
        Empty,
        Shown,
        Auto,
        Job,
        Controlled
    }

    private Mode CurrentMode => Status is not null ? Mode.Controlled
        : Generator is not null ? Mode.Job
        : Source is not null ? Mode.Shown
        : Simulation is not null ? Mode.Auto
        : Mode.Empty;

    /// <summary>The pose being drawn (tests read it).</summary>
    internal ImageRevealEngine Engine => _engine;

    /// <summary>The image being revealed: the app's, the generator's, or the simulation's current picture.</summary>
    internal IImage? DisplayedImage => _image;

    /// <summary>The copies made from it (null before layout, or where offscreen rendering is unavailable).</summary>
    internal ImageRevealChain? Chain => _chain;

    /// <summary>True while the canvas has nothing to show: no status, generator, image or simulation.</summary>
    internal bool IsEmpty => CurrentMode == Mode.Empty;

    /// <summary>The words the pill shows now (null when it shows none).</summary>
    internal string? PillText { get; private set; }

    /// <summary>The count the pill shows now ("18 / 30"), or null.</summary>
    internal string? CountText => _count is { IsVisible: true } count ? count.Text : null;

    /// <summary>The latest polite announcement.</summary>
    internal string? Announcement => _live?.Announcement;

    /// <summary>The frame loop is running.</summary>
    internal bool IsFrameLoopRunning => _loop.IsRunning;

    /// <summary>Advances the pose by <paramref name="seconds"/> (tests step time by hand; the frame loop calls it every frame).</summary>
    internal void Advance(double seconds)
    {
        _engine.Level = ArcMotion.GetEffectiveLevel(this);
        _engine.Tick(seconds);
        if (_snapshot is not null)
        {
            _snapshotOpacity = _engine.Level == ArcMotionLevel.Off ? 0 : Math.Max(0, _snapshotOpacity - seconds / SwapFade);
            if (_snapshotOpacity <= 0)
            {
                DropSnapshot();
            }
        }
        Push();
    }

    private bool Frame(double dt)
    {
        Advance(dt);
        return _engine.WantsFrames || _snapshot is not null;
    }

    private void Wake()
    {
        SyncEngine();
        _loop.Start();
        Push();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_retry is not null)
        {
            _retry.Click -= OnRetry;
        }
        if (_canvas is not null)
        {
            _canvas.SizeChanged -= OnCanvasSizeChanged;
        }
        _canvas = e.NameScope.Find<ImageRevealCanvas>(CanvasPart);
        _pillHost = e.NameScope.Find<Control>(PillHostPart);
        _pill = e.NameScope.Find<ImageRevealPill>(PillPart);
        _spinner = e.NameScope.Find<ArcSpinner>(SpinnerPart);
        _ring = e.NameScope.Find<ArcRing>(RingPart);
        _check = e.NameScope.Find<ArcGlyph>(CheckPart);
        _label = e.NameScope.Find<ArcContentSwap>(LabelPart);
        _count = e.NameScope.Find<TextBlock>(CountPart);
        _countReserve = e.NameScope.Find<TextBlock>(CountReservePart);
        _error = e.NameScope.Find<Control>(ErrorPart);
        _errorText = e.NameScope.Find<TextBlock>(ErrorTextPart);
        _retry = e.NameScope.Find<AvaloniaButton>(RetryPart);
        _live = e.NameScope.Find<ExplainerAnnouncer>(LivePart);
        if (_retry is not null)
        {
            _retry.Click += OnRetry;
        }
        if (_canvas is not null)
        {
            _canvas.Engine = _engine;
            _canvas.Seed = Seed;
            _canvas.Chain = _chain;
            _canvas.SizeChanged += OnCanvasSizeChanged;
        }
        if (_count is not null)
        {
            _count.FontFeatures = s_tabular;
        }
        if (_countReserve is not null)
        {
            _countReserve.FontFeatures = s_tabular;
        }
        _shownStep = -1;
        PillText = null;
        UpdatePseudoClasses();
        ApplyRingBrush();
        RefreshStatusParts();
        Push();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_begun)
        {
            Begin();
        }
        Wake();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DropSnapshot();
    }

    /// <summary>
    /// The first run when the control is shown: the simulation or the generator starts (or waits), a controlled job takes its
    /// status, the app's own image rests finished and an empty canvas rests on its surface.
    /// </summary>
    private void Begin()
    {
        _begun = true;
        _mode = CurrentMode;
        switch (_mode)
        {
            case Mode.Auto:
                SyncEngine();
                if (AutoPlay)
                {
                    _engine.Restart();
                }
                else
                {
                    _engine.Idle();
                    UsePicture(first: true);
                }
                break;
            case Mode.Job:
                if (AutoPlay)
                {
                    StartJob();
                }
                break;
            case Mode.Shown:
                SyncEngine();
                _engine.Settle(ImageRevealStatus.Done);
                break;
            case Mode.Empty:
                SyncEngine();
                _engine.Settle(ImageRevealStatus.Queued);
                break;
            default:
                // A job restored as done or failed rests in that pose; only later changes transition.
                SyncEngine();
                _engine.Settle(Status!.Value);
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == StatusProperty || property == GeneratorProperty)
        {
            OnModeInputChanged(change);
        }
        else if (property == SimulationProperty)
        {
            // A new simulation restarts a playing one; it only matters while nothing else drives the canvas.
            if (_begun && (CurrentMode == Mode.Auto || _mode == Mode.Auto))
            {
                OnModeInputChanged(change);
            }
        }
        else if (property == SourceProperty && _begun && CurrentMode != _mode)
        {
            // The app's own image takes over from a simulation or an empty canvas (or clearing it hands the canvas back).
            OnModeInputChanged(change);
        }
        else if (property == SourceProperty || property == ProgressProperty || property == DurationProperty
                 || property == QueueDurationProperty || property == HoldDurationProperty || property == StartDelayProperty
                 || property == IsLoopingProperty || property == ShowsSheenProperty)
        {
            if (_begun)
            {
                Wake();
            }
        }
        else if (property == RunKeyProperty)
        {
            if (_begun)
            {
                Restart();
            }
        }
        else if (property == AspectRatioProperty)
        {
            InvalidateMeasure();
        }
        else if (property == SizeProperty)
        {
            UpdatePseudoClasses();
        }
        else if (property == SeedProperty)
        {
            if (_canvas is not null)
            {
                _canvas.Seed = Seed;
                _canvas.InvalidateVisual();
            }
        }
        else if (property == RingBrushProperty)
        {
            ApplyRingBrush();
        }
        else if (property == LabelsProperty || property == QueueLabelProperty || property == ErrorMessageProperty
                 || property == AllowsRetryProperty || property == ShowsProgressProperty || property == AltTextProperty
                 || property == StepsProperty)
        {
            _shownStep = -1;
            RefreshStatusParts();
            Push();
        }
    }

    private void OnModeInputChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (!_begun)
        {
            return;
        }
        var mode = CurrentMode;
        _mode = mode;
        if (mode != Mode.Job)
        {
            CancelJob();
        }
        switch (mode)
        {
            case Mode.Controlled:
                SyncEngine();
                _engine.Request(Status!.Value);
                break;
            case Mode.Shown:
                SyncEngine();
                _engine.Request(ImageRevealStatus.Done);
                break;
            case Mode.Empty:
                SyncEngine();
                _engine.Idle();
                break;
            case Mode.Job:
                if (change.Property == GeneratorProperty || change.OldValue is not null)
                {
                    if (AutoPlay)
                    {
                        StartJob();
                    }
                }
                break;
            default:
                SyncEngine();
                if (AutoPlay)
                {
                    _engine.Restart();
                }
                else
                {
                    _engine.Idle();
                    UsePicture(first: true);
                }
                break;
        }
        _shownStep = -1;
        RefreshStatusParts();
        Wake();
    }

    /// <summary>Hands the engine what the app asked for, and picks up the image to reveal.</summary>
    private void SyncEngine()
    {
        var mode = CurrentMode;
        _engine.IsAuto = mode == Mode.Auto;
        _engine.IsBlank = mode == Mode.Empty;
        _engine.RequestedProgress = mode == Mode.Controlled ? Progress : _jobProgress;
        _engine.Duration = Math.Max(0, Duration.TotalSeconds);
        _engine.QueueSeconds = Math.Max(0, QueueDuration.TotalSeconds);
        _engine.HoldSeconds = Math.Max(0, HoldDuration.TotalSeconds);
        _engine.StartDelay = Math.Max(0, StartDelay.TotalSeconds);
        _engine.Loops = IsLooping;
        _engine.SheenEnabled = ShowsSheen;
        _engine.Level = ArcMotion.GetEffectiveLevel(this);
        var image = Source ?? mode switch
        {
            Mode.Job => _jobImage,
            Mode.Auto => Simulation?.Current?.Image,
            _ => null,
        };
        SetImage(image);
    }

    private void OnRunStarting()
    {
        if (CurrentMode == Mode.Auto)
        {
            UsePicture(first: false);
        }
    }

    /// <summary>
    /// Moves the simulation to the picture of the run that starts (or, with <paramref name="first"/>, picks the first run's
    /// picture from <see cref="Seed"/> without starting it) and hands it to the canvas.
    /// </summary>
    private void UsePicture(bool first)
    {
        if (Simulation is not { } simulation)
        {
            return;
        }
        if (first)
        {
            simulation.Prepare(Seed);
        }
        else
        {
            simulation.MoveNext(Seed);
        }
        SetImage(simulation.Current?.Image);
        RefreshStatusParts();
    }

    /// <summary>
    /// Swaps the image: a visible old frame is kept as a snapshot that crossfades out over the new one, and the copies are
    /// rebuilt for the new image.
    /// </summary>
    private void SetImage(IImage? image)
    {
        if (ReferenceEquals(image, _image))
        {
            return;
        }
        var visible = _image is not null && _engine.Preview * _engine.Dim > 0.02;
        if (visible)
        {
            TakeSnapshot();
        }
        _image = image;
        _engine.HasImage = image is not null;
        RebuildChain();
    }

    private void TakeSnapshot()
    {
        if (_canvas is not { } canvas || canvas.Bounds.Width < 1 || canvas.Bounds.Height < 1 || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            return;
        }
        try
        {
            var scale = canvas.Scaling;
            var pixels = new PixelSize((int)Math.Ceiling(canvas.Bounds.Width * scale), (int)Math.Ceiling(canvas.Bounds.Height * scale));
            var snapshot = new RenderTargetBitmap(pixels, new Vector(96 * scale, 96 * scale));
            snapshot.Render(canvas);
            DropSnapshot();
            _snapshot = snapshot;
            _snapshotOpacity = 1;
            canvas.Snapshot = snapshot;
            canvas.SnapshotOpacity = 1;
        }
        catch (Exception)
        {
            // No offscreen rendering here: the swap simply happens under the soft preview.
        }
    }

    private void DropSnapshot()
    {
        if (_canvas is not null)
        {
            _canvas.Snapshot = null;
            _canvas.SnapshotOpacity = 0;
        }
        _snapshot?.Dispose();
        _snapshot = null;
        _snapshotOpacity = 0;
    }

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e) => RebuildChain();

    /// <summary>The copies are made once per image and canvas shape (and near-copy width bucket), never per frame.</summary>
    private void RebuildChain()
    {
        var image = _image;
        var canvas = _canvas;
        if (image is null || canvas is null || canvas.Bounds.Width < 1 || canvas.Bounds.Height < 1)
        {
            if (image is null)
            {
                ReplaceChain(null);
            }
            return;
        }
        var aspect = canvas.Bounds.Width / canvas.Bounds.Height;
        var nearWidth = ImageRevealChain.NearWidthFor(canvas.Bounds.Width * canvas.Scaling);
        if (_chain is { } chain && ReferenceEquals(chain.Source, image) && Math.Abs(chain.Aspect - aspect) < 0.005 && chain.NearWidth == nearWidth)
        {
            return;
        }
        ReplaceChain(ImageRevealChain.Build(image, aspect, nearWidth));
    }

    private void ReplaceChain(ImageRevealChain? chain)
    {
        var old = _chain;
        _chain = chain;
        if (_canvas is not null)
        {
            _canvas.Chain = chain;
            _canvas.InvalidateVisual();
        }
        old?.Dispose();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 560;
        var aspect = AspectRatio > 0 && double.IsFinite(AspectRatio) ? AspectRatio : 1;
        var size = new Size(width, width / aspect);
        base.MeasureOverride(size);
        return size;
    }

    private void OnRetry(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(RetryRequestedEvent));
        if (CurrentMode == Mode.Job)
        {
            StartJob();
            Wake();
        }
    }

    private double? _jobProgress;

    /// <summary>Starts the generator for a new run, cancelling the one before.</summary>
    private void StartJob()
    {
        CancelJob();
        if (Generator is not { } generator)
        {
            return;
        }
        var cts = new CancellationTokenSource();
        _job = cts;
        _jobProgress = null;
        _jobImage = null;
        SyncEngine();
        _engine.Request(ImageRevealStatus.Queued);
        var progress = new ImageRevealProgressSink(this, cts);
        _ = RunJobAsync(generator, progress, cts);
    }

    private async Task RunJobAsync(ImageRevealGenerator generator, ImageRevealProgressSink progress, CancellationTokenSource cts)
    {
        try
        {
            var image = await generator(progress, cts.Token);
            if (!ReferenceEquals(_job, cts))
            {
                return;
            }
            if (image is not null)
            {
                _jobImage = image;
            }
            _jobProgress = 1;
            SyncEngine();
            _engine.Request(ImageRevealStatus.Done);
            Wake();
        }
        catch (Exception)
        {
            if (!ReferenceEquals(_job, cts) || cts.IsCancellationRequested)
            {
                return;
            }
            SyncEngine();
            _engine.Request(ImageRevealStatus.Error);
            Wake();
        }
    }

    /// <summary>A progress report from the running job (on the UI thread).</summary>
    internal void Report(CancellationTokenSource job, ImageRevealProgress report)
    {
        if (!ReferenceEquals(_job, job) || job.IsCancellationRequested)
        {
            return;
        }
        _jobProgress = double.IsFinite(report.Progress) ? Math.Clamp(report.Progress, 0, 1) : _jobProgress;
        if (report.Image is not null)
        {
            _jobImage = report.Image;
        }
        SyncEngine();
        if (_engine.Status == ImageRevealStatus.Queued)
        {
            _engine.Request(ImageRevealStatus.Generating);
        }
        Wake();
    }

    private void CancelJob()
    {
        if (_job is null)
        {
            return;
        }
        var job = _job;
        _job = null;
        job.Cancel();
        job.Dispose();
    }

    private void OnEngineStatusChanged(ImageRevealStatus old, ImageRevealStatus status)
    {
        ActualStatus = status;
        UpdatePseudoClasses();
        _shownStep = -1;
        RefreshStatusParts();
        Announce(status switch
        {
            ImageRevealStatus.Queued => QueueLabel ?? Labels.Queued,
            ImageRevealStatus.Generating => Labels.GeneratingImage,
            ImageRevealStatus.Error => ErrorMessage ?? Labels.Failed,
            _ => null,
        });
        RaiseEvent(new ImageRevealStatusChangedEventArgs(StatusChangedEvent, old, status));
    }

    private void OnEngineCompleted()
    {
        RefreshStatusParts();
        Announce(Labels.ImageReady);
        RaiseEvent(new RoutedEventArgs(CompletedEvent));
    }

    private void Announce(string? text)
    {
        if (_live is null || text is null || CurrentMode == Mode.Empty)
        {
            return;
        }
        var said = CurrentMode == Mode.Auto ? text + " (" + Labels.Simulated + ")" : text;
        if (said != _live.Announcement)
        {
            _live.Announce(said);
        }
    }

    private void UpdatePseudoClasses()
    {
        var status = _engine.Status;
        PseudoClasses.Set(SmallPseudoClass, Size == ControlSize.Small);
        PseudoClasses.Set(LargePseudoClass, Size == ControlSize.Large);
        PseudoClasses.Set(QueuedPseudoClass, status == ImageRevealStatus.Queued);
        PseudoClasses.Set(GeneratingPseudoClass, status == ImageRevealStatus.Generating);
        PseudoClasses.Set(DonePseudoClass, status == ImageRevealStatus.Done);
        PseudoClasses.Set(ErrorPseudoClass, status == ImageRevealStatus.Error);
        RefreshStatusParts();
    }

    private void ApplyRingBrush()
    {
        if (_ring is null)
        {
            return;
        }
        if (RingBrush is { } brush)
        {
            _ring.Foreground = brush;
        }
        else
        {
            _ring.ClearValue(ArcRing.ForegroundProperty);
        }
    }

    /// <summary>The name of the canvas: the state while it works, the alt text once done, "No image" while empty.</summary>
    internal string CanvasName => CurrentMode == Mode.Empty ? Labels.NoImage : _engine.Status switch
    {
        ImageRevealStatus.Queued => QueueLabel ?? Labels.Queued,
        ImageRevealStatus.Generating => Labels.GeneratingImage,
        ImageRevealStatus.Error => ErrorMessage ?? Labels.Failed,
        _ => AltText ?? (CurrentMode == Mode.Auto ? Simulation?.Current?.AltText : null) ?? Labels.GeneratedImage,
    };

    /// <summary>Puts the parts that change only with the status (the pill's icon and words, the error state) in line.</summary>
    private void RefreshStatusParts()
    {
        var status = _engine.Status;
        var small = Size == ControlSize.Small;
        AutomationProperties.SetName(this, CanvasName);
        if (_spinner is not null)
        {
            _spinner.IsVisible = status == ImageRevealStatus.Queued;
        }
        if (_ring is not null)
        {
            _ring.IsVisible = status == ImageRevealStatus.Generating;
        }
        if (_check is not null)
        {
            _check.IsVisible = status == ImageRevealStatus.Done;
        }
        string? text = status switch
        {
            ImageRevealStatus.Queued => QueueLabel ?? Labels.Queued,
            ImageRevealStatus.Generating => small ? null : Labels.Generating,
            ImageRevealStatus.Done => small ? null : Labels.Done,
            _ => null,
        };
        PillText = text;
        if (_label is not null)
        {
            if (!Equals(_label.Content, text))
            {
                _label.Content = text;
            }
            _label.IsVisible = text is not null;
        }
        var counting = status == ImageRevealStatus.Generating && Steps > 0;
        if (_count is not null)
        {
            _count.IsVisible = counting;
        }
        if (_countReserve is not null)
        {
            _countReserve.IsVisible = counting;
            _countReserve.Text = Labels.Count(Math.Max(0, Steps), Math.Max(0, Steps));
        }
        if (_pillHost is not null)
        {
            _pillHost.IsVisible = ShowsProgress && status != ImageRevealStatus.Error && CurrentMode != Mode.Empty;
        }
        if (_pill is not null)
        {
            _pill.IsProgress = counting;
            _pill.Maximum = Math.Max(0, Steps);
            if (!counting)
            {
                AutomationProperties.SetName(_pill, text ?? string.Empty);
            }
        }
        if (_errorText is not null)
        {
            _errorText.Text = ErrorMessage ?? Labels.Failed;
        }
        if (_retry is not null)
        {
            _retry.IsVisible = AllowsRetry;
            _retry.Content = Labels.Retry;
        }
    }

    /// <summary>Hands the pose to the canvas and the pill (per frame; only what changed is written).</summary>
    private void Push()
    {
        _canvas?.InvalidateVisual();
        if (_canvas is not null)
        {
            _canvas.SnapshotOpacity = _snapshotOpacity;
        }
        if (_ring is not null && _ring.IsVisible && Math.Abs(_ring.Value - _engine.PillValue) > 0.0005)
        {
            _ring.Value = _engine.PillValue;
        }
        if (_pillHost is not null && Math.Abs(_pillHost.Opacity - _engine.PillOpacity) > 0.001)
        {
            _pillHost.Opacity = _engine.PillOpacity;
        }
        if (_engine.Status == ImageRevealStatus.Generating && Steps > 0)
        {
            var step = _engine.StepOf(Steps);
            if (step != _shownStep)
            {
                _shownStep = step;
                if (_count is not null)
                {
                    _count.Text = Labels.Count(step, Steps);
                }
                if (_pill is not null)
                {
                    _pill.Value = step;
                    AutomationProperties.SetName(_pill, Labels.StepOf(step, Steps));
                }
            }
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ImageRevealAutomationPeer(this);

    /// <summary>Hands a job's reports to the control on the UI thread.</summary>
    private sealed class ImageRevealProgressSink(ImageReveal owner, CancellationTokenSource job) : IProgress<ImageRevealProgress>
    {
        public void Report(ImageRevealProgress value)
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                owner.Report(job, value);
            }
            else
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => owner.Report(job, value));
            }
        }
    }
}

/// <summary>Reports an <see cref="ImageReveal"/> as an image named by its state or alt text.</summary>
internal sealed class ImageRevealAutomationPeer(ImageReveal owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override string GetClassNameCore() => nameof(ImageReveal);

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
