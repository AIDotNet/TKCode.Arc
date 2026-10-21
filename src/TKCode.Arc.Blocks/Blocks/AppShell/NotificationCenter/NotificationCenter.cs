using System.Collections;
using System.Collections.Specialized;
using TKCode.Arc.Primitives;
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
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A home for updates: a bell with an unread badge that opens a panel of notifications with read state, an All /
/// Unread switch, expandable details and bulk actions ("Mark all read", "Clear read"). Use it when updates need a
/// persistent place instead of a brief toast, and connect the read state to your data store through
/// <see cref="NotificationItem.IsRead"/> and the <see cref="ReadChanged"/> and <see cref="Dismissed"/> events.
/// <code>
/// &lt;arc:NotificationCenter ReadChanged="OnReadChanged"&gt;
///     &lt;arc:NotificationItem Id="deploy" Title="Deploy finished" Description="Production is on v2.4.1." Time="2m" Tone="Success" /&gt;
///     &lt;arc:NotificationItem Id="mia" Title="Mia mentioned you" ActorName="Mia Chen" Time="1h" IsRead="True" /&gt;
/// &lt;/arc:NotificationCenter&gt;
/// </code>
/// The panel grows out of the bell (6px toward it at scale 0.97, standard 240ms) and leaves in 120ms; counts roll,
/// the summary rises into its new wording, the view highlight glides on the morph spring, rows collapse their height
/// on the smooth spring as they leave (cascading 35ms apart after a bulk action) and settle in with a 4px rise when the
/// panel opens. Escape closes the panel and returns focus to the bell.
/// Ported from Arc's free notification-center block (MIT, Copyright (c) 2026 Elia Kuratli).
/// </summary>
[TemplatePart(TriggerPart, typeof(AvaloniaButton))]
[TemplatePart(BadgePart, typeof(PresenceHost))]
[TemplatePart(BadgeCountPart, typeof(ArcContentSwap))]
[PseudoClasses(OpenPseudoClass, HasUnreadPseudoClass)]
public class NotificationCenter : TemplatedControl
{
    public const string TriggerPart = "PART_Trigger";
    public const string BadgePart = "PART_Badge";
    public const string BadgeCountPart = "PART_BadgeCount";

    public const string OpenPseudoClass = ":open";
    public const string HasUnreadPseudoClass = ":has-unread";

    #region Properties

    /// <summary>The notifications to show; when set it replaces <see cref="Items"/>. Collection changes are followed.</summary>
    public static readonly StyledProperty<IEnumerable<NotificationItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<NotificationCenter, IEnumerable<NotificationItem>?>(nameof(ItemsSource));

    /// <summary>The panel heading and the bell's accessible name. Default "Notifications".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<NotificationCenter, string>(nameof(Label), "Notifications");

    /// <summary>Every other word the control shows or announces, with English defaults.</summary>
    public static readonly StyledProperty<NotificationCenterLabels> LabelsProperty =
        AvaloniaProperty.Register<NotificationCenter, NotificationCenterLabels>(nameof(Labels), new NotificationCenterLabels());

