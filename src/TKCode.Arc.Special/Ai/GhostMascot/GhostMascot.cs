using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A floating ghost mascot that shows what an assistant is doing (Arc ghost mascot): a soft sheet whose hem ripples while
/// it hovers, a quiet form shade and two simple eyes, drawn in one control. Idle floats, blinks and glances around;
/// listening comes forward with wide eyes and a small head tilt; thinking drifts up, turns translucent and looks up at a
/// swirl that curls in and out; writing leans toward a line it writes with a tiny pen, eyes on the tip; done lifts with
/// happy eyes, a smile and a sparkle; error wobbles once, sinks and droops its eyes. Every status change blends from
/// the pose on screen over 400ms, never a hard cut. Leave <see cref="Status"/> unset and the ghost plays through every
/// state on a loop (for showcases); <see cref="StatusChanged"/> reports each step.
/// </summary>
[PseudoClasses(IdlePseudoClass, ListeningPseudoClass, ThinkingPseudoClass, WritingPseudoClass, DonePseudoClass,
    ErrorPseudoClass, CompactPseudoClass)]
public partial class GhostMascot : Control
{
    internal const string IdlePseudoClass = ":idle";
    internal const string ListeningPseudoClass = ":listening";
    internal const string ThinkingPseudoClass = ":thinking";
    internal const string WritingPseudoClass = ":writing";
    internal const string DonePseudoClass = ":done";
    internal const string ErrorPseudoClass = ":error";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>
    /// What the assistant is doing. Leave it unset (null) to play through every state on a loop, which is meant for
    /// showcases; <see cref="StatusChanged"/> then reports each step.
    /// </summary>
    public static readonly StyledProperty<GhostMascotStatus?> StatusProperty =
        AvaloniaProperty.Register<GhostMascot, GhostMascotStatus?>(nameof(Status));

