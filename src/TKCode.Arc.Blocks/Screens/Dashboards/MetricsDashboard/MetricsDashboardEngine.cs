namespace TKCode.Arc;

/// <summary>
/// One metric over one range, ready to draw: the chart points (days, or weeks when the range is longer than the weekly
/// threshold), the matching points of the previous period, the range's total and change, and the y axis.
/// </summary>
internal sealed class MetricsDashboardSeries
{
    /// <summary>The range in days.</summary>
    public required int Days { get; init; }

    /// <summary>True when each point is a week (Arc: 90D gives 13 weekly points, the last one 6 days long).</summary>
    public required bool IsWeekly { get; init; }

    /// <summary>The chart values, oldest first.</summary>
    public required double[] Values { get; init; }

    /// <summary>The previous period's values, aligned point for point; null when the data does not reach back that far.</summary>
    public required double[]? Previous { get; init; }

    /// <summary>The first day of each point.</summary>
    public required DateOnly[] Starts { get; init; }

    /// <summary>How many days each point covers (1, or up to 7 for weeks).</summary>
    public required int[] Spans { get; init; }

    /// <summary>The range's total (counts) or rate (ratios).</summary>
    public required double Total { get; init; }

    /// <summary>The previous period's total or rate.</summary>
    public required double? PreviousTotal { get; init; }

    /// <summary>The change against the previous period: percent for counts, points for ratios; null without a previous period.</summary>
    public required double? Change { get; init; }

    /// <summary>The y axis: exactly three rows.</summary>
    public required AnalyticsTickSet Ticks { get; init; }

    /// <summary>Number of points.</summary>
    public int Count => Values.Length;
}

/// <summary>
/// The dashboard's arithmetic (pure, tested without UI): windows, weekly buckets, totals, changes, the fixed three-row y
/// axis and the x label thinning, all as measured on Arc's Metrics dashboard.
/// </summary>
internal static class MetricsDashboardEngine
{
    /// <summary>
    /// Builds the series for the last <paramref name="days"/> days ending at <paramref name="end"/>. The previous period is
    /// the <paramref name="days"/> days before them. Ranges longer than <paramref name="weeklyThreshold"/> are summed in
    /// 7-day buckets counted from the range's first day; the last bucket keeps whatever days are left (Arc sums it as is).
    /// </summary>
    public static MetricsDashboardSeries Build(MetricsDashboardMetricKind kind, IReadOnlyList<double>? daily, IReadOnlyList<double>? numerators,
        IReadOnlyList<double>? denominators, int days, DateOnly end, int weeklyThreshold)
    {
        days = Math.Max(1, days);
        daily ??= Array.Empty<double>();
        var length = daily.Count;
        var span = Math.Min(days, length);
        var from = length - span;
        var weekly = days > weeklyThreshold;
        var bucket = weekly ? 7 : 1;
        var count = span == 0 ? 0 : (span + bucket - 1) / bucket;
        var values = new double[count];
        var starts = new DateOnly[count];
        var spans = new int[count];
        var hasPrevious = span == days && length >= 2 * days;
        var previous = hasPrevious ? new double[count] : null;
        var firstDay = end.AddDays(-(span - 1));
        for (var i = 0; i < count; i++)
        {
            var a = from + i * bucket;
            var b = Math.Min(a + bucket - 1, length - 1);
            values[i] = Aggregate(kind, daily, numerators, denominators, a, b);
            starts[i] = firstDay.AddDays(i * bucket);
            spans[i] = b - a + 1;
            if (previous is not null)
            {
                previous[i] = Aggregate(kind, daily, numerators, denominators, a - days, b - days);
            }
        }

        var total = span == 0 ? 0 : Aggregate(kind, daily, numerators, denominators, from, length - 1);
        double? previousTotal = hasPrevious ? Aggregate(kind, daily, numerators, denominators, from - days, length - 1 - days) : null;
        double? change = null;
        if (previousTotal is { } before)
        {
            if (kind == MetricsDashboardMetricKind.Ratio)
            {
                change = total - before;
            }
            else if (Math.Abs(before) > 1e-12)
            {
                change = (total - before) / Math.Abs(before) * 100;
            }
        }

        return new MetricsDashboardSeries
        {
            Days = days,
            IsWeekly = weekly,
            Values = values,
            Previous = previous,
            Starts = starts,
            Spans = spans,
            Total = total,
            PreviousTotal = previousTotal,
            Change = change,
            Ticks = Ticks(kind, values, previous)
        };
    }

