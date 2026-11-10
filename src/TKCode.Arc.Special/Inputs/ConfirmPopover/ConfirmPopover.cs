using System.Windows.Input;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The button of a <see cref="ConfirmPopover"/> that takes keyboard focus when it opens.</summary>
public enum ConfirmPopoverFocus
{
    /// <summary>The cancel button (default): a stray Enter keeps the item.</summary>
    Cancel,

    /// <summary>The confirm button, for actions that are easy to take back.</summary>
    Confirm
}

/// <summary>Arguments of <see cref="ConfirmPopover.Cancelled"/>.</summary>
public class ConfirmPopoverCancelledEventArgs : RoutedEventArgs
{
    public ConfirmPopoverCancelledEventArgs(RoutedEvent routedEvent, DismissReason reason)
        : base(routedEvent)
    {
        Reason = reason;
    }

    /// <summary>How the question was dismissed: the cancel button (<see cref="DismissReason.Programmatic"/>), Escape or a press outside.</summary>
    public DismissReason Reason { get; }
}

/// <summary>
/// A small question anchored to the control that asks it: "Delete this comment?" with Cancel and a confirm button. It
/// opens from its <see cref="Trigger"/> (or any control, through <see cref="AttachProperty"/> or <see cref="Open(Control)"/>),
/// moves focus to Cancel, and gives focus back to the trigger when it closes. Escape and a press outside cancel. The
/// confirm button raises <see cref="Confirmed"/> and runs <see cref="Command"/>; with a <see cref="ConfirmHandler"/> it
/// shows a spinner until the work finishes and closes only when the handler reports success.
/// </summary>
/// <remarks>The panel is hosted by a <see cref="Popover"/>, so it shares its placement, flipping and motion.</remarks>
[TemplatePart(TriggerPresenterPart, typeof(ContentPresenter))]
public class ConfirmPopover : TemplatedControl
{
    public const string TriggerPresenterPart = "PART_TriggerPresenter";

    #region Properties

    /// <summary>The control that opens the question, shown in place of the confirm popover.</summary>
    public static readonly StyledProperty<Control?> TriggerProperty =
        AvaloniaProperty.Register<ConfirmPopover, Control?>(nameof(Trigger));

    /// <summary>Whether the question is open. Two-way.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ConfirmPopover, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The question, short and specific ("Delete this comment?").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ConfirmPopover, string?>(nameof(Title), "Are you sure?");

    /// <summary>An optional line under the question that says what happens ("It is removed for everyone.").</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ConfirmPopover, string?>(nameof(Message));

    /// <summary>The confirm button's label: the action as a verb ("Delete").</summary>
    public static readonly StyledProperty<string> ConfirmTextProperty =
        AvaloniaProperty.Register<ConfirmPopover, string>(nameof(ConfirmText), "Delete");

    /// <summary>The cancel button's label.</summary>
    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<ConfirmPopover, string>(nameof(CancelText), "Cancel");

    /// <summary>True (default) for an action that destroys something: the confirm button and the mark use the danger color.</summary>
    public static readonly StyledProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.Register<ConfirmPopover, bool>(nameof(IsDestructive), true);

    /// <summary>The mark beside the question (a warning triangle by default); null hides it.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ConfirmPopover, Geometry?>(nameof(Icon), ArcIcons.TriangleAlert);

    /// <summary>Preferred side of the trigger (flips when it does not fit).</summary>
    public static readonly StyledProperty<PopoverPlacement> PlacementProperty =
        AvaloniaProperty.Register<ConfirmPopover, PopoverPlacement>(nameof(Placement), PopoverPlacement.Bottom);

    /// <summary>Alignment along the trigger edge.</summary>
    public static readonly StyledProperty<PopoverAlignment> AlignmentProperty =
        AvaloniaProperty.Register<ConfirmPopover, PopoverAlignment>(nameof(Alignment), PopoverAlignment.Center);

    /// <summary>The button that takes focus when the question opens (Cancel by default).</summary>
    public static readonly StyledProperty<ConfirmPopoverFocus> InitialFocusProperty =
        AvaloniaProperty.Register<ConfirmPopover, ConfirmPopoverFocus>(nameof(InitialFocus));

