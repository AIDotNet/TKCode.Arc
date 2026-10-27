using System.Globalization;

namespace TKCode.Arc;

/// <summary>The headline numbers of a range (see <see cref="RevenueData.Stats"/>).</summary>
/// <param name="EndMrr">Closing recurring revenue of the last month, or NaN when unknown.</param>
/// <param name="MrrChange">Change of the closing revenue since the month before the range (a fraction), or null.</param>
/// <param name="AverageNetNew">Average net new per month in the range.</param>
/// <param name="NetNewChange">Change of that average against the previous range of the same length (a fraction), or null.</param>
/// <param name="ChurnRate">Churned revenue over the opening revenue of the months, in percent, or null when unknown.</param>
/// <param name="ChurnChange">Change of the churn rate against the previous range, in percentage points, or null.</param>
internal readonly record struct RevenueStats(double EndMrr, double? MrrChange, double AverageNetNew, double? NetNewChange,
    double? ChurnRate, double? ChurnChange);

/// <summary>
/// The numbers of a <see cref="RevenueExplorer"/> resolved into arrays (closing revenue derived where missing) and the
/// pure rules the block measured: the headline numbers of a range, the y domain of the visible series, preset ranges
/// and the keyboard steps of the overview. No UI, so the rules are tested directly.
/// </summary>
internal sealed class RevenueData
{
    /// <summary>The empty data set.</summary>
    public static readonly RevenueData Empty = new([], double.NaN);

    /// <summary>Resolves <paramref name="months"/> (in order) with the revenue before the first month.</summary>
    public RevenueData(IReadOnlyList<RevenueMonth> months, double opening)
    {
        Count = months.Count;
        Opening = opening;
        Months = new DateOnly[Count];
        New = new double[Count];
        Expansion = new double[Count];
        Churn = new double[Count];
        Net = new double[Count];
        Mrr = new double[Count];
        var previous = opening;
        for (var i = 0; i < Count; i++)
        {
            var m = months[i];
            Months[i] = new DateOnly(m.Month.Year, m.Month.Month, 1);
            New[i] = Math.Max(0, m.New);
            Expansion[i] = Math.Max(0, m.Expansion);
            Churn[i] = m.Churn > 0 ? -m.Churn : m.Churn;
            Net[i] = New[i] + Expansion[i] + Churn[i];
            Mrr[i] = double.IsFinite(m.Mrr) ? m.Mrr : previous + Net[i];
            previous = Mrr[i];
        }
    }

    /// <summary>Months.</summary>
    public int Count { get; }

    /// <summary>Revenue before the first month (NaN when unknown).</summary>
    public double Opening { get; }

    /// <summary>First day of each month.</summary>
    public DateOnly[] Months { get; }

    /// <summary>New revenue per month.</summary>
    public double[] New { get; }

    /// <summary>Expansion revenue per month.</summary>
    public double[] Expansion { get; }

    /// <summary>Churn per month (zero or negative).</summary>
    public double[] Churn { get; }

    /// <summary>Net new per month.</summary>
    public double[] Net { get; }

    /// <summary>Closing recurring revenue per month.</summary>
    public double[] Mrr { get; }

    /// <summary>Closing revenue before month <paramref name="i"/> (the opening revenue for the first month); NaN when unknown.</summary>
    public double OpeningOf(int i) => i <= 0 ? Opening : Mrr[i - 1];

    /// <summary>Clamps a range to the data: start and end in range, at least <paramref name="minMonths"/> long when possible.</summary>
    public (int Start, int End) ClampRange(int start, int end, int minMonths)
    {
        if (Count == 0)
        {
            return (0, 0);
        }
        var last = Count - 1;
        var minSpan = Math.Clamp(minMonths - 1, 0, last);
        start = Math.Clamp(start, 0, last);
        end = Math.Clamp(Math.Max(end, start + minSpan), 0, last);
        // Short of room on the right: the window keeps its minimum by moving left.
        start = Math.Min(start, end - minSpan);
        return (start, end);
    }

