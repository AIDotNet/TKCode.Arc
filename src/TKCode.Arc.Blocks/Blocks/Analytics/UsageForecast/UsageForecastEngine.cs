namespace TKCode.Arc;

/// <summary>
/// One metric laid out over a billing cycle: the line (measured up to today, forecast after it) and the edges of the
/// likely range, one value per day 0..<see cref="Days"/>. Day 0 is the start of the cycle (the left edge of the plot);
/// day 1 is the first day. Up to today <see cref="Upper"/> and <see cref="Lower"/> equal the line.
/// </summary>
internal sealed class UsageSeries
{
    /// <summary>The share of a weekday's growth that a weekend day adds (measured: about 0.43).</summary>
    public const double WeekendRatio = 0.43;

    /// <summary>Growth of the daily increment per day of forecast (about half a percent).</summary>
    public const double IncrementGrowth = 0.005;

    /// <summary>How far above the line the range reaches one day ahead, in weekday increments (measured 0.37).</summary>
    public const double FirstSpread = 0.37;

    /// <summary>The lower edge's spread over the upper edge's (measured 0.68 to 0.71).</summary>
    public const double LowerRatio = 0.70;

    public UsageSeries(int days, int today, DateOnly periodStart)
    {
        Days = days;
        Today = today;
        PeriodStart = periodStart;
        Line = new double[days + 1];
        Upper = new double[days + 1];
        Lower = new double[days + 1];
    }

    /// <summary>The length of the cycle in days.</summary>
    public int Days { get; }

    /// <summary>The last measured day (0 when there is no reading).</summary>
    public int Today { get; }

    /// <summary>The first day of the cycle.</summary>
    public DateOnly PeriodStart { get; }

    /// <summary>The value on each day.</summary>
    public double[] Line { get; }

    /// <summary>The top of the likely range.</summary>
    public double[] Upper { get; }

    /// <summary>The bottom of the likely range.</summary>
    public double[] Lower { get; }

    /// <summary>The date of day <paramref name="day"/> (1 is <see cref="PeriodStart"/>).</summary>
    public DateOnly DateOf(int day) => PeriodStart.AddDays(day - 1);

    /// <summary>True for Saturday and Sunday.</summary>
    public bool IsWeekend(int day) => DateOf(day).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>
    /// Builds the series. Days after today come from <paramref name="forecast"/> (and the range edges) when given, else from
    /// the last two weeks of readings: the weekday and weekend daily increments are extrapolated (each growing half a percent
    /// a day), and the range widens with the two-thirds power of the days ahead, 0.37 weekday increments one day out, the lower
    /// edge at 0.70 of the upper one.
    /// </summary>
    public static UsageSeries Build(int days, int today, double? start, IReadOnlyList<double>? readings, DateOnly periodStart,
        IReadOnlyList<double>? forecast = null, IReadOnlyList<double>? forecastLow = null, IReadOnlyList<double>? forecastHigh = null)
    {
        days = Math.Max(1, days);
        var count = readings?.Count ?? 0;
        today = today > 0 ? Math.Min(Math.Min(today, count), days) : Math.Min(count, days);
        var series = new UsageSeries(days, today, periodStart);
        var line = series.Line;
        line[0] = start ?? (today > 0 ? readings![0] : 0);
        for (var d = 1; d <= today; d++)
        {
            line[d] = readings![d - 1];
        }
        for (var d = 0; d <= today; d++)
        {
            series.Upper[d] = line[d];
            series.Lower[d] = line[d];
        }
        if (today >= days)
        {
            return series;
        }

        var (weekday, weekend, spread) = series.Increments();
        for (var d = today + 1; d <= days; d++)
        {
            var h = d - today;
            var i = h - 1;
            if (forecast is not null && i < forecast.Count)
            {
                line[d] = forecast[i];
            }
            else
            {
                var increment = (series.IsWeekend(d) ? weekend : weekday) * (1 + IncrementGrowth * h);
                line[d] = line[d - 1] + increment;
            }
            var width = spread * Math.Pow(h, 2.0 / 3.0);
            series.Upper[d] = forecastHigh is not null && i < forecastHigh.Count ? forecastHigh[i] : line[d] + width;
            series.Lower[d] = forecastLow is not null && i < forecastLow.Count ? forecastLow[i] : line[d] - LowerRatio * width;
        }
        return series;
    }

