namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="CheckoutSummary"/>. Replace the whole object to localize the block; entries with a
/// number receive the already formatted amount.
/// </summary>
public sealed class CheckoutSummaryLabels
{
    /// <summary>Accessible name of the promo code field: "Promo code".</summary>
    public string CouponFieldName { get; init; } = "Promo code";

    /// <summary>Placeholder of the promo code field: "Promo code" (set it to a hint such as "Try HELLO15").</summary>
    public string CouponPlaceholder { get; init; } = "Promo code";

    /// <summary>The button that checks a code: "Apply".</summary>
    public string Apply { get; init; } = "Apply";

    /// <summary>Accessible name of the spinner while a code is checked: "Checking code".</summary>
    public string CheckingCode { get; init; } = "Checking code";

    /// <summary>Word after the code on the applied chip: "applied".</summary>
    public string Applied { get; init; } = "applied";

    /// <summary>Accessible name of the chip's remove button: code ⇒ "Remove code HELLO15".</summary>
    public Func<string, string> RemoveCode { get; init; } = code => $"Remove code {code}";

    /// <summary>Shown when a code check fails without a message of its own: "That code could not be applied."</summary>
    public string CouponFailed { get; init; } = "That code could not be applied.";

    /// <summary>First line of the breakdown: "Subtotal".</summary>
    public string Subtotal { get; init; } = "Subtotal";

    /// <summary>Tax label with its rate: (label, rate) ⇒ "Sales tax (7.5%)".</summary>
    public Func<string, string, string> TaxWithRate { get; init; } = (label, rate) => $"{label} ({rate})";

    /// <summary>Shown in place of the tax amount when there is no rate: "Calculated at checkout".</summary>
    public string TaxPending { get; init; } = "Calculated at checkout";

    /// <summary>Label of the total: "Total".</summary>
    public string Total { get; init; } = "Total";

    /// <summary>What a screen reader hears for the total: amount ⇒ "Total $96.75".</summary>
    public Func<string, string> TotalAnnouncement { get; init; } = amount => $"Total {amount}";

    /// <summary>Pay button label: amount ⇒ "Pay $96.75".</summary>
    public Func<string, string> Pay { get; init; } = amount => $"Pay {amount}";

    /// <summary>Pay button while the payment runs: "Processing".</summary>
    public string Processing { get; init; } = "Processing";

    /// <summary>Pay button after the payment went through: "Paid".</summary>
    public string Paid { get; init; } = "Paid";

    /// <summary>Pay button after a failed payment: "Try again".</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>Shown under the button when a payment fails without a message of its own: "The payment did not go through. Nothing was charged."</summary>
    public string PayFailed { get; init; } = "The payment did not go through. Nothing was charged.";
}
