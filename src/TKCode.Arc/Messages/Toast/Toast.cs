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
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Why a <see cref="Toast"/> closed.</summary>
public enum ToastCloseReason
{
    /// <summary>The dismiss button (or <see cref="Toast.Close"/>).</summary>
    Dismissed,

    /// <summary>Swiped away past the distance or speed threshold.</summary>
    Swiped,

    /// <summary>The <see cref="Toast.Duration"/> elapsed.</summary>
    TimedOut
}

/// <summary>Arguments of <see cref="Toast.Closed"/>.</summary>
public class ToastClosedEventArgs : RoutedEventArgs
{
    public ToastClosedEventArgs(RoutedEvent routedEvent, ToastCloseReason reason)
        : base(routedEvent)
    {
        Reason = reason;
    }

    /// <summary>How the toast was closed.</summary>
    public ToastCloseReason Reason { get; }
}

/// <summary>
/// Arc toast: a brief confirmation for a completed background action (one locally controlled success message such
/// as "Changes saved"). It rises from its edge on the morph spring while its check mark draws itself, closes after
/// <see cref="Duration"/> (the timer does not pause, so keep the copy short), and can be swiped away in either
/// direction with velocity. Text changes rise in place while the height springs to fit.
/// <para>
/// For app-wide notifications (types, actions, updates, stacking) call <see cref="Show(Visual, string, ToastKind, string?, ToastAction?, TimeSpan?, string?)"/>
/// from anywhere: it goes to the window's <see cref="ToastStack"/> (see <see cref="ToastHost"/>).
/// </para>
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(CheckPart, typeof(StrokeDrawGlyph))]
[TemplatePart(ClosePart, typeof(AvaloniaButton))]
[PseudoClasses(DraggingPseudoClass)]
public class Toast : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string CheckPart = "PART_Check";
    public const string ClosePart = "PART_Close";
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>A swipe past this distance (px) or speed (px/s) dismisses the toast.</summary>
    internal const double SwipeDistance = 80;

    internal const double SwipeVelocity = 480;

    #region 公共属性定义

    /// <summary>The message. A new title rises in while the old one lifts away.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Toast, string?>(nameof(Title));

    /// <summary>Optional second line.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Toast, string?>(nameof(Description));

    /// <summary>Whether the toast is shown (default true). Closing, swiping or timing out sets it to false. Two-way.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Toast, bool>(nameof(IsOpen), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Time before the toast closes itself (default 4.5s); zero or less keeps it until closed.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<Toast, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(4500));

    /// <summary>Raised when the toast starts closing.</summary>
    public static readonly RoutedEvent<ToastClosedEventArgs> ClosedEvent =
        RoutedEvent.Register<Toast, ToastClosedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    internal static readonly DirectProperty<Toast, Thickness> DescriptionMarginProperty =
        AvaloniaProperty.RegisterDirect<Toast, Thickness>(nameof(DescriptionMargin), o => o.DescriptionMargin);

    internal static readonly StyledProperty<double> MaxToastWidthProperty =
        AvaloniaProperty.Register<Toast, double>(nameof(MaxToastWidth), 416);

    /// <summary>The message. A new title rises in while the old one lifts away.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Optional second line.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Whether the toast is shown (default true). Closing, swiping or timing out sets it to false. Two-way.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Time before the toast closes itself (default 4.5s); zero or less keeps it until closed.</summary>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>Raised when the toast starts closing (button, swipe or timeout).</summary>
    public event EventHandler<ToastClosedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    internal Thickness DescriptionMargin
    {
        get => _descriptionMargin;
        private set => SetAndRaise(DescriptionMarginProperty, ref _descriptionMargin, value);
    }

    internal double MaxToastWidth
    {
        get => GetValue(MaxToastWidthProperty);
        set => SetValue(MaxToastWidthProperty, value);
    }

    #endregion

    #region 应用级通知

    /// <summary>
    /// Shows an app-wide toast in the <see cref="ToastStack"/> of <paramref name="owner"/>'s window (one placed in
    /// the window, or one created on its overlay layer) and returns its id. Reuse an id to update a toast in place.
    /// </summary>
    public static string Show(Visual owner, string title, ToastKind kind = ToastKind.Info, string? description = null,
                              ToastAction? action = null, TimeSpan? duration = null, string? id = null)
        => ToastHost.GetService(owner).Show(new ToastOptions
        {
            Id = id,
            Kind = kind,
            Title = title,
            Description = description,
            Action = action,
            Duration = duration
        });

    /// <summary>Morphs a shown toast in place (icon, copy, height, action) and restarts its timer.</summary>
    public static void Update(Visual owner, string id, ToastUpdate update) => ToastHost.GetService(owner).Update(id, update);

    /// <summary>Dismisses one toast, or every toast of the window when <paramref name="id"/> is null.</summary>
    public static void Dismiss(Visual owner, string? id = null) => ToastHost.GetService(owner).Dismiss(id);

    #endregion

    private static readonly SpringSpec ThrowSpring = SpringSpec.FromVisual(0.3, 0);
    private readonly MatrixTransform _transform = new();
    private readonly ToastSwipeTracker _swipe = new();
    private Thickness _descriptionMargin;
    private Panel? _root;
    private StrokeDrawGlyph? _check;
    private AvaloniaButton? _close;
    private MotionValue<double>? _x;
    private MotionValue<double>? _y;
    private MotionValue<double>? _scale;
    private MotionValue<double>? _opacity;
    private PausableTimer? _timer;
    private bool _visible;
    private int _generation;

    static Toast()
    {
        AffectsMeasure<Toast>(MaxToastWidthProperty);
    }

    /// <summary>Closes the toast like the dismiss button.</summary>
    public void Close() => Dismiss(ToastCloseReason.Dismissed, 0, 0);

    internal bool IsShowing => _visible;

    internal double OffsetX => _x?.Value ?? 0;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_root is not null)
        {
            _root.RenderTransform = null;
        }
        _root = e.NameScope.Find<Panel>(RootPart);
        _check = e.NameScope.Find<StrokeDrawGlyph>(CheckPart);
        _close = e.NameScope.Find<AvaloniaButton>(ClosePart);
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
            AutomationProperties.SetName(_close, "Dismiss notification");
        }
        if (_root is not null)
        {
            _root.RenderTransform = _transform;
            _root.IsVisible = _visible || IsOpen;
        }
        UpdateAutomation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                ShowToast(animate: IsLoaded);
            }
            else if (_visible)
            {
                Hide(0, 0);
            }
        }
        else if (change.Property == DescriptionProperty)
        {
            DescriptionMargin = string.IsNullOrEmpty(Description) ? default : new Thickness(0, 2, 0, 0);
            UpdateAutomation();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateAutomation();
        }
        else if (change.Property == DurationProperty && _visible)
        {
            RestartTimer();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (IsOpen && !_visible)
        {
            // Toasts that mount while the app runs rise in from their edge.
            ShowToast(animate: true);
        }
        else if (_visible)
        {
            RestartTimer();
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _timer?.Stop();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(this, Title ?? string.Empty);
        AutomationProperties.SetHelpText(this, Description ?? string.Empty);
    }

    private void EnsureValues()
    {
        if (_x is not null)
        {
            return;
        }
        _x = new MotionValue<double>(this, 0, MotionVectors.Double);
        _y = new MotionValue<double>(this, 0, MotionVectors.Double);
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _x.Changed += _ => ApplyTransform();
        _y.Changed += _ => ApplyTransform();
        _scale.Changed += _ => ApplyTransform();
        _opacity.Changed += v =>
        {
            if (_root is not null)
            {
                _root.Opacity = Math.Clamp(v, 0, 1);
            }
        };
    }

    private void ApplyTransform()
    {
        var s = _scale?.Value ?? 1;
        _transform.Matrix = Matrix.CreateScale(s, s) * Matrix.CreateTranslation(_x?.Value ?? 0, _y?.Value ?? 0);
    }

    private void ShowToast(bool animate)
    {
        EnsureValues();
        ++_generation;
        var wasVisible = _visible;
        _visible = true;
        if (_root is not null)
        {
            _root.IsVisible = true;
        }
        InvalidateMeasure();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off)
        {
            _x!.Jump(0);
            _y!.Jump(0);
            _scale!.Jump(1);
            _opacity!.Jump(1);
            if (_check is not null)
            {
                _check.Progress = 1;
            }
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            _x!.Jump(0);
            _y!.Jump(0);
            _scale!.Jump(1);
            if (!wasVisible)
            {
                _opacity!.Jump(0);
            }
            _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            if (_check is not null)
            {
                _check.Progress = 1;
            }
        }
        else
        {
            if (!wasVisible && _opacity!.Value <= 0.001)
            {
                // Hidden: rise from 16px below at 0.96 scale.
                _x!.Jump(0);
                _y!.Jump(16);
                _scale!.Jump(0.96);
            }
            // A toast raised mid-exit simply turns back from where it is.
            _ = _x!.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph));
            _ = _y!.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph));
            _ = _scale!.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
            _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
            if (_check is not null)
            {
                _check.Progress = 0;
                _ = _check.Play(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromSeconds(0.12));
            }
        }
        RestartTimer();
    }

    private void RestartTimer()
    {
        _timer?.Stop();
        if (!_visible || !IsLoaded || Duration <= TimeSpan.Zero)
        {
            return;
        }
        _timer ??= new PausableTimer(Duration, OnTimeout);
        _timer.Restart(Duration);
    }

    private void OnTimeout()
    {
        if (_visible)
        {
            Dismiss(ToastCloseReason.TimedOut, 0, 0);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Dismiss(ToastCloseReason.Dismissed, 0, 0);

    /// <summary>Closing tells the owner at once while the exit plays here, so a new toast can be raised mid-exit.</summary>
    private void Dismiss(ToastCloseReason reason, double throwTo, double velocity)
    {
        if (!_visible)
        {
            return;
        }
        Hide(throwTo, velocity);
        SetCurrentValue(IsOpenProperty, false);
        RaiseEvent(new ToastClosedEventArgs(ClosedEvent, reason));
    }

    private void Hide(double throwTo, double velocity)
    {
        if (!_visible)
        {
            return;
        }
        _visible = false;
        _timer?.Stop();
        EnsureValues();
        var generation = ++_generation;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            _opacity!.Jump(0);
            Finish(generation);
            return;
        }
        Task<bool> done;
        if (level == ArcMotionLevel.Reduced)
        {
            done = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else if (throwTo != 0)
        {
            // A thrown toast keeps its release velocity and settles loosely; it is invisible once the fade ends.
            _ = _x!.AnimateTo(throwTo, MotionSpec.Spring(ThrowSpring), velocity);
            done = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        }
        else
        {
            // A closed toast sinks back toward the edge it came from.
            _ = _y!.AnimateTo(8, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
            _ = _scale!.AnimateTo(0.97, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
            done = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        }
        FinishWhenDone(generation, done);
    }

    private void FinishWhenDone(int generation, Task<bool> task)
    {
        task.ThenOnUiThread(Dispatcher, () =>
        {
            Finish(generation);
        });
    }

    private void Finish(int generation)
    {
        if (generation != _generation || _visible)
        {
            return;
        }
        _x?.Stop();
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
        return new Size(double.IsInfinity(width) ? size.Width : width, size.Height);
    }

    #region 拖动甩出

    private bool CanDrag => _visible && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!CanDrag || !point.Properties.IsLeftButtonPressed || IsInsideButton(e.Source))
        {
            return;
        }
        // Catch a toast that is still springing back, from exactly where it is.
        _x!.Stop();
        _swipe.Begin(point.Position, _x.Value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_swipe.IsTracking)
        {
            return;
        }
        var started = _swipe.Move(e.GetPosition(this), e.Timestamp);
        if (started)
        {
            e.Pointer.Capture(this);
            PseudoClasses.Set(DraggingPseudoClass, true);
        }
        if (_swipe.IsDragging)
        {
            _x!.Jump(_swipe.RawOffset);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndSwipe(e.Timestamp, cancelled: false, e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndSwipe(0, cancelled: true, null);
    }

    private void EndSwipe(ulong timestamp, bool cancelled, IPointer? pointer)
    {
        if (!_swipe.IsTracking)
        {
            return;
        }
        var dragging = _swipe.IsDragging;
        var velocity = _swipe.End(timestamp, cancelled);
        if (!dragging)
        {
            return;
        }
        PseudoClasses.Set(DraggingPseudoClass, false);
        pointer?.Capture(null);
        var offset = _x!.Value;
        if (!cancelled && (Math.Abs(offset) >= SwipeDistance || Math.Abs(velocity) >= SwipeVelocity))
        {
            var direction = Math.Sign(offset != 0 ? offset : velocity);
            var width = _root?.Bounds.Width > 0 ? _root.Bounds.Width : 320;
            Dismiss(ToastCloseReason.Swiped, direction * (width + 48), velocity);
            return;
        }
        // A short swipe springs back with its release velocity and a slight settle instead of drifting home.
        _ = _x.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy), velocity);
    }

    private bool IsInsideButton(object? source)
    {
        for (var visual = source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
        {
            if (visual is AvaloniaButton)
            {
                return true;
            }
        }
        return false;
    }

    #endregion
}
