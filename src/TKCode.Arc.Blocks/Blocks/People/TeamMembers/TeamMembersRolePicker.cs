using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The two looks of <see cref="TeamMembersRolePicker"/>.</summary>
internal enum TeamMembersPickerLook
{
    /// <summary>A bordered 36px field (Invite as).</summary>
    Field,

    /// <summary>A borderless 32px trigger that tints on hover (a member's role).</summary>
    Ghost,
}

/// <summary>
/// A role select: the trigger shows the chosen role's name (a text morph when it changes) and a chevron that turns over
/// while the menu is open. The menu (6 below, aligned to the trigger's right edge) lists every role with its description
/// and a check on the chosen one. Keyboard as a select: Enter, Space, Up or Down opens it with the chosen role focused;
/// arrows, Home and End move; Enter or Space chooses; Escape closes; focus returns to the trigger. The owner decides what
/// a choice does (<see cref="Chosen"/>) and writes <see cref="SelectedKey"/>.
/// </summary>
[TemplatePart(TextPart, typeof(TextMorph))]
[PseudoClasses(FieldPseudoClass, GhostPseudoClass, OpenPseudoClass)]
internal sealed class TeamMembersRolePicker : TemplatedControl
{
    public const string TextPart = "PART_Text";
    public const string FieldPseudoClass = ":field";
    public const string GhostPseudoClass = ":ghost";
    public const string OpenPseudoClass = ":open";

    public static readonly StyledProperty<IReadOnlyList<TeamMembersRoleOption>?> OptionsProperty =
        AvaloniaProperty.Register<TeamMembersRolePicker, IReadOnlyList<TeamMembersRoleOption>?>(nameof(Options));

    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<TeamMembersRolePicker, string?>(nameof(SelectedKey));

    public static readonly StyledProperty<TeamMembersPickerLook> LookProperty =
        AvaloniaProperty.Register<TeamMembersRolePicker, TeamMembersPickerLook>(nameof(Look));

    public static readonly DirectProperty<TeamMembersRolePicker, string> DisplayTextProperty =
        AvaloniaProperty.RegisterDirect<TeamMembersRolePicker, string>(nameof(DisplayText), o => o.DisplayText);

    public static readonly StyledProperty<double> MenuOffsetProperty =
        AvaloniaProperty.Register<TeamMembersRolePicker, double>(nameof(MenuOffset), 6);

    private readonly List<TeamMembersRoleMenuItem> _items = new();
    private string _displayText = string.Empty;
    private ArcPopup? _popup;
    private TeamMembersRoleMenu? _menu;
    private TeamMembersRoleMenuItem? _highlighted;
    private TopLevel? _hooked;

    static TeamMembersRolePicker()
    {
        FocusableProperty.OverrideDefaultValue<TeamMembersRolePicker>(true);
    }

    public TeamMembersRolePicker()
    {
        PseudoClasses.Set(FieldPseudoClass, true);
    }

