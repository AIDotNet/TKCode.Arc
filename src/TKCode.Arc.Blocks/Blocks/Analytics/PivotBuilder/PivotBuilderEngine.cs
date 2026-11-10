using System.Collections;
using System.Globalization;

namespace TKCode.Arc;

/// <summary>Computes a pivot from records, fields and a layout. Pure: no UI.</summary>
internal static class PivotBuilderEngine
{
    private struct Acc
    {
        public double Sum;
        public double Min;
        public double Max;
        public int Count;
        public int Numbers;

        public void Add(object? raw)
        {
            Count++;
            if (TryNumber(raw, out var v))
            {
                if (Numbers == 0)
                {
                    Min = v;
                    Max = v;
                }
                Sum += v;
                Min = Math.Min(Min, v);
                Max = Math.Max(Max, v);
                Numbers++;
            }
        }

        public readonly double? Result(PivotBuilderAggregation aggregation) => Count == 0
            ? null
            : aggregation switch
            {
                PivotBuilderAggregation.Count => Count,
                PivotBuilderAggregation.Sum => Numbers == 0 ? null : Sum,
                PivotBuilderAggregation.Average => Numbers == 0 ? null : Sum / Numbers,
                PivotBuilderAggregation.Min => Numbers == 0 ? null : Min,
                PivotBuilderAggregation.Max => Numbers == 0 ? null : Max,
                _ => null,
            };
    }

    public static bool TryNumber(object? raw, out double value)
    {
        switch (raw)
        {
            case double d:
                value = d;
                return true;
            case float f:
                value = f;
                return true;
            case decimal m:
                value = (double)m;
                return true;
            case int i:
                value = i;
                return true;
            case long l:
                value = l;
                return true;
            case IConvertible c when raw is not string && raw is not bool:
                try
                {
                    value = c.ToDouble(CultureInfo.InvariantCulture);
                    return true;
                }
                catch (FormatException)
                {
                    break;
                }
                catch (InvalidCastException)
                {
                    break;
                }
        }
        value = 0;
        return false;
    }

    private static string KeyText(object? raw) => raw switch
    {
        null => "—",
        IFormattable f => f.ToString(null, CultureInfo.CurrentCulture),
        _ => raw.ToString() ?? "—",
    };

    private sealed class KeyComparer : IComparer<string[]>, IEqualityComparer<string[]>
    {
        public static readonly KeyComparer Instance = new();

        public int Compare(string[]? x, string[]? y)
        {
            for (var i = 0; i < Math.Min(x!.Length, y!.Length); i++)
            {
                var c = double.TryParse(x[i], NumberStyles.Any, CultureInfo.CurrentCulture, out var a)
                        && double.TryParse(y[i], NumberStyles.Any, CultureInfo.CurrentCulture, out var b)
                    ? a.CompareTo(b)
                    : string.Compare(x[i], y[i], StringComparison.CurrentCulture);
                if (c != 0)
                {
                    return c;
                }
            }
            return x.Length.CompareTo(y.Length);
        }

        public bool Equals(string[]? x, string[]? y) => x!.AsSpan().SequenceEqual(y);

        public int GetHashCode(string[] obj)
        {
            var hash = new HashCode();
            foreach (var s in obj)
            {
                hash.Add(s);
            }
            return hash.ToHashCode();
        }
    }

    public static PivotBuilderResult Compute(IEnumerable records, IReadOnlyDictionary<string, PivotBuilderField> fields,
        IReadOnlyList<string> rowFields, IReadOnlyList<string> columnFields, IReadOnlyList<PivotBuilderValue> values)
    {
        var rows = rowFields.Where(fields.ContainsKey).Select(k => fields[k]).ToArray();
        var columns = columnFields.Where(fields.ContainsKey).Select(k => fields[k]).ToArray();
        var vals = values.Where(v => fields.ContainsKey(v.FieldKey)).ToArray();
        var list = records.Cast<object>().Where(r => r is not null).ToList();

        var rowKeyOf = new string[list.Count][];
        var colKeyOf = new string[list.Count][];
        var rowSet = new HashSet<string[]>(KeyComparer.Instance);
        var colSet = new HashSet<string[]>(KeyComparer.Instance);
        for (var i = 0; i < list.Count; i++)
        {
            rowKeyOf[i] = rows.Select(f => KeyText(f.Selector(list[i]))).ToArray();
            colKeyOf[i] = columns.Select(f => KeyText(f.Selector(list[i]))).ToArray();
            rowSet.Add(rowKeyOf[i]);
            colSet.Add(colKeyOf[i]);
        }
        if (list.Count == 0)
        {
            rowSet.Add([]);
            colSet.Add([]);
        }
        var rowKeys = rowSet.OrderBy(k => k, KeyComparer.Instance).ToList();
        var colKeys = colSet.OrderBy(k => k, KeyComparer.Instance).ToList();
        var rowIndex = rowKeys.Select((k, i) => (k, i)).ToDictionary(t => t.k, t => t.i, KeyComparer.Instance);
        var colIndex = colKeys.Select((k, i) => (k, i)).ToDictionary(t => t.k, t => t.i, KeyComparer.Instance);

        var cells = new Acc[rowKeys.Count, colKeys.Count, vals.Length];
        var rowTotals = new Acc[rowKeys.Count, vals.Length];
        var colTotals = new Acc[colKeys.Count, vals.Length];
        var grand = new Acc[vals.Length];
        for (var i = 0; i < list.Count; i++)
        {
            var r = rowIndex[rowKeyOf[i]];
            var c = colIndex[colKeyOf[i]];
            for (var v = 0; v < vals.Length; v++)
            {
                var raw = fields[vals[v].FieldKey].Selector(list[i]);
                cells[r, c, v].Add(raw);
                rowTotals[r, v].Add(raw);
                colTotals[c, v].Add(raw);
                grand[v].Add(raw);
            }
        }

        var outCells = new double?[rowKeys.Count, colKeys.Count, vals.Length];
        var outRows = new double?[rowKeys.Count, vals.Length];
        var outCols = new double?[colKeys.Count, vals.Length];
        var outGrand = new double?[vals.Length];
        for (var v = 0; v < vals.Length; v++)
        {
            var agg = vals[v].Aggregation;
            for (var r = 0; r < rowKeys.Count; r++)
            {
                for (var c = 0; c < colKeys.Count; c++)
                {
                    outCells[r, c, v] = cells[r, c, v].Result(agg);
                }
                outRows[r, v] = rowTotals[r, v].Result(agg);
            }
            for (var c = 0; c < colKeys.Count; c++)
            {
                outCols[c, v] = colTotals[c, v].Result(agg);
            }
            outGrand[v] = grand[v].Result(agg);
        }
        return new PivotBuilderResult(rowKeys, colKeys, vals, outCells, outRows, outCols, outGrand);
    }
}
