using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

internal partial class AiConversationMenuButton
{
    /// <summary>The menu sits this far below the pill.</summary>
    internal const double MenuGap = 6;

    /// <summary>Letters typed within this time make one typeahead prefix.</summary>
    internal static readonly TimeSpan TypeaheadWindow = TimeSpan.FromMilliseconds(500);

    private readonly List<AiConversationMenuRow> _rows = new();
    private readonly List<AiChatConversation> _observedItems = new();
    private ArcPopup? _popup;
    private AiConversationMenuPanel? _panel;
    private AiConversationMenuRow? _highlighted;
    private TopLevel? _hooked;
    private INotifyCollectionChanged? _observedConversations;
    private string _typeahead = string.Empty;
    private DateTime _lastTyped = DateTime.MinValue;
    private bool _keyboard;

    /// <summary>The clock of typeahead (tests replace it).</summary>
    internal Func<DateTime> Now { get; set; } = static () => DateTime.UtcNow;

    /// <summary>The menu surface (null until the first open).</summary>
    internal AiConversationMenuPanel? MenuPanel => _panel;

    /// <summary>The popup of the menu (null until the first open).</summary>
    internal ArcPopup? MenuPopup => _popup;

    /// <summary>The rows of the menu, in order ("New chat" first).</summary>
    internal IReadOnlyList<AiConversationMenuRow> Rows => _rows;

    /// <summary>The highlighted row (pointer or keyboard position), or null.</summary>
    internal AiConversationMenuRow? HighlightedRow => _highlighted;

    #region Open and close

