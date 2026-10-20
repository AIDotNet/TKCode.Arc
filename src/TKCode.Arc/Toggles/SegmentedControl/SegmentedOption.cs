namespace TKCode.Arc;

/// <summary>One option of a <see cref="SegmentedControl"/> (Arc <c>Segment</c>): a short label and optional accessory.</summary>
public class SegmentedOption : ITogglesValueSource
{
    public SegmentedOption()
    {
    }

    public SegmentedOption(object? value, string? label, object? accessory = null)
    {
        Value = value;
        Label = label;
        Accessory = accessory;
    }

    /// <summary>The value reported as the control's <c>SelectedValue</c>.</summary>
    public object? Value { get; set; }

    /// <summary>Segment text; falls back to <see cref="Value"/>. Keep it short: segments never wrap or shrink.</summary>
    public string? Label { get; set; }

    /// <summary>Optional content after the label, such as a badge or a count.</summary>
    public object? Accessory { get; set; }

    public bool IsDisabled { get; set; }

    object? ITogglesValueSource.ToggleValue => Value;

    public override string ToString() => Label ?? Value?.ToString() ?? string.Empty;
}
