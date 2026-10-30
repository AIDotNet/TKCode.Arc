using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// One option of <see cref="RadioCards"/> (Arc <c>RadioCardOption</c>): a label with optional description, a meta value
/// (price, estimate), a decorative icon, and a reason shown in place of the description when the option is disabled.
/// Its properties can be bound in XAML; the cards follow later changes.
/// </summary>
public class RadioCardOption : AvaloniaObject, ITogglesValueSource
{
    /// <summary>The value reported as the group's <c>SelectedValue</c>.</summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<RadioCardOption, object?>(nameof(Value));

    /// <summary>Card title (text or any content); falls back to <see cref="Value"/>.</summary>
    public static readonly StyledProperty<object?> LabelProperty =
        AvaloniaProperty.Register<RadioCardOption, object?>(nameof(Label));

    /// <summary>One or two short lines under the label.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RadioCardOption, string?>(nameof(Description));

    /// <summary>A price, estimate or other value: under the text in a grid card, at the end of a list row.</summary>
    public static readonly StyledProperty<object?> MetaProperty =
        AvaloniaProperty.Register<RadioCardOption, object?>(nameof(Meta));

    /// <summary>Decorative 18px icon beside the label: a Lucide <see cref="Avalonia.Media.Geometry"/> (for example
    /// <c>ArcIcons.Zap</c>) or any content.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RadioCardOption, object?>(nameof(Icon));

    /// <summary>A disabled card cannot be selected and is skipped by the arrow keys.</summary>
    public static readonly StyledProperty<bool> IsDisabledProperty =
        AvaloniaProperty.Register<RadioCardOption, bool>(nameof(IsDisabled));

    /// <summary>Short reason shown in place of the description when the option is disabled.</summary>
    public static readonly StyledProperty<string?> DisabledReasonProperty =
        AvaloniaProperty.Register<RadioCardOption, string?>(nameof(DisabledReason));

    public RadioCardOption()
    {
    }

    public RadioCardOption(object? value, object? label, string? description = null, object? meta = null)
    {
        Value = value;
        Label = label;
        Description = description;
        Meta = meta;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="MetaProperty"/>
    public object? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="IsDisabledProperty"/>
    public bool IsDisabled { get => GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }

    /// <inheritdoc cref="DisabledReasonProperty"/>
    public string? DisabledReason { get => GetValue(DisabledReasonProperty); set => SetValue(DisabledReasonProperty, value); }

    object? ITogglesValueSource.ToggleValue => Value;

    public override string ToString() => Label?.ToString() ?? Value?.ToString() ?? string.Empty;
}
