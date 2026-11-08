using System.Globalization;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Invented sample content for <see cref="LiveCheckout"/> (the block's defaults and the Gallery demos): three plans, three
/// add-ons, six billing countries, three fictional wallets, a billing sample, test cards and coupons, plus the simulated
/// backend the preview uses when no handler is set. The delays are injectable so tests can step them.
/// </summary>
public static class LiveCheckoutSamples
{
    /// <summary>How long the simulated card payment takes.</summary>
    public static readonly TimeSpan CardDelay = TimeSpan.FromMilliseconds(1500);

    /// <summary>How long the simulated wallet payment takes.</summary>
    public static readonly TimeSpan WalletDelay = TimeSpan.FromMilliseconds(1100);

    /// <summary>How long the simulated coupon check takes.</summary>
    public static readonly TimeSpan CouponDelay = TimeSpan.FromMilliseconds(480);

    /// <summary>A card number that passes the checksum but that the simulator always declines.</summary>
    public const string DeclinedCardNumber = "4000 0000 0000 9995";

    /// <summary>The simulator's decline message.</summary>
    public const string DeclineMessage = "This card was declined. Try a different one.";

    /// <summary>The coupons the simulator accepts: code → percent.</summary>
    public static IReadOnlyDictionary<string, decimal> Coupons { get; } = new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
        ["SPRING15"] = 15,
        ["CREW10"] = 10
    };

    /// <summary>Solo, Crew and Fleet, priced per seat per month.</summary>
    public static List<LiveCheckoutPlan> Plans() =>
    [
        new() { Id = "solo", Name = "Solo", MonthlySeatPrice = 14m, Blurb = "Personal notes and boards" },
        new() { Id = "crew", Name = "Crew", MonthlySeatPrice = 28m, Blurb = "Automations and guests" },
        new() { Id = "fleet", Name = "Fleet", MonthlySeatPrice = 49m, Blurb = "Audit, SLA and admin" }
    ];

    /// <summary>The plan selected by default.</summary>
    public const string DefaultPlanId = "crew";

    /// <summary>Priority care, Single sign-on and Audit archive, priced per month.</summary>
    public static List<LiveCheckoutAddon> Addons() =>
    [
        new() { Id = "care", Name = "Priority care", Description = "Answers within four hours", MonthlyPrice = 45m },
        new() { Id = "sso", Name = "Single sign-on", Description = "SAML and SCIM directory sync", MonthlyPrice = 89m },
        new() { Id = "archive", Name = "Audit archive", Description = "Two years of history", MonthlyPrice = 25m }
    ];

    /// <summary>The add-on ticked by default.</summary>
    public const string DefaultAddonId = "care";

    /// <summary>Six billing countries with their tax and postal code rules.</summary>
    public static List<LiveCheckoutCountry> Countries() =>
    [
        new()
        {
            Code = "NL", Name = "Netherlands", TaxRate = 0.21m, TaxLabel = "VAT",
            PostalPattern = @"\d{4}\s?[A-Za-z]{2}", PostalHint = "Use four digits and two letters, like 1016 HP"
        },
        new()
        {
            Code = "US", Name = "United States", TaxRate = 0m, TaxLabel = "Sales tax",
            PostalPattern = @"\d{5}(-\d{4})?", PostalHint = "Use a 5-digit ZIP or ZIP+4"
        },
        new()
        {
            Code = "FR", Name = "France", TaxRate = 0.20m, TaxLabel = "TVA",
            PostalPattern = @"\d{5}", PostalHint = "Use five digits"
        },
        new()
        {
            Code = "JP", Name = "Japan", TaxRate = 0.10m, TaxLabel = "Consumption tax",
            PostalPattern = @"\d{3}-?\d{4}", PostalHint = "Use the 123-4567 format"
        },
        new()
        {
            Code = "CA", Name = "Canada", TaxRate = 0.05m, TaxLabel = "GST",
            PostalPattern = @"[A-Za-z]\d[A-Za-z]\s?\d[A-Za-z]\d", PostalHint = "Use the A1A 1A1 format"
        },
        new()
        {
            Code = "NZ", Name = "New Zealand", TaxRate = 0.15m, TaxLabel = "GST",
            PostalPattern = @"\d{4}", PostalHint = "Use four digits"
        }
    ];

    /// <summary>Orbit Pay, Kite Wallet and Lumen Link: invented wallets with self-drawn marks.</summary>
    public static List<LiveCheckoutWallet> Wallets() =>
    [
        new()
        {
            Id = "orbit", Name = "Orbit Pay", MarkText = "Pay",
            // A planet on a tilted orbit.
            Glyph = Geometry.Parse("M14.6 12a2.6 2.6 0 1 1-5.2 0a2.6 2.6 0 1 1 5.2 0Z M3.2 15.6c-1.2-2.1 2.4-5.6 8-7.8s10.6-2.3 11.6-.2c.6 1.3-.6 3.2-3 5"),
            Background = Colors.Black, Foreground = Colors.White,
            DarkBackground = Colors.White, DarkForeground = Colors.Black
        },
        new()
        {
            Id = "kite", Name = "Kite Wallet", MarkText = "Kite",
            // A kite with its spars and a curl of tail.
            Glyph = Geometry.Parse("M12 2.5l6 7.5-6 9-6-9Z M6 10h12 M12 2.5V19 M12 19c-1.6 1-.2 2 .8 2.5"),
            Background = Color.FromRgb(0x1d, 0x23, 0x2b), Foreground = Colors.White,
            BorderColor = Color.FromRgb(0x6b, 0x71, 0x78)
        },
        new()
        {
            Id = "lumen", Name = "Lumen Link", MarkText = "LumenLink", IsMarkItalic = true,
            Background = Color.FromRgb(0xf6, 0xbb, 0x3e), Foreground = Color.FromRgb(0x1f, 0x2a, 0x63)
        }
    ];

    /// <summary>The billing sample the form starts with.</summary>
    public static class Billing
    {
        /// <summary>Receipt email.</summary>
        public const string Email = "mara@lumenworks.dev";

        /// <summary>Name on the card.</summary>
        public const string Name = "Mara Lind";

        /// <summary>Country code.</summary>
        public const string CountryCode = "NL";

        /// <summary>Street address.</summary>
        public const string Address = "Prinsengracht 412";

        /// <summary>City.</summary>
        public const string City = "Amsterdam";

        /// <summary>Postal code.</summary>
        public const string PostalCode = "1016 HP";
    }

    /// <summary>A card that the simulator charges.</summary>
    public static LiveCheckoutTestCard TestCard() => new() { Number = "4111 1111 1111 1111", Expiry = "12/30", Cvc = "737" };

    /// <summary>A card that the simulator declines.</summary>
    public static LiveCheckoutTestCard DeclinedCard() => new() { Number = DeclinedCardNumber, Expiry = "12/30", Cvc = "737" };

    /// <summary>
    /// The sample coupon check: after <see cref="CouponDelay"/> it accepts the codes in <see cref="Coupons"/> and refuses
    /// the rest. <paramref name="delay"/> replaces the wait (tests pass one that completes at once or on demand).
    /// </summary>
    public static Func<string, CancellationToken, Task<LiveCheckoutCouponResult>> CreateCouponValidator(
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        var wait = delay ?? Task.Delay;
        return async (code, token) =>
        {
            await wait(CouponDelay, token);
            var key = code.Trim().ToUpperInvariant();
            return Coupons.TryGetValue(key, out var percent)
                ? LiveCheckoutCouponResult.Accept(key, percent)
                : LiveCheckoutCouponResult.Reject(key);
        };
    }

    /// <summary>
    /// The simulated payment: card orders take <see cref="CardDelay"/> and fail for <see cref="DeclinedCardNumber"/>; wallet
    /// orders take <see cref="WalletDelay"/> and always succeed. Returns a receipt numbered "LW-" and six random digits.
    /// </summary>
    public static Func<LiveCheckoutOrder, CancellationToken, Task<LiveCheckoutReceipt?>> CreatePaymentSimulator(
        Func<TimeSpan, CancellationToken, Task>? delay = null, Func<int>? random = null)
    {
        var wait = delay ?? Task.Delay;
        var next = random ?? (() => Random.Shared.Next(0, 1_000_000));
        var declined = LiveCheckoutValidation.Digits(DeclinedCardNumber);
        return async (order, token) =>
        {
            var wallet = order.PaymentMethod == LiveCheckoutPaymentMethod.Wallet;
            await wait(wallet ? WalletDelay : CardDelay, token);
            if (!wallet && order.CardNumber == declined)
            {
                throw new InvalidOperationException(DeclineMessage);
            }
            return new LiveCheckoutReceipt { OrderNumber = "LW-" + next().ToString("D6", CultureInfo.InvariantCulture) };
        };
    }
}
