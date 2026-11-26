using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>Event data of <see cref="NumberStepper.ValueChanged"/>.</summary>
public sealed class NumberStepperValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public NumberStepperValueChangedEventArgs(RoutedEvent routedEvent, double oldValue, double newValue)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The value before the change.</summary>
    public double OldValue { get; }

    /// <summary>The value after the change.</summary>
    public double NewValue { get; }
}

/// <summary>
/// A touch-first mobile numeric stepper with 44 by 44 decrement and increment buttons around the current value.
/// Ideal for quantities, counts and compact settings inside <see cref="ListItem.Trailing"/>.
/// <para>
/// Holding either button repeats the step automatically and accelerates after a short hold; each step triggers a
/// selection haptic cue through <see cref="Haptics"/> (and a light cue on hitting a bound). Keyboard arrows, Home and
/// End also adjust the value. No hover state.
/// </para>
/// </summary>
[TemplatePart("PART_Decrement", typeof(AvaloniaButton))]
[TemplatePart("PART_Increment", typeof(AvaloniaButton))]
public class NumberStepper : TemplatedControl
{
    private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan FastInterval = TimeSpan.FromMilliseconds(60);
    private const int FastAfterSteps = 8;

    /// <summary>Current numeric value (two-way, clamped to <see cref="Minimum"/> .. <see cref="Maximum"/>): 0.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<NumberStepper, double>(nameof(Value), 0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay, coerce: CoerceValue);

    /// <summary>Lowest allowed value: 0.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<NumberStepper, double>(nameof(Minimum), 0);

    /// <summary>Highest allowed value: 100.</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<NumberStepper, double>(nameof(Maximum), 100);

    /// <summary>Amount added or subtracted per tap: 1.</summary>
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<NumberStepper, double>(nameof(Step), 1);

    /// <summary>Format string applied to <see cref="Value"/> for display: "0.##".</summary>
    public static readonly StyledProperty<string?> FormatStringProperty =
        AvaloniaProperty.Register<NumberStepper, string?>(nameof(FormatString), "0.##");

    /// <summary>Whether stepping past <see cref="Maximum"/> wraps to <see cref="Minimum"/> (and vice versa): false.</summary>
    public static readonly StyledProperty<bool> IsWrappingProperty =
        AvaloniaProperty.Register<NumberStepper, bool>(nameof(IsWrapping));

    /// <summary>The formatted text currently displayed between the two buttons.</summary>
    public static readonly DirectProperty<NumberStepper, string> FormattedValueProperty =
        AvaloniaProperty.RegisterDirect<NumberStepper, string>(nameof(FormattedValue), s => s.FormattedValue);

    /// <summary>Raised whenever <see cref="Value"/> changes.</summary>
    public static readonly RoutedEvent<NumberStepperValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<NumberStepper, NumberStepperValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    private AvaloniaButton? _decrement;
    private AvaloniaButton? _increment;
    private DispatcherTimer? _repeatTimer;
    private int _repeatDirection;
    private int _repeatCount;
    private string _formattedValue = "0";

    /// <inheritdoc cref="ValueProperty"/>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="MinimumProperty"/>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <inheritdoc cref="MaximumProperty"/>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <inheritdoc cref="StepProperty"/>
    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <inheritdoc cref="FormatStringProperty"/>
    public string? FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    /// <inheritdoc cref="IsWrappingProperty"/>
    public bool IsWrapping
    {
        get => GetValue(IsWrappingProperty);
        set => SetValue(IsWrappingProperty, value);
    }

    /// <inheritdoc cref="FormattedValueProperty"/>
    public string FormattedValue => _formattedValue;

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<NumberStepperValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(NumberStepper);

    /// <summary>Increases <see cref="Value"/> by <see cref="Step"/> (wrapping if <see cref="IsWrapping"/> is true).</summary>
    public void Increment() => StepBy(1);

    /// <summary>Decreases <see cref="Value"/> by <see cref="Step"/> (wrapping if <see cref="IsWrapping"/> is true).</summary>
    public void Decrement() => StepBy(-1);

