using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An animated spark mark that shows an AI at work (Arc AI spark): four tapered primary rays and four lighter diagonal
/// rays around a soft core. Each status is its own motion: a slow breath at rest, a ripple while thinking, a heartbeat
/// while writing, a gather when done and a small shake on an error, and every change blends from the pose the mark is
/// already in, so states never hard cut. Set <see cref="Variant"/> to Pill for a status label or to Avatar for a round
/// tile. Leave <see cref="Status"/> unset and the spark plays through idle, thinking, writing and done on its own (for
/// demos).
/// </summary>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(MarkPart, typeof(AiSparkMark))]
[TemplatePart(LabelBoxPart, typeof(SpringSizeDecorator))]
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[PseudoClasses(IdlePseudoClass, ThinkingPseudoClass, GeneratingPseudoClass, DonePseudoClass, ErrorPseudoClass, BusyPseudoClass,
    MarkPseudoClass, PillPseudoClass, AvatarPseudoClass)]
public class AiSpark : TemplatedControl
{
    internal const string FramePart = "PART_Frame";
    internal const string MarkPart = "PART_Mark";
    internal const string LabelBoxPart = "PART_LabelBox";
    internal const string LabelPart = "PART_Label";

    internal const string IdlePseudoClass = ":idle";
    internal const string ThinkingPseudoClass = ":thinking";
    internal const string GeneratingPseudoClass = ":generating";
    internal const string DonePseudoClass = ":done";
    internal const string ErrorPseudoClass = ":error";
    internal const string BusyPseudoClass = ":busy";
    internal const string MarkPseudoClass = ":mark";
    internal const string PillPseudoClass = ":pill";
    internal const string AvatarPseudoClass = ":avatar";

    /// <summary>The default size of the mark (24), the avatar tile (32) and the mark inside the pill (16).</summary>
    internal const double MarkDefaultSize = 24;

    internal const double AvatarDefaultSize = 32;

    internal const double PillDefaultSize = 16;

    /// <summary>
    /// What the AI is doing. Leave it unset (null) and the spark plays through idle, thinking, generating and done on its
    /// own, which is meant for demos and marketing pages.
    /// </summary>
    public static readonly StyledProperty<AiSparkStatus?> StatusProperty =
        AvaloniaProperty.Register<AiSpark, AiSparkStatus?>(nameof(Status));

    /// <summary>
    /// The size in px of the mark, of the avatar tile, or of the mark inside the pill; NaN (default) gives 24, 32 for the
    /// avatar and 16 in the pill. Set it to the surrounding font size to drop the mark into a line of text. Small marks get
    /// heavier rays and under 18px the diagonals become dots.
    /// </summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AiSpark, double>(nameof(Size), double.NaN);

    /// <summary>
    /// The semantic color of the mark; the diagonals and the core use a lighter tone of it. An error always eases to a muted
    /// danger tone.
    /// </summary>
    public static readonly StyledProperty<AiSparkTone> ToneProperty =
        AvaloniaProperty.Register<AiSpark, AiSparkTone>(nameof(Tone));

    /// <summary>A color for the mark that overrides <see cref="Tone"/> (a solid brush; a gradient uses its first stop).</summary>
    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<AiSpark, IBrush?>(nameof(MarkBrush));

    /// <summary>The spark alone, with a status label in a capsule, or in a round tinted tile.</summary>
    public static readonly StyledProperty<AiSparkVariant> VariantProperty =
        AvaloniaProperty.Register<AiSpark, AiSparkVariant>(nameof(Variant));

    /// <summary>The pill's text, overriding the built-in label for the current status ("Reading 12 files").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AiSpark, string?>(nameof(Label));

    /// <summary>Multiplies every loop and spin speed (1 is the designed pace).</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<AiSpark, double>(nameof(Speed), 1, coerce: (_, v) => double.IsFinite(v) ? Math.Max(0, v) : 1);

    /// <summary>A very slow breathing twinkle while idle. Turn it off for a fully still idle mark.</summary>
    public static readonly StyledProperty<bool> TwinkleProperty =
        AvaloniaProperty.Register<AiSpark, bool>(nameof(Twinkle), true);

    /// <summary>Freezes the animation in place; resuming continues from the same phase.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<AiSpark, bool>(nameof(IsPaused));