    /// <summary>The range of a preset: the newest <paramref name="months"/> months (0 or more than there are: all).</summary>
    public (int Start, int End) PresetRange(int months)
    {
        if (Count == 0)
        {
            return (0, 0);
        }
        var n = months <= 0 ? Count : Math.Min(months, Count);
        return (Count - n, Count - 1);
    }

    /// <summary>Net new averaged over a range, NaN when empty.</summary>
    private double Average(int start, int end)
    {
        var sum = 0.0;
        for (var i = start; i <= end; i++)
        {
            sum += Net[i];
        }
        return end >= start ? sum / (end - start + 1) : double.NaN;
    }

    /// <summary>Churned revenue over the opening revenue of the same months, in percent; null when an opening is unknown.</summary>
    private double? ChurnRate(int start, int end)
    {
        double churned = 0, opening = 0;
        for (var i = start; i <= end; i++)
        {
            var o = OpeningOf(i);
            if (!double.IsFinite(o))
            {
                return null;
            }
            churned += -Churn[i];
            opening += o;
        }
        return opening > 0 ? churned / opening * 100 : null;
    }

    /// <summary>
    /// The headline numbers of months <paramref name="start"/> to <paramref name="end"/>: the closing revenue and its
    /// change since the month before; the average net new and its change against the previous range of the same length;
    /// the churn rate (churn over the months' opening revenue) and its change in points. A change is null where the
    /// months before the range are missing.
    /// </summary>
    public RevenueStats Stats(int start, int end)
    {
        if (Count == 0)
        {
            return new RevenueStats(double.NaN, null, 0, null, null, null);
        }
        (start, end) = ClampRange(start, end, 1);
        var length = end - start + 1;
        var endMrr = Mrr[end];
        var before = OpeningOf(start);
        double? mrrChange = double.IsFinite(before) && before > 0 ? endMrr / before - 1 : null;
        var average = Average(start, end);
        double? netChange = null;
        double? rate = ChurnRate(start, end);
        double? rateChange = null;
        if (start - length >= 0)
        {
            var previous = Average(start - length, start - 1);
            if (Math.Abs(previous) > 1e-9)
            {
                netChange = average / previous - 1;
            }
            if (rate is { } now && ChurnRate(start - length, start - 1) is { } then)
            {
                rateChange = now - then;
            }
        }
        return new RevenueStats(endMrr, mrrChange, average, netChange, rate, rateChange);
    }

    /// <summary>
    /// The y domain and its tick set for a range. The top is 18% above the highest visible stack plus 8% of the deepest
    /// churn, the bottom 20% below the deepest visible churn (measured fits, 0.3% error); a hidden series counts as zero
    /// and with every series hidden the domain is the one of "new" alone. <paramref name="compare"/> adds the previous
    /// period's data (months <c>i - length</c>) to the extremes.
    /// </summary>
    public AnalyticsTickSet Domain(int start, int end, bool showNew, bool showExpansion, bool showChurn, bool compare, double plotHeight)
    {
        if (Count == 0)
        {
            return AnalyticsTicks.FitHeight(0, 1, plotHeight);
        }
        if (!showNew && !showExpansion && !showChurn)
        {
            showNew = true;
        }
        (start, end) = ClampRange(start, end, 1);
        var length = end - start + 1;
        var maxPos = 0.0;
        var minNeg = 0.0;
        for (var i = start; i <= end; i++)
        {
            maxPos = Math.Max(maxPos, Top(i, showNew, showExpansion));
            if (showChurn)
            {
                minNeg = Math.Min(minNeg, Churn[i]);
            }
            var p = i - length;
            if (compare && p >= 0)
            {
                maxPos = Math.Max(maxPos, Top(p, showNew, showExpansion));
                if (showChurn)
                {
                    minNeg = Math.Min(minNeg, Churn[p]);
                }
            }
        }
        var lo = 1.2 * minNeg;
        var hi = Math.Max(1.18 * maxPos + 0.08 * -minNeg, 1);
        return AnalyticsTicks.FitHeight(lo, hi, plotHeight);
    }

