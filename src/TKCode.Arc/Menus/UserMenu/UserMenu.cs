using System.Collections;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AtomUI.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Presence status shown by the <see cref="UserMenu"/> dot and switch.</summary>
public enum UserMenuStatus
{
    Available,
    Busy,
    Away
}

/// <summary>Theme preference reported by the <see cref="UserMenu"/> switch (applying it is up to the app).</summary>
public enum UserMenuThemePreference
{
    Light,
    Dark,
    System
}

public class UserMenuThemePreferenceChangedEventArgs : RoutedEventArgs
{
    public UserMenuThemePreferenceChangedEventArgs(RoutedEvent routedEvent, UserMenuThemePreference themePreference)
        : base(routedEvent)
    {
        ThemePreference = themePreference;
    }

    public UserMenuThemePreference ThemePreference { get; }
}

public class UserMenuStatusChangedEventArgs : RoutedEventArgs
{
    public UserMenuStatusChangedEventArgs(RoutedEvent routedEvent, UserMenuStatus status)
        : base(routedEvent)
    {
        Status = status;
    }

    public UserMenuStatus Status { get; }
}

/// <summary>
/// The account menu behind a person's avatar in an app's top bar (Arc user menu): a compact identity header, a short
/// group of account destinations (<see cref="Items"/>, keep it to three or four), an inline light / dark / system
/// switch, an optional presence switch and sign out. Theme and status choices keep the menu open; everything else
/// closes it and returns focus to the avatar. When <see cref="SignOutAction"/> returns an unfinished task the row shows
/// a spinner and "Signing out", and the menu closes once it settles. Below a 640px wide window the menu opens as a
/// bottom sheet that follows a downward drag.
/// <para>
/// The panel scales out of the avatar on a spring that never overshoots, rows fade in 16ms apart, one highlight glides
/// between rows and the switch thumbs slide on Arc's CSS spring.
/// </para>
/// </summary>
[TemplatePart(TriggerPart, typeof(AvaloniaButton))]
[PseudoClasses(OpenPseudoClass, CompactPseudoClass, SigningOutPseudoClass)]
public class UserMenu : TemplatedControl
{
    public const string TriggerPart = "PART_Trigger";
    public const string OpenPseudoClass = ":open";
    public const string CompactPseudoClass = ":compact";
    public const string SigningOutPseudoClass = ":signing-out";

    /// <summary>Window width below which the menu becomes a bottom sheet (Arc: 640px).</summary>
    public const double CompactBreakpoint = 640;

    #region 公共属性定义

    /// <summary>Name shown in the menu header; also used in the avatar's automation name.</summary>
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<UserMenu, string?>(nameof(DisplayName));

    /// <summary>Email address shown under the name in the menu header.</summary>
    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<UserMenu, string?>(nameof(Email));

    /// <summary>Plan or role shown as a small badge beside the name ("Pro").</summary>
    public static readonly StyledProperty<string?> PlanProperty =
        AvaloniaProperty.Register<UserMenu, string?>(nameof(Plan));

    /// <summary>Photo; falls back to initials while it loads or when it fails.</summary>
    public static readonly StyledProperty<ImageSource?> AvatarSourceProperty =
        AvaloniaProperty.Register<UserMenu, ImageSource?>(nameof(AvatarSource));

    /// <summary>Shows the name and a chevron beside the avatar (wide windows only).</summary>
    public static readonly StyledProperty<bool> IsNameVisibleProperty =
        AvaloniaProperty.Register<UserMenu, bool>(nameof(IsNameVisible));