    public IReadOnlyList<TeamMembersRoleOption>? Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }

    public string? SelectedKey { get => GetValue(SelectedKeyProperty); set => SetValue(SelectedKeyProperty, value); }

    public TeamMembersPickerLook Look { get => GetValue(LookProperty); set => SetValue(LookProperty, value); }

    public double MenuOffset { get => GetValue(MenuOffsetProperty); set => SetValue(MenuOffsetProperty, value); }

    /// <summary>The chosen role's name (or its key when it is not among the options).</summary>
    public string DisplayText { get => _displayText; private set => SetAndRaise(DisplayTextProperty, ref _displayText, value); }

    /// <summary>True while the menu is open.</summary>
    public bool IsMenuOpen { get; private set; }

    /// <summary>The menu's items while it is open (tests).</summary>
    internal IReadOnlyList<TeamMembersRoleMenuItem> MenuItems => _items;

    /// <summary>The highlighted menu item (tests).</summary>
    internal TeamMembersRoleMenuItem? Highlighted => _highlighted;

    /// <summary>The popup (null until first opened).</summary>
    internal ArcPopup? Popup => _popup;

    /// <summary>Raised when a role is chosen in the menu (also when it is the current one); the argument is its key.</summary>
    public event Action<TeamMembersRolePicker, string?>? Chosen;

    protected override AutomationPeer OnCreateAutomationPeer() => new TeamMembersRolePickerPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OptionsProperty || change.Property == SelectedKeyProperty)
        {
            UpdateText();
            if (IsMenuOpen)
            {
                BuildItems();
            }
        }
        else if (change.Property == LookProperty)
        {
            PseudoClasses.Set(FieldPseudoClass, Look == TeamMembersPickerLook.Field);
            PseudoClasses.Set(GhostPseudoClass, Look == TeamMembersPickerLook.Ghost);
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            CloseMenu(returnFocus: false);
        }
    }

    private void UpdateText()
    {
        var key = SelectedKey;
        var option = Options?.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.Ordinal));
        DisplayText = option?.DisplayLabel ?? key ?? string.Empty;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        Focus(NavigationMethod.Pointer);
        if (IsMenuOpen)
        {
            CloseMenu(returnFocus: true, NavigationMethod.Pointer);
        }
        else
        {
            OpenMenu(keyboard: false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsMenuOpen)
        {
            return;
        }
        if (e.Key is Key.Enter or Key.Space or Key.Down or Key.Up)
        {
            e.Handled = true;
            OpenMenu(keyboard: true);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CloseMenu(returnFocus: false);
    }

    #region Menu

    /// <summary>Opens the menu with the chosen role highlighted and focused.</summary>
    public void OpenMenu(bool keyboard)
    {
        if (IsMenuOpen || !IsEffectivelyEnabled || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }
        EnsureMenu();
        BuildItems();
        IsMenuOpen = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        _popup!.IsOpen = true;
        Hook(topLevel);
        var method = keyboard ? NavigationMethod.Directional : NavigationMethod.Pointer;
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsMenuOpen)
            {
                return;
            }
            var start = _items.FirstOrDefault(i => i.IsChecked) ?? _items.FirstOrDefault();
            if (start is not null)
            {
                MoveTo(start, method);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Closes the menu; with <paramref name="returnFocus"/> (or focus inside the menu) the trigger takes focus back.</summary>
    public void CloseMenu(bool returnFocus = true, NavigationMethod method = NavigationMethod.Tab)
    {
        if (!IsMenuOpen)
        {
            return;
        }
        IsMenuOpen = false;
        PseudoClasses.Set(OpenPseudoClass, false);
        Unhook();
        var inside = _menu?.IsKeyboardFocusWithin == true;
        SetHighlight(null);
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
        _menu = new TeamMembersRoleMenu { Owner = this };
        _popup = new ArcPopup
        {
            Child = _menu,
            Placement = PlacementMode.BottomEdgeAlignedRight,
            MarginToAnchor = MenuOffset,
            EnterOffset = 6,
            EnterScale = 0.97,
            IsLightDismissEnabled = false,
            WindowManagerAddShadowHint = false,
            PlacementTarget = this,
            OverlayInputPassThroughElement = this
        };
        LogicalChildren.Add(_popup);
    }

    private void BuildItems()
    {
        if (_menu is null)
        {
            return;
        }
        var focused = _highlighted?.IsFocused == true ? _highlighted.Option : null;
        _menu.List.Children.Clear();
        _items.Clear();
        _highlighted = null;
        foreach (var option in Options ?? [])
        {
            var item = new TeamMembersRoleMenuItem(option, this)
            {
                IsChecked = string.Equals(option.Key, SelectedKey, StringComparison.Ordinal)
            };
            _items.Add(item);
            _menu.List.Children.Add(item);
        }
        if (focused is not null && _items.FirstOrDefault(i => ReferenceEquals(i.Option, focused)) is { } again)
        {
            MoveTo(again, NavigationMethod.Directional);
        }
    }

    private void SetHighlight(TeamMembersRoleMenuItem? item)
    {
        if (ReferenceEquals(item, _highlighted))
        {
            return;
        }
        _highlighted?.SetHighlighted(false);
        _highlighted = item;
        item?.SetHighlighted(true);
    }

    private void MoveTo(TeamMembersRoleMenuItem item, NavigationMethod method)
    {
        SetHighlight(item);
        item.Focus(method);
    }

    internal void OnMenuKeyDown(KeyEventArgs e)
    {
        if (e.Handled || !IsMenuOpen)
        {
            return;
        }
        var count = _items.Count;
        var current = _highlighted is null ? -1 : _items.IndexOf(_highlighted);
        int? next = e.Key switch
        {
            Key.Down => count == 0 ? null : Math.Min(count - 1, current + 1),
            Key.Up => count == 0 ? null : current < 0 ? count - 1 : Math.Max(0, current - 1),
            Key.Home => count == 0 ? null : 0,
            Key.End => count == 0 ? null : count - 1,
            _ => null
        };
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
            case Key.Home:
            case Key.End:
                e.Handled = true;
                if (next is { } index)
                {
                    MoveTo(_items[index], NavigationMethod.Directional);
                }
                return;
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                if (_highlighted is { } item)
                {
                    Choose(item, NavigationMethod.Tab);
                }
                return;
            case Key.Escape:
                e.Handled = true;
                CloseMenu(returnFocus: true, NavigationMethod.Tab);
                return;
            case Key.Tab:
                // A select keeps focus in its list.
                e.Handled = true;
                return;
        }
    }

    internal void OnItemPointerEntered(TeamMembersRoleMenuItem item, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch || !IsMenuOpen)
        {
            return;
        }
        MoveTo(item, NavigationMethod.Pointer);
    }

    internal void OnItemInvoked(TeamMembersRoleMenuItem item) => Choose(item, NavigationMethod.Pointer);

    private void Choose(TeamMembersRoleMenuItem item, NavigationMethod method)
    {
        if (!IsMenuOpen)
        {
            return;
        }
        CloseMenu(returnFocus: true, method);
        Chosen?.Invoke(this, item.Option.Key);
    }

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

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsMenuOpen || e.Source is not Visual source)
        {
            return;
        }
        var inMenu = _menu is { } menu && (ReferenceEquals(menu, source) || menu.IsVisualAncestorOf(source));
        var onTrigger = ReferenceEquals(source, this) || this.IsVisualAncestorOf(source);
        if (!inMenu && !onTrigger)
        {
            CloseMenu(returnFocus: false, NavigationMethod.Pointer);
        }
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && IsMenuOpen)
        {
            e.Handled = true;
            CloseMenu(returnFocus: true, NavigationMethod.Tab);
        }
    }

    #endregion
}

