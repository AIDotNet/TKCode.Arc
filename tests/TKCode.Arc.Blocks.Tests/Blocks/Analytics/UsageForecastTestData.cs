namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>Invented sample cycle for the Usage forecast tests and snapshots: Jun 1-30 2027, today is day 21.</summary>
internal static class UsageForecastTestData
{
    public static readonly DateOnly Start = new(2027, 6, 1);

    public static UsageMetric Events()
    {
        var rng = new AnalyticsSeed(7);
        var readings = new double[21];
        var sum = 0.0;
        for (var d = 1; d <= 21; d++)
        {
            var weekend = Start.AddDays(d - 1).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            sum += weekend ? rng.NextGaussian(0.52, 0.06) : rng.NextGaussian(1.35, 0.12);
            readings[d - 1] = Math.Round(sum, 3);
        }
        return new UsageMetric
        {
            Key = "events", Label = "Events", Readings = readings, StartValue = 0, AxisMinimum = 0, AxisMaximum = 40,
            BudgetMinimum = 8, BudgetMaximum = 40, BudgetStep = 0.5, Budget = 26, UnitPrice = 150, PriceNote = "At $0.15 per 1k events",
            Unit = "M", ValueFormatter = v => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public static UsageMetric Database()
    {
        var rng = new AnalyticsSeed(11);
        var readings = new double[21];
        var level = 610.0;
        for (var d = 1; d <= 21; d++)
        {
            level += d == 9 ? -28 : rng.NextGaussian(4.5, 3.0);
            readings[d - 1] = Math.Round(level, 1);
        }
        return new UsageMetric
        {
            Key = "database", Label = "Database", Readings = readings, AxisMinimum = 550, AxisMaximum = 800,
            BudgetMinimum = 600, BudgetMaximum = 800, BudgetStep = 5, Budget = 760, UnitPrice = 0.4, PriceNote = "At $0.40 per GB",
            Unit = "GB", UnitSeparator = " "
        };
    }

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
            Key = "editors", Label = "Editors", Readings = readings, AxisMinimum = 50, AxisMaximum = 80,
            BudgetMinimum = 55, BudgetMaximum = 80, BudgetStep = 1, Budget = 70, UnitPrice = 18, PriceNote = "At $18 per editor",
            Unit = "editors", UnitSeparator = " ", ValueFormatter = v => v.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public static UsageForecast Block(params UsageMetric[] metrics)
    {
        var block = new UsageForecast
        {
            Title = "Usage outlook", Subtitle = "Harborline Studio · Growth plan · Renews Jul 1", PeriodStart = Start, PeriodDays = 30, Width = 760
        };
        foreach (var m in metrics.Length == 0 ? new[] { Events(), Database(), Editors() } : metrics)
        {
            block.Metrics.Add(m);
        }
        return block;
    }
}
