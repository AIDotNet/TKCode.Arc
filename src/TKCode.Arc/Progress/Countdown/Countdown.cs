using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Counts down to a fixed moment, or through a duration, with rolling digit wheels (Arc Countdown), and morphs into a
/// live label at zero. The clock ticks just after each whole second, only while the countdown is on screen; a separate
/// <see cref="PausableTimer"/> makes <see cref="Completed"/> land on time even while ticking is paused. A
/// <see cref="Duration"/> countdown can be paused and resumed (<see cref="IsPaused"/>, <see cref="Pause"/>,
/// <see cref="Resume"/>); a <see cref="Target"/> countdown counts to a fixed instant, so pausing only freezes the
/// display. Wheels turn the short way round; the shell springs to the size of whatever it holds.
/// </summary>
[TemplatePart(FacePart, typeof(CountdownFace))]
[PseudoClasses(CompletePseudoClass, CompactPseudoClass)]
public class Countdown : TemplatedControl
{
    public const string FacePart = "PART_Face";
    public const string CompletePseudoClass = ":complete";
    public const string CompactPseudoClass = ":compact";

    /// <summary>The moment to count down to. Takes precedence over <see cref="Duration"/>.</summary>
    public static readonly StyledProperty<DateTimeOffset?> TargetProperty =
        AvaloniaProperty.Register<Countdown, DateTimeOffset?>(nameof(Target));

    /// <summary>Counts down this long from when the countdown is shown (or restarted); pausable.</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<Countdown, TimeSpan?>(nameof(Duration));

    /// <summary>Units to show, largest first; the largest absorbs the rest. Defaults to days through seconds.</summary>
    public static readonly StyledProperty<CountdownUnits> UnitsProperty =
        AvaloniaProperty.Register<Countdown, CountdownUnits>(nameof(Units), CountdownUnits.All);

    /// <summary>Large display digits with unit names (default) or one compact pill.</summary>
    public static readonly StyledProperty<CountdownVariant> VariantProperty =
        AvaloniaProperty.Register<Countdown, CountdownVariant>(nameof(Variant));

    /// <summary>Shown once the countdown reaches zero, beside a pulsing live dot (Arc <c>completeLabel</c>, "Live now").</summary>
    public static readonly StyledProperty<object?> CompleteContentProperty =
        AvaloniaProperty.Register<Countdown, object?>(nameof(CompleteContent), "Live now");

    /// <summary>Accessible name (Arc <c>label</c>, "Time remaining").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Countdown, string>(nameof(Label), "Time remaining");

    /// <summary>Clock source; pass a server-synced provider when device time cannot be trusted.</summary>
    public static readonly StyledProperty<TimeProvider> TimeProviderProperty =
        AvaloniaProperty.Register<Countdown, TimeProvider>(nameof(TimeProvider), TimeProvider.System);

    /// <summary>Pauses a duration countdown (remaining time is kept); freezes the display of a target countdown.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<Countdown, bool>(nameof(IsPaused), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Time left, read from the clock on every tick.</summary>
    public static readonly DirectProperty<Countdown, TimeSpan> RemainingProperty =
        AvaloniaProperty.RegisterDirect<Countdown, TimeSpan>(nameof(Remaining), o => o.Remaining);

    /// <summary>True once the countdown has reached zero.</summary>
    public static readonly DirectProperty<Countdown, bool> IsCompletedProperty =
        AvaloniaProperty.RegisterDirect<Countdown, bool>(nameof(IsCompleted), o => o.IsCompleted);

    internal static readonly DirectProperty<Countdown, bool> IsRunningShownProperty =
        AvaloniaProperty.RegisterDirect<Countdown, bool>(nameof(IsRunningShown), o => o.IsRunningShown);

    internal static readonly StyledProperty<double> DigitSizeProperty =
        AvaloniaProperty.Register<Countdown, double>(nameof(DigitSize), 40);

    internal static readonly StyledProperty<double> DoneSizeProperty =
        AvaloniaProperty.Register<Countdown, double>(nameof(DoneSize), 36);

    internal static readonly StyledProperty<double> PulseSizeProperty =
        AvaloniaProperty.Register<Countdown, double>(nameof(PulseSize), 10);

    internal static readonly StyledProperty<double> DoneGapProperty =
        AvaloniaProperty.Register<Countdown, double>(nameof(DoneGap), 6);

