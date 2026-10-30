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
/// An image generation visual that paints the way an artist does (Arc Sketch to image, design reference). Pencil lines
/// sketch the scene stroke by stroke with a pen tip that lifts and travels between strokes, a round brush sweeps flat color
/// blocks in region by region from back to front, two soft detail passes, film grain and a warm light refine it, and it
/// resolves into the finished picture. Under the canvas a stage label with an icon crossfades above the prompt, beside a
/// thin progress line, a percent and a Replay button.
/// <para>
/// Give it a <see cref="Scene"/> (what gets drawn) and, optionally, the finished <see cref="Image"/>. With no
/// <see cref="Status"/> it loops on its own over them; set <see cref="Status"/> (or <see cref="Progress"/>) to follow a real
/// generation. Nothing is generated: it is a simulated preview over your scene. Without a scene or an image it rests on
/// blank paper, waiting. One clock drives a single 0 → 1 timeline; a frame redraws the ink layer and sets layer opacities,
/// and allocates nothing.
/// </para>
/// </summary>
[TemplatePart(CanvasPart, typeof(SketchToImageCanvas))]
[TemplatePart(ProgressPart, typeof(SketchToImageProgress))]
[TemplatePart(StagePart, typeof(ArcContentSwap))]
[TemplatePart(PromptPart, typeof(TextBlock))]
[TemplatePart(PercentPart, typeof(TextBlock))]
[TemplatePart(ReplayPart, typeof(AvaloniaButton))]
[TemplatePart(ReplayTextPart, typeof(TextBlock))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(SmallPseudoClass, LargePseudoClass, NeutralPseudoClass, CompactPseudoClass, BusyPseudoClass)]
public class SketchToImage : TemplatedControl
{
    internal const string CanvasPart = "PART_Canvas";
    internal const string ProgressPart = "PART_Progress";
    internal const string StagePart = "PART_Stage";
    internal const string PromptPart = "PART_Prompt";
    internal const string PercentPart = "PART_Percent";
    internal const string ReplayPart = "PART_Replay";
    internal const string ReplayTextPart = "PART_ReplayText";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string SmallPseudoClass = ":small";
    internal const string LargePseudoClass = ":large";
    internal const string NeutralPseudoClass = ":neutral";
    internal const string CompactPseudoClass = ":compact";
    internal const string BusyPseudoClass = ":busy";

    /// <summary>Below this card width the Replay button drops to its icon.</summary>
    internal const double CompactWidth = 400;

    /// <summary>Lucide "brush" (the blocking stage's icon; not in the Core subset).</summary>
    internal static readonly Geometry BrushGlyph = ArcIcons.Parse(
        "m9.06 11.9 8.07-8.06a2.85 2.85 0 1 1 4.03 4.03l-8.06 8.08 M7.07 14.94c-1.66 0-3 1.35-3 3.02 0 1.33-2.5 1.52-2 2.02 1.08 1.1 2.49 2.02 4 2.02 2.2 0 4-1.8 4-4.04a3.01 3.01 0 0 0-3-3.02z");

    /// <summary>
    /// The generation stage. Null (default) plays the whole pass on its own and loops (a card with no scene and no image waits
    /// on blank paper instead). A stage plays to its end and holds
    /// there with a shimmering label until the next one arrives; moving back (for example to idle for a new request) fades
    /// the canvas to blank paper first. Showing the control with done shows the picture straight away.
    /// </summary>
    public static readonly StyledProperty<SketchToImageStatus?> StatusProperty =
        AvaloniaProperty.Register<SketchToImage, SketchToImageStatus?>(nameof(Status));

    /// <summary>
    /// Where the pass should be, from 0 (blank paper) to 1 (the picture), in place of <see cref="Status"/>'s stage ends
    /// (sketching ends at 0.38, blocking at 0.62, refining at 0.88). The painting still travels there at its own pace.
    /// </summary>
    public static readonly StyledProperty<double?> ProgressProperty =
        AvaloniaProperty.Register<SketchToImage, double?>(nameof(Progress), coerce: (_, value) => value is { } v ? Math.Clamp(v, 0, 1) : null);

