using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Arc announcement bar: a slim banner for the top of a page. It rotates 2–4 short messages (the next one rises
/// from below while the bar springs to the new height), pauses on hover, focus or a minimized window, and with
/// <see cref="IsNavigationVisible"/> adds previous, next and a pause ring that fills over <see cref="Interval"/>.
/// Messages can carry a call to action and a live countdown. Dismissing collapses the height so the page below
/// eases up; with a <see cref="DismissalKey"/> the dismissal is remembered (<see cref="IMessageStorage"/>).
/// </summary>
[TemplatePart(PresencePart, typeof(MessagesPresenceFrame))]
[TemplatePart(ViewportPart, typeof(AnnouncementBarViewport))]
[TemplatePart(PreviousPart, typeof(AvaloniaButton))]
[TemplatePart(NextPart, typeof(AvaloniaButton))]
[TemplatePart(RingPart, typeof(AnnouncementBarRing))]
[TemplatePart(DismissPart, typeof(AvaloniaButton))]
[PseudoClasses(NarrowPseudoClass, NavigablePseudoClass, RotatingPseudoClass, DismissiblePseudoClass)]
public class AnnouncementBar : TemplatedControl
{
    public const string PresencePart = "PART_Presence";
    public const string ViewportPart = "PART_Viewport";
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string RingPart = "PART_Ring";
    public const string DismissPart = "PART_Dismiss";
    public const string NarrowPseudoClass = ":narrow";
    public const string NavigablePseudoClass = ":navigable";
    public const string RotatingPseudoClass = ":rotating";
    public const string DismissiblePseudoClass = ":dismissible";

    /// <summary>Bars this narrow (or narrower) drop the mirror column and read from the start edge.</summary>
    private const double NarrowWidth = 560;

    #region 公共属性定义

    /// <summary>Items shown when <see cref="ItemsSource"/> is not set (XAML children).</summary>
    [Content]
    public AvaloniaList<Announcement> Messages { get; } = new();

    /// <summary>Messages from a binding; overrides <see cref="Messages"/> when set.</summary>
    public static readonly StyledProperty<IList<Announcement>?> ItemsSourceProperty =
        AvaloniaProperty.Register<AnnouncementBar, IList<Announcement>?>(nameof(ItemsSource));

    /// <summary>Remembers the dismissal under this key; change the key to show a new campaign to everyone again.</summary>
    public static readonly StyledProperty<string?> DismissalKeyProperty =
        AvaloniaProperty.Register<AnnouncementBar, string?>(nameof(DismissalKey));

    /// <summary>Whether the bar is shown (two-way). A remembered dismissal keeps it hidden.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AnnouncementBar, bool>(nameof(IsOpen), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Index of the visible message (two-way).</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<AnnouncementBar, int>(nameof(SelectedIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Time each message stays before the next one (default 6s).</summary>
    public static readonly StyledProperty<TimeSpan> IntervalProperty =
        AvaloniaProperty.Register<AnnouncementBar, TimeSpan>(nameof(Interval), TimeSpan.FromSeconds(6));

    /// <summary>Rotate automatically (default true); off under reduced motion.</summary>
    public static readonly StyledProperty<bool> IsAutoPlayEnabledProperty =
        AvaloniaProperty.Register<AnnouncementBar, bool>(nameof(IsAutoPlayEnabled), true);

    /// <summary>Show previous, next and the pause ring when there are several messages (default false).</summary>
    public static readonly StyledProperty<bool> IsNavigationVisibleProperty =
        AvaloniaProperty.Register<AnnouncementBar, bool>(nameof(IsNavigationVisible));

    /// <summary>Show the dismiss button (default true).</summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<AnnouncementBar, bool>(nameof(IsDismissible), true);

    /// <summary>Paused by the pause ring (two-way).</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<AnnouncementBar, bool>(nameof(IsPaused), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<AnnouncementBarTone> ToneProperty =
        AvaloniaProperty.Register<AnnouncementBar, AnnouncementBarTone>(nameof(Tone));

    /// <summary>Accessible name of the region (default "Announcements").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AnnouncementBar, string>(nameof(Label), "Announcements");

    /// <summary>Where the dismissal is remembered; null uses <see cref="MessageStorage.Default"/>.</summary>
    public static readonly StyledProperty<IMessageStorage?> StorageProperty =
        AvaloniaProperty.Register<AnnouncementBar, IMessageStorage?>(nameof(Storage));

    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<AnnouncementBar, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<AnnouncementEventArgs> SelectedIndexChangedEvent =
        RoutedEvent.Register<AnnouncementBar, AnnouncementEventArgs>(nameof(SelectedIndexChanged), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<AnnouncementEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<AnnouncementBar, AnnouncementEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<AnnouncementEventArgs> CountdownEndedEvent =
        RoutedEvent.Register<AnnouncementBar, AnnouncementEventArgs>(nameof(CountdownEnded), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<AnnouncementBar, IBrush?>(nameof(MutedForeground));

    internal static readonly DirectProperty<AnnouncementBar, bool> IsShownProperty =
        AvaloniaProperty.RegisterDirect<AnnouncementBar, bool>(nameof(IsShown), o => o.IsShown);

    internal static readonly DirectProperty<AnnouncementBar, double> SideWidthProperty =
        AvaloniaProperty.RegisterDirect<AnnouncementBar, double>(nameof(SideWidth), o => o.SideWidth);

    internal static readonly DirectProperty<AnnouncementBar, double> RingProgressProperty =
        AvaloniaProperty.RegisterDirect<AnnouncementBar, double>(nameof(RingProgress), o => o.RingProgress);

    /// <summary>Messages from a binding; overrides <see cref="Messages"/> when set.</summary>
    public IList<Announcement>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Remembers the dismissal under this key; change the key to show a new campaign to everyone again.</summary>
    public string? DismissalKey { get => GetValue(DismissalKeyProperty); set => SetValue(DismissalKeyProperty, value); }
    /// <summary>Whether the bar is shown (two-way). A remembered dismissal keeps it hidden.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    /// <summary>Index of the visible message (two-way).</summary>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    /// <summary>Time each message stays before the next one (default 6s).</summary>
    public TimeSpan Interval { get => GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }
    /// <summary>Rotate automatically (default true); off under reduced motion.</summary>
    public bool IsAutoPlayEnabled { get => GetValue(IsAutoPlayEnabledProperty); set => SetValue(IsAutoPlayEnabledProperty, value); }
    /// <summary>Show previous, next and the pause ring when there are several messages (default false).</summary>
    public bool IsNavigationVisible { get => GetValue(IsNavigationVisibleProperty); set => SetValue(IsNavigationVisibleProperty, value); }
    /// <summary>Show the dismiss button (default true).</summary>
    public bool IsDismissible { get => GetValue(IsDismissibleProperty); set => SetValue(IsDismissibleProperty, value); }
    /// <summary>Paused by the pause ring (two-way).</summary>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }
    /// <summary>Neutral (muted surface, default) or inverted (foreground fill).</summary>
    public AnnouncementBarTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    /// <summary>Accessible name of the region (default "Announcements").</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Where the dismissal is remembered; null uses <see cref="MessageStorage.Default"/>.</summary>
    public IMessageStorage? Storage { get => GetValue(StorageProperty); set => SetValue(StorageProperty, value); }

    /// <summary>Raised when the bar was dismissed.</summary>
    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    /// <summary>Raised when another message becomes visible.</summary>
    public event EventHandler<AnnouncementEventArgs>? SelectedIndexChanged
    {
        add => AddHandler(SelectedIndexChangedEvent, value);
        remove => RemoveHandler(SelectedIndexChangedEvent, value);
    }

    /// <summary>Raised when a message's call to action is invoked.</summary>
    public event EventHandler<AnnouncementEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    /// <summary>Raised once when a message's countdown reaches zero.</summary>
    public event EventHandler<AnnouncementEventArgs>? CountdownEnded
    {
        add => AddHandler(CountdownEndedEvent, value);
        remove => RemoveHandler(CountdownEndedEvent, value);
    }

    internal IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }

    internal bool IsShown
    {
        get => _isShown;
        private set => SetAndRaise(IsShownProperty, ref _isShown, value);
    }

    internal double SideWidth
    {
        get => _sideWidth;
        private set => SetAndRaise(SideWidthProperty, ref _sideWidth, value);
    }

    internal double RingProgress
    {
        get => _ringProgress;
        private set => SetAndRaise(RingProgressProperty, ref _ringProgress, value);
    }

    #endregion

    /// <summary>Forgets a remembered dismissal so the bar with this key shows again.</summary>
    public static void ClearDismissal(string key, IMessageStorage? storage = null)
        => (storage ?? MessageStorage.Default).Write(StorageKeyFor(key), null);

    private static string StorageKeyFor(string key) => $"arc-announcement:{key}";

    private readonly MotionValue<double> _ring;
    private MessagesPresenceFrame? _presence;
    private AnnouncementBarViewport? _viewport;
    private AvaloniaButton? _previous;
    private AvaloniaButton? _next;
    private AnnouncementBarRing? _ringButton;
    private AvaloniaButton? _dismiss;
    private PausableTimer? _clock;
    private IDisposable? _windowStateSubscription;
    private bool _isShown = true;
    private double _sideWidth;
    private double _ringProgress;
    private bool _remembered;
    private bool _hovered;
    private bool _focused;
    private bool _windowHidden;
    private bool _running;
    private int _direction = 1;
    private int _shownIndex = -1;

    static AnnouncementBar()
    {
        FocusableProperty.OverrideDefaultValue<AnnouncementBar>(false);
    }

    public AnnouncementBar()
    {
        Messages.CollectionChanged += (_, _) => OnMessagesChanged();
        _ring = new MotionValue<double>(this, 0, MotionVectors.Double);
        _ring.Changed += v => RingProgress = v;
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
        AutomationProperties.SetName(this, Label);
    }

    private IList<Announcement> Items => ItemsSource ?? Messages;

    /// <summary>Number of messages.</summary>
    public int Count => Items.Count;

    /// <summary>The visible message, if any.</summary>
    public Announcement? Current => Count == 0 ? null : Items[Wrap(SelectedIndex)];

    /// <summary>True while messages rotate automatically (several messages, auto play on, full motion).</summary>
    public bool IsRotating => IsAutoPlayEnabled && Count > 1 && ArcMotion.IsFull(this);

    internal bool IsRunning => _running;

    internal AnnouncementBarViewport? Viewport => _viewport;

    private int Wrap(int index) => Count == 0 ? 0 : (index % Count + Count) % Count;

    /// <summary>Shows the next message (rises from below).</summary>
    public void Next() => Go(1);

    /// <summary>Shows the previous message (drops from above).</summary>
    public void Previous() => Go(-1);

    /// <summary>Hides the bar like its dismiss button and remembers it when <see cref="DismissalKey"/> is set.</summary>
    public void Dismiss()
    {
        if (DismissalKey is { Length: > 0 } key)
        {
            (Storage ?? MessageStorage.Default).Write(StorageKeyFor(key), "dismissed");
        }
        SetCurrentValue(IsOpenProperty, false);
        RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
    }

    private void Go(int step)
    {
        if (Count < 2)
        {
            return;
        }
        _direction = step > 0 ? 1 : -1;
        SetCurrentValue(SelectedIndexProperty, Wrap(SelectedIndex + step));
    }

    #region 模板与生命周期

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        if (_dismiss is not null)
        {
            _dismiss.Click -= OnDismissClick;
        }
        if (_ringButton is not null)
        {
            _ringButton.IsCheckedChanged -= OnRingChanged;
        }
        _presence = e.NameScope.Find<MessagesPresenceFrame>(PresencePart);
        _viewport = e.NameScope.Find<AnnouncementBarViewport>(ViewportPart);
        _previous = e.NameScope.Find<AvaloniaButton>(PreviousPart);
        _next = e.NameScope.Find<AvaloniaButton>(NextPart);
        _ringButton = e.NameScope.Find<AnnouncementBarRing>(RingPart);
        _dismiss = e.NameScope.Find<AvaloniaButton>(DismissPart);
        if (_presence is not null)
        {
            // Dismissing collapses on a 0.44s critically damped spring; under reduced motion it only fades.
            _presence.SizeSpring = SpringSpec.FromVisual(0.44, 0);
            _presence.ExitFade = ArcMotionPresets.Exit;
            _presence.ReducedFade = ArcMotionPresets.Fast;
        }
        if (_viewport is not null)
        {
            _viewport.Owner = this;
            _shownIndex = -1;
        }
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
            AutomationProperties.SetName(_previous, "Previous announcement");
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
            AutomationProperties.SetName(_next, "Next announcement");
        }
        if (_dismiss is not null)
        {
            _dismiss.Click += OnDismissClick;
            AutomationProperties.SetName(_dismiss, "Dismiss");
        }
        if (_ringButton is not null)
        {
            _ringButton.IsChecked = IsPaused;
            _ringButton.IsCheckedChanged += OnRingChanged;
        }
        ShowCurrent(animate: false);
        UpdateControls();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ReadDismissal();
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            _windowStateSubscription = window.GetObservable(Window.WindowStateProperty).Subscribe(new WindowStateObserver(this));
        }
        UpdateRunning();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _windowStateSubscription?.Dispose();
        _windowStateSubscription = null;
        _clock?.Pause();
        _ring.Stop();
        _running = false;
    }

    private sealed class WindowStateObserver : IObserver<WindowState>
    {
        private readonly AnnouncementBar _owner;

        public WindowStateObserver(AnnouncementBar owner) => _owner = owner;

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(WindowState value)
        {
            _owner._windowHidden = value == WindowState.Minimized;
            _owner.UpdateRunning();
        }
    }

    private void ReadDismissal()
    {
        _remembered = DismissalKey is { Length: > 0 } key &&
                      (Storage ?? MessageStorage.Default).Read(StorageKeyFor(key)) == "dismissed";
        IsShown = IsOpen && !_remembered;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            IsShown = IsOpen && !_remembered;
            UpdateRunning();
        }
        else if (change.Property == DismissalKeyProperty || change.Property == StorageProperty)
        {
            if (IsAttached)
            {
                ReadDismissal();
                UpdateRunning();
            }
        }
        else if (change.Property == SelectedIndexProperty)
        {
            OnIndexChanged();
        }
        else if (change.Property == ItemsSourceProperty)
        {
            OnMessagesChanged();
        }
        else if (change.Property == IsNavigationVisibleProperty || change.Property == IsDismissibleProperty ||
                 change.Property == IsAutoPlayEnabledProperty)
        {
            UpdateControls();
            UpdateRunning();
        }
        else if (change.Property == IsPausedProperty)
        {
            if (_ringButton is not null)
            {
                _ringButton.IsChecked = IsPaused;
            }
            UpdateControls();
            UpdateRunning();
        }
        else if (change.Property == IntervalProperty)
        {
            _clock = null;
            UpdateRunning(restart: true);
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == BoundsProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, Bounds.Width > 0 && Bounds.Width <= NarrowWidth);
            UpdateControls();
        }
        else if (change.Property == ArcMotion.LevelProperty)
        {
            UpdateControls();
            UpdateRunning();
        }
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;

    private void OnMessagesChanged()
    {
        _shownIndex = -1;
        ShowCurrent(animate: false);
        UpdateControls();
        UpdateRunning(restart: true);
    }

    private void OnIndexChanged()
    {
        ShowCurrent(animate: true);
        _ring.Jump(0);
        UpdateRunning(restart: true);
        if (Current is { } current)
        {
            RaiseEvent(new AnnouncementEventArgs(SelectedIndexChangedEvent, current, Wrap(SelectedIndex)));
        }
    }

    private void ShowCurrent(bool animate)
    {
        if (_viewport is null)
        {
            return;
        }
        var index = Wrap(SelectedIndex);
        var current = Current;
        if (current is not null && index == _shownIndex && ReferenceEquals(_viewport.CurrentFace?.Announcement, current))
        {
            return;
        }
        _shownIndex = index;
        _viewport.Show(current, index + 1, Count, animate ? _direction : 1);
        _direction = 1;
        UpdateLive();
    }

    private void UpdateLive()
    {
        if (_viewport is null)
        {
            return;
        }
        // Rotating messages are not read out; paused or manual changes are.
        AutomationProperties.SetLiveSetting(_viewport, _running ? AutomationLiveSetting.Off : AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_viewport, Current?.Message ?? string.Empty);
    }

    private void UpdateControls()
    {
        var rotating = IsRotating;
        var navigable = IsNavigationVisible && Count > 1;
        PseudoClasses.Set(NavigablePseudoClass, navigable);
        PseudoClasses.Set(RotatingPseudoClass, navigable && rotating);
        PseudoClasses.Set(DismissiblePseudoClass, IsDismissible);
        var controls = (navigable ? 2 + (rotating ? 1 : 0) : 0) + (IsDismissible ? 1 : 0);
        // The side column mirrors the controls, so the message stays centered on the page.
        SideWidth = PseudoClasses.Contains(NarrowPseudoClass) ? 0 : controls * 32 + Math.Max(controls - 1, 0) * 2;
        if (_ringButton is not null)
        {
            AutomationProperties.SetName(_ringButton, IsPaused ? "Resume announcements" : "Pause announcements");
        }
    }

    #endregion

    #region 轮播时钟

    /// <summary>The ring is the clock: it runs to one, then advances. Pausing stops it; resuming finishes the rest.</summary>
    private void UpdateRunning(bool restart = false)
    {
        var running = IsRotating && IsShown && IsAttached && !IsPaused && !_hovered && !_focused && !_windowHidden;
        _clock ??= new PausableTimer(Interval, OnClockElapsed);
        if (restart)
        {
            _clock.Restart(Interval);
            _clock.Pause();
            _ring.Jump(0);
        }
        var wasRunning = _running;
        _running = running;
        if (running)
        {
            if (!wasRunning || restart)
            {
                _clock.Resume();
                var remaining = _clock.Remaining;
                if (remaining > TimeSpan.Zero)
                {
                    _ = _ring.AnimateTo(1, MotionSpec.Tween(remaining, s_linear));
                }
            }
        }
        else
        {
            _clock.Pause();
            _ring.Stop();
        }
        UpdateLive();
    }

    private static readonly Easing s_linear = new LinearEasing();

    private void OnClockElapsed()
    {
        if (_running)
        {
            Go(1);
        }
    }

    #endregion

    #region 交互

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => Go(-1);

    private void OnNextClick(object? sender, RoutedEventArgs e) => Go(1);

    private void OnDismissClick(object? sender, RoutedEventArgs e) => Dismiss();

    private void OnRingChanged(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsPausedProperty, _ringButton?.IsChecked == true);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _hovered = true;
            UpdateRunning();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        UpdateRunning();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focused = true;
        UpdateRunning();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (e.NewFocusedElement is Visual next && this.IsVisualAncestorOf(next))
        {
            return;
        }
        _focused = false;
        UpdateRunning();
    }

    internal void InvokeAction(Announcement announcement)
    {
        if (announcement.Action is not { } action)
        {
            return;
        }
        if (action.Command is { } command && command.CanExecute(action.CommandParameter))
        {
            command.Execute(action.CommandParameter);
        }
        if (action.Href is { } href && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(href);
        }
        RaiseEvent(new AnnouncementEventArgs(ActionInvokedEvent, announcement, Items.IndexOf(announcement)));
    }

    internal void OnCountdownEnded(Announcement announcement)
    {
        RaiseEvent(new AnnouncementEventArgs(CountdownEndedEvent, announcement, Items.IndexOf(announcement)));
    }

    #endregion
}
