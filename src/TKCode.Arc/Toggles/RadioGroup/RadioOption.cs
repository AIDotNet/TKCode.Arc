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

    /// <summary>Whether the option can be chosen. A disabled option is dimmed and skipped by the arrow keys.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<RadioOption, bool>(nameof(IsEnabled), true);

    /// <summary>
    /// Whether the option is shown. A hidden option takes no room and is skipped by the arrow keys; it stays in the
    /// control's items, so when it is the selected one the selection is kept and only the indicator is not drawn.
    /// </summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<RadioOption, bool>(nameof(IsVisible), true);

    /// <summary>The former name of <see cref="IsEnabledProperty"/>, inverted; it stays in step with it.</summary>
    [Obsolete("Use IsEnabled instead (deprecated in 1.1.0). IsDisabled is the inverse of IsEnabled.")]
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
