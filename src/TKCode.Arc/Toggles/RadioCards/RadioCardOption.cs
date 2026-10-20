namespace TKCode.Arc;

/// <summary>
/// One option of <see cref="RadioCards"/> (Arc <c>RadioCardOption</c>): a label with optional description, a meta value
/// (price, estimate), a decorative icon, and a reason shown in place of the description when the option is disabled.
/// </summary>
public class RadioCardOption : ITogglesValueSource
{
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

    /// <summary>The value reported as the group's <c>SelectedValue</c>.</summary>
    public object? Value { get; set; }

    /// <summary>Card title (text or any content); falls back to <see cref="Value"/>.</summary>
    public object? Label { get; set; }

    /// <summary>One or two short lines under the label.</summary>
    public string? Description { get; set; }

    /// <summary>A price, estimate or other value: under the text in a grid card, at the end of a list row.</summary>
    public object? Meta { get; set; }

    /// <summary>Decorative 18px icon beside the label: a Lucide <see cref="Avalonia.Media.Geometry"/> (for example
    /// <c>ArcIcons.Zap</c>) or any content.</summary>
    public object? Icon { get; set; }

    public bool IsDisabled { get; set; }

    /// <summary>Short reason shown in place of the description when the option is disabled.</summary>
    public string? DisabledReason { get; set; }

    object? ITogglesValueSource.ToggleValue => Value;

    public override string ToString() => Label?.ToString() ?? Value?.ToString() ?? string.Empty;
}