    /// <summary>Whether the panel is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<NotificationCenter, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Which notifications the panel lists: All (default) or Unread (two-way).</summary>
    public static readonly StyledProperty<NotificationCenterView> ViewProperty =
        AvaloniaProperty.Register<NotificationCenter, NotificationCenterView>(nameof(View), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How many notifications are unread.</summary>
    public static readonly DirectProperty<NotificationCenter, int> UnreadCountProperty =
        AvaloniaProperty.RegisterDirect<NotificationCenter, int>(nameof(UnreadCount), o => o.UnreadCount);

    /// <summary>How many notifications are read.</summary>
    public static readonly DirectProperty<NotificationCenter, int> ReadCountProperty =
        AvaloniaProperty.RegisterDirect<NotificationCenter, int>(nameof(ReadCount), o => o.ReadCount);

    /// <summary>Gap between the bell and the panel (sideOffset 12).</summary>
    internal static readonly StyledProperty<double> PanelGapProperty =
        AvaloniaProperty.Register<NotificationCenter, double>(nameof(PanelGap), 12);

    /// <summary>Raised when a notification is marked read or unread; "Mark all read" raises it once per notification.</summary>
    public static readonly RoutedEvent<NotificationReadChangedEventArgs> ReadChangedEvent =
        RoutedEvent.Register<NotificationCenter, NotificationReadChangedEventArgs>(nameof(ReadChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a notification is dismissed; "Clear read" raises it once per notification.</summary>
    public static readonly RoutedEvent<NotificationDismissedEventArgs> DismissedEvent =
        RoutedEvent.Register<NotificationCenter, NotificationDismissedEventArgs>(nameof(Dismissed), RoutingStrategies.Bubble);

    /// <summary>The notifications to show; when set it replaces <see cref="Items"/>. Collection changes are followed.</summary>
    public IEnumerable<NotificationItem>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>The panel heading and the bell's accessible name. Default "Notifications".</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Every other word the control shows or announces, with English defaults.</summary>
    public NotificationCenterLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>Whether the panel is open (two-way).</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Which notifications the panel lists: All (default) or Unread (two-way).</summary>
    public NotificationCenterView View { get => GetValue(ViewProperty); set => SetValue(ViewProperty, value); }

    /// <summary>How many notifications are unread.</summary>
    public int UnreadCount
    {
        get => _unreadCount;
        private set => SetAndRaise(UnreadCountProperty, ref _unreadCount, value);
    }

    /// <summary>How many notifications are read.</summary>
    public int ReadCount
    {
        get => _readCount;
        private set => SetAndRaise(ReadCountProperty, ref _readCount, value);
    }

    internal double PanelGap { get => GetValue(PanelGapProperty); set => SetValue(PanelGapProperty, value); }

    /// <summary>Notifications declared inline (used when <see cref="ItemsSource"/> is null). Dismissing removes them.</summary>
    [Content]
    public AvaloniaList<NotificationItem> Items { get; } = new();

    /// <summary>Raised when a notification is marked read or unread; "Mark all read" raises it once per notification.</summary>
    public event EventHandler<NotificationReadChangedEventArgs>? ReadChanged
    {
        add => AddHandler(ReadChangedEvent, value);
        remove => RemoveHandler(ReadChangedEvent, value);
    }

    /// <summary>Raised when a notification is dismissed; "Clear read" raises it once per notification.</summary>
    public event EventHandler<NotificationDismissedEventArgs>? Dismissed
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    #endregion

    private enum CloseReason
    {
        Programmatic,
        Escape,
        CloseButton,
        OutsidePress
    }

    private readonly List<NotificationItem> _all = new();
    private readonly List<NotificationItem> _visible = new();
    private HashSet<NotificationItem> _subscribed = new();
    private HashSet<NotificationItem> _current = new();
    private readonly HashSet<NotificationItem> _hidden = new();
    private INotifyCollectionChanged? _observedSource;
    private NotificationCenterButton? _trigger;
    private PresenceHost? _badge;
    private ArcContentSwap? _badgeCount;
    private NotificationCenterPresence? _presence;
    private NotificationCenterPanel? _panel;
    private IDisposable? _clientSizeSubscription;
    private int _unreadCount;
    private int _readCount;
    private int _badgeValue = -1;
    private object? _expandedKey;
    private bool _bulk;
    private bool _pendingOpen;
    private bool _openedByKeyboard;
    private int _batch;
    private CloseReason _closeReason;
    private NavigationMethod _closeFocusMethod = NavigationMethod.Unspecified;

    static NotificationCenter()
    {
        FocusableProperty.OverrideDefaultValue<NotificationCenter>(false);
    }

    public NotificationCenter()
    {
        Items.CollectionChanged += OnCollectionChanged;
    }

    /// <summary>The panel (created the first time it opens).</summary>
    internal NotificationCenterPanel? Panel => _panel;

    /// <summary>The popup hosting the panel (created the first time it opens).</summary>
    internal NotificationCenterPopup? Popup => _presence?.Popup;

    internal NotificationCenterButton? Trigger => _trigger;

    /// <summary>The notifications the current view lists, in order.</summary>
    internal IReadOnlyList<NotificationItem> VisibleItems => _visible;

    internal int ItemCount => _all.Count;

    internal object? ExpandedKey => _expandedKey;

    internal bool IsBulk => _bulk;

    #region Public actions

    /// <summary>Opens the panel.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the panel; focus returns to the bell when it was inside the panel.</summary>
    public void Close() => Close(CloseReason.Programmatic, NavigationMethod.Unspecified);

    /// <summary>Marks every unread notification read and raises <see cref="ReadChanged"/> for each of them.</summary>
    public void MarkAllRead() => MarkAllRead(fromPanel: false, keyboard: false);

    /// <summary>Dismisses every read notification and raises <see cref="Dismissed"/> for each of them.</summary>
    public void ClearRead() => ClearRead(fromPanel: false, keyboard: false);

    #endregion

    #region Data

    private IEnumerable<NotificationItem> Source => ItemsSource ?? Items;

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, Items) && ItemsSource is not null)
        {
            return;
        }
        Refresh();
    }