    /// <summary>The stacked top of the positive series at month <paramref name="i"/>.</summary>
    public double Top(int i, bool showNew, bool showExpansion) => (showNew ? New[i] : 0) + (showExpansion ? Expansion[i] : 0);

    /// <summary>
    /// The range after a key on the overview. <paramref name="part"/> 0 is the window (moves both ends, width kept), 1 the
    /// start handle, 2 the end handle; <paramref name="step"/> is −1 or +1 months (×3 with Shift) or
    /// <see cref="int.MinValue"/> / <see cref="int.MaxValue"/> for Home / End. Handles never come closer than
    /// <paramref name="minMonths"/>; nothing swaps.
    /// </summary>
    public (int Start, int End) Step(int part, int start, int end, int step, int minMonths)
    {
        var last = Count - 1;
        var minSpan = Math.Max(1, minMonths - 1);
        var span = end - start;
        switch (part)
        {
            case 0:
            {
                var s = step == int.MinValue ? 0 : step == int.MaxValue ? last - span : start + step;
                s = Math.Clamp(s, 0, Math.Max(0, last - span));
                return (s, s + span);
            }
            case 1:
            {
                var s = step == int.MinValue ? 0 : step == int.MaxValue ? end - minSpan : start + step;
                return (Math.Clamp(s, 0, Math.Max(0, end - minSpan)), end);
            }
            default:
            {
                var e = step == int.MinValue ? start + minSpan : step == int.MaxValue ? last : end + step;
                return (start, Math.Clamp(e, Math.Min(last, start + minSpan), last));
            }
        }
    }

    /// <summary>The window start after a click at month position <paramref name="clickMonth"/>: the window's centre moves there.</summary>
    public int CenterOn(double clickMonth, int start, int end)
    {
        var span = end - start;
        return Math.Clamp((int)Math.Round(clickMonth - span / 2.0, MidpointRounding.AwayFromZero), 0, Math.Max(0, Count - 1 - span));
    }

    /// <summary>Label stride that keeps month labels at least <paramref name="minPixels"/> apart (1 shows every month).</summary>
    public static int LabelStride(double pixelsPerMonth, double minPixels = 40)
    {
        if (pixelsPerMonth <= 0)
        {
            return 1;
        }
        return Math.Max(1, (int)Math.Ceiling(minPixels / pixelsPerMonth - 1e-9));
    }

    /// <summary>Default axis label of a month: the year for January, else the short month name.</summary>
    public static string DefaultMonthLabel(DateOnly month, CultureInfo culture) =>
        month.Month == 1 ? month.Year.ToString(culture) : culture.DateTimeFormat.GetAbbreviatedMonthName(month.Month);

    /// <summary>Default month and year ("Mar 2026").</summary>
    public static string DefaultMonthYear(DateOnly month, CultureInfo culture) =>
        $"{culture.DateTimeFormat.GetAbbreviatedMonthName(month.Month)} {month.Year.ToString(culture)}";

    /// <summary>Default range text ("Sep 2025 – Aug 2026"; the first year is left out inside one year: "Mar – Aug 2026").</summary>
    public static string DefaultRange(DateOnly from, DateOnly to, CultureInfo culture)
    {
        var a = culture.DateTimeFormat.GetAbbreviatedMonthName(from.Month);
        var b = culture.DateTimeFormat.GetAbbreviatedMonthName(to.Month);
        var y2 = to.Year.ToString(culture);
        return from.Year == to.Year ? $"{a} – {b} {y2}" : $"{a} {from.Year.ToString(culture)} – {b} {y2}";
    }
}
