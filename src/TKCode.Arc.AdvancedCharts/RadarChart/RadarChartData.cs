using Avalonia.Collections;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How the rings of a <see cref="RadarChart"/> are drawn.</summary>
public enum RadarGridShape
{
    /// <summary>Concentric circles.</summary>
    Circle,

    /// <summary>Concentric polygons with one corner on each axis.</summary>
    Polygon
}

/// <summary>One measure of a <see cref="RadarChart"/>; axes run clockwise from the top in the order given.</summary>
public sealed class RadarAxis
{
    /// <summary>Creates an empty axis (for XAML).</summary>
    public RadarAxis()
    {
    }

    /// <summary>Creates an axis.</summary>
    public RadarAxis(string key, string label, double? maximum = null)
    {
        Key = key;
        Label = label;
        Maximum = maximum;
    }

    /// <summary>Stable identity: series values, <see cref="RadarChart.ActiveAxis"/> and morphs refer to it.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Text at the end of the axis.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The value at the outer ring of this axis; when null the chart's <see cref="RadarChart.Maximum"/> applies.</summary>
    public double? Maximum { get; set; }
}

/// <summary>A value of a <see cref="RadarSeries"/> on one axis.</summary>
public sealed class RadarValue
{
    /// <summary>Creates an empty value (for XAML).</summary>
    public RadarValue()
    {
    }

    /// <summary>Creates a value.</summary>
    public RadarValue(string axis, double value)
    {
        Axis = axis;
        Value = value;
    }

    /// <summary>Key of the axis this value belongs to.</summary>
    public string Axis { get; set; } = string.Empty;

    /// <summary>The value; NaN reads as missing.</summary>
    public double Value { get; set; } = double.NaN;
}

/// <summary>One profile of a <see cref="RadarChart"/>: a closed shape with a value per axis.</summary>
public sealed class RadarSeries
{
    /// <summary>Creates an empty series (for XAML).</summary>
    public RadarSeries()
    {
    }

    /// <summary>Creates a series from values keyed by axis.</summary>
    public RadarSeries(string key, string label, IEnumerable<KeyValuePair<string, double>> values)
    {
        Key = key;
        Label = label;
        foreach (var (axis, value) in values)
        {
            Values.Add(new RadarValue(axis, value));
        }
    }

    /// <summary>Stable identity: a series that keeps its key morphs when its values change.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Name shown in the legend and the tooltip.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The values, keyed by axis. An axis without a value draws at the centre.</summary>
    [Content]
    public AvaloniaList<RadarValue> Values { get; } = new();

    /// <summary>The value on an axis, or NaN when it is missing.</summary>
    public double ValueOf(string axis)
    {
        foreach (var value in Values)
        {
            if (value.Axis == axis)
            {
                return value.Value;
            }
        }
        return double.NaN;
    }
}

/// <summary>Arguments of <see cref="RadarChart.ActiveAxisChanged"/>.</summary>
public sealed class RadarChartActiveAxisChangedEventArgs : EventArgs
{
    internal RadarChartActiveAxisChangedEventArgs(string? key, RadarAxis? axis)
    {
        Key = key;
        Axis = axis;
    }

    /// <summary>Key of the axis being read, or null when the reading was cleared.</summary>
    public string? Key { get; }

    /// <summary>The axis being read, or null.</summary>
    public RadarAxis? Axis { get; }
}
