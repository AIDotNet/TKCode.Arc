using System.Windows.Input;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Side of the trigger an anchored surface opens on.</summary>
public enum PopoverPlacement
{
    Top,
    Bottom,
    Left,
    Right
}

/// <summary>Alignment of an anchored surface along the trigger edge.</summary>
public enum PopoverAlignment
{
    Start,
    Center,
    End
}

/// <summary>What on the trigger opens a <see cref="Popover"/>.</summary>
public enum PopoverTriggerMode
{
    /// <summary>A click (or Enter / Space on the trigger) toggles the panel; focus moves into it.</summary>
    Click,

    /// <summary>
    /// The pointer resting on the trigger opens the panel (after <see cref="Popover.OpenDelay"/>) and leaving both the
    /// trigger and the panel closes it (after <see cref="Popover.CloseDelay"/>). Focus stays where it is. Enter / Space on
    /// the trigger opens it for the keyboard (focus then moves into the panel) and a touch tap toggles it.
    /// </summary>
    Hover,

    /// <summary>Nothing on the trigger opens it: set <see cref="Popover.IsOpen"/>, or call <see cref="Popover.ShowAt"/>.</summary>
    Manual
}

/// <summary>
/// A small surface anchored to a trigger and opened by a click, for interactive content that should not block the page:
/// share settings, a small filter form, a picker built from your own controls. Two ways to use it:
/// <code>
/// &lt;!-- Trigger + content --&gt;
/// &lt;arc:Popover&gt;
///     &lt;arc:Popover.Trigger&gt;&lt;arc:Button Variant="Secondary" Content="Share"/&gt;&lt;/arc:Popover.Trigger&gt;
///     &lt;TextBlock Text="Anyone with the link can view."/&gt;
/// &lt;/arc:Popover&gt;
///
/// &lt;!-- Attached to any control --&gt;
/// &lt;arc:Button Content="Filters"&gt;
///     &lt;arc:Popover.Attach&gt;&lt;arc:Popover Placement="Top"&gt; … &lt;/arc:Popover&gt;&lt;/arc:Popover.Attach&gt;
/// &lt;/arc:Button&gt;
/// </code>
/// Code can open any content anchored to any control with <see cref="ShowAt"/>. The panel starts 5px toward its
/// trigger at scale 0.97, fades in fast and settles on a spring from the side it actually landed on; it leaves in about
/// 160ms at half the travel, and an open and a close reverse each other mid-flight. Escape and a press outside close it
/// and return focus to the trigger; focus moves into the panel on open. The trigger answers presses with color only
/// (no press scale), since the panel is measured against it.
/// </summary>
public class Popover : ContentControl
{
    #region Properties

    public static readonly StyledProperty<Control?> TriggerProperty =
        AvaloniaProperty.Register<Popover, Control?>(nameof(Trigger));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Popover, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<PopoverPlacement> PlacementProperty =
        AvaloniaProperty.Register<Popover, PopoverPlacement>(nameof(Placement), PopoverPlacement.Bottom);

    public static readonly StyledProperty<PopoverAlignment> AlignmentProperty =
        AvaloniaProperty.Register<Popover, PopoverAlignment>(nameof(Alignment), PopoverAlignment.Start);

    public static readonly StyledProperty<double> OffsetProperty =
        AvaloniaProperty.Register<Popover, double>(nameof(Offset), 6);

    public static readonly StyledProperty<bool> IsModalProperty =
        AvaloniaProperty.Register<Popover, bool>(nameof(IsModal));

    /// <summary>
    /// Draws the floating shadow around the panel (default true). Turn it off, and style <see cref="PopoverSurface"/> with a
    /// transparent <c>Background</c> and no border, to show the content on its own.
    /// </summary>
    public static readonly StyledProperty<bool> HasShadowProperty =
        AvaloniaProperty.Register<Popover, bool>(nameof(HasShadow), true);

    /// <summary>What on the trigger opens the panel: <see cref="PopoverTriggerMode.Click"/> (default), <see cref="PopoverTriggerMode.Hover"/> or <see cref="PopoverTriggerMode.Manual"/>.</summary>
    public static readonly StyledProperty<PopoverTriggerMode> TriggerModeProperty =
        AvaloniaProperty.Register<Popover, PopoverTriggerMode>(nameof(TriggerMode), PopoverTriggerMode.Click);

