using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// One option of a <see cref="SegmentedControl"/> (Arc <c>Segment</c>): a short label and optional accessory. Its
/// properties can be bound in XAML (for example a localized label); the control follows later changes.
/// </summary>
public class SegmentedOption : AvaloniaObject, ITogglesValueSource
{
    /// <summary>The value reported as the control's <c>SelectedValue</c>.</summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<SegmentedOption, object?>(nameof(Value));

    /// <summary>Segment text; falls back to <see cref="Value"/>. Keep it short: segments never wrap or shrink.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SegmentedOption, string?>(nameof(Label));

    /// <summary>Optional content after the label, such as a badge or a count.</summary>
    public static readonly StyledProperty<object?> AccessoryProperty =
        AvaloniaProperty.Register<SegmentedOption, object?>(nameof(Accessory));

    /// <summary>A disabled segment cannot be selected and is skipped by the arrow keys.</summary>
    public static readonly StyledProperty<bool> IsDisabledProperty =
        AvaloniaProperty.Register<SegmentedOption, bool>(nameof(IsDisabled));

    public SegmentedOption()
    {
    }

    public SegmentedOption(object? value, string? label, object? accessory = null)
    {
        Value = value;
        Label = label;
        Accessory = accessory;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="AccessoryProperty"/>
    public object? Accessory { get => GetValue(AccessoryProperty); set => SetValue(AccessoryProperty, value); }

    /// <inheritdoc cref="IsDisabledProperty"/>
    public bool IsDisabled { get => GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }

    object? ITogglesValueSource.ToggleValue => Value;

    public override string ToString() => Label ?? Value?.ToString() ?? string.Empty;
}
