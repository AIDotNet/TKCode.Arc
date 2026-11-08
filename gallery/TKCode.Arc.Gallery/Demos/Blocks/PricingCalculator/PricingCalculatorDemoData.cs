using Avalonia.Collections;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Invented plans for the pricing calculator demos.</summary>
public static class PricingCalculatorDemoData
{
    /// <summary>Seat stops of the crew plans: 1 to 500.</summary>
    public static IReadOnlyList<int> CrewStops { get; } = [1, 2, 3, 5, 8, 12, 16, 24, 32, 48, 64, 100, 150, 250, 500];

    /// <summary>
    /// Two plans for a field-service app: Crew ($18 a seat, $15 yearly, up to 16 seats) and Fleet ($30, $24 yearly,
    /// $4 off every seat from 64 seats); Fleet is recommended from 12 seats.
    /// </summary>
    public static AvaloniaList<PricingCalculatorPlan> CrewPlans()
    {
        var crew = new PricingCalculatorPlan
        {
            Id = "crew",
            Name = "Crew",
            Description = "Dispatch and job notes for small teams",
            MonthlySeatPrice = 18,
            YearlySeatPrice = 15,
            MaxSeats = 16,
            RecommendFromSeats = 1
        };
        crew.Features.Add(new PricingCalculatorFeature("Job board and map"));
        crew.Features.Add(new PricingCalculatorFeature { Kind = PricingCalculatorFeatureKind.PerSeat, PerSeatAmount = 50, Text = "{0} photo uploads a month" });
        crew.Features.Add(new PricingCalculatorFeature("Offline mode"));

        var fleet = new PricingCalculatorPlan
        {
            Id = "fleet",
            Name = "Fleet",
            Description = "Routing, reports and roles for big operations",
            MonthlySeatPrice = 30,
            YearlySeatPrice = 24,
            RecommendFromSeats = 12
        };
        fleet.VolumeTiers.Add(new PricingCalculatorVolumeTier(64, 4));
        fleet.Features.Add(new PricingCalculatorFeature("Everything in Crew"));
        fleet.Features.Add(new PricingCalculatorFeature("Route planning"));
        fleet.Features.Add(new PricingCalculatorFeature { Kind = PricingCalculatorFeatureKind.VolumeDiscount, Text = "Fleet rate: {0} off each seat" });
        fleet.Features.Add(new PricingCalculatorFeature("Quarterly route review") { IsYearlyOnly = true });

        return new AvaloniaList<PricingCalculatorPlan> { crew, fleet };
    }
}
