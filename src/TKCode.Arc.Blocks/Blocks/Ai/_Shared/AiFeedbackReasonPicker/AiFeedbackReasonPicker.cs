using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="AiFeedbackReasonPicker.ReasonChosenEvent"/>: the reason the person gave for a down rating.</summary>
internal sealed class AiFeedbackReasonEventArgs : RoutedEventArgs
{
    public AiFeedbackReasonEventArgs(RoutedEvent routedEvent, object source, string reason, int index) : base(routedEvent, source)
    {
        Reason = reason;
        Index = index;
    }

    /// <summary>The reason, as shown.</summary>
    public string Reason { get; }

    /// <summary>Its position in the reasons.</summary>
    public int Index { get; }
}

/// <summary>
/// Asks why an AI answer was rated down (Arc answer): a popover above <see cref="PlacementTarget"/> (the thumbs down), left
/// edges aligned, 8px away (below when there is no room), with <see cref="Title"/> and a pill per reason. Opening focuses
/// the first reason (with its keyboard ring when opened from the keyboard); Tab moves between the reasons, round from the
/// last to the first. Choosing one raises <see cref="ReasonChosen"/> at once (the rating counts from here) and the reasons
/// give way in place to the thanks: they fade out in 160ms while the thanks fade in over 240ms after 60ms and the popover
/// springs to its new height (Arc's smooth spring) with its bottom edge where it was. After
/// <see cref="ConfirmationDuration"/> (1.5s) it closes by itself and focus goes back to the target. Escape closes it
/// without a reason and focus goes back to the target; a press outside closes it and leaves focus where it lands. Closing
/// without a reason rates nothing. Reduced motion crossfades in 120ms and jumps the height; Off swaps at once.
/// </summary>
internal sealed class AiFeedbackReasonPicker : Control
{
    /// <summary>The popover sits this far from its target.</summary>
    internal const double PopoverGap = 8;

    /// <summary>How far the popover starts toward its target while entering.</summary>
    internal const double PopoverTravel = 5;

    #region Properties

    /// <summary>The control the popover opens against (the thumbs down).</summary>
    public static readonly StyledProperty<Control?> PlacementTargetProperty =
        AvaloniaProperty.Register<AiFeedbackReasonPicker, Control?>(nameof(PlacementTarget));

    /// <summary>The question above the reasons and the popover's accessible name ("What went wrong?").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiFeedbackReasonPicker, string?>(nameof(Title));

    /// <summary>The reasons to choose from.</summary>
    public static readonly StyledProperty<IEnumerable<string>?> ReasonsProperty =
        AvaloniaProperty.Register<AiFeedbackReasonPicker, IEnumerable<string>?>(nameof(Reasons));

    /// <summary>The first line of the thanks ("Thanks for the feedback").</summary>
    public static readonly StyledProperty<string?> ThanksTitleProperty =
        AvaloniaProperty.Register<AiFeedbackReasonPicker, string?>(nameof(ThanksTitle));

    /// <summary>The second line of the thanks; see also <see cref="ThanksDescriptionFor"/>.</summary>
    public static readonly StyledProperty<string?> ThanksDescriptionProperty =
        AvaloniaProperty.Register<AiFeedbackReasonPicker, string?>(nameof(ThanksDescription));

    /// <summary>Makes the second line of the thanks from the chosen reason ("Noted: too long"); null shows <see cref="ThanksDescription"/>.</summary>
    public static readonly StyledProperty<Func<string, string>?> ThanksDescriptionForProperty =
        AvaloniaProperty.Register<AiFeedbackReasonPicker, Func<string, string>?>(nameof(ThanksDescriptionFor));

    /// <summary>How long the thanks stay before the popover closes by itself (1.5s).</summary>
    public static readonly StyledProperty<TimeSpan> ConfirmationDurationProperty =
        AvaloniaProperty.Register<AiFeedbackReasonPicker, TimeSpan>(nameof(ConfirmationDuration), TimeSpan.FromSeconds(1.5));

