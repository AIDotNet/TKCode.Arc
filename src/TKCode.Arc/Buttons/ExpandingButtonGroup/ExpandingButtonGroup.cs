using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="ExpandingButtonGroup.ActionInvoked"/>.</summary>
public sealed class ExpandingButtonGroupActionEventArgs : RoutedEventArgs
{
    public ExpandingButtonGroupActionEventArgs(RoutedEvent routedEvent, ExpandingButtonGroupItem item, object? data) : base(routedEvent)
    {
        Item = item;
        Data = data;
    }

    /// <summary>The action that runs.</summary>
    public ExpandingButtonGroupItem Item { get; }

    /// <summary>The source item (the button itself for inline items, the data object for <c>ItemsSource</c>).</summary>
    public object? Data { get; }
}

/// <summary>
/// A compact pill of icon buttons for 3 to 6 related actions (message, file and card actions). One action always shows
/// its label; the one you point at or focus takes it over while its neighbours slide aside (width springs inside one
/// row, P5), and an action can confirm in place: the icon draws a check and the label becomes a past tense word (P2,
/// P15). On touch the first tap reveals the label and a second tap runs the action. One Tab stop; arrow keys move and
/// wrap, Home and End jump, Enter and Space run.
/// </summary>
[PseudoClasses(SmallPseudoClass)]
public class ExpandingButtonGroup : ItemsControl
{
    public const string SmallPseudoClass = ":small";
    public const string StatusPart = "PART_Status";

    /// <summary>Pixels the pointer must travel into a neighbouring action before the expansion moves to it.</summary>
    internal const double Hysteresis = 4;

    #region 公共属性定义

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ExpandingButtonGroup, string>(nameof(Label), "Actions");

    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<ExpandingButtonGroup, ControlSize>(nameof(Size), ControlSize.Medium);

    public static readonly StyledProperty<object?> DefaultExpandedItemProperty =
        AvaloniaProperty.Register<ExpandingButtonGroup, object?>(nameof(DefaultExpandedItem));

    public static readonly StyledProperty<TimeSpan> ConfirmDurationProperty =
        AvaloniaProperty.Register<ExpandingButtonGroup, TimeSpan>(nameof(ConfirmDuration), TimeSpan.FromMilliseconds(1400));

    public static readonly DirectProperty<ExpandingButtonGroup, ExpandingButtonGroupItem?> ExpandedItemProperty =
        AvaloniaProperty.RegisterDirect<ExpandingButtonGroup, ExpandingButtonGroupItem?>(nameof(ExpandedItem), o => o.ExpandedItem);

    public static readonly RoutedEvent<ExpandingButtonGroupActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<ExpandingButtonGroup, ExpandingButtonGroupActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    /// <summary>Accessible name of the toolbar, such as "Message actions".</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Small (28px, 14px icons) or Medium (36px, 16px icons; default). Large is treated as Medium.</summary>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>The action (item or its container) whose label shows at rest; null uses the first enabled action.</summary>
    public object? DefaultExpandedItem { get => GetValue(DefaultExpandedItemProperty); set => SetValue(DefaultExpandedItemProperty, value); }

    /// <summary>How long a confirmation stays (default 1.4s).</summary>
    public TimeSpan ConfirmDuration { get => GetValue(ConfirmDurationProperty); set => SetValue(ConfirmDurationProperty, value); }

    private ExpandingButtonGroupItem? _expandedItem;

    /// <summary>The action currently showing its label.</summary>
    public ExpandingButtonGroupItem? ExpandedItem
    {
        get => _expandedItem;
        private set => SetAndRaise(ExpandedItemProperty, ref _expandedItem, value);
    }

    /// <summary>Raised whenever an action runs, before its own <see cref="ExpandingButtonGroupItem.AsyncAction"/>.</summary>
    public event EventHandler<ExpandingButtonGroupActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    #endregion

    // Mirrors of what the pointer and keyboard are doing right now, so leave and blur fall back correctly.
    private ExpandingButtonGroupItem? _intent;
    private ExpandingButtonGroupItem? _pointerOver;
    private Point? _pointerAt;
    private ExpandingButtonGroupItem? _keyFocus;
    private ExpandingButtonGroupItem? _focused;
    private ExpandingButtonGroupItem? _touchOpen;
    private ExpandingButtonGroupItem? _done;
    private ExpandingButtonGroupItem? _busy;
    private (ExpandingButtonGroupItem Item, PointerType Type, bool WasExpanded)? _press;
    private PausableTimer? _confirmTimer;
    private ButtonsLiveRegion? _status;
    private TopLevel? _outsideWatch;

