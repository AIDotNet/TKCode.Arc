using System.Globalization;

namespace TKCode.Arc;

/// <summary>The x axis tick ladders of the metric explorer, finest first.</summary>
internal enum MetricExplorerTickLevel
{
    /// <summary>Every Monday, "Sep 7".</summary>
    Weekly,

    /// <summary>Every other Monday, "Sep 7".</summary>
    Biweekly,

    /// <summary>The 1st and the 15th, "Sep 15".</summary>
    SemiMonthly,

    /// <summary>The 1st of every month, "Sep".</summary>
    Monthly,

    /// <summary>The 1st of January, April, July and October, "Oct 2026".</summary>
    Quarterly,
}

/// <summary>
/// The metric explorer's pure rules (notes sections 4.1–4.4), kept free of UI so they can be tested against the measured
/// numbers: the y axis (five ticks, the fine ladder, 3% padding, a decimals cap per metric), the x tick ladder and its
/// pixel-spacing fallback, the edge fade of x labels, nearest-day hit testing, changes and their texts, sparkline samples.
/// </summary>
internal static class MetricExplorerMath
{
    /// <summary>Spacing an "Mmm d" label needs between ticks (measured ≈ 84px: 30D weekly holds at plot 352, falls at 346).</summary>
    public const double DayLabelSpacing = 84;

    /// <summary>Spacing an "Mmm" label needs (measured between 48.05 and 48.55px: 12M monthly holds at plot 579, falls at 573).</summary>
    public const double MonthLabelSpacing = 48.3;

    /// <summary>X labels fade over this many pixels next to the plot edges, starting 4px inside them.</summary>
    public const double EdgeFadeWidth = 12;

    /// <summary>Distance from the plot edges where the fade reaches zero.</summary>
    public const double EdgeFadeInset = 4;

    private static readonly CultureInfo s_culture = AnalyticsFormat.DefaultCulture;

    #region Axes

    /// <summary>Chart height for a width: a quarter of it, clamped to [<paramref name="min"/>, <paramref name="max"/>] (180 / 240).</summary>
    public static double ChartHeight(double width, double min = 180, double max = 240) =>
        Math.Clamp(Math.Round(width / 4, 2), min, max);

    /// <summary>
    /// The y axis: <paramref name="rows"/> + 1 ticks from the smallest fine-ladder step (1, 1.5, 2, 2.5, 3, 4, 5 × 10ⁿ) at
    /// least a quarter of the range padded by 3% on both sides, the bottom tick a multiple of the step at or below the
    /// padded minimum, the first step whose top tick reaches the padded maximum wins. Steps with more decimals than
    /// <paramref name="maxDecimals"/> are skipped. Values are divided by <paramref name="unit"/> first (1e6 for "$1.22M") and
    /// the ticks come back in raw units.
    /// </summary>
    public static AnalyticsTickSet AxisTicks(double min, double max, int maxDecimals, double unit = 1, int rows = 4, double padding = 0.03)
    {
        if (!(unit > 0))
        {
            unit = 1;
        }
        if (!double.IsFinite(min) || !double.IsFinite(max))
        {
            min = 0;
            max = 1;
        }
        min /= unit;
        max /= unit;
        if (max < min)
        {
            (min, max) = (max, min);
        }
        var span = max - min;
        if (span <= 0)
        {
            span = Math.Max(Math.Abs(max), Math.Pow(10, -Math.Max(0, maxDecimals))) * 0.1;
        }
        var lowPad = min - span * padding;
        var highPad = max + span * padding;
        var step = AnalyticsTicks.NiceCeiling((highPad - lowPad) / rows, AnalyticsTicks.FineLadder);
        var smallest = Math.Pow(10, -Math.Max(0, maxDecimals));
        for (var attempt = 0; attempt < 64; attempt++)
        {
            if (maxDecimals < 0 || step >= smallest - 1e-12 && AnalyticsTicks.DecimalsFor(step) <= maxDecimals)
            {
                var lo = AnalyticsTicks.Clean(Math.Floor(lowPad / step + 1e-9) * step);
                if (lo + rows * step >= highPad - 1e-9)
                {
                    return Build(lo, step, rows, unit);
                }
            }
            step = AnalyticsTicks.NextStep(step, AnalyticsTicks.FineLadder);
        }
        return Build(lowPad, (highPad - lowPad) / rows, rows, unit);
    }

