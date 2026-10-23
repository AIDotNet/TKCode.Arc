using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class ModelMenu
{
    /// <summary>Hovering a row with a submenu opens it after this long (Radix).</summary>
    internal static readonly TimeSpan HoverOpenDelay = TimeSpan.FromMilliseconds(100);

    private ModelMenuPanel? _mainPanel;
    private ModelMenuPresence? _main;
    private ModelMenuPresence? _effortSub;
    private ModelMenuPresence? _moreSub;
    private ModelMenuPage? _openSub;
    private ModelMenuActionRow? _subTrigger;
    private bool _paged;
    private bool _pendingOpen;
    private ModelMenuOpenFocus _pendingFocus;
    private bool _keyboard;
    private bool _closeReturnFocus = true;
    private NavigationMethod _closeMethod = NavigationMethod.Pointer;
    private TopLevel? _hooked;
    private IDisposable? _clientSizeSubscription;
    private DispatcherTimer? _hoverTimer;
    private ModelMenuActionRow? _hoverRow;
    private ModelMenuSafeZone? _zone;
    private DateTime _zoneUntil;
    private bool _towardSubmenu = true;
    private Point _lastPointer;
    private bool _hasPointer;
    private ModelMenuPanel? _pointerPanel;
    private ModelMenuItem? _lastPointerRow;
    private bool _picking;

    #region Test and owner access

    /// <summary>The menu's surface (created on the first open).</summary>
    internal ModelMenuPanel? MainPanel => _mainPanel;

    internal ModelMenuPresence? MainPresence => _main;

    internal ModelMenuPresence? EffortPresence => _effortSub;

    internal ModelMenuPresence? MorePresence => _moreSub;

    /// <summary>Which submenu is open beside the menu (null in a narrow window, where they are pages).</summary>
    internal ModelMenuPage? OpenSubmenu => _openSub;

    /// <summary>True when the menu opened in a window no wider than <see cref="PagedBelowWidth"/>.</summary>
    internal bool IsPaged => _paged;

    internal bool IsHoverPending => _hoverTimer?.IsEnabled == true;

    internal ModelMenuSafeZone? SafeZone => _zone;

    private bool IsMainShown => _main is { IsShown: true };

    private ModelMenuPresence? OpenSubPresence => _openSub switch
    {
        ModelMenuPage.Effort when _effortSub is { IsShown: true } => _effortSub,
        ModelMenuPage.More when _moreSub is { IsShown: true } => _moreSub,
        _ => null
    };

    /// <summary>Opens the submenu the pointer is waiting on now instead of after the hover delay (tests).</summary>
    internal bool FlushHoverIntent()
    {
        if (_hoverTimer is not { IsEnabled: true })
        {
            return false;
        }
        OnHoverElapsed(_hoverTimer, EventArgs.Empty);
        return true;
    }

    #endregion

    #region Public actions

    /// <summary>
    /// Opens the menu against <paramref name="anchor"/>. <paramref name="focus"/> says where the keyboard position starts:
    /// nowhere for a pointer (<see cref="ModelMenuOpenFocus.Surface"/>), or a highlighted row for a key.
    /// </summary>
    public void Open(Control anchor, ModelMenuOpenFocus focus = ModelMenuOpenFocus.Surface)
    {
        _pendingFocus = focus;
        SetCurrentValue(PlacementTargetProperty, anchor);
        if (!IsOpen)
        {
            SetCurrentValue(IsOpenProperty, true);
            return;
        }
        if (IsMainShown)
        {
            var popup = _main!.Popup;
            if (!ReferenceEquals(popup.PlacementTarget, anchor))
            {
                // Opened again from another anchor: the menu moves there.
                CloseSubmenu(animated: false);
                popup.PlacementTarget = anchor;
                popup.OverlayInputPassThroughElement = anchor;
                popup.Reposition();
            }
            if (focus != ModelMenuOpenFocus.Surface)
            {
                ApplyOpenFocus(focus);
            }
            return;
        }
        OpenCore();
    }

    /// <summary>Opens the menu against <paramref name="anchor"/>, or closes it when it is open (a trigger's press).</summary>
    public void Toggle(Control anchor, ModelMenuOpenFocus focus = ModelMenuOpenFocus.Surface)
    {
        if (IsOpen)
        {
            CloseFrom(returnFocus: true, focus == ModelMenuOpenFocus.Surface ? NavigationMethod.Pointer : NavigationMethod.Tab);
        }
        else
        {
            Open(anchor, focus);
        }
    }

    /// <summary>
    /// Closes the menu and its submenus. With <paramref name="returnFocus"/> the anchor takes focus back, showing its
    /// keyboard focus ring when the menu was last used with keys.
    /// </summary>
    public void Close(bool returnFocus = true) =>
        CloseFrom(returnFocus, _keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);

    private void CloseFrom(bool returnFocus, NavigationMethod method)
    {
        if (!IsOpen)
        {
            return;
        }
        _closeReturnFocus = returnFocus;
        _closeMethod = method;
        SetCurrentValue(IsOpenProperty, false);
    }

    #endregion

    #region Open and close

    private void OnNavigationPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                OpenCore();
            }
            else
            {
                CloseCore();
            }
        }
        else if (change.Property == PlacementTargetProperty)
        {
            if (_pendingOpen && IsOpen)
            {
                Dispatcher.UIThread.Post(OpenPending, DispatcherPriority.Loaded);
            }
        }
        else if (change.Property == PlacementProperty && IsMainShown)
        {
            _main!.Popup.Placement = Placement;
            _main.Popup.Reposition();
        }
    }

    /// <summary>
    /// The effort layout or "More models" changed: every list is rebuilt; an open menu refits its width, drops a submenu
    /// or page of the old layout and shows the new main list (keeping the keyboard position on the same entry).
    /// </summary>
    private void OnLayoutChanged()
    {
        _version++;
        if (!IsMainShown || _mainPanel is not { } main)
        {
            return;
        }
        CloseSubmenu(animated: false);
        main.IsInline = IsInline;
        main.IsSimple = IsSimple;
        if (TopLevel.GetTopLevel(main) is { } topLevel)
        {
            FitMain(topLevel.ClientSize);
        }
        if (main.Page != ModelMenuPage.Main)
        {
            main.ShowView(View(ViewSlot.Main), 0);
        }
        else
        {
            RefreshShown();
        }
    }

    private void OpenPending()
    {
        if (_pendingOpen && IsOpen)
        {
            OpenCore();
        }
    }

    private void EnsureMain()
    {
        if (_main is not null)
        {
            return;
        }
        _mainPanel = new ModelMenuPanel(this, ModelMenuPage.Main);
        _mainPanel.SizeChanged += OnMainSizeChanged;
        ListenToPointer(_mainPanel);
        _main = new ModelMenuPresence(_mainPanel, new ModelMenuPopup(isSubmenu: false));
        _main.Shown += OnMainShown;
        _main.Hidden += OnMainHidden;
        LogicalChildren.Add(_main.Popup);
    }

    private ModelMenuPresence EnsureSubmenu(ModelMenuPage page)
    {
        var existing = page == ModelMenuPage.Effort ? _effortSub : _moreSub;
        if (existing is not null)
        {
            return existing;
        }
        var panel = new ModelMenuPanel(this, page);
        ListenToPointer(panel);
        var presence = new ModelMenuPresence(panel, new ModelMenuPopup(isSubmenu: true));
        presence.Hidden += (_, _) => panel.SetActive(null, ModelMenuMove.Keyboard);
        LogicalChildren.Add(presence.Popup);
        if (page == ModelMenuPage.Effort)
        {
            _effortSub = presence;
        }
        else
        {
            _moreSub = presence;
        }
        return presence;
    }

    private void OpenCore()
    {
        if (PlacementTarget is not { } anchor || TopLevel.GetTopLevel(anchor) is not { } topLevel)
        {
            _pendingOpen = true;
            return;
        }
        _pendingOpen = false;
        EnsureMain();
        var paged = topLevel.ClientSize.Width <= PagedBelowWidth;
        if (paged != _paged)
        {
            _paged = paged;
            _version++;
        }
        var panel = _mainPanel!;
        panel.IsInline = IsInline;
        panel.IsSimple = IsSimple;
        panel.ShowView(View(ViewSlot.Main), 0);
        if (panel.Scroll is { } scroll)
        {
            scroll.Offset = default;
        }
        AutomationProperties.SetName(panel, Labels.Menu);
        FitMain(topLevel.ClientSize);
        var popup = _main!.Popup;
        popup.PlacementTarget = anchor;
        // Presses on the anchor reach it (the trigger toggles the menu itself); outside presses are handled by Hook.
        popup.OverlayInputPassThroughElement = anchor;
        popup.MarginToAnchor = AnchorGap;
        popup.CollisionPadding = ViewportInset / 2;
        if (popup.RequestedPlacement != Placement)
        {
            popup.Placement = Placement;
        }
        Hook(topLevel);
        _keyboard = _pendingFocus != ModelMenuOpenFocus.Surface;
        var reversing = _main.IsClosing;
        _main.Show();
        if (reversing)
        {
            PostOpenFocus();
        }
    }

    private void OnMainShown(object? sender, EventArgs e)
    {
        PostOpenFocus();
        RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
    }

    private void PostOpenFocus()
    {
        var focus = _pendingFocus;
        Dispatcher.UIThread.Post(() => ApplyOpenFocus(focus), DispatcherPriority.Loaded);
    }

    private void ApplyOpenFocus(ModelMenuOpenFocus focus)
    {
        if (!IsMainShown || _mainPanel is not { } panel)
        {
            return;
        }
        var rows = panel.Rows;
        ModelMenuItem? row = focus switch
        {
            ModelMenuOpenFocus.Selected => SelectedRowIn(panel.View) ?? First(rows),
            ModelMenuOpenFocus.First => First(rows),
            ModelMenuOpenFocus.Last => rows.Count > 0 ? rows[^1] : null,
            _ => null
        };
        if (row is null)
        {
            panel.Focus(NavigationMethod.Pointer);
            return;
        }
        _keyboard = true;
        FocusRow(panel, row);
    }

    private void CloseCore()
    {
        _pendingOpen = false;
        var returnFocus = _closeReturnFocus;
        var method = _closeMethod;
        _closeReturnFocus = true;
        _closeMethod = NavigationMethod.Pointer;
        CancelHover();
        _zone = null;
        _pointerPanel = null;
        _lastPointerRow = null;
        _hasPointer = false;
        if (_main is not { } main || !main.Popup.IsOpen)
        {
            Unhook();
            return;
        }
        var focusInside = IsFocusInside();
        Unhook();
        CloseSubmenu(animated: true);
        // The highlight fades with the surface (Arc: the chosen row's highlight leaves in 80ms).
        _mainPanel?.SetActive(null, ModelMenuMove.Pointer);
        // Focus goes back before the surface hides: hiding detaches the focused row, and the focus manager then
        // restores focus to the anchor with an unspecified method (no keyboard ring), after which asking again is a no-op.
        if (PlacementTarget is { } anchor && (returnFocus || focusInside))
        {
            anchor.Focus(method);
        }
        main.Hide();
    }

    private void OnMainHidden(object? sender, EventArgs e)
    {
        _mainPanel?.SetActive(null, ModelMenuMove.Keyboard);
        RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
    }

    private bool IsFocusInside() =>
        _mainPanel?.IsKeyboardFocusWithin == true || _effortSub?.Panel.IsKeyboardFocusWithin == true ||
        _moreSub?.Panel.IsKeyboardFocusWithin == true;

    private void FitMain(Size client)
    {
        if (_mainPanel is not null)
        {
            var room = Math.Max(0, client.Width - ViewportInset);
            if (IsSimple)
            {
                // As wide as its rows, at least the simple minimum, never wider than the window allows.
                _mainPanel.Width = double.NaN;
                _mainPanel.MinWidth = Math.Min(SimpleMinWidth, room);
                _mainPanel.MaxWidth = room;
                return;
            }
            var width = IsInline ? InlineSurfaceWidth : SurfaceWidth;
            _mainPanel.MinWidth = 0;
            _mainPanel.MaxWidth = double.PositiveInfinity;
            _mainPanel.Width = Math.Min(width, room);
        }
    }

    private void OnMainSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // The positioner does not follow size changes: a menu above its anchor (or a page that changes the height) must
        // stay attached to it.
        if (IsMainShown)
        {
            _main!.Popup.Reposition();
            OpenSubPresence?.Popup.Reposition();
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        // The menu left the window: everything goes without its exit.
        CancelHover();
        Unhook();
        _effortSub?.Hide(immediate: true);
        _moreSub?.Hide(immediate: true);
        _main?.Hide(immediate: true);
        _openSub = null;
        _subTrigger = null;
        if (IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    #endregion

    #region Window hooks

    private void Hook(TopLevel topLevel)
    {
        if (ReferenceEquals(_hooked, topLevel))
        {
            return;
        }
        Unhook();
        _hooked = topLevel;
        topLevel.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(PointerMovedEvent, OnTopLevelPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Bubble);
        _clientSizeSubscription = topLevel.GetObservable(TopLevel.ClientSizeProperty).Subscribe(new ClientSizeObserver(this));
    }

    /// <summary>
    /// Pointer input over a surface stays inside its popup host (its route ends at the popup, not the window), so every
    /// panel reports its moves too; the window reports the moves everywhere else.
    /// </summary>
    private void ListenToPointer(ModelMenuPanel panel)
    {
        panel.AddHandler(PointerMovedEvent, OnTopLevelPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        panel.AddHandler(PointerPressedEvent, OnPanelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnPanelPointerPressed(object? sender, PointerPressedEventArgs e) => _keyboard = false;

    private void Unhook()
    {
        _clientSizeSubscription?.Dispose();
        _clientSizeSubscription = null;
        if (_hooked is not { } topLevel)
        {
            return;
        }
        topLevel.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        topLevel.RemoveHandler(PointerMovedEvent, OnTopLevelPointerMoved);
        topLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _hooked = null;
    }

    private void OnClientSizeChanged(Size size)
    {
        if (!IsMainShown)
        {
            return;
        }
        FitMain(size);
        // A submenu placed for the old window could now hang outside it.
        CloseSubmenu(animated: false);
    }

    private bool IsWithinMenu(Visual visual)
    {
        foreach (var panel in Panels())
        {
            if (ReferenceEquals(panel, visual) || panel.IsVisualAncestorOf(visual))
            {
                return true;
            }
        }
        return false;
    }

    private IEnumerable<ModelMenuPanel> Panels()
    {
        if (_mainPanel is not null)
        {
            yield return _mainPanel;
        }
        if (_effortSub is not null)
        {
            yield return _effortSub.Panel;
        }
        if (_moreSub is not null)
        {
            yield return _moreSub.Panel;
        }
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsMainShown || e.Source is not Visual source)
        {
            return;
        }
        if (IsWithinMenu(source))
        {
            _keyboard = false;
            return;
        }
        // A press on the anchor passes through: the trigger toggles the menu itself.
        if (PlacementTarget is { } anchor && (ReferenceEquals(anchor, source) || anchor.IsVisualAncestorOf(source)))
        {
            return;
        }
        CloseFrom(returnFocus: false, NavigationMethod.Pointer);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        // Keys from focus inside the menu are handled by its panels; this catches Escape with focus elsewhere.
        if (!e.Handled && e.Key == Key.Escape && IsMainShown)
        {
            e.Handled = true;
            CloseFrom(returnFocus: true, NavigationMethod.Tab);
        }
    }

    #endregion

    #region Pointer

    private void OnTopLevelPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsMainShown || _hooked is not { } topLevel || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var point = e.GetPosition(topLevel);
        var dx = _hasPointer ? point.X - _lastPointer.X : 0;
        if (_hasPointer && point == _lastPointer)
        {
            return;
        }
        _lastPointer = point;
        _hasPointer = true;
        OnPointerAt(point, dx);
    }

    /// <summary>
    /// One pointer move (top-level coordinates): highlight the row under it, open a submenu after the hover delay, keep an
    /// open submenu while the pointer heads for it through the safe zone, close it when the pointer settles on another row
    /// or leaves both surfaces.
    /// </summary>
    private void OnPointerAt(Point point, double dx)
    {
        var topLevel = _hooked!;
        var main = _mainPanel!;
        var sub = OpenSubPresence;
        if (sub is not null && sub.Panel.Contains(point, topLevel))
        {
            _keyboard = false;
            _zone = null;
            CancelHover();
            Highlight(sub.Panel, sub.Panel.RowAt(point, topLevel));
            if (!ReferenceEquals(main.Active, _subTrigger) && _subTrigger is not null)
            {
                main.SetActive(_subTrigger, ModelMenuMove.Pointer);
            }
            _pointerPanel = sub.Panel;
            _lastPointerRow = null;
            return;
        }
        if (sub is not null && ReferenceEquals(_pointerPanel, sub.Panel))
        {
            // Off the submenu: its highlight leaves (the pointer may be on its way back to the menu).
            Highlight(sub.Panel, null);
        }
        if (main.Contains(point, topLevel))
        {
            _keyboard = false;
            var row = main.RowAt(point, topLevel);
            if (sub is not null)
            {
                if (ReferenceEquals(row, _subTrigger))
                {
                    _zone = null;
                    if (!ReferenceEquals(main.Active, row))
                    {
                        main.SetActive(row, ModelMenuMove.Pointer);
                    }
                    Track(main, row);
                    return;
                }
                if (ReferenceEquals(_lastPointerRow, _subTrigger) && ReferenceEquals(_pointerPanel, main))
                {
                    StartZone(point, dx, sub);
                }
                if (IsInZone(point, dx))
                {
                    Track(main, row);
                    return;
                }
                CloseSubmenu(animated: true);
            }
            Highlight(main, row);
            if (row is ModelMenuActionRow { HasSubmenu: true } action)
            {
                ScheduleSubmenu(action);
            }
            else
            {
                CancelHover();
            }
            Track(main, row);
            return;
        }
        // Outside both surfaces: only a pointer that was over the menu leaves it (keyboard use is left alone).
        if (_pointerPanel is null)
        {
            return;
        }
        if (sub is not null)
        {
            if (ReferenceEquals(_lastPointerRow, _subTrigger) && ReferenceEquals(_pointerPanel, main))
            {
                StartZone(point, dx, sub);
            }
            if (IsInZone(point, dx))
            {
                Track(main, null);
                return;
            }
            CloseSubmenu(animated: true);
        }
        CancelHover();
        Highlight(main, null);
        _pointerPanel = null;
        _lastPointerRow = null;
    }

    private void Track(ModelMenuPanel panel, ModelMenuItem? row)
    {
        _pointerPanel = panel;
        _lastPointerRow = row;
    }

    /// <summary>The pointer drives the highlight of <paramref name="panel"/> (and focus follows it, as in a native menu).</summary>
    private void Highlight(ModelMenuPanel panel, ModelMenuItem? row)
    {
        if (ReferenceEquals(panel.Active, row))
        {
            return;
        }
        panel.SetActive(row, ModelMenuMove.Pointer);
        if (row is not null)
        {
            row.Focus(NavigationMethod.Pointer);
        }
        else if (panel.IsKeyboardFocusWithin)
        {
            panel.Focus(NavigationMethod.Pointer);
        }
    }

    private void StartZone(Point exit, double dx, ModelMenuPresence sub)
    {
        if (_hooked is not { } topLevel || sub.Panel.TranslatePoint(default, topLevel) is not { } origin)
        {
            return;
        }
        var zone = new ModelMenuSafeZone(exit, new Rect(origin, sub.Panel.Bounds.Size), !sub.Popup.OpensLeft);
        _zone = zone;
        _zoneUntil = Now() + ModelMenuSafeZone.Lifetime;
        _towardSubmenu = zone.IsToward(dx, true);
    }

    private bool IsInZone(Point point, double dx)
    {
        if (_zone is not { } zone)
        {
            return false;
        }
        if (Now() > _zoneUntil)
        {
            _zone = null;
            return false;
        }
        _towardSubmenu = zone.IsToward(dx, _towardSubmenu);
        if (_towardSubmenu && zone.Contains(point))
        {
            return true;
        }
        _zone = null;
        return false;
    }

    private void ScheduleSubmenu(ModelMenuActionRow row)
    {
        if (_openSub == PageOf(row.Kind) && OpenSubPresence is not null)
        {
            return;
        }
        if (ReferenceEquals(_hoverRow, row) && _hoverTimer is { IsEnabled: true })
        {
            return;
        }
        _hoverRow = row;
        _hoverTimer ??= new DispatcherTimer(HoverOpenDelay, DispatcherPriority.Input, OnHoverElapsed);
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void CancelHover()
    {
        _hoverTimer?.Stop();
        _hoverRow = null;
    }

    private void OnHoverElapsed(object? sender, EventArgs e)
    {
        _hoverTimer?.Stop();
        var row = _hoverRow;
        _hoverRow = null;
        if (row is not null && IsMainShown && ReferenceEquals(_mainPanel?.Active, row))
        {
            OpenSubmenuFor(row.Kind, focusInside: false, NavigationMethod.Pointer, fromHover: true);
        }
    }

    #endregion

    #region Keyboard

    /// <summary>The menu keyboard model, for keys from focus inside any of the panels.</summary>
    internal void OnPanelKeyDown(ModelMenuPanel panel, KeyEventArgs e)
    {
        if (!IsMainShown)
        {
            return;
        }
        _keyboard = true;
        CancelHover();
        _zone = null;
        var rows = panel.Rows;
        var current = panel.Active is { } active ? IndexOf(rows, active) : -1;
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
            case Key.Home:
            case Key.End:
            {
                e.Handled = true;
                if (rows.Count == 0)
                {
                    return;
                }
                var next = e.Key switch
                {
                    Key.Down => current < 0 ? 0 : (current + 1) % rows.Count,
                    Key.Up => current < 0 ? rows.Count - 1 : (current - 1 + rows.Count) % rows.Count,
                    Key.Home => 0,
                    _ => rows.Count - 1
                };
                MoveKeyboard(panel, rows[next]);
                return;
            }
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                if (panel.Active is { } row)
                {
                    OnRowInvoked(panel, row, NavigationMethod.Tab);
                }
                return;
            case Key.Right:
                e.Handled = true;
                if (panel.Active is ModelMenuEffortStrip forward)
                {
                    StepEffort(panel, forward, 1);
                    return;
                }
                if (ReferenceEquals(panel, _mainPanel) && panel.Page == ModelMenuPage.Main &&
                    panel.Active is ModelMenuActionRow { Kind: ModelMenuRowKind.Effort or ModelMenuRowKind.More } opener)
                {
                    OpenSubmenuFor(opener.Kind, focusInside: true, NavigationMethod.Tab);
                }
                return;
            case Key.Left:
                e.Handled = true;
                if (panel.Active is ModelMenuEffortStrip back)
                {
                    StepEffort(panel, back, -1);
                    return;
                }
                if (!ReferenceEquals(panel, _mainPanel))
                {
                    var trigger = _subTrigger;
                    CloseSubmenu(animated: true);
                    if (trigger is not null && _mainPanel is not null)
                    {
                        FocusRow(_mainPanel, trigger);
                    }
                }
                else if (panel.Page != ModelMenuPage.Main)
                {
                    GoBack(NavigationMethod.Tab);
                }
                return;
            case Key.Escape:
                e.Handled = true;
                CloseFrom(returnFocus: true, NavigationMethod.Tab);
                return;
            case Key.Tab:
                // Focus stays in the menu (Arc's menu traps Tab).
                e.Handled = true;
                return;
        }
        if (e.KeySymbol is { Length: > 0 } symbol && !char.IsControl(symbol[0]) && !char.IsWhiteSpace(symbol[0]) &&
            (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt)) == 0)
        {
            e.Handled = true;
            var names = new string?[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                names[i] = rows[i].TypeaheadText;
            }
            if (panel.Typeahead.Next(symbol, names, current) is { } match)
            {
                MoveKeyboard(panel, rows[match]);
            }
        }
    }

    private void MoveKeyboard(ModelMenuPanel panel, ModelMenuItem row)
    {
        if (ReferenceEquals(panel, _mainPanel) && _openSub is not null && !ReferenceEquals(row, _subTrigger))
        {
            // The keyboard left the row of the open submenu: it closes, as when focus leaves it in a native menu.
            CloseSubmenu(animated: true);
        }
        FocusRow(panel, row);
    }

    /// <summary>The keyboard position goes to <paramref name="row"/>: the highlight jumps, focus follows, it scrolls into view.</summary>
    private static void FocusRow(ModelMenuPanel panel, ModelMenuItem row)
    {
        panel.SetActive(row, ModelMenuMove.Keyboard);
        row.Focus(NavigationMethod.Directional);
    }

    private static int IndexOf(IReadOnlyList<ModelMenuItem> rows, ModelMenuItem row)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i], row))
            {
                return i;
            }
        }
        return -1;
    }

    #endregion

    #region Choosing

    /// <summary>A row was chosen: a click, Enter / Space, or automation.</summary>
    internal void OnRowInvoked(ModelMenuPanel panel, ModelMenuItem row, NavigationMethod method)
    {
        if (!IsMainShown)
        {
            return;
        }
        var pointer = method == NavigationMethod.Pointer;
        _keyboard = !pointer;
        var closeMethod = pointer ? NavigationMethod.Pointer : NavigationMethod.Tab;
        switch (row)
        {
            case ModelMenuModelRow model:
                Pick(model.Model, closeMethod);
                break;
            case ModelMenuEffortRow level:
                Pick(level.Level, closeMethod);
                break;
            case ModelMenuEffortStrip strip:
                // Enter, Space or Invoke on the segmented row: the level stays, it is reported, the menu stays open.
                if ((strip.Level ?? Effort) is { } current)
                {
                    PickInline(current);
                }
                break;
            case ModelMenuActionRow { IsBack: true }:
                GoBack(method);
                break;
            case ModelMenuActionRow action:
                CancelHover();
                OpenSubmenuFor(action.Kind, focusInside: !pointer, method);
                break;
        }
    }

    /// <summary>
    /// ModelChosen, then (when it changed) the selection, then the effort snap, then the menu closes. The snap waits until
    /// everyone has seen the new selection, so EffortChosen never arrives before the SelectedModel change.
    /// </summary>
    private void Pick(AiModel model, NavigationMethod closeMethod)
    {
        var previous = SelectedModel;
        RaiseEvent(new ModelMenuModelEventArgs(ModelChosenEvent, this, model, previous));
        if (!ReferenceEquals(model, previous))
        {
            _picking = true;
            _deferSnap = true;
            try
            {
                SetCurrentValue(SelectedModelProperty, model);
            }
            finally
            {
                _deferSnap = false;
            }
            try
            {
                if (ReferenceEquals(SelectedModel, model))
                {
                    SnapEffort(model, raise: true);
                }
            }
            finally
            {
                _picking = false;
            }
        }
        CloseFrom(returnFocus: true, closeMethod);
    }

    /// <summary>The effort changes, EffortChosen reports it (also for the current level), then the menu closes.</summary>
    private void Pick(ModelEffort level, NavigationMethod closeMethod)
    {
        var previous = Effort;
        _snapping = true;
        _picking = true;
        try
        {
            SetCurrentValue(EffortProperty, level);
        }
        finally
        {
            _snapping = false;
            _picking = false;
        }
        RaiseEvent(new ModelMenuEffortEventArgs(EffortChosenEvent, this, level, previous, isSnap: false));
        CloseFrom(returnFocus: true, closeMethod);
    }

    /// <summary>
    /// The inline layout's choice of a level: the effort changes (the segmented row and the header update in place), then
    /// EffortChosen reports it (also for the current level); the menu stays open.
    /// </summary>
    private void PickInline(ModelEffort level)
    {
        var previous = Effort;
        _snapping = true;
        try
        {
            SetCurrentValue(EffortProperty, level);
        }
        finally
        {
            _snapping = false;
        }
        RaiseEvent(new ModelMenuEffortEventArgs(EffortChosenEvent, this, level, previous, isSnap: false));
    }

    /// <summary>← / → on the segmented row: the next level in that direction (wrapping) applies at once.</summary>
    private void StepEffort(ModelMenuPanel panel, ModelMenuEffortStrip strip, int delta)
    {
        if (strip.Step(delta) is { } level)
        {
            FocusRow(panel, strip);
            PickInline(level);
        }
    }

    /// <summary>A level of the segmented row was clicked (or invoked by automation).</summary>
    internal void OnEffortCellChosen(ModelMenuPanel panel, ModelMenuEffortStrip strip, ModelEffort level, NavigationMethod method)
    {
        if (!IsMainShown)
        {
            return;
        }
        var pointer = method == NavigationMethod.Pointer;
        _keyboard = !pointer;
        if (pointer && !ReferenceEquals(panel.Active, strip))
        {
            panel.SetActive(strip, ModelMenuMove.Pointer);
            strip.Focus(NavigationMethod.Pointer);
        }
        PickInline(level);
    }

    #endregion

    #region Submenus and pages

    private static ModelMenuPage PageOf(ModelMenuRowKind kind) => kind == ModelMenuRowKind.Effort ? ModelMenuPage.Effort : ModelMenuPage.More;

    /// <summary>
    /// Opens the effort or More models list: a submenu beside the menu (right, else left when only that side has room),
    /// or a page inside the menu in a narrow window or when neither side has room (not for a hover). With
    /// <paramref name="focusInside"/> (a key) the keyboard position moves to the selected entry (the first one when none
    /// is selected).
    /// </summary>
    private void OpenSubmenuFor(ModelMenuRowKind kind, bool focusInside, NavigationMethod method, bool fromHover = false)
    {
        if (_mainPanel is not { } main || kind is not (ModelMenuRowKind.Effort or ModelMenuRowKind.More))
        {
            return;
        }
        var page = PageOf(kind);
        if (_paged)
        {
            ShowPage(page, method);
            return;
        }
        var trigger = ActionRow(main.View, kind);
        if (trigger is null)
        {
            return;
        }
        if (_openSub == page && OpenSubPresence is { } open)
        {
            if (focusInside)
            {
                EnterSubmenu(open.Panel);
            }
            return;
        }
        CloseSubmenu(animated: true);
        var presence = EnsureSubmenu(page);
        var panel = presence.Panel;
        panel.Width = page == ModelMenuPage.Effort ? EffortMenuWidth : MoreMenuWidth;
        panel.ShowView(View(page == ModelMenuPage.Effort ? ViewSlot.EffortSubmenu : ViewSlot.MoreSubmenu), 0);
        AutomationProperties.SetName(panel, page == ModelMenuPage.Effort ? Labels.EffortMenu : Labels.MoreModelsMenu);
        if (ChooseSide(panel.Width) is not { } opensLeft)
        {
            // No room on either side: a click or a key opens the list as a page inside the menu; resting the pointer
            // on the row never changes the page.
            if (!fromHover)
            {
                ShowPage(page, method);
            }
            return;
        }
        presence.Popup.SubmenuAnchor = topLevel => SubmenuAnchorFor(trigger, topLevel);
        presence.Popup.ConfigureSubmenu(main, opensLeft, SubmenuGap, SubmenuOffset);
        _openSub = page;
        _subTrigger = trigger;
        trigger.IsExpanded = true;
        presence.Show();
        if (focusInside)
        {
            EnterSubmenu(panel);
        }
        else if (!ReferenceEquals(main.Active, trigger))
        {
            main.SetActive(trigger, ModelMenuMove.Pointer);
        }
    }

    /// <summary>The keyboard moves into a submenu: its selected entry takes focus and the menu's highlight goes (Arc).</summary>
    private void EnterSubmenu(ModelMenuPanel panel)
    {
        _mainPanel?.SetActive(null, ModelMenuMove.Keyboard);
        if ((SelectedRowIn(panel.View) ?? First(panel.Rows)) is { } row)
        {
            FocusRow(panel, row);
        }
        else
        {
            panel.Focus(NavigationMethod.Tab);
        }
    }

    /// <summary>The menu's own box, at the height of <paramref name="trigger"/>: a submenu placed against it lines up with the row.</summary>
    private Rect? SubmenuAnchorFor(ModelMenuActionRow trigger, TopLevel topLevel)
    {
        if (_mainPanel is not { } main || main.TranslatePoint(default, topLevel) is not { } surface ||
            trigger.TranslatePoint(default, topLevel) is not { } row)
        {
            return null;
        }
        return new Rect(surface.X, row.Y, main.Bounds.Width, trigger.Bounds.Height);
    }

    /// <summary>False: right of the menu, true: left of it, null: no room on either side.</summary>
    private bool? ChooseSide(double width)
    {
        if (_mainPanel is not { } main || TopLevel.GetTopLevel(main) is not { } topLevel ||
            main.TranslatePoint(default, topLevel) is not { } surface)
        {
            return false;
        }
        var client = topLevel.ClientSize.Width;
        if (client - (surface.X + main.Bounds.Width) - SubmenuGap >= width)
        {
            return false;
        }
        if (surface.X - SubmenuGap >= width)
        {
            return true;
        }
        return null;
    }

    private void CloseSubmenu(bool animated)
    {
        if (_openSub is null)
        {
            return;
        }
        var presence = _openSub == ModelMenuPage.Effort ? _effortSub : _moreSub;
        var focusInside = presence?.Panel.IsKeyboardFocusWithin == true;
        _openSub = null;
        if (_subTrigger is not null)
        {
            _subTrigger.IsExpanded = false;
        }
        _subTrigger = null;
        _zone = null;
        presence?.Hide(immediate: !animated);
        if (focusInside && _mainPanel is { } main)
        {
            // Focus never stays in a leaving submenu.
            if (main.Active is { } row)
            {
                row.Focus(NavigationMethod.Pointer);
            }
            else
            {
                main.Focus(NavigationMethod.Pointer);
            }
        }
    }

    /// <summary>Automation collapsed a submenu.</summary>
    internal void CloseSubmenuFromAutomation() => CloseSubmenu(animated: true);

    /// <summary>A narrow window: the menu shows the list as a page (back row first) that enters from the right.</summary>
    private void ShowPage(ModelMenuPage page, NavigationMethod method)
    {
        if (_mainPanel is not { } main)
        {
            return;
        }
        CloseSubmenu(animated: false);
        var view = View(page == ModelMenuPage.Effort ? ViewSlot.EffortPage : ViewSlot.MorePage);
        main.ShowView(view, 1);
        AutomationProperties.SetName(main, page == ModelMenuPage.Effort ? Labels.EffortMenu : Labels.MoreModelsMenu);
        if (method == NavigationMethod.Pointer)
        {
            PointerAfterPageChange(main);
            return;
        }
        // Unlike Arc, the highlight shows on the focused row right away.
        if ((SelectedRowIn(view) ?? FirstAfterBack(view)) is { } row)
        {
            FocusRow(main, row);
        }
    }

    /// <summary>Back from a page to the main list; the keyboard position returns to the row that opened the page.</summary>
    private void GoBack(NavigationMethod method)
    {
        if (_mainPanel is not { } main || main.Page == ModelMenuPage.Main)
        {
            return;
        }
        var from = main.Page;
        var view = View(ViewSlot.Main);
        main.ShowView(view, -1);
        AutomationProperties.SetName(main, Labels.Menu);
        if (method == NavigationMethod.Pointer)
        {
            PointerAfterPageChange(main);
            return;
        }
        if ((ActionRow(view, from == ModelMenuPage.Effort ? ModelMenuRowKind.Effort : ModelMenuRowKind.More) ?? First(view.Rows)) is { } row)
        {
            FocusRow(main, row);
        }
    }

    /// <summary>After a click changed the page, the row under the resting pointer is highlighted once the page is laid out.</summary>
    private void PointerAfterPageChange(ModelMenuPanel panel)
    {
        panel.Focus(NavigationMethod.Pointer);
        _lastPointerRow = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (IsMainShown && _hasPointer && _hooked is { } topLevel && panel.RowAt(_lastPointer, topLevel) is { } row)
            {
                Highlight(panel, row);
                Track(panel, row);
            }
        }, DispatcherPriority.Loaded);
    }

    private ModelMenuItem? SelectedRowIn(ModelMenuPageView? view)
    {
        if (view is null)
        {
            return null;
        }
        foreach (var row in view.Rows)
        {
            if (row is ModelMenuModelRow model && ReferenceEquals(model.Model, SelectedModel) ||
                row is ModelMenuEffortRow level && level.Level == Effort)
            {
                return row;
            }
        }
        return null;
    }

    private static ModelMenuItem? First(IReadOnlyList<ModelMenuItem> rows) => rows.Count > 0 ? rows[0] : null;

    private static ModelMenuItem? FirstAfterBack(ModelMenuPageView view)
    {
        foreach (var row in view.Rows)
        {
            if (!ReferenceEquals(row, view.BackRow))
            {
                return row;
            }
        }
        return view.BackRow;
    }

    private static ModelMenuActionRow? ActionRow(ModelMenuPageView? view, ModelMenuRowKind kind)
    {
        if (view is null)
        {
            return null;
        }
        foreach (var row in view.Rows)
        {
            if (row is ModelMenuActionRow action && action.Kind == kind)
            {
                return action;
            }
        }
        return null;
    }

    #endregion

    private sealed class ClientSizeObserver(ModelMenu owner) : IObserver<Size>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Size value) => owner.OnClientSizeChanged(value);
    }
}
