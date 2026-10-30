using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A workspace switcher for multi-tenant apps (Arc Team switcher, design reference). The trigger shows the current
/// workspace's tile, its name and its plan; the menu under it has a search field, your role in each workspace and a
/// Cmd/Ctrl+1 to 9 shortcut for each of the first nine, and lets you create or join a workspace inline. One highlight
/// glides between rows, and switching crossfades the trigger to the new workspace through a short loading state.
/// <para>
/// Give it your workspaces in <see cref="Workspaces"/> and the active id in <see cref="Value"/> (two-way). Loading a
/// workspace, creating one and joining one are your async delegates (<see cref="SwitchHandler"/>,
/// <see cref="CreateHandler"/>, <see cref="JoinHandler"/>); each one left out is simulated. With no workspaces, a
/// <see cref="Simulation"/> supplies demo workspaces and the invites its simulated join accepts; with neither, the trigger
/// says there is no workspace. The root is up to 360px wide and fills narrower containers.
/// </para>
/// </summary>
[TemplatePart(TriggerPart, typeof(TeamSwitcherTrigger))]
[TemplatePart(FacePart, typeof(TeamSwitcherFace))]
[TemplatePart(PopupPart, typeof(TeamSwitcherPopup))]
[TemplatePart(MenuPart, typeof(TeamSwitcherMenu))]
[TemplatePart(LivePart, typeof(ExplainerAnnouncer))]
[PseudoClasses(SwitchingPseudoClass, OpenPseudoClass, ErrorPseudoClass)]
public partial class TeamSwitcher : TemplatedControl
{
    internal const string TriggerPart = "PART_Trigger";
    internal const string FacePart = "PART_Face";
    internal const string PopupPart = "PART_Popup";
    internal const string MenuPart = "PART_Menu";
    internal const string LivePart = "PART_Live";

    internal const string SwitchingPseudoClass = ":switching";
    internal const string OpenPseudoClass = ":open";
    internal const string ErrorPseudoClass = ":error";

    /// <summary>The id of the active workspace (two-way). Null starts on the first workspace.</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<TeamSwitcher, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the menu is open (two-way). Setting it opens the menu without moving focus.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<TeamSwitcher, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Loads a workspace. The trigger crossfades to it with a switching state until the task finishes; an exception
    /// reverts and shows its message. A newer switch cancels the token of the older one. Null simulates a load of
    /// <see cref="SimulatedDelay"/>.
    /// </summary>
    public static readonly StyledProperty<Func<TeamSwitcherWorkspace, CancellationToken, Task>?> SwitchHandlerProperty =
        AvaloniaProperty.Register<TeamSwitcher, Func<TeamSwitcherWorkspace, CancellationToken, Task>?>(nameof(SwitchHandler));

    /// <summary>
    /// Creates a workspace from its name. Return it to add it and switch to it (or null to only close the menu); throw to
    /// show the exception's message under the field. Null simulates it.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<TeamSwitcherWorkspace?>>?> CreateHandlerProperty =
        AvaloniaProperty.Register<TeamSwitcher, Func<string, CancellationToken, Task<TeamSwitcherWorkspace?>>?>(nameof(CreateHandler));

    /// <summary>
    /// Joins a workspace from an invite link or code. Return the workspace to add it and switch to it; throw to show the
    /// exception's message. Null simulates it: it accepts the invites of <see cref="Simulation"/>, if any.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<TeamSwitcherWorkspace?>>?> JoinHandlerProperty =
        AvaloniaProperty.Register<TeamSwitcher, Func<string, CancellationToken, Task<TeamSwitcherWorkspace?>>?>(nameof(JoinHandler));

    /// <summary>
    /// Listens for Cmd/Ctrl+1 to 9 anywhere in the window. Off, the shortcuts work while focus is inside the switcher.
    /// </summary>
    public static readonly StyledProperty<bool> UsesGlobalShortcutsProperty =
        AvaloniaProperty.Register<TeamSwitcher, bool>(nameof(UsesGlobalShortcuts));

