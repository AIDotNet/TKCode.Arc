using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>One series of a <see cref="ChartData"/> snapshot.</summary>
internal sealed class ChartSeriesData
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required double[] Values { get; init; }
    public Color? Color { get; init; }
    public bool IsDashed { get; init; }
    public bool? HasArea { get; init; }

    /// <summary>Position in the series list; drives the default palette color.</summary>
    public int Order { get; init; }
}

/// <summary>
/// Immutable snapshot of a chart's data, rebuilt once per data change from <c>ItemsSource</c>, the selectors or
/// bindings, and the <c>Series</c> collection. Everything a frame needs is precomputed here.
/// </summary>
internal sealed class ChartData
{
    public static readonly ChartData Empty = new()
    {
        Items = [], X = [], Keys = [], Labels = [], AxisLabels = [], PointColors = [], Series = []
    };

    public required object?[] Items { get; init; }
    public required object?[] X { get; init; }
    public required string[] Keys { get; init; }
    public required string[] Labels { get; init; }
    public required string?[] AxisLabels { get; init; }
    public required Color?[] PointColors { get; init; }
    public required ChartSeriesData[] Series { get; init; }

    public int Count => Keys.Length;

    public bool IsEmpty => Count == 0;

    public ChartSeriesData? FindSeries(string key)
    {
        foreach (var series in Series)
        {
            if (series.Key == key)
            {
                return series;
            }
        }
        return null;
    }