    /// <summary>Raised once, the moment the countdown reaches zero while it is shown.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CompletedEvent =
        RoutedEvent.Register<Countdown, RoutedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    public DateTimeOffset? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public TimeSpan? Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public CountdownUnits Units { get => GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }
    public CountdownVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public object? CompleteContent { get => GetValue(CompleteContentProperty); set => SetValue(CompleteContentProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public TimeProvider TimeProvider { get => GetValue(TimeProviderProperty); set => SetValue(TimeProviderProperty, value); }
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }
    internal double DigitSize { get => GetValue(DigitSizeProperty); set => SetValue(DigitSizeProperty, value); }
    internal double DoneSize { get => GetValue(DoneSizeProperty); set => SetValue(DoneSizeProperty, value); }
    internal double PulseSize { get => GetValue(PulseSizeProperty); set => SetValue(PulseSizeProperty, value); }
    internal double DoneGap { get => GetValue(DoneGapProperty); set => SetValue(DoneGapProperty, value); }

    /// <summary>Fires once, the moment the countdown reaches zero while it is shown.</summary>
    public event EventHandler<RoutedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    private TimeSpan _remaining;

    /// <summary>Time left (zero once complete).</summary>
    public TimeSpan Remaining
    {
        get => _remaining;
        private set => SetAndRaise(RemainingProperty, ref _remaining, value);
    }

    private bool _isCompleted;

    public bool IsCompleted
    {
        get => _isCompleted;
        private set => SetAndRaise(IsCompletedProperty, ref _isCompleted, value);
    }

    private bool _isRunningShown = true;

    internal bool IsRunningShown
    {
        get => _isRunningShown;
        private set => SetAndRaise(IsRunningShownProperty, ref _isRunningShown, value);
    }

    private CountdownFace? _face;
    private PausableTimer? _completion;
    private DispatcherTimer? _tick;
    private ViewportVisibility? _visibility;
    private bool _inView = true;
    private bool _armed;
    private bool _hasTicked;
    private long _shownSeconds = -1;

    static Countdown()
    {
        FocusableProperty.OverrideDefaultValue<Countdown>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<Countdown>(AutomationControlType.Text);
        AutomationProperties.LiveSettingProperty.OverrideDefaultValue<Countdown>(AutomationLiveSetting.Off);
    }

    public Countdown()
    {
        AutomationProperties.SetName(this, Label);
    }

    /// <summary>
    /// Resolves a target string without reading the device's time zone: ISO text with <c>Z</c> or an offset is exact;
    /// wall time without an offset (such as "2026-10-06T10:00") is read in <paramref name="timeZone"/>, or UTC.
    /// </summary>
    public static DateTimeOffset ResolveTarget(string text, TimeZoneInfo? timeZone = null) => CountdownTime.Resolve(text, timeZone);

    /// <summary>Pauses a duration countdown (or freezes a target countdown's display).</summary>
    public void Pause() => IsPaused = true;

    public void Resume() => IsPaused = false;

    /// <summary>Starts a duration countdown again from its full <see cref="Duration"/>, or re-reads the target.</summary>
    public void Restart()
    {
        _completion?.Dispose();
        _completion = null;
        _armed = false;
        IsCompleted = false;
        IsRunningShown = true;
        PseudoClasses.Set(CompletePseudoClass, false);
        if (TopLevel.GetTopLevel(this) is not null)
        {
            Arm();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _face = e.NameScope.Find<CountdownFace>(FacePart);
        _shownSeconds = -1;
        UpdateDisplay(animate: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _visibility = new ViewportVisibility(this, 0.0001, once: false);
        _visibility.VisibilityChanged += (_, visible) =>
        {
            _inView = visible;
            ScheduleTick();
        };
        if (!_armed || !IsDurationMode)
        {
            Arm();
        }
        else if (!IsPaused)
        {
            _completion?.Resume();
        }
        // The first reading waits for the first layout, so the digits can roll in from zero (P18).
        Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(this) is not null)
            {
                _hasTicked = true;
                Tick();
            }
        }, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
        _tick?.Stop();
        // A duration keeps its remaining time while the countdown is away; a target keeps counting regardless.
        _completion?.Pause();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TargetProperty || change.Property == DurationProperty || change.Property == TimeProviderProperty)
        {
            Restart();
            Tick();
        }
        else if (change.Property == UnitsProperty || change.Property == VariantProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, Variant == CountdownVariant.Compact);
            _shownSeconds = -1;
            UpdateDisplay(animate: false);
            UpdateSizes();
        }
        else if (change.Property == IsPausedProperty)
        {
            OnPausedChanged();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == BoundsProperty || change.Property == FontSizeProperty)
        {
            UpdateSizes();
        }
        else if (change.Property == CompleteContentProperty)
        {
            UpdateSpoken();
        }
    }

    /// <summary>Large digits follow the container width: clamp(1.75rem, 12.5% of the width, text-5xl).</summary>
    internal static double LargeDigitSize(double width) => Math.Clamp(0.125 * width, 28, 72);

    /// <summary>The live label: clamp(1.75rem, 11% of the width, text-4xl).</summary>
    internal static double LargeDoneSize(double width) => Math.Clamp(0.11 * width, 28, 52);

    private void UpdateSizes()
    {
        if (Variant == CountdownVariant.Compact)
        {
            DigitSize = FontSize;
            DoneSize = FontSize;
        }
        else
        {
            var width = Bounds.Width > 0 ? Bounds.Width : 320;
            DigitSize = LargeDigitSize(width);
            DoneSize = LargeDoneSize(width);
        }
        PulseSize = Math.Max(8, 0.3 * DoneSize);
        DoneGap = 0.4 * DoneSize;
    }

    private bool IsDurationMode => Target is null && Duration is not null;

    private TimeSpan ComputeRemaining()
    {
        if (Target is { } target)
        {
            var left = target - TimeProvider.GetUtcNow();
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
        if (_completion is not null)
        {
            var left = _completion.Remaining;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
        return Duration is { } duration && duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    /// <summary>Arms the completion timer, so <see cref="Completed"/> lands on time even while ticking is paused.</summary>
    private void Arm()
    {
        _armed = true;
        _completion?.Dispose();
        _completion = null;
        if (Target is null && Duration is null)
        {
            return;
        }
        var left = IsDurationMode ? Duration!.Value : ComputeRemaining();
        if (left <= TimeSpan.Zero)
        {
            // Mounted after the moment passed: show the finished state without firing.
            SetCompleted(raise: false);
            return;
        }
        // A target is read just after the moment (8ms slack, like Arc); long targets re-arm in chunks below the
        // platform timer limit. A duration is the remaining time itself.
        var wait = TimeSpan.FromMilliseconds(Math.Min(left.TotalMilliseconds + (IsDurationMode ? 0 : 8), int.MaxValue));
        _completion = new PausableTimer(wait, OnCompletionTimer);
        if (IsPaused && IsDurationMode)
        {
            return;
        }
        _completion.Start();
    }

    private void OnCompletionTimer()
    {
        if (!IsDurationMode && ComputeRemaining() > TimeSpan.Zero)
        {
            Arm();
            return;
        }
        Tick();
    }

    private void OnPausedChanged()
    {
        if (IsDurationMode && _completion is not null)
        {
            if (IsPaused)
            {
                _completion.Pause();
            }
            else if (!IsCompleted)
            {
                _completion.Resume();
            }
        }
        Tick();
    }

    /// <summary>Reads the clock, updates the display, and schedules the next tick just after the next whole second.</summary>
    internal void Tick()
    {
        if (TopLevel.GetTopLevel(this) is null || !_hasTicked)
        {
            Remaining = ComputeRemaining();
            return;
        }
        if (IsPaused && !IsDurationMode && !IsCompleted)
        {
            // A paused target countdown keeps its frozen display; completion still fires on time.
            if (ComputeRemaining() > TimeSpan.Zero)
            {
                _tick?.Stop();
                return;
            }
        }
        var left = ComputeRemaining();
        Remaining = left;
        if (left <= TimeSpan.Zero && (Target is not null || Duration is not null))
        {
            SetCompleted(raise: _armed && !IsCompleted);
            return;
        }
        UpdateDisplay(animate: true);
        ScheduleTick();
    }

    private void ScheduleTick()
    {
        var run = TopLevel.GetTopLevel(this) is not null && _inView && !IsPaused && !IsCompleted &&
                  (Target is not null || Duration is not null);
        if (!run)
        {
            _tick?.Stop();
            return;
        }
        var ms = Remaining.TotalMilliseconds % 1000;
        var wait = TimeSpan.FromMilliseconds((ms < 1 ? 1000 : ms) + 8);
        _tick ??= new DispatcherTimer(DispatcherPriority.Normal);
        _tick.Stop();
        _tick.Tick -= OnTick;
        _tick.Tick += OnTick;
        _tick.Interval = wait;
        _tick.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _tick?.Stop();
        Tick();
    }

    private void SetCompleted(bool raise)
    {
        _tick?.Stop();
        Remaining = TimeSpan.Zero;
        if (IsCompleted)
        {
            return;
        }
        IsCompleted = true;
        IsRunningShown = false;
        PseudoClasses.Set(CompletePseudoClass, true);
        UpdateSpoken();
        if (raise)
        {
            RaiseEvent(new RoutedEventArgs(CompletedEvent));
        }
    }

    /// <summary>Whole seconds shown (Arc: ceil(left / 1000)).</summary>
    internal static long DisplaySeconds(TimeSpan left) => (long)Math.Ceiling(Math.Max(0, left.TotalMilliseconds) / 1000);

    private void UpdateDisplay(bool animate)
    {
        // Until the clock is first read the wheels rest at zero, dimmed (Arc's pending state).
        var seconds = IsCompleted || !_hasTicked ? 0 : DisplaySeconds(Remaining);
        _face?.SetPending(!_hasTicked && !IsCompleted, animate);
        if (seconds == _shownSeconds && _face is not null)
        {
            return;
        }
        _shownSeconds = seconds;
        _face?.SetParts(CountdownTime.Split(seconds, Units), animate);
        UpdateSpoken();
    }

    private void UpdateSpoken()
    {
        string spoken;
        if (IsCompleted)
        {
            spoken = CompleteContent as string ?? "Complete";
        }
        else
        {
            spoken = CountdownTime.Spoken(DisplaySeconds(Remaining), Units);
        }
        AutomationProperties.SetHelpText(this, spoken);
    }
}
