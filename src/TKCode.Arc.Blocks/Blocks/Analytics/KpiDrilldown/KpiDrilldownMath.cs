namespace TKCode.Arc;

/// <summary>One metric read for one range: the current and prior periods and the change between their last days.</summary>
internal sealed class KpiPeriod
{
    /// <summary>The range in days.</summary>
    public int Days { get; init; }

    /// <summary>The current period's daily values (the last <see cref="Days"/> of the series).</summary>
    public double[] Current { get; init; } = [];

    /// <summary>The prior period's daily values (the <see cref="Days"/> before; NaN where the series is too short).</summary>
    public double[] Previous { get; init; } = [];

    /// <summary>True when the prior period has at least one value.</summary>
    public bool HasPrevious { get; init; }

    /// <summary>The value on the last day (the card's value).</summary>
    public double Last { get; init; } = double.NaN;

    /// <summary>The value on the prior period's last day ("vs …").</summary>
    public double PriorLast { get; init; } = double.NaN;

    /// <summary>The change in the change format's units (percent or points); null when it cannot be computed.</summary>
    public double? Change { get; init; }

    /// <summary>The first and last dates of the current period.</summary>
    public DateOnly FirstDate { get; init; }

    /// <inheritdoc cref="FirstDate"/>
    public DateOnly LastDate { get; init; }

    /// <summary>The date of day <paramref name="day"/> of the current period (0 = first).</summary>
    public DateOnly DateOf(int day) => FirstDate.AddDays(day);
}

/// <summary>One breakdown row read for one range.</summary>
internal readonly record struct KpiBreakdownLine(string Label, double Value, double Previous, double Share, double? Change);

/// <summary>
/// The arithmetic of <see cref="KpiDrilldown"/> (notes sections 3, 4 and 8), pure so tests can pin the measured numbers:
/// periods and changes, the fixed-count resampling (40 sparkline / 72 detail points), the sparkline's normalised rows,
/// the detail domain (min / max of what is drawn, no nice values), cursor index ↔ day, the readout clamp and the
/// container breakpoints.
/// </summary>
internal static class KpiDrilldownMath
{
    /// <summary>Points of every sparkline and of every detail line, whatever the range.</summary>
    public const int SparklinePoints = 40;

    /// <inheritdoc cref="SparklinePoints"/>
    public const int DetailPoints = 72;

    /// <summary>The detail plot rows (220 high): gridlines at 28 / 103 / 178, area floor 196, cursor from 10, date baseline 214.</summary>
    public const double GridTop = 28;

    /// <inheritdoc cref="GridTop"/>
    public const double GridMiddle = 103;

    /// <inheritdoc cref="GridTop"/>
    public const double GridBottom = 178;

    /// <inheritdoc cref="GridTop"/>
    public const double AreaFloor = 196;

    /// <inheritdoc cref="GridTop"/>
    public const double CursorTop = 10;

    /// <inheritdoc cref="GridTop"/>
    public const double DateBaseline = 214;

    /// <summary>The y label column right of the plot (56), labels 10 into it.</summary>
    public const double AxisWidth = 56;

    /// <inheritdoc cref="AxisWidth"/>
    public const double AxisLabelGap = 10;

    /// <summary>The readout's centre stays this far from the plot's ends.</summary>
    public const double ReadoutClamp = 70;

    /// <summary>Three columns from this block width, two from <see cref="TwoColumnWidth"/>, one (compact) below.</summary>
    public const double ThreeColumnWidth = 760;

    /// <inheritdoc cref="ThreeColumnWidth"/>
    public const double TwoColumnWidth = 520;

    /// <summary>Reads <paramref name="values"/> for a range of <paramref name="days"/> ending on <paramref name="end"/>.</summary>
    public static KpiPeriod Period(IReadOnlyList<double>? values, int days, KpiKind kind, DateOnly end)
    {
        days = Math.Max(1, days);
        var current = new double[days];
        var previous = new double[days];
        var count = values?.Count ?? 0;
        var hasPrevious = false;
        for (var i = 0; i < days; i++)
        {
            var c = count - days + i;
            var p = c - days;
            current[i] = c >= 0 && values is not null ? values[c] : double.NaN;
            previous[i] = p >= 0 && values is not null ? values[p] : double.NaN;
            hasPrevious |= p >= 0;
        }
        // A series shorter than the range starts with its first value (the line stays defined end to end).
        FillLeading(current);
        if (hasPrevious)
        {
            FillLeading(previous);
        }
        var last = current[^1];
        var priorLast = previous[^1];
        return new KpiPeriod
        {
            Days = days,
            Current = current,
            Previous = previous,
            HasPrevious = hasPrevious,
            Last = last,
            PriorLast = priorLast,
            Change = ChangeOf(last, priorLast, kind),
            FirstDate = end.AddDays(-(days - 1)),
            LastDate = end
        };
    }

    /// <summary>The change from <paramref name="previous"/> to <paramref name="current"/>: percent (Total) or points (Rate).</summary>
    public static double? ChangeOf(double current, double previous, KpiKind kind)
    {
        if (!double.IsFinite(current) || !double.IsFinite(previous))
        {
            return null;
        }
        if (kind == KpiKind.Rate)
        {
            return current - previous;
        }
        if (Math.Abs(previous) < 1e-12)
        {
            return null;
        }
        return (current - previous) / Math.Abs(previous) * 100;
    }

    /// <summary>Linear resampling onto <c>destination.Length</c> evenly spaced points, first and last kept.</summary>
    public static void Resample(ReadOnlySpan<double> source, Span<double> destination) =>
        AnalyticsCurves.ResampleLinear(source, destination);