    /// <summary>
    /// Demo workspaces to show while <see cref="Workspaces"/> is empty, with the invites the simulated join accepts and what
    /// the simulated create makes. Null (the default) shows no workspace. Give each switcher its own simulation.
    /// </summary>
    public static readonly StyledProperty<TeamSwitcherSimulation?> SimulationProperty =
        AvaloniaProperty.Register<TeamSwitcher, TeamSwitcherSimulation?>(nameof(Simulation));

    /// <summary>How long the simulated switch, create and join take when no delegate is set.</summary>
    public static readonly StyledProperty<TimeSpan> SimulatedDelayProperty =
        AvaloniaProperty.Register<TeamSwitcher, TimeSpan>(nameof(SimulatedDelay), TimeSpan.FromMilliseconds(650));

    /// <summary>Every word the switcher shows or says.</summary>
    public static readonly StyledProperty<TeamSwitcherLabels> LabelsProperty =
        AvaloniaProperty.Register<TeamSwitcher, TeamSwitcherLabels>(nameof(Labels), TeamSwitcherLabels.Default);

    /// <summary>Raised once a switch has finished loading (and <see cref="Value"/> has changed). Bubbles.</summary>
    public static readonly RoutedEvent<TeamSwitcherValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<TeamSwitcher, TeamSwitcherValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SwitchHandler"/> threw: the trigger went back to the previous workspace. Bubbles.</summary>
    public static readonly RoutedEvent<TeamSwitcherSwitchFailedEventArgs> SwitchFailedEvent =
        RoutedEvent.Register<TeamSwitcher, TeamSwitcherSwitchFailedEventArgs>(nameof(SwitchFailed), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when someone created or joined a workspace from the menu. Add it to your list too; the switcher already
    /// shows it (de-duplicated by id) and switches to it. Bubbles.
    /// </summary>
    public static readonly RoutedEvent<TeamSwitcherWorkspaceAddedEventArgs> WorkspaceAddedEvent =
        RoutedEvent.Register<TeamSwitcher, TeamSwitcherWorkspaceAddedEventArgs>(nameof(WorkspaceAdded), RoutingStrategies.Bubble);

    /// <summary>Raised when the menu opens or closes. Bubbles.</summary>
    public static readonly RoutedEvent<TeamSwitcherOpenChangedEventArgs> OpenChangedEvent =
        RoutedEvent.Register<TeamSwitcher, TeamSwitcherOpenChangedEventArgs>(nameof(OpenChanged), RoutingStrategies.Bubble);

    private readonly AvaloniaList<TeamSwitcherWorkspace> _workspaces = [];
    private readonly List<TeamSwitcherWorkspace> _added = [];
    private readonly List<TeamSwitcherWorkspace> _effective = [];
    private readonly HashSet<TeamSwitcherWorkspace> _watched = [];
    private TeamSwitcherTrigger? _trigger;
    private TeamSwitcherFace? _face;
    private TeamSwitcherPopup? _popup;
    private TeamSwitcherMenu? _menu;
    private ExplainerAnnouncer? _live;
    private TopLevel? _globalTopLevel;
    private bool _focusOnOpen;
    private bool _returnFocusOnClose;
    // Whether the last press or key the switcher saw was a pointer: focus then goes back to the trigger without the
    // keyboard ring (a mouse pick must not light it up), as browsers do for :focus-visible.
    private bool _pointerInput;
    private bool _effectiveStale = true;

    /// <summary>Creates the switcher.</summary>
    public TeamSwitcher()
    {
        _workspaces.CollectionChanged += OnWorkspacesChanged;
        AddHandler(KeyDownEvent, OnShortcutKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnInputPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        WorkspacesChanged();
    }

    /// <inheritdoc cref="ValueProperty"/>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="SwitchHandlerProperty"/>
    public Func<TeamSwitcherWorkspace, CancellationToken, Task>? SwitchHandler
    {
        get => GetValue(SwitchHandlerProperty);
        set => SetValue(SwitchHandlerProperty, value);
    }

    /// <inheritdoc cref="CreateHandlerProperty"/>
    public Func<string, CancellationToken, Task<TeamSwitcherWorkspace?>>? CreateHandler
    {
        get => GetValue(CreateHandlerProperty);
        set => SetValue(CreateHandlerProperty, value);
    }

    /// <inheritdoc cref="JoinHandlerProperty"/>
    public Func<string, CancellationToken, Task<TeamSwitcherWorkspace?>>? JoinHandler
    {
        get => GetValue(JoinHandlerProperty);
        set => SetValue(JoinHandlerProperty, value);
    }

    /// <inheritdoc cref="UsesGlobalShortcutsProperty"/>
    public bool UsesGlobalShortcuts { get => GetValue(UsesGlobalShortcutsProperty); set => SetValue(UsesGlobalShortcutsProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public TeamSwitcherSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="SimulatedDelayProperty"/>
    public TimeSpan SimulatedDelay { get => GetValue(SimulatedDelayProperty); set => SetValue(SimulatedDelayProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public TeamSwitcherLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>
    /// The workspaces you belong to, in order (the first nine get Cmd/Ctrl+1 to 9). Empty shows the <see cref="Simulation"/>'s, if any.
    /// </summary>
    [Content]
    public AvaloniaList<TeamSwitcherWorkspace> Workspaces => _workspaces;

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<TeamSwitcherValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <inheritdoc cref="SwitchFailedEvent"/>
    public event EventHandler<TeamSwitcherSwitchFailedEventArgs>? SwitchFailed
    {
        add => AddHandler(SwitchFailedEvent, value);
        remove => RemoveHandler(SwitchFailedEvent, value);
    }

    /// <inheritdoc cref="WorkspaceAddedEvent"/>
    public event EventHandler<TeamSwitcherWorkspaceAddedEventArgs>? WorkspaceAdded
    {
        add => AddHandler(WorkspaceAddedEvent, value);
        remove => RemoveHandler(WorkspaceAddedEvent, value);
    }

    /// <inheritdoc cref="OpenChangedEvent"/>
    public event EventHandler<TeamSwitcherOpenChangedEventArgs>? OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary>Opens the menu and moves focus to its search field.</summary>
    public void Open()
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        _focusOnOpen = true;
        if (IsOpen)
        {
            FocusMenu();
            return;
        }
        SetCurrentValue(IsOpenProperty, true);
    }

    /// <summary>Closes the menu and gives focus back to the trigger.</summary>
    public void Close() => CloseMenu(returnFocus: true);

    /// <summary>The workspaces on screen: <see cref="Workspaces"/> (or the simulation's) plus those created or joined here, by id.</summary>
    internal IReadOnlyList<TeamSwitcherWorkspace> EffectiveWorkspaces
    {
        get
        {
            if (_effectiveStale)
            {
                _effectiveStale = false;
                _effective.Clear();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var w in _workspaces.Count > 0 ? (IEnumerable<TeamSwitcherWorkspace>)_workspaces : Simulation?.Workspaces ?? [])
                {
                    if (ids.Add(w.Id))
                    {
                        _effective.Add(w);
                    }
                }
                foreach (var w in _added)
                {
                    if (ids.Add(w.Id))
                    {
                        _effective.Add(w);
                    }
                }
            }
            return _effective;
        }
    }

    /// <summary>Whether the simulation's workspaces are on screen.</summary>
    internal bool UsesSimulation => _workspaces.Count == 0 && Simulation is not null;

    /// <summary>The simulation in use: none once the app gives its own workspaces.</summary>
    internal TeamSwitcherSimulation? ActiveSimulation => UsesSimulation ? Simulation : null;

    /// <summary>The invites the simulated join accepts: the active simulation's, or none.</summary>
    internal IReadOnlyList<TeamSwitcherSimulationInvite> SimulatedInvites => ActiveSimulation?.Invites ?? [];

    /// <summary>The active workspace: the one <see cref="Value"/> names, or the first.</summary>
    internal TeamSwitcherWorkspace? CurrentWorkspace
    {
        get
        {
            var list = EffectiveWorkspaces;
            if (Value is { } id)
            {
                foreach (var w in list)
                {
                    if (w.Id == id)
                    {
                        return w;
                    }
                }
            }
            return list.Count > 0 ? list[0] : null;
        }
    }

    internal TeamSwitcherTrigger? Trigger => _trigger;

    internal TeamSwitcherFace? Face => _face;

    internal TeamSwitcherPopup? Popup => _popup;

    internal TeamSwitcherMenu? Menu => _menu;

    /// <summary>The last thing the live region said.</summary>
    internal string? Announcement => _live?.Announcement;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
            _trigger.RemoveHandler(KeyDownEvent, OnTriggerKeyDown);
            _trigger.SizeChanged -= OnTriggerSizeChanged;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
        }
        if (_menu is not null)
        {
            _menu.RemoveHandler(PointerPressedEvent, OnInputPointerPressed);
            _menu.RemoveHandler(KeyDownEvent, OnInputKeyDown);
        }
        _trigger = e.NameScope.Find<TeamSwitcherTrigger>(TriggerPart);
        _face = e.NameScope.Find<TeamSwitcherFace>(FacePart);
        _popup = e.NameScope.Find<TeamSwitcherPopup>(PopupPart);
        _menu = e.NameScope.Find<TeamSwitcherMenu>(MenuPart);
        _live = e.NameScope.Find<ExplainerAnnouncer>(LivePart);
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.AddHandler(KeyDownEvent, OnTriggerKeyDown, RoutingStrategies.Tunnel);
            _trigger.SizeChanged += OnTriggerSizeChanged;
            _trigger.IsExpanded = IsOpen;
        }
        if (_popup is not null)
        {
            _popup.PlacementTarget = _trigger;
            _popup.Closed += OnPopupClosed;
        }
        if (_menu is not null)
        {
            // The menu lives in a popup: its input is watched on the menu itself as well as on the switcher.
            _menu.AddHandler(PointerPressedEvent, OnInputPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _menu.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        _menu?.Attach(this);
        _menu?.Rebuild();
        PseudoClasses.Set(OpenPseudoClass, IsOpen);
        UpdateFace();
        if (IsOpen)
        {
            // Starts open (Arc's defaultOpen): shown once laid out, without taking focus.
            Dispatcher.UIThread.Post(() =>
            {
                if (IsOpen)
                {
                    OpenPopup();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            OnValueChanged();
        }
        else if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            if (_trigger is not null)
            {
                _trigger.IsExpanded = open;
            }
            if (open)
            {
                OpenPopup();
            }
            else
            {
                ClosePopup();
            }
            RaiseEvent(new TeamSwitcherOpenChangedEventArgs(OpenChangedEvent, open));
        }
        else if (change.Property == LabelsProperty)
        {
            _menu?.ApplyLabels();
            _menu?.RefreshRows();
            UpdateFace();
        }
        else if (change.Property == JoinHandlerProperty)
        {
            _menu?.ApplyLabels();
        }
        else if (change.Property == SimulationProperty)
        {
            // A new simulation starts over: what was created or joined in the old one goes with it.
            _added.Clear();
            WorkspacesChanged();
            _menu?.ApplyLabels();
        }
        else if (change.Property == UsesGlobalShortcutsProperty)
        {
            UpdateGlobalShortcuts();
        }
        else if (change.Property == IsEnabledProperty && !IsEnabled && IsOpen)
        {
            CloseMenu(returnFocus: false);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateGlobalShortcuts();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateGlobalShortcuts();
        CancelSwitch();
    }

    private void OnWorkspacesChanged(object? sender, NotifyCollectionChangedEventArgs e) => WorkspacesChanged();

    /// <summary>The list changed (items, or a workspace's details): the rows and the trigger follow.</summary>
    private void WorkspacesChanged()
    {
        _effectiveStale = true;
        var list = EffectiveWorkspaces;
        foreach (var w in _watched.Where(w => !list.Contains(w)).ToList())
        {
            w.PropertyChanged -= OnWorkspacePropertyChanged;
            _watched.Remove(w);
        }
        foreach (var w in list)
        {
            if (_watched.Add(w))
            {
                w.PropertyChanged += OnWorkspacePropertyChanged;
            }
        }
        _menu?.Rebuild();
        UpdateFace();
    }

    private void OnWorkspacePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TeamSwitcherWorkspace.IdProperty)
        {
            WorkspacesChanged();
        }
        else
        {
            _menu?.RefreshRows();
            UpdateFace();
        }
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        if (IsOpen)
        {
            CloseMenu(returnFocus: true);
        }
        else
        {
            Open();
        }
    }

    /// <summary>ArrowDown on the trigger opens the menu (Enter and Space press the button).</summary>
    private void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.None && !IsOpen)
        {
            e.Handled = true;
            Open();
        }
    }

    private void OnTriggerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_menu is not null && e.NewSize.Width > 0)
        {
            _menu.SurfaceWidth = e.NewSize.Width;
        }
    }

