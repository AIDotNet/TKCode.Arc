using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// What the prompt studio's two popovers (the version menu and the model menu) share: a control that takes no room in the
/// layout and owns a popover anchored 6px below <see cref="PlacementTarget"/> (the header pill), left or right edges
/// aligned, with Arc's popover motion (<see cref="PromptPopoverPresence"/>). Place it anywhere in the studio's tree (the
/// popover inherits its theme from there) and let the pill's <c>OpenRequested</c> call <see cref="Toggle"/>; mirror
/// <see cref="IsOpen"/> to the pill's <c>IsOpen</c>.
/// <para>
/// The popover is a non-modal dialog (Radix popover): a press outside it closes it without moving focus; Escape closes it
/// and gives focus back to the pill (with its keyboard ring); Tab and Shift+Tab cycle through its stops
/// (<see cref="TabStops"/>) instead of leaving it. Focus goes back to the pill before the surface hides, because hiding
/// detaches the focused element and the focus manager would put focus back without a method.
/// </para>
/// </summary>
internal abstract class PromptPopoverMenu : Control
{
    /// <summary>The popover sits this far below the pill.</summary>
    internal const double AnchorGap = 6;

    /// <summary>Room the popover keeps from the window's left and right edges.</summary>
    internal const double CollisionPadding = 8;

    /// <summary>Whether the popover is open (two-way): true opens it with nothing focused for the keys yet, false closes it.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<PromptPopoverMenu, bool>(nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The pill the popover hangs from (and gives focus back to).</summary>
    public static readonly StyledProperty<Control?> PlacementTargetProperty =
        AvaloniaProperty.Register<PromptPopoverMenu, Control?>(nameof(PlacementTarget));

    /// <summary>The accessible name of the popover (the dialog), such as "Versions".</summary>
    public static readonly StyledProperty<string?> DialogNameProperty =
        AvaloniaProperty.Register<PromptPopoverMenu, string?>(nameof(DialogName));

    /// <summary>Raised when the popover is shown.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenedEvent =
        RoutedEvent.Register<PromptPopoverMenu, RoutedEventArgs>(nameof(Opened), RoutingStrategies.Bubble);

    /// <summary>Raised when the popover has closed (after its exit).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<PromptPopoverMenu, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    private PromptPopoverPresence? _presence;
    private PromptPopoverSurface? _surface;
    private TopLevel? _hooked;
    private ModelMenuOpenFocus _pendingFocus = ModelMenuOpenFocus.Surface;
    private bool _closeReturnFocus;
    private NavigationMethod _closeMethod = NavigationMethod.Pointer;
    private bool _syncing;