    /// <summary>
    /// Days <paramref name="from"/> to <paramref name="to"/> (inclusive) of a series: their sum for counts; for ratios the
    /// summed numerators over the summed denominators (in percent) when both are given, otherwise the mean.
    /// </summary>
    public static double Aggregate(MetricsDashboardMetricKind kind, IReadOnlyList<double> daily, IReadOnlyList<double>? numerators,
        IReadOnlyList<double>? denominators, int from, int to)
    {
        from = Math.Max(0, from);
        to = Math.Min(daily.Count - 1, to);
        if (to < from)
        {
            return 0;
        }
        if (kind == MetricsDashboardMetricKind.Count)
        {
            double sum = 0;
            for (var i = from; i <= to; i++)
            {
                sum += daily[i];
            }
            return sum;
        }
        if (numerators is not null && denominators is not null && numerators.Count > to && denominators.Count > to)
        {
            double n = 0;
            double d = 0;
            for (var i = from; i <= to; i++)
            {
                n += numerators[i];
                d += denominators[i];
            }
            if (Math.Abs(d) > 1e-12)
            {
                return n / d * 100;
            }
        }
        double total = 0;
        for (var i = from; i <= to; i++)
        {
            total += daily[i];
        }
        return total / (to - from + 1);
    }

    /// <summary>
    /// The y axis, always three rows (four labels). Counts start at zero with <c>step = niceCeil(max / 3)</c> (measured:
    /// 1374 → 0–1.5K). Rates use <see cref="AnalyticsTicks.FixedRows"/>, which always covers the data; both cover the
    /// previous period too, so turning Compare on never changes the axis and the dashed line never leaves the plot.
    /// </summary>
    public static AnalyticsTickSet Ticks(MetricsDashboardMetricKind kind, ReadOnlySpan<double> values, ReadOnlySpan<double> previous)
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        foreach (var v in values)
        {
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        foreach (var v in previous)
        {
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        if (double.IsInfinity(min))
        {
            min = 0;
            max = 0;
        }
        return kind == MetricsDashboardMetricKind.Count || min >= max
            ? AnalyticsTicks.FixedRowsFromZero(Math.Max(max, 0))
            : AnalyticsTicks.FixedRows(min, max);
    }

    /// <summary>The most x labels a layout shows: 6 large, 5 medium, 4 small.</summary>
    public static int MaxLabels(MetricsDashboardSize size) => size switch
    {
        MetricsDashboardSize.Large => 6,
        MetricsDashboardSize.Medium => 5,
        _ => 4
    };

    /// <summary>Every how many points an x label goes: <c>ceil(n / maxLabels)</c> (30 points: 5 / 6 / 8; 13 weeks: 3 / 3 / 4; 7 days: 2).</summary>
    public static int LabelStep(int count, int maxLabels) => Math.Max(1, (int)Math.Ceiling(count / (double)Math.Max(1, maxLabels)));

    /// <summary>Default value text: counts "36,686", rates "4.8%".</summary>
    public static string FormatValue(MetricsDashboardMetricKind kind, double value)
        => kind == MetricsDashboardMetricKind.Ratio ? AnalyticsFormat.Percent(value, 1) : AnalyticsFormat.Number(value);

    /// <summary>Default change text (unsigned): counts "11.7%", rates "0.1 pts".</summary>
    public static string FormatChange(MetricsDashboardMetricKind kind, double change)
        => kind == MetricsDashboardMetricKind.Ratio ? AnalyticsFormat.Points(Math.Abs(change), 1) : AnalyticsFormat.Percent(Math.Abs(change), 1);

    /// <summary>
    /// Default axis label: counts "0", "500", "1.5K", "15K"; rates in percent with the decimals the step needs (a .25 step
    /// writes "4.25%", 2.5 writes "42.5%"; Arc rounded those to "4.3%" and "43%").
    /// </summary>
    public static string FormatAxis(MetricsDashboardMetricKind kind, double value, double step)
        => kind == MetricsDashboardMetricKind.Ratio
            ? AnalyticsFormat.Percent(value, AnalyticsTicks.DecimalsFor(step))
            : AnalyticsFormat.CompactFixed(value, 1, upperCaseThousands: true, trimZeros: true);
}
