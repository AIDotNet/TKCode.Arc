using System.Runtime.CompilerServices;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The scrolling surface of a chat thread that follows new content at the bottom. While it follows, content that grows
/// (a streamed reply, a new turn, an opened panel) or a viewport that shrinks moves the offset to the bottom by chasing it:
/// every frame covers 22% of the remaining distance (at 60 Hz; the share is scaled by the frame time), snapping once less
/// than a pixel is left; Reduced and Off motion jump. Only the offset moves: the thread itself is not laid out again.
/// <para>
/// A scroll by the person (wheel, touch, dragging the scrollbar, keys such as PageUp, ↑ or Home) that moves the offset up
/// releases the follow, however short; offsets the scroller sets itself (and offsets code sets without any input) never
/// do. Coming back within <see cref="ReattachDistance"/> (6px) of the bottom, or <see cref="ScrollToLatest"/>, follows
/// again. While it does not follow and there is content below, a round "scroll to latest" button shows centered 10px above
/// the bottom of the viewport (in 160ms, out 100ms); pressing it chases back down.
/// </para>
/// <para>
/// <see cref="SaveOffset"/> and <see cref="RestoreOffset"/> keep an offset per conversation (any key object, held
/// weakly): a restored conversation that was following lands at its bottom, others at their offset; a key without a saved
/// offset lands at the bottom. Both without motion, once the new content is laid out.
/// </para>
/// <para>
/// The chase is code-driven (Core <see cref="LiveLoop"/>): its target, the bottom, moves every frame while a reply streams,
/// and the offset of a scroll viewer is not a styled value a ControlTheme transition could animate.
/// </para>
/// </summary>
[TemplatePart(ScrollPart, typeof(ScrollArea))]
[TemplatePart(JumpPart, typeof(AiFollowJumpButton))]
[PseudoClasses(FollowingPseudoClass)]
internal class AiFollowScroller : TemplatedControl
{
    public const string ScrollPart = "PART_Scroll";
    public const string JumpPart = "PART_Jump";
    public const string FollowingPseudoClass = ":following";

    /// <summary>Within this distance (px) of the bottom the scroller follows again (Arc: 8 still shows the button, 4 does not).</summary>
    internal const double AttachDistance = 6;

    /// <summary>Within this distance (px) of the bottom the scroller follows again: <see cref="AttachDistance"/>, 4 in the model arena.</summary>
    internal double ReattachDistance { get; set; } = AttachDistance;

    /// <summary>The share of the remaining distance the chase covers in a 60 Hz frame.</summary>
    internal const double ChaseRate = 0.22;

    /// <summary>Offset changes this soon after a wheel, pointer or key event in the thread count as the person's scroll.</summary>
    internal static readonly TimeSpan InputWindow = TimeSpan.FromMilliseconds(250);

    #region Properties

    /// <summary>The thread.</summary>
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<AiFollowScroller, object?>(nameof(Content));

    /// <summary>The accessible name of the scroll-to-latest button ("Scroll to latest message").</summary>
    public static readonly StyledProperty<string?> JumpButtonNameProperty =
        AvaloniaProperty.Register<AiFollowScroller, string?>(nameof(JumpButtonName));

    /// <summary>The tooltip of the scroll-to-latest button; null shows none (Arc shows none).</summary>
    public static readonly StyledProperty<string?> JumpButtonTipProperty =
        AvaloniaProperty.Register<AiFollowScroller, string?>(nameof(JumpButtonTip));

    /// <summary>Whether the scroller follows new content at the bottom (read-only).</summary>
    public static readonly DirectProperty<AiFollowScroller, bool> IsFollowingProperty =
        AvaloniaProperty.RegisterDirect<AiFollowScroller, bool>(nameof(IsFollowing), o => o.IsFollowing);

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <inheritdoc cref="JumpButtonNameProperty"/>
    public string? JumpButtonName { get => GetValue(JumpButtonNameProperty); set => SetValue(JumpButtonNameProperty, value); }

    /// <inheritdoc cref="JumpButtonTipProperty"/>
    public string? JumpButtonTip { get => GetValue(JumpButtonTipProperty); set => SetValue(JumpButtonTipProperty, value); }

    /// <inheritdoc cref="IsFollowingProperty"/>
    public bool IsFollowing { get => _isFollowing; private set => SetAndRaise(IsFollowingProperty, ref _isFollowing, value); }

    /// <summary>Raised when <see cref="IsFollowing"/> changes.</summary>
    public event EventHandler? FollowingChanged;

    #endregion

