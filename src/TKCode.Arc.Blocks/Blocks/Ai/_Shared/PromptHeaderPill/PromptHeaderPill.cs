using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Raised when a prompt studio header pill asks its owner to open (or, when open, close) its menu.</summary>
internal sealed class PromptPillOpenRequestedEventArgs : RoutedEventArgs
{
    public PromptPillOpenRequestedEventArgs(RoutedEvent routedEvent, object source, ModelMenuOpenFocus focus)
        : base(routedEvent, source)
    {
        Focus = focus;
    }

    /// <summary><see cref="ModelMenuOpenFocus.Surface"/> for a pointer press, <see cref="ModelMenuOpenFocus.Selected"/> for a key.</summary>
    public ModelMenuOpenFocus Focus { get; }
}

/// <summary>
/// The behavior the prompt studio's two header pills share (the version pill and the model pill): a 32px bordered pill that
/// opens nothing itself. A primary-button press raises <see cref="OpenRequested"/> with <see cref="ModelMenuOpenFocus.Surface"/>
/// (on press, as Arc does, but not with Ctrl held: a context click on macOS); Enter, Space or ↓ raise it with
/// <see cref="ModelMenuOpenFocus.Selected"/>. The owner opens or closes its popover and sets <see cref="IsOpen"/> while it
/// is open (pseudo-class <c>:open</c>, the hover look). The pill never clicks. Hover (<c>:hover</c>) ignores touch, as
/// Arc's <c>(hover: hover)</c> rule does. Assistive technology sees a button that expands a dialog.
/// </summary>
[PseudoClasses(OpenPseudoClass, HoverPseudoClass)]
internal abstract class PromptHeaderPill : AvaloniaButton
{
    public const string OpenPseudoClass = ":open";
    public const string HoverPseudoClass = ":hover";

    /// <summary>Set by the owner while its popover is open: the pill keeps its hover look (the chevron does not turn).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<PromptHeaderPill, bool>(nameof(IsOpen));

    /// <summary>
    /// Raised when the pill asks its owner to open its popover, or to close it when <see cref="IsOpen"/> is true (the owner
    /// toggles).
    /// </summary>
    public static readonly RoutedEvent<PromptPillOpenRequestedEventArgs> OpenRequestedEvent =
        RoutedEvent.Register<PromptHeaderPill, PromptPillOpenRequestedEventArgs>(nameof(OpenRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="OpenRequestedEvent"/>
    public event EventHandler<PromptPillOpenRequestedEventArgs>? OpenRequested
    {
        add => AddHandler(OpenRequestedEvent, value);
        remove => RemoveHandler(OpenRequestedEvent, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            if (ControlAutomationPeer.FromElement(this) is PillAutomationPeer peer)
            {
                peer.RaiseExpandCollapseChanged(open);
            }
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A pill disabled under the pointer gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    /// <summary>Asks the owner to open (or close) the popover; a disabled pill asks nothing.</summary>
    internal void RequestOpen(ModelMenuOpenFocus focus)
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        RaiseEvent(new PromptPillOpenRequestedEventArgs(OpenRequestedEvent, this, focus));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        // Button's own press handling is skipped: it would show a pressed state and click on release.
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if ((e.KeyModifiers & KeyModifiers.Control) == 0)
            {
                e.Handled = true;
                RequestOpen(ModelMenuOpenFocus.Surface);
            }
            return;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Shortcuts with Ctrl, Alt or the platform key pass through (Mod+Enter runs the prompt).
        if (!e.Handled && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0 &&
            e.Key is Key.Enter or Key.Space or Key.Down)
        {
            e.Handled = true;
            RequestOpen(ModelMenuOpenFocus.Selected);
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        // Space already asked on its way down; Button would click again on the way up.
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }
        base.OnKeyUp(e);
    }

    /// <summary>The pill never clicks: <see cref="OpenRequested"/> replaces <c>Click</c> and <c>Command</c>.</summary>
    protected override void OnClick()
    {
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
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

    protected override AutomationPeer OnCreateAutomationPeer() => new PillAutomationPeer(this);

    /// <summary>
    /// A button that expands a dialog (<c>aria-haspopup="dialog"</c>, <c>aria-expanded</c>): its name carries the state the
    /// pill shows, so its text layers are not exposed. Invoke, Expand and Collapse ask the owner as a key does.
    /// </summary>
    private sealed class PillAutomationPeer(PromptHeaderPill owner) : ButtonAutomationPeer(owner), IInvokeProvider, IExpandCollapseProvider
    {
        private PromptHeaderPill Pill => (PromptHeaderPill)Owner;

        void IInvokeProvider.Invoke()
        {
            EnsureEnabled();
            Pill.RequestOpen(ModelMenuOpenFocus.Selected);
        }

        public ExpandCollapseState ExpandCollapseState => Pill.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        /// <summary>The popover is a dialog, not a menu.</summary>
        public bool ShowsMenu => false;

        public void Expand()
        {
            EnsureEnabled();
            if (!Pill.IsOpen)
            {
                Pill.RequestOpen(ModelMenuOpenFocus.Selected);
            }
        }

        public void Collapse()
        {
            EnsureEnabled();
            if (Pill.IsOpen)
            {
                Pill.RequestOpen(ModelMenuOpenFocus.Selected);
            }
        }

        public void RaiseExpandCollapseChanged(bool open) =>
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>
/// One text layer of a header pill (a new layer per change, so the pill can crossfade the old one out): 14px on a 19.6 line,
/// tracked −0.01em, one line. <see cref="IsSecondary"/> is the regular, secondary-colored word (the model's effort);
/// otherwise medium in the text color (the version label, the model's name).
/// </summary>
[PseudoClasses(SecondaryPseudoClass)]
internal sealed class PromptPillText : TextBlock
{
    public const string SecondaryPseudoClass = ":secondary";

    public static readonly StyledProperty<bool> IsSecondaryProperty =
        AvaloniaProperty.Register<PromptPillText, bool>(nameof(IsSecondary));

    static PromptPillText()
    {
        FocusableProperty.OverrideDefaultValue<PromptPillText>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<PromptPillText>(false);
    }

    public bool IsSecondary { get => GetValue(IsSecondaryProperty); set => SetValue(IsSecondaryProperty, value); }

    protected override Type StyleKeyOverride => typeof(PromptPillText);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSecondaryProperty)
        {
            PseudoClasses.Set(SecondaryPseudoClass, change.GetNewValue<bool>());
        }
    }
}