    /// <summary>The weekday and weekend increments and the one-day range of the last two weeks of readings.</summary>
    private (double Weekday, double Weekend, double Spread) Increments()
    {
        var first = Math.Max(1, Today - 13);
        var weekdays = new List<double>();
        var weekends = new List<double>();
        for (var d = first; d <= Today; d++)
        {
            if (d < 2 && Line[0] == Line[1])
            {
                continue;
            }
            (IsWeekend(d) ? weekends : weekdays).Add(Line[d] - Line[d - 1]);
        }
        var all = weekdays.Concat(weekends).ToList();
        var mean = all.Count > 0 ? all.Average() : 0;
        var weekday = weekdays.Count > 0 ? weekdays.Average() : mean;
        var weekend = weekends.Count > 0 ? weekends.Average() : weekday * WeekendRatio;
        var residuals = weekdays.Select(v => Math.Abs(v - weekday)).Concat(weekends.Select(v => Math.Abs(v - weekend))).OrderBy(v => v).ToList();
        var sigma = residuals.Count > 0 ? 1.4826 * residuals[residuals.Count / 2] : 0;
        var spread = Math.Max(FirstSpread * Math.Abs(weekday), 1.5 * sigma);
        return (weekday, weekend, spread);
    }
}

/// <summary>What the numbers say about one budget (headline, tiles, chips, marker positions).</summary>
internal sealed class UsageAnalysis
{
    public UsageOutcome Outcome { get; init; }

    /// <summary>The fractional day where the line reaches the budget; null when it never does.</summary>
    public double? CrossingX { get; init; }

    /// <summary>The day the budget is reached (the first day the reading is at or above it); 0 when none.</summary>
    public int CrossingDay { get; init; }

    /// <summary>The earliest day the budget could be reached (top of the range); 0 when none.</summary>
    public int EarlyDay { get; init; }

    /// <summary>The latest day the budget could be reached (bottom of the range); 0 when it is not reached.</summary>
    public int LateDay { get; init; }

    public double Used { get; init; }

    public double UsedPercent { get; init; }

    public double Projected { get; init; }

    public double ProjectedLow { get; init; }

    public double ProjectedHigh { get; init; }

    public double OverageCost { get; init; }

    public UsageThresholdState[] Thresholds { get; init; } = [];
}

/// <summary>One alert threshold against the budget.</summary>
internal readonly record struct UsageThresholdState(UsageAlertStatus Status, double? X, int Day);

internal static class UsageForecastEngine
{
    /// <summary>
    /// The first fractional day (0..N) where <paramref name="line"/> is at or above <paramref name="level"/>, found by linear
    /// interpolation inside the first segment that crosses it; 0 when the line starts above it, null when it never gets there.
    /// </summary>
    public static double? Crossing(ReadOnlySpan<double> line, double level)
    {
        if (line.Length == 0)
        {
            return null;
        }
        if (line[0] >= level)
        {
            return 0;
        }
        for (var d = 1; d < line.Length; d++)
        {
            if (line[d] >= level)
            {
                var a = line[d - 1];
                var b = line[d];
                return d - 1 + (b - a > 0 ? (level - a) / (b - a) : 1);
            }
        }
        return null;
    }

    /// <summary>The day a fractional crossing counts as: the first day whose reading reaches the level (at least 1).</summary>
    public static int DayOf(double x) => Math.Max(1, (int)Math.Ceiling(x - 1e-9));