    /// <summary>
    /// Opens the menu below the pill. With <paramref name="keyboard"/> the first row is highlighted and focused; otherwise
    /// nothing is highlighted and the menu surface takes focus.
    /// </summary>
    public void OpenMenu(bool keyboard = false)
    {
        if (IsMenuOpen || !IsEffectivelyEnabled || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }
        EnsureMenu();
        _keyboard = keyboard;
        _typeahead = string.Empty;
        BuildRows();
        ObserveData(true);
        IsMenuOpen = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        RaiseExpandCollapseChanged(true);
        _popup!.IsOpen = true;
        Hook(topLevel);
        Dispatcher.UIThread.Post(() => ApplyOpenFocus(keyboard), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Closes the menu. With <paramref name="returnFocus"/> (and always when focus is in the menu) the pill takes focus
    /// back, by <paramref name="method"/> (Tab shows its keyboard ring).
    /// </summary>
    public void CloseMenu(bool returnFocus = true, NavigationMethod method = NavigationMethod.Tab)
    {
        if (!IsMenuOpen)
        {
            return;
        }
        IsMenuOpen = false;
        PseudoClasses.Set(OpenPseudoClass, false);
        RaiseExpandCollapseChanged(false);
        Unhook();
        ObserveData(false);
        var inside = _panel?.IsKeyboardFocusWithin == true;
        SetHighlight(null);
        // Focus goes back before the surface hides: hiding detaches the focused row and the focus manager would put focus
        // back without a method (no keyboard ring), after which asking again does nothing.
        if ((returnFocus || inside) && TopLevel.GetTopLevel(this) is not null)
        {
            Focus(method);
        }
        if (_popup is not null)
        {
            _popup.IsOpen = false;
        }
    }

    private void EnsureMenu()
    {
        if (_popup is not null)
        {
            return;
        }
        _panel = new AiConversationMenuPanel { Owner = this };
        _popup = new ArcPopup
        {
            Child = _panel,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            MarginToAnchor = MenuGap,
            EnterOffset = 4,
            EnterScale = 0.98,
            IsLightDismissEnabled = false,
            WindowManagerAddShadowHint = false,
            PlacementTarget = this,
            OverlayInputPassThroughElement = this
        };
        LogicalChildren.Add(_popup);
    }

    private void ApplyOpenFocus(bool keyboard)
    {
        if (!IsMenuOpen || _panel is null)
        {
            return;
        }
        if (keyboard && _rows.Count > 0)
        {
            MoveTo(_rows[0]);
            return;
        }
        _panel.Focus(NavigationMethod.Pointer);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
        CloseMenu(returnFocus: false, NavigationMethod.Pointer);
    }

    #endregion

    #region Rows

    /// <summary>
    /// "New chat", a separator, then the conversations: ungrouped ones first, then each group (in the order its first
    /// conversation appears) under its heading. A rebuild keeps the highlighted row (and its focus).
    /// </summary>
    private void BuildRows()
    {
        if (_panel is null)
        {
            return;
        }
        var previous = _highlighted;
        var hadFocus = previous?.IsFocused == true;
        var list = _panel.List;
        list.Children.Clear();
        _rows.Clear();
        _highlighted = null;

        var newChat = new AiConversationMenuRow(AiConversationMenuRowKind.NewChat, null)
        {
            Text = NewChatText ?? string.Empty,
            Icon = ArcIcons.SquarePen
        };
        Add(newChat);

        var ungrouped = new List<AiChatConversation>();
        var groups = new List<(string Heading, List<AiChatConversation> Items)>();
        foreach (var conversation in DistinctConversations())
        {
            var heading = conversation.Group;
            if (string.IsNullOrEmpty(heading))
            {
                ungrouped.Add(conversation);
                continue;
            }
            var index = groups.FindIndex(g => g.Heading == heading);
            if (index < 0)
            {
                groups.Add((heading, new List<AiChatConversation> { conversation }));
            }
            else
            {
                groups[index].Items.Add(conversation);
            }
        }
        if (ungrouped.Count > 0 || groups.Count > 0)
        {
            list.Children.Add(new AiConversationMenuSeparator());
        }
        foreach (var conversation in ungrouped)
        {
            Add(ConversationRow(conversation));
        }
        foreach (var (heading, items) in groups)
        {
            list.Children.Add(new AiConversationMenuHeading { Text = heading });
            foreach (var conversation in items)
            {
                Add(ConversationRow(conversation));
            }
        }

        if (previous is not null && _rows.FirstOrDefault(r => r.Kind == previous.Kind && ReferenceEquals(r.Conversation, previous.Conversation)) is { } again)
        {
            SetHighlight(again);
            if (hadFocus)
            {
                again.Focus(NavigationMethod.Directional);
            }
        }

        void Add(AiConversationMenuRow row)
        {
            list.Children.Add(row);
            _rows.Add(row);
        }
    }

    private AiConversationMenuRow ConversationRow(AiChatConversation conversation) =>
        new(AiConversationMenuRowKind.Conversation, conversation)
        {
            Text = AiConversationTitle.Of(conversation, UntitledText),
            IsChecked = ReferenceEquals(conversation, Current)
        };

    private List<AiChatConversation> DistinctConversations()
    {
        var result = new List<AiChatConversation>();
        if (Conversations is { } source)
        {
            foreach (var conversation in source)
            {
                if (conversation is not null && !result.Contains(conversation))
                {
                    result.Add(conversation);
                }
            }
        }
        return result;
    }

    private void SetHighlight(AiConversationMenuRow? row)
    {
        if (ReferenceEquals(row, _highlighted))
        {
            return;
        }
        _highlighted?.SetHighlighted(false);
        _highlighted = row;
        row?.SetHighlighted(true);
    }

    private void MoveTo(AiConversationMenuRow row)
    {
        _keyboard = true;
        SetHighlight(row);
        row.Focus(NavigationMethod.Directional);
        row.BringIntoView();
    }

    #endregion

    #region Menu input

    /// <summary>Keys from the menu (the surface or a row has focus).</summary>
    internal void OnMenuKeyDown(KeyEventArgs e)
    {
        if (e.Handled || !IsMenuOpen)
        {
            return;
        }
        var current = _highlighted is null ? -1 : _rows.IndexOf(_highlighted);
        var count = _rows.Count;
        int? next = null;
        switch (e.Key)
        {
            case Key.Down:
                next = count == 0 ? null : (current + 1) % count;
                break;
            case Key.Up:
                next = count == 0 ? null : current < 0 ? count - 1 : (current - 1 + count) % count;
                break;
            case Key.Home:
                next = count == 0 ? null : 0;
                break;
            case Key.End:
                next = count == 0 ? null : count - 1;
                break;
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                if (_highlighted is { } row)
                {
                    Choose(row, NavigationMethod.Directional);
                }
                return;
            case Key.Escape:
                e.Handled = true;
                CloseMenu(returnFocus: true, NavigationMethod.Tab);
                return;
            case Key.Tab:
                // Radix keeps focus in the menu: Tab does nothing.
                e.Handled = true;
                return;
        }
        if (e.Key is Key.Down or Key.Up or Key.Home or Key.End)
        {
            e.Handled = true;
            if (next is { } index)
            {
                MoveTo(_rows[index]);
            }
            return;
        }
        if (e.KeySymbol is { Length: 1 } symbol && !char.IsControl(symbol[0]) && symbol[0] != ' ' &&
            (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt)) == 0)
        {
            e.Handled = true;
            if (TypeAhead(current, symbol) is { } match)
            {
                MoveTo(_rows[match]);
            }
        }
    }

    /// <summary>The next row (after the highlighted one, wrapping) whose text starts with the letters typed so far.</summary>
    private int? TypeAhead(int current, string symbol)
    {
        var now = Now();
        _typeahead = now - _lastTyped > TypeaheadWindow ? symbol : _typeahead + symbol;
        _lastTyped = now;
        var count = _rows.Count;
        // A prefix that is still being typed may stay on the current row; a new single letter moves on.
        var start = _typeahead.Length > 1 ? 0 : 1;
        for (var step = start; step <= count; step++)
        {
            var index = ((current + step) % count + count) % count;
            if (_rows[index].Text?.StartsWith(_typeahead, StringComparison.CurrentCultureIgnoreCase) == true)
            {
                return index;
            }
        }
        return null;
    }

    internal void OnRowPointerEntered(AiConversationMenuRow row, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch || !IsMenuOpen)
        {
            return;
        }
        _keyboard = false;
        SetHighlight(row);
        row.Focus(NavigationMethod.Pointer);
    }

