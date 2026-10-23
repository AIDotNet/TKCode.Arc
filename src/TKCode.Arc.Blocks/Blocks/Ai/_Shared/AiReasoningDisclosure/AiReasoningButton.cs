using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The button of an <see cref="AiReasoningDisclosure"/>: 28px high, a pill with 6px sides, the label (14 / 19.6, secondary
/// text; Foreground on hover and while open, never a fill) and a 14px chevron 2px after it that turns 90° when open. The
/// label is a <see cref="TextShimmer"/> while thinking and plain text after; a change swaps in Arc's label mode (old out in
/// 100ms, new in over 160ms rising 2px) while the width follows on <see cref="ArcSpring.Quick"/>, the new text clipped by
/// the width until it catches up. Without anything to open (<see cref="CanToggle"/> false) it is a plain label: no chevron,
/// no hover, not a tab stop. Automation reads the label as the name and reports expanded or collapsed.
/// </summary>
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[PseudoClasses(ExpandedPseudoClass, HoverPseudoClass, StaticPseudoClass)]
internal sealed class AiReasoningButton : AvaloniaButton
{
    public const string LabelPart = "PART_Label";

    public const string ExpandedPseudoClass = ":expanded";

    /// <summary>Mouse or pen over the button (touch leaves no hover look behind).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>Nothing to open: the label alone.</summary>
    public const string StaticPseudoClass = ":static";

    /// <summary>Arc's label text: 19.6 line, tracked −0.01em.</summary>
    internal const double LabelLineHeight = 19.6;

    internal const double LabelLetterSpacing = -0.14;

    public static readonly StyledProperty<bool> IsThinkingProperty =
        AiReasoningDisclosure.IsThinkingProperty.AddOwner<AiReasoningButton>();

    public static readonly StyledProperty<string?> ThinkingTextProperty =
        AiReasoningDisclosure.ThinkingTextProperty.AddOwner<AiReasoningButton>();

    public static readonly StyledProperty<string?> ThoughtTextProperty =
        AiReasoningDisclosure.ThoughtTextProperty.AddOwner<AiReasoningButton>();

    /// <summary>Whether the chevron shows the open state (the owner sets it).</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiReasoningButton, bool>(nameof(IsExpanded));

    /// <summary>Whether there is anything to open (the owner sets it); false makes the button a plain label.</summary>
    public static readonly StyledProperty<bool> CanToggleProperty =
        AvaloniaProperty.Register<AiReasoningButton, bool>(nameof(CanToggle), true);

    /// <inheritdoc cref="AiReasoningDisclosure.IsThinkingProperty"/>
    public bool IsThinking { get => GetValue(IsThinkingProperty); set => SetValue(IsThinkingProperty, value); }

    /// <inheritdoc cref="AiReasoningDisclosure.ThinkingTextProperty"/>
    public string? ThinkingText { get => GetValue(ThinkingTextProperty); set => SetValue(ThinkingTextProperty, value); }

    /// <inheritdoc cref="AiReasoningDisclosure.ThoughtTextProperty"/>
    public string? ThoughtText { get => GetValue(ThoughtTextProperty); set => SetValue(ThoughtTextProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="CanToggleProperty"/>
    public bool CanToggle { get => GetValue(CanToggleProperty); set => SetValue(CanToggleProperty, value); }

    private ArcContentSwap? _swap;
    private Control? _label;
    private bool _labelThinking;
    private string? _labelText;

    protected override Type StyleKeyOverride => typeof(AiReasoningButton);

    /// <summary>The label control on screen now (a <see cref="TextShimmer"/> or a <see cref="TextBlock"/>).</summary>
    internal Control? Label => _label;

    /// <summary>The words of the label on screen now.</summary>
    internal string? LabelText => _labelText;

    protected override bool IsEnabledCore => base.IsEnabledCore && CanToggle;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swap = e.NameScope.Find<ArcContentSwap>(LabelPart);
        _label = null;
        _labelText = null;
        UpdateLabel(instant: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsThinkingProperty || change.Property == ThinkingTextProperty || change.Property == ThoughtTextProperty)
        {
            UpdateLabel(instant: false);
        }
        else if (change.Property == IsExpandedProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(ExpandedPseudoClass, open);
            (ControlAutomationPeer.FromElement(this) as AiReasoningButtonAutomationPeer)?.RaiseExpandCollapseChanged(open);
        }
        else if (change.Property == CanToggleProperty)
        {
            PseudoClasses.Set(StaticPseudoClass, !change.GetNewValue<bool>());
            UpdateIsEffectivelyEnabled();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A button disabled under the pointer gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    /// <summary>
    /// The label changes only when the words or the shimmer change. Each change is a new layer so the old one can fade out
    /// in the swap; <paramref name="instant"/> (a new template) puts it in place without motion.
    /// </summary>
    private void UpdateLabel(bool instant)
    {
        var thinking = IsThinking;
        var text = thinking ? ThinkingText : ThoughtText ?? ThinkingText;
        AutomationProperties.SetName(this, text ?? string.Empty);
        if (_swap is null || (_label is not null && thinking == _labelThinking && text == _labelText))
        {
            return;
        }
        _labelThinking = thinking;
        _labelText = text;
        _label = thinking
            ? new TextShimmer { Text = text, IsActive = true, VerticalAlignment = VerticalAlignment.Center }
            : new TextBlock
            {
                Text = text,
                LineHeight = LabelLineHeight,
                LetterSpacing = LabelLetterSpacing,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            };
        if (instant)
        {
            // SetCurrentValue keeps the template's Mode the value a theme can still change.
            var mode = _swap.Mode;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, ContentSwapMode.None);
            _swap.Content = _label;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, mode);
        }
        else
        {
            _swap.Content = _label;
        }
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

    protected override AutomationPeer OnCreateAutomationPeer() => new AiReasoningButtonAutomationPeer(this);

    /// <summary>
    /// A button that opens a region (<c>aria-expanded</c>): named by its label, so the shimmer and the text layers are not
    /// exposed. Expand and Collapse act like a press.
    /// </summary>
    private sealed class AiReasoningButtonAutomationPeer(AiReasoningButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private AiReasoningButton Reasoning => (AiReasoningButton)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            !Reasoning.CanToggle ? ExpandCollapseState.LeafNode
            : Reasoning.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            EnsureEnabled();
            if (!Reasoning.IsExpanded)
            {
                ((IInvokeProvider)this).Invoke();
            }
        }

        public void Collapse()
        {
            EnsureEnabled();
            if (Reasoning.IsExpanded)
            {
                ((IInvokeProvider)this).Invoke();
            }
        }

        public void RaiseExpandCollapseChanged(bool open) =>
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
