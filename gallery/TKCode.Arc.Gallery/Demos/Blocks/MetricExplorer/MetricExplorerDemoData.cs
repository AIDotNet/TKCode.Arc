namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The MetricExplorer demos' data (all invented, seeded and deterministic): two years of daily values per metric, built as
/// a random walk along a gentle weekly pattern and then pinned to chosen values 30, 90 and 364 days ago and today, so the
/// card numbers are known (+1.6% over 30 days for the first metric, and so on) while the lines stay irregular.
/// </summary>
public static class MetricExplorerDemoData
{
    /// <summary>The day the series end on (a Wednesday).</summary>
    public static readonly DateTime Today = new(2026, 10, 14);

    public const string Title = "Workspace health";

    public const string Caption = "Northwind Labs · refreshed Oct 14, 2026 at 08:30 UTC";

    private const int Days = 730;

    /// <summary>Four metrics of a SaaS workspace: run-rate, active teams, trial conversion and net retention (the last one falls).</summary>
    public static ExplorerMetric[] Workspace() =>
    [
        new("Annual run-rate", Daily(20261014, 3_400_000, 38_000, 0.004, [(0, 4_620_000), (30, 4_547_000), (90, 4_310_000), (364, 3_555_000)], 100),
            ExplorerValueFormat.CurrencyCompact)
        {
            Key = "arr", ShortTitle = "ARR", Description = "Yearly value of active contracts, after discounts."
        },
        new("Weekly active teams", Daily(20261015, 4_300, 21, 0.03, [(0, 5_874), (30, 5_709), (90, 5_380), (364, 4_290)], 1),
            ExplorerValueFormat.Count)
        {
            Key = "teams", ShortTitle = "Teams", Description = "Teams with at least two members active in the last 7 days."
        },
        new("Trial conversion", Daily(20261016, 22.0, 0.09, 0.0, [(0, 27.3), (30, 26.1), (90, 25.0), (364, 22.4)], 0.01),
            ExplorerValueFormat.Percent)
        {
            Key = "conversion", ShortTitle = "Conversion", ChangeMode = ExplorerChangeMode.Points,
            Description = "Share of trials that start a paid plan within 14 days."
        },
        new("Net revenue retention", Daily(20261017, 104.0, 0.12, 0.0, [(0, 108.6), (30, 109.7), (90, 111.2), (364, 105.2)], 0.01),
            ExplorerValueFormat.Percent)
        {
            Key = "retention", ShortTitle = "Retention", ChangeMode = ExplorerChangeMode.Points,
            Description = "Revenue kept from last year's customers, counting upgrades and cancellations."
        },
    ];

    /// <summary>Three finance metrics; the last one, monthly burn, is better when it falls.</summary>
    public static ExplorerMetric[] Finance() =>
    [
        new("Net revenue", Daily(7031, 1_620_000, 21_000, 0.003, [(0, 2_180_000), (30, 2_110_000), (90, 1_960_000), (364, 1_620_000)], 100),
            ExplorerValueFormat.CurrencyCompact)
        {
            Key = "revenue", Description = "Invoiced amounts net of refunds and credits."
        },
        new("Gross margin", Daily(7032, 68.5, 0.10, 0.0, [(0, 71.4), (30, 70.9), (90, 70.2), (364, 68.5)], 0.01), ExplorerValueFormat.Percent)
        {
            Key = "margin", ChangeMode = ExplorerChangeMode.Points, Description = "Revenue left after hosting, support and payment costs."
        },
        new("Monthly burn", Daily(7033, 390_000, 6_500, 0.01, [(0, 412_000), (30, 438_000), (90, 455_000), (364, 390_000)], 100),
            ExplorerValueFormat.CurrencyCompact)
        {
            Key = "burn", ShortTitle = "Burn", IsHigherBetter = false, Description = "Cash spent per month, before financing."
        },
    ];

    /// <summary>
    /// A daily series: a random walk from <paramref name="start"/> (step <paramref name="step"/>), a weekly pattern of relative
    /// size <paramref name="season"/>, then pulled through the anchors (days back, value) by spreading each miss linearly
    /// between anchors, and rounded to <paramref name="rounding"/>.
    /// </summary>
    public static double[] Daily(uint seed, double start, double step, double season, (int Back, double Value)[] anchors, double rounding)
    {
        var random = new Mulberry32(seed);
        var values = new double[Days];
        var level = start;
        var goal = anchors.OrderBy(a => a.Back).First().Value;
        for (var i = 0; i < Days; i++)
        {
            level += (goal - start) / Days + random.Gaussian(0, step);
            var weekly = 1 + season * Math.Sin(2 * Math.PI * i / 7);
            values[i] = level * weekly;
        }
        // Residuals at the anchors (oldest first), spread linearly; before the oldest anchor they fade in from zero.
        var pins = anchors.Select(a => (Index: Days - 1 - a.Back, a.Value)).OrderBy(a => a.Index).ToArray();
        var residuals = pins.Select(p => p.Value - values[p.Index]).ToArray();
        var pinIndexes = new List<int> { 0 };
        var pinResiduals = new List<double> { 0 };
        for (var i = 0; i < pins.Length; i++)
        {
            pinIndexes.Add(pins[i].Index);
            pinResiduals.Add(residuals[i]);
        }
        for (var i = 0; i < Days; i++)
        {
            var s = 0;
            while (s < pinIndexes.Count - 2 && i > pinIndexes[s + 1])
            {
                s++;
            }
            var t = (i - pinIndexes[s]) / (double)Math.Max(1, pinIndexes[s + 1] - pinIndexes[s]);
            values[i] += pinResiduals[s] + (pinResiduals[s + 1] - pinResiduals[s]) * Math.Clamp(t, 0, 1);
            values[i] = Math.Round(values[i] / rounding) * rounding;
        }
        foreach (var pin in pins)
        {
            values[pin.Index] = Math.Round(pin.Value / rounding) * rounding;
        }
        return values;
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
