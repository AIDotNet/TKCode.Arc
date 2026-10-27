using System.Globalization;
using Avalonia;

namespace TKCode.Arc;

/// <summary>One incident as the engine sees it: when, how long, how bad and for whom.</summary>
/// <param name="Start">Start.</param>
/// <param name="Duration">Length.</param>
/// <param name="Severity">Degraded or outage.</param>
/// <param name="Services">Keys of the affected services.</param>
internal readonly record struct UptimeInterval(DateTimeOffset Start, TimeSpan Duration, ServiceStatus Severity, IReadOnlyList<string> Services);

/// <summary>One bucketed day: its date, status, uptime fraction, downtime minutes and the indices of the intervals that touched it.</summary>
internal readonly record struct UptimeBucket(DateOnly Date, ServiceStatus Status, double Uptime, double Minutes, int[] Intervals);

/// <summary>
/// The arithmetic of <see cref="UptimeStatus"/>, measured from the Analytics group's Uptime status (design reference only,
/// ADR 0024; notes sections 3 and 4): the 90 bars of a service (bar width, x positions, heights, which bar a pointer is
/// over), the daily bucketing of incidents (a day's uptime is 1 minus its downtime minutes over 1,440; the window's is 1
/// minus all minutes over days times 1,440), the latency chart's geometry (domain, x positions, tick labels) and the hover
/// card's position.
/// </summary>
internal static class UptimeEngine
{
    /// <summary>Minutes in a day.</summary>
    public const double MinutesPerDay = 1440;

    /// <summary>The bar band's height (34).</summary>
    public const double StripHeight = 34;

    /// <summary>The widest bar (10 at the 1,112px reference) and the narrowest (3).</summary>
    public const double MinBarWidth = 3;

    /// <summary>The gap the bar width aims at (2): width = round((W - 2 (n - 1)) / n).</summary>
    public const double TargetGap = 2;

    /// <summary>The chart's plot: x from 44 to W - 36, y from 12 (top) to 186 (zero) in a 216 high chart.</summary>
    public const double PlotLeft = 44;

    public const double PlotRightInset = 36;

    public const double PlotTop = 12;

    public const double PlotBottom = 186;

    public const double ChartHeight = 216;

    /// <summary>The hover card is 256 wide and sits 8 above the bars, 8 from the section's edges.</summary>
    public const double TipWidth = 256;

    public const double TipMargin = 8;

    public const double TipGap = 8;

    // Bars ---------------------------------------------------------------------------------------------------------------

    /// <summary>The bar width for a band <paramref name="width"/> wide: round((W - 2 (n - 1)) / n), at least 3 (1112 → 10, 881.6 → 8, 770 → 7, 608 → 5, 472 → 3).</summary>
    public static double BarWidth(double width, int count)
    {
        if (count <= 0)
        {
            return MinBarWidth;
        }
        return Math.Max(MinBarWidth, Math.Round((width - TargetGap * (count - 1)) / count, MidpointRounding.AwayFromZero));
    }

    /// <summary>The left edge of bar <paramref name="index"/>: round(i (W - w) / (n - 1)); the first bar is at 0, the last ends at W.</summary>
    public static double BarX(int index, double width, double barWidth, int count)
    {
        if (count <= 1)
        {
            return 0;
        }
        return Math.Round(index * (width - barWidth) / (count - 1), MidpointRounding.AwayFromZero);
    }

    /// <summary>The bar whose centre is nearest to <paramref name="x"/> (clamped to the ends).</summary>
    public static int IndexAt(double x, double width, double barWidth, int count)
    {
        if (count <= 1 || width <= barWidth)
        {
            return 0;
        }
        var index = (int)Math.Round((x - barWidth / 2) * (count - 1) / (width - barWidth), MidpointRounding.AwayFromZero);
        return Math.Clamp(index, 0, count - 1);
    }

    /// <summary>The bar's height: 34 for operational, 65% (22) for degraded, 35% (12) for an outage.</summary>
    public static double BarHeight(ServiceStatus status, double stripHeight = StripHeight, double degraded = 0.65, double outage = 0.35) =>
        Math.Round(stripHeight * status switch
        {
            ServiceStatus.Operational => 1,
            ServiceStatus.Degraded => degraded,
            _ => outage
        }, MidpointRounding.AwayFromZero);

    // Days ---------------------------------------------------------------------------------------------------------------

    /// <summary>The minutes of the interval that fall on the UTC <paramref name="day"/>.</summary>
    public static double MinutesOnDay(DateTimeOffset start, TimeSpan duration, DateOnly day)
    {
        var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var begin = start > dayStart ? start : dayStart;
        var end = start + duration < dayEnd ? start + duration : dayEnd;
        return end > begin ? (end - begin).TotalMinutes : 0;
    }