    /// <summary>Min-max normalisation of a sparkline (0 = lowest, 1 = highest; a flat line sits at 0.5).</summary>
    public static void Normalize(ReadOnlySpan<double> source, Span<double> destination)
    {
        var (min, max) = Extent(source, ReadOnlySpan<double>.Empty);
        var span = max - min;
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = !double.IsFinite(source[i]) || span <= 1e-12 ? 0.5 : (source[i] - min) / span;
        }
    }

    /// <summary>A sparkline row in pixels: viewBox 100 × 32 stretched to <paramref name="height"/>, 2 units of air above and below.</summary>
    public static double SparkY(double normalized, double height) => (2 + (1 - normalized) * 28) / 32 * height;

    /// <summary>
    /// The detail domain: [min, max] of the current period and, with <paramref name="compare"/>, the prior one. No nice
    /// values (notes 4). A flat domain is opened by 1% (or 1) so the line sits in the middle.
    /// </summary>
    public static (double Min, double Max) Domain(ReadOnlySpan<double> current, ReadOnlySpan<double> previous, bool compare)
    {
        var (min, max) = Extent(current, compare ? previous : ReadOnlySpan<double>.Empty);
        if (!double.IsFinite(min))
        {
            return (0, 1);
        }
        if (max - min <= 1e-12)
        {
            var pad = Math.Abs(max) > 1e-9 ? Math.Abs(max) * 0.01 : 1;
            return (min - pad, max + pad);
        }
        return (min, max);
    }

    /// <summary>Plot row of <paramref name="value"/> in a domain: max → 28, min → 178.</summary>
    public static double PlotY(double value, double min, double max)
    {
        var span = max - min;
        var t = Math.Abs(span) < 1e-12 ? 0.5 : (value - min) / span;
        return GridBottom - t * (GridBottom - GridTop);
    }

    /// <summary>The day under resampled point <paramref name="index"/> (0 … points − 1) of a <paramref name="days"/>-day period.</summary>
    public static int DayOfIndex(int index, int points, int days) =>
        points < 2 || days < 2 ? Math.Max(0, days - 1) : (int)Math.Round((double)index * (days - 1) / (points - 1), MidpointRounding.AwayFromZero);

    /// <summary>The resampled point nearest to day <paramref name="day"/>.</summary>
    public static int IndexOfDay(int day, int points, int days) =>
        points < 2 || days < 2 ? Math.Max(0, points - 1) : (int)Math.Round((double)day * (points - 1) / (days - 1), MidpointRounding.AwayFromZero);

    /// <summary>The resampled point nearest to <paramref name="x"/> in a plot <paramref name="plotWidth"/> wide (clamped; right of the plot = the last).</summary>
    public static int NearestIndex(double x, double plotWidth, int points)
    {
        if (points < 2 || plotWidth <= 0)
        {
            return Math.Max(0, points - 1);
        }
        var step = plotWidth / (points - 1);
        return Math.Clamp((int)Math.Round(x / step, MidpointRounding.AwayFromZero), 0, points - 1);
    }

    /// <summary>The readout's centre for a cursor at <paramref name="x"/>: clamped 70 from each end of the plot.</summary>
    public static double ReadoutCenter(double x, double plotWidth) =>
        plotWidth <= 2 * ReadoutClamp ? plotWidth / 2 : Math.Clamp(x, ReadoutClamp, plotWidth - ReadoutClamp);

    /// <summary>The middle date tick's day: ⌊(n − 1) / 2⌋.</summary>
    public static int MiddleTickDay(int days) => Math.Max(0, (days - 1) / 2);

    /// <summary>Card columns for a block <paramref name="width"/>: 3 from 760, 2 from 520, else 1.</summary>
    public static int Columns(double width) => width >= ThreeColumnWidth ? 3 : width >= TwoColumnWidth ? 2 : 1;

    /// <summary>The compact (phone) layout applies below 520.</summary>
    public static bool IsCompact(double width) => width < TwoColumnWidth;

    /// <summary>
    /// Reads a breakdown for a range: each row's value on the last day and on the prior period's last day (or its fixed
    /// numbers), its share of the rows' total and its change (percent for Total metrics, points for Rate ones).
    /// </summary>
    public static List<KpiBreakdownLine> Breakdown(KpiBreakdown? breakdown, int days, KpiKind kind)
    {
        var lines = new List<KpiBreakdownLine>();
        if (breakdown is null)
        {
            return lines;
        }
        double total = 0;
        foreach (var row in breakdown.Rows)
        {
            double value, previous;
            if (row.Values is { Count: > 0 } series)
            {
                value = series[^1];
                var p = series.Count - 1 - days;
                previous = p >= 0 ? series[p] : double.NaN;
            }
            else
            {
                value = row.Value;
                previous = row.PreviousValue;
            }
            if (double.IsFinite(value) && value > 0)
            {
                total += value;
            }
            lines.Add(new KpiBreakdownLine(row.Label, value, previous, 0, ChangeOf(value, previous, kind)));
        }
        for (var i = 0; i < lines.Count; i++)
        {
            var share = total > 0 && double.IsFinite(lines[i].Value) ? Math.Max(0, lines[i].Value) / total : 0;
            lines[i] = lines[i] with { Share = share };
        }
        return lines;
    }

    private static (double Min, double Max) Extent(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        foreach (var v in a)
        {
            if (double.IsFinite(v))
            {
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
        }
        foreach (var v in b)
        {
            if (double.IsFinite(v))
            {
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
        }
        return double.IsFinite(min) ? (min, max) : (double.NaN, double.NaN);
    }

    private static void FillLeading(double[] values)
    {
        var first = Array.FindIndex(values, double.IsFinite);
        if (first <= 0)
        {
            return;
        }
        for (var i = 0; i < first; i++)
        {
            values[i] = values[first];
        }
    }
}
