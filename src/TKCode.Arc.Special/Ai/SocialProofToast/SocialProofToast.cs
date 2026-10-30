using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A stack of social proof notifications for landing and pricing pages (Arc Social proof toast, design reference): recent
/// trials, upgrades and verified purchases from people and companies arrive every few seconds. A new toast rises in at
/// the front while the others step back and shrink; hovering or keyboard focus pauses the cycle and fans the stack out,
/// and visitors can dismiss any toast.
/// <para>
/// Give it the feed in <see cref="Events"/> (oldest first) and it shows one every <see cref="Interval"/>, or call
/// <see cref="Push"/> to show an event the moment it happens. With no events it plays its <see cref="Simulation"/>, if any,
/// and otherwise shows nothing until you push one. Inline it
/// fills its container's width; a corner <see cref="Position"/> pins the stack to the window instead.
/// </para>
/// </summary>
[TemplatePart(HostPart, typeof(Panel))]
[PseudoClasses(ExpandedPseudoClass)]
public class SocialProofToast : TemplatedControl
{
    internal const string HostPart = "PART_Host";
    internal const string ExpandedPseudoClass = ":expanded";

    /// <summary>Every move (entering, stepping back, fanning out, closing a gap): a bounce-free spring, visual duration 0.24s.</summary>
    internal static readonly SpringSpec MoveSpring = SpringSpec.FromVisual(0.24, 0);

    /// <summary>How long a new toast takes to fade in.</summary>
    internal static readonly TimeSpan EnterFade = ArcMotionPresets.Standard;

    /// <summary>How long a dismissed or pushed-out toast takes to fade (and shrink) away.</summary>
    internal static readonly TimeSpan ExitFade = ArcMotionPresets.Fast;

    /// <summary>How many removed toasts are kept for reuse.</summary>
    private const int PoolSize = 2;

    /// <summary>Time between toasts. The countdown keeps its remaining time across hover and focus pauses.</summary>
    public static readonly StyledProperty<TimeSpan> IntervalProperty =
        AvaloniaProperty.Register<SocialProofToast, TimeSpan>(nameof(Interval), TimeSpan.FromSeconds(4));

    /// <summary>Time before the first toast.</summary>
    public static readonly StyledProperty<TimeSpan> InitialDelayProperty =
        AvaloniaProperty.Register<SocialProofToast, TimeSpan>(nameof(InitialDelay), TimeSpan.FromMilliseconds(800));

    /// <summary>How many toasts stack at once. Older ones fade out from the back.</summary>
    public static readonly StyledProperty<int> MaxVisibleProperty =
        AvaloniaProperty.Register<SocialProofToast, int>(nameof(MaxVisible), 3, coerce: (_, v) => Math.Max(1, v));

    /// <summary>How many toasts already show when the stack appears, without an entrance. Useful for previews and screenshots.</summary>
    public static readonly StyledProperty<int> PrefillProperty =
        AvaloniaProperty.Register<SocialProofToast, int>(nameof(Prefill), coerce: (_, v) => Math.Max(0, v));

    /// <summary>Inline in the layout (default), or pinned to a window corner 24px from the edges (16px and full width on narrow windows).</summary>
    public static readonly StyledProperty<SocialProofToastPosition> PositionProperty =
        AvaloniaProperty.Register<SocialProofToast, SocialProofToastPosition>(nameof(Position));

    /// <summary>Fans the stack out while it is hovered or holds keyboard focus.</summary>
    public static readonly StyledProperty<bool> ExpandOnHoverProperty =
        AvaloniaProperty.Register<SocialProofToast, bool>(nameof(ExpandOnHover), true);

    /// <summary>Starts the feed over when it ends. False stops after the last event.</summary>
    public static readonly StyledProperty<bool> LoopProperty =
        AvaloniaProperty.Register<SocialProofToast, bool>(nameof(Loop), true);

    /// <summary>Stops after this many toasts in a session, so the page stays calm. Null shows toasts for as long as the feed lasts.</summary>
    public static readonly StyledProperty<int?> LimitProperty =
        AvaloniaProperty.Register<SocialProofToast, int?>(nameof(Limit));

