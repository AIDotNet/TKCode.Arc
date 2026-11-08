using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>One day of credit usage for <see cref="CreditTopUp.Usage"/>: the day and the credits spent on it.</summary>
/// <param name="Date">The calendar day.</param>
/// <param name="Credits">Credits spent that day (the last day of the list is today, so it may be a partial day).</param>
public sealed record CreditTopUpUsageDay(DateOnly Date, long Credits);

/// <summary>A volume bonus: buying at least <see cref="MinCredits"/> adds <see cref="Percent"/> percent on top.</summary>
/// <param name="MinCredits">The smallest purchase, in credits, that earns this bonus.</param>
/// <param name="Percent">The bonus in percent of the credits bought (10 means 10%).</param>
public sealed record CreditTopUpBonusTier(long MinCredits, double Percent);

/// <summary>
/// What credits cost: a price per credit in an ISO 4217 currency and the volume bonuses. Every price, badge, total and
/// the per 1,000 rate of <see cref="CreditTopUp"/> derive from it; use the same object with
/// <see cref="CreditTopUp.Quote"/> on a server to price the same order.
/// </summary>
public sealed record CreditTopUpPricing
{
    /// <summary>The price of one credit (default 0.008).</summary>
    public decimal UnitPrice { get; init; } = 0.008m;

    /// <summary>ISO 4217 currency code of every price ("USD" by default).</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>The volume bonuses; the highest tier at or below the amount applies (default +10% from 6,000, +15% from 24,000).</summary>
    public IReadOnlyList<CreditTopUpBonusTier> BonusTiers { get; init; } =
    [
        new CreditTopUpBonusTier(6_000, 10),
        new CreditTopUpBonusTier(24_000, 15)
    ];

    /// <summary>The default pricing: $0.008 a credit, +10% from 6,000 and +15% from 24,000.</summary>
    public static CreditTopUpPricing Default { get; } = new();
}

/// <summary>The custom amount slider's range in credits.</summary>
public sealed record CreditTopUpRange
{
    /// <summary>The smallest amount (default 500).</summary>
    public long Minimum { get; init; } = 500;

    /// <summary>The largest amount (default 30,000).</summary>
    public long Maximum { get; init; } = 30_000;

    /// <summary>The step amounts snap to (default 500).</summary>
    public long Step { get; init; } = 500;

    /// <summary>500 to 30,000 in steps of 500.</summary>
    public static CreditTopUpRange Default { get; } = new();
}

/// <summary>The price of an amount of credits under a <see cref="CreditTopUpPricing"/>.</summary>
/// <param name="Credits">The credits bought.</param>
/// <param name="Bonus">The bonus credits added on top.</param>
/// <param name="Total">What the balance grows by: credits plus bonus.</param>
/// <param name="Price">What is charged: credits × unit price.</param>
/// <param name="Currency">The ISO 4217 code of the price.</param>
public sealed record CreditTopUpQuote(long Credits, long Bonus, long Total, decimal Price, string Currency);

/// <summary>The fictional card networks <see cref="CreditTopUpPaymentMethod"/> can show a mark for.</summary>
public enum CreditTopUpCardBrand
{
    /// <summary>A plain card glyph.</summary>
    Generic,

    /// <summary>The Nimbus mark: a capsule with three slanted stripes.</summary>
    Nimbus,

    /// <summary>The Orbit mark: a disc crossed by a tilted ring.</summary>
    Orbit
}

/// <summary>A saved card of <see cref="CreditTopUp.PaymentMethods"/>.</summary>
public sealed record CreditTopUpPaymentMethod
{
    /// <summary>Your identifier of the card; reported by <see cref="CreditTopUp.PaymentMethodChanged"/> and in the order.</summary>
    public required string Id { get; init; }

    /// <summary>The network whose mark is drawn.</summary>
    public CreditTopUpCardBrand Brand { get; init; }

    /// <summary>The brand name read in the card row ("Nimbus"); empty uses the brand's own name.</summary>
    public string? BrandName { get; init; }

    /// <summary>The last four digits of the card number.</summary>
    public required string Last4 { get; init; }

    /// <summary>The expiry month (1–12).</summary>
    public int ExpiryMonth { get; init; }

    /// <summary>The expiry year (four digits).</summary>
    public int ExpiryYear { get; init; }

