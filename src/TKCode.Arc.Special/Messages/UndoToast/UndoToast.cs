using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Why an <see cref="UndoToast"/> closed.</summary>
public enum UndoToastCloseReason
{
    /// <summary>The Undo button, the undo shortcut or <see cref="UndoToast.Undo"/>: the action is to be reverted.</summary>
    Undone,

    /// <summary>The countdown ran out: the action stands.</summary>
    TimedOut,

    /// <summary>The dismiss button, Escape or <see cref="UndoToast.Dismiss"/>: the action stands.</summary>
    Dismissed,

    /// <summary>A newer action was shown in the same toast: the earlier one stands.</summary>
    Replaced
}

/// <summary>Arguments of <see cref="UndoToast.Closed"/>.</summary>
public class UndoToastClosedEventArgs : RoutedEventArgs
{
    public UndoToastClosedEventArgs(RoutedEvent routedEvent, UndoToastCloseReason reason, string? message)
        : base(routedEvent)
    {
        Reason = reason;
        Message = message;
    }

    /// <summary>How the toast closed; only <see cref="UndoToastCloseReason.Undone"/> asks for the action to be reverted.</summary>
    public UndoToastCloseReason Reason { get; }

    /// <summary>The message of the action the toast was showing.</summary>
    public string? Message { get; }

    /// <summary>True when the action was undone.</summary>
    public bool IsUndone => Reason == UndoToastCloseReason.Undone;
}

