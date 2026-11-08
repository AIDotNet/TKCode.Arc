using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>
/// Sample content of <see cref="PricingCalculator"/> (all invented): 23 seat stops, three plans (Solo, Studio, Scale) and
/// a simulated trial backend. A calculator without plans or stops of its own shows these.
/// </summary>
public static class PricingCalculatorSamples
{
    /// <summary>How long the simulated trial request takes (1.1 s, as in the reference preview).</summary>
    public static readonly TimeSpan TrialDelay = TimeSpan.FromMilliseconds(1100);

    /// <summary>The sample seat stops: 1–8, 10, 12, 15, 20, 25, 30, 40, 50, 60, 80, 100, 120, 150, 200, 300.</summary>
    public static IReadOnlyList<int> SeatStops { get; } = [1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 15, 20, 25, 30, 40, 50, 60, 80, 100, 120, 150, 200, 300];

    /// <summary>The seats the sample starts with (20).</summary>
    public const int DefaultSeats = 20;

    /// <summary>
    /// Three new sample plans: Solo ($15 a seat, $12 yearly, up to 8 seats), Studio ($25 / $20) and Scale ($40 / $32);
    /// Studio and Scale take $3 off every seat from 30 seats and $5 from 120; recommended from 1, 5 and 50 seats.
    /// </summary>
    public static AvaloniaList<PricingCalculatorPlan> CreatePlans()
    {
        var solo = new PricingCalculatorPlan
        {
            Id = "solo",
            Name = "Solo",
            Description = "For freelancers and tiny studios",
            MonthlySeatPrice = 15,
            YearlySeatPrice = 12,
            MaxSeats = 8,
            RecommendFromSeats = 1
        };
        solo.Features.Add(new PricingCalculatorFeature("Unlimited boards and pages"));
        solo.Features.Add(new PricingCalculatorFeature { Kind = PricingCalculatorFeatureKind.PerSeat, PerSeatAmount = 10, Text = "{0} GB file storage" });
        solo.Features.Add(new PricingCalculatorFeature("14 day activity history"));
        solo.Features.Add(new PricingCalculatorFeature("Community help desk"));

        var studio = new PricingCalculatorPlan
        {
            Id = "studio",
            Name = "Studio",
            Description = "For product squads that ship weekly",
            MonthlySeatPrice = 25,
            YearlySeatPrice = 20,
            RecommendFromSeats = 5
        };
        AddVolumeTiers(studio);
        studio.Features.Add(new PricingCalculatorFeature("All of Solo"));
        studio.Features.Add(new PricingCalculatorFeature { Kind = PricingCalculatorFeatureKind.PerSeat, PerSeatAmount = 25, Text = "{0} GB file storage" });
        studio.Features.Add(new PricingCalculatorFeature("Single sign-on with Google"));
        studio.Features.Add(new PricingCalculatorFeature { Kind = PricingCalculatorFeatureKind.VolumeDiscount });
        studio.Features.Add(new PricingCalculatorFeature("Kickoff workshop with our team") { IsYearlyOnly = true });

        var scale = new PricingCalculatorPlan
        {
            Id = "scale",
            Name = "Scale",
            Description = "For orgs with audit and compliance needs",
            MonthlySeatPrice = 40,
            YearlySeatPrice = 32,
            RecommendFromSeats = 50
        };
        AddVolumeTiers(scale);
        scale.Features.Add(new PricingCalculatorFeature("All of Studio"));
        scale.Features.Add(new PricingCalculatorFeature("SCIM user provisioning"));
        scale.Features.Add(new PricingCalculatorFeature("Two year audit trail"));
        scale.Features.Add(new PricingCalculatorFeature { Kind = PricingCalculatorFeatureKind.VolumeDiscount });
        scale.Features.Add(new PricingCalculatorFeature("Dedicated account partner") { MinSeats = 50 });
        scale.Features.Add(new PricingCalculatorFeature("99.9% uptime SLA") { IsYearlyOnly = true });

        return new AvaloniaList<PricingCalculatorPlan> { solo, studio, scale };
    }

    private static void AddVolumeTiers(PricingCalculatorPlan plan)
    {
        plan.VolumeTiers.Add(new PricingCalculatorVolumeTier(30, 3));
        plan.VolumeTiers.Add(new PricingCalculatorVolumeTier(120, 5));
    }

    /// <summary>
    /// The simulated trial backend of the preview: waits <see cref="TrialDelay"/> and succeeds. <paramref name="delay"/>
    /// replaces the wait (tests step it); null uses <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// </summary>
    public static Func<PricingCalculatorTrialRequest, CancellationToken, Task<bool>> SimulatedTrial(
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        var wait = delay ?? Task.Delay;
        return async (_, token) =>
        {
            await wait(TrialDelay, token);
            return true;
        };
    }
}
