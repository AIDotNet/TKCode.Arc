namespace TKCode.Arc;

/// <summary>One billing period of a <see cref="BillingToggle"/> (Arc <c>BillingToggleOption</c>).</summary>
public class BillingToggleOption : ITogglesValueSource
{
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

    /// <summary>The value reported as <see cref="BillingToggle.Value"/>.</summary>
    public string? Value { get; set; }

    public string? Label { get; set; }

    /// <summary>Short savings note, e.g. "Save 20%".</summary>
    public string? Badge { get; set; }

    /// <summary>Badge text once this option is selected, e.g. "You save $48". Defaults to <see cref="Badge"/>.</summary>
    public string? ActiveBadge { get; set; }

    object? ITogglesValueSource.ToggleValue => Value;

    public override string ToString() => Label ?? Value ?? string.Empty;
}
