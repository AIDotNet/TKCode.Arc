using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>One slice of a <see cref="PrizeWheel"/>: its prize and its odds.</summary>
public class PrizeWheelSegment : AvaloniaObject
{
    /// <summary>The prize, written on the slice. Keep it short: a word or two.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PrizeWheelSegment, string?>(nameof(Label));

    /// <summary>The slice's odds relative to the others (1 by default; 0 never wins).</summary>
    public static readonly StyledProperty<double> WeightProperty =
        AvaloniaProperty.Register<PrizeWheelSegment, double>(nameof(Weight), 1);

    /// <summary>A fill of the slice's own; by default slices alternate between the wheel's two surfaces.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<PrizeWheelSegment, IBrush?>(nameof(Fill));

    /// <summary>The label color on a slice with its own <see cref="Fill"/>; the wheel's text color by default.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<PrizeWheelSegment, IBrush?>(nameof(Foreground));

    public PrizeWheelSegment()
    {
    }

    public PrizeWheelSegment(string label, double weight = 1)
    {
        Label = label;
        Weight = weight;
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="WeightProperty"/>
    public double Weight
    {
        get => GetValue(WeightProperty);
        set => SetValue(WeightProperty, value);
    }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    internal event EventHandler? Changed;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => Label ?? string.Empty;
}