    /// <summary>
    /// One full pass from blank paper to the picture. Sketching takes about 38%, blocking 24%, refining 26% and the final
    /// crossfade 12%.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<SketchToImage, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(9));

    /// <summary>When it plays on its own, holds the picture for about two seconds, fades back to blank paper and paints again.</summary>
    public static readonly StyledProperty<bool> IsLoopingProperty =
        AvaloniaProperty.Register<SketchToImage, bool>(nameof(IsLooping), true);

    /// <summary>The card's maximum width (340, 520 or 720px) and pencil weight. Small shows only Replay's icon.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<SketchToImage, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>The color of the pencil lines, pen tip, stage icon and progress line.</summary>
    public static readonly StyledProperty<SketchToImageTone> ToneProperty =
        AvaloniaProperty.Register<SketchToImage, SketchToImageTone>(nameof(Tone));

    /// <summary>The prompt under the stage label, on one line with the full text in a tooltip. Empty or null (the default) hides it.</summary>
    public static readonly StyledProperty<string?> PromptProperty =
        AvaloniaProperty.Register<SketchToImage, string?>(nameof(Prompt));

    /// <summary>What gets drawn and the picture it resolves into. Null (with no <see cref="Image"/>) leaves blank paper: the card waits and does not loop.</summary>
    public static readonly StyledProperty<SketchToImageScene?> SceneProperty =
        AvaloniaProperty.Register<SketchToImage, SketchToImageScene?>(nameof(Scene));

    /// <summary>The finished picture (your real result), stretched to fill the canvas; it overrides the scene's own.</summary>
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<SketchToImage, IImage?>(nameof(Image));

    /// <summary>Shows the Replay button, which fades to blank paper and paints again up to the current stage.</summary>
    public static readonly StyledProperty<bool> ShowsReplayProperty =
        AvaloniaProperty.Register<SketchToImage, bool>(nameof(ShowsReplay), true);

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<SketchToImageLabels> LabelsProperty =
        AvaloniaProperty.Register<SketchToImage, SketchToImageLabels>(nameof(Labels), SketchToImageLabels.Default);

    /// <summary>The stage on screen (it can trail <see cref="Status"/> while the painting travels there).</summary>
    public static readonly DirectProperty<SketchToImage, SketchToImageStatus> StageProperty =
        AvaloniaProperty.RegisterDirect<SketchToImage, SketchToImageStatus>(nameof(Stage), o => o.Stage);

    /// <summary>Raised once each time the stage on screen changes, never per frame. Bubbles.</summary>
    public static readonly RoutedEvent<SketchToImageStageChangedEventArgs> StageChangedEvent =
        RoutedEvent.Register<SketchToImage, SketchToImageStageChangedEventArgs>(nameof(StageChanged), RoutingStrategies.Bubble);

    /// <summary>Raised each time the picture is fully revealed. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CompletedEvent =
        RoutedEvent.Register<SketchToImage, RoutedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <summary>Raised when Replay is pressed, for example to rerun a real request. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ReplayRequestedEvent =
        RoutedEvent.Register<SketchToImage, RoutedEventArgs>(nameof(ReplayRequested), RoutingStrategies.Bubble);

    /// <summary>What an empty card draws: nothing.</summary>
    private static readonly SketchToImageScene s_blank = new();

    private readonly SketchToImagePlayer _player = new();
    private readonly LiveLoop _clock;
    private SketchToImagePrepared? _prepared;
    private SketchToImageCanvas? _canvas;
    private SketchToImageProgress? _progress;
    private ArcContentSwap? _stageSwap;
    private TextBlock? _prompt;
    private TextBlock? _percent;
    private AvaloniaButton? _replay;
    private TextBlock? _replayText;
    private ExplainerAnnouncer? _announcer;
    private SketchToImageStageLabel? _label;
    private SketchToImageStatus _stage;
    private bool _begun;
    private string? _pendingAnnouncement;
    private int _shownPercent = -1;

