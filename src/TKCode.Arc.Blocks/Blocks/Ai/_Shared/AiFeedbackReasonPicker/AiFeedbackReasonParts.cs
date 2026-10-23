using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The popover of a down rating: 280 wide, padding 16, radius 26, 1px Border on the raised surface (the floating shadow
/// comes from the popup). Its content stage springs to the height of what it shows (Arc's smooth spring) while
/// the reasons give way to the thanks in place. It takes focus when nothing inside has it, and hands keys to its
/// <see cref="AiFeedbackReasonPicker"/>.
/// </summary>
[TemplatePart(StagePart, typeof(SpringSizeDecorator))]
[TemplatePart(SwapPart, typeof(ArcContentSwap))]
internal sealed class AiFeedbackReasonPanel : TemplatedControl
{
    public const string StagePart = "PART_Stage";
    public const string SwapPart = "PART_Swap";

    private ArcContentSwap? _swap;
    private Control? _content;

    static AiFeedbackReasonPanel()
    {
        FocusableProperty.OverrideDefaultValue<AiFeedbackReasonPanel>(true);
        IsTabStopProperty.OverrideDefaultValue<AiFeedbackReasonPanel>(false);
    }

    public AiFeedbackReasonPanel(AiFeedbackReasonPicker owner)
    {
        Owner = owner;
    }

    /// <summary>The picker that owns the popover.</summary>
    public AiFeedbackReasonPicker Owner { get; }

    /// <summary>The height spring of the content.</summary>
    internal SpringSizeDecorator? Stage { get; private set; }

    /// <summary>The swap between the reasons and the thanks.</summary>
    internal ArcContentSwap? Swap => _swap;

    /// <summary>What the popover shows (the reasons or the thanks); a new value swaps in place.</summary>
    public Control? ShownContent
    {
        get => _content;
        set
        {
            _content = value;
            if (_swap is not null)
            {
                _swap.Content = value;
            }
        }
    }

    /// <summary>Shows <paramref name="content"/> without motion (the popover opens with the reasons).</summary>
    public void ShowAtOnce(Control content)
    {
        _content = content;
        if (_swap is null)
        {
            return;
        }
        var mode = _swap.Mode;
        _swap.SetCurrentValue(ArcContentSwap.ModeProperty, ContentSwapMode.None);
        _swap.Content = content;
        _swap.SetCurrentValue(ArcContentSwap.ModeProperty, mode);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Stage = e.NameScope.Find<SpringSizeDecorator>(StagePart);
        _swap = e.NameScope.Find<ArcContentSwap>(SwapPart);
        if (_content is { } content)
        {
            ShowAtOnce(content);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        Owner.OnPanelKeyDown(e);
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PanelPeer(this);

    /// <summary>A named pane (Arc: <c>role="dialog"</c> named "What went wrong").</summary>
    private sealed class PanelPeer(AiFeedbackReasonPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The reasons of a down rating: the question (14 / 19.6, medium, 10px above the reasons), then the reasons as pills that
/// wrap 6px apart both ways. Tab moves between the reasons and comes round from the last to the first.
/// </summary>
internal sealed class AiFeedbackReasonsView : TemplatedControl
{
    /// <summary>The question above the reasons.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiFeedbackReasonsView, string?>(nameof(Title));

    static AiFeedbackReasonsView()
    {
        FocusableProperty.OverrideDefaultValue<AiFeedbackReasonsView>(false);
    }

    public AiFeedbackReasonsView()
    {
        KeyboardNavigation.SetTabNavigation(Reasons, KeyboardNavigationMode.Cycle);
    }

    /// <summary>The question above the reasons.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The reason pills (built by the picker).</summary>
    public WrapPanel Reasons { get; } = new() { ItemSpacing = 6, LineSpacing = 6 };

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<ContentPresenter>("PART_Reasons") is { } presenter)
        {
            presenter.Content = Reasons;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            // The pills form a group named by the question (Arc: role="group" aria-labelledby).
            AutomationProperties.SetName(Reasons, change.GetNewValue<string?>() ?? string.Empty);
        }
    }
}

/// <summary>
/// The thanks after a reason was chosen: a 18px check in the success color (in a 20px cell), 10px, then two lines of
/// 14 / 20.3 (the thanks in medium Foreground, the note in the secondary text color), 4px above and below. Screen readers
/// hear it (polite live region, Arc's <c>role="status"</c>).
/// </summary>
internal sealed class AiFeedbackThanksView : TemplatedControl
{
    /// <summary>The first line of the thanks.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiFeedbackThanksView, string?>(nameof(Title));

    /// <summary>The second line of the thanks.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AiFeedbackThanksView, string?>(nameof(Description));

    static AiFeedbackThanksView()
    {
        FocusableProperty.OverrideDefaultValue<AiFeedbackThanksView>(false);
    }

    public AiFeedbackThanksView()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    /// <summary>The first line ("Thanks for the feedback").</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The second line.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == DescriptionProperty)
        {
            var name = string.IsNullOrEmpty(Description) ? Title : string.IsNullOrEmpty(Title) ? Description : Title + ". " + Description;
            AutomationProperties.SetName(this, name ?? string.Empty);
        }
    }
}

/// <summary>
/// A reason pill: 32px, padding 0 12, pill radius, 1px Border on the surface, 14px in the secondary text color. Hover:
/// SurfaceMuted, BorderStrong and Foreground (160ms). The keyboard ring is 2px, 2px outside.
/// </summary>
[PseudoClasses(HoverPseudoClass)]
internal sealed class AiFeedbackReasonButton : AvaloniaButton
{
    /// <summary>Mouse or pen over the pill.</summary>
    public const string HoverPseudoClass = ":hover";

    public AiFeedbackReasonButton(string reason, int index)
    {
        Reason = reason;
        Index = index;
        Content = reason;
        AutomationProperties.SetName(this, reason);
    }

    /// <summary>The reason.</summary>
    public string Reason { get; }

    /// <summary>Its position in the reasons.</summary>
    public int Index { get; }

    /// <summary>True when the last activation came from the keyboard (Enter or Space), false after a pointer press.</summary>
    internal bool LastActivationWasKeyboard { get; private set; }

    protected override Type StyleKeyOverride => typeof(AiFeedbackReasonButton);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        LastActivationWasKeyboard = false;
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            LastActivationWasKeyboard = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}
