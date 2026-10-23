using System.Diagnostics;
using System.Text;
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

/// <summary>
/// A read-only preview of a person or link that opens after a short hover or keyboard focus: mentions, avatars, author
/// names, link previews — where a click must stay free for navigation.
/// <code>
/// &lt;arc:HoverCard&gt;
///     &lt;arc:HoverCard.Trigger&gt;&lt;arc:Button Variant="Ghost" Content="@maya"/&gt;&lt;/arc:HoverCard.Trigger&gt;
///     &lt;arc:HoverCardProfile DisplayName="Maya Chen" Role="Product designer, Payments"/&gt;
/// &lt;/arc:HoverCard&gt;
/// &lt;!-- or on any control --&gt;
/// &lt;TextBlock Text="@maya"&gt;&lt;arc:HoverCard.Attach&gt;&lt;arc:HoverCard&gt;…&lt;/arc:HoverCard&gt;&lt;/arc:HoverCard.Attach&gt;&lt;/TextBlock&gt;
/// </code>
/// It waits 500ms; moving to another trigger while a card is open (or within 300ms of one closing) opens the next
/// after 80ms with a fade only. The pointer can travel into the card during a 140ms grace and it stays open while the
/// pointer is over it. Tab focus opens at once, Enter skips the wait, Escape closes (and it stays closed while the
/// pointer rests on the trigger); on touch a tap toggles it. One card is open at a time. The card grows from the
/// trigger edge: scale 0.96 and 4px toward the trigger on the smooth spring, with its own fade; leaving mid-exit and
/// coming back turns it around. Focus never enters the card, so keep its content read-only.
/// </summary>
public class HoverCard : ContentControl
{
    #region Properties

