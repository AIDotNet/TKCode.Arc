namespace TKCode.Arc;

/// <summary>
/// The numbers an <see cref="ActivityTerrain"/> derives from a metric: the week's total and its change on the week
/// before, the peak hours (largest first; ties by day then hour), the day totals, the busiest day and the working-hours
/// share (working-hours total ÷ week total).
/// </summary>
internal sealed class ActivityTerrainSummary
{
    private ActivityTerrainSummary(double[] values, double[]? previous, double total, double? previousTotal, double max,
                                   ActivityTerrainCell peak, ActivityTerrainCell[] peaks, double[] dayTotals, int busiestDay,
                                   double workingShare)
    {
        Values = values;
        Previous = previous;
        Total = total;
        PreviousTotal = previousTotal;
        Max = max;
        Peak = peak;
        Peaks = peaks;
        DayTotals = dayTotals;
        BusiestDay = busiestDay;
        WorkingShare = workingShare;
    }

    /// <summary>The 168 values (missing entries as zero).</summary>
    public double[] Values { get; }

    /// <summary>The previous week's 168 values, or null.</summary>
    public double[]? Previous { get; }

    public double Total { get; }

    public double? PreviousTotal { get; }

    /// <summary>The week's change (0.086 = +8.6%), or null without a previous week (or when it summed to zero).</summary>
    public double? Change => PreviousTotal is > 0 and { } previous ? Total / previous - 1 : null;

    /// <summary>The largest value (bar height and color scale).</summary>
    public double Max { get; }

    /// <summary>The cell of the largest value (the first arrow key lands here).</summary>
    public ActivityTerrainCell Peak { get; }

    /// <summary>The largest hours, largest first.</summary>
    public ActivityTerrainCell[] Peaks { get; }

    public double[] DayTotals { get; }

    public int BusiestDay { get; }

    /// <summary>The share of the week's total that falls in the working hours (0–1).</summary>
    public double WorkingShare { get; }

    /// <summary>True when every value is zero (the terrain shows "No data").</summary>
    public bool IsEmpty => Max <= 0;

    public double ValueAt(ActivityTerrainCell cell) => Values[cell.Index];

    public double? PreviousAt(ActivityTerrainCell cell) => Previous?[cell.Index];

    /// <summary>The cell's change on the same hour last week, or null.</summary>
    public double? ChangeAt(ActivityTerrainCell cell) =>
        Previous is { } previous && previous[cell.Index] > 0 ? Values[cell.Index] / previous[cell.Index] - 1 : null;

    /// <summary>The cell's share of its day's total (0–1).</summary>
    public double ShareOfDay(ActivityTerrainCell cell) =>
        DayTotals[cell.Day] > 0 ? Values[cell.Index] / DayTotals[cell.Day] : 0;

    /// <summary>The largest value of a day (the card's 24 small bars are scaled to it).</summary>
    public double DayMax(int day)
    {
        var max = 0.0;
        for (var h = 0; h < ActivityTerrainMath.Hours; h++)
        {
            max = Math.Max(max, Values[day * ActivityTerrainMath.Hours + h]);
        }
        return max;
    }

    public static ActivityTerrainSummary Empty { get; } = Create(null, null, 3, new ActivityTerrainWorkingHours());

    public static ActivityTerrainSummary Create(IReadOnlyList<double>? values, IReadOnlyList<double>? previousValues, int peakCount,
                                                ActivityTerrainWorkingHours? workingHours)
    {
        var cells = ActivityTerrainMath.Cells;
        var current = new double[cells];
        double[]? previous = previousValues is null ? null : new double[cells];
        double total = 0, previousTotal = 0, max = 0, working = 0;
        var peak = new ActivityTerrainCell(0, 0);
        var dayTotals = new double[ActivityTerrainMath.Days];
        var hours = workingHours ?? new ActivityTerrainWorkingHours();
        for (var i = 0; i < cells; i++)
        {
            var value = Math.Max(0, ActivityTerrainMetric.At(values, i));
            current[i] = value;
            total += value;
            var cell = ActivityTerrainCell.FromIndex(i);
            dayTotals[cell.Day] += value;
            if (hours.Contains(cell))
            {
                working += value;
            }
            if (value > max)
            {
                max = value;
                peak = cell;
            }
            if (previous is not null)
            {
                var before = Math.Max(0, ActivityTerrainMetric.At(previousValues, i));
                previous[i] = before;
                previousTotal += before;
            }
        }
        var order = new int[cells];
        for (var i = 0; i < cells; i++)
        {
            order[i] = i;
        }
        // Largest first; equal values keep day, then hour order (the index order).
        Array.Sort(order, (a, b) =>
        {
            var byValue = current[b].CompareTo(current[a]);
            return byValue != 0 ? byValue : a.CompareTo(b);
        });
        var count = Math.Clamp(peakCount, 0, cells);
        var peaks = new ActivityTerrainCell[count];
        for (var i = 0; i < count; i++)
        {
            peaks[i] = ActivityTerrainCell.FromIndex(order[i]);
        }
        var busiest = 0;
        for (var d = 1; d < ActivityTerrainMath.Days; d++)
        {
            if (dayTotals[d] > dayTotals[busiest])
            {
                busiest = d;
            }
        }
        return new ActivityTerrainSummary(current, previous, total, previous is null ? null : previousTotal, max, peak, peaks,
            dayTotals, busiest, total > 0 ? working / total : 0);
    }
}
