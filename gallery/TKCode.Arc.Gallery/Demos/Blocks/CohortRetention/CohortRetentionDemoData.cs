namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The CohortRetention demos' data (all invented, seeded and deterministic): team spaces created each month from Jun 2026
/// to May 2027 and how many were still being edited each month after, for two plans and both together; and sixteen weekly
/// sign-up cohorts of a mobile app.
/// </summary>
public static class CohortRetentionDemoData
{
    public const string Title = "Space retention";

    public const string Subtitle = "Team spaces still editing each month after creation. Updated through Jun 30, 2027.";

    private static readonly string[] s_months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>All spaces, Starter-plan spaces and Business-plan spaces, twelve monthly cohorts each.</summary>
    public static RetentionSegment[] SpaceSegments()
    {
        var random = new Mulberry32(20270630);
        const int cohorts = 12;
        var starter = new (int Size, int[] Retained)[cohorts];
        var business = new (int Size, int[] Retained)[cohorts];
        for (var r = 0; r < cohorts; r++)
        {
            var months = cohorts - r;
            var starterSize = (int)Math.Round(260 + 9 * r + random.Gaussian(0, 18));
            var businessSize = (int)Math.Round(95 + 3 * r + random.Gaussian(0, 8));
            starter[r] = (starterSize, Curve(random, starterSize, months, 0.70 + 0.006 * r, 0.29, 0.32));
            business[r] = (businessSize, Curve(random, businessSize, months, 0.91 + 0.003 * r, 0.74, 0.22));
        }

        var all = new List<RetentionCohort>();
        var starterCohorts = new List<RetentionCohort>();
        var businessCohorts = new List<RetentionCohort>();
        for (var r = 0; r < cohorts; r++)
        {
            var (label, shortLabel) = MonthName(2026, 6 + r);
            var sum = new int[starter[r].Retained.Length];
            for (var m = 0; m < sum.Length; m++)
            {
                sum[m] = starter[r].Retained[m] + business[r].Retained[m];
            }
            all.Add(new RetentionCohort(label, starter[r].Size + business[r].Size, sum, shortLabel) { Key = $"all-{r}" });
            starterCohorts.Add(new RetentionCohort(label, starter[r].Size, starter[r].Retained, shortLabel) { Key = $"starter-{r}" });
            businessCohorts.Add(new RetentionCohort(label, business[r].Size, business[r].Retained, shortLabel) { Key = $"business-{r}" });
        }
        return
        [
            new RetentionSegment("All", all, "all spaces") { Key = "all" },
            new RetentionSegment("Starter", starterCohorts, "Starter-plan spaces") { Key = "starter" },
            new RetentionSegment("Business", businessCohorts, "Business-plan spaces") { Key = "business" },
        ];
    }

    /// <summary>Sixteen weekly sign-up cohorts (week 14 to week 29) of a mobile app, one segment.</summary>
    public static RetentionSegment[] WeeklySegments()
    {
        var random = new Mulberry32(20270714);
        const int cohorts = 16;
        var list = new List<RetentionCohort>();
        for (var r = 0; r < cohorts; r++)
        {
            var size = (int)Math.Round(140 + 4 * r + random.Gaussian(0, 12));
            var label = $"Wk {14 + r}";
            list.Add(new RetentionCohort(label, size, Curve(random, size, cohorts - r, 0.62 + 0.004 * r, 0.18, 0.25), label) { Key = $"w{14 + r}" });
        }
        return [new RetentionSegment("All installs", list, "all installs") { Key = "all" }];
    }

    // rate(m) = floor + (first − floor)·e^(−k(m − 1)) + noise, never above the month before.
    private static int[] Curve(Mulberry32 random, int size, int months, double first, double floor, double k)
    {
        var retained = new int[months];
        var previous = 1.0;
        for (var m = 1; m <= months; m++)
        {
            var rate = floor + (first - floor) * Math.Exp(-k * (m - 1)) + random.Gaussian(0, 0.008);
            rate = Math.Clamp(Math.Min(rate, previous), 0, 1);
            previous = rate;
            retained[m - 1] = (int)Math.Round(rate * size);
        }
        return retained;
    }

    private static (string Label, string Short) MonthName(int year, int month)
    {
        var y = year + (month - 1) / 12;
        var m = (month - 1) % 12;
        return ($"{s_months[m]} {y}", $"{s_months[m]} ’{y % 100:00}");
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
            while (s >= 1 || s == 0);
            var factor = Math.Sqrt(-2 * Math.Log(s) / s);
            _spare = v * factor;
            return mean + sd * u * factor;
        }
    }
}
