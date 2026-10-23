using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The status of one answer in a pane head (Arc model arena): an icon and a word in a 24px pill, 12 / 16.8, the word in the
/// secondary text color.
/// <list type="bullet">
/// <item>Thinking and Writing: a LoaderCircle turning every 900ms and the word shimmering (both start over with a new word);</item>
/// <item>Done: a Success check; Finished first: a flag and AccentStrong text on AccentSubtle; Stopped: a filled square;
/// Failed: a Danger alert circle;</item>
/// <item>Idle: nothing (no content, zero width).</item>
/// </list>
/// A new word swaps in Arc's label mode (the old word fades out in 100ms out of layout, the new one fades in over 160ms rising
/// 2px) while the pill's width follows on <see cref="ArcSpring.Quick"/>, clipped at the moving edge. The narrow tab's pill
/// (<see cref="IsCompact"/>) is 20 high with a 12px icon and says <see cref="FinishedFirstShortText"/>. Reduced motion
/// crossfades in 120ms and jumps the width; Off jumps; the spinner and the shimmer rest below full motion. The words come
/// from the text properties (there is no default text); the word shown is <see cref="Label"/>, also the accessible name.
/// </summary>
[TemplatePart(FacePart, typeof(ArcContentSwap))]
[TemplatePart(SizePart, typeof(SpringSizeDecorator))]
[PseudoClasses(IdlePseudoClass, ThinkingPseudoClass, WritingPseudoClass, DonePseudoClass, FinishedFirstPseudoClass,
    StoppedPseudoClass, FailedPseudoClass, CompactPseudoClass)]
internal sealed class AiStatusPill : TemplatedControl
{
    public const string FacePart = "PART_Face";
    public const string SizePart = "PART_Size";

    public const string IdlePseudoClass = ":idle";
    public const string ThinkingPseudoClass = ":thinking";
    public const string WritingPseudoClass = ":writing";
    public const string DonePseudoClass = ":done";
    public const string FinishedFirstPseudoClass = ":finished-first";
    public const string StoppedPseudoClass = ":stopped";
    public const string FailedPseudoClass = ":failed";
    public const string CompactPseudoClass = ":compact";

    /// <summary>Where the answer is; Idle shows nothing.</summary>
    public static readonly StyledProperty<ArenaPaneStatus> StatusProperty =
        AvaloniaProperty.Register<AiStatusPill, ArenaPaneStatus>(nameof(Status));

    /// <summary>The narrow tab's pill: 20 high, a 12px icon and <see cref="FinishedFirstShortText"/>.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<AiStatusPill, bool>(nameof(IsCompact));

    /// <summary>The word while waiting for the first text ("Thinking").</summary>
    public static readonly StyledProperty<string?> ThinkingTextProperty =
        AvaloniaProperty.Register<AiStatusPill, string?>(nameof(ThinkingText));

    /// <summary>The word while text arrives ("Writing").</summary>
    public static readonly StyledProperty<string?> WritingTextProperty =
        AvaloniaProperty.Register<AiStatusPill, string?>(nameof(WritingText));

    /// <summary>The word of a finished answer ("Done").</summary>
    public static readonly StyledProperty<string?> DoneTextProperty =
        AvaloniaProperty.Register<AiStatusPill, string?>(nameof(DoneText));

    /// <summary>The word of the answer that finished first ("Finished first").</summary>
    public static readonly StyledProperty<string?> FinishedFirstTextProperty =
        AvaloniaProperty.Register<AiStatusPill, string?>(nameof(FinishedFirstText));

    /// <summary>The compact pill's word for the answer that finished first ("First"); null uses <see cref="FinishedFirstText"/>.</summary>
    public static readonly StyledProperty<string?> FinishedFirstShortTextProperty =
        AvaloniaProperty.Register<AiStatusPill, string?>(nameof(FinishedFirstShortText));

    /// <summary>The word of a stopped answer ("Stopped").</summary>
    public static readonly StyledProperty<string?> StoppedTextProperty =
        AvaloniaProperty.Register<AiStatusPill, string?>(nameof(StoppedText));

    /// <summary>The word of a failed answer ("Failed").</summary>
    public static readonly StyledProperty<string?> FailedTextProperty =
        AvaloniaProperty.Register<AiStatusPill, string?>(nameof(FailedText));

    /// <summary>The word shown now (empty while idle); also the accessible name.</summary>
    public static readonly DirectProperty<AiStatusPill, string> LabelProperty =
        AvaloniaProperty.RegisterDirect<AiStatusPill, string>(nameof(Label), o => o.Label);

    private ArcContentSwap? _swap;
    private AiStatusPillFace? _face;
    private string _label = string.Empty;

    static AiStatusPill()
    {
        FocusableProperty.OverrideDefaultValue<AiStatusPill>(false);
        IsTabStopProperty.OverrideDefaultValue<AiStatusPill>(false);
    }

    public AiStatusPill()
    {
        UpdatePseudoClasses();
    }

    /// <inheritdoc cref="StatusProperty"/>
    public ArenaPaneStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <inheritdoc cref="ThinkingTextProperty"/>
    public string? ThinkingText { get => GetValue(ThinkingTextProperty); set => SetValue(ThinkingTextProperty, value); }

    /// <inheritdoc cref="WritingTextProperty"/>
    public string? WritingText { get => GetValue(WritingTextProperty); set => SetValue(WritingTextProperty, value); }