    /// <summary>
    /// Announces status changes as a polite live region. Turn it off for decorative copies so only one spark speaks; they
    /// then present as an image with an accessible name.
    /// </summary>
    public static readonly StyledProperty<bool> AnnounceProperty =
        AvaloniaProperty.Register<AiSpark, bool>(nameof(Announce), true);

    /// <summary>The accessible name; null (default) uses a phrase for the status ("AI is thinking"), or the label in the pill.</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<AiSpark, string?>(nameof(AccessibleName));

    /// <summary>Every word the spark shows or announces.</summary>
    public static readonly StyledProperty<AiSparkLabels> LabelsProperty =
        AvaloniaProperty.Register<AiSpark, AiSparkLabels>(nameof(Labels), new AiSparkLabels());

    /// <summary>The status the spark shows: <see cref="Status"/>, or the current self-play step while it is unset.</summary>
    public static readonly DirectProperty<AiSpark, AiSparkStatus> EffectiveStatusProperty =
        AvaloniaProperty.RegisterDirect<AiSpark, AiSparkStatus>(nameof(EffectiveStatus), o => o.EffectiveStatus);

    private AiSparkStatus _effectiveStatus;
    private Border? _frame;
    private AiSparkMark? _mark;
    private ArcContentSwap? _label;
    private SpringSizeDecorator? _labelBox;
    private ToolCallText? _labelText;

