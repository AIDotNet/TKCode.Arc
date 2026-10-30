using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// One option of a <see cref="RadioGroup"/> (Arc <c>{ value, label, description? }</c>). Use it in
/// <see cref="Avalonia.Controls.ItemsControl.ItemsSource"/> or inline in XAML; the group reports <see cref="Value"/>
/// as its <c>SelectedValue</c>. Its properties can be bound in XAML; the group follows later changes.
/// </summary>
public class RadioOption : AvaloniaObject, ITogglesValueSource
{
    /// <summary>The value reported as the group's <c>SelectedValue</c>.</summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<RadioOption, object?>(nameof(Value));

    /// <summary>Row title; falls back to <see cref="Value"/>.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RadioOption, string?>(nameof(Label));

    /// <summary>Secondary line under the label; wraps on narrow widths.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RadioOption, string?>(nameof(Description));

    /// <summary>A disabled option cannot be selected and is skipped by the arrow keys.</summary>
    public static readonly StyledProperty<bool> IsDisabledProperty =
        AvaloniaProperty.Register<RadioOption, bool>(nameof(IsDisabled));

    public RadioOption()
    {
    }

    public RadioOption(object? value, string? label, string? description = null)
    {
        Value = value;
        Label = label;
        Description = description;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsDisabledProperty"/>
    public bool IsDisabled { get => GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }

    object? ITogglesValueSource.ToggleValue => Value;

    public override string ToString() => Label ?? Value?.ToString() ?? string.Empty;
}