    /// <summary>An offset kept for a conversation.</summary>
    private sealed class SavedOffset(double offset, bool following)
    {
        public double Offset { get; } = offset;
        public bool Following { get; } = following;
    }

    private readonly ConditionalWeakTable<object, SavedOffset> _saved = new();
    private readonly LiveLoop _chase;
    private ScrollArea? _scroll;
    private AiFollowJumpButton? _jump;
    private AiFadePresence? _jumpPresence;
    private bool _isFollowing = true;
    private bool _writing;
    private double _ownOffset = double.NaN;
    private DateTime _lastInput = DateTime.MinValue;
    private SavedOffset? _pendingRestore;
    private bool _restorePending;

    static AiFollowScroller()
    {
        FocusableProperty.OverrideDefaultValue<AiFollowScroller>(false);
    }

    public AiFollowScroller()
    {
        PseudoClasses.Set(FollowingPseudoClass, true);
        // The scroller is always on screen while it matters; watching its viewport would cost a walk per layout.
        _chase = new LiveLoop(this, Chase) { TracksViewport = false };
        _chase.RunningChanged += OnChaseRunningChanged;
    }

    /// <summary>The scroll area part (null before the template is applied).</summary>
    internal ScrollArea? Scroll => _scroll;

    /// <summary>The scroll-to-latest button part.</summary>
    internal AiFollowJumpButton? JumpButton => _jump;

    /// <summary>True while the button is asked to show (it may still be fading in).</summary>
    internal bool IsJumpButtonShown => _jumpPresence?.IsShown == true;

    /// <summary>The button's fade.</summary>
    internal AiFadePresence? JumpPresence => _jumpPresence;

    /// <summary>True while the chase delivers frames.</summary>
    internal bool IsChasing => _chase.IsRunning;

    /// <summary>The clock of the input window (tests replace it).</summary>
    internal Func<DateTime> Now { get; set; } = static () => DateTime.UtcNow;

