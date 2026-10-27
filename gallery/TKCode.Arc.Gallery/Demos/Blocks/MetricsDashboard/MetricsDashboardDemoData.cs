namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The MetricsDashboard demos' data, all invented and deterministic (mulberry32, seed 20261018): 180 days ending on
/// 18 October 2026 for a made-up site, so every range (7, 30, 90 days) has a previous period to compare with. Sessions
/// rise slowly (about 35% over 90 days) with a weekday rhythm; trials follow at about 4.6%; activation drifts on a 23-day wave; churn risk on a
/// 17-day wave. The last week is arranged to dip so that 7D shows falling (red) counts and a falling (green) churn risk,
/// while 30D and 90D show growth. A second set describes a product's usage instead.
/// </summary>
public static class MetricsDashboardDemoData
{
    public const string Title = "Insights";

    public const string Source = "northwind.dev";

    public static readonly DateOnly EndDate = new(2026, 10, 18);

    private const int Days = 180;

    /// <summary>The four web metrics: two counts, two rates (churn risk is better when lower).</summary>
    public static MetricsDashboardMetric[] SiteMetrics()
    {
        var random = new Mulberry32(20261018);
        var sessions = new double[Days];
        var trials = new double[Days];
        var activation = new double[Days];
        var churn = new double[Days];
        for (var d = 0; d < Days; d++)
        {
            var noise = random.Signed();
            sessions[d] = 840 * (1 + 0.0035 * (d - 90)) * (1 + 0.14 * Weekday(d)) * (1 + 0.06 * noise);
        }
        for (var d = 0; d < Days; d++)
        {
            trials[d] = sessions[d] * 0.046 * (1 + 0.10 * random.Signed());
            activation[d] = 4.0 + 0.005 * d + 0.35 * Math.Sin(2 * Math.PI * d / 23) + 0.12 * random.Signed();
            churn[d] = 40 + 2 * Math.Sin(2 * Math.PI * d / 17) + 0.6 * random.Signed() + (d >= 120 ? 0.03 * (d - 120) : 0);
        }
        // The week before last was busy: the last 7 days compare lower for the counts and for churn risk.
        for (var d = Days - 14; d < Days - 7; d++)
        {
            sessions[d] *= 1.14;
            trials[d] *= 1.16;
            churn[d] += 2.6;
        }
        return
        [
            new MetricsDashboardMetric { Key = "sessions", Label = "Sessions", Daily = sessions },
            new MetricsDashboardMetric { Key = "trials", Label = "Trials", Daily = trials },
            new MetricsDashboardMetric { Key = "activation", Label = "Activation", Kind = MetricsDashboardMetricKind.Ratio, Daily = activation },
            new MetricsDashboardMetric
            {
                Key = "churn", Label = "Churn risk", Kind = MetricsDashboardMetricKind.Ratio, HigherIsBetter = false, Daily = churn
            },
        ];
    }

    /// <summary>The site's most visited paths (7D, 30D, 90D).</summary>
    public static MetricsDashboardItem[] TopPaths() =>
        Ranked(("/", 10840), ("/features", 5912), ("/guides/quick-tour", 4188), ("/notes/release-cadence", 2760), ("/roadmap", 1705));

    /// <summary>Where the site's sessions came from (7D, 30D, 90D).</summary>
    public static MetricsDashboardItem[] Referrers() =>
        Ranked(("Direct", 12960), ("Search", 9850), ("Forum", 3710), ("Social", 2640), ("Digest email", 1980));

    /// <summary>
    /// A product's usage: active teams and new projects (counts), seat utilization (a rate built from seats used and
    /// seats paid, so weeks and ranges are exact ratios) and failed jobs (a rate that is better when lower).
    /// </summary>
    public static MetricsDashboardMetric[] UsageMetrics()
    {
        var random = new Mulberry32(4207);
        var teams = new double[Days];
        var projects = new double[Days];
        var used = new double[Days];
        var paid = new double[Days];
        var utilization = new double[Days];
        var failed = new double[Days];
        for (var d = 0; d < Days; d++)
        {
            teams[d] = 310 * (1 + 0.0022 * d) * (1 + 0.22 * Weekday(d)) * (1 + 0.05 * random.Signed());
            projects[d] = teams[d] * 0.09 * (1 + 0.2 * random.Signed());
            paid[d] = 5200 + 9 * d;
            used[d] = paid[d] * (0.71 + 0.04 * Math.Sin(2 * Math.PI * d / 30) + 0.0004 * d + 0.015 * random.Signed());
            utilization[d] = used[d] / paid[d] * 100;
            failed[d] = Math.Max(0.2, 2.4 - 0.006 * d + 0.35 * Math.Sin(2 * Math.PI * d / 11) + 0.2 * random.Signed());
        }
        return
        [
            new MetricsDashboardMetric { Key = "teams", Label = "Active teams", Daily = teams },
            new MetricsDashboardMetric { Key = "projects", Label = "New projects", Daily = projects },
            new MetricsDashboardMetric
            {
                Key = "seats", Label = "Seat utilization", Kind = MetricsDashboardMetricKind.Ratio, Daily = utilization,
                Numerators = used, Denominators = paid
            },
            new MetricsDashboardMetric
            {
                Key = "failed", Label = "Failed jobs", Kind = MetricsDashboardMetricKind.Ratio, HigherIsBetter = false, Daily = failed,
                ValueFormatter = v => $"{v:0.00}%", DeltaFormatter = v => $"{v:0.00} pts"
            },
        ];
    }

    /// <summary>The busiest workspaces of the usage demo.</summary>
    public static MetricsDashboardItem[] Workspaces() =>
        Ranked(("Atlas Logistics", 4820), ("Harbor & Pine", 3115), ("Kestrel Studio", 2290), ("Bluefield Labs", 1408), ("Orchard Health", 960));

    /// <summary>Usage by plan.</summary>
    public static MetricsDashboardItem[] Plans() =>
        Ranked(("Enterprise", 8240), ("Team", 5130), ("Starter", 2210), ("Trial", 990), ("Education", 430));

    private static MetricsDashboardItem[] Ranked(params (string Label, double Value)[] rows) =>
        rows.Select(r => new MetricsDashboardItem
        {
            Label = r.Label,
            Value = r.Value,
            // 7D, 30D, 90D: the same mix at each range, as in the reference.
            Values = [Math.Round(r.Value * 0.24), r.Value, Math.Round(r.Value * 2.7)]
        }).ToArray();

    /// <summary>+1 Monday to Friday, −1 at the weekend (18 October 2026 is a Sunday).</summary>
    private static double Weekday(int d)
    {
        var day = EndDate.AddDays(d - (Days - 1)).DayOfWeek;
        return day is DayOfWeek.Saturday or DayOfWeek.Sunday ? -1 : 1;
    }

    /// <summary>mulberry32 (the five lines every Analytics demo copies); <see cref="Signed"/> maps to [−1, 1).</summary>
    private sealed class Mulberry32(uint seed)
    {
        private uint _state = seed;

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

        public double Signed() => Next() * 2 - 1;
    }
}
