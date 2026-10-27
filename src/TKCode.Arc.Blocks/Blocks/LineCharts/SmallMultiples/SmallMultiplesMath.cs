using System.Globalization;

namespace TKCode.Arc;

/// <summary>The derived numbers of one panel: values, growth against the comparison period, and both y domains.</summary>
internal sealed class SmallMultiplesRow
{
    public SmallMultiplesRow(SmallMultiplesSeries series, string key, string name, double[] values)
    {
        Series = series;
        Key = key;
        Name = name;
        Values = values;
    }

    public SmallMultiplesSeries Series { get; }

    public string Key { get; }

    public string Name { get; }

    /// <summary>One value per period (NaN where missing).</summary>
    public double[] Values { get; }

    /// <summary>The value of the last period (NaN when missing).</summary>
    public double Latest { get; set; } = double.NaN;

    /// <summary>Growth from the comparison period to the latest (0.199 is 19.9%); NaN when it cannot be measured.</summary>
    public double Growth { get; set; } = double.NaN;

    /// <summary>The index of the comparison period.</summary>
    public int CompareIndex { get; set; }

    /// <summary>|Growth| below the flat threshold.</summary>
    public bool IsFlat { get; set; }

    /// <summary>The independent y domain: the series' own range with 10% room above and below.</summary>
    public (double Min, double Max) Independent { get; set; }

    public double ValueAt(int index) => index >= 0 && index < Values.Length ? Values[index] : double.NaN;
}

/// <summary>The rules behind <see cref="SmallMultiples"/>: growth, flat trends, the two y scales and the panel orders.</summary>
internal static class SmallMultiplesMath
{
    /// <summary>Room above and below the series' own range on the independent scale (share of the range).</summary>
    public const double IndependentPadding = 0.1;

    /// <summary>Room above the largest value on the shared scale (share of it).</summary>
    public const double SharedHeadroom = 0.08;

    /// <summary>Containers at least this wide show four columns; narrower ones two.</summary>
    public const double FourColumnWidth = 640;

    /// <summary>Containers at least this wide get the tallest plots.</summary>
    public const double TallPlotWidth = 1120;

    /// <summary>Containers narrower than this move each panel's value under its name and narrow the tick gutter.</summary>
    public const double PhoneWidth = 480;

    public static int Columns(double width) => width >= FourColumnWidth ? 4 : 2;

    /// <summary>The plot height for a container width: 104 from 1120, 92 from 640, 76 below.</summary>
    public static double PlotHeight(double width) => width >= TallPlotWidth ? 104 : width >= FourColumnWidth ? 92 : 76;

    /// <summary>Builds the rows for <paramref name="series"/> over <paramref name="periodCount"/> periods.</summary>
    public static List<SmallMultiplesRow> BuildRows(IReadOnlyList<SmallMultiplesSeries> series, int periodCount, int growthLag,
        double flatThreshold)
    {
        var rows = new List<SmallMultiplesRow>(series.Count);
        foreach (var s in series)
        {
            var source = s.Values ?? [];
            var count = periodCount > 0 ? periodCount : source.Count;
            var values = new double[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = i < source.Count && double.IsFinite(source[i]) ? source[i] : double.NaN;
            }
            var row = new SmallMultiplesRow(s, s.EffectiveKey, s.Name ?? s.Key ?? "", values);
            Derive(row, growthLag, flatThreshold);
            rows.Add(row);
        }
        return rows;
    }

    public static void Derive(SmallMultiplesRow row, int growthLag, double flatThreshold)
    {
        var values = row.Values;
        var last = values.Length - 1;
        row.Latest = last >= 0 ? values[last] : double.NaN;
        row.CompareIndex = Math.Max(0, last - Math.Max(1, growthLag));
        var basis = row.ValueAt(row.CompareIndex);
        row.Growth = double.IsFinite(basis) && basis != 0 && double.IsFinite(row.Latest) && last > 0
            ? (row.Latest - basis) / Math.Abs(basis)
            : double.NaN;
        row.IsFlat = double.IsFinite(row.Growth) && Math.Abs(row.Growth) < flatThreshold;
        row.Independent = IndependentDomain(values);
    }

