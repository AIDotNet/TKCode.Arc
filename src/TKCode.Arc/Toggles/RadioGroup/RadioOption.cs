namespace TKCode.Arc;

/// <summary>
/// One option of a <see cref="RadioGroup"/> (Arc <c>{ value, label, description? }</c>). Use it in
/// <see cref="Avalonia.Controls.ItemsControl.ItemsSource"/> or inline in XAML; the group reports <see cref="Value"/>
/// as its <c>SelectedValue</c>.
/// </summary>
public class RadioOption : ITogglesValueSource
{
    public RadioOption()
    {
    }

    public RadioOption(object? value, string? label, string? description = null)
    {
        Value = value;
        Label = label;
        Description = description;
    }

    /// <summary>The value reported as the group's <c>SelectedValue</c>.</summary>
    public object? Value { get; set; }

    /// <summary>Row title; falls back to <see cref="Value"/>.</summary>
    public string? Label { get; set; }

    /// <summary>Secondary line under the label; wraps on narrow widths.</summary>
    public string? Description { get; set; }

    /// <summary>A disabled option cannot be selected and is skipped by the arrow keys.</summary>
    public bool IsDisabled { get; set; }

    object? ITogglesValueSource.ToggleValue => Value;

    public override string ToString() => Label ?? Value?.ToString() ?? string.Empty;
}
