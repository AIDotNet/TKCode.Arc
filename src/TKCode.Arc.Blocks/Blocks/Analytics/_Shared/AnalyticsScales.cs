namespace TKCode.Arc;

/// <summary>
/// Maps a value domain onto a pixel range. A y axis passes the bottom pixel as <see cref="RangeStart"/> and the top pixel
/// as <see cref="RangeEnd"/> (y grows downwards). A zero-width domain maps everything to <see cref="RangeStart"/>.
/// </summary>
/// <param name="DomainMin">Value mapped to <see cref="RangeStart"/>.</param>
/// <param name="DomainMax">Value mapped to <see cref="RangeEnd"/>.</param>
/// <param name="RangeStart">Pixel of <see cref="DomainMin"/>.</param>
/// <param name="RangeEnd">Pixel of <see cref="DomainMax"/>.</param>
internal readonly record struct AnalyticsLinearScale(double DomainMin, double DomainMax, double RangeStart, double RangeEnd)
{
    /// <summary>Domain width (1 when the domain is empty, so <see cref="Map"/> never divides by zero).</summary>
    public double Span => DomainMax - DomainMin is var span && Math.Abs(span) > 1e-12 ? span : 1;

    /// <summary>Pixel of <paramref name="value"/> (not clamped).</summary>
    public double Map(double value) => RangeStart + (value - DomainMin) / Span * (RangeEnd - RangeStart);

    /// <summary>Value at <paramref name="pixel"/> (not clamped).</summary>
    public double Invert(double pixel)
        => Math.Abs(RangeEnd - RangeStart) < 1e-12 ? DomainMin : DomainMin + (pixel - RangeStart) / (RangeEnd - RangeStart) * Span;
}

/// <summary>
/// Points spread evenly over a pixel range, the first on <see cref="RangeStart"/> and the last on <see cref="RangeEnd"/>
/// (Arc's <c>x = i / (n − 1) · W</c>: Metrics dashboard, KPI drilldown, Usage forecast, Uptime latency). One point sits in
/// the middle of the range.
/// </summary>
/// <param name="Count">Number of points.</param>
/// <param name="RangeStart">Pixel of the first point.</param>
/// <param name="RangeEnd">Pixel of the last point.</param>
internal readonly record struct AnalyticsPointScale(int Count, double RangeStart, double RangeEnd)
{
    /// <summary>Distance between neighbouring points (0 for fewer than two points).</summary>
    public double Step => Count < 2 ? 0 : (RangeEnd - RangeStart) / (Count - 1);

    /// <summary>Pixel of point <paramref name="index"/> (fractional indexes interpolate).</summary>
    public double Map(double index) => Count < 2 ? (RangeStart + RangeEnd) / 2 : RangeStart + index * Step;

    /// <summary>
    /// The nearest point to <paramref name="pixel"/>, clamped to the points: <c>round(x / W · (n − 1))</c>, switching
    /// exactly half way between two points (measured in Metrics dashboard). −1 when there are no points.
    /// </summary>
    public int NearestIndex(double pixel)
    {
        if (Count <= 0)
        {
            return -1;
        }
        if (Count == 1 || Math.Abs(Step) < 1e-12)
        {
            return 0;
        }
        return Math.Clamp((int)Math.Round((pixel - RangeStart) / Step, MidpointRounding.AwayFromZero), 0, Count - 1);
    }
}

/// <summary>
/// Evenly sized slots for categories (waterfall columns, uptime days, cohort columns). <see cref="Center"/> is the middle
/// of a slot; <see cref="IndexAt"/> is the slot under a pixel (clamped).
/// </summary>
/// <param name="Count">Number of slots.</param>
/// <param name="RangeStart">Left edge of the first slot.</param>
/// <param name="RangeEnd">Right edge of the last slot.</param>
internal readonly record struct AnalyticsBandScale(int Count, double RangeStart, double RangeEnd)
{
    /// <summary>Slot width.</summary>
    public double Step => Count <= 0 ? 0 : (RangeEnd - RangeStart) / Count;

    /// <summary>Left edge of slot <paramref name="index"/>.</summary>
    public double Start(double index) => RangeStart + index * Step;

    /// <summary>Middle of slot <paramref name="index"/>.</summary>
    public double Center(double index) => RangeStart + (index + 0.5) * Step;

    /// <summary>The slot under <paramref name="pixel"/>, clamped to the slots; −1 when there are none.</summary>
    public int IndexAt(double pixel)
        => Count <= 0 ? -1 : Math.Clamp((int)Math.Floor((pixel - RangeStart) / (Step == 0 ? 1 : Step)), 0, Count - 1);
}