    /// <summary>Runs when the action is confirmed (after <see cref="ConfirmHandler"/> succeeds, when there is one).</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ConfirmPopover, ICommand?>(nameof(Command));

    /// <summary>The parameter of <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<ConfirmPopover, object?>(nameof(CommandParameter));

    /// <summary>
    /// Optional work to run when the confirm button is pressed. The button shows a spinner and Cancel is disabled until it
    /// finishes; true closes the question as confirmed, false (or an exception) keeps it open so the person can try again.
    /// </summary>
    public static readonly StyledProperty<Func<Task<bool>>?> ConfirmHandlerProperty =
        AvaloniaProperty.Register<ConfirmPopover, Func<Task<bool>>?>(nameof(ConfirmHandler));

    /// <summary>True while <see cref="ConfirmHandler"/> runs.</summary>
    public static readonly DirectProperty<ConfirmPopover, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<ConfirmPopover, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>Raised when the action is confirmed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ConfirmedEvent =
        RoutedEvent.Register<ConfirmPopover, RoutedEventArgs>(nameof(Confirmed), RoutingStrategies.Bubble);

    /// <summary>Raised when the question closes without confirming.</summary>
    public static readonly RoutedEvent<ConfirmPopoverCancelledEventArgs> CancelledEvent =
        RoutedEvent.Register<ConfirmPopover, ConfirmPopoverCancelledEventArgs>(nameof(Cancelled), RoutingStrategies.Bubble);

    /// <summary>Attaches a confirm popover to any control: a click on the control opens it.</summary>
    public static readonly AttachedProperty<ConfirmPopover?> AttachProperty =
        AvaloniaProperty.RegisterAttached<ConfirmPopover, Control, ConfirmPopover?>("Attach");

    /// <summary>The confirm popover attached to <paramref name="element"/>.</summary>
    public static ConfirmPopover? GetAttach(Control element) => element.GetValue(AttachProperty);

    /// <summary>Attaches a confirm popover to <paramref name="element"/>.</summary>
    public static void SetAttach(Control element, ConfirmPopover? value) => element.SetValue(AttachProperty, value);

