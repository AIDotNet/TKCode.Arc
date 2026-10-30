using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One term of a <see cref="TrainingRun"/>'s stats or results: a muted label over the value (a rolling readout or text in
/// tabular figures) and an optional hint. The hint line is always reserved, so the live stats and the results are the
/// same height and the card does not change size when one replaces the other.
/// </summary>
internal sealed class TrainingRunStat : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TrainingRunStat, string?>(nameof(Label));

    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<TrainingRunStat, object?>(nameof(Value));

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<TrainingRunStat, string?>(nameof(Hint));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }

    /// <summary>The value as text (a readout's reading or the string).</summary>
    public string ValueText => Value switch
    {
        TrainingRunReadout readout => readout.Text,
        string text => text,
        _ => string.Empty,
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == ValueProperty || change.Property == HintProperty)
        {
            UpdateName();
        }
    }

    /// <summary>The term's accessible name: "Elapsed: 12m 4s" (the description list reads as label and value).</summary>
    internal void UpdateName()
    {
        var name = Label + ": " + ValueText;
        if (!string.IsNullOrEmpty(Hint))
        {
            name += ", " + Hint;
        }
        AutomationProperties.SetName(this, name);
    }
}