/// <summary>
/// A toast for an action that can be taken back: "Conversation archived" with an Undo button and a ring that counts the
/// seconds left. The countdown holds while the pointer is over the toast or focus is inside it, and continues where it
/// stopped. When it runs out (or the toast is dismissed) the action stands; Undo, or the platform's undo shortcut while
/// the toast is open, raises <see cref="Undone"/> and runs <see cref="UndoCommand"/>. Showing a new action in an open toast
/// lets the earlier one stand and restarts the countdown. <see cref="ShowAsync"/> answers with true when the action
/// was undone, so the caller can defer the real work until the toast closes.
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(RingPart, typeof(ArcRing))]
[TemplatePart(SecondsPart, typeof(TextBlock))]
[TemplatePart(UndoPart, typeof(Button))]
[TemplatePart(ClosePart, typeof(Button))]
[PseudoClasses(PausedPseudoClass, CountdownPseudoClass)]
public class UndoToast : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string RingPart = "PART_Ring";
    public const string SecondsPart = "PART_Seconds";
    public const string UndoPart = "PART_Undo";
    public const string ClosePart = "PART_Close";
    public const string PausedPseudoClass = ":paused";
    public const string CountdownPseudoClass = ":countdown";

    #region Properties

    /// <summary>What just happened, in the past tense ("Conversation archived").</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<UndoToast, string?>(nameof(Message));

    /// <summary>The undo button's label.</summary>
    public static readonly StyledProperty<string> UndoTextProperty =
        AvaloniaProperty.Register<UndoToast, string>(nameof(UndoText), "Undo");

    /// <summary>How long the action can be undone (5 seconds by default); time under the pointer or with focus inside does not count.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<UndoToast, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(5));

    /// <summary>Whether the toast is shown. Setting it to false dismisses the action (it stands). Two-way.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<UndoToast, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the countdown ring with the seconds left is shown (default true).</summary>
    public static readonly StyledProperty<bool> IsCountdownVisibleProperty =
        AvaloniaProperty.Register<UndoToast, bool>(nameof(IsCountdownVisible), true);

    /// <summary>Whether the platform's undo shortcut (Ctrl+Z, Cmd+Z on macOS) undoes the action while the toast is open, unless a text field has focus (default true).</summary>
    public static readonly StyledProperty<bool> IsShortcutEnabledProperty =
        AvaloniaProperty.Register<UndoToast, bool>(nameof(IsShortcutEnabled), true);

    /// <summary>Runs when the action is undone.</summary>
    public static readonly StyledProperty<ICommand?> UndoCommandProperty =
        AvaloniaProperty.Register<UndoToast, ICommand?>(nameof(UndoCommand));

    /// <summary>The parameter of <see cref="UndoCommand"/>.</summary>
    public static readonly StyledProperty<object?> UndoCommandParameterProperty =
        AvaloniaProperty.Register<UndoToast, object?>(nameof(UndoCommandParameter));

    /// <summary>The words the toast gives assistive technology (English by default).</summary>
    public static readonly StyledProperty<UndoToastLabels> LabelsProperty =
        AvaloniaProperty.Register<UndoToast, UndoToastLabels>(nameof(Labels), UndoToastLabels.Default);

    /// <summary>True while the countdown holds (pointer over the toast, or focus inside it).</summary>
    public static readonly DirectProperty<UndoToast, bool> IsPausedProperty =
        AvaloniaProperty.RegisterDirect<UndoToast, bool>(nameof(IsPaused), o => o.IsPaused);

    /// <summary>Raised when the action is undone.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> UndoneEvent =
        RoutedEvent.Register<UndoToast, RoutedEventArgs>(nameof(Undone), RoutingStrategies.Bubble);

    /// <summary>Raised when the toast closes, with the reason.</summary>
    public static readonly RoutedEvent<UndoToastClosedEventArgs> ClosedEvent =
        RoutedEvent.Register<UndoToast, UndoToastClosedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> MaxToastWidthProperty =
        AvaloniaProperty.Register<UndoToast, double>(nameof(MaxToastWidth), 416);

    /// <summary>What just happened, in the past tense ("Conversation archived").</summary>
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <summary>The undo button's label.</summary>
    public string UndoText { get => GetValue(UndoTextProperty); set => SetValue(UndoTextProperty, value); }

    /// <summary>How long the action can be undone (5 seconds by default); time under the pointer or with focus inside does not count.</summary>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <summary>Whether the toast is shown. Setting it to false dismisses the action (it stands). Two-way.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Whether the countdown ring with the seconds left is shown (default true).</summary>
    public bool IsCountdownVisible { get => GetValue(IsCountdownVisibleProperty); set => SetValue(IsCountdownVisibleProperty, value); }

    /// <summary>Whether the platform's undo shortcut (Ctrl+Z, Cmd+Z on macOS) undoes the action while the toast is open, unless a text field has focus (default true).</summary>
    public bool IsShortcutEnabled { get => GetValue(IsShortcutEnabledProperty); set => SetValue(IsShortcutEnabledProperty, value); }

    /// <summary>Runs when the action is undone.</summary>
    public ICommand? UndoCommand { get => GetValue(UndoCommandProperty); set => SetValue(UndoCommandProperty, value); }

    /// <summary>The parameter of <see cref="UndoCommand"/>.</summary>
    public object? UndoCommandParameter { get => GetValue(UndoCommandParameterProperty); set => SetValue(UndoCommandParameterProperty, value); }

    /// <summary>The words the toast gives assistive technology (English by default).</summary>
    public UndoToastLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>True while the countdown holds (pointer over the toast, or focus inside it).</summary>
    public bool IsPaused { get => _isPaused; private set => SetAndRaise(IsPausedProperty, ref _isPaused, value); }

    /// <summary>Time left to undo (zero when closed).</summary>
    public TimeSpan Remaining => _timeline?.Remaining ?? TimeSpan.Zero;

    /// <summary>Raised when the action is undone.</summary>
    public event EventHandler<RoutedEventArgs>? Undone
    {
        add => AddHandler(UndoneEvent, value);
        remove => RemoveHandler(UndoneEvent, value);
    }

    /// <summary>Raised when the toast closes, with the reason.</summary>
    public event EventHandler<UndoToastClosedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    internal double MaxToastWidth { get => GetValue(MaxToastWidthProperty); set => SetValue(MaxToastWidthProperty, value); }

    #endregion

    // Motion is driven from code: the toast must stay visible until its exit ends and then stop taking space, which
    // a ControlTheme transition cannot express.
    private readonly MatrixTransform _transform = new();
    private Panel? _root;
    private ArcRing? _ring;
    private TextBlock? _seconds;
    private Button? _undo;
    private Button? _close;
    private MotionValue<double>? _y;
    private MotionValue<double>? _scale;
    private MotionValue<double>? _opacity;
    private MotionTimeline? _timeline;
    private TopLevel? _shortcutRoot;
    private TaskCompletionSource<bool>? _pending;
    private bool _visible;
    private bool _isPaused;
    private bool _pointerInside;
    private bool _closing;
    private int _shownSeconds = -1;
    private int _generation;

    // Enter: rise 16px from 96%; exit: sink 8px to 97% (Arc's toast motion).
    private const double EnterOffset = 16;
    private const double EnterScale = 0.96;
    private const double ExitOffset = 8;
    private const double ExitScale = 0.97;

    private static readonly string[] s_seconds = Enumerable.Range(0, 100).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    static UndoToast()
    {
        AffectsMeasure<UndoToast>(MaxToastWidthProperty);
        FocusableProperty.OverrideDefaultValue<UndoToast>(false);
    }

    /// <summary>True while the toast shows an action (not while its exit plays).</summary>
    internal bool IsShowing => _visible;

    internal MotionTimeline? Timeline => _timeline;

    /// <summary>
    /// Shows <paramref name="message"/> with a fresh countdown (<paramref name="duration"/>, or <see cref="Duration"/>). An
    /// action the toast was already showing stands (closes as <see cref="UndoToastCloseReason.Replaced"/>).
    /// </summary>
    public void Show(string message, TimeSpan? duration = null)
    {
        var replacing = _visible;
        if (replacing)
        {
            Finish(UndoToastCloseReason.Replaced, animate: false);
        }
        if (duration is { } d)
        {
            SetCurrentValue(DurationProperty, d);
        }
        SetCurrentValue(MessageProperty, message);
        if (IsOpen)
        {
            // A replaced action keeps the toast where it is; only the copy and the countdown start over.
            ShowCore(animate: IsLoaded, inPlace: replacing);
        }
        else
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    /// <summary>
    /// Shows <paramref name="message"/> like <see cref="Show"/> and completes when the toast closes: true when the action
    /// was undone, false when it stands.
    /// </summary>
    public Task<bool> ShowAsync(string message, TimeSpan? duration = null)
    {
        Show(message, duration);
        if (!_visible)
        {
            return Task.FromResult(false);
        }
        _pending = new TaskCompletionSource<bool>();
        return _pending.Task;
    }

    /// <summary>Undoes the action shown, as the Undo button does.</summary>
    public void Undo()
    {
        if (!_visible)
        {
            return;
        }
        Finish(UndoToastCloseReason.Undone, animate: true);
    }

    /// <summary>Closes the toast and lets the action stand, as the dismiss button does.</summary>
    public void Dismiss()
    {
        if (!_visible)
        {
            return;
        }
        Finish(UndoToastCloseReason.Dismissed, animate: true);
    }

    #region Template and state

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_undo is not null)
        {
            _undo.Click -= OnUndoClick;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_root is not null)
        {
            _root.RenderTransform = null;
        }
        _root = e.NameScope.Find<Panel>(RootPart);
        _ring = e.NameScope.Find<ArcRing>(RingPart);
        _seconds = e.NameScope.Find<TextBlock>(SecondsPart);
        _undo = e.NameScope.Find<Button>(UndoPart);
        _close = e.NameScope.Find<Button>(ClosePart);
        if (_undo is not null)
        {
            _undo.Click += OnUndoClick;
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (_root is not null)
        {
            _root.RenderTransform = _transform;
            _root.IsVisible = _visible;
        }
        _shownSeconds = -1;
        UpdateCountdown(_timeline?.Value ?? 0);
        UpdateAutomation();
        UpdatePseudoClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (_closing)
            {
                return;
            }
            if (change.GetNewValue<bool>())
            {
                ShowCore(animate: IsLoaded);
            }
            else if (_visible)
            {
                Finish(UndoToastCloseReason.Dismissed, animate: true);
            }
        }
        else if (change.Property == MessageProperty || change.Property == LabelsProperty || change.Property == UndoTextProperty)
        {
            UpdateAutomation();
        }
        else if (change.Property == IsCountdownVisibleProperty)
        {
            UpdatePseudoClasses();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty)
        {
            UpdatePause();
        }
        else if (change.Property == IsShortcutEnabledProperty)
        {
            UpdateShortcut();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateShortcut();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateShortcut();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (IsOpen && !_visible)
        {
            ShowCore(animate: true);
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        // A toast that leaves the window lets its action stand.
        if (_visible)
        {
            Finish(UndoToastCloseReason.Dismissed, animate: false);
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(PausedPseudoClass, IsPaused);
        PseudoClasses.Set(CountdownPseudoClass, IsCountdownVisible);
    }

    private void UpdateAutomation()
    {
        var labels = Labels ?? UndoToastLabels.Default;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(this, Message ?? string.Empty);
        AutomationProperties.SetHelpText(this, IsShortcutEnabled && UndoGestureText() is { } gesture ? labels.ShortcutHint(gesture) : string.Empty);
        if (_undo is not null)
        {
            AutomationProperties.SetName(_undo, labels.UndoName(UndoText, Message ?? string.Empty));
        }
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, labels.Dismiss);
        }
    }

    #endregion

    #region Show and close

    private void EnsureMotion()
    {
        if (_y is not null)
        {
            return;
        }
        _y = new MotionValue<double>(this, 0, MotionVectors.Double);
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _y.Changed += _ => ApplyTransform();
        _scale.Changed += _ => ApplyTransform();
        _opacity.Changed += v =>
        {
            if (_root is not null)
            {
                _root.Opacity = Math.Clamp(v, 0, 1);
            }
        };
        _timeline = new MotionTimeline(this);
        _timeline.Changed += UpdateCountdown;
        _timeline.Completed += OnTimelineCompleted;
    }

    private void ApplyTransform()
    {
        var s = _scale?.Value ?? 1;
        _transform.Matrix = Matrix.CreateScale(s, s) * Matrix.CreateTranslation(0, _y?.Value ?? 0);
    }

    private void ShowCore(bool animate, bool inPlace = false)
    {
        EnsureMotion();
        ++_generation;
        var wasVisible = _visible;
        _visible = true;
        if (_root is not null)
        {
            _root.IsVisible = true;
        }
        InvalidateMeasure();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (inPlace)
        {
            // Already shown: keep whatever motion is settling.
        }
        else if (!animate || level == ArcMotionLevel.Off)
        {
            _y!.Jump(0);
            _scale!.Jump(1);
            _opacity!.Jump(1);
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            _y!.Jump(0);
            _scale!.Jump(1);
            if (!wasVisible && _opacity!.Value >= 0.999)
            {
                _opacity.Jump(0);
            }
            _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else
        {
            if (!wasVisible && (_opacity!.Value >= 0.999 || _opacity.Value <= 0.001))
            {
                // Rises from below its edge; a toast raised mid-exit turns back from where it is.
                _y!.Jump(EnterOffset);
                _scale!.Jump(EnterScale);
                _opacity.Jump(0);
            }
            _ = _y!.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph));
            _ = _scale!.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
            _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        }
        _shownSeconds = -1;
        var duration = Duration > TimeSpan.Zero ? Duration : TimeSpan.FromSeconds(5);
        _timeline!.Start(duration);
        UpdateCountdown(0);
        UpdatePause();
        UpdateShortcut();
        UpdateAutomation();
    }

    private void OnTimelineCompleted()
    {
        if (_visible)
        {
            Finish(UndoToastCloseReason.TimedOut, animate: true);
        }
    }

    private void Finish(UndoToastCloseReason reason, bool animate)
    {
        if (!_visible)
        {
            return;
        }
        var message = Message;
        var pending = _pending;
        _pending = null;
        _visible = false;
        _timeline?.Stop();
        IsPaused = false;
        UpdatePseudoClasses();
        UpdateShortcut();
        if (reason != UndoToastCloseReason.Replaced)
        {
            _closing = true;
            try
            {
                SetCurrentValue(IsOpenProperty, false);
            }
            finally
            {
                _closing = false;
            }
            Hide(animate);
        }
        if (reason == UndoToastCloseReason.Undone)
        {
            RaiseEvent(new RoutedEventArgs(UndoneEvent));
            if (UndoCommand is { } command && command.CanExecute(UndoCommandParameter))
            {
                command.Execute(UndoCommandParameter);
            }
        }
        RaiseEvent(new UndoToastClosedEventArgs(ClosedEvent, reason, message));
        pending?.TrySetResult(reason == UndoToastCloseReason.Undone);
    }

    private async void Hide(bool animate)
    {
        EnsureMotion();
        var generation = ++_generation;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || !IsLoaded || level == ArcMotionLevel.Off)
        {
            _opacity!.Jump(0);
            Collapse(generation);
            return;
        }
        Task<bool> done;
        if (level == ArcMotionLevel.Reduced)
        {
            done = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else
        {
            // Sinks back toward the edge it came from.
            _ = _y!.AnimateTo(ExitOffset, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Exit));
            _ = _scale!.AnimateTo(ExitScale, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Exit));
            done = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Exit));
        }
        await done;
        Collapse(generation);
    }

    private void Collapse(int generation)
    {
        if (generation != _generation || _visible)
        {
            return;
        }
        if (_root is not null)
        {
            _root.IsVisible = false;
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Min(availableSize.Width, MaxToastWidth);
        var size = base.MeasureOverride(availableSize.WithWidth(width));
        if (_root is { IsVisible: false })
        {
            return default;
        }
        return size;
    }

    #endregion

    #region Countdown and pause

    private void UpdateCountdown(double progress)
    {
        if (_ring is not null)
        {
            _ring.Value = Math.Clamp(1 - progress, 0, 1);
        }
        if (_seconds is null)
        {
            return;
        }
        var remaining = _visible && _timeline is not null ? _timeline.Remaining : TimeSpan.Zero;
        var seconds = Math.Clamp((int)Math.Ceiling(remaining.TotalSeconds - 1e-6), 0, s_seconds.Length - 1);
        if (seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            _seconds.Text = s_seconds[seconds];
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _pointerInside = true;
        UpdatePause();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerInside = false;
        UpdatePause();
    }

    private void UpdatePause()
    {
        var hold = _visible && (_pointerInside || IsKeyboardFocusWithin);
        if (hold == IsPaused)
        {
            return;
        }
        IsPaused = hold;
        if (_timeline is not null)
        {
            if (hold)
            {
                _timeline.Pause();
            }
            else
            {
                _timeline.Resume();
            }
        }
        UpdatePseudoClasses();
    }

    #endregion

    #region Input

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Undo();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Dismiss();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && _visible)
        {
            e.Handled = true;
            Dismiss();
        }
    }

    private void UpdateShortcut()
    {
        var root = _visible && IsShortcutEnabled ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(root, _shortcutRoot))
        {
            return;
        }
        _shortcutRoot?.RemoveHandler(KeyDownEvent, OnRootKeyDown);
        _shortcutRoot = root;
        _shortcutRoot?.AddHandler(KeyDownEvent, OnRootKeyDown, RoutingStrategies.Bubble);
    }

    private void OnRootKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !_visible || !IsShortcutEnabled || !IsUndoGesture(e))
        {
            return;
        }
        // A text field keeps its own undo.
        if (e.Source is TextBox || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }
        e.Handled = true;
        Undo();
    }

    private static bool IsUndoGesture(KeyEventArgs e)
    {
        if (Application.Current?.PlatformSettings?.HotkeyConfiguration.Undo is { Count: > 0 } gestures)
        {
            foreach (var gesture in gestures)
            {
                if (gesture.Matches(e))
                {
                    return true;
                }
            }
            return false;
        }
        return e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control;
    }

    private static string? UndoGestureText()
    {
        if (Application.Current?.PlatformSettings?.HotkeyConfiguration.Undo is { Count: > 0 } gestures)
        {
            return gestures[0].ToString("p", null);
        }
        return "Ctrl+Z";
    }

    #endregion
}
