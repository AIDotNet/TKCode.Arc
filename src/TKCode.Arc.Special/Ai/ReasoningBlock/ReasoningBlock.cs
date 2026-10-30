using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Where a <see cref="ReasoningBlock"/> is in its life.</summary>
public enum ReasoningStatus
{
    /// <summary>The model is thinking: the label shimmers, the seconds count and the latest reasoning streams.</summary>
    Thinking,

    /// <summary>The model has finished: "Thought for 7s", the reasoning behind the head.</summary>
    Done,

    /// <summary>The person stopped it: "Stopped after 4s".</summary>
    Stopped,

    /// <summary>The thinking failed: "Thinking interrupted", with Retry and the reason at the end of the reasoning.</summary>
    Error
}

/// <summary>How a <see cref="ReasoningBlock"/> is drawn.</summary>
public enum ReasoningBlockVariant
{
    /// <summary>A quiet bordered surface.</summary>
    Card,

    /// <summary>No border and no side padding, to sit flush inside a message bubble.</summary>
    Plain
}

/// <summary>
/// The collapsible thinking block of an AI reply (Arc Reasoning block, design reference). While the model thinks, a
/// shimmering label counts the seconds and the latest reasoning streams under it in muted text, two lines tall. When it
/// finishes it folds to "Thought for 7s" with a chevron; opening it shows the full reasoning as steps with headings on a
/// quiet rail, then a footer with the outcome and a copy button.
/// <para>
/// Give it the reasoning as one growing string in <see cref="Reasoning"/> (blank lines split paragraphs and a line written
/// as <c>**Heading**</c> or <c>## Heading</c> starts a step) or as <see cref="ReasoningStep"/>s in <see cref="Steps"/>,
/// and set <see cref="Status"/>. The seconds are counted here unless you set <see cref="Elapsed"/>. With no data it reads
/// "Thought process" and has nothing to open; a <see cref="Simulation"/> plays a reasoning you pass in instead.
/// </para>
/// </summary>
[TemplatePart(TogglePart, typeof(ReasoningBlockToggle))]
[TemplatePart(MarkPart, typeof(ArcGlyph))]
[TemplatePart(LabelPart, typeof(ToolCallText))]
[TemplatePart(TimePart, typeof(TextBlock))]
[TemplatePart(StopPart, typeof(AvaloniaButton))]
[TemplatePart(RetryPart, typeof(AvaloniaButton))]
[TemplatePart(PreviewBoxPart, typeof(AiHeightBox))]
[TemplatePart(PreviewPart, typeof(TextBlock))]
[TemplatePart(BodyPart, typeof(AiHeightBox))]
[TemplatePart(StepsPart, typeof(StackPanel))]
[TemplatePart(ErrorPart, typeof(TextBlock))]
[TemplatePart(FooterPart, typeof(Grid))]
[TemplatePart(StatusGlyphPart, typeof(ArcGlyph))]
[TemplatePart(StatusTextPart, typeof(TextBlock))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[PseudoClasses(ThinkingPseudoClass, ExpandedPseudoClass, StoppedPseudoClass, ErrorPseudoClass, PlainPseudoClass)]
public class ReasoningBlock : TemplatedControl
{
    internal const string TogglePart = "PART_Toggle";
    internal const string MarkPart = "PART_Mark";
    internal const string LabelPart = "PART_Label";
    internal const string TimePart = "PART_Time";
    internal const string StopPart = "PART_Stop";
    internal const string RetryPart = "PART_Retry";
    internal const string PreviewBoxPart = "PART_PreviewBox";
    internal const string PreviewPart = "PART_Preview";
    internal const string BodyPart = "PART_Body";
    internal const string StepsPart = "PART_Steps";
    internal const string ErrorPart = "PART_Error";
    internal const string FooterPart = "PART_Footer";
    internal const string StatusGlyphPart = "PART_StatusGlyph";
    internal const string StatusTextPart = "PART_StatusText";
    internal const string CopyPart = "PART_Copy";

    internal const string ThinkingPseudoClass = ":thinking";
    internal const string ExpandedPseudoClass = ":expanded";
    internal const string StoppedPseudoClass = ":stopped";
    internal const string ErrorPseudoClass = ":error";
    internal const string PlainPseudoClass = ":plain";

    /// <summary>How many characters of the newest reasoning the preview draws from (it then wraps to the token's lines).</summary>
    internal const int PreviewCharacters = 220;

    /// <summary>Where the block is: thinking, done, stopped or failed.</summary>
    public static readonly StyledProperty<ReasoningStatus> StatusProperty =
        AvaloniaProperty.Register<ReasoningBlock, ReasoningStatus>(nameof(Status), ReasoningStatus.Done);

    /// <summary>The reasoning so far as one string. Set it as it grows; it takes the place of <see cref="Steps"/>.</summary>
    public static readonly StyledProperty<string?> ReasoningProperty =
        AvaloniaProperty.Register<ReasoningBlock, string?>(nameof(Reasoning));

    /// <summary>Whether the steps are open (two-way).</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ReasoningBlock, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The time the model has thought. Null counts it here while thinking and keeps the count after.</summary>
    public static readonly StyledProperty<TimeSpan?> ElapsedProperty =
        AvaloniaProperty.Register<ReasoningBlock, TimeSpan?>(nameof(Elapsed));

    /// <summary>The reason shown at the end of the reasoning when <see cref="Status"/> is <see cref="ReasoningStatus.Error"/>.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<ReasoningBlock, string?>(nameof(ErrorText));

    /// <summary>Shows a Stop button while the model thinks; it raises <see cref="StopRequested"/>.</summary>
    public static readonly StyledProperty<bool> AllowsStopProperty =
        AvaloniaProperty.Register<ReasoningBlock, bool>(nameof(AllowsStop));

    /// <summary>The 18px mark at the left of the head (a 24 × 24 stroked glyph); the sparkle by default.</summary>
    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<ReasoningBlock, Geometry?>(nameof(Mark), ArcIcons.Sparkles);

    /// <summary>The mark's color; the accent by default.</summary>
    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<ReasoningBlock, IBrush?>(nameof(MarkBrush));

    /// <summary>A card or a plain block.</summary>
    public static readonly StyledProperty<ReasoningBlockVariant> VariantProperty =
        AvaloniaProperty.Register<ReasoningBlock, ReasoningBlockVariant>(nameof(Variant));

    /// <summary>Every word the block shows or says.</summary>
    public static readonly StyledProperty<ReasoningBlockLabels> LabelsProperty =
        AvaloniaProperty.Register<ReasoningBlock, ReasoningBlockLabels>(nameof(Labels), ReasoningBlockLabels.Default);

    /// <summary>
    /// A simulated model to play while the block has no reasoning, steps, status or time of its own (demos and
    /// prototypes). The block starts it over each time it takes it up; null shows the block's own state.
    /// </summary>
    public static readonly StyledProperty<ReasoningBlockSimulation?> SimulationProperty =
        AvaloniaProperty.Register<ReasoningBlock, ReasoningBlockSimulation?>(nameof(Simulation));

    /// <summary>Raised when the person presses Stop. Bubbles. Abort your request, then set <see cref="Status"/> to Stopped.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StopRequestedEvent =
        RoutedEvent.Register<ReasoningBlock, RoutedEventArgs>(nameof(StopRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Retry on a failed block. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<ReasoningBlock, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    private readonly AvaloniaList<ReasoningStep> _steps = [];
    private readonly List<ReasoningStep> _parsed = [];
    private readonly List<(ReasoningStep Step, ReasoningStepView View)> _views = [];
    private LiveLoop? _clock;
    private double _counted;
    private ReasoningBlockSimulation? _playing;
    private ReasoningBlockSimulation? _hooked;
    private bool _attached;
    private ReasoningBlockToggle? _toggle;
    private AvaloniaButton? _stop;
    private AvaloniaButton? _retry;
    private ArcGlyph? _mark;
    private ToolCallText? _label;
    private TextBlock? _time;
    private AiHeightBox? _previewBox;
    private TextBlock? _preview;
    private AiHeightBox? _body;
    private StackPanel? _stepsPanel;
    private TextBlock? _error;
    private Grid? _footer;
    private ArcGlyph? _statusGlyph;
    private TextBlock? _statusText;
    private CopyButton? _copy;
    private string? _shownLabel;
    private readonly TranslateTransform _glide = new();
    private MotionValue<double>? _glideOffset;

    /// <summary>The preview's glide: the text moves up by the height of the line that just wrapped, on a bounce-free 0.24s spring.</summary>
    private static readonly SpringSpec s_glideSpring = SpringSpec.FromVisual(0.24, 0);

    /// <summary>Creates the block.</summary>
    public ReasoningBlock()
    {
        _steps.CollectionChanged += OnStepsChanged;
    }

    /// <inheritdoc cref="StatusProperty"/>
    public ReasoningStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ReasoningProperty"/>
    public string? Reasoning { get => GetValue(ReasoningProperty); set => SetValue(ReasoningProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="ElapsedProperty"/>
    public TimeSpan? Elapsed { get => GetValue(ElapsedProperty); set => SetValue(ElapsedProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="AllowsStopProperty"/>
    public bool AllowsStop { get => GetValue(AllowsStopProperty); set => SetValue(AllowsStopProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="MarkBrushProperty"/>
    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    /// <inheritdoc cref="VariantProperty"/>
    public ReasoningBlockVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ReasoningBlockLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public ReasoningBlockSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <summary>The reasoning as steps, when it does not come as one string. Append to a step's text as it streams.</summary>
    [Content]
    public AvaloniaList<ReasoningStep> Steps => _steps;

    /// <inheritdoc cref="StopRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? StopRequested
    {
        add => AddHandler(StopRequestedEvent, value);
        remove => RemoveHandler(StopRequestedEvent, value);
    }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <summary>Whether the app has given the block anything: steps, text, a status other than Done, or a time.</summary>
    internal bool HasOwnData =>
        Status != ReasoningStatus.Done || _steps.Count > 0 || Reasoning is not null || Elapsed is not null || _counted > 0;

    /// <summary>The simulation on screen: the one set, while the block has nothing of its own.</summary>
    internal ReasoningBlockSimulation? ActiveSimulation => HasOwnData ? null : Simulation;

    /// <summary>Whether the block plays its <see cref="Simulation"/>.</summary>
    internal bool IsSimulating => ActiveSimulation is not null;

    /// <summary>The status on screen: the simulation's while it plays, otherwise <see cref="Status"/>.</summary>
    internal ReasoningStatus DisplayedStatus => ActiveSimulation?.Status ?? Status;

    /// <summary>The seconds the head shows, or null when the block has no time.</summary>
    internal long? DisplayedSeconds
    {
        get
        {
            if (ActiveSimulation is { } simulation)
            {
                return simulation.HasStarted ? AiDuration.WholeSecondsFloor(simulation.Elapsed) : null;
            }
            if (Elapsed is { } elapsed)
            {
                return AiDuration.WholeSecondsFloor(elapsed);
            }
            return _counted > 0 || Status == ReasoningStatus.Thinking ? (long)_counted : null;
        }
    }

    /// <summary>The word of the head: "Thinking", "Thought", "Thought process", "Stopped" or "Thinking interrupted".</summary>
    internal string DisplayedLabel => DisplayedStatus switch
    {
        ReasoningStatus.Thinking => Labels.Thinking,
        ReasoningStatus.Stopped => Labels.Stopped,
        ReasoningStatus.Error => Labels.Interrupted,
        _ => DisplayedSeconds is null ? Labels.ThoughtProcess : Labels.Thought,
    };

    /// <summary>The time after the word, or empty.</summary>
    internal string DisplayedTime => (DisplayedStatus, DisplayedSeconds) switch
    {
        (ReasoningStatus.Error, _) or (_, null) => string.Empty,
        (ReasoningStatus.Thinking, < 1) => string.Empty,
        (ReasoningStatus.Stopped, { } s) => Labels.After(s),
        (_, { } s) => Labels.Duration(s),
    };

    internal AiHeightBox? BodyBox => _body;

    internal AiHeightBox? PreviewBox => _previewBox;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _toggle = e.NameScope.Find<ReasoningBlockToggle>(TogglePart);
        _stop = e.NameScope.Find<AvaloniaButton>(StopPart);
        _retry = e.NameScope.Find<AvaloniaButton>(RetryPart);
        _mark = e.NameScope.Find<ArcGlyph>(MarkPart);
        _label = e.NameScope.Find<ToolCallText>(LabelPart);
        _time = e.NameScope.Find<TextBlock>(TimePart);
        _previewBox = e.NameScope.Find<AiHeightBox>(PreviewBoxPart);
        _preview = e.NameScope.Find<TextBlock>(PreviewPart);
        _body = e.NameScope.Find<AiHeightBox>(BodyPart);
        _stepsPanel = e.NameScope.Find<StackPanel>(StepsPart);
        _error = e.NameScope.Find<TextBlock>(ErrorPart);
        _footer = e.NameScope.Find<Grid>(FooterPart);
        _statusGlyph = e.NameScope.Find<ArcGlyph>(StatusGlyphPart);
        _statusText = e.NameScope.Find<TextBlock>(StatusTextPart);
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        if (_toggle is not null)
        {
            _toggle.Click += OnToggle;
        }
        if (_stop is not null)
        {
            _stop.Click += OnStop;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetry;
        }
        if (_preview is not null)
        {
            _preview.RenderTransform = _glide;
            _preview.SizeChanged += OnPreviewSizeChanged;
        }
        // Opening, closing and the preview folding away all use one critically damped spring (0.26s).
        if (_body is not null)
        {
            _body.HeightSpring = AiGlyphMotion.Settle;
        }
        if (_previewBox is not null)
        {
            _previewBox.HeightSpring = AiGlyphMotion.Settle;
        }
        _views.Clear();
        _stepsPanel?.Children.Clear();
        _shownLabel = null;
        PseudoClasses.Set(PlainPseudoClass, Variant == ReasoningBlockVariant.Plain);
        SetStatusClasses();
        PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
        Refresh(instant: true);
    }

    private void Unhook()
    {
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggle;
        }
        if (_stop is not null)
        {
            _stop.Click -= OnStop;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetry;
        }
        if (_preview is not null)
        {
            _preview.SizeChanged -= OnPreviewSizeChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            if (Status == ReasoningStatus.Thinking && change.GetOldValue<ReasoningStatus>() != ReasoningStatus.Thinking)
            {
                _counted = 0;
            }
            SyncSimulation();
            Refresh(instant: false);
        }
        else if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
            Refresh(instant: false);
        }
        else if (change.Property == VariantProperty)
        {
            PseudoClasses.Set(PlainPseudoClass, Variant == ReasoningBlockVariant.Plain);
        }
        else if (change.Property == ElapsedProperty || change.Property == ReasoningProperty || change.Property == SimulationProperty)
        {
            if (change.Property == SimulationProperty)
            {
                Hook();
            }
            SyncSimulation();
            Refresh(instant: false);
        }
        else if (change.Property == LabelsProperty || change.Property == ErrorTextProperty
                 || change.Property == AllowsStopProperty || change.Property == MarkProperty)
        {
            Refresh(instant: false);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Hook();
        SyncSimulation();
        Refresh(instant: true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Hook();
    }

    /// <summary>Follows the simulation while the block is on a window, so a detached block holds no handler on it.</summary>
    private void Hook()
    {
        var wanted = _attached ? Simulation : null;
        if (ReferenceEquals(wanted, _hooked))
        {
            return;
        }
        if (_hooked is not null)
        {
            _hooked.Changed -= OnSimulationChanged;
        }
        _hooked = wanted;
        if (_hooked is not null)
        {
            _hooked.Changed += OnSimulationChanged;
        }
    }

    private void OnSimulationChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, ActiveSimulation))
        {
            return;
        }
        SetStatusClasses();
        UpdateClock();
        Refresh(instant: false);
    }

    /// <summary>
    /// Takes the simulation up when the block has nothing of its own (starting it over) and lets it go when the app's
    /// data arrives or the simulation is removed; then the status classes and the clock follow.
    /// </summary>
    private void SyncSimulation()
    {
        var active = ActiveSimulation;
        if (!ReferenceEquals(active, _playing))
        {
            _playing = active;
            active?.Restart();
        }
        SetStatusClasses();
        UpdateClock();
    }

    private void SetStatusClasses()
    {
        var status = DisplayedStatus;
        PseudoClasses.Set(ThinkingPseudoClass, status == ReasoningStatus.Thinking);
        PseudoClasses.Set(StoppedPseudoClass, status == ReasoningStatus.Stopped);
        PseudoClasses.Set(ErrorPseudoClass, status == ReasoningStatus.Error);
    }

    /// <summary>
    /// The live preview stays anchored to its newest line. When a line wraps the text grows upward, so it is moved back down
    /// by the growth and glides up to rest instead of jumping (code-driven because the offset is the measured height change,
    /// which a ControlTheme transition cannot express). Reduced motion and Off: no glide.
    /// </summary>
    private void OnPreviewSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var grew = e.NewSize.Height - e.PreviousSize.Height;
        if (grew <= 0.5 || e.PreviousSize.Height <= 0 || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            return;
        }
        if (_glideOffset is null)
        {
            _glideOffset = new MotionValue<double>(this, 0, MotionVectors.Double);
            _glideOffset.Changed += value => _glide.Y = value;
        }
        _glideOffset.Jump(grew);
        _glide.Y = grew;
        _ = _glideOffset.AnimateTo(0, MotionSpec.Spring(s_glideSpring));
    }

    private void OnToggle(object? sender, RoutedEventArgs e) => SetCurrentValue(IsExpandedProperty, !IsExpanded);

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        // A simulated model stops here; a real one is stopped by the app that handles the event.
        ActiveSimulation?.Stop();
        RaiseEvent(new RoutedEventArgs(StopRequestedEvent));
    }

    private void OnRetry(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RetryRequestedEvent));

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ReasoningStep step in e.OldItems)
            {
                step.PropertyChanged -= OnStepChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (ReasoningStep step in e.NewItems)
            {
                step.PropertyChanged += OnStepChanged;
            }
        }
        SyncSimulation();
        Refresh(instant: false);
    }

    private void OnStepChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ReasoningStep.TextProperty || e.Property == ReasoningStep.HeadingProperty)
        {
            Refresh(instant: false);
        }
    }

    /// <summary>
    /// One clock, at every motion level and only on screen: it advances a playing simulation, or counts the seconds while
    /// the app's block thinks without an <see cref="Elapsed"/> of its own.
    /// </summary>
    private void UpdateClock()
    {
        var runs = ActiveSimulation is { } simulation
            ? simulation.IsPlaying
            : Status == ReasoningStatus.Thinking && Elapsed is null;
        if (runs)
        {
            _clock ??= new LiveLoop(this, Tick) { MinimumLevel = ArcMotionLevel.Off };
            _clock.Start();
        }
        else
        {
            _clock?.Stop();
        }
    }

    private bool Tick(double seconds)
    {
        if (ActiveSimulation is { } simulation)
        {
            // The simulation raises Changed, which refreshes the block.
            simulation.Advance(TimeSpan.FromSeconds(seconds));
            return simulation.IsPlaying;
        }
        var before = (long)_counted;
        _counted += seconds;
        if ((long)_counted != before)
        {
            Refresh(instant: false);
        }
        return true;
    }

    /// <summary>The steps on screen: the parsed string, the steps given, or what the simulation has streamed.</summary>
    private List<ReasoningStep> DisplayedSteps()
    {
        if ((Reasoning ?? ActiveSimulation?.Text) is { } text)
        {
            var parsed = ReasoningBlockText.Parse(text);
            // The parsed steps are reused in place so streaming never rebuilds a row.
            for (var i = 0; i < parsed.Count; i++)
            {
                if (i == _parsed.Count)
                {
                    _parsed.Add(new ReasoningStep());
                }
                if (_parsed[i].Heading != parsed[i].Heading)
                {
                    _parsed[i].Heading = parsed[i].Heading;
                }
                if (_parsed[i].Text != parsed[i].Text)
                {
                    _parsed[i].Text = parsed[i].Text;
                }
            }
            if (_parsed.Count > parsed.Count)
            {
                _parsed.RemoveRange(parsed.Count, _parsed.Count - parsed.Count);
            }
            return _parsed;
        }
        return [.. _steps];
    }

    /// <summary>Brings every part in line with the state; a part that already shows the value is left alone.</summary>
    private void Refresh(bool instant)
    {
        if (_toggle is null)
        {
            return;
        }
        var steps = DisplayedSteps();
        var status = DisplayedStatus;
        var thinking = status == ReasoningStatus.Thinking;
        var label = DisplayedLabel;
        var time = DisplayedTime;
        if (_label is not null && _shownLabel != label)
        {
            _shownLabel = label;
            _label.Text = label;
        }
        if (_label is not null)
        {
            _label.IsShimmering = thinking;
        }
        if (_time is not null)
        {
            if (_time.Text != time)
            {
                _time.Text = time;
            }
            _time.IsVisible = time.Length > 0;
        }
        if (_mark is not null)
        {
            _mark.Data = status == ReasoningStatus.Error ? ArcIcons.CircleAlert : Mark ?? ArcIcons.Sparkles;
        }
        var name = time.Length > 0 ? label + " " + time : label;
        AutomationProperties.SetName(_toggle, name + ", " + (IsExpanded ? Labels.Hide : Labels.Show));
        _toggle.IsExpanded = IsExpanded;

        if (_stop is not null)
        {
            _stop.IsVisible = thinking && AllowsStop;
            _stop.Content = Labels.Stop;
        }
        if (_retry is not null)
        {
            _retry.IsVisible = status == ReasoningStatus.Error;
            _retry.Content = Labels.Retry;
        }

        SyncStepViews(steps);

        var showsError = status == ReasoningStatus.Error && !string.IsNullOrEmpty(ErrorText);
        if (_error is not null)
        {
            _error.Text = ErrorText;
            _error.IsVisible = showsError;
        }
        var finished = status is ReasoningStatus.Done or ReasoningStatus.Stopped;
        if (_footer is not null)
        {
            _footer.IsVisible = finished;
            if (finished)
            {
                if (_statusText is not null)
                {
                    _statusText.Text = status == ReasoningStatus.Done ? Labels.Done : Labels.StoppedStatus;
                }
                if (_statusGlyph is not null)
                {
                    _statusGlyph.Data = status == ReasoningStatus.Done ? ArcIcons.Check : ArcIcons.Square;
                }
                if (_copy is not null)
                {
                    var plain = ReasoningBlockText.PlainText(steps);
                    if (_copy.Value != plain)
                    {
                        _copy.Value = plain;
                    }
                    AutomationProperties.SetName(_copy, Labels.CopyReasoning);
                }
            }
        }

        if (_preview is not null && _previewBox is not null)
        {
            var latest = steps.Count > 0 ? ReasoningBlockText.Tail(steps[^1].Text, PreviewCharacters) : string.Empty;
            if (_preview.Text != latest)
            {
                _preview.Text = latest;
            }
            Apply(_previewBox, thinking && !IsExpanded && latest.Length > 0, instant);
        }
        if (_body is not null)
        {
            Apply(_body, IsExpanded && (steps.Count > 0 || showsError), instant);
        }
    }

    private static void Apply(AiHeightBox box, bool open, bool instant)
    {
        if (box.IsExpanded == open)
        {
            return;
        }
        if (instant && open)
        {
            box.OpenInstantly();
        }
        else
        {
            box.IsExpanded = open;
        }
    }

    /// <summary>
    /// One view per step, updated in place: streaming text changes a text block, never rebuilds a row, and a step that
    /// arrives while the reasoning is open is appended and fades in on its own (the rest of the list does not move).
    /// </summary>
    private void SyncStepViews(List<ReasoningStep> steps)
    {
        if (_stepsPanel is null)
        {
            return;
        }
        var prefix = _views.Count <= steps.Count;
        for (var i = 0; prefix && i < _views.Count; i++)
        {
            prefix = ReferenceEquals(_views[i].Step, steps[i]);
        }
        if (!prefix)
        {
            _views.Clear();
            _stepsPanel.Children.Clear();
        }
        var fades = prefix && _views.Count > 0 && _body?.IsExpanded == true && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        for (var i = _views.Count; i < steps.Count; i++)
        {
            var view = new ReasoningStepView();
            _views.Add((steps[i], view));
            _stepsPanel.Children.Add(view);
            if (fades)
            {
                view.Opacity = 0;
                var fade = view;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => fade.Opacity = 1, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
        for (var i = 0; i < steps.Count; i++)
        {
            var (step, view) = _views[i];
            if (view.Heading != step.Heading)
            {
                view.Heading = step.Heading;
            }
            if (view.Text != step.Text)
            {
                view.Text = step.Text;
            }
            view.SetPosition(i == 0, i == steps.Count - 1);
        }
    }
}
