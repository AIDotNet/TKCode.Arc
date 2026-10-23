using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One action under an AI answer (Arc answer: Copy, Answer again, the ratings, Link, the version arrows): a 36 × 36 circle
/// with a 16px Lucide glyph (stroke 1.75) in the secondary text color. Hover fills it SurfaceMuted and turns the glyph
/// Foreground (160ms); a press darkens the fill to half SurfaceMuted, half Border; while its menu or popover is open
/// (<see cref="IsExpanded"/>) it keeps the hover look. Disabled: half opacity, muted glyph. A new <see cref="Glyph"/>
/// swaps with <see cref="AiAnswerGlyphSwap"/>. The keyboard ring is 2px, 2px outside. Its name and tooltip (above) come
/// from the owner. A button with <see cref="HasPopup"/> reports itself as expandable to automation.
/// </summary>
[TemplatePart(GlyphPart, typeof(AiAnswerGlyphSwap))]
[PseudoClasses(HoverPseudoClass, ExpandedPseudoClass)]
internal class AiAnswerActionButton : AvaloniaButton
{
    public const string GlyphPart = "PART_Glyph";

    /// <summary>Mouse or pen over the button.</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>Its menu or popover is open.</summary>
    public const string ExpandedPseudoClass = ":expanded";

    /// <summary>The Lucide glyph.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AiAnswerActionButton, Geometry?>(nameof(Glyph));

    /// <summary>The glyph's size (16).</summary>
    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<AiAnswerActionButton, double>(nameof(GlyphSize), 16);

    /// <summary>The button opens a menu or a popover (automation: expand / collapse).</summary>
    public static readonly StyledProperty<bool> HasPopupProperty =
        AvaloniaProperty.Register<AiAnswerActionButton, bool>(nameof(HasPopup));

    /// <summary>Its menu or popover is open.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiAnswerActionButton, bool>(nameof(IsExpanded));