    /// <summary>
    /// The window of <paramref name="count"/> days ending on <paramref name="endDay"/>, oldest first, for one service:
    /// the downtime of each day, its uptime (1 − minutes / 1,440, not below 0) and its status (the most severe incident that
    /// touched it). Degraded and outage minutes count alike.
    /// </summary>
    public static UptimeBucket[] Bucket(DateOnly endDay, int count, string serviceKey, IReadOnlyList<UptimeInterval> intervals)
    {
        var days = new UptimeBucket[Math.Max(0, count)];
        var touched = new List<int>();
        for (var i = 0; i < days.Length; i++)
        {
            var date = endDay.AddDays(i - (days.Length - 1));
            var minutes = 0.0;
            var status = ServiceStatus.Operational;
            touched.Clear();
            for (var k = 0; k < intervals.Count; k++)
            {
                var interval = intervals[k];
                if (!Affects(interval, serviceKey))
                {
                    continue;
                }
                var part = MinutesOnDay(interval.Start, interval.Duration, date);
                if (part <= 0)
                {
                    continue;
                }
                minutes += part;
                touched.Add(k);
                if (interval.Severity > status)
                {
                    status = interval.Severity;
                }
            }
            days[i] = new UptimeBucket(date, status, Math.Clamp(1 - minutes / MinutesPerDay, 0, 1), minutes, touched.ToArray());
        }
        return days;
    }

    /// <summary>The window's uptime: 1 minus the downtime minutes of its days over days times 1,440.</summary>
    public static double WindowUptime(IReadOnlyList<UptimeBucket> days)
    {
        if (days.Count == 0)
        {
            return 1;
        }
        var minutes = 0.0;
        foreach (var day in days)
        {
            minutes += day.Minutes;
        }
        return Math.Clamp(1 - minutes / (days.Count * MinutesPerDay), 0, 1);
    }

    /// <summary>True when the interval lists the service (an interval with no services affects none).</summary>
    public static bool Affects(UptimeInterval interval, string serviceKey)
    {
        foreach (var key in interval.Services)
        {
            if (string.Equals(key, serviceKey, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The uptime in percent units rounded to two decimals ("99.95" for 0.99946).</summary>
    public static double Percent(double fraction) => Math.Round(fraction * 100, 2, MidpointRounding.AwayFromZero);

    /// <summary>"99.95%".</summary>
    public static string PercentText(double fraction) => Percent(fraction).ToString("0.00", CultureInfo.InvariantCulture) + "%";

    /// <summary>"23 min" (a minute count rounded; hours are not folded).</summary>
    public static string MinutesText(double minutes, string unit = "min") =>
        Math.Round(minutes, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + " " + unit;

    // Hover card -----------------------------------------------------------------------------------------------------------

    /// <summary>The hover card's left edge: centred on the bar, clamped to [8, W - 8 - 256] of the section.</summary>
    public static double TipLeft(double barCenter, double sectionWidth)
    {
        var max = Math.Max(TipMargin, sectionWidth - TipMargin - TipWidth);
        return Math.Clamp(barCenter - TipWidth / 2, TipMargin, max);
    }

    // Latency chart --------------------------------------------------------------------------------------------------------

    /// <summary>The y domain: nice ticks from 0 over max(target, highest p95) × 1.2 (400 / 266 → 0..500 by 100; 1,000 / 612 → 0..1,250 by 250).</summary>
    public static AnalyticsTickSet ChartTicks(double target, double maxP95, double headroom = 1.2) =>
        AnalyticsTicks.Headroom(Math.Max(target, maxP95), headroom);

    /// <summary>The x of sample <paramref name="index"/>: 44 + i (plot width) / (n - 1).</summary>
    public static double ChartX(int index, int count, double chartWidth)
    {
        var plot = Math.Max(0, chartWidth - PlotLeft - PlotRightInset);
        return count <= 1 ? PlotLeft : PlotLeft + index * plot / (count - 1);
    }

    /// <summary>The y of a value: 186 − 174 v / top.</summary>
    public static double ChartY(double value, double top) =>
        top <= 0 ? PlotBottom : PlotBottom - (PlotBottom - PlotTop) * value / top;

    /// <summary>The sample nearest to a pointer x (switches exactly half way between two samples).</summary>
    public static int NearestSample(double x, int count, double chartWidth)
    {
        if (count <= 1)
        {
            return 0;
        }
        var plot = Math.Max(1e-6, chartWidth - PlotLeft - PlotRightInset);
        return Math.Clamp((int)Math.Round((x - PlotLeft) * (count - 1) / plot, MidpointRounding.AwayFromZero), 0, count - 1);
    }

    /// <summary>The samples that carry a time label: 6, 18, 30, 42 of 48 (every 12th from the 6th, a label per six hours).</summary>
    public static int[] LabelIndices(int count)
    {
        var list = new List<int>();
        for (var i = 6; i < count; i += 12)
        {
            list.Add(i);
        }
        return list.ToArray();
    }

    /// <summary>A crisp 1px line position: round(y) + 0.5.</summary>
    public static double Crisp(double v) => Math.Round(v, MidpointRounding.AwayFromZero) + 0.5;
}
