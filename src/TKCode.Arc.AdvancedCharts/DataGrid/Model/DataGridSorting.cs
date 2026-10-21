using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Sorting of grid values. Pure functions, usable on any thread: numbers compare numerically, days by date, select
/// values by their option order, and text naturally ("Item 2" before "Item 10") ignoring case and accents. Empty values
/// always sink to the bottom, whichever the direction, and ties keep their original order.
/// </summary>
public static class DataGridSorting
{
    private const CompareOptions TextOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace |
                                               CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType |
                                               CompareOptions.NumericOrdering;

    /// <summary>True for null and empty strings.</summary>
    public static bool IsEmpty(object? value) => value is null || value is string { Length: 0 };

    /// <summary>
    /// Compares two non-empty normalized values in ascending order. <paramref name="optionOrder"/> ranks select values.
    /// </summary>
    public static int Compare(object? a, object? b, IReadOnlyDictionary<string, int>? optionOrder, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (optionOrder is not null && a is string sa && b is string sb &&
            optionOrder.TryGetValue(sa, out var ia) && optionOrder.TryGetValue(sb, out var ib))
        {
            return ia.CompareTo(ib);
        }
        switch (a)
        {
            case decimal da when b is decimal db:
                return da.CompareTo(db);
            case DateOnly ta when b is DateOnly tb:
                return ta.CompareTo(tb);
            case bool ba when b is bool bb:
                return ba.CompareTo(bb);
        }
        return culture.CompareInfo.Compare(TextOf(a), TextOf(b), TextOptions);
    }

    /// <summary>
    /// Sorts <paramref name="rows"/> by <paramref name="keys"/> (stable). <paramref name="valueOf"/> returns the
    /// normalized value of a row for a sort key's column; <paramref name="optionOrders"/> ranks select columns by key.
    /// </summary>
    public static List<T> Sort<T>(IEnumerable<T> rows, IReadOnlyList<DataGridSort> keys, Func<T, string, object?> valueOf,
                                  IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? optionOrders = null,
                                  CultureInfo? culture = null)
    {
        var list = rows.ToList();
        if (keys.Count == 0 || list.Count < 2)
        {
            return list;
        }
        var columns = new object?[keys.Count][];
        var orders = new IReadOnlyDictionary<string, int>?[keys.Count];
        for (var k = 0; k < keys.Count; k++)
        {
            var values = new object?[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                values[i] = valueOf(list[i], keys[k].Key);
            }
            columns[k] = values;
            orders[k] = optionOrders is not null && optionOrders.TryGetValue(keys[k].Key, out var order) ? order : null;
        }
        var indices = Enumerable.Range(0, list.Count).ToArray();
        SortIndices(indices, columns, keys, orders, culture ?? CultureInfo.CurrentCulture);
        return indices.Select(i => list[i]).ToList();
    }

    /// <summary>
    /// Sorts <paramref name="indices"/> (row numbers into the value arrays) in place. Ties keep ascending index order,
    /// which makes the sort stable when the indices start in source order.
    /// </summary>
    internal static void SortIndices(int[] indices, object?[][] values, IReadOnlyList<DataGridSort> keys,
                                     IReadOnlyDictionary<string, int>?[] optionOrders, CultureInfo culture)
    {
        if (keys.Count == 0 || indices.Length < 2)
        {
            return;
        }
        var compare = culture.CompareInfo;
        Array.Sort(indices, (x, y) =>
        {
            for (var k = 0; k < keys.Count; k++)
            {
                var column = values[k];
                var a = column[x];
                var b = column[y];
                var emptyA = IsEmpty(a);
                var emptyB = IsEmpty(b);
                if (emptyA || emptyB)
                {
                    if (emptyA && emptyB)
                    {
                        continue;
                    }
                    return emptyA ? 1 : -1;
                }
                int result;
                var order = optionOrders[k];
                if (order is not null && a is string sa && b is string sb &&
                    order.TryGetValue(sa, out var ia) && order.TryGetValue(sb, out var ib))
                {
                    result = ia.CompareTo(ib);
                }
                else if (a is decimal da && b is decimal db)
                {
                    result = da.CompareTo(db);
                }
                else if (a is DateOnly ta && b is DateOnly tb)
                {
                    result = ta.CompareTo(tb);
                }
                else
                {
                    result = compare.Compare(TextOf(a), TextOf(b), TextOptions);
                }
                if (result != 0)
                {
                    return keys[k].Direction == DataGridSortDirection.Ascending ? result : -result;
                }
            }
            return x.CompareTo(y);
        });
    }

    /// <summary>Ranks options by their position (first one wins for duplicates).</summary>
    public static IReadOnlyDictionary<string, int> OptionOrder(IEnumerable<string> options)
    {
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        var i = 0;
        foreach (var option in options)
        {
            order.TryAdd(option, i++);
        }
        return order;
    }

    internal static string TextOf(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}

/// <summary>The totals row and group subtotal math. Pure functions, usable on any thread.</summary>
public static class DataGridAggregates
{
    /// <summary>The aggregate a column type gets by default: Sum for numbers and currency, Average for percent, None otherwise.</summary>
    public static DataGridAggregate DefaultFor(DataGridColumnType type) => type switch
    {
        DataGridColumnType.Number or DataGridColumnType.Currency => DataGridAggregate.Sum,
        DataGridColumnType.Percent => DataGridAggregate.Average,
        _ => DataGridAggregate.None
    };