    static PromptPopoverMenu()
    {
        IsVisibleProperty.OverrideDefaultValue<PromptPopoverMenu>(false);
        FocusableProperty.OverrideDefaultValue<PromptPopoverMenu>(false);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="PlacementTargetProperty"/>
    public Control? PlacementTarget { get => GetValue(PlacementTargetProperty); set => SetValue(PlacementTargetProperty, value); }

    /// <inheritdoc cref="DialogNameProperty"/>
    public string? DialogName { get => GetValue(DialogNameProperty); set => SetValue(DialogNameProperty, value); }

    /// <inheritdoc cref="OpenedEvent"/>
    public event EventHandler<RoutedEventArgs>? Opened { add => AddHandler(OpenedEvent, value); remove => RemoveHandler(OpenedEvent, value); }

    /// <inheritdoc cref="ClosedEvent"/>
    public event EventHandler<RoutedEventArgs>? Closed { add => AddHandler(ClosedEvent, value); remove => RemoveHandler(ClosedEvent, value); }

    /// <summary>The surface (null until the first open).</summary>
    internal PromptPopoverSurface? Surface => _surface;

    /// <summary>The popup and its motion (null until the first open).</summary>
    internal PromptPopoverPresence? Presence => _presence;

    /// <summary>Whether the popover is on screen and not leaving.</summary>
    internal bool IsShown => _presence is { IsShown: true };

    /// <summary>The keys were used last (Escape and choices give focus back with the pill's keyboard ring).</summary>
    protected bool UsesKeyboard { get; set; }

    /// <summary>Left edges aligned (the version menu) or right edges aligned (the model menu).</summary>
    protected abstract PlacementMode Placement { get; }

    /// <summary>The surface's width (352 or 320).</summary>
    protected abstract double SurfaceWidth { get; }

    /// <summary>Builds the content of the surface (once).</summary>
    protected abstract Control CreateContent();

    /// <summary>Brings the content up to date before the popover shows.</summary>
    protected abstract void OnOpening();

    /// <summary>Puts focus where the popover starts once it is laid out (the checked row).</summary>
    protected abstract void ApplyOpenFocus(ModelMenuOpenFocus focus);

    /// <summary>The stops Tab cycles through, in order (hidden, disabled and unfocusable ones are skipped).</summary>
    protected abstract IEnumerable<Control> TabStops();

    /// <summary>The popover closed (after its exit, or at once).</summary>
    protected virtual void OnHidden()
    {
    }

    #region Open and close

    /// <summary>Opens the popover. <paramref name="focus"/>: a pointer (<see cref="ModelMenuOpenFocus.Surface"/>) or a key.</summary>
    public void Open(ModelMenuOpenFocus focus = ModelMenuOpenFocus.Selected)
    {
        _pendingFocus = focus;
        if (IsOpen)
        {
            if (IsShown && focus != ModelMenuOpenFocus.Surface)
            {
                UsesKeyboard = true;
                ApplyOpenFocus(focus);
            }
            else if (!IsShown)
            {
                OpenCore();
            }
            return;
        }
        Sync(true);
        OpenCore();
    }

    /// <summary>Opens the popover, or closes it when open (the pill's press or key).</summary>
    public void Toggle(ModelMenuOpenFocus focus = ModelMenuOpenFocus.Selected)
    {
        if (IsOpen)
        {
            Close(returnFocus: true, focus == ModelMenuOpenFocus.Surface ? NavigationMethod.Pointer : NavigationMethod.Tab);
        }
        else
        {
            Open(focus);
        }
    }

    /// <summary>
    /// Closes the popover. With <paramref name="returnFocus"/> (and always when focus is inside it) the pill takes focus
    /// back by <paramref name="method"/> (Tab shows its keyboard ring; null: the keys' ring when the keys were used last).
    /// </summary>
    public void Close(bool returnFocus = true, NavigationMethod? method = null)
    {
        if (!IsOpen)
        {
            return;
        }
        _closeReturnFocus = returnFocus;
        _closeMethod = method ?? (UsesKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        Sync(false);
        CloseCore();
    }

    private void Sync(bool open)
    {
        _syncing = true;
        try
        {
            SetCurrentValue(IsOpenProperty, open);
        }
        finally
        {
            _syncing = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty && !_syncing)
        {
            if (change.GetNewValue<bool>())
            {
                _pendingFocus = ModelMenuOpenFocus.Surface;
                OpenCore();
            }
            else
            {
                _closeReturnFocus = false;
                _closeMethod = NavigationMethod.Pointer;
                CloseCore();
            }
        }
        else if (change.Property == DialogNameProperty && _surface is not null)
        {
            AutomationProperties.SetName(_surface, DialogName);
        }
    }

    private void EnsurePresence()
    {
        if (_presence is not null)
        {
            return;
        }
        _surface = new PromptPopoverSurface { Owner = this, Content = CreateContent() };
        _surface.SizeChanged += (_, _) =>
        {
            if (IsShown)
            {
                _presence!.Popup.Reposition();
            }
        };
        _presence = new PromptPopoverPresence(_surface, new ModelMenuPopup(isSubmenu: false));
        _presence.Shown += OnShown;
        _presence.Hidden += OnPresenceHidden;
        LogicalChildren.Add(_presence.Popup);
    }

    private void OpenCore()
    {
        if (PlacementTarget is not { } anchor || TopLevel.GetTopLevel(anchor) is not { } topLevel)
        {
            // Nothing to hang from: stay closed.
            Sync(false);
            return;
        }
        EnsurePresence();
        var surface = _surface!;
        surface.Width = Math.Min(SurfaceWidth, Math.Max(0, topLevel.ClientSize.Width - 2 * CollisionPadding));
        AutomationProperties.SetName(surface, DialogName);
        OnOpening();
        var popup = _presence!.Popup;
        popup.PlacementTarget = anchor;
        // Presses on the pill reach it (it toggles the popover itself); outside presses are handled by the hook.
        popup.OverlayInputPassThroughElement = anchor;
        popup.MarginToAnchor = AnchorGap;
        popup.CollisionPadding = CollisionPadding;
        if (popup.RequestedPlacement != Placement)
        {
            popup.Placement = Placement;
        }
        Hook(topLevel);
        UsesKeyboard = _pendingFocus != ModelMenuOpenFocus.Surface;
        var reversing = _presence.IsClosing;
        _presence.Show();
        if (reversing)
        {
            PostOpenFocus();
        }
    }

    private void OnShown(object? sender, EventArgs e)
    {
        PostOpenFocus();
        RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
    }

    private void PostOpenFocus()
    {
        var focus = _pendingFocus;
        Dispatcher.UIThread.Post(() =>
        {
            if (IsShown)
            {
                ApplyOpenFocus(focus);
            }
        }, DispatcherPriority.Loaded);
    }

    private void CloseCore()
    {
        var returnFocus = _closeReturnFocus;
        var method = _closeMethod;
        _closeReturnFocus = false;
        _closeMethod = NavigationMethod.Pointer;
        Unhook();
        if (_presence is not { } presence || !presence.Popup.IsOpen)
        {
            return;
        }
        var inside = _surface?.IsKeyboardFocusWithin == true;
        if (PlacementTarget is { } anchor && (returnFocus || inside) && TopLevel.GetTopLevel(anchor) is not null)
        {
            anchor.Focus(method);
        }
        presence.Hide();
    }

    private void OnPresenceHidden(object? sender, EventArgs e)
    {
        OnHidden();
        RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        // The studio left the window: the popover goes without its exit.
        Unhook();
        if (IsOpen)
        {
            Sync(false);
        }
        _presence?.Hide(immediate: true);
    }

    #endregion

    #region Keys and outside presses

    /// <summary>Keys from inside the popover (bubbled to its surface). Escape closes it, Tab cycles its stops.</summary>
    internal void OnSurfaceKeyDown(KeyEventArgs e)
    {
        if (e.Handled || !IsShown)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(returnFocus: true, NavigationMethod.Tab);
            return;
        }
        if (e.Key == Key.Tab && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0)
        {
            e.Handled = true;
            UsesKeyboard = true;
            CycleFocus((e.KeyModifiers & KeyModifiers.Shift) != 0 ? -1 : 1);
            return;
        }
        OnMenuKeyDown(e);
    }

    /// <summary>The popover's own keys (arrows, Home, End...).</summary>
    protected virtual void OnMenuKeyDown(KeyEventArgs e)
    {
    }

    private void CycleFocus(int step)
    {
        var stops = new List<Control>();
        foreach (var stop in TabStops())
        {
            if (stop.IsEffectivelyVisible && stop.IsEffectivelyEnabled && stop.Focusable)
            {
                stops.Add(stop);
            }
        }
        if (stops.Count == 0)
        {
            return;
        }
        var current = stops.FindIndex(s => s.IsKeyboardFocusWithin);
        var next = current < 0 ? (step > 0 ? 0 : stops.Count - 1) : ((current + step) % stops.Count + stops.Count) % stops.Count;
        stops[next].Focus(NavigationMethod.Tab);
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

    /// <summary>Whether <paramref name="visual"/> is the surface or inside it.</summary>
    internal bool IsInSurface(Visual visual) => _surface is { } surface && (ReferenceEquals(surface, visual) || surface.IsVisualAncestorOf(visual));

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsShown || e.Source is not Visual source)
        {
            return;
        }
        if (IsInSurface(source))
        {
            UsesKeyboard = false;
            return;
        }
        // A press on the pill passes through: the pill toggles the popover itself.
        if (PlacementTarget is { } anchor && (ReferenceEquals(anchor, source) || anchor.IsVisualAncestorOf(source)))
        {
            return;
        }
        Close(returnFocus: false, NavigationMethod.Pointer);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        // Keys with focus inside reach the surface first; this catches Escape with focus elsewhere.
        if (!e.Handled && e.Key == Key.Escape && IsShown)
        {
            e.Handled = true;
            Close(returnFocus: true, NavigationMethod.Tab);
        }
    }

    #endregion
}

/// <summary>
/// The surface of a prompt studio popover: 1px Border, radius 26 (RadiusPanel) on the Surface, 6px around its content (the
/// floating shadow comes from the popup). It hands the keys that bubble to it to its <see cref="Owner"/>; assistive
/// technology sees a pane named by the owner's <c>DialogName</c>.
/// </summary>
internal sealed class PromptPopoverSurface : ContentControl
{
    static PromptPopoverSurface()
    {
        FocusableProperty.OverrideDefaultValue<PromptPopoverSurface>(true);
        IsTabStopProperty.OverrideDefaultValue<PromptPopoverSurface>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<PromptPopoverSurface>(KeyboardNavigationMode.None);
    }

    /// <summary>The popover this surface belongs to.</summary>
    public PromptPopoverMenu? Owner { get; init; }

    protected override Type StyleKeyOverride => typeof(PromptPopoverSurface);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        Owner?.OnSurfaceKeyDown(e);
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SurfacePeer(this);

    private sealed class SurfacePeer(PromptPopoverSurface owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
    }
}
