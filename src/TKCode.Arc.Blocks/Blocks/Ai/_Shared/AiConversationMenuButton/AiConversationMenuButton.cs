using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Raised when a conversation is picked in the title menu.</summary>
internal sealed class AiConversationChosenEventArgs : RoutedEventArgs
{
    public AiConversationChosenEventArgs(RoutedEvent routedEvent, object? source, AiChatConversation conversation,
                                         AiChatConversation? previous)
        : base(routedEvent, source)
    {
        Conversation = conversation;
        Previous = previous;
    }

    /// <summary>The conversation picked (now <see cref="AiConversationMenuButton.Current"/>).</summary>
    public AiChatConversation Conversation { get; }

    /// <summary>The conversation that was current before.</summary>
    public AiChatConversation? Previous { get; }

    /// <summary>False when the current conversation was picked again.</summary>
    public bool IsChange => !ReferenceEquals(Conversation, Previous);
}

/// <summary>
/// The chat's title pill and its conversation menu. The pill is 34px high with the pill radius and padding 0 8 0 10: the
/// title (14 / 19.6, medium, ellipsis when its slot is too narrow: the title truncates before anything else in the header)
/// and a ChevronDown 14 in the muted text color 6px after it, turning over while the menu is open. Hover and the open menu
/// tint it SurfaceMuted. A new title crossfades (old out 100ms, new in 160ms rising 2px) and the pill's width follows the
/// new text on the quick spring, capped by the room it has. Reduced motion crossfades in 120ms and jumps the width.
/// <para>
/// A primary press opens the menu (on press, as Arc does) with nothing highlighted; Enter, Space or ↓ open it with the
/// first row highlighted. The menu (260 wide, left-aligned 6px below the pill, at most 420 high, scrolling inside) lists
/// "New chat", a separator, then <see cref="Conversations"/> grouped by <see cref="AiChatConversation.Group"/> under muted
/// headings, the <see cref="Current"/> one with a check. Keys: ↑ ↓ (wrapping), Home, End, letters jump to the next row
/// starting with them, Enter or Space choose, Escape closes; focus goes back to the pill. Choosing raises
/// <see cref="NewChatRequested"/> or sets <see cref="Current"/> and raises <see cref="ConversationChosen"/>, after the menu
/// closed and the pill has focus again (a handler may move focus on, e.g. to the message box). It announces nothing.
/// </para>
/// </summary>
[TemplatePart(TextPart, typeof(ArcContentSwap))]
[TemplatePart(TextSizePart, typeof(SpringSizeDecorator))]
[PseudoClasses(OpenPseudoClass, HoverPseudoClass)]
internal partial class AiConversationMenuButton : AvaloniaButton
{
    public const string TextPart = "PART_Text";
    public const string TextSizePart = "PART_TextSize";
    public const string OpenPseudoClass = ":open";
    public const string HoverPseudoClass = ":hover";

    #region Properties

    /// <summary>The conversations of the menu (the current one included). Collection changes are followed while the menu is open.</summary>
    public static readonly StyledProperty<IEnumerable<AiChatConversation>?> ConversationsProperty =
        AvaloniaProperty.Register<AiConversationMenuButton, IEnumerable<AiChatConversation>?>(nameof(Conversations));