    /// <summary>
    /// Aggregates normalized values. Count counts non-empty values; the others use the decimal values only and give null
    /// when there are none (None always gives null).
    /// </summary>
    public static decimal? Compute(IEnumerable<object?> values, DataGridAggregate kind)
    {
        if (kind == DataGridAggregate.None)
        {
            return null;
        }
        var accumulator = new Accumulator();
        foreach (var value in values)
        {
            accumulator.Add(value);
        }
        return accumulator.Result(kind);
    }

    /// <summary>Running aggregate over values (no allocation per value).</summary>
    internal struct Accumulator
    {
        private decimal _sum;
        private decimal _min;
        private decimal _max;
        private int _numbers;
        private int _filled;
        private bool _overflow;

        public void Add(object? value)
        {
            if (DataGridSorting.IsEmpty(value))
            {
                return;
            }
            _filled++;
            if (value is not decimal d)
            {
                return;
            }
            if (_numbers == 0)
            {
                _min = d;
                _max = d;
            }
            else
            {
                if (d < _min)
                {
                    _min = d;
                }
                if (d > _max)
                {
                    _max = d;
                }
            }
            _numbers++;
            if (!_overflow)
            {
                try
                {
                    _sum += d;
                }
                catch (OverflowException)
                {
                    _overflow = true;
                }
            }
        }

        public readonly decimal? Result(DataGridAggregate kind) => kind switch
        {
            DataGridAggregate.Count => _filled,
            DataGridAggregate.None => null,
            _ when _numbers == 0 || _overflow && kind is DataGridAggregate.Sum or DataGridAggregate.Average => null,
            DataGridAggregate.Sum => _sum,
            DataGridAggregate.Average => _sum / _numbers,
            DataGridAggregate.Min => _min,
            DataGridAggregate.Max => _max,
            _ => null
        };

        public readonly decimal Sum => _sum;

        public readonly int Numbers => _numbers;

        public readonly int Filled => _filled;
    }
}

/// <summary>
/// Fill handle values. Pure functions, usable on any thread: an evenly spaced number run continues its series; in a
/// date column, days continue by days, or by months when they share a day of the month or are all month ends; anything
/// else repeats.
/// </summary>
public static class DataGridFillSeries
{
    /// <summary>
    /// The <paramref name="count"/> values that follow <paramref name="source"/> (<paramref name="forward"/>: down or
    /// right) or precede it (up or left, nearest first). <paramref name="type"/> enables the day and month series.
    /// </summary>
    public static IReadOnlyList<object?> Extend(IReadOnlyList<object?> source, int count, bool forward, DataGridColumnType? type = null)
    {
        var result = new object?[Math.Max(0, count)];
        if (count <= 0 || source.Count == 0)
        {
            return result;
        }
        if (type == DataGridColumnType.Date && source.Count > 1 && source.All(v => v is DateOnly) &&
            TryDateSeries(source.Cast<DateOnly>().ToArray(), count, forward, result))
        {
            return result;
        }
        if (source.Count > 1 && source.All(v => v is decimal))
        {
            var numbers = source.Cast<decimal>().ToArray();
            var step = numbers[1] - numbers[0];
            var even = true;
            for (var i = 2; i < numbers.Length && even; i++)
            {
                even = numbers[i] - numbers[i - 1] == step;
            }
            if (even)
            {
                try
                {
                    for (var i = 0; i < count; i++)
                    {
                        result[i] = forward ? numbers[^1] + step * (i + 1) : numbers[0] - step * (i + 1);
                    }
                    return result;
                }
                catch (OverflowException)
                {
                }
            }
        }
        for (var i = 0; i < count; i++)
        {
            result[i] = forward ? source[i % source.Count] : source[source.Count - 1 - i % source.Count];
        }
        return result;
    }

    private static bool TryDateSeries(DateOnly[] days, int count, bool forward, object?[] result)
    {
        var monthEnds = days.All(d => d.Day == DateTime.DaysInMonth(d.Year, d.Month));
        var byMonth = monthEnds || days.All(d => d.Day == days[0].Day);
        static int MonthIndex(DateOnly d) => d.Year * 12 + d.Month - 1;
        var step = byMonth ? MonthIndex(days[1]) - MonthIndex(days[0]) : days[1].DayNumber - days[0].DayNumber;
        if (step == 0)
        {
            return false;
        }
        for (var i = 2; i < days.Length; i++)
        {
            var delta = byMonth ? MonthIndex(days[i]) - MonthIndex(days[i - 1]) : days[i].DayNumber - days[i - 1].DayNumber;
            if (delta != step)
            {
                return false;
            }
        }
        var origin = forward ? days[^1] : days[0];
        var direction = forward ? 1 : -1;
        for (var i = 0; i < count; i++)
        {
            var offset = (long)direction * step * (i + 1);
            if (byMonth)
            {
                var index = MonthIndex(origin) + offset;
                var year = (int)Math.Floor(index / 12.0);
                var month = (int)(index - year * 12L) + 1;
                if (year is < 1 or > 9999)
                {
                    result[i] = null;
                    continue;
                }
                var day = monthEnds ? DateTime.DaysInMonth(year, month) : Math.Min(origin.Day, DateTime.DaysInMonth(year, month));
                result[i] = new DateOnly(year, month, day);
            }
            else
            {
                var number = origin.DayNumber + offset;
                result[i] = number >= DateOnly.MinValue.DayNumber && number <= DateOnly.MaxValue.DayNumber
                    ? DateOnly.FromDayNumber((int)number)
                    : null;
            }
        }
        return true;
    }
}