/// <summary>Value-axis gridlines: the domain they cover and the tick values (bottom to top unless stated).</summary>
/// <param name="Min">Domain bottom.</param>
/// <param name="Max">Domain top.</param>
/// <param name="Step">Distance between ticks (0 for irregular sets such as <see cref="AnalyticsTicks.MinMidMax"/>).</param>
/// <param name="Ticks">Tick values.</param>
internal readonly record struct AnalyticsTickSet(double Min, double Max, double Step, double[] Ticks)
{
    /// <summary>Decimals a label needs to show every tick exactly (see <see cref="AnalyticsTicks.DecimalsFor"/>).</summary>
    public int Decimals => AnalyticsTicks.DecimalsFor(Step);
}

/// <summary>
/// Tick rules measured in the Analytics notes. Every rule returns clean values (no 0.30000000000000004) and is pure, so
/// blocks unit-test their axes. Ladders are the step factors per power of ten.
/// </summary>
internal static class AnalyticsTicks
{
    /// <summary>Arc's usual ladder: 1, 2, 2.5, 5 × 10ⁿ.</summary>
    public static readonly double[] Ladder = [1, 2, 2.5, 5];

    /// <summary>Metric explorer's finer ladder: 1, 1.5, 2, 2.5, 3, 4, 5 × 10ⁿ.</summary>
    public static readonly double[] FineLadder = [1, 1.5, 2, 2.5, 3, 4, 5];

    /// <summary>The smallest ladder step ≥ <paramref name="value"/> (<c>niceCeil</c>); 1 for non-positive values.</summary>
    public static double NiceCeiling(double value, IReadOnlyList<double>? ladder = null)
    {
        ladder ??= Ladder;
        if (!(value > 0) || double.IsInfinity(value))
        {
            return 1;
        }
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        for (var round = 0; round < 2; round++)
        {
            foreach (var factor in ladder)
            {
                var step = Clean(factor * magnitude);
                if (step >= value * (1 - 1e-9))
                {
                    return step;
                }
            }
            magnitude *= 10;
        }
        return Clean(magnitude);
    }

    /// <summary>The ladder step nearest to <paramref name="value"/> (linear distance; MRR waterfall's span / 6).</summary>
    public static double NiceNearest(double value, IReadOnlyList<double>? ladder = null)
    {
        ladder ??= Ladder;
        if (!(value > 0) || double.IsInfinity(value))
        {
            return 1;
        }
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        var best = magnitude;
        var bestDistance = double.MaxValue;
        foreach (var scale in (ReadOnlySpan<double>)[magnitude, magnitude * 10])
        {
            foreach (var factor in ladder)
            {
                var step = Clean(factor * scale);
                var distance = Math.Abs(step - value);
                if (distance < bestDistance - 1e-12)
                {
                    best = step;
                    bestDistance = distance;
                }
            }
        }
        return best;
    }

    /// <summary>The ladder step after <paramref name="step"/> (2 → 2.5, 5 → 10).</summary>
    public static double NextStep(double step, IReadOnlyList<double>? ladder = null) => NiceCeiling(step * (1 + 1e-6), ladder);