    /// <summary>Whether the popover is open (read-only).</summary>
    public static readonly DirectProperty<AiFeedbackReasonPicker, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<AiFeedbackReasonPicker, bool>(nameof(IsOpen), o => o.IsOpen);

    /// <summary>Whether the popover shows the thanks (read-only).</summary>
    public static readonly DirectProperty<AiFeedbackReasonPicker, bool> IsConfirmingProperty =
        AvaloniaProperty.RegisterDirect<AiFeedbackReasonPicker, bool>(nameof(IsConfirming), o => o.IsConfirming);

    /// <summary>Raised (bubbling) when a reason is chosen, before the thanks show.</summary>
    public static readonly RoutedEvent<AiFeedbackReasonEventArgs> ReasonChosenEvent =
        RoutedEvent.Register<AiFeedbackReasonPicker, AiFeedbackReasonEventArgs>(nameof(ReasonChosen), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PlacementTargetProperty"/>
    public Control? PlacementTarget { get => GetValue(PlacementTargetProperty); set => SetValue(PlacementTargetProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ReasonsProperty"/>
    public IEnumerable<string>? Reasons { get => GetValue(ReasonsProperty); set => SetValue(ReasonsProperty, value); }

    /// <inheritdoc cref="ThanksTitleProperty"/>
    public string? ThanksTitle { get => GetValue(ThanksTitleProperty); set => SetValue(ThanksTitleProperty, value); }

    /// <inheritdoc cref="ThanksDescriptionProperty"/>
    public string? ThanksDescription { get => GetValue(ThanksDescriptionProperty); set => SetValue(ThanksDescriptionProperty, value); }

    /// <inheritdoc cref="ThanksDescriptionForProperty"/>
    public Func<string, string>? ThanksDescriptionFor { get => GetValue(ThanksDescriptionForProperty); set => SetValue(ThanksDescriptionForProperty, value); }

    /// <inheritdoc cref="ConfirmationDurationProperty"/>
    public TimeSpan ConfirmationDuration { get => GetValue(ConfirmationDurationProperty); set => SetValue(ConfirmationDurationProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => _isOpen; private set => SetAndRaise(IsOpenProperty, ref _isOpen, value); }

    /// <inheritdoc cref="IsConfirmingProperty"/>
    public bool IsConfirming { get => _isConfirming; private set => SetAndRaise(IsConfirmingProperty, ref _isConfirming, value); }

    /// <inheritdoc cref="ReasonChosenEvent"/>
    public event EventHandler<AiFeedbackReasonEventArgs>? ReasonChosen
    {
        add => AddHandler(ReasonChosenEvent, value);
        remove => RemoveHandler(ReasonChosenEvent, value);
    }

    #endregion

    private readonly List<AiFeedbackReasonButton> _buttons = new();
    private AiFeedbackReasonPanel? _panel;
    private AiFeedbackReasonsView? _reasonsView;
    private AiFeedbackThanksView? _thanksView;
    private AiActionsPopup? _popup;
    private AiActionsPopupPresence? _presence;
    private TopLevel? _hooked;
    private DispatcherTimer? _timer;
    private DateTimeOffset? _closesAt;
    private bool _keyboard;
    private bool _isOpen;
    private bool _isConfirming;

    static AiFeedbackReasonPicker()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<AiFeedbackReasonPicker>(false);
        FocusableProperty.OverrideDefaultValue<AiFeedbackReasonPicker>(false);
    }

    /// <summary>Where the time of the thanks comes from (tests move it by hand and call <see cref="Tick"/>).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>The popover surface (null until the first open).</summary>
    internal AiFeedbackReasonPanel? Panel => _panel;

    /// <summary>The popup of the popover.</summary>
    internal AiActionsPopup? Popup => _popup;

    /// <summary>The presence (motion state, tests).</summary>
    internal AiActionsPopupPresence? Presence => _presence;

    /// <summary>The reason pills, in order.</summary>
    internal IReadOnlyList<AiFeedbackReasonButton> ReasonButtons => _buttons;

    /// <summary>The reasons view (title and pills).</summary>
    internal AiFeedbackReasonsView? ReasonsView => _reasonsView;

    /// <summary>The thanks view.</summary>
    internal AiFeedbackThanksView? ThanksView => _thanksView;

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            if (_reasonsView is not null)
            {
                _reasonsView.Title = Title;
            }
            if (_panel is not null)
            {
                AutomationProperties.SetName(_panel, Title ?? string.Empty);
            }
        }
        else if (change.Property == ReasonsProperty && IsOpen && !IsConfirming)
        {
            BuildReasons();
        }
        else if (change.Property == PlacementTargetProperty)
        {
            Close(returnFocus: false, NavigationMethod.Pointer, immediate: true);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Close(returnFocus: false, NavigationMethod.Pointer, immediate: true);
    }

    #region Open and close

    /// <summary>Opens the popover or closes it when open (focus back on the target).</summary>
    public void Toggle(bool keyboard)
    {
        if (IsOpen)
        {
            Close(returnFocus: true, keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        else
        {
            Open(keyboard);
        }
    }

    /// <summary>Opens the popover with the reasons and focuses the first one (with its ring when <paramref name="keyboard"/>).</summary>
    public void Open(bool keyboard = false)
    {
        if (IsOpen || PlacementTarget is not { } target || !target.IsEffectivelyEnabled || TopLevel.GetTopLevel(target) is not { } topLevel)
        {
            return;
        }
        EnsurePopover();
        _keyboard = keyboard;
        StopTimer();
        IsConfirming = false;
        _reasonsView!.Title = Title;
        BuildReasons();
        _panel!.ShowAtOnce(_reasonsView);
        AutomationProperties.SetName(_panel, Title ?? string.Empty);
        IsOpen = true;
        _popup!.PlacementTarget = target;
        _popup.OverlayInputPassThroughElement = target;
        _presence!.Show();
        Hook(topLevel);
        FocusFirstReason();
        // The surface is laid out in the popup only now: focus again once it is in place.
        Dispatcher.UIThread.Post(FocusFirstReason, DispatcherPriority.Loaded);
    }

    private void FocusFirstReason()
    {
        if (!IsOpen || IsConfirming || _panel is null)
        {
            return;
        }
        if (_buttons.Count > 0)
        {
            if (!_buttons[0].IsFocused && !_panel.IsKeyboardFocusWithin)
            {
                _buttons[0].Focus(_keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
            }
        }
        else if (!_panel.IsKeyboardFocusWithin)
        {
            _panel.Focus(NavigationMethod.Pointer);
        }
    }

    /// <summary>
    /// Closes the popover. With <paramref name="returnFocus"/> (and always when focus is inside it) the target takes focus
    /// back, by <paramref name="method"/> (Tab shows its keyboard ring and its tooltip).
    /// </summary>
    public void Close(bool returnFocus = true, NavigationMethod method = NavigationMethod.Tab, bool immediate = false)
    {
        if (!IsOpen)
        {
            return;
        }
        var inside = _panel?.IsKeyboardFocusWithin == true;
        IsOpen = false;
        StopTimer();
        Unhook();
        // Focus goes back before the surface hides (a focused pill that leaves would drop focus without a method).
        if ((returnFocus || inside) && PlacementTarget is { } target && TopLevel.GetTopLevel(target) is not null)
        {
            target.Focus(method);
        }
        _presence?.Hide(immediate);
        IsConfirming = false;
    }

    private void EnsurePopover()
    {
        if (_popup is not null)
        {
            return;
        }
        _panel = new AiFeedbackReasonPanel(this);
        _reasonsView = new AiFeedbackReasonsView();
        _thanksView = new AiFeedbackThanksView();
        _popup = new AiActionsPopup(isSubmenu: false)
        {
            Placement = PlacementMode.TopEdgeAlignedLeft,
            MarginToAnchor = PopoverGap
        };
        _presence = new AiActionsPopupPresence(_panel, _popup, PopoverTravel);
        LogicalChildren.Add(_popup);
    }

    private void BuildReasons()
    {
        if (_reasonsView is null)
        {
            return;
        }
        foreach (var button in _buttons)
        {
            button.Click -= OnReasonClick;
        }
        _buttons.Clear();
        _reasonsView.Reasons.Children.Clear();
        if (Reasons is not { } reasons)
        {
            return;
        }
        var index = 0;
        foreach (var reason in reasons)
        {
            if (string.IsNullOrEmpty(reason))
            {
                continue;
            }
            var button = new AiFeedbackReasonButton(reason, index++);
            button.Click += OnReasonClick;
            _buttons.Add(button);
            _reasonsView.Reasons.Children.Add(button);
        }
    }

    #endregion

    #region Choice and the thanks

    private void OnReasonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is AiFeedbackReasonButton button)
        {
            Choose(button);
        }
    }

    /// <summary>Records the reason, shows the thanks in place and starts the time to close.</summary>
    private void Choose(AiFeedbackReasonButton button)
    {
        if (!IsOpen || IsConfirming || _panel is null || _thanksView is null)
        {
            return;
        }
        // A reason picked from the keyboard brings the ring back to the target at the end.
        _keyboard = button.LastActivationWasKeyboard;
        IsConfirming = true;
        _thanksView.Title = ThanksTitle;
        _thanksView.Description = ThanksDescriptionFor?.Invoke(button.Reason) ?? ThanksDescription;
        // Focus leaves the pill that is going away and waits on the surface (Escape still closes).
        _panel.Focus(NavigationMethod.Pointer);
        _panel.ShownContent = _thanksView;
        StartTimer();
        RaiseEvent(new AiFeedbackReasonEventArgs(ReasonChosenEvent, this, button.Reason, button.Index));
    }

    private void StartTimer()
    {
        _closesAt = Time.GetUtcNow() + ConfirmationDuration;
        if (_timer is null)
        {
            _timer = new DispatcherTimer();
            _timer.Tick += (_, _) => Tick();
        }
        _timer.Stop();
        _timer.Interval = ConfirmationDuration > TimeSpan.Zero ? ConfirmationDuration : TimeSpan.FromMilliseconds(1);
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _closesAt = null;
    }

    /// <summary>Closes the thanks once their time is up (the timer calls it; tests call it after moving the time).</summary>
    internal void Tick()
    {
        if (_closesAt is not { } until)
        {
            _timer?.Stop();
            return;
        }
        var now = Time.GetUtcNow();
        if (now < until)
        {
            // Early (a timer runs on its own clock): wait for the rest.
            if (_timer is not null)
            {
                _timer.Interval = until - now;
            }
            return;
        }
        StopTimer();
        Close(returnFocus: true, _keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }

    #endregion

    #region Keys and outside presses

    /// <summary>Keys from the popover (it or a pill has focus).</summary>
    internal void OnPanelKeyDown(KeyEventArgs e)
    {
        if (e.Handled || !IsOpen)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(returnFocus: true, NavigationMethod.Tab);
        }
        else if (e.Key == Key.Tab && IsConfirming)
        {
            // Nothing to move between while the thanks show.
            e.Handled = true;
        }
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

    private static bool IsWithin(Visual? root, Visual visual) =>
        root is not null && (ReferenceEquals(root, visual) || root.IsVisualAncestorOf(visual));

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpen || e.Source is not Visual source)
        {
            return;
        }
        // Presses in the popover are its own; a press on the target passes through (it toggles the popover itself).
        if (IsWithin(_panel, source) || IsWithin(PlacementTarget, source))
        {
            return;
        }
        Close(returnFocus: false, NavigationMethod.Pointer);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        // Keys from focus inside the popover are handled by its surface; this catches Escape with focus elsewhere.
        if (!e.Handled && e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            Close(returnFocus: true, NavigationMethod.Tab);
        }
    }

    #endregion
}