    private void ObserveSource()
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnCollectionChanged;
            _observedSource = null;
        }
        if (ItemsSource is INotifyCollectionChanged observable)
        {
            _observedSource = observable;
            observable.CollectionChanged += OnCollectionChanged;
        }
        _hidden.Clear();
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == NotificationItem.TagProperty)
        {
            return;
        }
        Refresh();
    }

    /// <summary>Recomputes the lists and counts and shows them in the bell and the panel.</summary>
    private void Refresh()
    {
        if (_batch > 0)
        {
            return;
        }
        _all.Clear();
        foreach (var item in Source)
        {
            if (item is not null && !_hidden.Contains(item))
            {
                _all.Add(item);
            }
        }
        // Follow every item's properties (read state from the app, edited titles).
        _current.Clear();
        _current.UnionWith(_all);
        foreach (var item in _subscribed)
        {
            if (!_current.Contains(item))
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }
        foreach (var item in _all)
        {
            if (!_subscribed.Contains(item))
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }
        (_subscribed, _current) = (_current, _subscribed);
        _visible.Clear();
        var unread = 0;
        foreach (var item in _all)
        {
            if (!item.IsRead)
            {
                unread++;
            }
            if (View == NotificationCenterView.All || !item.IsRead)
            {
                _visible.Add(item);
            }
        }
        UnreadCount = unread;
        ReadCount = _all.Count - unread;
        UpdateTrigger();
        _panel?.Sync(opening: false);
    }

    private void Batch(Action action)
    {
        _batch++;
        try
        {
            action();
        }
        finally
        {
            _batch--;
        }
        Refresh();
    }

    private void Remove(NotificationItem item)
    {
        if (ItemsSource is null)
        {
            Items.Remove(item);
        }
        else if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } list)
        {
            list.Remove(item);
        }
        else if (ItemsSource is ICollection<NotificationItem> { IsReadOnly: false } collection)
        {
            collection.Remove(item);
        }
        else
        {
            // A read-only source keeps the item; the control hides it.
            _hidden.Add(item);
        }
    }

    private static object KeyOf(NotificationItem item) => string.IsNullOrEmpty(item.Id) ? item : item.Id;

    #endregion

    #region Actions from the panel

    internal void ToggleExpanded(NotificationItem item)
    {
        var key = KeyOf(item);
        _expandedKey = Equals(_expandedKey, key) ? null : key;
        _panel?.Sync(opening: false);
    }

    internal void SelectView(NotificationCenterView view)
    {
        _bulk = false;
        _expandedKey = null;
        if (View == view)
        {
            _panel?.Sync(opening: false);
            return;
        }
        SetCurrentValue(ViewProperty, view);
    }

    /// <summary>Marks one notification read or unread. In the Unread view, marking read moves focus to a neighbor.</summary>
    internal void ToggleRead(NotificationItem item, bool keyboard)
    {
        var next = !item.IsRead;
        if (next && View == NotificationCenterView.Unread)
        {
            MoveFocusFrom(item, keyboard);
            _expandedKey = null;
        }
        _bulk = false;
        Batch(() => item.SetCurrentValue(NotificationItem.IsReadProperty, next));
        RaiseEvent(new NotificationReadChangedEventArgs(ReadChangedEvent, item, next));
    }

    /// <summary>Dismisses one notification; focus moves to the next row, the previous one, or the Unread button.</summary>
    internal void DismissItem(NotificationItem item, bool keyboard)
    {
        MoveFocusFrom(item, keyboard);
        _bulk = false;
        if (Equals(_expandedKey, KeyOf(item)))
        {
            _expandedKey = null;
        }
        Batch(() => Remove(item));
        RaiseEvent(new NotificationDismissedEventArgs(DismissedEvent, item));
    }

    /// <summary>Marks every unread notification read; from the panel, focus then returns to the current view button.</summary>
    internal void MarkAllRead(bool fromPanel, bool keyboard)
    {
        var unread = _all.Where(i => !i.IsRead).ToList();
        _bulk = true;
        _expandedKey = null;
        Batch(() =>
        {
            foreach (var item in unread)
            {
                item.SetCurrentValue(NotificationItem.IsReadProperty, true);
            }
        });
        foreach (var item in unread)
        {
            RaiseEvent(new NotificationReadChangedEventArgs(ReadChangedEvent, item, true));
        }
        if (fromPanel)
        {
            var view = View;
            AfterFrame(() => FocusInPanel(panel => panel.FocusView(view, Method(keyboard))));
        }
    }

    /// <summary>Dismisses every read notification; from the panel, focus then moves to the All button.</summary>
    internal void ClearRead(bool fromPanel, bool keyboard)
    {
        var read = _all.Where(i => i.IsRead).ToList();
        _bulk = true;
        if (_expandedKey is not null && read.Any(i => Equals(KeyOf(i), _expandedKey)))
        {
            _expandedKey = null;
        }
        Batch(() =>
        {
            foreach (var item in read)
            {
                Remove(item);
            }
        });
        foreach (var item in read)
        {
            RaiseEvent(new NotificationDismissedEventArgs(DismissedEvent, item));
        }
        if (fromPanel)
        {
            AfterFrame(() => FocusInPanel(panel => panel.FocusView(NotificationCenterView.All, Method(keyboard))));
        }
    }

    internal void CloseFromPanel(bool keyboard) =>
        Close(CloseReason.CloseButton, keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);

    /// <summary>Focus goes to the next shown row, else the previous one, else the Unread button (Arc).</summary>
    private void MoveFocusFrom(NotificationItem item, bool keyboard)
    {
        var index = _visible.IndexOf(item);
        var next = index >= 0 && index + 1 < _visible.Count ? _visible[index + 1] : index > 0 ? _visible[index - 1] : null;
        var method = Method(keyboard);
        AfterFrame(() => FocusInPanel(panel =>
        {
            if (next is null || !panel.FocusItem(next, method))
            {
                panel.FocusView(NotificationCenterView.Unread, method);
            }
        }));
    }

    private void FocusInPanel(Action<NotificationCenterPanel> focus)
    {
        if (IsOpen && _panel is not null)
        {
            focus(_panel);
        }
    }

    private static NavigationMethod Method(bool keyboard) => keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;

    private static void AfterFrame(Action action) => Dispatcher.UIThread.Post(action, DispatcherPriority.Loaded);

    #endregion

    #region Template and trigger

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
        }
        base.OnApplyTemplate(e);
        _trigger = e.NameScope.Find<NotificationCenterButton>(TriggerPart);
        _badge = e.NameScope.Find<PresenceHost>(BadgePart);
        _badgeCount = e.NameScope.Find<ArcContentSwap>(BadgeCountPart);
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.IsOn = IsOpen;
            if (_presence is not null)
            {
                _presence.Popup.PlacementTarget = _trigger;
            }
        }
        _badgeValue = -1;
        Refresh();
        if (_pendingOpen && IsOpen)
        {
            Dispatcher.UIThread.Post(OpenPending, DispatcherPriority.Loaded);
        }
    }

    private void OpenPending()
    {
        if (_pendingOpen && IsOpen)
        {
            OpenCore();
        }
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsOpen)
        {
            Close(CloseReason.Programmatic, NavigationMethod.Pointer);
            return;
        }
        _openedByKeyboard = _trigger?.IsKeyboardActivation == true;
        SetCurrentValue(IsOpenProperty, true);
    }

    /// <summary>The badge pops in and out and its number rolls; it shows "9+" past nine.</summary>
    private void UpdateTrigger()
    {
        var unread = _unreadCount;
        PseudoClasses.Set(HasUnreadPseudoClass, unread > 0);
        if (_badge is not null)
        {
            _badge.IsPresent = unread > 0;
        }
        if (_badgeCount is not null && unread > 0 && unread != _badgeValue)
        {
            _badgeCount.Direction = unread > _badgeValue ? 1 : -1;
            var text = unread > 9 ? "9+" : unread.ToString(System.Globalization.CultureInfo.CurrentCulture);
            if (_badgeValue < 0)
            {
                var mode = _badgeCount.Mode;
                _badgeCount.Mode = ContentSwapMode.None;
                _badgeCount.Content = text;
                _badgeCount.Mode = mode;
            }
            else if (!Equals(_badgeCount.Content, text))
            {
                _badgeCount.Content = text;
            }
            _badgeValue = unread;
        }
        if (_trigger is not null)
        {
            AutomationProperties.SetName(_trigger, Labels.Trigger(Label, unread));
        }
    }

    #endregion

    #region Open and close

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveSource();
            Refresh();
        }
        else if (change.Property == ViewProperty)
        {
            _bulk = false;
            _expandedKey = null;
            Refresh();
        }
        else if (change.Property == LabelProperty || change.Property == LabelsProperty)
        {
            UpdateTrigger();
            _panel?.Sync(opening: false);
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

    private void Close(CloseReason reason, NavigationMethod method)
    {
        if (!IsOpen)
        {
            return;
        }
        _closeReason = reason;
        _closeFocusMethod = method;
        SetCurrentValue(IsOpenProperty, false);
    }

    private void EnsurePresence()
    {
        if (_presence is not null)
        {
            return;
        }
        _panel = new NotificationCenterPanel(this);
        _presence = new NotificationCenterPresence(_panel);
        LogicalChildren.Add(_presence.Popup);
        _presence.OutsidePressed += (_, _) => Close(CloseReason.OutsidePress, NavigationMethod.Pointer);
        _presence.EscapePressed += (_, e) =>
        {
            e.Handled = true;
            Close(CloseReason.Escape, NavigationMethod.Tab);
        };
    }

    private void OpenCore()
    {
        if (_trigger is null || TopLevel.GetTopLevel(_trigger) is not { } topLevel)
        {
            _pendingOpen = true;
            return;
        }
        _pendingOpen = false;
        EnsurePresence();
        PseudoClasses.Set(OpenPseudoClass, true);
        _trigger.IsOn = true;
        _clientSizeSubscription?.Dispose();
        _clientSizeSubscription = topLevel.GetObservable(TopLevel.ClientSizeProperty).Subscribe(new ClientSizeObserver(this));
        FitPanel(topLevel.ClientSize);
        _presence!.Show(_trigger, PanelGap);
        _panel!.Sync(opening: true);
        // Radix moves focus to the first focusable element of the content: the close button.
        var method = _openedByKeyboard ? NavigationMethod.Tab : NavigationMethod.Unspecified;
        _openedByKeyboard = false;
        AfterFrame(() =>
        {
            if (IsOpen)
            {
                _panel.FocusFirst(method);
            }
        });
    }

    private void FitPanel(Size client)
    {
        if (_panel is null)
        {
            return;
        }
        Rect? bell = _trigger is not null && TopLevel.GetTopLevel(_trigger) is { } topLevel &&
                     _trigger.TranslatePoint(default, topLevel) is { } origin
            ? new Rect(origin, _trigger.Bounds.Size)
            : null;
        _panel.FitTo(client, bell, PanelGap);
    }

    private void CloseCore()
    {
        _pendingOpen = false;
        var reason = _closeReason;
        var method = _closeFocusMethod;
        _closeReason = CloseReason.Programmatic;
        _closeFocusMethod = NavigationMethod.Unspecified;
        PseudoClasses.Set(OpenPseudoClass, false);
        _clientSizeSubscription?.Dispose();
        _clientSizeSubscription = null;
        if (_trigger is not null)
        {
            _trigger.IsOn = false;
        }
        var focusInside = _panel?.IsFocusWithin == true;
        _presence?.Hide();
        if (_trigger is not null && (focusInside || reason is CloseReason.Escape or CloseReason.CloseButton))
        {
            _trigger.Focus(method);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_pendingOpen && IsOpen)
        {
            Dispatcher.UIThread.Post(OpenPending, DispatcherPriority.Loaded);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The bell left the window: the panel goes without its exit and opens again when the bell comes back.
        _clientSizeSubscription?.Dispose();
        _clientSizeSubscription = null;
        _presence?.Hide(immediate: true);
        if (IsOpen)
        {
            _pendingOpen = true;
        }
    }

    private sealed class ClientSizeObserver(NotificationCenter owner) : IObserver<Size>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Size value) => owner.FitPanel(value);
    }

    #endregion
}