    private static AnalyticsTickSet Build(double lo, double step, int rows, double unit)
    {
        var ticks = new double[rows + 1];
        for (var i = 0; i <= rows; i++)
        {
            ticks[i] = AnalyticsTicks.Clean((lo + i * step)) * unit;
        }
        return new AnalyticsTickSet(ticks[0], ticks[^1], AnalyticsTicks.Clean(step) * unit, ticks);
    }

    /// <summary>The display unit of a compact amount: 1e9, 1e6, 1e3 or 1 (by the largest magnitude shown).</summary>
    public static double CompactUnit(double magnitude)
    {
        magnitude = Math.Abs(magnitude);
        return magnitude >= 1e9 ? 1e9 : magnitude >= 1e6 ? 1e6 : magnitude >= 1e3 ? 1e3 : 1;
    }

    /// <summary>The letter of a compact unit.</summary>
    public static string CompactSuffix(double unit) => unit switch
    {
        >= 1e9 => "B",
        >= 1e6 => "M",
        >= 1e3 => "K",
        _ => string.Empty
    };

    /// <summary>The decimals cap of a format when the metric does not set one.</summary>
    public static int DefaultAxisDecimals(ExplorerValueFormat format) => format switch
    {
        ExplorerValueFormat.CurrencyCompact => 2,
        ExplorerValueFormat.Ratio => 2,
        ExplorerValueFormat.Percent => 1,
        _ => 0
    };

    /// <summary>The y axis of a metric over [min, max]: ticks in raw units.</summary>
    public static AnalyticsTickSet MetricAxis(ExplorerValueFormat format, int maxAxisDecimals, double min, double max)
    {
        var decimals = maxAxisDecimals >= 0 ? maxAxisDecimals : DefaultAxisDecimals(format);
        var unit = format == ExplorerValueFormat.CurrencyCompact ? CompactUnit(Math.Max(Math.Abs(min), Math.Abs(max))) : 1;
        return AxisTicks(min, max, decimals, unit);
    }

    /// <summary>A y axis label by the format's rule (compact money keeps its two decimals; percentages show the step's).</summary>
    public static string AxisLabel(ExplorerValueFormat format, string symbol, int maxAxisDecimals, double value, double step, double unit)
    {
        var decimals = maxAxisDecimals >= 0 ? maxAxisDecimals : DefaultAxisDecimals(format);
        switch (format)
        {
            case ExplorerValueFormat.CurrencyCompact:
                {
                    var u = unit > 0 ? unit : 1;
                    return AnalyticsFormat.WithTrueMinus(symbol + (value / u).ToString("N" + decimals, s_culture) + CompactSuffix(u));
                }
            case ExplorerValueFormat.Currency:
                return AnalyticsFormat.Currency(value, Math.Min(decimals, AnalyticsTicks.DecimalsFor(step)), symbol);
            case ExplorerValueFormat.Percent:
                return AnalyticsFormat.Percent(value, Math.Min(Math.Max(0, decimals), AnalyticsTicks.DecimalsFor(step)));
            case ExplorerValueFormat.Ratio:
                return AnalyticsFormat.Number(value, Math.Min(Math.Max(0, decimals), AnalyticsTicks.DecimalsFor(step))) + "×";
            default:
                return AnalyticsFormat.Number(value, Math.Min(Math.Max(0, decimals), AnalyticsTicks.DecimalsFor(step)));
        }
    }

    /// <summary>A value as the cards, tabs and readout write it.</summary>
    public static string ValueText(ExplorerValueFormat format, string symbol, double value) => format switch
    {
        ExplorerValueFormat.CurrencyCompact => AnalyticsFormat.CompactFixed(value, 2, symbol, upperCaseThousands: true),
        ExplorerValueFormat.Currency => AnalyticsFormat.Currency(value, 0, symbol),
        ExplorerValueFormat.Percent => AnalyticsFormat.Percent(value, 1),
        ExplorerValueFormat.Ratio => AnalyticsFormat.Number(value, 2) + "×",
        _ => AnalyticsFormat.Number(value)
    };