    /// <summary>Everything the block shows about <paramref name="budget"/>.</summary>
    public static UsageAnalysis Analyze(UsageSeries series, double budget, double unitPrice, ReadOnlySpan<double> thresholdPercents)
    {
        var n = series.Days;
        var t = series.Today;
        var cross = Crossing(series.Line, budget);
        var outcome = cross is null ? UsageOutcome.StaysUnder
            : cross.Value <= t ? UsageOutcome.Passed
            : DayOf(cross.Value) >= n ? UsageOutcome.CrossesOnLastDay
            : UsageOutcome.WillCross;
        var early = Crossing(series.Upper, budget);
        var late = Crossing(series.Lower, budget);
        var projected = series.Line[n];
        var used = series.Line[t];
        var thresholds = new UsageThresholdState[thresholdPercents.Length];
        for (var i = 0; i < thresholds.Length; i++)
        {
            var level = thresholdPercents[i] / 100 * budget;
            var x = Crossing(series.Line, level);
            if (x is null)
            {
                thresholds[i] = new UsageThresholdState(UsageAlertStatus.NotExpected, null, 0);
            }
            else if (n >= 1 && series.Line[Math.Min(1, n)] >= level)
            {
                thresholds[i] = new UsageThresholdState(UsageAlertStatus.AlreadyPast, x, 1);
            }
            else
            {
                var day = DayOf(x.Value);
                thresholds[i] = new UsageThresholdState(x.Value <= t ? UsageAlertStatus.Reached : UsageAlertStatus.Expected, x, day);
            }
        }
        return new UsageAnalysis
        {
            Outcome = outcome,
            CrossingX = cross,
            CrossingDay = cross is { } c ? DayOf(c) : 0,
            EarlyDay = early is { } e ? DayOf(e) : 0,
            LateDay = late is { } l ? DayOf(l) : 0,
            Used = used,
            UsedPercent = budget > 0 ? Math.Round(used / budget * 100, MidpointRounding.AwayFromZero) : 0,
            Projected = projected,
            ProjectedLow = series.Lower[n],
            ProjectedHigh = series.Upper[n],
            OverageCost = Math.Max(0, projected - budget) * Math.Max(0, unitPrice),
            Thresholds = thresholds
        };
    }

    /// <summary>
    /// The value axis: six ticks, five equal steps. Uses the metric's own domain when it names one, else a round top (by the
    /// ladder 1, 2, 2.5, 5) that holds the data and the budget range.
    /// </summary>
    public static (double Min, double Max) Domain(double axisMin, double axisMax, double dataMax, double budgetMax)
    {
        if (!double.IsNaN(axisMax) && axisMax > axisMin)
        {
            return (axisMin, axisMax);
        }
        var top = Math.Max(Math.Max(dataMax, budgetMax), axisMin + 1e-6);
        var step = AnalyticsTicks.NiceCeiling((top - axisMin) / 5);
        return (axisMin, axisMin + step * 5);
    }

    /// <summary>The budget a pointer position stands for: relative to the start of the drag, snapped to <paramref name="step"/>.</summary>
    public static double Snap(double value, double min, double step)
    {
        if (step <= 0)
        {
            return value;
        }
        return AnalyticsTicks.Clean(min + Math.Round((value - min) / step, MidpointRounding.AwayFromZero) * step);
    }

    /// <summary>
    /// How far a line dragged <paramref name="excess"/> px beyond its limit shows: it follows but is squeezed (41px shows
    /// as 9px, 151px as 12px, saturating near 13px: 13 (1 - e^(-x/35))).
    /// </summary>
    public static double Rubber(double excess) => excess <= 0 ? 0 : 13 * (1 - Math.Exp(-excess / 35));

    /// <summary>How much the date labels near the crossing marker fade: 1 beyond 40px, 0 within 28px.</summary>
    public static double LabelFade(double labelX, double markerX) => Math.Clamp((Math.Abs(labelX - markerX) - 28) / 12, 0, 1);
}
