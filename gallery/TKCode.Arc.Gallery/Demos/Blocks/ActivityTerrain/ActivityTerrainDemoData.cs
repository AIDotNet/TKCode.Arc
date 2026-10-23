namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// An invented week for the activity terrain demos: visits, builds and incidents per hour for Mar 2 to 8, 2026,
/// generated from a fixed seed (mulberry32 20260302) so every run shows the same week. Weekdays peak late morning and
/// mid-afternoon with a small evening tail; weekends are quieter and flatter; builds happen on weekday afternoons; the
/// incidents are low except one spike on Wednesday afternoon.
/// </summary>
public static class ActivityTerrainDemoData
{
    public const string Title = "Weekly rhythm";
    public const string Subtitle = "Hourly visits, builds and incidents for Mar 2 to 8, 2026, server time.";

    private static readonly double[] DayWeight = [1.00, 1.07, 1.10, 1.03, 0.89, 0.31, 0.26];
    private static readonly double[] BuildDayWeight = [1, 1.03, 1.05, 1.0, 0.82, 0.08, 0.05];

    /// <summary>The demo's three metrics: visits, builds and incidents (red, a rise is bad news).</summary>
    public static IReadOnlyList<ActivityTerrainMetric> Metrics(uint seed = 20260302)
    {
        var random = new Mulberry32(seed);
        var visits = new double[168];
        var builds = new double[168];
        var incidents = new double[168];
        for (var d = 0; d < 7; d++)
        {
            for (var h = 0; h < 24; h++)
            {
                var i = d * 24 + h;
                var shape = Shape(h);
                var visit = 4600 * DayWeight[d] * shape * Math.Exp(0.045 * random.NextGaussian());
                if (d >= 5)
                {
                    visit *= 0.8 + 0.4 * shape;
                }
                visits[i] = Math.Round(visit);
                var buildShape = Math.Exp(-Math.Pow((h - 14.8) / 3.2, 2));
                builds[i] = Math.Round(33 * BuildDayWeight[d] * buildShape * Math.Exp(0.18 * random.NextGaussian()));
                incidents[i] = Math.Round(2.2 * DayWeight[d] * shape * Math.Exp(0.35 * random.NextGaussian()));
            }
        }
        // One bad afternoon: an incident on Wednesday at 14:00 that tails off over two hours.
        incidents[2 * 24 + 14] += 620;
        incidents[2 * 24 + 15] += 280;
        incidents[2 * 24 + 16] += 110;
        return
        [
            new ActivityTerrainMetric
            {
                Key = "visits", Label = "Visits", Unit = "visits",
                Values = visits, PreviousValues = Previous(visits, random, 0.06, 0.07)
            },
            new ActivityTerrainMetric
            {
                Key = "builds", Label = "Builds", Unit = "builds",
                Values = builds, PreviousValues = Previous(builds, random, 0.06, 0.07)
            },
            new ActivityTerrainMetric
            {
                Key = "incidents", Label = "Incidents", Unit = "incidents", Tone = ActivityTerrainTone.Danger, HigherIsBetter = false,
                Values = incidents, PreviousValues = Previous(incidents, random, 0.15, 0.2)
            }
        ];
    }

    /// <summary>A week of visits only, for a seed (the live data demo draws a new week each time).</summary>
    public static double[] Visits(uint seed) => (double[])Metrics(seed)[0].Values!;

    /// <summary>Two peaks a day (late morning, mid-afternoon) and a small evening tail.</summary>
    private static double Shape(int h) =>
        0.035 + 0.88 * Math.Exp(-Math.Pow((h - 10.4) / 2.1, 2)) + 0.82 * Math.Exp(-Math.Pow((h - 15.2) / 2.5, 2)) +
        0.20 * Math.Exp(-Math.Pow((h - 20.8) / 1.9, 2));

    /// <summary>The week before: each hour this week's value divided by 1 + a growth drawn per hour.</summary>
    private static double[] Previous(double[] values, Mulberry32 random, double mean, double deviation)
    {
        var previous = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var growth = mean + deviation * random.NextGaussian();
            previous[i] = Math.Round(values[i] / Math.Max(0.2, 1 + growth));
        }
        return previous;
    }

    /// <summary>mulberry32: a small seeded generator, with Box–Muller normals (drawn in pairs).</summary>
    private sealed class Mulberry32(uint seed)
    {
        private uint _state = seed;
        private double? _spare;

        public double NextDouble()
        {
            unchecked
            {
                _state += 0x6D2B79F5;
                var t = _state;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public double NextGaussian()
        {
            if (_spare is { } spare)
            {
                _spare = null;
                return spare;
            }
            var u1 = 1 - NextDouble();
            var u2 = NextDouble();
            var r = Math.Sqrt(-2 * Math.Log(u1));
            _spare = r * Math.Sin(2 * Math.PI * u2);
            return r * Math.Cos(2 * Math.PI * u2);
        }
    }
}
