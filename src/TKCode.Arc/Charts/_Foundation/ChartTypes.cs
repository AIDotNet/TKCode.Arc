using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a line runs between data points.</summary>
public enum ChartCurve
{
    /// <summary>A monotone cubic that never swings past the data (Arc's default).</summary>
    Smooth,

    /// <summary>Straight segments.</summary>
    Linear
}

/// <summary>The color of a single-series chart, such as a sparkline.</summary>
public enum ChartTone
{
    Accent,
    Success,
    Warning,
    Danger
}

/// <summary>
/// A ready-made chart item: an X value (a category string or a date), a value, and optional labels.
/// Charts read it without selectors: <c>ItemsSource = new[] { new ChartPoint("Mon", 42) }</c>.
/// </summary>
/// <param name="X">The category or date, such as "Mon" or a <see cref="DateTime"/>.</param>
/// <param name="Value">The value.</param>
public sealed record ChartPoint(object? X, double Value)
{
    /// <summary>Stable identity across data changes; defaults to the X value.</summary>
    public string? Key { get; init; }

    /// <summary>Full label for the tooltip, the headline and screen readers, such as "Tue, Sep 15".</summary>
    public string? Label { get; init; }

    /// <summary>Short label on the category axis, such as "Sep 15". An empty string keeps the axis quiet there.</summary>
    public string? AxisLabel { get; init; }

    /// <summary>Overrides the palette color of this item (DonutChart segments).</summary>
    public Color? Color { get; init; }
}

/// <summary>What a value formatter is formatting.</summary>
/// <param name="Value">The value.</param>
/// <param name="Index">Point index, or -1 for an aggregate (average, total, axis tick).</param>
/// <param name="SeriesKey">The series key, or null for single-series charts and ticks.</param>
/// <param name="SeriesName">The series name, or null.</param>
public readonly record struct ChartValueContext(double Value, int Index, string? SeriesKey, string? SeriesName);

/// <summary>One series' value at the active point.</summary>
/// <param name="Key">Series key.</param>
/// <param name="Name">Series name.</param>
/// <param name="Value">The value.</param>
public readonly record struct ChartSeriesValue(string Key, string Name, double Value);

/// <summary>Raised when the readout moves to another point (pointer or keyboard), and with a null index when it is released.</summary>
public sealed class ChartActiveIndexChangedEventArgs : RoutedEventArgs
{
    public ChartActiveIndexChangedEventArgs(RoutedEvent routedEvent, int? index, object? item, string? label,
                                            IReadOnlyList<ChartSeriesValue> values, string? seriesKey = null)
        : base(routedEvent)
    {
        Index = index;
        Item = item;
        Label = label;
        Values = values;
        SeriesKey = seriesKey;
    }

    /// <summary>The active point, or null when the readout was released.</summary>
    public int? Index { get; }

    /// <summary>The <c>ItemsSource</c> item at <see cref="Index"/>, if any.</summary>
    public object? Item { get; }

    /// <summary>The full label of the active point.</summary>
    public string? Label { get; }

    /// <summary>The visible series' values at the active point.</summary>
    public IReadOnlyList<ChartSeriesValue> Values { get; }

    /// <summary>The isolated series (Streamgraph layer), if any.</summary>
    public string? SeriesKey { get; }
}

/// <summary>Raised when the set of hidden series (or segments) changes from the legend.</summary>
public sealed class ChartHiddenSeriesChangedEventArgs : RoutedEventArgs
{
    public ChartHiddenSeriesChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<string> hidden) : base(routedEvent)
        => Hidden = hidden;

    /// <summary>Keys of the hidden series.</summary>
    public IReadOnlyList<string> Hidden { get; }
}
