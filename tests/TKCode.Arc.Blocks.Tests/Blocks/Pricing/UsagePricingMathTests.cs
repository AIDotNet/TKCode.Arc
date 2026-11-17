using System.Globalization;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// UsagePricingMath against the notes (2b.3–2b.5): Arc's preview numbers check the formulas, the invented sample
/// (section 11) checks the defaults.
/// </summary>
public class UsagePricingMathTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The shape of Arc's preview plans (only used to verify the formulas, never shipped).</summary>
    private static readonly UsagePricingTerms[] ArcShape =
    [
        new(12, 2, 1_000_000, 8, 5, 2_000_000),
        new(24, 4, 10_000_000, 4, 40, 25_000_000),
        new(38, 6, 50_000_000, 2, null, null)
    ];

    private static UsagePricingTerms[] Sample() => UsagePricingSamples.CreatePlans().Select(UsagePricingTerms.Of).ToArray();

    [Theory]
    [InlineData(5, 2_000_000, 0)]
    [InlineData(1, 100_000, 0)]
    [InlineData(6, 2_000_000, 1)]
    [InlineData(5, 2_500_000, 1)]
    [InlineData(40, 25_000_000, 1)]
    [InlineData(45, 2_500_000, 2)]
    [InlineData(1, 30_000_000, 2)]
    [InlineData(200, 200_000_000, 2)]
    public void Recommends_the_first_plan_whose_limits_cover_both(int seats, double usage, int expected)
        => Assert.Equal(expected, UsagePricingMath.Recommend(ArcShape, seats, usage));

    [Fact]
    public void Recommendation_ignores_price()
    {
        // 1 seat, 25M: the middle plan costs $84, the last only $38, yet the middle one is recommended (Arc's logic).
        var index = UsagePricingMath.Recommend(ArcShape, 1, 25_000_000);
        Assert.Equal(1, index);
        var middle = UsagePricingMath.Quote(ArcShape, 1, 25_000_000, false, 1e6);
        Assert.Equal(84m, middle.Total);
    }

    [Theory]
    [InlineData(3, 15_000_000, UsagePricingReason.UsageAbove)]
    [InlineData(20, 100_000, UsagePricingReason.SeatsAbove)]
    [InlineData(18, 15_000_000, UsagePricingReason.BothAbove)]
    [InlineData(45, 2_500_000, UsagePricingReason.SeatsAbove)]
    [InlineData(10, 30_000_000, UsagePricingReason.UsageAbove)]
    [InlineData(45, 30_000_000, UsagePricingReason.BothAbove)]
    [InlineData(3, 1_000_000, UsagePricingReason.WithinLimits)]
    public void Reason_compares_with_the_previous_plans_limits(int seats, double usage, UsagePricingReason expected)
    {
        var index = UsagePricingMath.Recommend(ArcShape, seats, usage);
        Assert.Equal(expected, UsagePricingMath.Reason(ArcShape, index, seats, usage));
    }

    [Fact]
    public void Costs_match_the_preview()
    {
        Assert.Equal(432m, UsagePricingMath.SeatCost(18, 24));
        Assert.Equal(1_710m, UsagePricingMath.SeatCost(45, 38));
        Assert.Equal(2m, UsagePricingMath.UsageCost(UsagePricingMath.Overage(1_250_000, 1_000_000), 8, 1e6));
        Assert.Equal(10m, UsagePricingMath.UsageCost(UsagePricingMath.Overage(12_500_000, 10_000_000), 4, 1e6));
        Assert.Equal(50m, UsagePricingMath.UsageCost(UsagePricingMath.Overage(75_000_000, 50_000_000), 2, 1e6));
        Assert.Equal(0, UsagePricingMath.Overage(800_000, 1_000_000));
        Assert.Equal(72m, UsagePricingMath.YearlyOff(18, 4));
        Assert.Equal(864m, UsagePricingMath.YearlySaving(18, 4));
        Assert.Equal(14_400m, UsagePricingMath.YearlySaving(200, 6));
        Assert.Equal(4_320m, UsagePricingMath.SeatsPerYear(18, 24, 4));
        Assert.Equal(76_800m, UsagePricingMath.SeatsPerYear(200, 38, 6));
        Assert.Equal(120m, UsagePricingMath.SeatsPerYear(1, 12, 2));
    }

    [Theory]
    [InlineData(18, 15_000_000, false, 452)]
    [InlineData(18, 15_000_000, true, 380)]
    [InlineData(200, 60_000_000, true, 6_420)]
    [InlineData(1, 75_000_000, false, 88)]
    [InlineData(200, 200_000_000, false, 7_900)]
    public void Totals_match_the_preview(int seats, double usage, bool yearly, decimal total)
        => Assert.Equal(total, UsagePricingMath.Quote(ArcShape, seats, usage, yearly, 1e6).Total);

    [Fact]
    public void Saving_counts_seats_only()
    {
        var a = UsagePricingMath.Quote(ArcShape, 200, 60_000_000, true, 1e6);
        var b = UsagePricingMath.Quote(ArcShape, 200, 200_000_000, false, 1e6);
        Assert.Equal(14_400m, a.YearlySaving);
        Assert.Equal(14_400m, b.YearlySaving);
    }

    [Fact]
    public void Sample_default_is_Studio_436()
    {
        var plans = Sample();
        var seats = UsagePricingSamples.SeatStops[UsagePricingSamples.DefaultSeatIndex];
        var usage = UsagePricingSamples.UsageStops[UsagePricingSamples.DefaultUsageIndex];
        Assert.Equal(16, seats);
        Assert.Equal(12_000_000, usage);
        var monthly = UsagePricingMath.Quote(plans, seats, usage, false, 1e6);
        Assert.Equal(1, monthly.PlanIndex);
        Assert.Equal(UsagePricingReason.BothAbove, monthly.Reason);
        Assert.Equal(416m, monthly.SeatCost);
        Assert.Equal(20m, monthly.UsageCost);
        Assert.Equal(436m, monthly.Total);
        Assert.Equal(960m, monthly.YearlySaving);
        Assert.Equal(4_032m, monthly.SeatsPerYear);
        Assert.Equal(356m, UsagePricingMath.Quote(plans, seats, usage, true, 1e6).Total);
        var max = UsagePricingMath.Quote(plans, 250, 300_000_000, false, 1e6);
        Assert.Equal(2, max.PlanIndex);
        Assert.Equal(11_020m, max.Total);
    }

    [Fact]
    public void Sample_stops_have_the_measured_counts()
    {
        Assert.Equal(23, UsagePricingSamples.SeatStops.Count);
        Assert.Equal(27, UsagePricingSamples.UsageStops.Count);
    }

    [Fact]
    public void Marks_sit_at_the_first_stop_past_the_previous_limit()
    {
        var plans = Sample();
        Assert.Equal(4, UsagePricingMath.MarkIndex(UsagePricingSamples.SeatStops, plans[0].MaxSeats, v => v));
        Assert.Equal(15, UsagePricingMath.MarkIndex(UsagePricingSamples.SeatStops, plans[1].MaxSeats, v => v));
        Assert.Equal(7, UsagePricingMath.MarkIndex(UsagePricingSamples.UsageStops, plans[0].MaxUsage, v => v));
        Assert.Equal(17, UsagePricingMath.MarkIndex(UsagePricingSamples.UsageStops, plans[1].MaxUsage, v => v));
        Assert.Equal(-1, UsagePricingMath.MarkIndex(UsagePricingSamples.SeatStops, null, v => v));
        // Stops are spaced by index: Arc's 5/22 and 16/22.
        Assert.Equal(0.22727, UsagePricingMath.StopFraction(5, 23), 5);
        Assert.Equal(0.72727, UsagePricingMath.StopFraction(16, 23), 5);
        Assert.Equal(0.30769, UsagePricingMath.StopFraction(8, 27), 5);
    }

    [Theory]
    [InlineData(100_000, "100k")]
    [InlineData(750_000, "750k")]
    [InlineData(1_000_000, "1M")]
    [InlineData(1_250_000, "1.25M")]
    [InlineData(2_500_000, "2.5M")]
    [InlineData(12_500_000, "12.5M")]
    [InlineData(15_000_000, "15M")]
    [InlineData(300_000_000, "300M")]
    [InlineData(1_500_000_000, "1.5B")]
    [InlineData(800, "800")]
    public void Usage_format(double value, string expected) => Assert.Equal(expected, UsagePricingMath.FormatUsage(value, En));

    [Fact]
    public void Money_is_whole_with_a_true_minus()
    {
        Assert.Equal("$1,710", UsagePricingMath.Money(1_710, "USD", En));
        Assert.Equal("$14,400", UsagePricingMath.Money(14_400, "USD", En));
        Assert.Equal("−$72", UsagePricingMath.Money(-72, "USD", En));
        Assert.Equal("$0", UsagePricingMath.Money(0, "USD", En));
        Assert.Equal("€1.710", UsagePricingMath.Money(1_710, "EUR", CultureInfo.GetCultureInfo("de-DE")));
        Assert.Equal("1,710 CHF", UsagePricingMath.Money(1_710, "chf", En));
        Assert.Equal("$5", UsagePricingMath.Rate(5, "USD", En));
        Assert.Equal("$0.40", UsagePricingMath.Rate(0.4m, "USD", En));
    }

    [Fact]
    public void Feature_lines_put_new_features_first()
    {
        var plans = UsagePricingSamples.CreatePlans().Select(p => (IReadOnlyList<UsagePricingFeature>)p.Features).ToList();
        var spark = UsagePricingMath.FeatureLines(plans, 0);
        Assert.All(spark, l => Assert.True(l.IsNew));
        Assert.Equal(["history", "rate", "help"], spark.Select(l => l.Key));
        var studio = UsagePricingMath.FeatureLines(plans, 1);
        Assert.Equal(["links", "trail", "history", "rate", "help"], studio.Select(l => l.Key));
        Assert.Equal([true, true, false, false, false], studio.Select(l => l.IsNew));
        var fleet = UsagePricingMath.FeatureLines(plans, 2);
        Assert.Equal(["sso", "uptime", "links", "trail", "history", "rate", "help"], fleet.Select(l => l.Key));
        Assert.Equal([true, true, false, false, false, false, false], fleet.Select(l => l.IsNew));
        Assert.Null(fleet[0].Value);
        Assert.Equal("Dedicated engineer", fleet[6].Value);
    }

    [Fact]
    public void Inherited_lines_follow_the_previous_plans_order()
    {
        var a = new List<UsagePricingFeature> { new("x", "X"), new("y", "Y") };
        var b = new List<UsagePricingFeature> { new("y", "Y2"), new("z", "Z"), new("x", "X2") };
        var lines = UsagePricingMath.FeatureLines([a, b], 1);
        Assert.Equal(["z", "x", "y"], lines.Select(l => l.Key));
    }

    [Fact]
    public void Split_around_finds_the_value()
    {
        Assert.Equal(("", " saved per year"), UsagePricingMath.SplitAround(a => $"{a} saved per year"));
        Assert.Equal(("Save ", " a year"), UsagePricingMath.SplitAround(a => $"Save {a} a year"));
        Assert.Equal(("Nothing", null), UsagePricingMath.SplitAround(_ => "Nothing"));
    }
}
