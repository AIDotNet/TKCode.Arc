using System.Globalization;

namespace TKCode.Arc;

/// <summary>Maps a value domain onto a pixel range (y grows downwards when <c>RangeStart &gt; RangeEnd</c>).</summary>
internal readonly record struct ChartLinearScale(double DomainMin, double DomainMax, double RangeStart, double RangeEnd)
{
    public double Span => DomainMax - DomainMin is var span && Math.Abs(span) > 1e-12 ? span : 1;

    public double Map(double value) => RangeStart + (value - DomainMin) / Span * (RangeEnd - RangeStart);

    public double Invert(double pixel)
        => Math.Abs(RangeEnd - RangeStart) < 1e-12 ? DomainMin : DomainMin + (pixel - RangeStart) / (RangeEnd - RangeStart) * Span;
}

/// <summary>
/// Evenly sized slots for categories (bars). <see cref="Fill"/> is the share of a slot a band fills, capped at
/// <see cref="MaxBand"/> pixels so wide containers gain spacing instead of thicker bars.
/// </summary>
internal readonly record struct ChartBandScale(int Count, double RangeStart, double RangeEnd, double Fill = 0.58, double MaxBand = 28)
{
    public double Step => Count <= 0 ? 0 : (RangeEnd - RangeStart) / Count;

    public double BandWidth => Math.Min(Step * Fill, MaxBand);

    public double Center(double index) => RangeStart + (index + 0.5) * Step;

    public int IndexAt(double pixel)
        => Count <= 0 ? -1 : Math.Clamp((int)Math.Floor((pixel - RangeStart) / (Step == 0 ? 1 : Step)), 0, Count - 1);
}

/// <summary>Maps epoch milliseconds onto a pixel range.</summary>
internal readonly record struct ChartTimeScale(double Start, double End, double RangeStart, double RangeEnd)
{
    public double Span => Math.Max(1, End - Start);

    public double Map(double time) => RangeStart + (time - Start) / Span * (RangeEnd - RangeStart);

    public double Map(DateTimeOffset time) => Map(ChartTime.ToMs(time));

    public double Invert(double pixel)
        => Math.Abs(RangeEnd - RangeStart) < 1e-12 ? Start : Start + (pixel - RangeStart) / (RangeEnd - RangeStart) * Span;
}

/// <summary>Clean gridline values for a value axis.</summary>
internal readonly record struct ChartTickSet(double Min, double Max, double Step, double[] Ticks)
{
    public static readonly ChartTickSet Default = ChartTicks.Nice(0, 100);
}

/// <summary>"Nice" ticks: steps of 1, 2, 2.5 or 5 × 10ⁿ (Arc).</summary>
internal static class ChartTicks
{
    private static readonly double[] s_lineFactors = [1, 2, 2.5, 5, 10];
    private static readonly double[] s_barFactors = [1, 2, 3, 5, 10];

    /// <summary>
    /// LineChart: the smallest step of 1, 2, 2.5 or 5 that covers the data in <paramref name="maxRows"/> rows or
    /// fewer, from zero unless the data dips below it.
    /// </summary>
    public static ChartTickSet Nice(double low, double high, int maxRows = 4)
    {
        var lo = Math.Min(0, low);
        var hi = Math.Max(high, lo + 1);
        var span = hi - lo;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(span / maxRows)));
        foreach (var factor in s_lineFactors)
        {
            var step = factor * magnitude;
            var bottom = Math.Floor(lo / step) * step;
            var top = Math.Ceiling(hi / step) * step;
            if ((top - bottom) / step <= maxRows + 1e-9)
            {
                var count = (int)Math.Round((top - bottom) / step) + 1;
                var ticks = new double[count];
                for (var i = 0; i < count; i++)
                {
                    ticks[i] = Clean(bottom + i * step);
                }
                return new ChartTickSet(Clean(bottom), Clean(top), Clean(step), ticks);
            }
        }
        return new ChartTickSet(lo, hi, hi - lo, [lo, hi]);
    }

    /// <summary>BarChart: the smallest step of 1, 2, 3 or 5 from zero that covers <paramref name="max"/> in four rows or fewer.</summary>
    public static ChartTickSet FromZero(double max, int maxRows = 4)
    {
        var safe = Math.Max(max, 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(safe / maxRows)));
        foreach (var factor in s_barFactors)
        {
            var step = factor * magnitude;
            var rows = (int)Math.Ceiling(safe / step - 1e-9);
            if (rows <= maxRows)
            {
                var ticks = new double[rows + 1];
                for (var i = 0; i <= rows; i++)
                {
                    ticks[i] = Clean(i * step);
                }
                return new ChartTickSet(0, Clean(rows * step), Clean(step), ticks);
            }
        }
        return new ChartTickSet(0, safe, safe, [0, safe]);
    }

    /// <summary>BrushChart: clean gridlines from zero with steps of 1, 2, 2.5 or 5 in four rows or fewer.</summary>
    public static ChartTickSet TopFromZero(double high, int maxRows = 4)
    {
        var hi = Math.Max(high, 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(hi / maxRows)));
        foreach (var factor in s_lineFactors)
        {
            var step = factor * magnitude;
            var top = Math.Ceiling(hi / step) * step;
            if (top / step <= maxRows + 1e-9)
            {
                var count = (int)Math.Round(top / step) + 1;
                var ticks = new double[count];
                for (var i = 0; i < count; i++)
                {
                    ticks[i] = Clean(i * step);
                }
                return new ChartTickSet(0, Clean(top), Clean(step), ticks);
            }
        }
        return new ChartTickSet(0, hi, hi / maxRows, [0, hi]);
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
}

