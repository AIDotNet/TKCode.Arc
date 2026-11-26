using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Mobile.DesignTokens;

namespace TKCode.Arc.Mobile;

/// <summary>Event data of <see cref="TouchSlider.ValueChanged"/>.</summary>
public sealed class TouchSliderValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public TouchSliderValueChangedEventArgs(RoutedEvent routedEvent, double oldValue, double newValue)
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
/// A touch-first mobile slider with a 44 px vertical hit area, a 28 px circular thumb, an optional floating value
/// bubble above the thumb while dragging (<see cref="ShowValueBubble"/>) so a finger does not hide the readout, and
/// haptic cues (<see cref="Haptics"/>) on discrete step crossings and range bounds. No hover state.
/// </summary>
[TemplatePart("PART_Root", typeof(Control))]
[TemplatePart("PART_Track", typeof(Border))]
[TemplatePart("PART_Fill", typeof(Border))]
[TemplatePart("PART_Thumb", typeof(Border))]
[TemplatePart("PART_Bubble", typeof(Border))]
[PseudoClasses(":dragging")]
public class TouchSlider : TemplatedControl
{
    /// <summary>Current numeric value (two-way, clamped to <see cref="Minimum"/> .. <see cref="Maximum"/>): 0.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<TouchSlider, double>(nameof(Value), 0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay, coerce: CoerceValue);

    /// <summary>Lowest allowed value: 0.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<TouchSlider, double>(nameof(Minimum), 0);

    /// <summary>Highest allowed value: 100.</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<TouchSlider, double>(nameof(Maximum), 100);

    /// <summary>Discrete step increment (0 means continuous): 0.</summary>
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<TouchSlider, double>(nameof(Step), 0);

    /// <summary>Whether a floating readout bubble appears above the thumb while dragging: true.</summary>
    public static readonly StyledProperty<bool> ShowValueBubbleProperty =
        AvaloniaProperty.Register<TouchSlider, bool>(nameof(ShowValueBubble), true);

    /// <summary>Format string applied to <see cref="Value"/> for the floating bubble and assistive text: "0.##".</summary>
    public static readonly StyledProperty<string?> FormatStringProperty =
        AvaloniaProperty.Register<TouchSlider, string?>(nameof(FormatString), "0.##");

    /// <summary>Whether the person is currently dragging the thumb.</summary>
    public static readonly DirectProperty<TouchSlider, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<TouchSlider, bool>(nameof(IsDragging), s => s.IsDragging);

    /// <summary>Formatted readout of <see cref="Value"/>.</summary>
    public static readonly DirectProperty<TouchSlider, string> FormattedValueProperty =
        AvaloniaProperty.RegisterDirect<TouchSlider, string>(nameof(FormattedValue), s => s.FormattedValue);

    /// <summary>Raised whenever <see cref="Value"/> changes.</summary>
    public static readonly RoutedEvent<TouchSliderValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<TouchSlider, TouchSliderValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    private Control? _root;
    private Border? _track;
    private Border? _fill;
    private Border? _thumb;
    private Border? _bubble;
    private IPointer? _captured;
    private bool _isDragging;
    private bool _atBound;
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

    /// <inheritdoc cref="ShowValueBubbleProperty"/>
    public bool ShowValueBubble
    {
        get => GetValue(ShowValueBubbleProperty);
        set => SetValue(ShowValueBubbleProperty, value);
    }

    /// <inheritdoc cref="FormatStringProperty"/>
    public string? FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    /// <inheritdoc cref="IsDraggingProperty"/>
    public bool IsDragging => _isDragging;

    /// <inheritdoc cref="FormattedValueProperty"/>
    public string FormattedValue => _formattedValue;

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<TouchSliderValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(TouchSlider);

    private double ThumbSize =>
        this.TryFindResource(TouchSliderTokens.Key(TouchSliderTokenKind.ThumbSize), out var v) && v is double d && d > 0 ? d : 28;

    private static double CoerceValue(AvaloniaObject o, double value)
    {
        if (o is not TouchSlider slider || !double.IsFinite(value))
        {
            return 0;
        }
        var min = slider.Minimum;
        var max = Math.Max(min, slider.Maximum);
        var clamped = Math.Clamp(value, min, max);
        var step = Math.Abs(slider.Step);
        if (step > 0 && double.IsFinite(step))
        {
            var snapped = min + Math.Round((clamped - min) / step) * step;
            clamped = Math.Clamp(Math.Round(snapped, 10, MidpointRounding.AwayFromZero), min, max);
        }
        return clamped;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _root = e.NameScope.Find<Control>("PART_Root");
        _track = e.NameScope.Find<Border>("PART_Track");
        _fill = e.NameScope.Find<Border>("PART_Fill");
        _thumb = e.NameScope.Find<Border>("PART_Thumb");
        _bubble = e.NameScope.Find<Border>("PART_Bubble");
        if (_root is not null)
        {
            _root.PointerPressed += OnRootPointerPressed;
            _root.PointerMoved += OnRootPointerMoved;
            _root.PointerReleased += OnRootPointerReleased;
            _root.PointerCaptureLost += OnRootPointerCaptureLost;
        }
        UpdateFormatted();
        UpdateVisual();
    }