    #endregion

    #region Changes

    /// <summary>
    /// The change from <paramref name="before"/> to <paramref name="now"/>: percent units for <see cref="ExplorerChangeMode.Relative"/>
    /// (1.6 is "+1.6%"), the difference for <see cref="ExplorerChangeMode.Points"/>. NaN when it cannot be computed.
    /// </summary>
    public static double Change(ExplorerChangeMode mode, double now, double before)
    {
        if (!double.IsFinite(now) || !double.IsFinite(before))
        {
            return double.NaN;
        }
        if (mode == ExplorerChangeMode.Points)
        {
            return now - before;
        }
        return before == 0 ? double.NaN : (now - before) / Math.Abs(before) * 100;
    }

    /// <summary>The signed one-decimal format of a change ("+1.6%", "−1.1 pts").</summary>
    public static AnalyticsNumberFormat ChangeFormat(ExplorerChangeMode mode) =>
        mode == ExplorerChangeMode.Points ? AnalyticsNumberFormat.Points(1, signed: true) : AnalyticsNumberFormat.Percent(1, signed: true);

    /// <summary>The direction and tone of a change (flat when it rounds to zero at one decimal).</summary>
    public static (AnalyticsDirection Direction, AnalyticsTone Tone) Trend(double change, bool higherIsBetter)
    {
        if (!double.IsFinite(change))
        {
            return (AnalyticsDirection.Flat, AnalyticsTone.Flat);
        }
        var tone = AnalyticsFormat.Tone(change, !higherIsBetter, 1);
        if (tone == AnalyticsTone.Flat)
        {
            return (AnalyticsDirection.Flat, AnalyticsTone.Flat);
        }
        return (change > 0 ? AnalyticsDirection.Up : AnalyticsDirection.Down, tone);
    }

    #endregion

    #region Days and ticks

    /// <summary>The first-choice ladder of a range: weekly up to 45 days, every other week up to 120, monthly beyond.</summary>
    public static MetricExplorerTickLevel PreferredLevel(int days) => days switch
    {
        <= 45 => MetricExplorerTickLevel.Weekly,
        <= 120 => MetricExplorerTickLevel.Biweekly,
        _ => MetricExplorerTickLevel.Monthly
    };

    /// <summary>Average days between two ticks of a ladder (half-months count 15, months 365.25 / 12).</summary>
    public static double LevelDays(MetricExplorerTickLevel level) => level switch
    {
        MetricExplorerTickLevel.Weekly => 7,
        MetricExplorerTickLevel.Biweekly => 14,
        MetricExplorerTickLevel.SemiMonthly => 15,
        MetricExplorerTickLevel.Monthly => 365.25 / 12,
        _ => 365.25 / 4
    };

    /// <summary>The spacing a ladder's labels need.</summary>
    public static double LevelSpacing(MetricExplorerTickLevel level) =>
        level <= MetricExplorerTickLevel.SemiMonthly ? DayLabelSpacing : MonthLabelSpacing;

    /// <summary>
    /// The ladder for <paramref name="days"/> days on a plot <paramref name="plotWidth"/> wide: the range's first choice, then
    /// coarser ones while ticks would be closer than their labels need.
    /// </summary>
    public static MetricExplorerTickLevel ChooseLevel(int days, double plotWidth)
    {
        var level = PreferredLevel(days);
        var perDay = plotWidth / Math.Max(1, days - 1);
        while (level < MetricExplorerTickLevel.Quarterly && perDay * LevelDays(level) < LevelSpacing(level) - 1e-9)
        {
            level++;
        }
        return level;
    }

    /// <summary>Whether <paramref name="date"/> carries a tick of <paramref name="level"/>.</summary>
    public static bool IsTick(MetricExplorerTickLevel level, DateTime date) => level switch
    {
        MetricExplorerTickLevel.Weekly => date.DayOfWeek == DayOfWeek.Monday,
        // Every other Monday on a fixed calendar parity, so ticks do not hop when the window moves.
        MetricExplorerTickLevel.Biweekly => date.DayOfWeek == DayOfWeek.Monday && (DateOnly.FromDateTime(date).DayNumber / 7) % 2 == 0,
        MetricExplorerTickLevel.SemiMonthly => date.Day is 1 or 15,
        MetricExplorerTickLevel.Monthly => date.Day == 1,
        _ => date.Day == 1 && (date.Month - 1) % 3 == 0
    };