    private AiAnswerGlyphSwap? _glyph;

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="GlyphSizeProperty"/>
    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    /// <inheritdoc cref="HasPopupProperty"/>
    public bool HasPopup { get => GetValue(HasPopupProperty); set => SetValue(HasPopupProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override Type StyleKeyOverride => typeof(AiAnswerActionButton);

    /// <summary>The glyph part.</summary>
    internal AiAnswerGlyphSwap? GlyphSwap => _glyph;

    /// <summary>The glyph on screen now.</summary>
    internal AiActionGlyph? ShownGlyph => _glyph?.ShownGlyph;

    /// <summary>True when the last activation came from the keyboard (Enter or Space), false after a pointer press.</summary>
    internal bool LastActivationWasKeyboard { get; private set; }

    /// <summary>Whether the glyph is filled (a chosen rating).</summary>
    protected virtual bool IsGlyphFilled => false;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<AiAnswerGlyphSwap>(GlyphPart);
        RefreshGlyph();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlyphProperty || change.Property == GlyphSizeProperty)
        {
            RefreshGlyph();
        }
        else if (change.Property == IsExpandedProperty)
        {
            var expanded = change.GetNewValue<bool>();
            PseudoClasses.Set(ExpandedPseudoClass, expanded);
            RaiseExpandedChanged(expanded);
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A button disabled under the pointer gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    /// <summary>Hands the glyph, its size and its fill to the swap (which animates a change once shown).</summary>
    protected void RefreshGlyph()
    {
        if (_glyph is null)
        {
            return;
        }
        _glyph.Size = GlyphSize;
        // Data first: a new glyph and a new fill at once make one swap (the fill joins the same change).
        _glyph.IsFilled = IsGlyphFilled;
        _glyph.Data = Glyph;
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

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        HasPopup ? new ExpandableAutomationPeer(this) : new ActionButtonAutomationPeer(this);

    /// <summary>Tells automation that the popup opened or closed.</summary>
    private void RaiseExpandedChanged(bool expanded)
    {
        if (ControlAutomationPeer.FromElement(this) is ActionButtonAutomationPeer peer and IExpandCollapseProvider)
        {
            peer.RaiseExpandedChanged(expanded);
        }
    }

    /// <summary>A button named by the owner's wording; the glyph is not read.</summary>
    protected class ActionButtonAutomationPeer(AiAnswerActionButton owner) : ButtonAutomationPeer(owner)
    {
        protected AiAnswerActionButton Action => (AiAnswerActionButton)Owner;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        /// <summary>Clicks the button (the owner opens or closes its popup).</summary>
        internal void Activate()
        {
            EnsureEnabled();
            ((IInvokeProvider)this).Invoke();
        }

        /// <summary>Reports a new expand / collapse state (only peers that offer the pattern are asked).</summary>
        internal void RaiseExpandedChanged(bool expanded) =>
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                expanded ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }

    /// <summary>A button that opens a menu or a popover (<c>aria-haspopup</c>, <c>aria-expanded</c>).</summary>
    private sealed class ExpandableAutomationPeer(AiAnswerActionButton owner) : ActionButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => Action.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand()
        {
            if (!Action.IsExpanded)
            {
                Activate();
            }
        }

        public void Collapse()
        {
            if (Action.IsExpanded)
            {
                Activate();
            }
        }
    }
}

/// <summary>
/// "Good answer" or "Bad answer" under an AI answer: an <see cref="AiAnswerActionButton"/> with a chosen state
/// (<see cref="IsPressedOn"/>, set by the owner from the rating) that fills the glyph in Foreground with no fill behind it;
/// the outline and the filled glyph swap like any glyph change. Automation reports it as a toggle (Arc's
/// <c>aria-pressed</c>); toggling asks the owner, which decides.
/// </summary>
[PseudoClasses(OnPseudoClass)]
internal sealed class AiAnswerRatingButton : AiAnswerActionButton
{
    /// <summary>Set while the rating is chosen.</summary>
    public const string OnPseudoClass = ":on";

    /// <summary>Whether this rating is the chosen one.</summary>
    public static readonly StyledProperty<bool> IsPressedOnProperty =
        AvaloniaProperty.Register<AiAnswerRatingButton, bool>(nameof(IsPressedOn));

    /// <inheritdoc cref="IsPressedOnProperty"/>
    public bool IsPressedOn { get => GetValue(IsPressedOnProperty); set => SetValue(IsPressedOnProperty, value); }

    // The action button's theme serves both (its :on style is the chosen rating).
    protected override Type StyleKeyOverride => typeof(AiAnswerActionButton);

    protected override bool IsGlyphFilled => IsPressedOn;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPressedOnProperty)
        {
            var on = change.GetNewValue<bool>();
            PseudoClasses.Set(OnPseudoClass, on);
            RefreshGlyph();
            (ControlAutomationPeer.FromElement(this) as RatingAutomationPeer)?.RaiseToggleChanged(on);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        HasPopup ? new ExpandableRatingAutomationPeer(this) : new RatingAutomationPeer(this);

    /// <summary>A toggle button (<c>aria-pressed</c>) named by the owner's wording.</summary>
    private class RatingAutomationPeer(AiAnswerRatingButton owner) : ActionButtonAutomationPeer(owner), IToggleProvider
    {
        private AiAnswerRatingButton Rating => (AiAnswerRatingButton)Owner;

        public ToggleState ToggleState => Rating.IsPressedOn ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Activate();

        public void RaiseToggleChanged(bool on) =>
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                on ? ToggleState.Off : ToggleState.On,
                on ? ToggleState.On : ToggleState.Off);
    }

    /// <summary>"Bad answer": a toggle that also opens the reason popover (<c>aria-expanded</c>).</summary>
    private sealed class ExpandableRatingAutomationPeer(AiAnswerRatingButton owner) : RatingAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => Action.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Action.IsExpanded)
            {
                Activate();
            }
        }

        public void Collapse()
        {
            if (Action.IsExpanded)
            {
                Activate();
            }
        }
    }
}