    /// <summary>The series' own range with <see cref="IndependentPadding"/> room; a flat line gets ±5% (or ±1 at zero).</summary>
    public static (double Min, double Max) IndependentDomain(IReadOnlyList<double> values)
    {
        var (min, max) = Range(values);
        if (!double.IsFinite(min))
        {
            return (0, 1);
        }
        var span = max - min;
        if (span <= 0)
        {
            var room = min == 0 ? 1 : Math.Abs(min) * 0.05;
            return (min - room, max + room);
        }
        return (min - span * IndependentPadding, max + span * IndependentPadding);
    }

    /// <summary>Zero to the largest value of every row, with <see cref="SharedHeadroom"/>; lower when values go negative.</summary>
    public static (double Min, double Max) SharedDomain(IReadOnlyList<SmallMultiplesRow> rows)
    {
        var min = 0.0;
        var max = 0.0;
        foreach (var row in rows)
        {
            var (lo, hi) = Range(row.Values);
            if (double.IsFinite(lo))
            {
                min = Math.Min(min, lo);
                max = Math.Max(max, hi);
            }
        }
        if (max <= min)
        {
            return (min, min + 1);
        }
        return (min < 0 ? min * (1 + SharedHeadroom) : 0, max * (1 + SharedHeadroom));
    }

    /// <summary>The sum of every row per period (missing values count as zero; NaN where every row is missing).</summary>
    public static double[] Totals(IReadOnlyList<SmallMultiplesRow> rows, int periodCount)
    {
        var totals = new double[periodCount];
        for (var i = 0; i < periodCount; i++)
        {
            var sum = 0.0;
            var any = false;
            foreach (var row in rows)
            {
                var v = row.ValueAt(i);
                if (double.IsFinite(v))
                {
                    sum += v;
                    any = true;
                }
            }
            totals[i] = any ? sum : double.NaN;
        }
        return totals;
    }

    public static (double Min, double Max) Range(IReadOnlyList<double> values)
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        foreach (var v in values)
        {
            if (!double.IsFinite(v))
            {
                continue;
            }
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        return double.IsFinite(min) ? (min, max) : (double.NaN, double.NaN);
    }

    /// <summary>The rows in <paramref name="sort"/> order (stable: ties keep the given order).</summary>
    public static List<SmallMultiplesRow> Sorted(IReadOnlyList<SmallMultiplesRow> rows, SmallMultiplesSort sort, CultureInfo culture)
    {
        var indexed = rows.Select((row, index) => (Row: row, Index: index)).ToList();
        Comparison<(SmallMultiplesRow Row, int Index)> comparison = sort switch
        {
            SmallMultiplesSort.Growth => (a, b) => Descending(a.Row.Growth, b.Row.Growth),
            SmallMultiplesSort.Name => (a, b) => string.Compare(a.Row.Name, b.Row.Name, culture, CompareOptions.IgnoreCase),
            _ => (a, b) => Descending(a.Row.Latest, b.Row.Latest),
        };
        indexed.Sort((a, b) =>
        {
            var c = comparison(a, b);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        });
        return indexed.Select(p => p.Row).ToList();
    }

    // Missing values sort last.
    private static int Descending(double a, double b)
    {
        var fa = double.IsFinite(a);
        var fb = double.IsFinite(b);
        if (!fa || !fb)
        {
            return fa == fb ? 0 : fa ? -1 : 1;
        }
        return b.CompareTo(a);
    }

    /// <summary>The linear interpolation of <paramref name="values"/> at a fractional index (NaN outside or over a gap).</summary>
    public static double Interpolate(IReadOnlyList<double> values, double index)
    {
        if (values.Count == 0 || !double.IsFinite(index))
        {
            return double.NaN;
        }
        index = Math.Clamp(index, 0, values.Count - 1);
        var i = (int)Math.Floor(index);
        if (i >= values.Count - 1)
        {
            return values[^1];
        }
        var t = index - i;
        var a = values[i];
        var b = values[i + 1];
        if (!double.IsFinite(a) || !double.IsFinite(b))
        {
            return t < 0.5 ? a : b;
        }
        return a + (b - a) * t;
    }
}
