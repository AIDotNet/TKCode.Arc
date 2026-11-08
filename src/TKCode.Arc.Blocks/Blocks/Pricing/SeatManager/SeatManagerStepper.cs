using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A minus or plus button of the <see cref="SeatManager"/> stepper (Arc Seat manager, design reference; notes 2, 2b.4):
/// a 32px ghost circle that steps on pointer press (not on click), and after 380 ms of holding repeats every 70 ms until
/// the pointer is released or capture is lost. Enter steps on key down, Space on key up. At a limit it stays focusable and
/// pressable at half opacity (<c>:soft-disabled</c>; the block explains the limit); while the block is in its confirm step
/// it is <c>:locked</c> and ignores presses.
/// </summary>
[PseudoClasses(SoftDisabledPseudoClass, LockedPseudoClass)]
internal sealed class SeatManagerStepButton : TemplatedControl
{
    public const string SoftDisabledPseudoClass = ":soft-disabled";
    public const string LockedPseudoClass = ":locked";

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<SeatManagerStepButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<bool> IsSoftDisabledProperty =
        AvaloniaProperty.Register<SeatManagerStepButton, bool>(nameof(IsSoftDisabled));

    public static readonly StyledProperty<bool> IsLockedProperty =
        AvaloniaProperty.Register<SeatManagerStepButton, bool>(nameof(IsLocked));

    /// <summary>Hold before the first repeat (Arc: 380 ms).</summary>
    internal static readonly TimeSpan HoldDelay = TimeSpan.FromMilliseconds(380);

    /// <summary>Time between repeats while held (Arc: 70 ms).</summary>
    internal static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(70);

    private CancellationTokenSource? _hold;
    private bool _spaceDown;

    static SeatManagerStepButton()
    {
        FocusableProperty.OverrideDefaultValue<SeatManagerStepButton>(true);
    }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public bool IsSoftDisabled { get => GetValue(IsSoftDisabledProperty); set => SetValue(IsSoftDisabledProperty, value); }
    public bool IsLocked { get => GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }

    /// <summary>Raised for every step (the press, then each repeat).</summary>
    public event Action? Stepped;

    /// <summary>Waits between repeats; the block hands in its own (tests step it).</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (t, c) => Task.Delay(t, c);

    /// <summary>True while a press is held (tests).</summary>
    internal bool IsHolding => _hold is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSoftDisabledProperty)
        {
            PseudoClasses.Set(SoftDisabledPseudoClass, IsSoftDisabled);
        }
        else if (change.Property == IsLockedProperty)
        {
            PseudoClasses.Set(LockedPseudoClass, IsLocked);
            if (IsLocked)
            {
                StopHold();
            }
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            StopHold();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || IsLocked)
        {
            return;
        }
        e.Handled = true;
        e.Pointer.Capture(this);
        StartHold();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        StopHold();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        StopHold();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopHold();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (!IsLocked)
            {
                Stepped?.Invoke();
            }
        }
        else if (e.Key == Key.Space)
        {
            e.Handled = true;
            _spaceDown = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space && _spaceDown)
        {
            _spaceDown = false;
            e.Handled = true;
            if (!IsLocked)
            {
                Stepped?.Invoke();
            }
        }
    }

    private async void StartHold()
    {
        StopHold();
        var hold = new CancellationTokenSource();
        _hold = hold;
        Stepped?.Invoke();
        try
        {
            await Delay(HoldDelay, hold.Token);
            while (!hold.IsCancellationRequested)
            {
                Stepped?.Invoke();
                await Delay(RepeatInterval, hold.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Released.
        }
    }

    private void StopHold()
    {
        if (_hold is null)
        {
            return;
        }
        _hold.Cancel();
        _hold.Dispose();
        _hold = null;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SeatManagerStepButtonPeer(this);

    private sealed class SeatManagerStepButtonPeer(SeatManagerStepButton owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

        public void Invoke()
        {
            var button = (SeatManagerStepButton)Owner;
            if (!button.IsLocked)
            {
                button.Stepped?.Invoke();
            }
        }
    }
}

/// <summary>What a key on the seat count asks for.</summary>
internal enum SeatManagerSpinRequest
{
    Up,
    Down,
    PageUp,
    PageDown,
    Home,
    End
}

/// <summary>
/// The seat count in the middle of the <see cref="SeatManager"/> stepper: a focusable 44×32 spin button whose number
/// rolls. Arrow up / right add one, arrow down / left remove one, Page up / Page down move by five, Home and End jump to
/// the limits (the block clamps and explains). While the block confirms a change it leaves the tab order.
/// </summary>
internal sealed class SeatManagerSpinValue : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SeatManagerSpinValue, string?>(nameof(Text));

    static SeatManagerSpinValue()
    {
        FocusableProperty.OverrideDefaultValue<SeatManagerSpinValue>(true);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The range the screen reader is told about.</summary>
    internal (int Min, int Max, int Value) Range { get; set; }

    /// <summary>Raised for each recognised key.</summary>
    public event Action<SeatManagerSpinRequest>? Requested;

    /// <summary>Asked to set an exact value (screen readers).</summary>
    public event Action<int>? ValueRequested;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        SeatManagerSpinRequest? request = e.Key switch
        {
            Key.Up or Key.Right => SeatManagerSpinRequest.Up,
            Key.Down or Key.Left => SeatManagerSpinRequest.Down,
            Key.PageUp => SeatManagerSpinRequest.PageUp,
            Key.PageDown => SeatManagerSpinRequest.PageDown,
            Key.Home => SeatManagerSpinRequest.Home,
            Key.End => SeatManagerSpinRequest.End,
            _ => null
        };
        if (request is { } r)
        {
            e.Handled = true;
            Requested?.Invoke(r);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SeatManagerSpinValuePeer(this);

    private sealed class SeatManagerSpinValuePeer(SeatManagerSpinValue owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        private SeatManagerSpinValue Spin => (SeatManagerSpinValue)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

        public bool IsReadOnly => !Spin.IsEffectivelyEnabled;

        public double Minimum => Spin.Range.Min;

        public double Maximum => Spin.Range.Max;

        public double Value => Spin.Range.Value;

        public double LargeChange => 5;

        public double SmallChange => 1;

        public void SetValue(double value) => Spin.ValueRequested?.Invoke((int)Math.Round(value));
    }
}