    private static double CoerceValue(AvaloniaObject o, double value)
    {
        if (o is not NumberStepper stepper || !double.IsFinite(value))
        {
            return 0;
        }
        var min = stepper.Minimum;
        var max = Math.Max(min, stepper.Maximum);
        return Math.Clamp(value, min, max);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UnhookButton(_decrement, -1);
        UnhookButton(_increment, 1);
        _decrement = e.NameScope.Find<AvaloniaButton>("PART_Decrement");
        _increment = e.NameScope.Find<AvaloniaButton>("PART_Increment");
        HookButton(_decrement, -1);
        HookButton(_increment, 1);
        UpdateVisualState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopRepeat();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            UpdateVisualState();
            var oldVal = change.GetOldValue<double>();
            var newVal = change.GetNewValue<double>();
            if (Math.Abs(oldVal - newVal) > 1e-12)
            {
                RaiseEvent(new NumberStepperValueChangedEventArgs(ValueChangedEvent, oldVal, newVal));
            }
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            CoerceValue(ValueProperty);
            UpdateVisualState();
        }
        else if (change.Property == FormatStringProperty || change.Property == IsWrappingProperty)
        {
            UpdateVisualState();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            StopRepeat();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Up:
            case Key.Right:
                Increment();
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Left:
                Decrement();
                e.Handled = true;
                break;
            case Key.Home:
                SetCurrentValue(ValueProperty, Minimum);
                e.Handled = true;
                break;
            case Key.End:
                SetCurrentValue(ValueProperty, Math.Max(Minimum, Maximum));
                e.Handled = true;
                break;
        }
    }

    private void HookButton(AvaloniaButton? button, int direction)
    {
        if (button is null)
        {
            return;
        }
        if (direction < 0)
        {
            button.Click += OnDecrementClick;
            button.AddHandler(PointerPressedEvent, OnDecrementPressed, RoutingStrategies.Tunnel);
        }
        else
        {
            button.Click += OnIncrementClick;
            button.AddHandler(PointerPressedEvent, OnIncrementPressed, RoutingStrategies.Tunnel);
        }
        button.AddHandler(PointerReleasedEvent, OnButtonReleased, RoutingStrategies.Tunnel);
        button.AddHandler(PointerCaptureLostEvent, OnButtonCaptureLost, RoutingStrategies.Tunnel);
    }

    private void UnhookButton(AvaloniaButton? button, int direction)
    {
        if (button is null)
        {
            return;
        }
        if (direction < 0)
        {
            button.Click -= OnDecrementClick;
            button.RemoveHandler(PointerPressedEvent, OnDecrementPressed);
        }
        else
        {
            button.Click -= OnIncrementClick;
            button.RemoveHandler(PointerPressedEvent, OnIncrementPressed);
        }
        button.RemoveHandler(PointerReleasedEvent, OnButtonReleased);
        button.RemoveHandler(PointerCaptureLostEvent, OnButtonCaptureLost);
    }

    private void OnDecrementClick(object? sender, RoutedEventArgs e)
    {
        if (_repeatCount == 0)
        {
            StepBy(-1);
        }
        StopRepeat();
    }

    private void OnIncrementClick(object? sender, RoutedEventArgs e)
    {
        if (_repeatCount == 0)
        {
            StepBy(1);
        }
        StopRepeat();
    }

    private void OnDecrementPressed(object? sender, PointerPressedEventArgs e) => StartRepeat(-1);

    private void OnIncrementPressed(object? sender, PointerPressedEventArgs e) => StartRepeat(1);

    private void OnButtonReleased(object? sender, PointerReleasedEventArgs e) => StopRepeat();

    private void OnButtonCaptureLost(object? sender, PointerCaptureLostEventArgs e) => StopRepeat();

    private void StartRepeat(int direction)
    {
        StopRepeat();
        _repeatDirection = direction;
        _repeatCount = 0;
        _repeatTimer = new DispatcherTimer { Interval = RepeatDelay };
        _repeatTimer.Tick += OnRepeatTick;
        _repeatTimer.Start();
    }

    private void OnRepeatTick(object? sender, EventArgs e)
    {
        if (!IsEffectivelyEnabled || _repeatTimer is null)
        {
            StopRepeat();
            return;
        }
        _repeatCount++;
        _repeatTimer.Interval = _repeatCount >= FastAfterSteps ? FastInterval : RepeatInterval;
        if (!StepBy(_repeatDirection))
        {
            StopRepeat();
        }
    }

    private void StopRepeat()
    {
        if (_repeatTimer is not null)
        {
            _repeatTimer.Stop();
            _repeatTimer.Tick -= OnRepeatTick;
            _repeatTimer = null;
        }
        _repeatDirection = 0;
        _repeatCount = 0;
    }

    private bool StepBy(int direction)
    {
        var step = Math.Abs(Step);
        if (step <= 0 || !double.IsFinite(step))
        {
            return false;
        }
        var min = Minimum;
        var max = Math.Max(min, Maximum);
        var current = Value;
        var next = current + direction * step;

        // Avoid floating-point drift such as 0.1 + 0.2 = 0.30000000000000004.
        next = Math.Round(next, 10, MidpointRounding.AwayFromZero);

        if (next > max)
        {
            next = IsWrapping ? min : max;
        }
        else if (next < min)
        {
            next = IsWrapping ? max : min;
        }

        if (Math.Abs(next - current) < 1e-12)
        {
            Haptics.Trigger(HapticKind.Light);
            return false;
        }

        SetCurrentValue(ValueProperty, next);
        Haptics.Trigger(HapticKind.Selection);
        return true;
    }

    private void UpdateVisualState()
    {
        var min = Minimum;
        var max = Math.Max(min, Maximum);
        var value = Value;
        var formatted = string.IsNullOrEmpty(FormatString)
            ? value.ToString(CultureInfo.CurrentCulture)
            : value.ToString(FormatString, CultureInfo.CurrentCulture);
        SetAndRaise(FormattedValueProperty, ref _formattedValue, formatted);
        AutomationProperties.SetHelpText(this, formatted);

        if (_decrement is not null)
        {
            _decrement.IsEnabled = IsWrapping || value > min + 1e-12;
        }
        if (_increment is not null)
        {
            _increment.IsEnabled = IsWrapping || value < max - 1e-12;
        }
    }
}