    static SketchToImage()
    {
        FocusableProperty.OverrideDefaultValue<SketchToImage>(false);
    }

    /// <summary>Creates the card.</summary>
    public SketchToImage()
    {
        _clock = new LiveLoop(this, OnFrame) { MinimumLevel = ArcMotionLevel.Reduced };
        _clock.RunningChanged += OnClockRunningChanged;
        _player.Completed += OnPlayerCompleted;
        _player.Duration = Duration.TotalSeconds;
    }

    /// <inheritdoc cref="StatusProperty"/>
    public SketchToImageStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ProgressProperty"/>
    public double? Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="IsLoopingProperty"/>
    public bool IsLooping { get => GetValue(IsLoopingProperty); set => SetValue(IsLoopingProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public SketchToImageTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="PromptProperty"/>
    public string? Prompt { get => GetValue(PromptProperty); set => SetValue(PromptProperty, value); }

    /// <inheritdoc cref="SceneProperty"/>
    public SketchToImageScene? Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }

    /// <inheritdoc cref="ImageProperty"/>
    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    /// <inheritdoc cref="ShowsReplayProperty"/>
    public bool ShowsReplay { get => GetValue(ShowsReplayProperty); set => SetValue(ShowsReplayProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public SketchToImageLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StageProperty"/>
    public SketchToImageStatus Stage
    {
        get => _stage;
        private set => SetAndRaise(StageProperty, ref _stage, value);
    }

    /// <inheritdoc cref="StageChangedEvent"/>
    public event EventHandler<SketchToImageStageChangedEventArgs>? StageChanged
    {
        add => AddHandler(StageChangedEvent, value);
        remove => RemoveHandler(StageChangedEvent, value);
    }

    /// <inheritdoc cref="CompletedEvent"/>
    public event EventHandler<RoutedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    /// <inheritdoc cref="ReplayRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ReplayRequested
    {
        add => AddHandler(ReplayRequestedEvent, value);
        remove => RemoveHandler(ReplayRequestedEvent, value);
    }

    /// <summary>The position on the timeline drawn now (0 blank paper, 1 the picture).</summary>
    internal double Position => _player.T;

    /// <summary>How much of the canvas shows (it dips to 0 while fading to paper).</summary>
    internal double CanvasFade => _player.Fade;

    internal SketchToImagePlayer Player => _player;

    internal SketchToImageCanvas? Canvas => _canvas;

    internal SketchToImageStageLabel? StageLabel => _label;

    internal ExplainerAnnouncer? Announcer => _announcer;

    /// <summary>Fades the canvas to blank paper and paints again up to the current stage, and raises <see cref="ReplayRequested"/>.</summary>
    public void Replay()
    {
        RaiseEvent(new RoutedEventArgs(ReplayRequestedEvent));
        if (!_begun)
        {
            return;
        }
        _player.Replay(ArcMotion.GetEffectiveLevel(this));
        Drive();
    }

    /// <summary>Whether there is nothing to draw: no scene and no finished image.</summary>
    internal bool IsEmpty => Scene is null && Image is null;

    /// <summary>Where the app sends the painting; null loops on its own. An empty card rests on blank paper.</summary>
    private double? Target => Progress ?? (Status is { } status ? SketchToImageTimeline.EndOf(status) : IsEmpty ? 0 : null);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_replay is not null)
        {
            _replay.Click -= OnReplayClick;
        }
        _canvas = e.NameScope.Find<SketchToImageCanvas>(CanvasPart);
        _progress = e.NameScope.Find<SketchToImageProgress>(ProgressPart);
        _stageSwap = e.NameScope.Find<ArcContentSwap>(StagePart);
        _prompt = e.NameScope.Find<TextBlock>(PromptPart);
        _percent = e.NameScope.Find<TextBlock>(PercentPart);
        _replay = e.NameScope.Find<AvaloniaButton>(ReplayPart);
        _replayText = e.NameScope.Find<TextBlock>(ReplayTextPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_replay is not null)
        {
            _replay.Click += OnReplayClick;
        }
        if (_announcer is not null && _pendingAnnouncement is not null)
        {
            _announcer.Announce(_pendingAnnouncement);
            _pendingAnnouncement = null;
        }
        _label = null;
        _shownPercent = -1;
        UpdateSizeClasses();
        PseudoClasses.Set(NeutralPseudoClass, Tone == SketchToImageTone.Neutral);
        ApplyScene();
        ApplyTexts();
        Sync(force: true);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_begun)
        {
            _begun = true;
            _player.Duration = Duration.TotalSeconds;
            _player.Loops = IsLooping;
            _player.Begin(Target, ArcMotion.GetEffectiveLevel(this));
            if (!_player.IsControlled && _player.IsMoving)
            {
                // Said once; the template (and so the status region) may not exist yet.
                Announce(Labels.LoopingAnnouncement);
            }
            Sync(force: true);
        }
        Drive();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty || change.Property == ProgressProperty)
        {
            if (_begun)
            {
                _player.Retarget(Target, ArcMotion.GetEffectiveLevel(this));
                Drive();
            }
        }
        else if (change.Property == DurationProperty)
        {
            _player.Duration = Duration.TotalSeconds;
        }
        else if (change.Property == IsLoopingProperty)
        {
            _player.Loops = IsLooping;
            if (IsLooping && _begun && !_player.IsControlled && !_player.IsMoving && _player.T >= 1
                && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full)
            {
                _player.Replay(ArcMotionLevel.Full);
                Drive();
            }
        }
        else if (change.Property == SizeProperty)
        {
            UpdateSizeClasses();
        }
        else if (change.Property == ToneProperty)
        {
            PseudoClasses.Set(NeutralPseudoClass, Tone == SketchToImageTone.Neutral);
        }
        else if (change.Property == SceneProperty || change.Property == ImageProperty)
        {
            ApplyScene();
            UpdateCanvasName();
            if (_begun)
            {
                // A scene arriving on an empty card starts its loop; clearing it fades back to blank paper.
                _player.Retarget(Target, ArcMotion.GetEffectiveLevel(this));
                Drive();
            }
        }
        else if (change.Property == PromptProperty || change.Property == ShowsReplayProperty || change.Property == LabelsProperty)
        {
            ApplyTexts();
            if (change.Property == LabelsProperty)
            {
                _label = null;
                Sync(force: true);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateSizeClasses();
    }

    private void UpdateSizeClasses()
    {
        var size = Size;
        PseudoClasses.Set(SmallPseudoClass, size == ControlSize.Small);
        PseudoClasses.Set(LargePseudoClass, size == ControlSize.Large);
        var width = Bounds.Width;
        PseudoClasses.Set(CompactPseudoClass, size == ControlSize.Small || (width > 0 && width < CompactWidth));
    }

    private void OnReplayClick(object? sender, RoutedEventArgs e) => Replay();

    private void ApplyScene()
    {
        var scene = Scene ?? s_blank;
        if (_prepared is null || !ReferenceEquals(_prepared.Scene, scene))
        {
            _prepared = SketchToImagePrepared.From(scene);
        }
        _canvas?.SetScene(_prepared, Image);
    }

    private void ApplyTexts()
    {
        var labels = Labels;
        if (_prompt is not null)
        {
            var prompt = Prompt;
            _prompt.Text = prompt;
            _prompt.IsVisible = !string.IsNullOrEmpty(prompt);
            Tooltip.SetTip(_prompt, string.IsNullOrEmpty(prompt) ? null : prompt);
        }
        if (_replay is not null)
        {
            _replay.IsVisible = ShowsReplay;
            AutomationProperties.SetName(_replay, labels.ReplayName);
        }
        if (_replayText is not null)
        {
            _replayText.Text = labels.Replay;
        }
        if (_progress is not null)
        {
            AutomationProperties.SetName(_progress, labels.Progress);
        }
        UpdateCanvasName();
    }

    /// <summary>Starts or stops the clock: it runs only while something moves (and the card is on screen).</summary>
    private void Drive()
    {
        if (_player.IsMoving && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _player.Land();
        }
        if (_player.IsMoving)
        {
            _clock.Start();
        }
        else
        {
            _clock.Stop();
        }
        Sync(force: false);
    }

    private bool OnFrame(double dt)
    {
        var moving = _player.Step(dt, ArcMotion.GetEffectiveLevel(this));
        Sync(force: false);
        return moving;
    }

    // Motion turned off while the painting moved: the loop stops on its own, so the painting lands on its end state.
    private void OnClockRunningChanged(bool running)
    {
        if (!running && _player.IsMoving && _clock.IsActive && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _clock.Stop();
            _player.Land();
            Sync(force: false);
        }
    }

    private void Announce(string text)
    {
        if (_announcer is null)
        {
            _pendingAnnouncement = text;
            return;
        }
        _announcer.Announce(text);
    }

    private void OnPlayerCompleted() => RaiseEvent(new RoutedEventArgs(CompletedEvent));

    /// <summary>Brings the canvas, the progress line, the percent and the stage label in line with the player.</summary>
    private void Sync(bool force)
    {
        _canvas?.Show(_player.T, _player.Fade);
        var progress = _player.Progress;
        if (_progress is not null)
        {
            _progress.Fraction = progress;
        }
        var percent = (int)Math.Round(progress * 100);
        if (percent != _shownPercent || force)
        {
            _shownPercent = percent;
            if (_percent is not null)
            {
                _percent.Text = Labels.Percent(percent);
            }
            if (_progress is not null)
            {
                _progress.Percent = percent;
            }
        }
        var heading = _player.Target > 0 && _player.Phase == SketchToImagePhase.Play;
        var stage = SketchToImageTimeline.StageAt(_player.T, heading);
        if (stage != _stage || force || _label is null)
        {
            var changed = stage != _stage;
            Stage = stage;
            ShowStage(stage);
            if (changed && _begun)
            {
                RaiseEvent(new SketchToImageStageChangedEventArgs(StageChangedEvent, stage));
                if (_player.IsControlled)
                {
                    Announce(Labels.StageAnnouncement(Labels.Of(stage)));
                }
            }
        }
    }

    private void ShowStage(SketchToImageStatus stage)
    {
        var busy = stage is SketchToImageStatus.Sketching or SketchToImageStatus.Blocking or SketchToImageStatus.Refining;
        PseudoClasses.Set(BusyPseudoClass, busy);
        UpdateCanvasName();
        if (_stageSwap is null)
        {
            return;
        }
        if (_label is not null && _label.Stage == stage)
        {
            _label.IsShimmering = busy;
            return;
        }
        // A new label per stage change (a handful per pass): the swap fades the old one out while this one rises in.
        var label = new SketchToImageStageLabel(stage)
        {
            Glyph = GlyphOf(stage),
            Text = Labels.Of(stage),
            IsShimmering = busy,
        };
        if (_canvas is not null)
        {
            label.Bind(SketchToImageStageLabel.GlyphBrushProperty, _canvas.GetObservable(SketchToImageCanvas.InkBrushProperty));
        }
        _label = label;
        _stageSwap.Content = label;
    }

    private void UpdateCanvasName()
    {
        if (_canvas is null)
        {
            return;
        }
        var alt = Scene?.Alt;
        var name = string.IsNullOrEmpty(alt) ? Labels.Of(_stage)
            : _stage == SketchToImageStatus.Done ? alt
            : Labels.CanvasName(Labels.Of(_stage), alt);
        AutomationProperties.SetName(_canvas, name);
    }

    internal static Geometry GlyphOf(SketchToImageStatus stage) => stage switch
    {
        SketchToImageStatus.Sketching => ArcIcons.Pencil,
        SketchToImageStatus.Blocking => BrushGlyph,
        SketchToImageStatus.Refining => ArcIcons.Sparkles,
        SketchToImageStatus.Done => ArcIcons.Check,
        _ => ArcIcons.Hourglass,
    };
}