    /// <summary>
    /// The general nice axis (LineChart's rule): the smallest ladder step whose floor/ceil cover of [low, high] has at most
    /// <paramref name="maxRows"/> rows; zero is included unless <paramref name="includeZero"/> is false. Gives 4–5 ticks.
    /// </summary>
    public static AnalyticsTickSet Nice(double low, double high, int maxRows = 4, bool includeZero = true)
    {
        var lo = includeZero ? Math.Min(0, low) : low;
        var hi = includeZero ? Math.Max(0, high) : high;
        if (!(hi > lo))
        {
            hi = lo + 1;
        }
        maxRows = Math.Max(1, maxRows);
        var step = NiceCeiling((hi - lo) / maxRows);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var bottom = Math.Floor(lo / step + 1e-9) * step;
            var top = Math.Ceiling(hi / step - 1e-9) * step;
            if ((top - bottom) / step <= maxRows + 1e-9)
            {
                return Build(bottom, step, (int)Math.Round((top - bottom) / step));
            }
            step = NextStep(step);
        }
        return Build(lo, hi - lo, 1);
    }

    /// <summary>
    /// Metrics dashboard, count metrics: zero baseline and exactly <paramref name="rows"/> rows,
    /// <c>step = niceCeil(max / rows)</c>, top = rows · step (measured: 1374 → 0–1.5K step 500, 63 → 0–75, 431 → 0–600,
    /// 8855 → 0–15K).
    /// </summary>
    public static AnalyticsTickSet FixedRowsFromZero(double max, int rows = 3)
    {
        rows = Math.Max(1, rows);
        var step = NiceCeiling(Math.Max(max, 0) / rows);
        return Build(0, step, rows);
    }

    /// <summary>
    /// Metrics dashboard, ratio metrics (our rule; Arc's lets the line escape the plot): exactly <paramref name="rows"/>
    /// rows, <c>step = niceCeil(span / rows · headroom)</c>, <c>lo = floor(min / step) · step</c>, <c>hi = lo + rows · step</c>;
    /// when that still misses <paramref name="max"/> the step moves one ladder rung up.
    /// </summary>
    public static AnalyticsTickSet FixedRows(double min, double max, int rows = 3, double headroom = 1.2)
    {
        rows = Math.Max(1, rows);
        if (max < min)
        {
            (min, max) = (max, min);
        }
        var span = max - min;
        var step = NiceCeiling(span > 0 ? span / rows * headroom : Math.Max(Math.Abs(max), 1) * 0.1);
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var lo = Math.Floor(min / step + 1e-9) * step;
            if (lo + rows * step >= max - 1e-9)
            {
                return Build(lo, step, rows);
            }
            step = NextStep(step);
        }
        return Build(min, Math.Max(span, 1) / rows, rows);
    }

    /// <summary>
    /// Metric explorer: the data range (current + previous series) padded by <paramref name="padding"/> (3%) each side,
    /// exactly <paramref name="rows"/> (4) rows; candidates from <see cref="FineLadder"/> starting at the smallest step ≥
    /// padded span / rows; <c>lo = floor(paddedMin / step) · step</c>; the first candidate whose
    /// <c>lo + rows · step</c> reaches the padded max wins (fitted to 12 observations, e.g. [2150, 2350] step 50).
    /// </summary>
    public static AnalyticsTickSet PaddedRows(double min, double max, int rows = 4, double padding = 0.03)
    {
        rows = Math.Max(1, rows);
        if (max < min)
        {
            (min, max) = (max, min);
        }
        var span = max - min;
        if (span <= 0)
        {
            span = Math.Max(Math.Abs(max), 1) * 0.1;
        }
        var lowPad = min - span * padding;
        var highPad = max + span * padding;
        var step = NiceCeiling((highPad - lowPad) / rows, FineLadder);
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var lo = Math.Floor(lowPad / step + 1e-9) * step;
            if (lo + rows * step >= highPad - 1e-9)
            {
                return Build(lo, step, rows);
            }
            step = NextStep(step, FineLadder);
        }
        return Build(lowPad, (highPad - lowPad) / rows, rows);
    }

    /// <summary>
    /// MRR waterfall: <c>lo = min − 0.6 · span</c>, <c>hi = max + 0.05 · span</c>, step = the ladder value nearest to
    /// (hi − lo) / <paramref name="targetRows"/> (6), then floor / ceil to the step (7–9 ticks in all 8 observations).
    /// </summary>
    public static AnalyticsTickSet Waterfall(double min, double max, double below = 0.6, double above = 0.05, int targetRows = 6)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }
        var span = max - min;
        if (span <= 0)
        {
            span = Math.Max(Math.Abs(max), 1) * 0.1;
        }
        var rawLo = min - below * span;
        var rawHi = max + above * span;
        var step = NiceNearest((rawHi - rawLo) / Math.Max(1, targetRows));
        var lo = Math.Floor(rawLo / step + 1e-9) * step;
        var hi = Math.Ceiling(rawHi / step - 1e-9) * step;
        return Build(lo, step, (int)Math.Round((hi - lo) / step));
    }

    /// <summary>
    /// Uptime latency: zero to <c>ceil(raw / step) · step</c> where <c>raw = max · headroom</c> (1.2) and step is the
    /// smallest ladder value ≥ raw / <paramref name="rows"/> (5): 4–6 ticks (measured 500/100, 1250/250, 2500/500).
    /// </summary>
    public static AnalyticsTickSet Headroom(double max, double headroom = 1.2, int rows = 5)
    {
        var raw = Math.Max(max, 0) * headroom;
        if (raw <= 0)
        {
            raw = 1;
        }
        var step = NiceCeiling(raw / Math.Max(1, rows));
        var top = Math.Ceiling(raw / step - 1e-9) * step;
        return Build(0, step, (int)Math.Round(top / step));
    }

    /// <summary>
    /// Revenue explorer: ticks inside a fixed domain [<paramref name="lo"/>, <paramref name="hi"/>] (the domain is not
    /// widened); the smallest ladder step that leaves at most <c>height / rowPixels + 1</c> ticks (34px a row), then
    /// ticks closer than <paramref name="topGap"/> px to the top are dropped. Arc's own rule jumps (170k → 25k, 172k →
    /// 50k); this is the documented estimate.
    /// </summary>
    public static AnalyticsTickSet FitHeight(double lo, double hi, double height, double rowPixels = 34, double topGap = 8)
    {
        if (hi < lo)
        {
            (lo, hi) = (hi, lo);
        }
        var span = hi - lo;
        if (span <= 0 || height <= 0)
        {
            return new AnalyticsTickSet(lo, hi, 0, [Clean(lo)]);
        }
        var maxTicks = Math.Max(2, (int)Math.Floor(height / rowPixels) + 1);
        var step = NiceCeiling(span / maxTicks);
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var first = Math.Ceiling(lo / step - 1e-9);
            var last = Math.Floor(hi / step + 1e-9);
            if (last - first + 1 <= maxTicks)
            {
                var list = new List<double>();
                for (var k = first; k <= last; k++)
                {
                    var value = Clean(k * step);
                    var fromTop = (hi - value) / span * height;
                    if (fromTop >= topGap || value == 0)
                    {
                        list.Add(value);
                    }
                }
                return new AnalyticsTickSet(lo, hi, Clean(step), list.ToArray());
            }
            step = NextStep(step);
        }
        return new AnalyticsTickSet(lo, hi, span, [Clean(lo), Clean(hi)]);
    }

    /// <summary>
    /// KPI drilldown: no nice values; the domain is the data's [min, max] and the three ticks are max, mean of the two,
    /// and min (top to bottom), labelled in the metric's own format.
    /// </summary>
    public static AnalyticsTickSet MinMidMax(double min, double max)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }
        return new AnalyticsTickSet(min, max, 0, [max, (min + max) / 2, min]);
    }

    /// <summary>
    /// Decimals needed to write <paramref name="step"/> exactly (0.25 → 2, 2.5 → 1, 500 → 0), at most 6: labels of a
    /// stepped axis use it so 42.5 is not shown as "43".
    /// </summary>
    public static int DecimalsFor(double step)
    {
        if (!(step > 0) || double.IsInfinity(step))
        {
            return 0;
        }
        step = Clean(step);
        for (var d = 0; d <= 6; d++)
        {
            var scaled = step * Math.Pow(10, d);
            if (Math.Abs(scaled - Math.Round(scaled)) < 1e-6 * Math.Max(1, scaled))
            {
                return d;
            }
        }
        return 6;
    }

    /// <summary>Rounds away floating noise (0.30000000000000004 → 0.3), like <c>Number(v.toPrecision(12))</c>.</summary>
    public static double Clean(double value)
    {
        if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            return value == 0 ? 0 : value;
        }
        var digits = (int)Math.Ceiling(Math.Log10(Math.Abs(value)));
        var places = Math.Clamp(12 - digits, 0, 15);
        return Math.Round(value, places);
    }

    private static AnalyticsTickSet Build(double bottom, double step, int rows)
    {
        rows = Math.Max(1, rows);
        var ticks = new double[rows + 1];
        for (var i = 0; i <= rows; i++)
        {
            ticks[i] = Clean(bottom + i * step);
        }
        return new AnalyticsTickSet(Clean(bottom), Clean(bottom + rows * step), Clean(step), ticks);
    }
}

/// <summary>Pixel snapping for 1px strokes (Arc's <c>shape-rendering: crispEdges</c> and its +0.5 offsets).</summary>
internal static class AnalyticsPixels
{
    /// <summary>The centre of the device row a 1px horizontal line at <paramref name="y"/> covers: <c>round(y) + 0.5</c>.</summary>
    public static double Crisp(double y) => Math.Round(y, MidpointRounding.AwayFromZero) + 0.5;

    /// <summary>Snaps to whole pixels (Arc writes tick translations and bar edges as integers).</summary>
    public static double Snap(double v) => Math.Round(v, MidpointRounding.AwayFromZero);
}
