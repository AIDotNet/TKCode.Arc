using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// A mark on a <see cref="Slider"/> or <see cref="ElasticSlider"/> track. A mark without a label draws a tick; a mark
/// with a label also prints it under the track, and clicking the label moves there. On an elastic slider marks are
/// detents the dragged thumb clicks into.
/// </summary>
public class SliderMark
{
    public SliderMark()
    {
    }

    public SliderMark(double value, string? label = null)
    {
        Value = value;
        Label = label;
    }

    public double Value { get; set; }

    public string? Label { get; set; }

    /// <summary>A bare number is a tick without a label (Arc accepts <c>number | SliderMark</c>).</summary>
    public static implicit operator SliderMark(double value) => new(value);

    public override string ToString() => Label ?? Value.ToString(CultureInfo.InvariantCulture);
}