    internal void OnRowPointerExited(AiConversationMenuRow row, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch || !IsMenuOpen || !ReferenceEquals(row, _highlighted))
        {
            return;
        }
        // Radix: leaving an item gives focus back to the menu and removes the highlight.
        SetHighlight(null);
        _panel?.Focus(NavigationMethod.Pointer);
    }

    internal void OnRowInvoked(AiConversationMenuRow row, NavigationMethod method) => Choose(row, method);

    /// <summary>Closes the menu (focus back on the pill), then reports the choice.</summary>
    private void Choose(AiConversationMenuRow row, NavigationMethod method)
    {
        if (!IsMenuOpen)
        {
            return;
        }
        CloseMenu(returnFocus: true, method == NavigationMethod.Pointer ? NavigationMethod.Pointer : NavigationMethod.Tab);
        if (row.Kind == AiConversationMenuRowKind.NewChat)
        {
            RaiseEvent(new RoutedEventArgs(NewChatRequestedEvent, this));
            return;
        }
        if (row.Conversation is not { } conversation)
        {
            return;
        }
        var previous = Current;
        SetCurrentValue(CurrentProperty, conversation);
        RaiseEvent(new AiConversationChosenEventArgs(ConversationChosenEvent, this, conversation, previous));
    }

    #endregion

    #region Window and data hooks

    private void Hook(TopLevel topLevel)
    {
        Unhook();
        _hooked = topLevel;
        topLevel.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Bubble);
    }

    private void Unhook()
    {
        if (_hooked is not { } topLevel)
        {
            return;
        }
        topLevel.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        topLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _hooked = null;
    }

    private bool IsInMenu(Visual visual) => _panel is { } panel && (ReferenceEquals(panel, visual) || panel.IsVisualAncestorOf(visual));

    private bool IsInPill(Visual visual) => ReferenceEquals(visual, this) || this.IsVisualAncestorOf(visual);

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsMenuOpen || e.Source is not Visual source)
        {
            return;
        }
        if (IsInMenu(source))
        {
            _keyboard = false;
            return;
        }
        // A press on the pill passes through: the pill toggles the menu itself.
        if (IsInPill(source))
        {
            return;
        }
        CloseMenu(returnFocus: false, NavigationMethod.Pointer);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        // Keys from focus inside the menu are handled by its surface; this catches Escape with focus elsewhere.
        if (!e.Handled && e.Key == Key.Escape && IsMenuOpen)
        {
            e.Handled = true;
            CloseMenu(returnFocus: true, NavigationMethod.Tab);
        }
    }

    private void OnConversationsChanged()
    {
        if (IsMenuOpen)
        {
            ObserveData(true);
            BuildRows();
        }
    }

    private void OnMenuDataChanged()
    {
        if (IsMenuOpen)
        {
            BuildRows();
        }
    }

    // The conversations are app data that can outlive the pill: they are followed only while the menu is open.
    private void ObserveData(bool observe)
    {
        var next = observe ? Conversations as INotifyCollectionChanged : null;
        if (!ReferenceEquals(next, _observedConversations))
        {
            if (_observedConversations is not null)
            {
                _observedConversations.CollectionChanged -= OnConversationCollectionChanged;
            }
            _observedConversations = next;
            if (next is not null)
            {
                next.CollectionChanged += OnConversationCollectionChanged;
            }
        }
        foreach (var item in _observedItems)
        {
            item.PropertyChanged -= OnConversationPropertyChanged;
        }
        _observedItems.Clear();
        if (observe)
        {
            foreach (var conversation in DistinctConversations())
            {
                conversation.PropertyChanged += OnConversationPropertyChanged;
                _observedItems.Add(conversation);
            }
        }
    }

    private void OnConversationCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ObserveData(IsMenuOpen);
        OnMenuDataChanged();
    }

    private void OnConversationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiChatConversation.TitleProperty || e.Property == AiChatConversation.GroupProperty)
        {
            OnMenuDataChanged();
            if (ReferenceEquals(sender, Current))
            {
                RefreshText();
            }
        }
    }

    #endregion
}
