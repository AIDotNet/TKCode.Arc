using Avalonia;

namespace TKCode.Arc;

/// <summary>Color family of a <see cref="Gauge"/> ring and its state label.</summary>
public enum GaugeTone
{
    /// <summary>The accent (ring <c>Accent</c>, label <c>AccentStrong</c>).</summary>
    Accent,

    /// <summary>The success color.</summary>
    Success,

    /// <summary>The warning color.</summary>
    Warning,

    /// <summary>The danger color.</summary>
    Danger
}

/// <summary>
/// A band that starts at <see cref="From"/> (in value units). The highest band the value reaches sets the ring's tone
/// and names the state under the number, so the state never rests on color alone.
/// </summary>
public sealed class GaugeThreshold
{
    /// <summary>Creates an empty band (for XAML).</summary>
    public GaugeThreshold()
    {
    }

    /// <summary>Creates a band.</summary>
    public GaugeThreshold(double from, GaugeTone tone, string label)
    {
        From = from;
        Tone = tone;
        Label = label;
    }

    /// <summary>Lowest value of the band, in the gauge's value units.</summary>
    public double From { get; set; }

    /// <summary>Ring and label color while the value is in this band.</summary>
    public GaugeTone Tone { get; set; }

    /// <summary>State name shown under the number, such as "Healthy".</summary>
    public string Label { get; set; } = string.Empty;
}