/// <summary>Reports the role picker as a combo box whose value is the chosen role.</summary>
internal sealed class TeamMembersRolePickerPeer(TeamMembersRolePicker owner) : ControlAutomationPeer(owner), IExpandCollapseProvider
{
    private TeamMembersRolePicker Picker => (TeamMembersRolePicker)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Picker.DisplayText;

    protected override string? GetHelpTextCore() => Picker.DisplayText;

    public ExpandCollapseState ExpandCollapseState => Picker.IsMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => true;

    public void Expand() => Picker.OpenMenu(keyboard: true);

    public void Collapse() => Picker.CloseMenu();
}

/// <summary>The role menu's surface: padding 6, 1px Border, radius 20, Surface, floating shadow (from the popup).</summary>
internal sealed class TeamMembersRoleMenu : Border
{
    public TeamMembersRoleMenu()
    {
        List = new StackPanel();
        Child = List;
        Focusable = true;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    public TeamMembersRolePicker? Owner { get; set; }

    public StackPanel List { get; }

    protected override Type StyleKeyOverride => typeof(TeamMembersRoleMenu);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        Owner?.OnMenuKeyDown(e);
        base.OnKeyDown(e);
    }
}

/// <summary>One role in the menu: the name (13, medium), the description (12, TextMuted) and a check on the chosen role.</summary>
[PseudoClasses(HighlightedPseudoClass, CheckedPseudoClass)]
internal sealed class TeamMembersRoleMenuItem : TemplatedControl
{
    public const string HighlightedPseudoClass = ":highlighted";
    public const string CheckedPseudoClass = ":checked";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TeamMembersRoleMenuItem, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<TeamMembersRoleMenuItem, string?>(nameof(Description));

    public static readonly StyledProperty<bool> HasDescriptionProperty =
        AvaloniaProperty.Register<TeamMembersRoleMenuItem, bool>(nameof(HasDescription));

    private readonly TeamMembersRolePicker _owner;
    private bool _isChecked;

    static TeamMembersRoleMenuItem()
    {
        FocusableProperty.OverrideDefaultValue<TeamMembersRoleMenuItem>(true);
    }

    public TeamMembersRoleMenuItem(TeamMembersRoleOption option, TeamMembersRolePicker owner)
    {
        Option = option;
        _owner = owner;
        Label = option.DisplayLabel;
        Description = option.Description;
        HasDescription = !string.IsNullOrEmpty(option.Description);
        AutomationProperties.SetName(this, option.DisplayLabel);
        AutomationProperties.SetHelpText(this, option.Description);
    }

    public TeamMembersRoleOption Option { get; }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public bool HasDescription { get => GetValue(HasDescriptionProperty); set => SetValue(HasDescriptionProperty, value); }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            PseudoClasses.Set(CheckedPseudoClass, value);
        }
    }

    public bool IsHighlighted { get; private set; }

    public void SetHighlighted(bool value)
    {
        IsHighlighted = value;
        PseudoClasses.Set(HighlightedPseudoClass, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TeamMembersRoleMenuItemPeer(this);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _owner.OnItemPointerEntered(this, e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            e.Handled = true;
            _owner.OnItemInvoked(this);
        }
    }
}

/// <summary>Reports a role menu item as a selectable list item.</summary>
internal sealed class TeamMembersRoleMenuItemPeer(TeamMembersRoleMenuItem owner) : ControlAutomationPeer(owner), ISelectionItemProvider
{
    private TeamMembersRoleMenuItem Item => (TeamMembersRoleMenuItem)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    public bool IsSelected => Item.IsChecked;

    public ISelectionProvider? SelectionContainer => null;

    public void AddToSelection()
    {
    }

    public void RemoveFromSelection()
    {
    }

    public void Select()
    {
    }
}
