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
/// and names the state under the number, so the state never rests on color alone. Its properties can be bound in XAML
/// (for example a localized label); the gauge follows later changes.
/// </summary>
public sealed class GaugeThreshold : AvaloniaObject
{
    /// <summary>Lowest value of the band, in the gauge's value units.</summary>
    public static readonly StyledProperty<double> FromProperty =
        AvaloniaProperty.Register<GaugeThreshold, double>(nameof(From));

    /// <summary>Ring and label color while the value is in this band.</summary>
    public static readonly StyledProperty<GaugeTone> ToneProperty =
        AvaloniaProperty.Register<GaugeThreshold, GaugeTone>(nameof(Tone));

    /// <summary>State name shown under the number, such as "Healthy".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<GaugeThreshold, string>(nameof(Label), string.Empty);

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

    /// <inheritdoc cref="FromProperty"/>
    public double From { get => GetValue(FromProperty); set => SetValue(FromProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public GaugeTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
}