    public ExpandingButtonGroup()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
    }

    static ExpandingButtonGroup()
    {
        FocusableProperty.OverrideDefaultValue<ExpandingButtonGroup>(false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _status = e.NameScope.Find<ButtonsLiveRegion>(StatusPart);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        AutomationProperties.SetName(this, Label);
        PseudoClasses.Set(SmallPseudoClass, Size == ControlSize.Small);
    }

    #region 容器

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<ExpandingButtonGroupItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new ExpandingButtonGroupItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is ExpandingButtonGroupItem button)
        {
            if (!ReferenceEquals(item, button) && string.IsNullOrEmpty(button.Label))
            {
                button.Label = item?.ToString() ?? string.Empty;
            }
            button.Group = this;
            button.SetSmall(Size == ControlSize.Small);
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        // Realized now: the resting action can be resolved against every container.
        Refresh();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is ExpandingButtonGroupItem button)
        {
            button.Group = null;
            Forget(button);
        }
        Refresh();
    }

    private void Forget(ExpandingButtonGroupItem item)
    {
        _intent = Drop(_intent, item);
        _pointerOver = Drop(_pointerOver, item);
        _keyFocus = Drop(_keyFocus, item);
        _focused = Drop(_focused, item);
        _touchOpen = Drop(_touchOpen, item);
        _done = Drop(_done, item);
        _busy = Drop(_busy, item);
    }

    private static ExpandingButtonGroupItem? Drop(ExpandingButtonGroupItem? field, ExpandingButtonGroupItem item) =>
        ReferenceEquals(field, item) ? null : field;

    private List<ExpandingButtonGroupItem> Buttons => GetRealizedContainers().OfType<ExpandingButtonGroupItem>().Where(b => b.IsVisible).ToList();

    #endregion

    /// <summary>The resting action: <see cref="DefaultExpandedItem"/>, else the first enabled one.</summary>
    private ExpandingButtonGroupItem? RestItem
    {
        get
        {
            var buttons = Buttons;
            if (DefaultExpandedItem is { } wanted)
            {
                var match = buttons.FirstOrDefault(b => ReferenceEquals(b, wanted) || Equals(ItemFromContainer(b), wanted));
                if (match is not null)
                {
                    return match;
                }
            }
            return buttons.FirstOrDefault(b => !b.IsDisabled && b.IsEffectivelyEnabled) ?? buttons.FirstOrDefault();
        }
    }

    /// <summary>Applies expansion and the Tab stop from the current intent.</summary>
    private void Refresh()
    {
        var rest = RestItem;
        var expanded = _intent is not null && _intent.Group == this ? _intent : rest;
        ExpandedItem = expanded;
        foreach (var button in Buttons)
        {
            button.SetExpanded(ReferenceEquals(button, expanded));
        }
        var stop = _focused is not null && _focused.Group == this ? _focused : rest;
        if (stop is not null)
        {
            KeyboardNavigation.SetTabOnceActiveElement(this, stop);
        }
    }

    private void SetIntent(ExpandingButtonGroupItem? item)
    {
        _intent = item;
        Refresh();
        WatchOutside(_touchOpen is not null && ReferenceEquals(_touchOpen, _intent));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == SizeProperty)
        {
            PseudoClasses.Set(SmallPseudoClass, Size == ControlSize.Small);
            foreach (var button in Buttons)
            {
                button.SetSmall(Size == ControlSize.Small);
            }
        }
        else if (change.Property == DefaultExpandedItemProperty)
        {
            Refresh();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && !change.GetNewValue<bool>())
        {
            _keyFocus = null;
            _touchOpen = null;
            SetIntent(_pointerOver ?? _done);
        }
    }

    #region 指针

    private ExpandingButtonGroupItem? ItemAt(Point point, out Rect bounds)
    {
        foreach (var button in Buttons)
        {
            if (button.TranslatePoint(default, this) is { } origin)
            {
                var rect = new Rect(origin, button.Bounds.Size);
                if (rect.Contains(point))
                {
                    bounds = rect;
                    return button;
                }
            }
        }
        bounds = default;
        return null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        // Hover follows real pointer movement only: a button that slides under a resting pointer must not take over.
        var at = e.GetPosition(this);
        if (_pointerAt is { } last && Math.Abs(last.X - at.X) < 1 && Math.Abs(last.Y - at.Y) < 1)
        {
            return;
        }
        _pointerAt = at;
        var item = ItemAt(at, out var box);
        if (item is null || ReferenceEquals(item, _pointerOver))
        {
            return;
        }
        // Hysteresis: moving to the next action needs the pointer a few pixels inside it.
        if (_pointerOver is not null && (at.X < box.Left + Hysteresis || at.X > box.Right - Hysteresis))
        {
            return;
        }
        _pointerOver = item;
        SetIntent(item);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        _pointerAt = null;
        _pointerOver = null;
        SetIntent(_done ?? _keyFocus);
    }

    internal void OnItemPointerPressed(ExpandingButtonGroupItem item, PointerType type)
    {
        _press = (item, type, ReferenceEquals(ExpandedItem, item));
    }

    private void WatchOutside(bool watch)
    {
        var topLevel = watch ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(topLevel, _outsideWatch))
        {
            return;
        }
        _outsideWatch?.RemoveHandler(PointerPressedEvent, OnOutsidePointerPressed);
        _outsideWatch = topLevel;
        _outsideWatch?.AddHandler(PointerPressedEvent, OnOutsidePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>A tap outside closes a label opened by a first tap.</summary>
    private void OnOutsidePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source)))
        {
            return;
        }
        _touchOpen = null;
        SetIntent(_done ?? _keyFocus);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        WatchOutside(false);
        _confirmTimer?.Stop();
        if (_done is { } done)
        {
            _done = null;
            done.SetDone(false);
        }
    }

    #endregion

    #region 激活

    /// <summary>Called by an item instead of its default click.</summary>
    internal void OnItemClicked(ExpandingButtonGroupItem item)
    {
        var last = _press;
        _press = null;
        // Touch has no hover: the first tap opens the label, the second runs it. An item already showing its label runs at once.
        if (last is { } press && ReferenceEquals(press.Item, item) && press.Type == PointerType.Touch && !press.WasExpanded)
        {
            _touchOpen = item;
            SetIntent(item);
            return;
        }
        if (last?.Type == PointerType.Touch)
        {
            _touchOpen = item;
        }
        _ = ActivateAsync(item);
    }

    /// <summary>Runs <paramref name="item"/>'s action as a press would (skips disabled, busy and confirming actions).</summary>
    public Task ActivateAsync(ExpandingButtonGroupItem item)
    {
        if (item.IsDisabled || !item.IsEffectivelyEnabled || ReferenceEquals(_busy, item) || ReferenceEquals(_done, item))
        {
            return Task.CompletedTask;
        }
        return RunAsync(item);
    }

    private async Task RunAsync(ExpandingButtonGroupItem item)
    {
        SetIntent(item);
        RaiseEvent(new ExpandingButtonGroupActionEventArgs(ActionInvokedEvent, item, ItemFromContainer(item) ?? item));
        bool result;
        try
        {
            item.RaiseClick();
            var action = item.AsyncAction;
            if (action is null)
            {
                result = true;
            }
            else
            {
                var task = action();
                if (!task.IsCompleted)
                {
                    SetBusy(item);
                }
                result = await task;
            }
        }
        catch (Exception)
        {
            result = false;
        }
        finally
        {
            if (ReferenceEquals(_busy, item))
            {
                SetBusy(null);
            }
        }
        if (!result || string.IsNullOrEmpty(item.DoneLabel) || item.Group != this)
        {
            return;
        }
        Confirm(item);
    }

    private void SetBusy(ExpandingButtonGroupItem? item)
    {
        _busy?.SetBusy(false);
        _busy = item;
        _busy?.SetBusy(true);
    }

    private void Confirm(ExpandingButtonGroupItem item)
    {
        _confirmTimer?.Stop();
        if (_done is { } previous && !ReferenceEquals(previous, item))
        {
            previous.SetDone(false);
        }
        _done = item;
        item.SetDone(true);
        // The same word is announced again on a repeated action.
        _status?.Announce(item.DoneLabel);
        _confirmTimer ??= new PausableTimer(ConfirmDuration, EndConfirm);
        _confirmTimer.Restart(ConfirmDuration);
    }

    private void EndConfirm()
    {
        if (_done is not { } item)
        {
            return;
        }
        _done = null;
        item.SetDone(false);
        // Collapse unless the pointer or the keyboard is still on it.
        if (!ReferenceEquals(_pointerOver, item) && !ReferenceEquals(_keyFocus, item))
        {
            if (ReferenceEquals(_touchOpen, item))
            {
                _touchOpen = null;
            }
            if (ReferenceEquals(_intent, item))
            {
                SetIntent(_pointerOver ?? _keyFocus);
            }
        }
    }

    #endregion

    #region 键盘

    internal void OnItemFocused(ExpandingButtonGroupItem item, NavigationMethod method)
    {
        _focused = item;
        // Only keyboard focus expands; a click or a tap already has its own path.
        if (method is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            _keyFocus = item;
            SetIntent(item);
        }
        else
        {
            Refresh();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var list = Buttons;
        var index = list.FindIndex(b => b.IsFocused);
        if (index < 0)
        {
            return;
        }
        var last = list.Count - 1;
        var target = e.Key switch
        {
            Key.Right => index == last ? 0 : index + 1,
            Key.Left => index == 0 ? last : index - 1,
            Key.Home => 0,
            Key.End => last,
            _ => -1
        };
        if (target < 0)
        {
            return;
        }
        e.Handled = true;
        list[target].Focus(NavigationMethod.Directional);
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpandingButtonGroupAutomationPeer(this);

    private sealed class ExpandingButtonGroupAutomationPeer : ItemsControlAutomationPeer
    {
        public ExpandingButtonGroupAutomationPeer(ExpandingButtonGroup owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;
    }
}