    /// <summary>
    /// Width and height in px (default 96), from 24 inline next to a message to 160 in an empty state. Below 56 a compact
    /// drawing takes over: tighter framing, bigger eyes, heavier swirl, pen and line, and no shadow.
    /// </summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<GhostMascot, double>(nameof(Size), 96,
            coerce: (_, v) => double.IsFinite(v) && v > 0 ? Math.Clamp(v, 8, 1024) : 96);

    /// <summary>
    /// The sheet color from the theme: the accent with accent-foreground eyes (default), a pale raised surface with a
    /// hairline and foreground eyes (the marks keep the accent), or the foreground ink with background-colored eyes.
    /// </summary>
    public static readonly StyledProperty<GhostMascotTone> ToneProperty =
        AvaloniaProperty.Register<GhostMascot, GhostMascotTone>(nameof(Tone));

    /// <summary>
    /// Any color for the sheet (a solid brush; a gradient uses its first stop). Overrides <see cref="Tone"/>; the eyes
    /// become a dark mix of it unless <see cref="EyeBrush"/> is set.
    /// </summary>
    public static readonly StyledProperty<IBrush?> SheetBrushProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>(nameof(SheetBrush));

    /// <summary>Any color for the eyes, the happy eyes and the smile.</summary>
    public static readonly StyledProperty<IBrush?> EyeBrushProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>(nameof(EyeBrush));

    /// <summary>Any color for the marks: the thinking swirl, the pen and its line, and the sparkle.</summary>
    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>(nameof(MarkBrush));

    /// <summary>Any color for the form shade clipped to the sheet (null uses a darker, translucent tone of the sheet).</summary>
    public static readonly StyledProperty<IBrush?> ShadeBrushProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>(nameof(ShadeBrush));

    /// <summary>Any color for the floating ground shadow (null uses a translucent foreground).</summary>
    public static readonly StyledProperty<IBrush?> GroundBrushProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>(nameof(GroundBrush));

    /// <summary>
    /// The soft floating shadow under the ghost, which shrinks and fades as the ghost rises. Drawn only at 56px and up.
    /// </summary>
    public static readonly StyledProperty<bool> ShowGroundProperty =
        AvaloniaProperty.Register<GhostMascot, bool>(nameof(ShowGround), true);

    /// <summary>
    /// Freezes the ghost on the current frame; resuming continues from the same frame. The ghost also pauses by itself
    /// while it is off screen or hidden.
    /// </summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<GhostMascot, bool>(nameof(IsPaused));

    /// <summary>The assistant's name used in the accessible label, for example "Assistant, thinking" (default "Assistant").</summary>
    public static readonly StyledProperty<string> AssistantNameProperty =
        AvaloniaProperty.Register<GhostMascot, string>(nameof(AssistantName), "Assistant");

    /// <summary>Overrides the whole accessible label (null builds it from <see cref="AssistantName"/> and the status).</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<GhostMascot, string?>(nameof(AccessibleName));

    /// <summary>
    /// Announces status changes as a polite live region. Turn it on for the one ghost that carries status, not for
    /// decorative copies.
    /// </summary>
    public static readonly StyledProperty<bool> AnnounceProperty =
        AvaloniaProperty.Register<GhostMascot, bool>(nameof(Announce));

    /// <summary>Every word of the accessible label.</summary>
    public static readonly StyledProperty<GhostMascotLabels> LabelsProperty =
        AvaloniaProperty.Register<GhostMascot, GhostMascotLabels>(nameof(Labels), new GhostMascotLabels());

    /// <summary>The status the ghost shows: <see cref="Status"/>, or the current self-play step while it is unset.</summary>
    public static readonly DirectProperty<GhostMascot, GhostMascotStatus> EffectiveStatusProperty =
        AvaloniaProperty.RegisterDirect<GhostMascot, GhostMascotStatus>(nameof(EffectiveStatus), o => o.EffectiveStatus);

    /// <summary>
    /// Raised when the self-playing ghost (<see cref="Status"/> unset) moves to the next state. Use it to sync a caption
    /// or a control.
    /// </summary>
    public static readonly RoutedEvent<GhostMascotStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<GhostMascot, GhostMascotStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    // Theme inks (ArcDesign tokens) and token values, set by the ControlTheme.
    internal static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>("AccentInk");

    internal static readonly StyledProperty<IBrush?> AccentForegroundInkProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>("AccentForegroundInk");

    internal static readonly StyledProperty<IBrush?> ForegroundInkProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>("ForegroundInk");

    internal static readonly StyledProperty<IBrush?> BackgroundInkProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>("BackgroundInk");

    internal static readonly StyledProperty<IBrush?> RaisedInkProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>("RaisedInk");

    internal static readonly StyledProperty<IBrush?> HairlineInkProperty =
        AvaloniaProperty.Register<GhostMascot, IBrush?>("HairlineInk");

    internal static readonly StyledProperty<double> CompactSizeProperty =
        AvaloniaProperty.Register<GhostMascot, double>("CompactSize", 56);

    internal static readonly StyledProperty<double> ShadeOpacityProperty =
        AvaloniaProperty.Register<GhostMascot, double>("ShadeOpacity", 0.16);

    internal static readonly StyledProperty<double> GroundOpacityProperty =
        AvaloniaProperty.Register<GhostMascot, double>("GroundOpacity", 0.14);

    internal static readonly StyledProperty<double> HairlineWidthProperty =
        AvaloniaProperty.Register<GhostMascot, double>("HairlineWidth", 1);

    internal static readonly StyledProperty<double> EyeDarkMixProperty =
        AvaloniaProperty.Register<GhostMascot, double>("EyeDarkMix", 0.72);

    private GhostMascotStatus _effectiveStatus;

    internal IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }
    internal IBrush? AccentForegroundInk { get => GetValue(AccentForegroundInkProperty); set => SetValue(AccentForegroundInkProperty, value); }
    internal IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }
    internal IBrush? BackgroundInk { get => GetValue(BackgroundInkProperty); set => SetValue(BackgroundInkProperty, value); }
    internal IBrush? RaisedInk { get => GetValue(RaisedInkProperty); set => SetValue(RaisedInkProperty, value); }
    internal IBrush? HairlineInk { get => GetValue(HairlineInkProperty); set => SetValue(HairlineInkProperty, value); }
    internal double CompactSize { get => GetValue(CompactSizeProperty); set => SetValue(CompactSizeProperty, value); }
    internal double ShadeOpacity { get => GetValue(ShadeOpacityProperty); set => SetValue(ShadeOpacityProperty, value); }
    internal double GroundOpacity { get => GetValue(GroundOpacityProperty); set => SetValue(GroundOpacityProperty, value); }
    internal double HairlineWidth { get => GetValue(HairlineWidthProperty); set => SetValue(HairlineWidthProperty, value); }
    internal double EyeDarkMix { get => GetValue(EyeDarkMixProperty); set => SetValue(EyeDarkMixProperty, value); }

    static GhostMascot()
    {
        FocusableProperty.OverrideDefaultValue<GhostMascot>(false);
        ClipToBoundsProperty.OverrideDefaultValue<GhostMascot>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<GhostMascot>(Avalonia.Layout.HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<GhostMascot>(Avalonia.Layout.VerticalAlignment.Center);
        AffectsMeasure<GhostMascot>(SizeProperty);
        AffectsRender<GhostMascot>(ShowGroundProperty);
    }

    /// <summary>Creates a ghost that plays through its states until <see cref="Status"/> is set.</summary>
    public GhostMascot()
    {
        InitializeMotion();
        UpdatePseudoClasses();
        UpdateAccessibility();
    }

    /// <summary>Raised when the self-playing ghost moves to the next state.</summary>
    public event EventHandler<GhostMascotStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public GhostMascotStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public GhostMascotTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="SheetBrushProperty"/>
    public IBrush? SheetBrush { get => GetValue(SheetBrushProperty); set => SetValue(SheetBrushProperty, value); }

    /// <inheritdoc cref="EyeBrushProperty"/>
    public IBrush? EyeBrush { get => GetValue(EyeBrushProperty); set => SetValue(EyeBrushProperty, value); }

    /// <inheritdoc cref="MarkBrushProperty"/>
    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    /// <inheritdoc cref="ShadeBrushProperty"/>
    public IBrush? ShadeBrush { get => GetValue(ShadeBrushProperty); set => SetValue(ShadeBrushProperty, value); }

    /// <inheritdoc cref="GroundBrushProperty"/>
    public IBrush? GroundBrush { get => GetValue(GroundBrushProperty); set => SetValue(GroundBrushProperty, value); }

    /// <inheritdoc cref="ShowGroundProperty"/>
    public bool ShowGround { get => GetValue(ShowGroundProperty); set => SetValue(ShowGroundProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="AssistantNameProperty"/>
    public string AssistantName { get => GetValue(AssistantNameProperty); set => SetValue(AssistantNameProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="AnnounceProperty"/>
    public bool Announce { get => GetValue(AnnounceProperty); set => SetValue(AnnounceProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public GhostMascotLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="EffectiveStatusProperty"/>
    public GhostMascotStatus EffectiveStatus
    {
        get => _effectiveStatus;
        private set => SetAndRaise(EffectiveStatusProperty, ref _effectiveStatus, value);
    }

    /// <summary>True while no status is set and the ghost plays through its states.</summary>
    internal bool IsSelfPlaying => Status is null;

    /// <summary>Whether the compact drawing is in use (under 56px).</summary>
    internal bool IsCompact => Size < GetValue(CompactSizeProperty);

    /// <summary>The accessible label now: <see cref="AccessibleName"/>, or the name and the status ("Assistant, thinking").</summary>
    internal string SpokenName
    {
        get
        {
            if (!string.IsNullOrEmpty(AccessibleName))
            {
                return AccessibleName;
            }
            var name = string.IsNullOrWhiteSpace(AssistantName) ? "Assistant" : AssistantName;
            return (Labels ?? new GhostMascotLabels()).LabelFor(name, EffectiveStatus);
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == StatusProperty)
        {
            OnRequestedStatusChanged(change.GetNewValue<GhostMascotStatus?>());
        }
        else if (property == SizeProperty || property == CompactSizeProperty || property == HairlineWidthProperty)
        {
            UpdatePens();
            Sync();
            UpdatePseudoClasses();
            InvalidateVisual();
        }
        else if (property == ToneProperty || property == SheetBrushProperty || property == EyeBrushProperty ||
                 property == MarkBrushProperty || property == ShadeBrushProperty || property == GroundBrushProperty ||
                 property == AccentInkProperty || property == AccentForegroundInkProperty ||
                 property == ForegroundInkProperty || property == BackgroundInkProperty || property == RaisedInkProperty ||
                 property == HairlineInkProperty || property == ShadeOpacityProperty || property == GroundOpacityProperty ||
                 property == EyeDarkMixProperty)
        {
            UpdateColors();
            InvalidateVisual();
        }
        else if (property == IsPausedProperty)
        {
            UpdateLoop();
        }
        else if (property == ArcMotion.LevelProperty)
        {
            RefreshLevel();
        }
        else if (property == AssistantNameProperty || property == AccessibleNameProperty || property == AnnounceProperty ||
                 property == LabelsProperty)
        {
            UpdateAccessibility();
        }
    }

    /// <summary>Shows <paramref name="status"/> (a requested status or the next self-play step).</summary>
    private void ShowStatus(GhostMascotStatus status, bool selfPlay)
    {
        var previous = EffectiveStatus;
        SetPoseStatus(status);
        EffectiveStatus = status;
        UpdatePseudoClasses();
        UpdateAccessibility();
        if (selfPlay && status != previous)
        {
            RaiseEvent(new GhostMascotStatusChangedEventArgs(status));
        }
    }

    private void UpdatePseudoClasses()
    {
        var status = EffectiveStatus;
        PseudoClasses.Set(IdlePseudoClass, status == GhostMascotStatus.Idle);
        PseudoClasses.Set(ListeningPseudoClass, status == GhostMascotStatus.Listening);
        PseudoClasses.Set(ThinkingPseudoClass, status == GhostMascotStatus.Thinking);
        PseudoClasses.Set(WritingPseudoClass, status == GhostMascotStatus.Writing);
        PseudoClasses.Set(DonePseudoClass, status == GhostMascotStatus.Done);
        PseudoClasses.Set(ErrorPseudoClass, status == GhostMascotStatus.Error);
        PseudoClasses.Set(CompactPseudoClass, IsCompact);
    }

    /// <summary>
    /// The ghost is an image with a label built from the name and the status; with <see cref="Announce"/> on it is also a
    /// polite live region, so each change is read once.
    /// </summary>
    private void UpdateAccessibility()
    {
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Image);
        AutomationProperties.SetLiveSetting(this, Announce ? AutomationLiveSetting.Polite : AutomationLiveSetting.Off);
        AutomationProperties.SetName(this, SpokenName);
    }
}