    public static readonly StyledProperty<Control?> TriggerProperty =
        AvaloniaProperty.Register<HoverCard, Control?>(nameof(Trigger));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<HoverCard, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay, coerce: CoerceIsOpen);

    public static readonly StyledProperty<bool> OpensOnFocusProperty =
        AvaloniaProperty.Register<HoverCard, bool>(nameof(OpensOnFocus));

    public static readonly StyledProperty<PopoverPlacement> PlacementProperty =
        AvaloniaProperty.Register<HoverCard, PopoverPlacement>(nameof(Placement), PopoverPlacement.Bottom);

    public static readonly StyledProperty<PopoverAlignment> AlignmentProperty =
        AvaloniaProperty.Register<HoverCard, PopoverAlignment>(nameof(Alignment), PopoverAlignment.Start);

    public static readonly StyledProperty<TimeSpan> OpenDelayProperty =
        AvaloniaProperty.Register<HoverCard, TimeSpan>(nameof(OpenDelay), TimeSpan.FromMilliseconds(500));

    public static readonly StyledProperty<TimeSpan> CloseDelayProperty =
        AvaloniaProperty.Register<HoverCard, TimeSpan>(nameof(CloseDelay), TimeSpan.FromMilliseconds(140));

    public static readonly StyledProperty<double> CardWidthProperty =
        AvaloniaProperty.Register<HoverCard, double>(nameof(CardWidth), 296);

    public static readonly StyledProperty<double> OffsetProperty =
        AvaloniaProperty.Register<HoverCard, double>(nameof(Offset), 8);

    public static readonly StyledProperty<Thickness> CardPaddingProperty =
        AvaloniaProperty.Register<HoverCard, Thickness>(nameof(CardPadding), new Thickness(16));

    /// <summary>Attaches a hover card to any control.</summary>
    public static readonly AttachedProperty<HoverCard?> AttachProperty =
        AvaloniaProperty.RegisterAttached<HoverCard, Control, HoverCard?>("Attach");

    public static HoverCard? GetAttach(Control element) => element.GetValue(AttachProperty);

    public static void SetAttach(Control element, HoverCard? value) => element.SetValue(AttachProperty, value);

    /// <summary>The focusable trigger shown in place of the card (a mention, an avatar, a link).</summary>
    public Control? Trigger { get => GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>
    /// Open state (two-way): it follows the card when hover, focus, a click or Escape opens or closes it, and setting it
    /// opens or closes the card at once without the delay; any write cancels a pending open or close.
    /// </summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>
    /// Opens the card when keyboard focus (Tab or the arrow keys) lands on the trigger or inside it, after
    /// <see cref="OpenDelay"/> (zero opens at once), and closes it as soon as focus leaves unless the pointer rests on the
    /// trigger or the card; focus from a click or from code does not open it. Off by default, where Tab focus opens
    /// the card at once and losing focus starts the <see cref="CloseDelay"/> grace.
    /// </summary>
    public bool OpensOnFocus { get => GetValue(OpensOnFocusProperty); set => SetValue(OpensOnFocusProperty, value); }

    /// <summary>Preferred side (default Bottom).</summary>
    public PopoverPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>Alignment along the trigger edge (default Start).</summary>
    public PopoverAlignment Alignment { get => GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }

    /// <summary>Hover time before the first card opens (default 500ms); with <see cref="OpensOnFocus"/> also the wait after keyboard focus.</summary>
    public TimeSpan OpenDelay { get => GetValue(OpenDelayProperty); set => SetValue(OpenDelayProperty, value); }

    /// <summary>Grace after the pointer leaves, so it can travel into the card (default 140ms).</summary>
    public TimeSpan CloseDelay { get => GetValue(CloseDelayProperty); set => SetValue(CloseDelayProperty, value); }

    /// <summary>Card width (Arc: 18.5rem).</summary>
    public double CardWidth { get => GetValue(CardWidthProperty); set => SetValue(CardWidthProperty, value); }

    /// <summary>Gap to the trigger (Arc: 8).</summary>
    public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

    /// <summary>Space inside the card border (Arc: 16; a chat's citation preview uses 14 16).</summary>
    public Thickness CardPadding { get => GetValue(CardPaddingProperty); set => SetValue(CardPaddingProperty, value); }

    #endregion

    /// <summary>Raised when the card opens.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised when the card closes (the exit may still be playing).</summary>
    public event EventHandler<OverlayClosedEventArgs>? Closed;

    /// <summary>Moving to another trigger within this window opens its card almost at once.</summary>
    private static readonly TimeSpan SkipWindow = TimeSpan.FromMilliseconds(300);

    /// <summary>Even warm, a trigger waits this long, so a pointer crossing it on the way into an open card does not steal it.</summary>
    private static readonly TimeSpan Intent = TimeSpan.FromMilliseconds(80);

    private static HoverCard? s_open;
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();
    private static TimeSpan s_closedAt = TimeSpan.FromDays(-1);

    private OverlayAnchoredPresence? _presence;
    private PopoverSurface? _surface;
    private Control? _attachedTarget;
    private Control? _hookedTrigger;
    private DispatcherTimer? _openTimer;
    private DispatcherTimer? _closeTimer;
    private bool _insideTrigger;
    private bool _insideCard;
    private bool _focusInside;
    private bool _dismissed;
    private bool _instant;
    private bool _parentedToTarget;
    private PointerType _lastPointer = PointerType.Mouse;
    private string? _previousHelpText;
    private bool _described;
    private DismissReason _pendingReason = DismissReason.Programmatic;

    static HoverCard()
    {
        AttachProperty.Changed.AddClassHandler<Control>(OnAttachChanged);
        FocusableProperty.OverrideDefaultValue<HoverCard>(false);
    }

    /// <summary>
    /// Every write of <see cref="IsOpen"/>, even of the value it already has (which raises no change), cancels a pending
    /// delayed open or close; the coerce callback is the one hook that sees such writes.
    /// </summary>
    private static bool CoerceIsOpen(AvaloniaObject sender, bool value)
    {
        if (sender is HoverCard card)
        {
            card.StopTimers();
        }
        return value;
    }

    /// <summary>True while any card is open or one closed less than 300ms ago.</summary>
    internal static bool IsWarm => s_open is not null || s_clock.Elapsed - s_closedAt < SkipWindow;

    /// <summary>The card surface (created on first open).</summary>
    public PopoverSurface? Surface => _surface;

    /// <summary>The popup hosting the card.</summary>
    public ArcPopup? Popup => _presence?.Popup;

    /// <summary>The control the card is anchored to.</summary>
    public Control? Anchor => _attachedTarget ?? Trigger;

    /// <summary>Opens at once (no delay).</summary>
    public void Show() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes at once (no grace).</summary>
    public void Hide() => SetCurrentValue(IsOpenProperty, false);

    #region Triggers

    private static void OnAttachChanged(Control target, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is HoverCard old && ReferenceEquals(old._attachedTarget, target))
        {
            old.Hide();
            old.Unhook();
            old._attachedTarget = null;
        }
        if (e.NewValue is HoverCard card)
        {
            card._attachedTarget = target;
            card.Hook(target);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TriggerProperty && _attachedTarget is null)
        {
            Hook(change.GetNewValue<Control?>());
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
    }

    private void Hook(Control? trigger)
    {
        Unhook();
        _hookedTrigger = trigger;
        if (trigger is null)
        {
            return;
        }
        trigger.PointerEntered += OnTriggerEntered;
        trigger.PointerExited += OnTriggerExited;
        trigger.AddHandler(PointerPressedEvent, OnTriggerPressed, RoutingStrategies.Tunnel, true);
        trigger.AddHandler(PointerReleasedEvent, OnTriggerReleased, RoutingStrategies.Bubble, true);
        trigger.AddHandler(KeyDownEvent, OnTriggerKeyDown, RoutingStrategies.Bubble, true);
        trigger.GotFocus += OnTriggerGotFocus;
        trigger.LostFocus += OnTriggerLostFocus;
        trigger.DetachedFromVisualTree += OnTriggerDetached;
        if (trigger is Button arcButton)
        {
            arcButton.SetCurrentValue(Button.IsPressScaleEnabledProperty, false);
        }
    }

    private void Unhook()
    {
        if (_hookedTrigger is not { } trigger)
        {
            return;
        }
        trigger.PointerEntered -= OnTriggerEntered;
        trigger.PointerExited -= OnTriggerExited;
        trigger.RemoveHandler(PointerPressedEvent, OnTriggerPressed);
        trigger.RemoveHandler(PointerReleasedEvent, OnTriggerReleased);
        trigger.RemoveHandler(KeyDownEvent, OnTriggerKeyDown);
        trigger.GotFocus -= OnTriggerGotFocus;
        trigger.LostFocus -= OnTriggerLostFocus;
        trigger.DetachedFromVisualTree -= OnTriggerDetached;
        _hookedTrigger = null;
    }

    private void OnTriggerEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _insideTrigger = true;
        ScheduleOpen();
    }

    private void OnTriggerExited(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _insideTrigger = false;
        _dismissed = false;
        ScheduleClose();
    }

    private void OnTriggerPressed(object? sender, PointerPressedEventArgs e) => _lastPointer = e.Pointer.Type;

    private void OnTriggerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton is not (MouseButton.Left or MouseButton.None))
        {
            return;
        }
        // A tap toggles the card on touch; a click opens it without the wait.
        if (_lastPointer == PointerType.Touch)
        {
            if (IsOpen)
            {
                Hide();
            }
            else
            {
                OpenNow();
            }
        }
        else if (!IsOpen)
        {
            OpenNow();
        }
    }

    private void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !IsOpen)
        {
            OpenNow();
        }
        else if (e.Key == Key.Escape)
        {
            if (IsOpen)
            {
                // Focus stays on the trigger; only a key that closed the card is handled, so the next Escape reaches the host.
                e.Handled = true;
                _dismissed = true;
                _pendingReason = DismissReason.Escape;
                Hide();
            }
            else if (IsOpenScheduled)
            {
                // Nothing is open yet: the wait is cancelled and the card stays closed, but the key goes on to the host.
                _openTimer!.Stop();
                _dismissed = true;
            }
        }
    }

    private void OnTriggerGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (OpensOnFocus)
        {
            // Focus-visible rule: Tab and arrow-key focus open after the delay; a click or code focus does not.
            if (e.NavigationMethod is not (NavigationMethod.Tab or NavigationMethod.Directional) || _focusInside)
            {
                return;
            }
            _focusInside = true;
            _dismissed = false;
            if (OpenDelay <= TimeSpan.Zero)
            {
                OpenNow();
            }
            else
            {
                ScheduleOpen();
            }
            return;
        }
        // Keyboard focus opens at once; pointer focus does not.
        if (e.NavigationMethod != NavigationMethod.Tab)
        {
            return;
        }
        _focusInside = true;
        OpenNow();
    }

    private void OnTriggerLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (OpensOnFocus)
        {
            OnFocusLeft(e);
            return;
        }
        _focusInside = false;
        _dismissed = false;
        ScheduleClose();
    }

    private void OnFocusLeft(FocusChangedEventArgs e)
    {
        // Focus moving between parts of the trigger (or into the card) has not left.
        if (e.NewFocusedElement is Visual next && (Contains(_hookedTrigger, next) || Contains(_surface, next)))
        {
            return;
        }
        _focusInside = false;
        _dismissed = false;
        if (_insideTrigger || _insideCard)
        {
            // The pointer keeps the card (and a pending hover open); leaving with the pointer closes it as usual.
            return;
        }
        // Keyboard focus has no travel into the card to wait for: a pending open is dropped and an open card closes now.
        StopTimers();
        if (IsOpen)
        {
            Hide();
        }
    }

    private static bool Contains(Visual? root, Visual element) =>
        root is not null && (ReferenceEquals(root, element) || root.IsVisualAncestorOf(element));

    private void OnTriggerDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _focusInside = false;
        StopTimers();
        if (IsOpen)
        {
            Hide();
        }
        _presence?.Hide(immediate: true);
    }

    private void OnCardEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _insideCard = true;
        _closeTimer?.Stop();
    }

    private void OnCardExited(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _insideCard = false;
        ScheduleClose();
    }

    #endregion

    #region Timers

    private void OpenNow()
    {
        StopTimers();
        _dismissed = false;
        Show();
    }

    private void ScheduleOpen()
    {
        _closeTimer?.Stop();
        if (_dismissed || IsOpen)
        {
            return;
        }
        _openTimer ??= CreateTimer(() =>
        {
            if (_insideTrigger || _focusInside)
            {
                Show();
            }
        });
        _openTimer.Stop();
        _openTimer.Interval = IsWarm ? Intent : OpenDelay;
        _openTimer.Start();
    }

    private void ScheduleClose()
    {
        _openTimer?.Stop();
        if (_insideTrigger || _insideCard || _focusInside || !IsOpen)
        {
            return;
        }
        _closeTimer ??= CreateTimer(() =>
        {
            if (!_insideTrigger && !_insideCard && !_focusInside)
            {
                Hide();
            }
        });
        _closeTimer.Stop();
        _closeTimer.Interval = CloseDelay;
        _closeTimer.Start();
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

    /// <summary>A close is waiting for its grace (tests).</summary>
    internal bool IsCloseScheduled => _closeTimer?.IsEnabled == true;

    /// <summary>Runs pending delay timers now (tests: headless dispatcher timers do not advance).</summary>
    internal void FlushTimers()
    {
        if (_openTimer?.IsEnabled == true)
        {
            _openTimer.Stop();
            if (_insideTrigger || _focusInside)
            {
                Show();
            }
        }
        if (_closeTimer?.IsEnabled == true)
        {
            _closeTimer.Stop();
            if (!_insideTrigger && !_insideCard && !_focusInside)
            {
                Hide();
            }
        }
    }

    private void StopTimers()
    {
        _openTimer?.Stop();
        _closeTimer?.Stop();
    }

    #endregion

    #region Open / close

    private void EnsurePresence()
    {
        if (_presence is not null)
        {
            return;
        }
        _surface = new HoverCardSurface { Focusable = false, MinWidth = 0, MaxWidth = double.PositiveInfinity };
        // Focus never enters the card: Tab passes over anything focusable inside it.
        KeyboardNavigation.SetTabNavigation(_surface, KeyboardNavigationMode.None);
        _surface.Bind(WidthProperty, this.GetObservable(CardWidthProperty));
        _surface.Bind(PaddingProperty, this.GetObservable(CardPaddingProperty));
        _surface.Bind(ContentProperty, this.GetObservable(ContentProperty));
        _surface.Bind(ContentTemplateProperty, this.GetObservable(ContentTemplateProperty));
        _surface.PointerEntered += OnCardEntered;
        _surface.PointerExited += OnCardExited;
        _surface.Loaded += (_, _) => DescribeLate();
        _presence = new OverlayAnchoredPresence(_surface, OverlayAnchoredMotion.HoverCard);
        _presence.Popup.TakesFocusFromNativeControl = false;
        LogicalChildren.Add(_presence.Popup);
        _presence.Hidden += (_, _) => ReleaseParent();
        _presence.OutsidePressed += (_, _) =>
        {
            _pendingReason = DismissReason.PressOutside;
            Hide();
        };
        _presence.EscapePressed += (_, e) =>
        {
            e.Handled = true;
            _dismissed = true;
            _pendingReason = DismissReason.Escape;
            Hide();
        };
    }

    private void OpenCore()
    {
        var anchor = Anchor;
        if (anchor is null || TopLevel.GetTopLevel(anchor) is null)
        {
            SetCurrentValue(IsOpenProperty, false);
            return;
        }
        StopTimers();
        EnsurePresence();
        // One card at a time: opening this one closes the other, and a warm page opens it with a fade only.
        _instant = IsWarm;
        if (s_open is { } other && !ReferenceEquals(other, this))
        {
            other.Hide();
            _instant = true;
        }
        s_open = this;
        if (Parent is null && _attachedTarget is not null)
        {
            ((ISetLogicalParent)this).SetParent(_attachedTarget);
            _parentedToTarget = true;
        }
        _pendingReason = DismissReason.Programmatic;
        _presence!.Show(anchor, Placement switch
        {
            PopoverPlacement.Top => OverlayAnchorSide.Top,
            PopoverPlacement.Left => OverlayAnchorSide.Left,
            PopoverPlacement.Right => OverlayAnchorSide.Right,
            _ => OverlayAnchorSide.Bottom
        }, (OverlayAnchorAlign)(int)Alignment, Offset, _instant);
        // No aria-describedby in Avalonia: the card summary rides on the trigger's help text while open.
        _previousHelpText = AutomationProperties.GetHelpText(anchor);
        _described = false;
        if (Summary() is { Length: > 0 } summary)
        {
            AutomationProperties.SetHelpText(anchor, summary);
            _described = true;
        }
        Opened?.Invoke(this, EventArgs.Empty);
    }

    private string? Summary() => Content switch
    {
        string text => text,
        HoverCardProfile profile => string.IsNullOrEmpty(profile.Role) ? profile.DisplayName : $"{profile.DisplayName}, {profile.Role}",
        _ => AutomationProperties.GetName(this) is { Length: > 0 } name ? name : ContentText()
    };

    /// <summary>
    /// Other content describes itself: its own help text or name, else the text its text blocks show (a templated card
    /// has those only once laid out, so <see cref="DescribeLate"/> asks again when the surface loads).
    /// </summary>
    private string? ContentText()
    {
        if (Content is Control content)
        {
            if (AutomationProperties.GetHelpText(content) is { Length: > 0 } help)
            {
                return help;
            }
            if (AutomationProperties.GetName(content) is { Length: > 0 } name)
            {
                return name;
            }
            return CollectText(content);
        }
        return Content is null || _surface is null ? null : CollectText(_surface);
    }

    private static string? CollectText(Visual root)
    {
        StringBuilder? text = null;
        foreach (var visual in root.GetSelfAndVisualDescendants())
        {
            if (visual is TextBlock { IsVisible: true } block && (block.Text ?? block.Inlines?.Text)?.Trim() is { Length: > 0 } part)
            {
                text ??= new StringBuilder();
                if (text.Length > 0)
                {
                    text.Append(", ");
                }
                text.Append(part);
            }
        }
        return text?.ToString();
    }

    /// <summary>Fills in a summary that only the laid-out card could give (the surface loads after the card opens).</summary>
    private void DescribeLate()
    {
        if (!IsOpen || _described || Anchor is not { } anchor || Summary() is not { Length: > 0 } summary)
        {
            return;
        }
        AutomationProperties.SetHelpText(anchor, summary);
        _described = true;
    }

    private void CloseCore()
    {
        StopTimers();
        var reason = _pendingReason;
        _pendingReason = DismissReason.Programmatic;
        if (ReferenceEquals(s_open, this))
        {
            s_open = null;
            s_closedAt = s_clock.Elapsed;
        }
        _insideCard = false;
        if (Anchor is { } anchor)
        {
            AutomationProperties.SetHelpText(anchor, _previousHelpText);
        }
        _presence?.Hide();
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

    /// <summary>Resets the page-wide warm state (tests).</summary>
    internal static void ResetCoordinator()
    {
        s_open = null;
        s_closedAt = TimeSpan.FromDays(-1);
    }

    #endregion
}