    /// <inheritdoc cref="DoneTextProperty"/>
    public string? DoneText { get => GetValue(DoneTextProperty); set => SetValue(DoneTextProperty, value); }

    /// <inheritdoc cref="FinishedFirstTextProperty"/>
    public string? FinishedFirstText { get => GetValue(FinishedFirstTextProperty); set => SetValue(FinishedFirstTextProperty, value); }

    /// <inheritdoc cref="FinishedFirstShortTextProperty"/>
    public string? FinishedFirstShortText { get => GetValue(FinishedFirstShortTextProperty); set => SetValue(FinishedFirstShortTextProperty, value); }

    /// <inheritdoc cref="StoppedTextProperty"/>
    public string? StoppedText { get => GetValue(StoppedTextProperty); set => SetValue(StoppedTextProperty, value); }

    /// <inheritdoc cref="FailedTextProperty"/>
    public string? FailedText { get => GetValue(FailedTextProperty); set => SetValue(FailedTextProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => _label; private set => SetAndRaise(LabelProperty, ref _label, value); }

    /// <summary>The face on screen now; null while idle.</summary>
    internal AiStatusPillFace? Face => _face;

    /// <summary>The word for <paramref name="status"/>: the compact pill says the short form of Finished first.</summary>
    internal string WordFor(ArenaPaneStatus status, bool compact) => status switch
    {
        ArenaPaneStatus.Thinking => ThinkingText,
        ArenaPaneStatus.Writing => WritingText,
        ArenaPaneStatus.Done => DoneText,
        ArenaPaneStatus.FinishedFirst when compact && !string.IsNullOrEmpty(FinishedFirstShortText) => FinishedFirstShortText,
        ArenaPaneStatus.FinishedFirst => FinishedFirstText,
        ArenaPaneStatus.Stopped => StoppedText,
        ArenaPaneStatus.Failed => FailedText,
        _ => null
    } ?? string.Empty;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swap = e.NameScope.Find<ArcContentSwap>(FacePart);
        _face = null;
        Refresh(instant: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            Refresh(instant: false);
        }
        else if (change.Property == IsCompactProperty)
        {
            // A change of layout, not of status: the face changes size in place of a swap.
            Refresh(instant: true);
        }
        else if (change.Property == ThinkingTextProperty || change.Property == WritingTextProperty || change.Property == DoneTextProperty
                 || change.Property == FinishedFirstTextProperty || change.Property == FinishedFirstShortTextProperty
                 || change.Property == StoppedTextProperty || change.Property == FailedTextProperty)
        {
            Refresh(instant: false);
        }
    }

    private void UpdatePseudoClasses()
    {
        var status = Status;
        PseudoClasses.Set(IdlePseudoClass, status == ArenaPaneStatus.Idle);
        PseudoClasses.Set(ThinkingPseudoClass, status == ArenaPaneStatus.Thinking);
        PseudoClasses.Set(WritingPseudoClass, status == ArenaPaneStatus.Writing);
        PseudoClasses.Set(DonePseudoClass, status == ArenaPaneStatus.Done);
        PseudoClasses.Set(FinishedFirstPseudoClass, status == ArenaPaneStatus.FinishedFirst);
        PseudoClasses.Set(StoppedPseudoClass, status == ArenaPaneStatus.Stopped);
        PseudoClasses.Set(FailedPseudoClass, status == ArenaPaneStatus.Failed);
        PseudoClasses.Set(CompactPseudoClass, IsCompact);
    }

    /// <summary>
    /// Brings the pill in line with its status: a new word is a new face that swaps in (at once when
    /// <paramref name="instant"/>); idle shows no face.
    /// </summary>
    private void Refresh(bool instant)
    {
        UpdatePseudoClasses();
        var status = Status;
        var compact = IsCompact;
        var label = WordFor(status, compact);
        Label = label;
        AutomationProperties.SetName(this, label);
        // Arc puts aria-busy on the shimmering word.
        AutomationProperties.SetItemStatus(this, status is ArenaPaneStatus.Thinking or ArenaPaneStatus.Writing ? "Busy" : null);
        if (_swap is null)
        {
            return;
        }
        if (status == ArenaPaneStatus.Idle)
        {
            _face = null;
            Show(null, instant);
            return;
        }
        if (_face is { } current && current.Status == status && current.Text == label)
        {
            current.IsCompact = compact;
            return;
        }
        _face = new AiStatusPillFace { Status = status, Text = label, IsCompact = compact };
        Show(_face, instant);
    }

    private void Show(AiStatusPillFace? face, bool instant)
    {
        if (instant)
        {
            // SetCurrentValue keeps the template's Mode the value a theme can still change.
            var mode = _swap!.Mode;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, ContentSwapMode.None);
            _swap.Content = face;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, mode);
        }
        else
        {
            _swap!.Content = face;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PillPeer(this);

    /// <summary>One piece of text named by the word shown; the face inside is not read again. Idle leaves the tree.</summary>
    private sealed class PillPeer(AiStatusPill owner) : ControlAutomationPeer(owner)
    {
        private AiStatusPill Pill => (AiStatusPill)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => Pill.Label;

        protected override bool IsContentElementCore() => Pill.Status != ArenaPaneStatus.Idle;

        protected override bool IsControlElementCore() => Pill.Status != ArenaPaneStatus.Idle;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
