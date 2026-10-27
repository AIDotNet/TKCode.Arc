using Avalonia.Collections;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Sample data for the KPI drilldown demos, invented for a made-up creative-tools company ("Studio pulse"). Every series is
/// generated from a fixed seed (mulberry32): base level, a slow trend over the 180 days, a weekly rhythm and a decaying
/// random walk. The breakdown rows are series too (the metric times a fixed share with their own drift), so a range switch
/// recomputes the table.
/// </summary>
public static class KpiDrilldownDemoData
{
    public const int Days = 180;

    public static readonly DateOnly End = new(2027, 3, 15);

    private sealed record Spec(string Key, string Label, KpiKind Kind, KpiValueFormat Format, int Decimals, bool LowerIsBetter,
        double Base, double Trend, double Season, double Noise, uint Seed, string Description);

    private static readonly Spec[] s_specs =
    [
        new("arr", "Annual run rate", KpiKind.Total, KpiValueFormat.CurrencyCompact, 1, false, 2.2e6, 0.12, 0.004, 6e3, 11,
            "Contracted yearly revenue across all paid studios."),
        new("studios", "Paying studios", KpiKind.Total, KpiValueFormat.Number, 0, false, 3000, 0.08, 0.003, 9, 23,
            "Studios with an active paid plan on the last day."),
        new("expansion", "Expansion rate", KpiKind.Rate, KpiValueFormat.Percent, 1, false, 108, 0.04, 0.002, 0.35, 37,
            "Revenue kept from last year's studios, upgrades included."),
        new("refunds", "Refund rate", KpiKind.Rate, KpiValueFormat.Percent, 1, true, 3.1, -0.18, 0.01, 0.05, 41,
            "Share of charges refunded within thirty days."),
        new("deal", "Average deal size", KpiKind.Total, KpiValueFormat.Currency, 1, false, 61, 0.06, 0.003, 0.4, 53,
            "Run rate divided by paying studios."),
        new("activation", "Activation rate", KpiKind.Rate, KpiValueFormat.Percent, 1, false, 26, 0.02, 0.03, 0.9, 67,
            "New trials that publish a first project within a week.")
    ];

    private static readonly (string Key, string Label, string[] Names, double[] Shares)[] s_dimensions =
    [
        ("tier", "Tier", ["Studio Max", "Studio", "Team", "Solo"], [0.44, 0.30, 0.18, 0.08]),
        ("market", "Market", ["Americas", "Europe", "Asia Pacific", "Middle East & Africa"], [0.44, 0.30, 0.18, 0.08]),
        ("source", "Source", ["Self-serve", "Partner", "Outbound", "Events"], [0.44, 0.30, 0.18, 0.08])
    ];

    public static AvaloniaList<KpiDimension> Dimensions() =>
        new(s_dimensions.Select(d => new KpiDimension(d.Key, d.Label)));

    public static List<KpiMetric> Metrics(int count = 6, bool withBreakdowns = true) =>
        s_specs.Take(count).Select(s => Metric(s, withBreakdowns)).ToList();

    public static KpiMetric Metric(string key, bool withBreakdowns = true) => Metric(s_specs.First(s => s.Key == key), withBreakdowns);

    private static KpiMetric Metric(Spec spec, bool withBreakdowns)
    {
        var values = Series(spec, spec.Seed);
        var metric = new KpiMetric(spec.Key, spec.Label, values)
        {
            Kind = spec.Kind,
            Format = spec.Format,
            Decimals = spec.Decimals,
            IsLowerBetter = spec.LowerIsBetter,
            Description = spec.Description
        };
        if (withBreakdowns)
        {
            foreach (var dimension in s_dimensions)
            {
                var rows = new List<KpiBreakdownRow>();
                for (var i = 0; i < dimension.Names.Length; i++)
                {
                    // Rate metrics keep the metric's own level per member; totals split the total by share. One member
                    // per dimension drifts down so the table shows a fall.
                    var drift = i == dimension.Names.Length - 1 ? -0.0012 : 0.0004 * (3 - i);
                    var scale = spec.Kind == KpiKind.Rate ? 1 + (i - 1.5) * 0.04 : dimension.Shares[i];
                    var rng = new Mulberry(spec.Seed * 31 + (uint)i * 7 + 5);
                    var row = new double[values.Length];
                    for (var t = 0; t < row.Length; t++)
                    {
                        row[t] = values[t] * scale * (1 + drift * (t - row.Length) + (rng.Next() - 0.5) * 0.006);
                    }
                    rows.Add(new KpiBreakdownRow(dimension.Names[i], row));
                }
                metric.Breakdowns.Add(new KpiBreakdown(dimension.Key, rows));
            }
        }
        return metric;
    }

    private static double[] Series(Spec s, uint seed)
    {
        var rng = new Mulberry(seed);
        var values = new double[Days];
        double walk = 0;
        for (var t = 0; t < Days; t++)
        {
            walk = walk * 0.92 + Gaussian(rng) * s.Noise;
            var v = s.Base * (1 + s.Trend * t / Days) * (1 + s.Season * Math.Sin(2 * Math.PI * t / 7 + seed)) + walk;
            // Shape the last month: a plateau, a rise, a dip (so the 30-day sparkline reads like a story).
            var fromEnd = Days - 1 - t;
            if (fromEnd < 30)
            {
                var k = (29 - fromEnd) / 29.0;
                v += s.Base * s.Trend * 0.06 * (Math.Sin(k * Math.PI * 1.6) + k);
            }
            values[t] = s.Kind == KpiKind.Rate ? Math.Clamp(v, 0, 100) : v;
        }
        if (s.Key == "activation")
        {
            // Land the 30-day change on zero: the last day equals the day thirty before it.
            values[^1] = values[^31];
        }
        return values;
    }

    private static double Gaussian(Mulberry rng)
    {
        var u = Math.Max(1e-9, rng.Next());
        return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * rng.Next());
    }

    private sealed class Mulberry(uint seed)
    {
        private uint _state = seed;

        public double Next()
        {
            _state += 0x6D2B79F5;
            var t = _state;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }
}