    /// <summary>The largest offset: the bottom of the content.</summary>
    internal double BottomOffset => _scroll is null ? 0 : Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);

    /// <summary>How far the bottom of the viewport is from the bottom of the content.</summary>
    internal double DistanceToBottom => _scroll is null ? 0 : Math.Max(0, BottomOffset - _scroll.Offset.Y);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _scroll = e.NameScope.Find<ScrollArea>(ScrollPart);
        _jump = e.NameScope.Find<AiFollowJumpButton>(JumpPart);
        Hook();
    }

    private void Hook()
    {
        if (_scroll is { } scroll)
        {
            scroll.Content = Content;
            scroll.PropertyChanged += OnScrollPropertyChanged;
            scroll.ScrollChanged += OnScrollChanged;
            // Tunnel and handled too: the scroll viewer, its scrollbar and the thread's own controls handle these first.
            scroll.AddHandler(PointerWheelChangedEvent, OnUserInput, RoutingStrategies.Tunnel, handledEventsToo: true);
            scroll.AddHandler(PointerPressedEvent, OnUserInput, RoutingStrategies.Tunnel, handledEventsToo: true);
            scroll.AddHandler(PointerReleasedEvent, OnUserInput, RoutingStrategies.Tunnel, handledEventsToo: true);
            scroll.AddHandler(PointerMovedEvent, OnPointerMovedInput, RoutingStrategies.Tunnel, handledEventsToo: true);
            scroll.AddHandler(KeyDownEvent, OnUserInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        if (_jump is { } jump)
        {
            jump.Click += OnJumpClick;
            ApplyJumpTexts();
            _jumpPresence = new AiFadePresence(jump, shown: false);
        }
        UpdateJumpButton();
    }

    private void Unhook()
    {
        if (_scroll is { } scroll)
        {
            scroll.Content = null;
            scroll.PropertyChanged -= OnScrollPropertyChanged;
            scroll.ScrollChanged -= OnScrollChanged;
            scroll.RemoveHandler(PointerWheelChangedEvent, OnUserInput);
            scroll.RemoveHandler(PointerPressedEvent, OnUserInput);
            scroll.RemoveHandler(PointerReleasedEvent, OnUserInput);
            scroll.RemoveHandler(PointerMovedEvent, OnPointerMovedInput);
            scroll.RemoveHandler(KeyDownEvent, OnUserInput);
            scroll.LayoutUpdated -= OnRestoreLayout;
        }
        if (_jump is { } jump)
        {
            jump.Click -= OnJumpClick;
        }
        _jumpPresence?.Dispose();
        _jumpPresence = null;
        _chase.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty)
        {
            if (_scroll is not null)
            {
                _scroll.Content = change.NewValue;
            }
        }
        else if (change.Property == JumpButtonNameProperty || change.Property == JumpButtonTipProperty)
        {
            ApplyJumpTexts();
        }
    }

    private void ApplyJumpTexts()
    {
        if (_jump is null)
        {
            return;
        }
        Avalonia.Automation.AutomationProperties.SetName(_jump, JumpButtonName);
        Tooltip.SetTip(_jump, JumpButtonTip);
    }

    #endregion

    #region Following

    /// <summary>
    /// Follows again and moves to the newest content: a chase at full motion (<paramref name="animate"/>), a jump otherwise.
    /// Sending a message or pressing the scroll-to-latest button calls this.
    /// </summary>
    public void ScrollToLatest(bool animate = true)
    {
        SetFollowing(true);
        CancelRestore();
        if (animate)
        {
            FollowToBottom();
        }
        else
        {
            JumpToBottom();
        }
    }

    /// <summary>
    /// Starts over at the top and follows, without motion (a new round in the model arena, whose content was just cleared):
    /// earlier input no longer counts as a scroll by the person, so the clamp of the emptied content cannot release.
    /// </summary>
    internal void ResetToTop()
    {
        CancelRestore();
        _chase.Stop();
        _lastInput = DateTime.MinValue;
        SetFollowing(true);
        Write(0);
    }

    private void SetFollowing(bool following)
    {
        if (following == _isFollowing)
        {
            UpdateJumpButton();
            return;
        }
        IsFollowing = following;
        PseudoClasses.Set(FollowingPseudoClass, following);
        if (!following)
        {
            _chase.Stop();
        }
        UpdateJumpButton();
        FollowingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves to the bottom: chases at full motion once the scroller is on screen, jumps otherwise.</summary>
    private void FollowToBottom()
    {
        if (_scroll is null)
        {
            return;
        }
        if (Math.Abs(BottomOffset - _scroll.Offset.Y) < 0.5)
        {
            _chase.Stop();
            return;
        }
        if (IsLoaded && ArcMotion.IsFull(this))
        {
            _chase.Start();
            if (_chase.IsRunning)
            {
                return;
            }
            _chase.Stop();
        }
        JumpToBottom();
    }

    private void JumpToBottom()
    {
        _chase.Stop();
        Write(BottomOffset);
    }

    /// <summary>One frame of the chase: 22% of the remaining distance per 60 Hz frame; done within a pixel.</summary>
    private bool Chase(double seconds)
    {
        if (_scroll is not { } scroll || !_isFollowing)
        {
            return false;
        }
        var y = scroll.Offset.Y;
        if (!double.IsNaN(_ownOffset) && Math.Abs(y - _ownOffset) > 0.5)
        {
            // Someone else moved the offset since the last frame; the scroll handler decides first.
            return true;
        }
        var bottom = BottomOffset;
        var remaining = bottom - y;
        if (Math.Abs(remaining) < 1)
        {
            Write(bottom);
            return false;
        }
        var keep = Math.Pow(1 - ChaseRate, seconds * 60);
        Write(bottom - remaining * keep);
        return true;
    }

    private void OnChaseRunningChanged(bool running)
    {
        // Paused by a gate while still wanted (the motion level dropped): land at once.
        if (!running && _chase.IsActive && _isFollowing && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            JumpToBottom();
        }
    }

    /// <summary>Sets the offset on the scroller's own behalf; the change handlers tell these apart from other scrolls.</summary>
    private void Write(double y)
    {
        if (_scroll is not { } scroll)
        {
            return;
        }
        _writing = true;
        try
        {
            scroll.Offset = new Vector(scroll.Offset.X, y);
        }
        finally
        {
            _writing = false;
        }
        // The viewer clamps; remember what it took.
        _ownOffset = scroll.Offset.Y;
    }

    private void OnScrollPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollViewer.OffsetProperty || _writing || _scroll is null)
        {
            return;
        }
        var old = e.GetOldValue<Vector>().Y;
        var now = e.GetNewValue<Vector>().Y;
        if (Math.Abs(now - old) < 0.01)
        {
            return;
        }
        // The person's scroll up releases at once, before the next chase frame could pull it back down.
        if (_isFollowing && now < old - 0.5 && IsUserScrolling() && DistanceToBottom > ReattachDistance)
        {
            _ownOffset = now;
            SetFollowing(false);
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroll is not { } scroll || _restorePending)
        {
            return;
        }
        var y = scroll.Offset.Y;
        var foreign = Math.Abs(e.OffsetDelta.Y) > 0.01 && (double.IsNaN(_ownOffset) || Math.Abs(y - _ownOffset) > 0.5);
        if (foreign)
        {
            // A scroll the scroller did not make: the person's, a clamp, or code's.
            _ownOffset = y;
            if (DistanceToBottom <= ReattachDistance)
            {
                SetFollowing(true);
            }
            else if (_isFollowing && e.OffsetDelta.Y < 0 && IsUserScrolling())
            {
                SetFollowing(false);
            }
        }
        var layoutChanged = Math.Abs(e.ExtentDelta.Y) > 0.01 || Math.Abs(e.ViewportDelta.Y) > 0.01;
        if (_isFollowing && (layoutChanged || foreign))
        {
            if (e.ExtentDelta.Y > 0 && Math.Abs(scroll.Extent.Height - e.ExtentDelta.Y) < 0.5)
            {
                // The first content: it lands at the bottom without motion.
                JumpToBottom();
            }
            else
            {
                FollowToBottom();
            }
        }
        UpdateJumpButton();
    }

    private bool IsUserScrolling() => Now() - _lastInput <= InputWindow;

    private void OnUserInput(object? sender, RoutedEventArgs e) => _lastInput = Now();

    /// <summary>
    /// Counts the offset changes of the next <see cref="InputWindow"/> as the person's scroll. For an owner that takes the
    /// keys itself (the model arena's answer is the focus, not the scroll area, so its keys never pass the area).
    /// </summary>
    internal void NoteUserInput() => _lastInput = Now();

    private void OnPointerMovedInput(object? sender, PointerEventArgs e)
    {
        // Only a drag scrolls (scrollbar thumb, touch pan); hovering over the thread does not.
        var properties = e.GetCurrentPoint(this).Properties;
        if (e.Pointer.Type == PointerType.Touch || properties.IsLeftButtonPressed || properties.IsMiddleButtonPressed ||
            properties.IsRightButtonPressed)
        {
            _lastInput = Now();
        }
    }

    #endregion

    #region Scroll-to-latest button

    private void UpdateJumpButton()
    {
        if (_jumpPresence is null || _scroll is null)
        {
            return;
        }
        var show = !_isFollowing && DistanceToBottom > ReattachDistance;
        if (!show && _jump is { IsKeyboardFocusWithin: true })
        {
            // The button goes away under the keyboard: stay in the thread (the scroll area takes keys, not a tab stop).
            _scroll.Focus(NavigationMethod.Unspecified);
        }
        _jumpPresence.SetShown(show);
    }

    private void OnJumpClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ScrollToLatest();
    }

    #endregion

    #region Offsets per conversation

    /// <summary>Remembers the offset (and whether it follows) for <paramref name="key"/>, such as a conversation.</summary>
    public void SaveOffset(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_scroll is null)
        {
            return;
        }
        var saved = _restorePending && _pendingRestore is { } pending
            ? pending
            : new SavedOffset(_scroll.Offset.Y, _isFollowing);
        _saved.AddOrUpdate(key, saved);
    }

    /// <summary>
    /// Restores the offset saved for <paramref name="key"/> once the content now shown is laid out (call it after putting
    /// that conversation's thread in place): at the bottom when it was following or nothing was saved, else at the
    /// saved offset (clamped). Without motion.
    /// </summary>
    public void RestoreOffset(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _pendingRestore = _saved.TryGetValue(key, out var saved) ? saved : null;
        _chase.Stop();
        if (_scroll is not { } scroll)
        {
            return;
        }
        if (!_restorePending)
        {
            _restorePending = true;
            scroll.LayoutUpdated += OnRestoreLayout;
        }
        // Make sure a layout pass comes even when nothing else changes.
        scroll.InvalidateMeasure();
    }

    private void OnRestoreLayout(object? sender, EventArgs e)
    {
        var saved = _pendingRestore;
        CancelRestore();
        if (saved is null || saved.Following)
        {
            SetFollowing(true);
            JumpToBottom();
            return;
        }
        Write(Math.Min(saved.Offset, BottomOffset));
        SetFollowing(DistanceToBottom <= ReattachDistance);
    }

    private void CancelRestore()
    {
        if (!_restorePending)
        {
            return;
        }
        _restorePending = false;
        _pendingRestore = null;
        if (_scroll is not null)
        {
            _scroll.LayoutUpdated -= OnRestoreLayout;
        }
    }

    #endregion

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _chase.Stop();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_isFollowing && _scroll is not null)
        {
            // Content may have grown while away: land at the bottom without motion.
            Dispatcher.UIThread.Post(() =>
            {
                if (_isFollowing && !_restorePending && _scroll is not null)
                {
                    JumpToBottom();
                }
            }, DispatcherPriority.Loaded);
        }
    }
}
