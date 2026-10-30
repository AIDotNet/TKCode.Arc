using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// One billing period of a <see cref="BillingToggle"/> (Arc <c>BillingToggleOption</c>). Its properties can be bound in
/// XAML; the toggle follows later changes.
/// </summary>
public class BillingToggleOption : AvaloniaObject, ITogglesValueSource
{
    /// <summary>The value reported as <see cref="BillingToggle.Value"/>.</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<BillingToggleOption, string?>(nameof(Value));

    /// <summary>The option text, such as "Yearly"; falls back to <see cref="Value"/>.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<BillingToggleOption, string?>(nameof(Label));

    /// <summary>Short savings note, e.g. "Save 20%".</summary>
    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<BillingToggleOption, string?>(nameof(Badge));

    /// <summary>Badge text once this option is selected, e.g. "You save $48". Defaults to <see cref="Badge"/>.</summary>
    public static readonly StyledProperty<string?> ActiveBadgeProperty =
        AvaloniaProperty.Register<BillingToggleOption, string?>(nameof(ActiveBadge));

    public BillingToggleOption()
    {
    }

    public BillingToggleOption(string value, string label, string? badge = null, string? activeBadge = null)
    {
        Value = value;
        Label = label;
        Badge = badge;
        ActiveBadge = activeBadge;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="BadgeProperty"/>
    public string? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <inheritdoc cref="ActiveBadgeProperty"/>
    public string? ActiveBadge { get => GetValue(ActiveBadgeProperty); set => SetValue(ActiveBadgeProperty, value); }

    object? ITogglesValueSource.ToggleValue => Value;

    public override string ToString() => Label ?? Value ?? string.Empty;
}