    private void Detach()
    {
        if (_root is not null)
        {
            _root.PointerPressed -= OnRootPointerPressed;
            _root.PointerMoved -= OnRootPointerMoved;
            _root.PointerReleased -= OnRootPointerReleased;
            _root.PointerCaptureLost -= OnRootPointerCaptureLost;
        }
        EndDrag();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            UpdateFormatted();
            UpdateVisual();
            var oldVal = change.GetOldValue<double>();
            var newVal = change.GetNewValue<double>();
            if (Math.Abs(oldVal - newVal) > 1e-12)
            {
                RaiseEvent(new TouchSliderValueChangedEventArgs(ValueChangedEvent, oldVal, newVal));
            }
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty || change.Property == StepProperty)
        {
            CoerceValue(ValueProperty);
            UpdateFormatted();
            UpdateVisual();
        }
        else if (change.Property == FormatStringProperty)
        {
            UpdateFormatted();
        }
        else if (change.Property == BoundsProperty || change.Property == ShowValueBubbleProperty)
        {
            UpdateVisual();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            EndDrag();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled)
        {
            return;
        }
        var min = Minimum;
        var max = Math.Max(min, Maximum);
        var span = max - min;
        var step = Step > 0 ? Step : (span > 0 ? span / 20.0 : 1);
        switch (e.Key)
        {
            case Key.Right:
            case Key.Up:
                MoveToValue(Value + step, interactive: true);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Down:
                MoveToValue(Value - step, interactive: true);
                e.Handled = true;
                break;
            case Key.Home:
                MoveToValue(min, interactive: true);
                e.Handled = true;
                break;
            case Key.End:
                MoveToValue(max, interactive: true);
                e.Handled = true;
                break;
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || _root is null)
        {
            return;
        }
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(_root).Properties.IsLeftButtonPressed)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        _captured = e.Pointer;
        e.Pointer.Capture(_root);
        SetDragging(true);
        _atBound = IsAtBound(Value);
        ApplyPointerPosition(e.GetPosition(_root).X);
        e.Handled = true;
    }

    private void OnRootPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || !ReferenceEquals(e.Pointer, _captured) || _root is null)
        {
            return;
        }
        ApplyPointerPosition(e.GetPosition(_root).X);
        e.Handled = true;
    }

    private void OnRootPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging || !ReferenceEquals(e.Pointer, _captured))
        {
            return;
        }
        if (_root is not null)
        {
            ApplyPointerPosition(e.GetPosition(_root).X);
        }
        EndDrag();
        e.Handled = true;
    }

    private void OnRootPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (ReferenceEquals(e.Pointer, _captured))
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        if (_captured is not null)
        {
            _captured.Capture(null);
            _captured = null;
        }
        SetDragging(false);
    }

    private void SetDragging(bool dragging)
    {
        if (_isDragging == dragging)
        {
            return;
        }
        SetAndRaise(IsDraggingProperty, ref _isDragging, dragging);
        PseudoClasses.Set(":dragging", dragging);
        UpdateVisual();
    }

    private void ApplyPointerPosition(double x)
    {
        if (_root is null)
        {
            return;
        }
        var thumb = ThumbSize;
        var usable = Math.Max(1, _root.Bounds.Width - thumb);
        var fraction = Math.Clamp((x - thumb / 2) / usable, 0, 1);
        var min = Minimum;
        var max = Math.Max(min, Maximum);
        MoveToValue(min + fraction * (max - min), interactive: true);
    }

    private bool IsAtBound(double value)
    {
        var min = Minimum;
        var max = Math.Max(min, Maximum);
        return Math.Abs(value - min) < 1e-9 || Math.Abs(value - max) < 1e-9;
    }

    private void MoveToValue(double candidate, bool interactive)
    {
        var previous = Value;
        var next = CoerceValue(this, candidate);
        if (Math.Abs(next - previous) < 1e-12)
        {
            return;
        }
        SetCurrentValue(ValueProperty, next);
        if (!interactive)
        {
            return;
        }
        var nowBound = IsAtBound(next);
        if (nowBound && !_atBound)
        {
            Haptics.Trigger(HapticKind.Light);
        }
        else if (Step > 0)
        {
            Haptics.Trigger(HapticKind.Selection);
        }
        _atBound = nowBound;
    }

    private void UpdateFormatted()
    {
        var formatted = string.IsNullOrEmpty(FormatString)
            ? Value.ToString(CultureInfo.CurrentCulture)
            : Value.ToString(FormatString, CultureInfo.CurrentCulture);
        SetAndRaise(FormattedValueProperty, ref _formattedValue, formatted);
        AutomationProperties.SetHelpText(this, formatted);
    }

    private void UpdateVisual()
    {
        if (_root is null)
        {
            return;
        }
        var width = _root.Bounds.Width;
        var height = _root.Bounds.Height;
        var thumb = ThumbSize;
        var min = Minimum;
        var max = Math.Max(min, Maximum);
        var fraction = max > min ? Math.Clamp((Value - min) / (max - min), 0, 1) : 0;
        var usable = Math.Max(0, width - thumb);
        var centerX = thumb / 2 + fraction * usable;

        if (_track is not null)
        {
            _track.Margin = new Thickness(thumb / 2, 0);
        }
        if (_fill is not null && _track is not null)
        {
            var trackWidth = Math.Max(0, width - thumb);
            _fill.Width = trackWidth * fraction;
        }
        if (_thumb is not null)
        {
            Canvas.SetLeft(_thumb, centerX - thumb / 2);
            Canvas.SetTop(_thumb, Math.Max(0, (height - thumb) / 2));
        }
        if (_bubble is not null)
        {
            var show = _isDragging && ShowValueBubble;
            _bubble.IsVisible = show;
            if (show)
            {
                _bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var bubbleWidth = _bubble.DesiredSize.Width;
                var bubbleHeight = _bubble.DesiredSize.Height;
                var left = Math.Clamp(centerX - bubbleWidth / 2, 0, Math.Max(0, width - bubbleWidth));
                Canvas.SetLeft(_bubble, left);
                Canvas.SetTop(_bubble, (height - thumb) / 2 - bubbleHeight - 8);
            }
        }
    }
}
