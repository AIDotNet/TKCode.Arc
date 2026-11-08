namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="CreditTopUp"/> (the card's defaults) and replaceable simulated backends for demos.
/// </summary>
public static class CreditTopUpSamples
{
    /// <summary>The sample workspace's starting balance: 1,260 credits.</summary>
    public const long Balance = 1_260;

    /// <summary>How long the built-in simulated charge takes: 1.2 seconds.</summary>
    public static readonly TimeSpan SimulatedChargeTime = TimeSpan.FromMilliseconds(1200);

    private static readonly long[] s_usage = [412, 455, 438, 497, 351, 268, 247, 476, 521, 489, 540, 503, 529, 362];

    /// <summary>
    /// Fourteen days of usage, Nov 2 to Nov 15 2026, the last day (today) partial: the full days average 440 a day.
    /// </summary>
    public static IReadOnlyList<CreditTopUpUsageDay> Usage() => UsageEnding(new DateOnly(2026, 11, 15));

    /// <summary>The same fourteen values, ending on <paramref name="today"/>.</summary>
    public static IReadOnlyList<CreditTopUpUsageDay> UsageEnding(DateOnly today)
    {
        var days = new CreditTopUpUsageDay[s_usage.Length];
        for (var i = 0; i < s_usage.Length; i++)
        {
            days[i] = new CreditTopUpUsageDay(today.AddDays(i - (s_usage.Length - 1)), s_usage[i]);
        }
        return days;
    }

    /// <summary>The packs: 1,500, 6,000 and 24,000 credits.</summary>
    public static IReadOnlyList<long> Packs() => [1_500, 6_000, 24_000];

    /// <summary>The auto top-up thresholds: 750, 1,500 and 3,000 credits.</summary>
    public static IReadOnlyList<long> Thresholds() => [750, 1_500, 3_000];

    /// <summary>Two saved cards: Nimbus ending 3108 (11/2028) and Orbit ending 7720 (06/2029).</summary>
    public static IReadOnlyList<CreditTopUpPaymentMethod> PaymentMethods() =>
    [
        new CreditTopUpPaymentMethod { Id = "card_nimbus_3108", Brand = CreditTopUpCardBrand.Nimbus, Last4 = "3108", ExpiryMonth = 11, ExpiryYear = 2028 },
        new CreditTopUpPaymentMethod { Id = "card_orbit_7720", Brand = CreditTopUpCardBrand.Orbit, Last4 = "7720", ExpiryMonth = 6, ExpiryYear = 2029 }
    ];

    /// <summary>A purchase handler that waits <paramref name="delay"/> and succeeds (a stand-in for a payment API).</summary>
    public static Func<CreditTopUpOrder, CancellationToken, Task> SimulatedPurchase(TimeSpan delay)
        => (_, cancellation) => Task.Delay(delay, cancellation);

    /// <summary>
    /// A purchase handler that fails the first <paramref name="failures"/> charges with <paramref name="message"/> and then
    /// succeeds, each after <paramref name="delay"/>.
    /// </summary>
    public static Func<CreditTopUpOrder, CancellationToken, Task> DecliningPurchase(TimeSpan delay, int failures = 1,
        string message = "Card declined. Try another card.")
    {
        var left = failures;
        return async (_, cancellation) =>
        {
            await Task.Delay(delay, cancellation);
            if (left > 0)
            {
                left--;
                throw new InvalidOperationException(message);
            }
        };
    }
}
