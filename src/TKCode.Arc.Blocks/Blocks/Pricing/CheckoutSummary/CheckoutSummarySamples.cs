namespace TKCode.Arc;

/// <summary>
/// Invented sample content and a simulated backend for <see cref="CheckoutSummary"/>: a yearly drawing app, a plan
/// discount, sales tax, two promo codes (HELLO15 takes 15% off, FRIEND5 takes 5 off) checked in 700 ms, and a payment that
/// takes 1400 ms. Each simulation takes an optional delay function so tests can step time.
/// </summary>
public static class CheckoutSummarySamples
{
    /// <summary>How long the simulated code check takes (700 ms).</summary>
    public static readonly TimeSpan CouponCheckDelay = TimeSpan.FromMilliseconds(700);

    /// <summary>How long the simulated payment takes (1400 ms).</summary>
    public static readonly TimeSpan PaymentDelay = TimeSpan.FromMilliseconds(1400);

    /// <summary>The code that takes 15% off.</summary>
    public const string PercentCode = "HELLO15";

    /// <summary>The code that takes a fixed 5 off.</summary>
    public const string FixedCode = "FRIEND5";

    /// <summary>Placeholder that hints at the sample code: "Try HELLO15".</summary>
    public const string Placeholder = "Try HELLO15";

    /// <summary>A new sample product: "Sketchbook Pro, annual", 120, billed yearly.</summary>
    public static CheckoutSummaryItem CreateItem() => new()
    {
        Name = "Sketchbook Pro, annual",
        Description = "Infinite canvases and backups for one maker",
        Price = 120m,
        Period = "billed yearly"
    };

    /// <summary>A new sample automatic discount: "Annual plan", three months free, −30.</summary>
    public static CheckoutSummaryLine CreateAdjustment() => new()
    {
        Id = "annual",
        Label = "Annual plan",
        Note = "Three months free",
        Amount = -30m
    };

    /// <summary>A new sample tax: "Sales tax" at 7.5%.</summary>
    public static CheckoutSummaryTax CreateTax() => new() { Label = "Sales tax", Rate = 0.075m };

    /// <summary>The sample labels: the promo field hints at <see cref="PercentCode"/>.</summary>
    public static CheckoutSummaryLabels Labels { get; } = new() { CouponPlaceholder = Placeholder };

    /// <summary>
    /// The sample code check: after <see cref="CouponCheckDelay"/>, HELLO15 (any case, spaces around ignored) is 15% off,
    /// FRIEND5 is 5 off, anything else is refused with a hint.
    /// </summary>
    /// <param name="delay">Replaces <c>Task.Delay</c> (tests step a fake clock with it).</param>
    public static Func<string, CancellationToken, Task<CheckoutSummaryCouponResult>> CreateCouponValidator(
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        return async (code, token) =>
        {
            await delay(CouponCheckDelay, token);
            return Check(code);
        };
    }

    /// <summary>The sample rule without the wait.</summary>
    public static CheckoutSummaryCouponResult Check(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return normalized switch
        {
            PercentCode => CheckoutSummaryCouponResult.ValidPercent(PercentCode, 0.15m, "Hello, 15% off"),
            FixedCode => CheckoutSummaryCouponResult.Valid(FixedCode, 5m, "Friend credit"),
            _ => CheckoutSummaryCouponResult.Invalid("We couldn't find that code. Try HELLO15.")
        };
    }

    /// <summary>
    /// The sample payment: waits <see cref="PaymentDelay"/>, then succeeds, or throws when <paramref name="shouldFail"/>
    /// answers true at that moment.
    /// </summary>
    /// <param name="shouldFail">Asked when the wait ends; true makes the attempt fail.</param>
    /// <param name="delay">Replaces <c>Task.Delay</c> (tests step a fake clock with it).</param>
    public static Func<CancellationToken, Task> CreatePayHandler(Func<bool>? shouldFail = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        return async token =>
        {
            await delay(PaymentDelay, token);
            if (shouldFail?.Invoke() == true)
            {
                throw new InvalidOperationException("The card was declined. Try another card or try again.");
            }
        };
    }
}
