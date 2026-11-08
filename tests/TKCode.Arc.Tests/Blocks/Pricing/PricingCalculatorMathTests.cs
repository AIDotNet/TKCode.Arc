using System.Globalization;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// PricingCalculatorMath against the reference's measured numbers (notes section 2b: a 10 / 20 / 35 seat price set,
/// yearly = 0.8 × monthly, $2 / $4 volume cuts from 25 / 100 seats, a 10-seat cap) and against the invented samples
/// (notes section 11).
/// </summary>
public class PricingCalculatorMathTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The reference's rule shape with its numbers (test input only; the samples use their own).</summary>
    private static List<PricingCalculatorPlan> Reference()
    {
        var small = new PricingCalculatorPlan { Id = "a", Name = "A", MonthlySeatPrice = 10, MaxSeats = 10, RecommendFromSeats = 1 };
        var mid = new PricingCalculatorPlan { Id = "b", Name = "B", MonthlySeatPrice = 20, RecommendFromSeats = 6 };
        var big = new PricingCalculatorPlan { Id = "c", Name = "C", MonthlySeatPrice = 35, RecommendFromSeats = 50 };
        foreach (var plan in new[] { mid, big })
        {
            plan.VolumeTiers.Add(new PricingCalculatorVolumeTier(25, 2));
            plan.VolumeTiers.Add(new PricingCalculatorVolumeTier(100, 4));
        }
        return [small, mid, big];
    }

    private static readonly int[] ReferenceStops = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 25, 30, 40, 50, 60, 75, 100, 150, 200, 250];

    private const PricingCalculatorBilling M = PricingCalculatorBilling.Monthly;
    private const PricingCalculatorBilling Y = PricingCalculatorBilling.Yearly;

    [Theory]
    [InlineData(24, 0)]
    [InlineData(25, 2)]
    [InlineData(99, 2)]
    [InlineData(100, 4)]
    [InlineData(250, 4)]
    public void VolumeDiscountTakesTheLargestTierReached(int seats, int expected)
    {
        Assert.Equal(expected, PricingCalculatorMath.VolumeDiscount(Reference()[1], seats));
        Assert.Equal(0, PricingCalculatorMath.VolumeDiscount(Reference()[0], seats));
    }

    [Theory]
    [InlineData(1, 25, 18, 14)]
    [InlineData(2, 100, 31, 24)]
    [InlineData(2, 50, 33, 26)]
    [InlineData(0, 3, 10, 8)]
    public void SeatPriceSubtractsTheDiscountAfterTheYearlyFactor(int plan, int seats, int monthly, int yearly)
    {
        var p = Reference()[plan];
        Assert.Equal(monthly, PricingCalculatorMath.SeatPrice(p, M, seats, 0.2));
        Assert.Equal(yearly, PricingCalculatorMath.SeatPrice(p, Y, seats, 0.2));
    }

    [Theory]
    [InlineData(1, 20, Y, 3840)]
    [InlineData(2, 250, Y, 72000)]
    [InlineData(2, 75, M, 2475)]
    [InlineData(2, 3, Y, 1008)]
    [InlineData(0, 10, M, 100)]
    public void PlanTotalIsSeatPriceTimesSeatsAndMonths(int plan, int seats, PricingCalculatorBilling billing, int expected)
        => Assert.Equal(expected, PricingCalculatorMath.PlanTotal(Reference()[plan], billing, seats, 0.2));

    [Theory]
    [InlineData(0, 1, 24)]
    [InlineData(1, 6, 288)]
    [InlineData(2, 50, 4200)]
    [InlineData(1, 25, 1200)]
    [InlineData(2, 20, 1680)]
    [InlineData(0, 10, 240)]
    public void SavingsIgnoreTheVolumeDiscount(int plan, int seats, int expected)
        => Assert.Equal(expected, PricingCalculatorMath.YearlySavings(Reference()[plan], seats, 0.2));

    [Fact]
    public void ExplicitYearlyPriceWinsOverTheDiscount()
    {
        var plan = new PricingCalculatorPlan { MonthlySeatPrice = 25, YearlySeatPrice = 19 };
        Assert.Equal(19, PricingCalculatorMath.SeatPrice(plan, Y, 1, 0.2));
        plan.YearlySeatPrice = null;
        Assert.Equal(20, PricingCalculatorMath.SeatPrice(plan, Y, 1, 0.2));
        Assert.Equal(22.5m, PricingCalculatorMath.SeatPrice(plan, Y, 1, 0.1));
    }

    [Theory]
    [InlineData(1, "a")]
    [InlineData(5, "a")]
    [InlineData(6, "b")]
    [InlineData(40, "b")]
    [InlineData(45, "b")]
    [InlineData(49, "b")]
    [InlineData(50, "c")]
    [InlineData(250, "c")]
    public void RecommendationFollowsTheThresholds(int seats, string expected)
        => Assert.Equal(expected, PricingCalculatorMath.Recommend(Reference(), seats)!.Id);

    [Fact]
    public void BelowEveryThresholdTheFirstPlanIsRecommended()
    {
        var plans = Reference();
        plans[0].RecommendFromSeats = 3;
        Assert.Same(plans[0], PricingCalculatorMath.Recommend(plans, 1));
        Assert.Null(PricingCalculatorMath.Recommend([], 4));
    }

    [Fact]
    public void PickedPlanHoldsUntilItsCapAndComesBack()
    {
        var plans = Reference();
        // 20 seats, the big plan picked: stays when the seats drop to 12.
        Assert.Equal("c", PricingCalculatorMath.Select(plans, "c", 12, PricingCalculatorMath.Recommend(plans, 12))!.Id);
        // The capped plan picked, then 12 seats: falls back to the recommendation; at 10 it is selected again.
        Assert.Equal("b", PricingCalculatorMath.Select(plans, "a", 12, PricingCalculatorMath.Recommend(plans, 12))!.Id);
        Assert.Equal("a", PricingCalculatorMath.Select(plans, "a", 10, PricingCalculatorMath.Recommend(plans, 10))!.Id);
        // Nothing picked (or an unknown key): the recommendation.
        Assert.Equal("b", PricingCalculatorMath.Select(plans, null, 20, PricingCalculatorMath.Recommend(plans, 20))!.Id);
        Assert.Equal("b", PricingCalculatorMath.Select(plans, "zzz", 20, PricingCalculatorMath.Recommend(plans, 20))!.Id);
    }

    [Fact]
    public void MarksSitAtTheFirstRecommendedStop()
    {
        var plans = Reference();
        Assert.Equal(0, PricingCalculatorMath.MarkIndex(plans, ReferenceStops, plans[0]));
        Assert.Equal(5, PricingCalculatorMath.MarkIndex(plans, ReferenceStops, plans[1]));
        Assert.Equal(16, PricingCalculatorMath.MarkIndex(plans, ReferenceStops, plans[2]));
    }

    [Fact]
    public void StopsAndPageStep()
    {
        Assert.Equal(12, PricingCalculatorMath.NearestStopIndex(ReferenceStops, 20));
        Assert.Equal(12, PricingCalculatorMath.NearestStopIndex(ReferenceStops, 21));
        Assert.Equal(0, PricingCalculatorMath.NearestStopIndex(ReferenceStops, -5));
        Assert.Equal(22, PricingCalculatorMath.NearestStopIndex(ReferenceStops, 9999));
        Assert.Equal(2, PricingCalculatorMath.PageStep(23));
        Assert.Equal(1, PricingCalculatorMath.PageStep(3));
    }

    [Fact]
    public void FeatureLinesFollowSeatsAndBilling()
    {
        var plans = PricingCalculatorSamples.CreatePlans();
        var labels = new PricingCalculatorLabels();
        var studio20 = PricingCalculatorMath.Features(plans[1], 20, Y, "USD", labels).Select(l => l.Text).ToArray();
        Assert.Equal(["All of Solo", "500 GB file storage", "Single sign-on with Google", "Kickoff workshop with our team"], studio20);

        var studio30Monthly = PricingCalculatorMath.Features(plans[1], 30, M, "USD", labels).Select(l => l.Text).ToArray();
        Assert.Equal(["All of Solo", "750 GB file storage", "Single sign-on with Google", "Volume rate, $3 off every seat"], studio30Monthly);

        var scale120 = PricingCalculatorMath.Features(plans[2], 120, Y, "USD", labels);
        Assert.Equal(["f0", "f1", "f2", "f3", "f4", "f5"], scale120.Select(l => l.Key).ToArray());
        Assert.Equal("Volume rate, $5 off every seat", scale120[3].Text);
        Assert.Equal("Dedicated account partner", scale120[4].Text);

        var scale40 = PricingCalculatorMath.Features(plans[2], 40, M, "USD", labels).Select(l => l.Key).ToArray();
        Assert.Equal(["f0", "f1", "f2", "f3"], scale40);

        // Storage is not converted to larger units (300 seats ⇒ 7,500 GB).
        Assert.Equal("7,500 GB file storage", PricingCalculatorMath.Features(plans[1], 300, Y, "USD", labels)[1].Text);
    }

    [Fact]
    public void SamplesHaveTheReferenceShape()
    {
        var plans = PricingCalculatorSamples.CreatePlans();
        var stops = PricingCalculatorSamples.SeatStops;
        Assert.Equal(23, stops.Count);
        Assert.Equal(11, PricingCalculatorMath.NearestStopIndex(stops, PricingCalculatorSamples.DefaultSeats));
        Assert.Equal(4800, PricingCalculatorMath.PlanTotal(plans[1], Y, 20, 0.2));
        Assert.Equal(97200, PricingCalculatorMath.PlanTotal(plans[2], Y, 300, 0.2));
        Assert.Equal(15, PricingCalculatorMath.PlanTotal(plans[0], M, 1, 0.2));
        Assert.Equal(1200, PricingCalculatorMath.YearlySavings(plans[1], 20, 0.2));
        Assert.Equal("solo", PricingCalculatorMath.Recommend(plans, 4)!.Id);
        Assert.Equal("studio", PricingCalculatorMath.Recommend(plans, 5)!.Id);
        Assert.Equal("studio", PricingCalculatorMath.Recommend(plans, 40)!.Id);
        Assert.Equal("scale", PricingCalculatorMath.Recommend(plans, 50)!.Id);
        Assert.False(PricingCalculatorMath.IsAvailable(plans[0], 10));
        Assert.True(PricingCalculatorMath.IsAvailable(plans[0], 8));
        Assert.Equal(4, PricingCalculatorMath.MarkIndex(plans, stops, plans[1]));
        Assert.Equal(15, PricingCalculatorMath.MarkIndex(plans, stops, plans[2]));
    }

    [Fact]
    public void MoneyShapes()
    {
        Assert.Equal("$4,800", PricingCalculatorMath.Money(4800, "USD", EnUs));
        Assert.Equal("$12.50", PricingCalculatorMath.Money(12.5m, "USD", EnUs));
        Assert.Equal(new PricingCalculatorMath.MoneyShape("$", "", 0), PricingCalculatorMath.ShapeOf(4800, "USD", EnUs));
        Assert.Equal(new PricingCalculatorMath.MoneyShape("€", "", 2), PricingCalculatorMath.ShapeOf(12.5m, "EUR", EnUs));
        Assert.Equal(new PricingCalculatorMath.MoneyShape("", " CHF", 0), PricingCalculatorMath.ShapeOf(90, "CHF", EnUs));
        Assert.Equal(20, PricingCalculatorMath.DiscountPercent(0.2));
    }
}
