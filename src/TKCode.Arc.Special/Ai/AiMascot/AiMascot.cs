using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A small assistant character that shows what an AI is doing (Arc AI mascot, design reference): a soft pebble with pill
/// eyes, a quiet form shade and a companion dot. Idle breathes, floats, blinks and glances around. Listening leans in
/// with wider eyes while the dot pulses like a voice level. Thinking tilts the head and looks up as the dot orbits it,
/// passing behind and in front. Writing looks down and reads along a line the dot writes, then the pen lifts and returns.
/// Done squints happily as the dot turns into a sparkle. Error shakes once and shows worried brows. A status change
/// blends from the pose on screen over about 420ms, and the eyes follow the pointer on hover.
/// <para>
/// Set <see cref="Status"/> from your assistant's phases; leave it unset to play through every state on a loop (a
/// showcase). The mascot is a fixed square of <see cref="Size"/> px, from 24 (inline) to 160 (empty state); below 56 a
/// compact drawing takes over. It pauses by itself off screen, and reduced motion keeps a still pose for each state.
/// </para>
/// </summary>
[TemplatePart(CanvasPart, typeof(AiMascotCanvas))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(CompactPseudoClass)]
public class AiMascot : TemplatedControl
{
    internal const string CanvasPart = "PART_Canvas";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>What the assistant is doing. Leave it null to play through every state on a loop (a showcase).</summary>
    public static readonly StyledProperty<AiMascotStatus?> StatusProperty =
        AvaloniaProperty.Register<AiMascot, AiMascotStatus?>(nameof(Status));

    /// <summary>Width and height in px, from 24 (inline next to a message) to 160 (empty state). Below 56 the compact drawing takes over.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AiMascot, double>(nameof(Size), 96, coerce: (_, v) => double.IsNaN(v) || v < 8 ? 8 : v);

    /// <summary>Where the body color comes from: the accent, the foreground ink or a muted surface with a border.</summary>
    public static readonly StyledProperty<AiMascotTone> ToneProperty =
        AvaloniaProperty.Register<AiMascot, AiMascotTone>(nameof(Tone));

    /// <summary>Any color for the body. Overrides <see cref="Tone"/>; the eyes become a dark mix of it unless <see cref="EyeColor"/> is set.</summary>
    public static readonly StyledProperty<Color?> BodyColorProperty =
        AvaloniaProperty.Register<AiMascot, Color?>(nameof(BodyColor));

    /// <summary>Any color for the eyes, the squint and the brows.</summary>
    public static readonly StyledProperty<Color?> EyeColorProperty =
        AvaloniaProperty.Register<AiMascot, Color?>(nameof(EyeColor));

    /// <summary>Any color for the companion dot and the sparkle; the body color (the foreground for the soft tone) by default.</summary>
    public static readonly StyledProperty<Color?> DotColorProperty =
        AvaloniaProperty.Register<AiMascot, Color?>(nameof(DotColor));

    /// <summary>When the eyes follow the pointer: over the mascot, anywhere in the window or never. Touch is ignored; thinking and writing keep their own gaze.</summary>
    public static readonly StyledProperty<AiMascotPointerFollow> FollowPointerProperty =
        AvaloniaProperty.Register<AiMascot, AiMascotPointerFollow>(nameof(FollowPointer));

    /// <summary>Shows the companion dot that floats, pulses, orbits, writes and becomes the done sparkle.</summary>
    public static readonly StyledProperty<bool> ShowCompanionProperty =
        AvaloniaProperty.Register<AiMascot, bool>(nameof(ShowCompanion), true);

    /// <summary>Shows the soft contact shadow and the writing line under the mascot (drawn only at 56px and up).</summary>
    public static readonly StyledProperty<bool> ShowGroundProperty =
        AvaloniaProperty.Register<AiMascot, bool>(nameof(ShowGround), true);

    /// <summary>Freezes the mascot on the current frame. It also pauses by itself off screen and while hidden, and resumes without a jump.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<AiMascot, bool>(nameof(IsPaused));