    /// <summary>The control that opens the question, shown in place of the confirm popover.</summary>
    public Control? Trigger { get => GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>Whether the question is open. Two-way.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>The question, short and specific ("Delete this comment?").</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>An optional line under the question that says what happens ("It is removed for everyone.").</summary>
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <summary>The confirm button's label: the action as a verb ("Delete").</summary>
    public string ConfirmText { get => GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }

    /// <summary>The cancel button's label.</summary>
    public string CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <summary>True (default) for an action that destroys something: the confirm button and the mark use the danger color.</summary>
    public bool IsDestructive { get => GetValue(IsDestructiveProperty); set => SetValue(IsDestructiveProperty, value); }

    /// <summary>The mark beside the question (a warning triangle by default); null hides it.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Preferred side of the trigger (flips when it does not fit).</summary>
    public PopoverPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>Alignment along the trigger edge.</summary>
    public PopoverAlignment Alignment { get => GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }

    /// <summary>The button that takes focus when the question opens (Cancel by default).</summary>
    public ConfirmPopoverFocus InitialFocus { get => GetValue(InitialFocusProperty); set => SetValue(InitialFocusProperty, value); }

    /// <summary>Runs when the action is confirmed (after <see cref="ConfirmHandler"/> succeeds, when there is one).</summary>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>The parameter of <see cref="Command"/>.</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>
    /// Optional work to run when the confirm button is pressed. The button shows a spinner and Cancel is disabled until it
    /// finishes; true closes the question as confirmed, false (or an exception) keeps it open so the person can try again.
    /// </summary>
    public Func<Task<bool>>? ConfirmHandler { get => GetValue(ConfirmHandlerProperty); set => SetValue(ConfirmHandlerProperty, value); }

    /// <summary>True while <see cref="ConfirmHandler"/> runs.</summary>
    public bool IsBusy { get => _isBusy; private set => SetAndRaise(IsBusyProperty, ref _isBusy, value); }

    /// <summary>Raised when the action is confirmed.</summary>
    public event EventHandler<RoutedEventArgs>? Confirmed
    {
        add => AddHandler(ConfirmedEvent, value);
        remove => RemoveHandler(ConfirmedEvent, value);
    }

    /// <summary>Raised when the question closes without confirming (cancel button, Escape or a press outside).</summary>
    public event EventHandler<ConfirmPopoverCancelledEventArgs>? Cancelled
    {
        add => AddHandler(CancelledEvent, value);
        remove => RemoveHandler(CancelledEvent, value);
    }

    #endregion

    private Popover? _popover;
    private ConfirmPopoverPanel? _panel;
    private Control? _attachedTarget;
    private Control? _hookedTrigger;
    private Control? _openAnchor;
    private bool _parentedToTarget;
    private bool _confirmed;
    private bool _isBusy;
    private bool _syncing;
    private int _busyGeneration;
    private TaskCompletionSource<bool>? _pending;

    static ConfirmPopover()
    {
        AttachProperty.Changed.AddClassHandler<Control>(OnAttachChanged);
        FocusableProperty.OverrideDefaultValue<ConfirmPopover>(false);
        SpecialFocusModality.Watch();
    }

    /// <summary>The control the question is anchored to while it is open.</summary>
    public Control? Anchor => _openAnchor ?? _attachedTarget ?? Trigger;

    internal ConfirmPopoverPanel? Panel => _panel;

    internal Popover? Host => _popover;

    /// <summary>Opens the question at its trigger (or at the control it is attached to).</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Opens the question anchored to <paramref name="anchor"/> (any control in a window).</summary>
    public void Open(Control anchor)
    {
        if (IsOpen && !ReferenceEquals(anchor, Anchor))
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        _openAnchor = anchor;
        SetCurrentValue(IsOpenProperty, true);
    }

    /// <summary>Closes the question as cancelled.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>
    /// Opens the question (at <paramref name="anchor"/> when given) and completes when it closes: true when the action was
    /// confirmed, false when it was cancelled. A question that is already open is answered by the same task.
    /// </summary>
    public Task<bool> ConfirmAsync(Control? anchor = null)
    {
        if (IsOpen && _pending is not null && (anchor is null || ReferenceEquals(anchor, Anchor)))
        {
            return _pending.Task;
        }
        var pending = new TaskCompletionSource<bool>();
        if (anchor is not null)
        {
            Open(anchor);
        }
        else
        {
            Open();
        }
        if (!IsOpen)
        {
            pending.TrySetResult(false);
            return pending.Task;
        }
        _pending = pending;
        return pending.Task;
    }

    /// <summary>Confirms as the confirm button does (runs <see cref="ConfirmHandler"/> first when there is one).</summary>
    public void Confirm() => _ = ConfirmCoreAsync();

    #region Trigger

    private static void OnAttachChanged(Control target, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is ConfirmPopover old && ReferenceEquals(old._attachedTarget, target))
        {
            old.Close();
            old.UnhookTrigger();
            old._attachedTarget = null;
        }
        if (e.NewValue is ConfirmPopover popover)
        {
            popover._attachedTarget = target;
            popover.HookTrigger(target);
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
        if (trigger is AvaloniaButton button)
        {
            button.Click += OnTriggerClick;
        }
        else
        {
            trigger.Tapped += OnTriggerTapped;
            trigger.KeyDown += OnTriggerKeyDown;
        }
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
        if (_hookedTrigger is AvaloniaButton button)
        {
            button.Click -= OnTriggerClick;
        }
        else
        {
            _hookedTrigger.Tapped -= OnTriggerTapped;
            _hookedTrigger.KeyDown -= OnTriggerKeyDown;
        }
        _hookedTrigger = null;
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
            Close();
            return;
        }
        if (trigger is not null)
        {
            Open(trigger);
        }
        else
        {
            Open();
        }
    }

    private void UpdateTriggerState()
    {
        if (_hookedTrigger is { } trigger)
        {
            AutomationProperties.SetItemStatus(trigger, IsOpen ? "Expanded" : "Collapsed");
        }
    }

    #endregion

