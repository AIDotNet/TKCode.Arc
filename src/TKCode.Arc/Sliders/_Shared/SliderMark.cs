using System.Globalization;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// A mark on a <see cref="Slider"/> or <see cref="ElasticSlider"/> track. A mark without a label draws a tick; a mark
/// with a label also prints it under the track, and clicking the label moves there. On an elastic slider marks are
/// detents the dragged thumb clicks into. Its properties can be bound in XAML; the slider follows later changes.
/// </summary>
public class SliderMark : AvaloniaObject
{
    /// <summary>Where the mark sits, in the slider's value units.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SliderMark, double>(nameof(Value));

    /// <summary>Text printed under the track; without it the mark is a bare tick.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SliderMark, string?>(nameof(Label));

    public SliderMark()
    {
    }

    public SliderMark(double value, string? label = null)
    {
        Value = value;
        Label = label;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>A bare number is a tick without a label (Arc accepts <c>number | SliderMark</c>).</summary>
    public static implicit operator SliderMark(double value) => new(value);

    public override string ToString() => Label ?? Value.ToString(CultureInfo.InvariantCulture);
}