/// <summary>Time helpers: epoch milliseconds (UTC), and calendar ticks that fit a width (BrushChart).</summary>
internal static class ChartTime
{
    public const double Day = 86_400_000;
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    public static double ToMs(DateTimeOffset time) => time.ToUnixTimeMilliseconds();

    public static DateTimeOffset FromMs(double ms) => DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(ms));

    /// <summary>Converts DateTime, DateTimeOffset, DateOnly or a number (epoch ms) to epoch ms.</summary>
    public static bool TryToMs(object? value, out double ms)
    {
        switch (value)
        {
            case DateTimeOffset offset:
                ms = ToMs(offset);
                return true;
            case DateTime dateTime:
                ms = ToMs(new DateTimeOffset(dateTime.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc) : dateTime.ToUniversalTime()));
                return true;
            case DateOnly date:
                ms = ToMs(new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
                return true;
            case double d:
                ms = d;
                return true;
            case long l:
                ms = l;
                return true;
            default:
                ms = 0;
                return false;
        }
    }

    /// <summary>
    /// Calendar ticks that fit the width (at most one label per 76px): days, weeks, then months, quarters and
    /// years, each landing on a real boundary (UTC).
    /// </summary>
    public static void Ticks(double start, double end, double width, List<(double Time, string Label)> output)
    {
        output.Clear();
        var room = Math.Max(2, Math.Floor(width / 76));
        var span = end - start;
        foreach (var days in (ReadOnlySpan<int>)[1, 2, 7, 14])
        {
            if (span / (days * Day) > room)
            {
                continue;
            }
            var t = Math.Ceiling(start / Day) * Day;
            if (days >= 7)
            {
                while (FromMs(t).UtcDateTime.DayOfWeek != DayOfWeek.Monday)
                {
                    t += Day;
                }
            }
            for (; t <= end; t += days * Day)
            {
                output.Add((t, FromMs(t).UtcDateTime.ToString("MMM d", s_culture)));
            }
            return;
        }
        foreach (var months in (ReadOnlySpan<int>)[1, 2, 3, 6, 12])
        {
            if (span / (months * 30.44 * Day) > room)
            {
                continue;
            }
            var first = FromMs(start).UtcDateTime;
            var y = first.Year;
            var m = first.Month - 1 + (first.Day > 1 || first.TimeOfDay > TimeSpan.Zero ? 1 : 0);
            while (true)
            {
                y += m / 12;
                m %= 12;
                var t = ToMs(new DateTimeOffset(y, m + 1, 1, 0, 0, 0, TimeSpan.Zero));
                if (t > end)
                {
                    break;
                }
                if (m % months == 0)
                {
                    var date = FromMs(t).UtcDateTime;
                    output.Add((t, m == 0 ? date.ToString("yyyy", s_culture) : date.ToString("MMM", s_culture)));
                }
                m += 1;
            }
            return;
        }
        for (var y = FromMs(start).UtcDateTime.Year + 1; ToMs(new DateTimeOffset(y, 1, 1, 0, 0, 0, TimeSpan.Zero)) <= end; y++)
        {
            output.Add((ToMs(new DateTimeOffset(y, 1, 1, 0, 0, 0, TimeSpan.Zero)), y.ToString(s_culture)));
        }
    }
}