    private void OpenPopup()
    {
        if (_popup is null || _menu is null || _trigger is null || !IsAttachedToVisualTree())
        {
            return;
        }
        if (_trigger.Bounds.Width > 0)
        {
            _menu.SurfaceWidth = _trigger.Bounds.Width;
        }
        // The menu is only templated once the popup has put it on screen.
        _popup.IsOpen = true;
        _menu.ApplyTemplate();
        _menu.Reset();
        if (_focusOnOpen)
        {
            _focusOnOpen = false;
            Dispatcher.UIThread.Post(FocusMenu, DispatcherPriority.Loaded);
        }
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    private void FocusMenu()
    {
        if (IsOpen)
        {
            _menu?.FocusField();
        }
    }

    private void ClosePopup()
    {
        _focusOnOpen = false;
        var returnFocus = _returnFocusOnClose;
        _returnFocusOnClose = false;
        if (returnFocus)
        {
            // Before the popup closes: closing it with focus inside puts focus back on the trigger by itself, without
            // the keyboard ring, and focusing the already focused trigger then changes nothing.
            _trigger?.Focus(ReturnFocusMethod);
        }
        if (_popup is not null)
        {
            _popup.IsOpen = false;
        }
        _menu?.OnClosed();
    }

    /// <summary>Closes the menu; with <paramref name="returnFocus"/> the trigger gets focus back.</summary>
    internal void CloseMenu(bool returnFocus)
    {
        if (!IsOpen)
        {
            if (returnFocus)
            {
                _trigger?.Focus(ReturnFocusMethod);
            }
            return;
        }
        _returnFocusOnClose = returnFocus;
        SetCurrentValue(IsOpenProperty, false);
    }

    /// <summary>How focus goes back to the trigger: with the keyboard ring only after keyboard input.</summary>
    private NavigationMethod ReturnFocusMethod => _pointerInput ? NavigationMethod.Pointer : NavigationMethod.Tab;

    private void OnInputPointerPressed(object? sender, PointerPressedEventArgs e) => _pointerInput = true;

    private void OnInputKeyDown(object? sender, KeyEventArgs e) => _pointerInput = false;

    /// <summary>The popup went away on its own (a click outside): the open state follows.</summary>
    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void Announce(string text)
    {
        if (_live is not null && text != _live.Announcement)
        {
            _live.Announce(text);
        }
    }

    private void UpdateGlobalShortcuts()
    {
        var topLevel = UsesGlobalShortcuts ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(topLevel, _globalTopLevel))
        {
            return;
        }
        _globalTopLevel?.RemoveHandler(KeyDownEvent, OnShortcutKeyDown);
        _globalTopLevel = topLevel;
        _globalTopLevel?.AddHandler(KeyDownEvent, OnShortcutKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Cmd/Ctrl+1 to 9 switches to the workspace at that place in the list.</summary>
    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled || ShortcutNumber(e) is not { } n)
        {
            return;
        }
        var list = EffectiveWorkspaces;
        if (n > list.Count)
        {
            return;
        }
        e.Handled = true;
        Choose(list[n - 1]);
    }

    /// <summary>The digit of a Cmd/Ctrl+1 to 9 press, or null.</summary>
    internal static int? ShortcutNumber(KeyEventArgs e)
    {
        var modifiers = e.KeyModifiers;
        var command = (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (!command || (modifiers & (KeyModifiers.Alt | KeyModifiers.Shift)) != 0)
        {
            return null;
        }
        return e.Key switch
        {
            >= Key.D1 and <= Key.D9 => e.Key - Key.D0,
            >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad0,
            _ => null
        };
    }
}