    /// <summary>Pauses the cycle from outside, for example while a modal or cookie banner is open.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<SocialProofToast, bool>(nameof(IsPaused));

    /// <summary>Makes each toast a button that raises <see cref="Selected"/>, for example to open pricing or a customer story.</summary>
    public static readonly StyledProperty<bool> IsSelectableProperty =
        AvaloniaProperty.Register<SocialProofToast, bool>(nameof(IsSelectable));

    /// <summary>
    /// A simulated feed to play while <see cref="Events"/> is empty, with times measured back from each pass. Null (the
    /// default) leaves the stack empty until an event is pushed. Give each stack its own simulation.
    /// </summary>
    public static readonly StyledProperty<SocialProofToastSimulation?> SimulationProperty =
        AvaloniaProperty.Register<SocialProofToast, SocialProofToastSimulation?>(nameof(Simulation));

    /// <summary>Every word the stack shows or says.</summary>
    public static readonly StyledProperty<SocialProofToastLabels> LabelsProperty =
        AvaloniaProperty.Register<SocialProofToast, SocialProofToastLabels>(nameof(Labels), SocialProofToastLabels.Default);

    /// <summary>Raised each time a toast appears, for impression tracking. Bubbles.</summary>
    public static readonly RoutedEvent<SocialProofToastEventArgs> ShownEvent =
        RoutedEvent.Register<SocialProofToast, SocialProofToastEventArgs>(nameof(Shown), RoutingStrategies.Bubble);

    /// <summary>Raised when a visitor dismisses a toast. Bubbles.</summary>
    public static readonly RoutedEvent<SocialProofToastEventArgs> DismissedEvent =
        RoutedEvent.Register<SocialProofToast, SocialProofToastEventArgs>(nameof(Dismissed), RoutingStrategies.Bubble);

    /// <summary>Raised when a visitor presses a toast while <see cref="IsSelectable"/> is set. Bubbles.</summary>
    public static readonly RoutedEvent<SocialProofToastEventArgs> SelectedEvent =
        RoutedEvent.Register<SocialProofToast, SocialProofToastEventArgs>(nameof(Selected), RoutingStrategies.Bubble);

    private readonly AvaloniaList<SocialProofEvent> _events = [];
    private readonly SocialProofToastStack _stack = new();
    private readonly TextBlock _status = new()
    {
        Opacity = 0,
        IsHitTestVisible = false,
        Width = 1,
        Height = 1,
    };
    private readonly List<SocialProofToastCard> _live = [];
    private readonly List<SocialProofToastCard> _exiting = [];
    private readonly Stack<SocialProofToastCard> _pool = new();
    private readonly LiveLoop _loop;
    private SocialProofEvent[]? _simulated;
    private SocialProofToastCorner? _corner;
    private Panel? _host;
    private Window? _window;
    private bool _started;
    private bool _usingSimulation = true;
    private double _remaining;
    private int _next;
    private int _shownCount;
    private bool _hovered;
    private bool _keyboardFocus;
    private bool _touch;

