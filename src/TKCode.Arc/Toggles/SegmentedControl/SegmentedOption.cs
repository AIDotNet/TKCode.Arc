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

    /// <summary>Whether the segment can be chosen. A disabled segment is dimmed and skipped by the arrow keys.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<SegmentedOption, bool>(nameof(IsEnabled), true);

    /// <summary>
    /// Whether the segment is shown. A hidden segment takes no room and is skipped by the arrow keys; it stays in the
    /// control's items, so when it is the selected one the selection is kept and only the indicator is not drawn.
    /// </summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<SegmentedOption, bool>(nameof(IsVisible), true);

    /// <summary>The former name of <see cref="IsEnabledProperty"/>, inverted; it stays in step with it.</summary>
    [Obsolete("Use IsEnabled instead (deprecated in 1.1.0). IsDisabled is the inverse of IsEnabled.")]
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

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="IsVisibleProperty"/>
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }

    /// <inheritdoc cref="IsDisabledProperty"/>
    [Obsolete("Use IsEnabled instead (deprecated in 1.1.0). IsDisabled is the inverse of IsEnabled.")]
    public bool IsDisabled { get => GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
#pragma warning disable CS0618 // the alias is kept in step with its replacement
        TogglesOptionState.KeepDisabledAliasInStep(this, change, IsEnabledProperty, IsDisabledProperty);
#pragma warning restore CS0618
    }

    object? ITogglesValueSource.ToggleValue => Value;

    public override string ToString() => Label ?? Value?.ToString() ?? string.Empty;
}