    /// <summary>A tick's label: "Sep 7", "Sep" or "Oct 2026".</summary>
    public static string TickLabel(MetricExplorerTickLevel level, DateTime date) => level switch
    {
        MetricExplorerTickLevel.Monthly => date.ToString("MMM", s_culture),
        MetricExplorerTickLevel.Quarterly => date.ToString("MMM yyyy", s_culture),
        _ => date.ToString("MMM d", s_culture)
    };

    /// <summary>
    /// The opacity of an x label at <paramref name="x"/>: 1 inside, falling linearly to 0 over 12px as it nears a plot edge
    /// (0 within 4px of it). A tick exactly on the left edge is invisible (the first day of a 30-day window).
    /// </summary>
    public static double EdgeFade(double x, double left, double right) =>
        Math.Clamp(Math.Min(x - (left + EdgeFadeInset), right - EdgeFadeInset - x) / EdgeFadeWidth, 0, 1);

    /// <summary>The day under <paramref name="x"/>: <c>round((x − L) / (R − L) · (n − 1))</c>, clamped to [0, n − 1].</summary>
    public static int NearestIndex(double x, double left, double right, int count)
    {
        if (count <= 1 || right <= left)
        {
            return 0;
        }
        var i = (int)Math.Round((x - left) / (right - left) * (count - 1), MidpointRounding.AwayFromZero);
        return Math.Clamp(i, 0, count - 1);
    }

    /// <summary>
    /// The card sparkline's samples, oldest first: daily over the last <paramref name="days"/> days (30 → 30 points), or every
    /// <paramref name="step"/> days back from the last one over <paramref name="days"/> + 1 days (364 / 7 → 53 points).
    /// </summary>
    public static double[] SparklineSamples(IReadOnlyList<double> values, int days, int step)
    {
        if (values.Count == 0 || days < 1)
        {
            return [];
        }
        step = Math.Max(1, step);
        var span = step == 1 ? days - 1 : days;
        span = Math.Min(span, values.Count - 1);
        var count = span / step + 1;
        var result = new double[count];
        var last = values.Count - 1;
        for (var i = 0; i < count; i++)
        {
            result[i] = values[last - (count - 1 - i) * step];
        }
        return result;
    }

    /// <summary>
    /// The smallest and largest value the chart draws for a range: the current period and the previous one, both with
    /// their lead-in day (<c>k</c> = 0 … 2·days days back), limited to the series.
    /// </summary>
    public static (double Min, double Max) WindowExtent(IReadOnlyList<double> values, int days, bool includePrevious)
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        var last = values.Count - 1;
        var reach = includePrevious ? 2 * days : days;
        for (var k = 0; k <= reach && last - k >= 0; k++)
        {
            var v = values[last - k];
            if (!double.IsFinite(v))
            {
                continue;
            }
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        if (double.IsPositiveInfinity(min))
        {
            return (0, 1);
        }
        return (min, max);
    }

    /// <summary>The value <paramref name="daysBack"/> days before the last one (fractional days interpolate); NaN outside.</summary>
    public static double At(IReadOnlyList<double> values, double daysBack)
    {
        var last = values.Count - 1;
        var position = last - daysBack;
        if (position < 0 || position > last)
        {
            return double.NaN;
        }
        var i = (int)Math.Floor(position);
        var t = position - i;
        if (t < 1e-9 || i >= last)
        {
            return values[i];
        }
        return values[i] + (values[i + 1] - values[i]) * t;
    }

    /// <summary>"Aug 24 – Sep 22": the first and the last day shown (with years for ranges over 200 days).</summary>
    public static string RangeText(DateTime end, int days)
    {
        // A range that long reaches back into the previous year: name the years.
        var pattern = days > 200 ? "MMM d, yyyy" : "MMM d";
        return end.AddDays(-(days - 1)).ToString(pattern, s_culture) + " – " + end.ToString(pattern, s_culture);
    }

    #endregion
}
