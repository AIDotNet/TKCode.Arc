using System.Globalization;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The UsageForecast demos' data (all invented, seeded and deterministic): the billing cycle Jun 1 to Jun 30, 2027 of a
/// small studio, read on Monday Jun 21 (day 21): events sent so far (a running total in millions), database storage (a
/// level in GB with one clean-up on day 9) and editor seats (a level that grows by a seat now and then).
/// </summary>
public static class UsageForecastDemoData
{
    public const string Title = "Usage outlook";

    public const string Subtitle = "Harborline Studio · Growth plan · Renews Jul 1";

    public static readonly DateOnly Start = new(2027, 6, 1);

    /// <summary>Events, database and editors.</summary>
    public static UsageMetric[] Metrics() => [Events(), Database(), Editors()];

    /// <summary>Events sent so far, in millions: weekdays about 1.35M, weekends about 0.52M. Budget 26M, $150 per million over it.</summary>
    public static UsageMetric Events()
    {
        var random = new Mulberry32(7);
        var readings = new double[21];
        var sum = 0.0;
        for (var d = 1; d <= 21; d++)
        {
            var weekend = Start.AddDays(d - 1).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            sum += weekend ? random.Gaussian(0.52, 0.06) : random.Gaussian(1.35, 0.12);
            readings[d - 1] = Math.Round(sum, 3);
        }
        return new UsageMetric
        {
            Key = "events",
            Label = "Events",
            Readings = readings,
            StartValue = 0,
            AxisMinimum = 0,
            AxisMaximum = 40,
            BudgetMinimum = 8,
            BudgetMaximum = 40,
            BudgetStep = 0.5,
            Budget = 26,
            UnitPrice = 150,
            PriceNote = "At $0.15 per 1k events",
            Unit = "M",
            ValueFormatter = v => v.ToString("0.#", CultureInfo.InvariantCulture)
        };
    }

    /// <summary>Database storage in GB: starts at 610, grows about 4.5 a day, one clean-up on day 9. Budget 760 GB, $0.40 per GB over it.</summary>
    public static UsageMetric Database()
    {
        var random = new Mulberry32(11);
        var readings = new double[21];
        var level = 610.0;
        for (var d = 1; d <= 21; d++)
        {
            level += d == 9 ? -28 : random.Gaussian(4.5, 3.0);
            readings[d - 1] = Math.Round(level, 1);
        }
        return new UsageMetric
        {
            Key = "database",
            Label = "Database",
            Readings = readings,
            AxisMinimum = 550,
            AxisMaximum = 800,
            BudgetMinimum = 600,
            BudgetMaximum = 800,
            BudgetStep = 5,
            Budget = 760,
            UnitPrice = 0.4,
            PriceNote = "At $0.40 per GB",
            Unit = "GB",
            UnitSeparator = " "
        };
    }

    /// <summary>Editor seats: 64 at the start, one or two more every few days. Budget 70, $18 per seat over it.</summary>
    public static UsageMetric Editors()
    {
        var readings = new double[21];
        var count = 64.0;
        for (var d = 1; d <= 21; d++)
        {
            if (d % 3 == 0 || d % 7 == 0)
            {
                count += d % 2 == 0 ? 2 : 1;
            }
            readings[d - 1] = count;
        }
        return new UsageMetric
        {
            Key = "editors",
            Label = "Editors",
            Readings = readings,
            AxisMinimum = 50,
            AxisMaximum = 80,
            BudgetMinimum = 55,
            BudgetMaximum = 80,
            BudgetStep = 1,
            Budget = 70,
            UnitPrice = 18,
            PriceNote = "At $18 per editor",
            Unit = "editors",
            UnitSeparator = " ",
            ValueFormatter = v => v.ToString("0", CultureInfo.InvariantCulture)
        };
    }

    /// <summary>mulberry32 (the Showpiece demos' generator) with Box–Muller normals.</summary>
    private sealed class Mulberry32(uint seed)
    {
        private uint _state = seed;
        private double? _spare;

        public double Next()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                var t = _state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public double Gaussian(double mean, double sd)
        {
            if (_spare is { } spare)
            {
                _spare = null;
                return mean + sd * spare;
            }
            double u, v, s;
            do
            {
                u = Next() * 2 - 1;
                v = Next() * 2 - 1;
                s = u * u + v * v;
            }
            while (s is >= 1 or 0);
            var scale = Math.Sqrt(-2 * Math.Log(s) / s);
            _spare = v * scale;
            return mean + sd * u * scale;
        }
    }
}
