using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Arc toast stack: short, non-blocking results of what someone just did, stacked at the edge of the window until
/// they reach for them. New toasts rise from the bottom edge, older ones tuck 14px behind and shrink by 5% per step;
/// hovering or focusing the stack fans it out into a list (Alt+T moves focus in). Toasts pause while the stack is
/// open, dragged or the window is minimized, swipe right to dismiss, and morph in place when updated
/// (<see cref="Update"/>): a loading toast becomes its result.
/// <para>
/// Place one in the window (over the content, for example as the last child of the root panel) or let
/// <see cref="ToastHost"/> create one on the window's overlay layer; then call <see cref="Toast.Show(Visual, string, ToastKind, string?, ToastAction?, TimeSpan?, string?)"/>
/// or this control's <see cref="IToastService"/> methods from anywhere.
/// </para>
/// </summary>
[TemplatePart(ListPart, typeof(ToastStackPanel))]
[TemplatePart(PoliteRegionPart, typeof(MessagesLiveRegion))]
[TemplatePart(AssertiveRegionPart, typeof(MessagesLiveRegion))]
[PseudoClasses(ExpandedPseudoClass)]
public class ToastStack : TemplatedControl, IToastService
{
    public const string ListPart = "PART_List";
    public const string PoliteRegionPart = "PART_PoliteRegion";
    public const string AssertiveRegionPart = "PART_AssertiveRegion";
    public const string ExpandedPseudoClass = ":expanded";

    #region 公共属性定义

    /// <summary>Base lifetime (default 5s). Warnings and errors stay 1.6 times longer; loading toasts wait for an update.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<ToastStack, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(5));

    /// <summary>Oldest toasts beyond this count are dropped (default 12).</summary>
    public static readonly StyledProperty<int> LimitProperty =
        AvaloniaProperty.Register<ToastStack, int>(nameof(Limit), 12);

    /// <summary>Edge position (default bottom right). Windows narrower than 480px always center the stack.</summary>
    public static readonly StyledProperty<ToastStackPosition> PositionProperty =
        AvaloniaProperty.Register<ToastStack, ToastStackPosition>(nameof(Position));

    /// <summary>Pins the stack 16px inside its own bounds (panels and previews) instead of 24px from the window edge.</summary>
    public static readonly StyledProperty<bool> IsContainedProperty =
        AvaloniaProperty.Register<ToastStack, bool>(nameof(IsContained));

    /// <summary>How many toasts show at once (default 3); older ones wait behind and return as the front ones close.</summary>
    public static readonly StyledProperty<int> VisibleToastsProperty =
        AvaloniaProperty.Register<ToastStack, int>(nameof(VisibleToasts), 3);

    /// <summary>Alt+T moves keyboard focus into the stack (default true).</summary>
    public static readonly StyledProperty<bool> IsHotkeyEnabledProperty =
        AvaloniaProperty.Register<ToastStack, bool>(nameof(IsHotkeyEnabled), true);

    /// <summary>Accessible name of the notification region (default "Notifications").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ToastStack, string>(nameof(Label), "Notifications");

    public static readonly DirectProperty<ToastStack, int> CountProperty =
        AvaloniaProperty.RegisterDirect<ToastStack, int>(nameof(Count), o => o.Count);

    public static readonly DirectProperty<ToastStack, bool> IsExpandedProperty =
        AvaloniaProperty.RegisterDirect<ToastStack, bool>(nameof(IsExpanded), o => o.IsExpanded);

    internal static readonly StyledProperty<double> MaxListWidthProperty =
        AvaloniaProperty.Register<ToastStack, double>(nameof(MaxListWidth), 360);

    /// <summary>Base lifetime (default 5s). Warnings and errors stay 1.6 times longer; loading toasts wait for an update.</summary>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>Oldest toasts beyond this count are dropped (default 12).</summary>
    public int Limit
    {
        get => GetValue(LimitProperty);
        set => SetValue(LimitProperty, value);
    }

    /// <summary>Edge position (default bottom right). Windows narrower than 480px always center the stack.</summary>
    public ToastStackPosition Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>Pins the stack 16px inside its own bounds (panels and previews) instead of 24px from the window edge.</summary>
    public bool IsContained
    {
        get => GetValue(IsContainedProperty);
        set => SetValue(IsContainedProperty, value);
    }

    /// <summary>How many toasts show at once (default 3); older ones wait behind and return as the front ones close.</summary>
    public int VisibleToasts
    {
        get => GetValue(VisibleToastsProperty);
        set => SetValue(VisibleToastsProperty, value);
    }

    /// <summary>Alt+T moves keyboard focus into the stack (default true).</summary>
    public bool IsHotkeyEnabled
    {
        get => GetValue(IsHotkeyEnabledProperty);
        set => SetValue(IsHotkeyEnabledProperty, value);
    }