    static AiSpark()
    {
        FocusableProperty.OverrideDefaultValue<AiSpark>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<AiSpark>(Avalonia.Layout.HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<AiSpark>(Avalonia.Layout.VerticalAlignment.Center);
    }

    public AiSpark()
    {
        UpdatePseudoClasses();
        UpdateAccessibility();
    }

    /// <inheritdoc cref="StatusProperty"/>
    public AiSparkStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public AiSparkTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="MarkBrushProperty"/>
    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    /// <inheritdoc cref="VariantProperty"/>
    public AiSparkVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="SpeedProperty"/>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <inheritdoc cref="TwinkleProperty"/>
    public bool Twinkle { get => GetValue(TwinkleProperty); set => SetValue(TwinkleProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="AnnounceProperty"/>
    public bool Announce { get => GetValue(AnnounceProperty); set => SetValue(AnnounceProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiSparkLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="EffectiveStatusProperty"/>
    public AiSparkStatus EffectiveStatus
    {
        get => _effectiveStatus;
        private set => SetAndRaise(EffectiveStatusProperty, ref _effectiveStatus, value);
    }

    /// <summary>The text the pill shows now (the label, or the built-in word for the status).</summary>
    internal string LabelText => string.IsNullOrEmpty(Label) ? (Labels ?? new AiSparkLabels()).LabelFor(EffectiveStatus) : Label;

    internal AiSparkMark? Mark => _mark;

    internal ToolCallText? LabelTextPart => _labelText;

    /// <summary>The mark's (or tile's) side in px for the current variant and <see cref="Size"/>.</summary>
    internal double ResolvedSize => double.IsNaN(Size) || Size <= 0
        ? Variant switch
        {
            AiSparkVariant.Avatar => AvatarDefaultSize,
            AiSparkVariant.Pill => PillDefaultSize,
            _ => MarkDefaultSize
        }
        : Size;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_mark is not null)
        {
            _mark.EffectiveStatusChanged -= OnMarkStatusChanged;
        }
        _frame = e.NameScope.Find<Border>(FramePart);
        _mark = e.NameScope.Find<AiSparkMark>(MarkPart);
        _labelBox = e.NameScope.Find<SpringSizeDecorator>(LabelBoxPart);
        _label = e.NameScope.Find<ArcContentSwap>(LabelPart);
        _labelText = null;
        if (_mark is not null)
        {
            _mark.RequestedStatus = Status;
            _mark.Tone = Tone;
            _mark.OverrideBrush = MarkBrush;
            _mark.Speed = Speed;
            _mark.Twinkle = Twinkle;
            _mark.IsPaused = IsPaused;
            _mark.EffectiveStatusChanged += OnMarkStatusChanged;
            EffectiveStatus = _mark.EffectiveStatus;
        }
        UpdateLayoutParts();
        UpdateLabel();
        UpdatePseudoClasses();
        UpdateAccessibility();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == StatusProperty)
        {
            if (_mark is not null)
            {
                _mark.RequestedStatus = Status;
            }
            else
            {
                EffectiveStatus = Status ?? AiSparkStatus.Idle;
                UpdatePseudoClasses();
                UpdateAccessibility();
            }
        }
        else if (property == ToneProperty && _mark is not null)
        {
            _mark.Tone = Tone;
        }
        else if (property == MarkBrushProperty && _mark is not null)
        {
            _mark.OverrideBrush = MarkBrush;
        }
        else if (property == SpeedProperty && _mark is not null)
        {
            _mark.Speed = Speed;
        }
        else if (property == TwinkleProperty && _mark is not null)
        {
            _mark.Twinkle = Twinkle;
        }
        else if (property == IsPausedProperty && _mark is not null)
        {
            _mark.IsPaused = IsPaused;
        }
        else if (property == VariantProperty || property == SizeProperty)
        {
            UpdateLayoutParts();
            UpdatePseudoClasses();
            UpdateLabel();
            UpdateAccessibility();
        }
        else if (property == LabelProperty || property == LabelsProperty)
        {
            UpdateLabel();
            UpdateAccessibility();
        }
        else if (property == AnnounceProperty || property == AccessibleNameProperty)
        {
            UpdateAccessibility();
        }
    }

    private void OnMarkStatusChanged(AiSparkStatus status)
    {
        EffectiveStatus = status;
        UpdatePseudoClasses();
        UpdateLabel();
        UpdateAccessibility();
    }

    private void UpdateLayoutParts()
    {
        var size = ResolvedSize;
        if (_mark is not null)
        {
            _mark.Width = size;
            _mark.Height = size;
            _mark.IsTile = Variant == AiSparkVariant.Avatar;
        }
        if (_labelBox is not null)
        {
            _labelBox.IsVisible = Variant == AiSparkVariant.Pill;
        }
    }

    private void UpdateLabel()
    {
        if (_label is null || Variant != AiSparkVariant.Pill)
        {
            return;
        }
        var text = LabelText;
        var busy = EffectiveStatus is AiSparkStatus.Thinking or AiSparkStatus.Generating;
        if (_labelText is not null && _labelText.Text == text)
        {
            _labelText.IsShimmering = busy;
            return;
        }
        // A new text is a new label: the swap fades the old one out (100ms) and the new one in (160ms, 2px rise).
        _labelText = new ToolCallText
        {
            Text = text,
            IsShimmering = busy,
            MutedForeground = _mark?.MutedInk,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        _label.Content = _labelText;
    }

    private void UpdatePseudoClasses()
    {
        var status = EffectiveStatus;
        PseudoClasses.Set(IdlePseudoClass, status == AiSparkStatus.Idle);
        PseudoClasses.Set(ThinkingPseudoClass, status == AiSparkStatus.Thinking);
        PseudoClasses.Set(GeneratingPseudoClass, status == AiSparkStatus.Generating);
        PseudoClasses.Set(DonePseudoClass, status == AiSparkStatus.Done);
        PseudoClasses.Set(ErrorPseudoClass, status == AiSparkStatus.Error);
        PseudoClasses.Set(BusyPseudoClass, status is AiSparkStatus.Thinking or AiSparkStatus.Generating);
        PseudoClasses.Set(MarkPseudoClass, Variant == AiSparkVariant.Mark);
        PseudoClasses.Set(PillPseudoClass, Variant == AiSparkVariant.Pill);
        PseudoClasses.Set(AvatarPseudoClass, Variant == AiSparkVariant.Avatar);
    }

    /// <summary>The accessible name now: <see cref="AccessibleName"/>, the pill's label, or the status phrase.</summary>
    internal string SpokenName
    {
        get
        {
            if (!string.IsNullOrEmpty(AccessibleName))
            {
                return AccessibleName;
            }
            return Variant == AiSparkVariant.Pill ? LabelText : (Labels ?? new AiSparkLabels()).PhraseFor(EffectiveStatus);
        }
    }

    /// <summary>
    /// A live region (polite) carries the name while <see cref="Announce"/> is on and a status is set, so a change is read
    /// once; decorative copies and self-play present as an image with the name.
    /// </summary>
    private void UpdateAccessibility()
    {
        var speaks = Announce && Status is not null;
        AutomationProperties.SetLiveSetting(this, speaks ? AutomationLiveSetting.Polite : AutomationLiveSetting.Off);
        AutomationProperties.SetControlTypeOverride(this, speaks ? AutomationControlType.Text : AutomationControlType.Image);
        AutomationProperties.SetName(this, SpokenName);
    }
}