    /// <summary>The conversation shown (two-way); the menu checks it and a choice sets it.</summary>
    public static readonly StyledProperty<AiChatConversation?> CurrentProperty =
        AvaloniaProperty.Register<AiConversationMenuButton, AiChatConversation?>(nameof(Current), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The pill's text, computed by the chat. Null or empty falls back to the current conversation's title (see
    /// <see cref="AiConversationTitle"/>), or to <see cref="NewChatText"/> when there is none.
    /// </summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiConversationMenuButton, string?>(nameof(Title));

    /// <summary>The first row of the menu ("New chat").</summary>
    public static readonly StyledProperty<string?> NewChatTextProperty =
        AvaloniaProperty.Register<AiConversationMenuButton, string?>(nameof(NewChatText));

    /// <summary>The row text of a conversation without a title or a message ("Untitled chat").</summary>
    public static readonly StyledProperty<string?> UntitledTextProperty =
        AvaloniaProperty.Register<AiConversationMenuButton, string?>(nameof(UntitledText));

    /// <summary>Makes the pill's accessible name from the title ("Chats, current: {0}"); null names it by the title.</summary>
    public static readonly StyledProperty<Func<string, string>?> NameFormatProperty =
        AvaloniaProperty.Register<AiConversationMenuButton, Func<string, string>?>(nameof(NameFormat));

    /// <summary>Whether the menu is open (read-only).</summary>
    public static readonly DirectProperty<AiConversationMenuButton, bool> IsMenuOpenProperty =
        AvaloniaProperty.RegisterDirect<AiConversationMenuButton, bool>(nameof(IsMenuOpen), o => o.IsMenuOpen);

    /// <summary>Raised when "New chat" is chosen in the menu.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> NewChatRequestedEvent =
        RoutedEvent.Register<AiConversationMenuButton, RoutedEventArgs>(nameof(NewChatRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when a conversation is chosen in the menu (also the current one: see <see cref="AiConversationChosenEventArgs.IsChange"/>).</summary>
    public static readonly RoutedEvent<AiConversationChosenEventArgs> ConversationChosenEvent =
        RoutedEvent.Register<AiConversationMenuButton, AiConversationChosenEventArgs>(nameof(ConversationChosen), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ConversationsProperty"/>
    public IEnumerable<AiChatConversation>? Conversations { get => GetValue(ConversationsProperty); set => SetValue(ConversationsProperty, value); }

    /// <inheritdoc cref="CurrentProperty"/>
    public AiChatConversation? Current { get => GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="NewChatTextProperty"/>
    public string? NewChatText { get => GetValue(NewChatTextProperty); set => SetValue(NewChatTextProperty, value); }

    /// <inheritdoc cref="UntitledTextProperty"/>
    public string? UntitledText { get => GetValue(UntitledTextProperty); set => SetValue(UntitledTextProperty, value); }

    /// <inheritdoc cref="NameFormatProperty"/>
    public Func<string, string>? NameFormat { get => GetValue(NameFormatProperty); set => SetValue(NameFormatProperty, value); }

    /// <inheritdoc cref="IsMenuOpenProperty"/>
    public bool IsMenuOpen { get => _isMenuOpen; private set => SetAndRaise(IsMenuOpenProperty, ref _isMenuOpen, value); }

    /// <inheritdoc cref="NewChatRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? NewChatRequested
    {
        add => AddHandler(NewChatRequestedEvent, value);
        remove => RemoveHandler(NewChatRequestedEvent, value);
    }

    /// <inheritdoc cref="ConversationChosenEvent"/>
    public event EventHandler<AiConversationChosenEventArgs>? ConversationChosen
    {
        add => AddHandler(ConversationChosenEvent, value);
        remove => RemoveHandler(ConversationChosenEvent, value);
    }

    #endregion

    private ArcContentSwap? _textSwap;
    private SpringSizeDecorator? _textSize;
    private AiConversationPillText? _text;
    private bool _isMenuOpen;

    protected override Type StyleKeyOverride => typeof(AiConversationMenuButton);

    /// <summary>The text layer on screen.</summary>
    internal AiConversationPillText? CurrentText => _text;

    /// <summary>The width spring of the text.</summary>
    internal SpringSizeDecorator? TextSize => _textSize;

    /// <summary>The text the pill shows.</summary>
    internal string DisplayTitle
    {
        get
        {
            if (!string.IsNullOrEmpty(Title))
            {
                return Title!;
            }
            return Current is { } current ? AiConversationTitle.Of(current, UntitledText) : NewChatText ?? string.Empty;
        }
    }

    #region Text

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _textSwap = e.NameScope.Find<ArcContentSwap>(TextPart);
        _textSize = e.NameScope.Find<SpringSizeDecorator>(TextSizePart);
        // A new part starts with the current text (its first content never animates).
        _text = null;
        RefreshText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == NewChatTextProperty || change.Property == UntitledTextProperty ||
            change.Property == NameFormatProperty)
        {
            RefreshText();
            OnMenuDataChanged();
        }
        else if (change.Property == CurrentProperty)
        {
            RefreshText();
            OnMenuDataChanged();
        }
        else if (change.Property == ConversationsProperty)
        {
            OnConversationsChanged();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            PseudoClasses.Set(HoverPseudoClass, false);
            CloseMenu(returnFocus: false, NavigationMethod.Pointer);
        }
    }

    /// <summary>A new layer (crossfaded by the swap) only when the words change; the name always follows.</summary>
    private void RefreshText()
    {
        var title = DisplayTitle;
        AutomationProperties.SetName(this, NameFormat is { } format ? format(title) : title);
        if (_textSwap is null || (_text is not null && _text.Text == title))
        {
            return;
        }
        _text = new AiConversationPillText { Text = title };
        _textSwap.Content = _text;
    }

    #endregion

    #region Input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        // Input in the menu reaches the pill through the popup's logical parent: only the pill's own presses count.
        if (e.Source is Visual source && !IsInPill(source))
        {
            return;
        }
        // Arc opens on press, not release, but not with Ctrl held (a context click on macOS). Button's own press handling
        // is skipped: it would show the pressed state and click on release.
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if ((e.KeyModifiers & KeyModifiers.Control) == 0)
            {
                e.Handled = true;
                ToggleMenu(keyboard: false);
            }
            return;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Source is Visual source && !IsInPill(source))
        {
            return;
        }
        if (!e.Handled && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0 &&
            e.Key is Key.Enter or Key.Space or Key.Down)
        {
            e.Handled = true;
            ToggleMenu(keyboard: true);
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Source is Visual source && !IsInPill(source))
        {
            return;
        }
        // Space already opened on its way down; Button would click again on the way up.
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }
        base.OnKeyUp(e);
    }

    /// <summary>The pill never clicks: it opens its menu instead.</summary>
    protected override void OnClick()
    {
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    private void ToggleMenu(bool keyboard)
    {
        if (IsMenuOpen)
        {
            CloseMenu(returnFocus: true, keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        else
        {
            OpenMenu(keyboard);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new MenuButtonAutomationPeer(this);

    /// <summary>A button that expands a menu (<c>aria-haspopup="menu"</c>, <c>aria-expanded</c>); its name carries the title.</summary>
    private sealed class MenuButtonAutomationPeer(AiConversationMenuButton owner) : ButtonAutomationPeer(owner), IInvokeProvider, IExpandCollapseProvider
    {
        private AiConversationMenuButton Pill => (AiConversationMenuButton)Owner;

        void IInvokeProvider.Invoke()
        {
            EnsureEnabled();
            Pill.ToggleMenu(keyboard: true);
        }

        public ExpandCollapseState ExpandCollapseState => Pill.IsMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand()
        {
            EnsureEnabled();
            if (!Pill.IsMenuOpen)
            {
                Pill.OpenMenu(keyboard: true);
            }
        }

        public void Collapse()
        {
            EnsureEnabled();
            Pill.CloseMenu(returnFocus: true, NavigationMethod.Tab);
        }

        public void RaiseExpandCollapseChanged(bool open) =>
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }

    private void RaiseExpandCollapseChanged(bool open)
    {
        if (ControlAutomationPeer.FromElement(this) is MenuButtonAutomationPeer peer)
        {
            peer.RaiseExpandCollapseChanged(open);
        }
    }
}

/// <summary>One text layer of the title pill (a new one per title, so the old one can fade out): one line, ellipsis.</summary>
internal sealed class AiConversationPillText : TextBlock
{
    static AiConversationPillText()
    {
        FocusableProperty.OverrideDefaultValue<AiConversationPillText>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiConversationPillText>(false);
    }

    protected override Type StyleKeyOverride => typeof(AiConversationPillText);
}