    /// <summary>Hover time before the panel opens in <see cref="PopoverTriggerMode.Hover"/> mode (default 100ms).</summary>
    public static readonly StyledProperty<TimeSpan> OpenDelayProperty =
        AvaloniaProperty.Register<Popover, TimeSpan>(nameof(OpenDelay), TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Time the pointer may be away from both the trigger and the panel before it closes in <see cref="PopoverTriggerMode.Hover"/>
    /// mode (default 140ms; long enough to cross the gap between them).
    /// </summary>
    public static readonly StyledProperty<TimeSpan> CloseDelayProperty =
        AvaloniaProperty.Register<Popover, TimeSpan>(nameof(CloseDelay), TimeSpan.FromMilliseconds(140));

    /// <summary>Attaches a popover to any control: a click on the control toggles it.</summary>
    public static readonly AttachedProperty<Popover?> AttachProperty =
        AvaloniaProperty.RegisterAttached<Popover, Control, Popover?>("Attach");

    public static Popover? GetAttach(Control element) => element.GetValue(AttachProperty);

    public static void SetAttach(Control element, Popover? value) => element.SetValue(AttachProperty, value);

    /// <summary>The control that opens the panel, shown in place of the popover (optional with <see cref="AttachProperty"/> or <see cref="ShowAt"/>).</summary>
    public Control? Trigger { get => GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>Open state (two-way).</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Preferred side (flips when it does not fit; the motion follows the side it lands on).</summary>
    public PopoverPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>Alignment along the trigger edge (default Start).</summary>
    public PopoverAlignment Alignment { get => GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }

    /// <summary>Gap to the trigger in px (Arc: 6).</summary>
    public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

    /// <summary>Modal: presses outside are swallowed (they only close the popover). Default false.</summary>
    public bool IsModal { get => GetValue(IsModalProperty); set => SetValue(IsModalProperty, value); }

    /// <inheritdoc cref="TriggerModeProperty"/>
    public PopoverTriggerMode TriggerMode { get => GetValue(TriggerModeProperty); set => SetValue(TriggerModeProperty, value); }

    /// <inheritdoc cref="OpenDelayProperty"/>
    public TimeSpan OpenDelay { get => GetValue(OpenDelayProperty); set => SetValue(OpenDelayProperty, value); }

    /// <inheritdoc cref="CloseDelayProperty"/>
    public TimeSpan CloseDelay { get => GetValue(CloseDelayProperty); set => SetValue(CloseDelayProperty, value); }

    /// <inheritdoc cref="HasShadowProperty"/>
    public bool HasShadow { get => GetValue(HasShadowProperty); set => SetValue(HasShadowProperty, value); }

    #endregion

    /// <summary>Raised when the popover opens.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised when the popover closes (the exit may still be playing).</summary>
    public event EventHandler<OverlayClosedEventArgs>? Closed;

    public ICommand OpenCommand { get; }

    /// <summary>Closes the popover (PopoverClose).</summary>
    public ICommand CloseCommand { get; }

    private OverlayAnchoredPresence? _presence;
    private PopoverSurface? _surface;
    private Control? _attachedTarget;
    private Control? _showAnchor;
    private Control? _hookedTrigger;
    private PopoverTriggerMode _hookedMode;
    private DispatcherTimer? _openTimer;
    private DispatcherTimer? _closeTimer;
    private bool _overTrigger;
    private bool _overSurface;
    private bool _focusOnOpen;
    private bool _parentedToTarget;
    private bool _pendingOpen;
    private DismissReason _pendingReason = DismissReason.Programmatic;

    static Popover()
    {
        AttachProperty.Changed.AddClassHandler<Control>(OnAttachChanged);
        FocusableProperty.OverrideDefaultValue<Popover>(false);
        FocusModality.Watch();
    }

    public Popover()
    {
        OpenCommand = new OverlayCommand(_ => SetCurrentValue(IsOpenProperty, true));
        CloseCommand = new OverlayCommand(_ => Close());
    }

    /// <summary>The surface (created on first open).</summary>
    public PopoverSurface? Surface => _surface;

    /// <summary>The popup hosting the surface (created on first open).</summary>
    public ArcPopup? Popup => _presence?.Popup;

    /// <summary>The control the panel is anchored to.</summary>
    public Control? Anchor => _showAnchor ?? _attachedTarget ?? Trigger;

    /// <summary>Opens the panel anchored to <paramref name="anchor"/> (any control in a window).</summary>
    public void ShowAt(Control anchor)
    {
        if (IsOpen && !ReferenceEquals(anchor, Anchor))
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        _showAnchor = anchor;
        SetCurrentValue(IsOpenProperty, true);
    }

    /// <summary>Closes the panel.</summary>
    public void Close() => Close(DismissReason.Programmatic);

    private void Close(DismissReason reason)
    {
        if (!IsOpen)
        {
            return;
        }
        _pendingReason = reason;
        SetCurrentValue(IsOpenProperty, false);
    }

    #region Attached usage

    private static void OnAttachChanged(Control target, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is Popover old && ReferenceEquals(old._attachedTarget, target))
        {
            old.Close();
            old.UnhookTrigger();
            old._attachedTarget = null;
        }
        if (e.NewValue is Popover popover)
        {
            popover._attachedTarget = target;
            popover.HookTrigger(target);
        }
    }

    #endregion

    #region Trigger

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TriggerProperty)
        {
            if (_attachedTarget is null)
            {
                HookTrigger(change.GetNewValue<Control?>());
            }
        }
        else if (change.Property == IsOpenProperty)
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
        else if (change.Property == HasShadowProperty)
        {
            ApplyShadow();
        }
        else if (change.Property == TriggerModeProperty)
        {
            // Hook the trigger again for the new mode (the attached target counts as the trigger).
            HookTrigger(_hookedTrigger);
        }
        else if (IsOpen && (change.Property == PlacementProperty || change.Property == AlignmentProperty || change.Property == OffsetProperty))
        {
            ShowPresence();
        }
    }

    private void HookTrigger(Control? trigger)
    {
        UnhookTrigger();
        _hookedTrigger = trigger;
        if (trigger is null)
        {
            return;
        }
        _hookedMode = TriggerMode;
        switch (_hookedMode)
        {
            case PopoverTriggerMode.Click when trigger is AvaloniaButton button:
                button.Click += OnTriggerClick;
                break;
            case PopoverTriggerMode.Click:
                trigger.Tapped += OnTriggerTapped;
                trigger.KeyDown += OnTriggerKeyDown;
                break;
            case PopoverTriggerMode.Hover:
                trigger.PointerEntered += OnTriggerPointerEntered;
                trigger.PointerExited += OnTriggerPointerExited;
                // A button handles its own Enter / Space and taps, so these listen to handled events too.
                trigger.AddHandler(KeyDownEvent, OnTriggerHoverKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
                trigger.AddHandler(TappedEvent, OnTriggerTouchTapped, RoutingStrategies.Bubble, handledEventsToo: true);
                break;
        }
        trigger.DetachedFromVisualTree += OnTriggerDetached;
        // The panel is measured against the trigger, so it answers presses with color only.
        if (trigger is Button arcButton)
        {
            arcButton.SetCurrentValue(Button.IsPressScaleEnabledProperty, false);
        }
        UpdateTriggerState();
    }

    private void UnhookTrigger()
    {
        if (_hookedTrigger is null)
        {
            return;
        }
        var trigger = _hookedTrigger;
        switch (_hookedMode)
        {
            case PopoverTriggerMode.Click when trigger is AvaloniaButton button:
                button.Click -= OnTriggerClick;
                break;
            case PopoverTriggerMode.Click:
                trigger.Tapped -= OnTriggerTapped;
                trigger.KeyDown -= OnTriggerKeyDown;
                break;
            case PopoverTriggerMode.Hover:
                trigger.PointerEntered -= OnTriggerPointerEntered;
                trigger.PointerExited -= OnTriggerPointerExited;
                trigger.RemoveHandler(KeyDownEvent, OnTriggerHoverKeyDown);
                trigger.RemoveHandler(TappedEvent, OnTriggerTouchTapped);
                break;
        }
        trigger.DetachedFromVisualTree -= OnTriggerDetached;
        _hookedTrigger = null;
        StopHoverTimers();
        _overTrigger = false;
    }

    private void OnTriggerDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // The trigger left the window (page navigated away): close without the exit.
        StopHoverTimers();
        _overTrigger = false;
        if (IsOpen && ReferenceEquals(sender, Anchor))
        {
            Close();
            _presence?.Hide(immediate: true);
        }
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e) => Toggle(sender as Control);

    private void OnTriggerTapped(object? sender, TappedEventArgs e) => Toggle(sender as Control);

    private void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && !IsOpen)
        {
            e.Handled = true;
            Toggle(sender as Control);
        }
    }

    private void Toggle(Control? trigger)
    {
        if (IsOpen)
        {
            Close(DismissReason.Programmatic);
            return;
        }
        _showAnchor = trigger;
        SetCurrentValue(IsOpenProperty, true);
    }

    #region Hover

    private void OnTriggerPointerEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _overTrigger = true;
        _closeTimer?.Stop();
        if (IsOpen)
        {
            return;
        }
        if (OpenDelay <= TimeSpan.Zero)
        {
            OpenFromHover(sender as Control);
            return;
        }
        _openTimer ??= CreateTimer(OpenIfHovering);
        _openTimer.Stop();
        _openTimer.Interval = OpenDelay;
        _openTimer.Start();
    }

    private void OnTriggerPointerExited(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _overTrigger = false;
        ScheduleClose();
    }

    private void OnSurfacePointerEntered(object? sender, PointerEventArgs e)
    {
        if (TriggerMode != PopoverTriggerMode.Hover || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _overSurface = true;
        _closeTimer?.Stop();
    }

    private void OnSurfacePointerExited(object? sender, PointerEventArgs e)
    {
        if (TriggerMode != PopoverTriggerMode.Hover || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _overSurface = false;
        ScheduleClose();
    }

    private void OnTriggerHoverKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && !IsOpen)
        {
            // Without a pointer there is no hovering: the keyboard opens it and goes into the panel.
            _openTimer?.Stop();
            _focusOnOpen = true;
            OpenFromHover(sender as Control);
        }
    }

    private void OnTriggerTouchTapped(object? sender, TappedEventArgs e)
    {
        if (e.Pointer?.Type == PointerType.Touch)
        {
            _focusOnOpen = true;
            Toggle(sender as Control);
        }
    }

    private void OpenIfHovering()
    {
        if (_overTrigger && !IsOpen)
        {
            OpenFromHover(_hookedTrigger);
        }
    }

    private void OpenFromHover(Control? trigger)
    {
        _showAnchor = trigger;
        SetCurrentValue(IsOpenProperty, true);
    }

    private void ScheduleClose()
    {
        _openTimer?.Stop();
        if (!IsOpen)
        {
            return;
        }
        if (CloseDelay <= TimeSpan.Zero)
        {
            CloseIfAway();
            return;
        }
        _closeTimer ??= CreateTimer(CloseIfAway);
        _closeTimer.Stop();
        _closeTimer.Interval = CloseDelay;
        _closeTimer.Start();
    }

    private void CloseIfAway()
    {
        if (!_overTrigger && !_overSurface)
        {
            Close(DismissReason.Programmatic);
        }
    }

    private void StopHoverTimers()
    {
        _openTimer?.Stop();
        _closeTimer?.Stop();
    }

    private static DispatcherTimer CreateTimer(Action tick)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            tick();
        };
        return timer;
    }

    /// <summary>An open is waiting for its delay (tests).</summary>
    internal bool IsOpenScheduled => _openTimer?.IsEnabled == true;

    /// <summary>A close is waiting for its delay (tests).</summary>
    internal bool IsCloseScheduled => _closeTimer?.IsEnabled == true;

    /// <summary>Runs the pending open and close now, as if their delays had passed (tests: headless timers only fire inside a frame).</summary>
    internal void FlushTimers()
    {
        if (_openTimer?.IsEnabled == true)
        {
            _openTimer.Stop();
            OpenIfHovering();
        }
        if (_closeTimer?.IsEnabled == true)
        {
            _closeTimer.Stop();
            CloseIfAway();
        }
    }

    #endregion

    private void UpdateTriggerState()
    {
        if (_hookedTrigger is { } trigger)
        {
            // Mirrors aria-expanded for assistive technology.
            AutomationProperties.SetItemStatus(trigger, IsOpen ? "Expanded" : "Collapsed");
        }
    }

    #endregion

    #region Open / close

    /// <summary>
    /// The popup draws the shadow from the <c>ShadowFloating</c> token at style priority. Without a shadow the popup gets
    /// an empty one as a local value; with one the local value is cleared and the token applies again.
    /// </summary>
    private void ApplyShadow()
    {
        if (_presence is not { } presence)
        {
            return;
        }
        if (HasShadow)
        {
            presence.Popup.ClearValue(ArcPopup.PopupRootShadowProperty);
            presence.Popup.ClearValue(ArcPopup.OverlayHostShadowProperty);
        }
        else
        {
            presence.Popup.PopupRootShadow = default;
            presence.Popup.OverlayHostShadow = default;
        }
    }

    private void EnsurePresence()
    {
        if (_presence is not null)
        {
            return;
        }
        _surface = new PopoverSurface();
        _surface.Bind(ContentProperty, this.GetObservable(ContentProperty));
        _surface.Bind(ContentTemplateProperty, this.GetObservable(ContentTemplateProperty));
        _surface.PointerEntered += OnSurfacePointerEntered;
        _surface.PointerExited += OnSurfacePointerExited;
        _presence = new OverlayAnchoredPresence(_surface, OverlayAnchoredMotion.Popover);
        ApplyShadow();
        LogicalChildren.Add(_presence.Popup);
        _presence.Hidden += (_, _) => ReleaseParent();
        _presence.OutsidePressed += (_, e) =>
        {
            if (IsModal)
            {
                e.Handled = true;
            }
            Close(DismissReason.PressOutside);
        };
        _presence.EscapePressed += (_, e) =>
        {
            e.Handled = true;
            Close(DismissReason.Escape);
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_pendingOpen && IsOpen)
        {
            OpenCore();
        }
    }

    private void OpenCore()
    {
        var anchor = Anchor ?? this;
        if (TopLevel.GetTopLevel(anchor) is null)
        {
            _pendingOpen = true;
            return;
        }
        _pendingOpen = false;
        EnsurePresence();
        if (Parent is null && _attachedTarget is not null)
        {
            // Attached popovers join their target's logical tree while open (styles, DataContext).
            ((ISetLogicalParent)this).SetParent(_attachedTarget);
            _parentedToTarget = true;
        }
        if (AutomationProperties.GetName(this) is { Length: > 0 } name)
        {
            AutomationProperties.SetName(_surface!, name);
        }
        _pendingReason = DismissReason.Programmatic;
        ShowPresence();
        UpdateTriggerState();
        // Focus moves into the panel (first focusable element, or the panel itself); a panel opened by hovering leaves it where it is.
        if (TriggerMode != PopoverTriggerMode.Hover || _focusOnOpen)
        {
            var target = FocusManager.FindFirstFocusableElement(_surface!) ?? _surface;
            target?.Focus(NavigationMethod.Unspecified);
        }
        _focusOnOpen = false;
        Opened?.Invoke(this, EventArgs.Empty);
    }

    private void ShowPresence()
    {
        if (_presence is null || (Anchor ?? this) is not { } anchor)
        {
            return;
        }
        _presence.Show(anchor, ToSide(Placement), (OverlayAnchorAlign)(int)Alignment, Offset);
    }

    private static OverlayAnchorSide ToSide(PopoverPlacement placement) => placement switch
    {
        PopoverPlacement.Top => OverlayAnchorSide.Top,
        PopoverPlacement.Left => OverlayAnchorSide.Left,
        PopoverPlacement.Right => OverlayAnchorSide.Right,
        _ => OverlayAnchorSide.Bottom
    };

    private void CloseCore()
    {
        _pendingOpen = false;
        _focusOnOpen = false;
        _overSurface = false;
        StopHoverTimers();
        var reason = _pendingReason;
        _pendingReason = DismissReason.Programmatic;
        var anchor = Anchor;
        // The content is the popover's logical child, shown inside the surface: focus on it counts through the visual tree.
        var focusInside = _surface is not null && TopLevel.GetTopLevel(_surface)?.FocusManager?.GetFocusedElement() is Visual focused &&
                          (ReferenceEquals(focused, _surface) || _surface.IsLogicalAncestorOf(focused) || _surface.IsVisualAncestorOf(focused));
        if (anchor is not null && (focusInside || reason == DismissReason.Escape))
        {
            // Escape, or a key that pressed a closing button inside, gives the anchor its keyboard ring; a click does
            // not. Before the panel goes: hiding it with focus inside puts focus back by itself, without the ring, and
            // focusing the already focused anchor then changes nothing.
            var keyboard = reason == DismissReason.Escape || FocusModality.LastWasPointer(anchor) == false;
            anchor.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        }
        _presence?.Hide();
        UpdateTriggerState();
        _showAnchor = null;
        Closed?.Invoke(this, new OverlayClosedEventArgs(reason, null));
    }

    private void ReleaseParent()
    {
        if (_parentedToTarget && !IsOpen)
        {
            _parentedToTarget = false;
            ((ISetLogicalParent)this).SetParent(null);
        }
    }

    #endregion
}