    /// <summary>Accessible name of the notification region (default "Notifications").</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>How many toasts are queued (shown or waiting behind).</summary>
    public int Count
    {
        get => _count;
        private set => SetAndRaise(CountProperty, ref _count, value);
    }

    /// <summary>True while the stack is fanned out (hover, keyboard focus or a tap).</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        private set => SetAndRaise(IsExpandedProperty, ref _isExpanded, value);
    }

    internal double MaxListWidth
    {
        get => GetValue(MaxListWidthProperty);
        set => SetValue(MaxListWidthProperty, value);
    }

    /// <summary>Raised when a toast is shown, updated or dismissed.</summary>
    public event EventHandler? ToastsChanged;

    #endregion

    private readonly List<ToastStackRecord> _records = new();
    private readonly List<ToastStackItem> _leaving = new();
    private MessagesLiveRegion? _polite;
    private MessagesLiveRegion? _assertive;
    private ToastStackPanel? _panel;
    private int _count;
    private bool _isExpanded;
    private long _seq;
    private bool _hovered;
    private bool _focused;
    private bool _tapped;
    private bool _dragging;
    private bool _windowHidden;
    private bool _paused;
    private TopLevel? _topLevel;
    private IInputElement? _returnFocus;
    private IDisposable? _windowStateSubscription;

    static ToastStack()
    {
        AffectsMeasure<ToastStack>(PositionProperty, IsContainedProperty, MaxListWidthProperty);
        FocusableProperty.OverrideDefaultValue<ToastStack>(false);
    }

    public ToastStack()
    {
        AddHandler(GotFocusEvent, OnDescendantGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(LostFocusEvent, OnDescendantLostFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        UpdateAutomationName();
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
    }

    /// <summary>True when this stack was created by <see cref="ToastHost"/> on an overlay layer.</summary>
    internal bool IsAutoCreated { get; init; }

    internal ToastStackPanel? Panel => _panel;

    internal MessagesLiveRegion? PoliteRegion => _polite;

    internal MessagesLiveRegion? AssertiveRegion => _assertive;

    internal bool IsPaused => _paused;

    #region IToastService

    /// <inheritdoc />
    public string Show(ToastOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Id is { } existing && Find(existing) is not null)
        {
            Update(existing, new ToastUpdate
            {
                Kind = options.Kind,
                Title = options.Title,
                Description = options.Description,
                RemoveDescription = options.Description is null,
                Action = options.Action,
                RemoveAction = options.Action is null,
                Duration = options.Duration
            });
            return existing;
        }
        _seq++;
        var record = new ToastStackRecord
        {
            Id = options.Id ?? $"toast-{_seq}",
            Kind = options.Kind,
            Title = options.Title,
            Description = options.Description,
            Action = options.Action,
            Duration = options.Duration ?? DurationFor(options.Kind),
            Seq = _seq
        };
        _records.Insert(0, record);
        var item = new ToastStackItem(this, record);
        _panel?.AddFront(item);
        item.RestartTimer(record.Duration, _paused);
        if (_panel is null)
        {
            _pending.Add(item);
        }
        while (_records.Count > Math.Max(1, Limit))
        {
            Remove(_records[^1]);
        }
        Announce(record);
        Changed();
        return record.Id;
    }

    private readonly List<ToastStackItem> _pending = new();

    /// <inheritdoc />
    public void Update(string id, ToastUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var record = Find(id);
        if (record is null)
        {
            return;
        }
        if (update.Kind is { } kind)
        {
            record.Kind = kind;
        }
        if (update.Title is { } title)
        {
            record.Title = title;
        }
        if (update.RemoveDescription)
        {
            record.Description = null;
        }
        if (update.Description is { } description)
        {
            record.Description = description;
        }
        if (update.RemoveAction)
        {
            record.Action = null;
        }
        if (update.Action is { } action)
        {
            record.Action = action;
        }
        record.Duration = update.Duration ?? DurationFor(record.Kind);
        record.Version++;
        if (ItemOf(record) is { } item)
        {
            item.ApplyRecord();
            item.RestartTimer(record.Duration, _paused);
        }
        Announce(record);
        Changed();
    }

    /// <inheritdoc />
    public void Dismiss(string? id = null)
    {
        if (id is null)
        {
            foreach (var record in _records.ToArray())
            {
                Remove(record);
            }
            Changed();
            return;
        }
        if (Find(id) is { } found)
        {
            Remove(found);
            Changed();
        }
    }

    #endregion

    private TimeSpan? DurationFor(ToastKind kind) => kind switch
    {
        ToastKind.Loading => null,
        ToastKind.Warning or ToastKind.Error => TimeSpan.FromTicks((long)(Duration.Ticks * 1.6)),
        _ => Duration
    };

    private ToastStackRecord? Find(string id)
    {
        foreach (var record in _records)
        {
            if (record.Id == id)
            {
                return record;
            }
        }
        return null;
    }

    private ToastStackItem? ItemOf(ToastStackRecord record)
    {
        if (_panel is not null)
        {
            foreach (var item in _panel.Present)
            {
                if (item.Record == record)
                {
                    return item;
                }
            }
        }
        foreach (var item in _pending)
        {
            if (item.Record == record)
            {
                return item;
            }
        }
        return null;
    }

    private void Changed()
    {
        Count = _records.Count;
        if (_records.Count == 0)
        {
            _tapped = false;
        }
        UpdateExpanded();
        _panel?.InvalidateMeasure();
        ToastsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Announce(ToastStackRecord record)
    {
        var text = string.IsNullOrEmpty(record.Description)
            ? $"{ToastGlyphs.LabelFor(record.Kind)}: {record.Title}"
            : $"{ToastGlyphs.LabelFor(record.Kind)}: {record.Title}. {record.Description}";
        (record.Kind == ToastKind.Error ? _assertive : _polite)?.Announce(text);
    }

    private void Remove(ToastStackRecord record)
    {
        _records.Remove(record);
        var item = ItemOf(record);
        if (item is null)
        {
            return;
        }
        _pending.Remove(item);
        if (_panel is null)
        {
            return;
        }
        HandOffFocus(item);
        _panel.MarkLeaving(item);
        _leaving.Add(item);
        RemoveWhenGone(item, item.Leave());
    }

    private void RemoveWhenGone(ToastStackItem item, Task exit)
    {
        exit.ThenOnUiThread(Dispatcher, () =>
        {
            _leaving.Remove(item);
            _panel?.RemoveItem(item);
        });
    }

    /// <summary>Runs an action; the toast closes afterwards unless the callback updated it.</summary>
    internal void RunAction(ToastStackItem item, bool keyboard)
    {
        var record = item.Record;
        if (record.Action is not { } action || Find(record.Id) is null)
        {
            return;
        }
        var version = record.Version;
        action.Invoke(record.Id);
        if (Find(record.Id) is { } after && after.Version == version)
        {
            Dismiss(record.Id);
        }
        else if (keyboard && Find(record.Id) is { Action: null })
        {
            // An action that updates its toast usually folds itself away; keyboard focus lands on the close button.
            item.CloseButton?.Focus(NavigationMethod.Tab);
        }
    }

    internal void OnItemTimedOut(ToastStackItem item)
    {
        if (item.IsPresent)
        {
            Dismiss(item.Id);
        }
    }

    internal void OnItemDragChanged(bool dragging)
    {
        _dragging = dragging;
        UpdatePaused();
    }

    internal void OnItemTapped()
    {
        _tapped = !_tapped;
        UpdateExpanded();
    }

    #region 模板与生命周期

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_panel is not null)
        {
            _panel.PointerEntered -= OnListPointerEntered;
            _panel.PointerMoved -= OnListPointerMoved;
            _panel.PointerExited -= OnListPointerExited;
            _panel.Owner = null;
        }
        _panel = e.NameScope.Find<ToastStackPanel>(ListPart);
        _polite = e.NameScope.Find<MessagesLiveRegion>(PoliteRegionPart);
        _assertive = e.NameScope.Find<MessagesLiveRegion>(AssertiveRegionPart);
        if (_assertive is not null)
        {
            AutomationProperties.SetLiveSetting(_assertive, AutomationLiveSetting.Assertive);
        }
        if (_panel is not null)
        {
            _panel.Owner = this;
            _panel.PointerEntered += OnListPointerEntered;
            _panel.PointerMoved += OnListPointerMoved;
            _panel.PointerExited += OnListPointerExited;
            // Toasts shown before the template existed join now, oldest first so the newest ends up in front.
            foreach (var item in _pending)
            {
                _panel.AddFront(item);
            }
            _pending.Clear();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            ToastHost.Register(_topLevel, this);
            _topLevel.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
            _topLevel.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            if (_topLevel is Window window)
            {
                _windowStateSubscription = window.GetObservable(Window.WindowStateProperty).Subscribe(new WindowStateObserver(this));
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is not null)
        {
            ToastHost.Unregister(_topLevel, this);
            _topLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
            _topLevel.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        }
        _windowStateSubscription?.Dispose();
        _windowStateSubscription = null;
        _topLevel = null;
    }

    private sealed class WindowStateObserver : IObserver<WindowState>
    {
        private readonly ToastStack _owner;

        public WindowStateObserver(ToastStack owner) => _owner = owner;

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(WindowState value)
        {
            _owner._windowHidden = value == WindowState.Minimized;
            _owner.UpdatePaused();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == IsHotkeyEnabledProperty)
        {
            UpdateAutomationName();
        }
        else if (change.Property == VisibleToastsProperty)
        {
            _panel?.InvalidateMeasure();
        }
        else if (change.Property == LimitProperty)
        {
            while (_records.Count > Math.Max(1, Limit))
            {
                Remove(_records[^1]);
            }
            Changed();
        }
    }

    private void UpdateAutomationName()
    {
        AutomationProperties.SetName(this, IsHotkeyEnabled ? $"{Label} (Alt+T)" : Label);
    }

    #endregion

    #region 展开、暂停与焦点

    private void UpdateExpanded()
    {
        var expanded = _records.Count > 0 && (_hovered || _focused || _tapped);
        if (expanded != IsExpanded)
        {
            IsExpanded = expanded;
            PseudoClasses.Set(ExpandedPseudoClass, expanded);
            _panel?.InvalidateMeasure();
        }
        UpdatePaused();
    }

    private void UpdatePaused()
    {
        var paused = IsExpanded || _dragging || _windowHidden;
        if (paused == _paused)
        {
            return;
        }
        _paused = paused;
        if (_panel is null)
        {
            return;
        }
        // A copy: resuming a toast whose time ran out while paused fires its timer at once, and the dismissal that follows
        // takes it out of Present while this loop is still walking it.
        foreach (var item in _panel.Present.ToArray())
        {
            item.SetPaused(paused);
        }
    }

    private void OnListPointerEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _hovered = true;
            UpdateExpanded();
        }
    }

    private void OnListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && !_hovered)
        {
            _hovered = true;
            UpdateExpanded();
        }
    }

    private void OnListPointerExited(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _hovered = false;
            UpdateExpanded();
        }
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_tapped && e.Source is Visual source && !this.IsVisualAncestorOf(source))
        {
            _tapped = false;
            UpdateExpanded();
        }
    }

    private void OnDescendantGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.OldFocusedElement is not Visual old || !this.IsVisualAncestorOf(old))
        {
            _returnFocus ??= e.OldFocusedElement;
        }
        // Only keyboard focus opens the stack; a mouse click on a button should not pin it open.
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            _focused = true;
            UpdateExpanded();
        }
    }

    private void OnDescendantLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.NewFocusedElement is Visual next && this.IsVisualAncestorOf(next))
        {
            return;
        }
        _focused = false;
        _returnFocus = null;
        UpdateExpanded();
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsHotkeyEnabled || e.Key != Key.T || e.KeyModifiers != KeyModifiers.Alt)
        {
            return;
        }
        if (FocusInto())
        {
            e.Handled = true;
        }
    }

    /// <summary>Moves keyboard focus to the front toast and opens the stack (what Alt+T does).</summary>
    public bool FocusInto()
    {
        var target = FirstButton(_panel?.Present.FirstOrDefault(i => !i.IsInert));
        if (target is null)
        {
            return false;
        }
        var focused = _topLevel?.FocusManager?.GetFocusedElement();
        if (focused is not Visual v || !this.IsVisualAncestorOf(v))
        {
            _returnFocus = focused;
        }
        target.Focus(NavigationMethod.Tab);
        _focused = true;
        UpdateExpanded();
        return true;
    }

    private static AvaloniaButton? FirstButton(ToastStackItem? item)
    {
        if (item is null)
        {
            return null;
        }
        if (item.ActionButton is { IsEffectivelyVisible: true, Focusable: true } action && item.HasAction)
        {
            return action;
        }
        return item.CloseButton;
    }

    /// <summary>Keyboard users land on the next toast; with none left they return to where they were before.</summary>
    private void HandOffFocus(ToastStackItem leaving)
    {
        var focused = _topLevel?.FocusManager?.GetFocusedElement();
        if (focused is not Visual visual || !leaving.IsVisualAncestorOf(visual))
        {
            return;
        }
        var keyboard = visual is StyledElement element && element.Classes.Contains(":focus-visible");
        ToastStackItem? next = null;
        if (_panel is not null)
        {
            var present = _panel.Present;
            var at = -1;
            for (var i = 0; i < present.Count; i++)
            {
                if (present[i] == leaving)
                {
                    at = i;
                }
            }
            for (var i = 0; i < present.Count; i++)
            {
                var candidate = present[i];
                if (candidate == leaving || candidate.IsInert)
                {
                    continue;
                }
                next = candidate;
                if (i > at)
                {
                    break;
                }
            }
        }
        var button = FirstButton(next);
        if (keyboard && button is not null)
        {
            button.Focus(NavigationMethod.Tab);
        }
        else if (keyboard && _returnFocus is Visual back && TopLevel.GetTopLevel(back) is not null)
        {
            _returnFocus.Focus(NavigationMethod.Tab);
        }
        else
        {
            _topLevel?.FocusManager?.Focus(null);
        }
    }

    #endregion
}