    /// <summary>The name shown for the card's network.</summary>
    public string DisplayBrand => string.IsNullOrWhiteSpace(BrandName)
        ? Brand switch
        {
            CreditTopUpCardBrand.Nimbus => "Nimbus",
            CreditTopUpCardBrand.Orbit => "Orbit",
            _ => "Card"
        }
        : BrandName;
}

/// <summary>An order handed to <see cref="CreditTopUp.PurchaseHandler"/>: the quote and the card to charge.</summary>
/// <param name="Credits">The credits bought.</param>
/// <param name="Bonus">The bonus credits added on top.</param>
/// <param name="Total">What the balance grows by.</param>
/// <param name="Price">What is charged.</param>
/// <param name="Currency">The ISO 4217 code of the price.</param>
/// <param name="PaymentMethodId">The <see cref="CreditTopUpPaymentMethod.Id"/> of the card to charge (null when no card is saved).</param>
public sealed record CreditTopUpOrder(long Credits, long Bonus, long Total, decimal Price, string Currency, string? PaymentMethodId);

/// <summary>Auto top-up settings, as <see cref="CreditTopUp.AutoTopUpChanged"/> reports them.</summary>
/// <param name="IsEnabled">Whether auto top-up is on.</param>
/// <param name="Threshold">The balance below which a top-up happens.</param>
/// <param name="Credits">The amount a top-up adds, bonus included (the amount selected in the card).</param>
public sealed record CreditTopUpAutoSettings(bool IsEnabled, long Threshold, long Credits);

/// <summary>Where a purchase stands.</summary>
public enum CreditTopUpPhase
{
    /// <summary>Ready to buy.</summary>
    Idle,

    /// <summary>The charge is running; the button shows a spinner and ignores clicks.</summary>
    Processing,

    /// <summary>The credits were added; the button and the status line say so for a moment.</summary>
    Done,

    /// <summary>The last charge failed; the status line shows why and the button offers to try again.</summary>
    Error
}

/// <summary>How the balance reads against the auto top-up threshold.</summary>
public enum CreditTopUpHealth
{
    /// <summary>The balance is at or above the threshold.</summary>
    Healthy,

    /// <summary>The balance is below the threshold.</summary>
    RunningLow
}

/// <summary>Arguments of <see cref="CreditTopUp.AutoTopUpChanged"/>.</summary>
public sealed class CreditTopUpAutoTopUpChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CreditTopUpAutoTopUpChangedEventArgs(RoutedEvent routedEvent, CreditTopUpAutoSettings settings) : base(routedEvent)
    {
        Settings = settings;
    }

    /// <summary>The new settings.</summary>
    public CreditTopUpAutoSettings Settings { get; }
}

/// <summary>Arguments of <see cref="CreditTopUp.PaymentMethodChanged"/>.</summary>
public sealed class CreditTopUpPaymentMethodChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CreditTopUpPaymentMethodChangedEventArgs(RoutedEvent routedEvent, CreditTopUpPaymentMethod paymentMethod) : base(routedEvent)
    {
        PaymentMethod = paymentMethod;
    }

    /// <summary>The card now selected.</summary>
    public CreditTopUpPaymentMethod PaymentMethod { get; }
}

/// <summary>Arguments of <see cref="CreditTopUp.Purchased"/>.</summary>
public sealed class CreditTopUpPurchasedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CreditTopUpPurchasedEventArgs(RoutedEvent routedEvent, CreditTopUpOrder order, long balance) : base(routedEvent)
    {
        Order = order;
        Balance = balance;
    }

    /// <summary>The order that went through.</summary>
    public CreditTopUpOrder Order { get; }

    /// <summary>The balance after the credits were added.</summary>
    public long Balance { get; }
}

/// <summary>Arguments of <see cref="CreditTopUp.PurchaseFailed"/>.</summary>
public sealed class CreditTopUpPurchaseFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CreditTopUpPurchaseFailedEventArgs(RoutedEvent routedEvent, CreditTopUpOrder order, string message) : base(routedEvent)
    {
        Order = order;
        Message = message;
    }

    /// <summary>The order that failed.</summary>
    public CreditTopUpOrder Order { get; }

    /// <summary>The message shown in the status line.</summary>
    public string Message { get; }
}