    /// <summary>Creates the stack.</summary>
    public SocialProofToast()
    {
        _events.CollectionChanged += OnEventsChanged;
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        _stack.Children.Add(_status);
        _stack.PointerEntered += OnStackPointer;
        _stack.PointerMoved += OnStackPointer;
        _stack.PointerExited += OnStackPointerExited;
        _stack.GotFocus += OnStackGotFocus;
        _stack.PropertyChanged += OnStackPropertyChanged;
        _stack.KeyDown += OnStackKeyDown;
        AutomationProperties.SetLandmarkType(_stack, AutomationLandmarkType.Region);
        AutomationProperties.SetName(_stack, Labels.Region);
        _stack.Slots = MaxVisible;
        // One timer drives the cycle: it runs only while the stack is on screen and nothing pauses it, and it keeps the
        // remaining time while it is paused. The cycle is content, not decoration, so it runs at every motion level.
        _loop = new LiveLoop(_stack, Advance) { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <inheritdoc cref="IntervalProperty"/>
    public TimeSpan Interval { get => GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }

    /// <inheritdoc cref="InitialDelayProperty"/>
    public TimeSpan InitialDelay { get => GetValue(InitialDelayProperty); set => SetValue(InitialDelayProperty, value); }

    /// <inheritdoc cref="MaxVisibleProperty"/>
    public int MaxVisible { get => GetValue(MaxVisibleProperty); set => SetValue(MaxVisibleProperty, value); }

    /// <inheritdoc cref="PrefillProperty"/>
    public int Prefill { get => GetValue(PrefillProperty); set => SetValue(PrefillProperty, value); }

    /// <inheritdoc cref="PositionProperty"/>
    public SocialProofToastPosition Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }

    /// <inheritdoc cref="ExpandOnHoverProperty"/>
    public bool ExpandOnHover { get => GetValue(ExpandOnHoverProperty); set => SetValue(ExpandOnHoverProperty, value); }

    /// <inheritdoc cref="LoopProperty"/>
    public bool Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <inheritdoc cref="LimitProperty"/>
    public int? Limit { get => GetValue(LimitProperty); set => SetValue(LimitProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="IsSelectableProperty"/>
    public bool IsSelectable { get => GetValue(IsSelectableProperty); set => SetValue(IsSelectableProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public SocialProofToastSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public SocialProofToastLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The feed, oldest first. With none, the stack plays its <see cref="Simulation"/>, if any.</summary>
    [Content]
    public AvaloniaList<SocialProofEvent> Events => _events;

    /// <inheritdoc cref="ShownEvent"/>
    public event EventHandler<SocialProofToastEventArgs>? Shown
    {
        add => AddHandler(ShownEvent, value);
        remove => RemoveHandler(ShownEvent, value);
    }

    /// <inheritdoc cref="DismissedEvent"/>
    public event EventHandler<SocialProofToastEventArgs>? Dismissed
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    /// <inheritdoc cref="SelectedEvent"/>
    public event EventHandler<SocialProofToastEventArgs>? Selected
    {
        add => AddHandler(SelectedEvent, value);
        remove => RemoveHandler(SelectedEvent, value);
    }

    /// <summary>The clock relative times are measured against (tests replace it).</summary>
    internal Func<DateTimeOffset> Now { get; set; } = static () => DateTimeOffset.Now;

    /// <summary>The events on screen, front first (not the ones fading away).</summary>
    internal IReadOnlyList<SocialProofEvent> VisibleEvents => _live.Select(c => c.Event!).ToArray();

    /// <summary>The toasts on screen, front first.</summary>
    internal IReadOnlyList<SocialProofToastCard> LiveCards => _live;

    /// <summary>Toasts still fading away.</summary>
    internal int ExitingCount => _exiting.Count;

    internal SocialProofToastStack Stack => _stack;

    internal TextBlock StatusText => _status;

    /// <summary>The seconds until the next toast.</summary>
    internal double Remaining => _remaining;

    /// <summary>Whether the cycle's timer wants to run (nothing pauses it and the feed has more).</summary>
    internal bool IsCycling => _loop.IsActive;

    /// <summary>The stack is fanned out.</summary>
    internal bool IsFanned => ExpandOnHover && (_hovered || _keyboardFocus) && _live.Count > 0;

    /// <summary>How many toasts this session has shown (prefilled and cycled ones; pushed ones do not count).</summary>
    internal int ShownCount => _shownCount;

    private IReadOnlyList<SocialProofEvent> Feed => _events.Count > 0 ? _events : _simulated ??= Simulation?.Pass(Now()) ?? [];

    private bool HasMore
    {
        get
        {
            var feed = Feed;
            if (feed.Count == 0)
            {
                return false;
            }
            if (Limit is { } limit && _shownCount >= limit)
            {
                return false;
            }
            return Loop || _next < feed.Count;
        }
    }

    private bool IsFull => ArcMotion.GetEffectiveLevel(_stack) == ArcMotionLevel.Full;

    /// <summary>Shows <paramref name="socialProofEvent"/> at the front now, and starts the countdown to the next toast over.</summary>
    public void Push(SocialProofEvent socialProofEvent)
    {
        ArgumentNullException.ThrowIfNull(socialProofEvent);
        ShowEvent(socialProofEvent, animate: true);
        _remaining = Interval.TotalSeconds;
    }

    /// <summary>Clears the stack and starts the feed again from its first event (prefill, initial delay and limit included).</summary>
    public void Restart()
    {
        ClearCards();
        _started = false;
        _simulated = null;
        if (this.IsAttachedToVisualTree())
        {
            StartSession();
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host?.Children.Remove(_stack);
        _host = e.NameScope.Find<Panel>(HostPart);
        Mount();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // A minimized window is the desktop's background tab: the cycle waits until it is restored.
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowPropertyChanged;
        }
        Mount();
        if (!_started)
        {
            StartSession();
        }
        else
        {
            UpdateLoop();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window = null;
        }
        // A corner stack lives in the window's overlay layer: it leaves with the control.
        _corner?.Unmount();
        _loop.Stop();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            UpdateLoop();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty)
        {
            // A corner stack is not inside the control, so it follows the control's own visibility (and its timer,
            // which runs only while the stack is visible, follows it too).
            _stack.IsVisible = IsVisible;
        }
        else if (change.Property == PositionProperty)
        {
            Mount();
            Restack(animate: false);
        }
        else if (change.Property == MaxVisibleProperty)
        {
            _stack.Slots = MaxVisible;
            TrimOverflow();
            Restack(animate: true);
        }
        else if (change.Property == IsPausedProperty || change.Property == LoopProperty || change.Property == LimitProperty)
        {
            UpdateLoop();
        }
        else if (change.Property == SimulationProperty)
        {
            // A new simulation (or none) starts over, unless the app's own feed is playing.
            if (_events.Count == 0 && _started)
            {
                Restart();
            }
        }
        else if (change.Property == ExpandOnHoverProperty)
        {
            Restack(animate: true);
        }
        else if (change.Property == IsSelectableProperty)
        {
            foreach (var card in _live)
            {
                card.SetSelectable(IsSelectable);
            }
        }
        else if (change.Property == LabelsProperty)
        {
            AutomationProperties.SetName(_stack, Labels.Region);
            foreach (var card in _live)
            {
                card.SetLabels(Labels);
            }
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    // ---- Placement -------------------------------------------------------------------------------------------------

    /// <summary>Puts the stack inline in the template, or in the window's overlay layer for a corner position.</summary>
    private void Mount()
    {
        var position = Position;
        _stack.IsTopAnchored = position is SocialProofToastPosition.TopLeft or SocialProofToastPosition.TopRight;
        if (position != SocialProofToastPosition.Inline && this.IsAttachedToVisualTree() && ArcOverlayLayer.Find(this) is { } layer)
        {
            _host?.Children.Remove(_stack);
            _corner ??= new SocialProofToastCorner(_stack);
            _corner.Mount(layer, position);
            return;
        }
        _corner?.Unmount();
        _stack.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        _stack.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        _stack.Width = double.NaN;
        _stack.Margin = default;
        if (_host is not null && _stack.Parent is null)
        {
            _host.Children.Add(_stack);
        }
    }

    // ---- The cycle -------------------------------------------------------------------------------------------------

    private void StartSession()
    {
        _started = true;
        _usingSimulation = _events.Count == 0;
        _next = 0;
        _shownCount = 0;
        var feed = Feed;
        var count = Math.Min(Math.Min(Prefill, MaxVisible), feed.Count);
        if (Limit is { } limit)
        {
            count = Math.Min(count, Math.Max(0, limit));
        }
        for (var i = 0; i < count; i++)
        {
            ShowEvent(feed[i], animate: false);
            _shownCount++;
        }
        _next = count;
        _remaining = count > 0 ? Interval.TotalSeconds : InitialDelay.TotalSeconds;
        UpdateLoop();
    }

    /// <summary>One frame of the cycle: counts down and shows the next toast when the time is up.</summary>
    internal bool Advance(double seconds)
    {
        if (!HasMore)
        {
            UpdateLoop();
            return false;
        }
        _remaining -= seconds;
        if (_remaining > 0)
        {
            return true;
        }
        ShowNext();
        _remaining = Math.Max(0.05, Interval.TotalSeconds);
        if (!HasMore)
        {
            // The feed or the session's limit has run out: the timer stops until Restart.
            UpdateLoop();
            return false;
        }
        return true;
    }

    private void ShowNext()
    {
        var feed = Feed;
        if (_next >= feed.Count)
        {
            _next = 0;
            if (_events.Count == 0)
            {
                // The simulation's times are measured again for each pass, so it never drifts into "40 min ago".
                _simulated = Simulation?.Pass(Now()) ?? [];
                feed = _simulated;
                if (feed.Count == 0)
                {
                    return;
                }
            }
        }
        var e = feed[_next++];
        _shownCount++;
        ShowEvent(e, animate: true);
    }

    private void UpdateLoop()
    {
        var wants = _started && this.IsAttachedToVisualTree() && !IsPaused && !_hovered && !_keyboardFocus && HasMore
                    && _window is not { WindowState: WindowState.Minimized };
        if (wants)
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Moving from the simulation to the app's feed (or back) starts over; otherwise the cycle carries on where it is.
        if (_usingSimulation || _events.Count == 0)
        {
            Restart();
            return;
        }
        if (_next > _events.Count)
        {
            _next = _events.Count;
        }
        UpdateLoop();
    }

    // ---- Toasts ----------------------------------------------------------------------------------------------------

    private void ShowEvent(SocialProofEvent e, bool animate)
    {
        if (e.Id.Length > 0 && _live.FirstOrDefault(c => c.Event?.Id == e.Id) is { } duplicate)
        {
            _live.Remove(duplicate);
            _ = Exit(duplicate, steppingBack: false);
        }
        var now = Now();
        var card = _pool.Count > 0 ? _pool.Pop() : CreateCard();
        card.Show(e, Labels, now, IsSelectable);
        card.SetTouch(_touch);
        card.SetCovered(false);
        _stack.Children.Insert(0, card);
        _live.Insert(0, card);
        if (animate)
        {
            // It rises from the anchored edge (16px) and fades in; under reduced motion it only fades.
            var from = IsFull ? (_stack.IsTopAnchored ? -_stack.EnterOffset : _stack.EnterOffset) : 0;
            card.Jump(from, 1, 0);
            _ = card.FadeTo(1, EnterFade, ArcEasings.Enter);
        }
        else
        {
            card.Jump(0, 1, 1);
        }
        foreach (var other in _live)
        {
            if (!ReferenceEquals(other, card))
            {
                other.UpdateTime(now);
            }
        }
        TrimOverflow();
        Restack(animate);
        RaiseEvent(new SocialProofToastEventArgs(ShownEvent, e));
    }

    private SocialProofToastCard CreateCard()
    {
        var card = new SocialProofToastCard();
        card.DismissRequested += (_, _) => Dismiss(card, moveFocus: _keyboardFocus);
        card.Click += OnCardClick;
        return card;
    }

    private void OnCardClick(object? sender, RoutedEventArgs e)
    {
        if (IsSelectable && sender is SocialProofToastCard { IsExiting: false, Event: { } evt })
        {
            RaiseEvent(new SocialProofToastEventArgs(SelectedEvent, evt));
        }
    }

    /// <summary>Toasts past <see cref="MaxVisible"/> fade out from the back.</summary>
    private void TrimOverflow()
    {
        while (_live.Count > MaxVisible)
        {
            var old = _live[^1];
            _live.RemoveAt(_live.Count - 1);
            _ = Exit(old, steppingBack: true);
        }
    }

    /// <summary>Dismisses a toast: it fades and shrinks away, the rest close the gap and focus moves to the next toast.</summary>
    internal void Dismiss(SocialProofToastCard card, bool moveFocus)
    {
        var index = _live.IndexOf(card);
        if (index < 0 || card.Event is not { } e)
        {
            return;
        }
        _live.RemoveAt(index);
        _ = Exit(card, steppingBack: false);
        _status.Text = Labels.DismissedStatus(e.Sentence);
        if (moveFocus && _live.Count > 0)
        {
            var next = _live[Math.Min(index, _live.Count - 1)];
            if (next.Focusable)
            {
                next.Focus(NavigationMethod.Tab);
            }
            else
            {
                next.DismissButton?.Focus(NavigationMethod.Tab);
            }
        }
        Restack(animate: true);
        RaiseEvent(new SocialProofToastEventArgs(DismissedEvent, e));
    }

    /// <summary>Fades a toast away, then takes it out (and keeps it for reuse).</summary>
    private async Task Exit(SocialProofToastCard card, bool steppingBack)
    {
        card.MarkExiting();
        _exiting.Add(card);
        card.SetCovered(!IsFanned);
        if (steppingBack)
        {
            // Pushed past the limit: it keeps stepping back as it fades.
            var depth = MaxVisible;
            card.Place(Offset(depth, IsFanned), Scale(depth, IsFanned), MoveSpring);
        }
        else if (IsFull)
        {
            _ = card.ShrinkTo(card.TargetScale * _stack.ExitScale, ExitFade, ArcEasings.Standard);
        }
        await card.FadeTo(0, ExitFade, ArcEasings.Standard);
        if (!_exiting.Remove(card))
        {
            return;
        }
        _stack.Children.Remove(card);
        if (_pool.Count < PoolSize)
        {
            _pool.Push(card);
        }
    }

    private void ClearCards()
    {
        foreach (var card in _live.Concat(_exiting))
        {
            _stack.Children.Remove(card);
        }
        _live.Clear();
        _exiting.Clear();
        _pool.Clear();
    }

    private double Offset(int depth, bool fanned)
    {
        var step = fanned ? _stack.CardHeight + _stack.FanGap : _stack.StackStep;
        return (_stack.IsTopAnchored ? 1 : -1) * step * depth;
    }

    private double Scale(int depth, bool fanned) => fanned ? 1 : Math.Max(0.5, 1 - _stack.StackScaleStep * depth);

    /// <summary>
    /// Springs every toast to its place: the front one at the edge, the others stepped back 10px and 5% smaller each with
    /// their content faded out, or fanned out one toast height (plus the gap) apart while hovered or focused.
    /// </summary>
    private void Restack(bool animate)
    {
        var fanned = IsFanned;
        PseudoClasses.Set(ExpandedPseudoClass, fanned);
        // While fanned the gaps between toasts still count as the stack, so the pointer can cross them without closing it.
        _stack.Background = fanned ? Brushes.Transparent : null;
        for (var i = 0; i < _live.Count; i++)
        {
            var card = _live[i];
            card.ZIndex = 1000 - i;
            card.SetCovered(!fanned && i > 0);
            var y = Offset(i, fanned);
            var scale = Scale(i, fanned);
            if (animate)
            {
                card.Place(y, scale, MoveSpring);
            }
            else
            {
                card.Jump(y, scale, card.TargetOpacity);
            }
        }
    }

    // ---- Hover, focus and keys -------------------------------------------------------------------------------------

    private void OnStackPointer(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            // Touch has no hover: the dismiss buttons stay visible instead.
            if (!_touch)
            {
                _touch = true;
                foreach (var card in _live)
                {
                    card.SetTouch(true);
                }
            }
            return;
        }
        if (!_hovered)
        {
            _hovered = true;
            OnPauseChanged();
        }
    }

    private void OnStackPointerExited(object? sender, PointerEventArgs e)
    {
        if (_hovered)
        {
            _hovered = false;
            OnPauseChanged();
        }
    }

    private void OnStackGotFocus(object? sender, FocusChangedEventArgs e)
    {
        // Keyboard focus pauses and fans out; a click that focuses a button does not keep the stack open after the
        // pointer leaves.
        var keyboard = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        if (keyboard != _keyboardFocus)
        {
            _keyboardFocus = keyboard;
            OnPauseChanged();
        }
    }

    private void OnStackPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsKeyboardFocusWithinProperty && !_stack.IsKeyboardFocusWithin && _keyboardFocus)
        {
            _keyboardFocus = false;
            OnPauseChanged();
        }
    }

    private void OnPauseChanged()
    {
        Restack(animate: true);
        UpdateLoop();
    }

    private void OnStackKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _live.Count == 0)
        {
            return;
        }
        // Escape dismisses the focused toast, or the front one; focus moves to the next toast.
        var card = (e.Source as Visual)?.FindAncestorOfType<SocialProofToastCard>(includeSelf: true);
        if (card is null || card.IsExiting || !_live.Contains(card))
        {
            card = _live[0];
        }
        Dismiss(card, moveFocus: true);
        e.Handled = true;
    }
}
