namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The RevenueExplorer demos' data (all invented, seeded and deterministic): a subscription business's monthly new, expansion
/// and churned recurring revenue, 24 months from Mar 2024 with a dip in new revenue in the eighth month and a spike in churn
/// in the sixth, a 36-month history, and a smaller euro-based product.
/// </summary>
public static class RevenueExplorerDemoData
{
    public const string Title = "Subscription revenue";

    /// <summary>Recurring revenue before the first month of <see cref="Standard"/> and <see cref="LongHistory"/>.</summary>
    public const double Opening = 1_050_000;

    /// <summary>Recurring revenue before the first month of <see cref="Euro"/>.</summary>
    public const double EuroOpening = 214_000;

    /// <summary>24 months from Mar 2024 (seed 7).</summary>
    public static RevenueMonth[] Standard() => Generate(24, new DateOnly(2024, 3, 1), 7, 1);

    /// <summary>36 months from Mar 2023 (seed 11).</summary>
    public static RevenueMonth[] LongHistory() => Generate(36, new DateOnly(2023, 3, 1), 11, 1);

    /// <summary>24 months from Mar 2024 of a smaller product (seed 23, about a fifth of the size).</summary>
    public static RevenueMonth[] Euro() => Generate(24, new DateOnly(2024, 3, 1), 23, 0.2);

    private static RevenueMonth[] Generate(int count, DateOnly first, uint seed, double scale)
    {
        var random = new Mulberry32(seed);
        var months = new RevenueMonth[count];
        for (var i = 0; i < count; i++)
        {
            var @new = 38_000 + 1_400 * i + 6_000 * Math.Sin(i * 0.7) + random.Range(-3_000, 3_000);
            if (i == 7)
            {
                @new *= 0.72;
            }
            if (i >= 19 && i <= 21)
            {
                @new = 38_000 + 1_400 * 19 + random.Range(-1_500, 1_500);
            }
            var expansion = 14_000 + 550 * i + random.Range(-2_500, 2_500);
            var churn = 16_000 + 600 * i + random.Range(-2_000, 2_000);
            if (i == 5)
            {
                churn *= 1.35;
            }
            months[i] = new RevenueMonth(first.AddMonths(i), Math.Round(@new * scale), Math.Round(expansion * scale), -Math.Round(churn * scale));
        }
        return months;
    }

    /// <summary>mulberry32 (the Showpiece demos' generator).</summary>
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

        public double Range(double min, double max) => min + (max - min) * Next();
    }
}