    /// <summary>The assistant's name in the accessible label ("Assistant, thinking"); <see cref="AiMascotLabels.DefaultName"/> when null.</summary>
    public static readonly StyledProperty<string?> AssistantNameProperty =
        AvaloniaProperty.Register<AiMascot, string?>(nameof(AssistantName));

    /// <summary>Overrides the whole accessible label.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AiMascot, string?>(nameof(Label));

    /// <summary>Announces status changes in a polite live region. Turn it on for the one mascot that carries status, not for decorative copies.</summary>
    public static readonly StyledProperty<bool> AnnounceProperty =
        AvaloniaProperty.Register<AiMascot, bool>(nameof(Announce));

    /// <summary>Every word the mascot says.</summary>
    public static readonly StyledProperty<AiMascotLabels> LabelsProperty =
        AvaloniaProperty.Register<AiMascot, AiMascotLabels>(nameof(Labels), AiMascotLabels.Default);

    /// <summary>The state shown now: <see cref="Status"/>, or the self-playing tour's state while it is null.</summary>
    public static readonly DirectProperty<AiMascot, AiMascotStatus> DisplayedStatusProperty =
        AvaloniaProperty.RegisterDirect<AiMascot, AiMascotStatus>(nameof(DisplayedStatus), o => o.DisplayedStatus);

    /// <summary>The accessible label in use, for example "Assistant, thinking".</summary>
    public static readonly DirectProperty<AiMascot, string> AccessibleLabelProperty =
        AvaloniaProperty.RegisterDirect<AiMascot, string>(nameof(AccessibleLabel), o => o.AccessibleLabel);

    /// <summary>Raised when the self-playing tour (<see cref="Status"/> null) moves to the next state. Bubbles. Use it to sync a caption or a control.</summary>
    public static readonly RoutedEvent<AiMascotStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<AiMascot, AiMascotStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    private AiMascotCanvas? _canvas;
    private ExplainerAnnouncer? _announcer;
    private AiMascotStatus _displayed;
    private string _accessibleLabel = string.Empty;
    private int _tourIndex;
    private double _tourElapsed;

    static AiMascot()
    {
        FocusableProperty.OverrideDefaultValue<AiMascot>(false);
        AffectsMeasure<AiMascot>(SizeProperty);
    }

    /// <summary>Creates a mascot that plays its tour until <see cref="Status"/> is set.</summary>
    public AiMascot()
    {
        _displayed = AiMascotSamples.Tour[0].Status;
        UpdateLabel();
    }

    /// <inheritdoc cref="StatusProperty"/>
    public AiMascotStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public AiMascotTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="BodyColorProperty"/>
    public Color? BodyColor { get => GetValue(BodyColorProperty); set => SetValue(BodyColorProperty, value); }

    /// <inheritdoc cref="EyeColorProperty"/>
    public Color? EyeColor { get => GetValue(EyeColorProperty); set => SetValue(EyeColorProperty, value); }

    /// <inheritdoc cref="DotColorProperty"/>
    public Color? DotColor { get => GetValue(DotColorProperty); set => SetValue(DotColorProperty, value); }

    /// <inheritdoc cref="FollowPointerProperty"/>
    public AiMascotPointerFollow FollowPointer { get => GetValue(FollowPointerProperty); set => SetValue(FollowPointerProperty, value); }

    /// <inheritdoc cref="ShowCompanionProperty"/>
    public bool ShowCompanion { get => GetValue(ShowCompanionProperty); set => SetValue(ShowCompanionProperty, value); }