    /// <summary>Theme chosen in the inline switch (Light, Dark or System); two-way bound. Applying it is up to the app.</summary>
    public static readonly StyledProperty<UserMenuThemePreference> ThemePreferenceProperty =
        AvaloniaProperty.Register<UserMenu, UserMenuThemePreference>(nameof(ThemePreference), UserMenuThemePreference.System,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows the inline light / dark / system switch.</summary>
    public static readonly StyledProperty<bool> IsThemeSwitchVisibleProperty =
        AvaloniaProperty.Register<UserMenu, bool>(nameof(IsThemeSwitchVisible), true);

    /// <summary>Presence status (Available, Busy or Away) shown by the dot and the status switch; two-way bound.</summary>
    public static readonly StyledProperty<UserMenuStatus> StatusProperty =
        AvaloniaProperty.Register<UserMenu, UserMenuStatus>(nameof(Status), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows the presence dot on the avatar and the inline status switch.</summary>
    public static readonly StyledProperty<bool> IsStatusVisibleProperty =
        AvaloniaProperty.Register<UserMenu, bool>(nameof(IsStatusVisible));

    /// <summary>Sign-out handler. An unfinished task keeps the menu open with a spinner until it settles.</summary>
    public static readonly StyledProperty<Func<Task>?> SignOutActionProperty =
        AvaloniaProperty.Register<UserMenu, Func<Task>?>(nameof(SignOutAction));

    /// <summary>Shortcut hint for the sign-out row (for example "⇧⌘Q").</summary>
    public static readonly StyledProperty<string?> SignOutGestureTextProperty =
        AvaloniaProperty.Register<UserMenu, string?>(nameof(SignOutGestureText));

    /// <summary>Whether the menu is open; two-way bound.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<UserMenu, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Panel alignment against the avatar (Arc: end).</summary>
    public static readonly StyledProperty<MenuAlignment> PanelAlignmentProperty =
        AvaloniaProperty.Register<UserMenu, MenuAlignment>(nameof(PanelAlignment), MenuAlignment.End);

    /// <summary>Account destinations as data (rows are generated; <see cref="MenuEntry"/> items are used as is).</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<UserMenu, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Data template applied to the header of each row generated from ItemsSource.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<UserMenu, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly DirectProperty<UserMenu, bool> IsSigningOutProperty =
        AvaloniaProperty.RegisterDirect<UserMenu, bool>(nameof(IsSigningOut), o => o.IsSigningOut);

    /// <summary>Raised when an account destination row is clicked; the argument carries the entry and its item.</summary>
    public static readonly RoutedEvent<MenuEntryClickedEventArgs> ItemClickedEvent =
        RoutedEvent.Register<UserMenu, MenuEntryClickedEventArgs>(nameof(ItemClicked), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<UserMenuThemePreferenceChangedEventArgs> ThemePreferenceChangedEvent =
        RoutedEvent.Register<UserMenu, UserMenuThemePreferenceChangedEventArgs>(nameof(ThemePreferenceChanged), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<UserMenuStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<UserMenu, UserMenuStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> SignOutRequestedEvent =
        RoutedEvent.Register<UserMenu, RoutedEventArgs>(nameof(SignOutRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the menu opens.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenedEvent =
        RoutedEvent.Register<UserMenu, RoutedEventArgs>(nameof(Opened), RoutingStrategies.Direct);

    /// <summary>Raised when the menu closes.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<UserMenu, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Direct);

    /// <inheritdoc cref="DisplayNameProperty"/>
    public string? DisplayName
    {
        get => GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    /// <inheritdoc cref="EmailProperty"/>
    public string? Email
    {
        get => GetValue(EmailProperty);
        set => SetValue(EmailProperty, value);
    }

    public string? Plan
    {
        get => GetValue(PlanProperty);
        set => SetValue(PlanProperty, value);
    }

    public ImageSource? AvatarSource
    {
        get => GetValue(AvatarSourceProperty);
        set => SetValue(AvatarSourceProperty, value);
    }

    public bool IsNameVisible
    {
        get => GetValue(IsNameVisibleProperty);
        set => SetValue(IsNameVisibleProperty, value);
    }

    /// <inheritdoc cref="ThemePreferenceProperty"/>
    public UserMenuThemePreference ThemePreference
    {
        get => GetValue(ThemePreferenceProperty);
        set => SetValue(ThemePreferenceProperty, value);
    }

    /// <inheritdoc cref="IsThemeSwitchVisibleProperty"/>
    public bool IsThemeSwitchVisible
    {
        get => GetValue(IsThemeSwitchVisibleProperty);
        set => SetValue(IsThemeSwitchVisibleProperty, value);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public UserMenuStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public bool IsStatusVisible
    {
        get => GetValue(IsStatusVisibleProperty);
        set => SetValue(IsStatusVisibleProperty, value);
    }

    public Func<Task>? SignOutAction
    {
        get => GetValue(SignOutActionProperty);
        set => SetValue(SignOutActionProperty, value);
    }

    public string? SignOutGestureText
    {
        get => GetValue(SignOutGestureTextProperty);
        set => SetValue(SignOutGestureTextProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public MenuAlignment PanelAlignment
    {
        get => GetValue(PanelAlignmentProperty);
        set => SetValue(PanelAlignmentProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="ItemTemplateProperty"/>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    private bool _isSigningOut;

    /// <summary>True while an asynchronous <see cref="SignOutAction"/> runs.</summary>
    public bool IsSigningOut
    {
        get => _isSigningOut;
        private set => SetAndRaise(IsSigningOutProperty, ref _isSigningOut, value);
    }

    /// <summary>Account destinations declared inline (Profile, Settings, Billing).</summary>
    [Content]
    public AvaloniaList<MenuEntry> Items { get; } = new();

    /// <summary>The menu surface (the panel on wide windows, the sheet content on narrow ones).</summary>
    public MenuSurface Menu { get; }

    /// <summary>Raised when an account destination row is clicked; the argument carries the entry and its item.</summary>
    public event EventHandler<MenuEntryClickedEventArgs>? ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    /// <summary>Raised when a theme is chosen; the menu stays open and applying the theme is up to the app.</summary>
    public event EventHandler<UserMenuThemePreferenceChangedEventArgs>? ThemePreferenceChanged
    {
        add => AddHandler(ThemePreferenceChangedEvent, value);
        remove => RemoveHandler(ThemePreferenceChangedEvent, value);
    }

    /// <summary>Raised when a status is chosen; the menu stays open.</summary>
    public event EventHandler<UserMenuStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    /// <summary>Raised when the sign-out row is activated (before <see cref="SignOutAction"/> runs).</summary>
    public event EventHandler<RoutedEventArgs>? SignOutRequested
    {
        add => AddHandler(SignOutRequestedEvent, value);
        remove => RemoveHandler(SignOutRequestedEvent, value);
    }

    /// <summary>Raised when the menu opens.</summary>
    public event EventHandler<RoutedEventArgs>? Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    /// <summary>Raised when the menu closes.</summary>
    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    #endregion

    private static readonly AttachedProperty<object?> SourceItemProperty =
        AvaloniaProperty.RegisterAttached<UserMenu, MenuEntry, object?>("SourceItem");

    private static readonly UserMenuChoiceOption[] s_statusOptions =
    [
        new("Available", null, UserMenuStatus.Available),
        new("Busy", null, UserMenuStatus.Busy),
        new("Away", null, UserMenuStatus.Away)
    ];

    private static readonly UserMenuChoiceOption[] s_themeOptions =
    [
        new("Light", ArcIcons.Sun, null),
        new("Dark", ArcIcons.Moon, null),
        new("System", ArcIcons.Monitor, null)
    ];

    private readonly MenuEntrySeparator _itemsSeparator = new();
    private readonly MenuEntrySeparator _choicesSeparator = new();
    private readonly MenuEntrySeparator _signOutSeparator = new();
    private readonly List<MenuEntry> _generated = new();
    private readonly MenuEntry _signOut;
    private readonly ArcContentSwap _signOutLabel;
    private UserMenuChoiceRow? _statusRow;
    private UserMenuChoiceRow? _themeRow;
    private UserMenuIdentity? _identity;
    private AvaloniaButton? _trigger;
    private TopLevel? _topLevel;
    private bool _keyboardActivation;
    private bool _compact;

    public UserMenu()
    {
        Menu = new MenuSurface { Variant = MenuSurfaceVariant.Account, AnchorGap = 8, Alignment = MenuAlignment.End };
        Menu.ItemClicked += OnMenuItemClicked;
        Menu.Opened += (_, _) => RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
        Menu.Closed += (_, _) => RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
        Menu.PropertyChanged += (_, e) =>
        {
            if (e.Property == MenuSurface.IsOpenProperty)
            {
                SetCurrentValue(IsOpenProperty, e.GetNewValue<bool>());
            }
        };

        _signOutLabel = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, Content = "Sign out" };
        _signOut = new MenuEntry { Header = _signOutLabel, Icon = ArcIcons.LogOut, StaysOpenOnClick = true };
        TextSearch.SetText(_signOut, "Sign out");
        AutomationProperties.SetName(_signOut, "Sign out");
        _signOut.Click += (_, _) => _ = SignOutAsync();

        Items.CollectionChanged += (_, _) => RebuildRows();
        RebuildRows();
        UpdateAutomationName();
    }

    /// <summary>The avatar trigger the menu returns focus to.</summary>
    public Control? Anchor => _trigger;

    /// <summary>Opens the menu; with <paramref name="focusFirst"/> the highlight starts on the first row.</summary>
    public void Open(bool focusFirst = false) => OpenCore(focusFirst ? MenuOpenFocus.First : MenuOpenFocus.Surface);

    /// <summary>Closes the menu.</summary>
    public void Close() => Menu.Close(returnFocus: false);

    private void OpenCore(MenuOpenFocus focus)
    {
        if (_trigger is not { } trigger || TopLevel.GetTopLevel(trigger) is null)
        {
            SetCurrentValue(IsOpenProperty, true);
            return;
        }
        if (_identity is null)
        {
            _identity = new UserMenuIdentity(this);
            Menu.Header = _identity;
        }
        UpdateCompact();
        Menu.Variant = _compact ? MenuSurfaceVariant.Sheet : MenuSurfaceVariant.Account;
        Menu.Open(trigger, focus);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
            _trigger.RemoveHandler(KeyDownEvent, OnTriggerKeyDown);
        }
        _trigger = e.NameScope.Find<AvaloniaButton>(TriggerPart);
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.AddHandler(KeyDownEvent, OnTriggerKeyDown, RoutingStrategies.Tunnel);
        }
        UpdateAutomationName();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.SizeChanged += OnTopLevelSizeChanged;
        }
        UpdateCompact();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is not null)
        {
            _topLevel.SizeChanged -= OnTopLevelSizeChanged;
            _topLevel = null;
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // A menu that starts open never steals focus or scrolls the page.
        if (IsOpen && !Menu.IsOpen)
        {
            OpenCore(MenuOpenFocus.None);
        }
    }

    private void OnTopLevelSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateCompact();

    private void UpdateCompact()
    {
        var compact = _topLevel is { } topLevel && topLevel.ClientSize.Width > 0 && topLevel.ClientSize.Width < CompactBreakpoint;
        if (compact == _compact)
        {
            return;
        }
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (Menu.IsOpen)
        {
            // The presentation changes with the window: close rather than morph a panel into a sheet.
            Menu.Close(returnFocus: false);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            if (open && !Menu.IsOpen && IsLoaded)
            {
                OpenCore(MenuOpenFocus.Surface);
            }
            else if (!open && Menu.IsOpen)
            {
                Menu.Close(returnFocus: false);
            }
        }
        else if (change.Property == ThemePreferenceProperty)
        {
            if (_themeRow is not null)
            {
                _themeRow.Segments.SelectedIndex = (int)change.GetNewValue<UserMenuThemePreference>();
            }
        }
        else if (change.Property == StatusProperty)
        {
            if (_statusRow is not null)
            {
                _statusRow.Segments.SelectedIndex = (int)change.GetNewValue<UserMenuStatus>();
            }
            UpdateAutomationName();
        }
        else if (change.Property == IsThemeSwitchVisibleProperty || change.Property == IsStatusVisibleProperty)
        {
            RebuildRows();
            UpdateAutomationName();
        }
        else if (change.Property == ItemsSourceProperty || change.Property == ItemTemplateProperty)
        {
            RebuildRows();
        }
        else if (change.Property == SignOutGestureTextProperty)
        {
            _signOut.InputGestureText = change.GetNewValue<string?>();
        }
        else if (change.Property == PanelAlignmentProperty)
        {
            Menu.Alignment = change.GetNewValue<MenuAlignment>();
        }
        else if (change.Property == DisplayNameProperty)
        {
            UpdateAutomationName();
        }
    }

    private void UpdateAutomationName()
    {
        var name = $"Account menu, {DisplayName}";
        if (IsStatusVisible)
        {
            name += ", " + s_statusOptions[(int)Status].Label;
        }
        AutomationProperties.SetName(Menu, "Account menu");
        if (_trigger is not null)
        {
            AutomationProperties.SetName(_trigger, name);
        }
    }

    private void RebuildRows()
    {
        var rows = Menu.Items;
        rows.Clear();
        var destinations = new List<MenuEntry>(Items);
        _generated.Clear();
        if (ItemsSource is { } source)
        {
            foreach (var item in source)
            {
                if (item is MenuEntry entry)
                {
                    destinations.Add(entry);
                    continue;
                }
                var row = new MenuEntry { Header = item, DataContext = item };
                if (ItemTemplate is { } template)
                {
                    row.HeaderTemplate = template;
                }
                row.SetValue(SourceItemProperty, item);
                _generated.Add(row);
                destinations.Add(row);
            }
        }
        if (destinations.Count > 0)
        {
            rows.Add(_itemsSeparator);
            foreach (var entry in destinations)
            {
                rows.Add(entry);
            }
        }
        if (IsStatusVisible || IsThemeSwitchVisible)
        {
            rows.Add(_choicesSeparator);
            if (IsStatusVisible)
            {
                rows.Add(StatusRow);
            }
            if (IsThemeSwitchVisible)
            {
                rows.Add(ThemeRow);
            }
        }
        rows.Add(_signOutSeparator);
        rows.Add(_signOut);
    }

    private UserMenuChoiceRow StatusRow
    {
        get
        {
            if (_statusRow is null)
            {
                var dot = new UserMenuPresenceDot { DotSize = 10 };
                ArcTokens.Bind(dot, UserMenuPresenceDot.SurfaceBrushProperty, ArcDesignTokenKind.SurfaceRaised);
                dot.Bind(UserMenuPresenceDot.StatusProperty, this.GetObservable(StatusProperty));
                _statusRow = new UserMenuChoiceRow("Status", dot, s_statusOptions);
                _statusRow.Segments.SelectedIndex = (int)Status;
                _statusRow.Segments.SelectionChanged += index =>
                {
                    SetCurrentValue(StatusProperty, (UserMenuStatus)index);
                    RaiseEvent(new UserMenuStatusChangedEventArgs(StatusChangedEvent, (UserMenuStatus)index));
                };
            }
            return _statusRow;
        }
    }

    private UserMenuChoiceRow ThemeRow
    {
        get
        {
            if (_themeRow is null)
            {
                _themeRow = new UserMenuChoiceRow("Theme", ArcIcons.SunMoon, s_themeOptions);
                _themeRow.Segments.SelectedIndex = (int)ThemePreference;
                _themeRow.Segments.SelectionChanged += index =>
                {
                    SetCurrentValue(ThemePreferenceProperty, (UserMenuThemePreference)index);
                    RaiseEvent(new UserMenuThemePreferenceChangedEventArgs(ThemePreferenceChangedEvent, (UserMenuThemePreference)index));
                };
            }
            return _themeRow;
        }
    }

    private async Task SignOutAsync()
    {
        if (IsSigningOut)
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(SignOutRequestedEvent, this));
        var task = SignOutAction?.Invoke();
        if (task is null || task.IsCompleted)
        {
            Menu.Close(returnFocus: true);
            return;
        }
        // Async sign out keeps the menu open with its progress on the row, then closes once it settles.
        IsSigningOut = true;
        PseudoClasses.Set(SigningOutPseudoClass, true);
        _signOutLabel.Content = "Signing out";
        _signOut.Icon = new ArcSpinner { Width = 16, Height = 16 };
        AutomationProperties.SetHelpText(_signOut, "Busy");
        try
        {
            await task;
        }
        catch (Exception)
        {
            // The app reports its own sign-out failures; the menu only stops waiting.
        }
        finally
        {
            IsSigningOut = false;
            PseudoClasses.Set(SigningOutPseudoClass, false);
            _signOutLabel.Content = "Sign out";
            _signOut.Icon = ArcIcons.LogOut;
            AutomationProperties.SetHelpText(_signOut, null);
            Menu.Close(returnFocus: false);
        }
    }

    private void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
                e.Handled = true;
                if (!Menu.IsOpen)
                {
                    OpenCore(e.Key == Key.Down ? MenuOpenFocus.First : MenuOpenFocus.Last);
                }
                break;
            case Key.Enter:
            case Key.Space:
                _keyboardActivation = true;
                break;
        }
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        var focus = _keyboardActivation ? MenuOpenFocus.First : MenuOpenFocus.Surface;
        _keyboardActivation = false;
        if (Menu.IsOpen)
        {
            Menu.Close(returnFocus: false);
        }
        else
        {
            OpenCore(focus);
        }
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (ReferenceEquals(e.Entry, _signOut))
        {
            return;
        }
        var item = e.Entry.GetValue(SourceItemProperty) ?? e.Item;
        RaiseEvent(new MenuEntryClickedEventArgs(ItemClickedEvent, e.Entry, item));
    }
}