    /// <summary>True when keys and values match (no morph needed).</summary>
    public bool SameValues(ChartData other)
    {
        if (other.Count != Count || other.Series.Length != Series.Length)
        {
            return false;
        }
        for (var i = 0; i < Count; i++)
        {
            if (Keys[i] != other.Keys[i])
            {
                return false;
            }
        }
        for (var s = 0; s < Series.Length; s++)
        {
            if (Series[s].Key != other.Series[s].Key || !Series[s].Values.AsSpan().SequenceEqual(other.Series[s].Values))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>Builds <see cref="ChartData"/> without reflection: selectors, compiled bindings, or known item shapes.</summary>
internal static class ChartDataBuilder
{
    internal static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public static ChartData Build(ChartBase chart)
    {
        var items = Materialize(chart.ItemsSource);
        var series = chart.Series;
        using var reader = new ChartBindingReader();
        var count = items.Length;
        var seriesData = new List<ChartSeriesData>();
        if (series.Count > 0)
        {
            var order = 0;
            foreach (var s in series)
            {
                double[] values;
                if (s.Values is { } direct)
                {
                    values = direct.Select(Sanitize).ToArray();
                }
                else
                {
                    values = new double[items.Length];
                    for (var i = 0; i < items.Length; i++)
                    {
                        values[i] = ReadValue(items[i], s.ValueSelector, s.ValueBinding ?? chart.YBinding, chart.YSelector, reader);
                    }
                }
                count = Math.Max(count, values.Length);
                var key = s.EffectiveKey.Length > 0 ? s.EffectiveKey : $"series{order + 1}";
                seriesData.Add(new ChartSeriesData
                {
                    Key = key,
                    Name = s.Name ?? key,
                    Values = values,
                    Color = s.Color,
                    IsDashed = s.IsDashed,
                    HasArea = s.HasArea,
                    Order = order
                });
                order++;
            }
        }
        else if (items.Length > 0)
        {
            var values = new double[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                values[i] = ReadValue(items[i], null, chart.YBinding, chart.YSelector, reader);
            }
            seriesData.Add(new ChartSeriesData
            {
                Key = "value",
                Name = string.IsNullOrEmpty(chart.Label) ? "Value" : chart.Label,
                Values = values,
                Order = 0
            });
        }
        // Series shorter than the longest count their missing values as zero.
        for (var s = 0; s < seriesData.Count; s++)
        {
            if (seriesData[s].Values.Length < count)
            {
                var padded = new double[count];
                seriesData[s].Values.CopyTo(padded, 0);
                var old = seriesData[s];
                seriesData[s] = new ChartSeriesData
                {
                    Key = old.Key, Name = old.Name, Values = padded, Color = old.Color, IsDashed = old.IsDashed,
                    HasArea = old.HasArea, Order = old.Order
                };
            }
        }

        var x = new object?[count];
        var keys = new string[count];
        var labels = new string[count];
        var axis = new string?[count];
        var colors = new Color?[count];
        var xFormatter = chart.XLabelFormatter;
        var axisFormatter = chart.XAxisFormatter;
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var item = i < items.Length ? items[i] : null;
            var point = item as ChartPoint;
            object? xValue = point is not null ? point.X : i < items.Length ? ReadX(item, chart.XSelector, chart.XBinding, reader, i) : i;
            x[i] = xValue;
            labels[i] = point?.Label ?? xFormatter?.Invoke(xValue) ?? FullLabel(xValue, i);
            axis[i] = point?.AxisLabel is { } given ? given.Length == 0 ? null : given : axisFormatter is not null ? axisFormatter(xValue) : AxisLabel(xValue, i);
            colors[i] = point?.Color;
            var key = point?.Key ?? KeyOf(xValue, i);
            // Keys must be unique for morphing; repeated categories fall back to their position.
            keys[i] = used.Add(key) ? key : $"{key}#{i}";
        }
        return new ChartData
        {
            Items = items.Length == count ? items : PadItems(items, count),
            X = x,
            Keys = keys,
            Labels = labels,
            AxisLabels = axis,
            PointColors = colors,
            Series = seriesData.ToArray()
        };
    }

    private static object?[] PadItems(object?[] items, int count)
    {
        var padded = new object?[count];
        Array.Copy(items, padded, Math.Min(items.Length, count));
        return padded;
    }

    private static object?[] Materialize(IEnumerable? source)
    {
        if (source is null)
        {
            return [];
        }
        var list = new List<object?>();
        foreach (var item in source)
        {
            list.Add(item);
        }
        return list.ToArray();
    }

    private static double Sanitize(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;

    private static double ReadValue(object? item, Func<object, double>? seriesSelector, BindingBase? binding,
                                    Func<object, double>? chartSelector, ChartBindingReader reader)
    {
        if (item is null)
        {
            return 0;
        }
        if (seriesSelector is not null)
        {
            return Sanitize(seriesSelector(item));
        }
        if (binding is not null)
        {
            return Sanitize(ToDouble(reader.Read(item, binding)));
        }
        if (chartSelector is not null)
        {
            return Sanitize(chartSelector(item));
        }
        return Sanitize(item switch
        {
            ChartPoint point => point.Value,
            KeyValuePair<string, double> pair => pair.Value,
            ValueTuple<string, double> tuple => tuple.Item2,
            ValueTuple<DateTime, double> tuple => tuple.Item2,
            ValueTuple<DateTimeOffset, double> tuple => tuple.Item2,
            ValueTuple<DateOnly, double> tuple => tuple.Item2,
            _ => ToDouble(item)
        });
    }

    private static object? ReadX(object? item, Func<object, object?>? selector, BindingBase? binding, ChartBindingReader reader, int index)
    {
        if (item is null)
        {
            return index;
        }
        if (selector is not null)
        {
            return selector(item);
        }
        if (binding is not null)
        {
            return reader.Read(item, binding);
        }
        return item switch
        {
            KeyValuePair<string, double> pair => pair.Key,
            ValueTuple<string, double> tuple => tuple.Item1,
            ValueTuple<DateTime, double> tuple => tuple.Item1,
            ValueTuple<DateTimeOffset, double> tuple => tuple.Item1,
            ValueTuple<DateOnly, double> tuple => tuple.Item1,
            string or DateTime or DateTimeOffset or DateOnly => item,
            _ => index
        };
    }

    public static double ToDouble(object? value) => value switch
    {
        null => 0,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        short s => s,
        byte b => b,
        uint u => u,
        ulong ul => ul,
        string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        IConvertible convertible => SafeConvert(convertible),
        _ => 0
    };

    private static double SafeConvert(IConvertible convertible)
    {
        try
        {
            return convertible.ToDouble(CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Full label (tooltip, headline, screen readers): "Tue, Sep 15" for dates.</summary>
    public static string FullLabel(object? x, int index) => x switch
    {
        null => (index + 1).ToString(Culture),
        string text => text,
        DateTime date => date.ToString("ddd, MMM d", Culture),
        DateTimeOffset date => date.ToString("ddd, MMM d", Culture),
        DateOnly date => date.ToString("ddd, MMM d", Culture),
        IFormattable formattable => formattable.ToString(null, Culture),
        _ => x.ToString() ?? string.Empty
    };

    /// <summary>Short axis label: "Sep 15" for dates.</summary>
    public static string? AxisLabel(object? x, int index) => x switch
    {
        null => null,
        string text => text,
        DateTime date => date.ToString("MMM d", Culture),
        DateTimeOffset date => date.ToString("MMM d", Culture),
        DateOnly date => date.ToString("MMM d", Culture),
        int i when i == index => (index + 1).ToString(Culture),
        IFormattable formattable => formattable.ToString(null, Culture),
        _ => x.ToString()
    };

    private static string KeyOf(object? x, int index) => x switch
    {
        null => index.ToString(CultureInfo.InvariantCulture),
        string text => text,
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => x.ToString() ?? index.ToString(CultureInfo.InvariantCulture)
    };
}

/// <summary>
/// Evaluates a binding (typically a compiled binding) against an item without reflection: the item becomes the
/// data context of a detached element and the binding's current value is read once.
/// </summary>
internal sealed class ChartBindingReader : IDisposable
{
    private static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<ChartBindingHost, object?>("Value");

    private ChartBindingHost? _host;
    private BindingBase? _binding;
    private IDisposable? _subscription;

    public object? Read(object item, BindingBase binding)
    {
        _host ??= new ChartBindingHost();
        if (!ReferenceEquals(_binding, binding))
        {
            _subscription?.Dispose();
            _binding = binding;
            _subscription = _host.Bind(ValueProperty, binding);
        }
        _host.DataContext = item;
        return _host.GetValue(ValueProperty);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private sealed class ChartBindingHost : StyledElement
    {
    }
}