    /// <inheritdoc cref="ShowGroundProperty"/>
    public bool ShowGround { get => GetValue(ShowGroundProperty); set => SetValue(ShowGroundProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="AssistantNameProperty"/>
    public string? AssistantName { get => GetValue(AssistantNameProperty); set => SetValue(AssistantNameProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="AnnounceProperty"/>
    public bool Announce { get => GetValue(AnnounceProperty); set => SetValue(AnnounceProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiMascotLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="DisplayedStatusProperty"/>
    public AiMascotStatus DisplayedStatus
    {
        get => _displayed;
        private set => SetAndRaise(DisplayedStatusProperty, ref _displayed, value);
    }

    /// <inheritdoc cref="AccessibleLabelProperty"/>
    public string AccessibleLabel
    {
        get => _accessibleLabel;
        private set => SetAndRaise(AccessibleLabelProperty, ref _accessibleLabel, value);
    }

    /// <inheritdoc cref="StatusChangedEvent"/>
    public event EventHandler<AiMascotStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    internal AiMascotCanvas? CanvasPartControl => _canvas;

    internal ExplainerAnnouncer? AnnouncerPartControl => _announcer;

    /// <summary>Whether the self-playing tour is on (<see cref="Status"/> is null).</summary>
    internal bool IsTouring => Status is null;

    /// <summary>Restarts the self-playing tour from its first state.</summary>
    internal void RestartTour()
    {
        _tourIndex = 0;
        _tourElapsed = 0;
        if (Status is null)
        {
            Show(AiMascotSamples.Tour[0].Status, raise: false);
        }
    }

    /// <summary>Moves the tour on by <paramref name="seconds"/> (called by the drawing's clock; tests call it too).</summary>
    internal void AdvanceTour(double seconds)
    {
        if (Status is not null || seconds <= 0)
        {
            return;
        }
        _tourElapsed += seconds;
        var tour = AiMascotSamples.Tour;
        while (_tourElapsed >= tour[_tourIndex].Seconds)
        {
            _tourElapsed -= tour[_tourIndex].Seconds;
            _tourIndex = (_tourIndex + 1) % tour.Length;
            Show(tour[_tourIndex].Status, raise: true);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _canvas?.Attach(null);
        _canvas = e.NameScope.Find<AiMascotCanvas>(CanvasPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_canvas is not null)
        {
            _canvas.Attach(this);
            _canvas.ShowStatus(_displayed);
            AutomationProperties.SetName(_canvas, _accessibleLabel);
        }
        PseudoClasses.Set(CompactPseudoClass, _canvas?.IsCompact ?? Size < 56);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return new Size(Size, Size);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == StatusProperty)
        {
            if (Status is { } status)
            {
                Show(status, raise: false);
            }
            else
            {
                RestartTour();
            }
            _canvas?.UpdateLoop();
        }
        else if (p == IsPausedProperty)
        {
            _canvas?.UpdateLoop();
        }
        else if (p == SizeProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, _canvas?.IsCompact ?? Size < 56);
            _canvas?.InvalidateMeasure();
            _canvas?.InvalidateVisual();
        }
        else if (p == ToneProperty || p == BodyColorProperty || p == EyeColorProperty || p == DotColorProperty)
        {
            _canvas?.InvalidatePalette();
        }
        else if (p == FollowPointerProperty)
        {
            _canvas?.UpdatePointerHost();
        }
        else if (p == ShowCompanionProperty || p == ShowGroundProperty)
        {
            _canvas?.InvalidateVisual();
        }
        else if (p == AssistantNameProperty || p == LabelProperty || p == LabelsProperty)
        {
            UpdateLabel();
        }
    }

    private void Show(AiMascotStatus status, bool raise)
    {
        var previous = _displayed;
        _canvas?.ShowStatus(status);
        if (previous == status)
        {
            return;
        }
        DisplayedStatus = status;
        UpdateLabel();
        if (Announce)
        {
            _announcer?.Announce(_accessibleLabel);
        }
        if (raise)
        {
            RaiseEvent(new AiMascotStatusChangedEventArgs(StatusChangedEvent, status, previous) { Source = this });
        }
    }

    private void UpdateLabel()
    {
        var labels = Labels ?? AiMascotLabels.Default;
        var name = string.IsNullOrWhiteSpace(AssistantName) ? labels.DefaultName : AssistantName!;
        AccessibleLabel = string.IsNullOrWhiteSpace(Label) ? labels.Label(name, labels.StatusWord(_displayed)) : Label!;
        if (_canvas is not null)
        {
            AutomationProperties.SetName(_canvas, _accessibleLabel);
        }
    }
}
