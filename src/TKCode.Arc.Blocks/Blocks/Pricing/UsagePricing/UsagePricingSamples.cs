namespace TKCode.Arc;

/// <summary>
/// The invented sample of <see cref="UsagePricing"/> (also its defaults): three plans (Spark, Studio, Fleet), 23 seat
/// stops and 27 monthly call stops, and a simulated trial backend for demos.
/// </summary>
public static class UsagePricingSamples
{
    /// <summary>Seat stops: 1–6 one by one, then wider steps up to 250.</summary>
    public static IReadOnlyList<int> SeatStops { get; } =
        [1, 2, 3, 4, 5, 6, 8, 10, 12, 14, 16, 20, 24, 30, 36, 42, 50, 60, 80, 100, 125, 160, 250];

    /// <summary>Monthly call stops from 100k to 300M, roughly logarithmic.</summary>
    public static IReadOnlyList<double> UsageStops { get; } =
    [
        100_000, 200_000, 400_000, 600_000, 800_000, 1_000_000, 1_500_000, 2_000_000, 3_000_000, 4_000_000, 5_000_000,
        6_000_000, 8_000_000, 10_000_000, 12_000_000, 15_000_000, 20_000_000, 25_000_000, 30_000_000, 40_000_000,
        50_000_000, 60_000_000, 80_000_000, 100_000_000, 150_000_000, 200_000_000, 300_000_000
    ];

    /// <summary>The seat stop selected at first (16 seats).</summary>
    public const int DefaultSeatIndex = 10;

    /// <summary>The usage stop selected at first (12M calls).</summary>
    public const int DefaultUsageIndex = 14;

    /// <summary>Creates the three sample plans (a new list each call).</summary>
    public static List<UsagePricingPlan> CreatePlans()
    {
        var spark = new UsagePricingPlan("Spark", 14m, 2m, 1_000_000, 9m, 4, 1_500_000);
        spark.Features.AddRange(
        [
            new UsagePricingFeature("history", "Event history", "7 days"),
            new UsagePricingFeature("rate", "Rate cap", "120 req/s"),
            new UsagePricingFeature("help", "Help channel", "Email")
        ]);
        var studio = new UsagePricingPlan("Studio", 26m, 5m, 8_000_000, 5m, 36, 20_000_000);
        studio.Features.AddRange(
        [
            new UsagePricingFeature("links", "Branded links", "Up to 12"),
            new UsagePricingFeature("trail", "Activity trail", "60 days"),
            new UsagePricingFeature("history", "Event history", "30 days"),
            new UsagePricingFeature("rate", "Rate cap", "1,200 req/s"),
            new UsagePricingFeature("help", "Help channel", "Chat")
        ]);
        var fleet = new UsagePricingPlan("Fleet", 42m, 7m, 40_000_000, 2m, null, null);
        fleet.Features.AddRange(
        [
            new UsagePricingFeature("sso", "Single sign-on & provisioning"),
            new UsagePricingFeature("uptime", "Uptime promise", "99.95%"),
            new UsagePricingFeature("links", "Branded links", "Unlimited"),
            new UsagePricingFeature("trail", "Activity trail", "18 months"),
            new UsagePricingFeature("history", "Event history", "12 months"),
            new UsagePricingFeature("rate", "Rate cap", "12,000 req/s"),
            new UsagePricingFeature("help", "Help channel", "Dedicated engineer")
        ]);
        return [spark, studio, fleet];
    }

    /// <summary>
    /// A stand-in for a sign-up backend: waits <paramref name="delay"/> and succeeds, or throws
    /// <see cref="InvalidOperationException"/> when <paramref name="fail"/> says so. <paramref name="wait"/> replaces
    /// <see cref="Task.Delay(TimeSpan, CancellationToken)"/> (tests pass a wait they step themselves).
    /// </summary>
    public static Func<UsagePricingTrialRequest, CancellationToken, Task> SimulatedTrial(TimeSpan delay,
        Func<UsagePricingTrialRequest, bool>? fail = null, Func<TimeSpan, CancellationToken, Task>? wait = null)
    {
        wait ??= Task.Delay;
        return async (request, token) =>
        {
            await wait(delay, token);
            if (fail?.Invoke(request) == true)
            {
                throw new InvalidOperationException($"The {request.Plan.Name} trial could not be started.");
            }
        };
    }
}