    #region Open / close

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
            if (_syncing)
            {
                return;
            }
            if (change.GetNewValue<bool>())
            {
                OpenCore();
            }
            else
            {
                _popover?.Close();
            }
        }
    }

    private void EnsurePopover()
    {
        if (_popover is not null)
        {
            return;
        }
        _panel = new ConfirmPopoverPanel(this);
        _popover = new Popover { Content = _panel };
        _popover.Bind(Popover.PlacementProperty, this.GetObservable(PlacementProperty));
        _popover.Bind(Popover.AlignmentProperty, this.GetObservable(AlignmentProperty));
        LogicalChildren.Add(_popover);
        _popover.Opened += OnPopoverOpened;
        _popover.Closed += OnPopoverClosed;
    }

    private void OpenCore()
    {
        var anchor = Anchor ?? this;
        if (TopLevel.GetTopLevel(anchor) is null)
        {
            // Not in a window yet: nothing to anchor to.
            SetOpenSilently(false);
            return;
        }
        EnsurePopover();
        if (Parent is null && _attachedTarget is not null)
        {
            // An attached question joins its target's logical tree while open (styles, resources, DataContext).
            ((ISetLogicalParent)this).SetParent(_attachedTarget);
            _parentedToTarget = true;
        }
        _confirmed = false;
        ++_busyGeneration;
        _panel!.SetBusy(false);
        AutomationProperties.SetName(_popover!, Title ?? string.Empty);
        _popover!.ShowAt(anchor);
        UpdateTriggerState();
    }

    private void OnPopoverOpened(object? sender, EventArgs e)
    {
        if (_panel is null)
        {
            return;
        }
        // The popover focused the first button; move to the chosen one, with the ring after keyboard use.
        var target = InitialFocus == ConfirmPopoverFocus.Confirm ? _panel.ConfirmButton : _panel.CancelButton;
        target?.Focus(SpecialFocusModality.For(Anchor));
    }

    private void OnPopoverClosed(object? sender, OverlayClosedEventArgs e)
    {
        SetOpenSilently(false);
        UpdateTriggerState();
        _openAnchor = null;
        var confirmed = _confirmed;
        _confirmed = false;
        if (_parentedToTarget)
        {
            _parentedToTarget = false;
            ((ISetLogicalParent)this).SetParent(null);
        }
        var pending = _pending;
        _pending = null;
        if (confirmed)
        {
            RaiseEvent(new RoutedEventArgs(ConfirmedEvent));
            if (Command is { } command && command.CanExecute(CommandParameter))
            {
                command.Execute(CommandParameter);
            }
        }
        else
        {
            RaiseEvent(new ConfirmPopoverCancelledEventArgs(CancelledEvent, e.Reason));
        }
        pending?.TrySetResult(confirmed);
    }

    private void SetOpenSilently(bool value)
    {
        _syncing = true;
        try
        {
            SetCurrentValue(IsOpenProperty, value);
        }
        finally
        {
            _syncing = false;
        }
    }

    internal void CancelFromPanel() => _popover?.Close();

    internal async Task ConfirmCoreAsync()
    {
        if (!IsOpen || IsBusy || _popover is null)
        {
            return;
        }
        if (ConfirmHandler is { } handler)
        {
            var generation = ++_busyGeneration;
            IsBusy = true;
            _panel?.SetBusy(true);
            bool ok;
            try
            {
                ok = await handler();
            }
            catch (Exception)
            {
                ok = false;
            }
            IsBusy = false;
            var current = generation == _busyGeneration && IsOpen;
            if (current)
            {
                _panel?.SetBusy(false);
            }
            if (!ok)
            {
                if (current)
                {
                    // Keep the question open for another try, with focus on the confirm button.
                    _panel?.ConfirmButton?.Focus(NavigationMethod.Unspecified);
                }
                return;
            }
            if (!current)
            {
                // Dismissed while the work ran, but the work did happen: report it.
                RaiseEvent(new RoutedEventArgs(ConfirmedEvent));
                if (Command is { } late && late.CanExecute(CommandParameter))
                {
                    late.Execute(CommandParameter);
                }
                return;
            }
        }
        _confirmed = true;
        _popover.Close();
    }

    #endregion
}
