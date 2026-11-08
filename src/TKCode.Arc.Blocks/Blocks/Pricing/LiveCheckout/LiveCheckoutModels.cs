using System.Text.RegularExpressions;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How often a <see cref="LiveCheckout"/> subscription is billed.</summary>
public enum LiveCheckoutBilling
{
    /// <summary>One month at a time, at the plan's monthly seat price.</summary>
    Monthly,

    /// <summary>Twelve months up front, with <see cref="LiveCheckout.YearlyDiscount"/> taken off.</summary>
    Yearly
}

/// <summary>A card network the checkout recognises from the first digits of a card number (fictional networks).</summary>
public enum LiveCheckoutCardBrand
{
    /// <summary>No network recognised yet; no name is shown.</summary>
    None,

    /// <summary>Numbers starting with 4: sixteen digits in groups of four.</summary>
    Novacard,

    /// <summary>Numbers starting with 51–55 or 2221–2720: sixteen digits in groups of four.</summary>
    Meridian,

    /// <summary>Numbers starting with 34 or 37: fifteen digits grouped 4-6-5, with a four-digit security code.</summary>
    Atlas
}

/// <summary>How an order was paid.</summary>
public enum LiveCheckoutPaymentMethod
{
    /// <summary>With the card fields.</summary>
    Card,

    /// <summary>With one of the express wallet buttons (see <see cref="LiveCheckoutOrder.WalletId"/>).</summary>
    Wallet
}

/// <summary>What kind of row a <see cref="LiveCheckoutLine"/> is.</summary>
public enum LiveCheckoutLineKind
{
    /// <summary>The selected plan for the chosen seats.</summary>
    Plan,

    /// <summary>A selected add-on.</summary>
    Addon
}

/// <summary>One plan a customer can choose in a <see cref="LiveCheckout"/>.</summary>
public sealed class LiveCheckoutPlan
{
    /// <summary>Stable id, matched by <see cref="LiveCheckout.SelectedPlanId"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Plan name shown on the card and in the summary ("Crew").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Price of one seat for one month, in the checkout's currency (major units, such as 28.00).</summary>
    public decimal MonthlySeatPrice { get; set; }

    /// <summary>One short line under the price; null hides it.</summary>
    public string? Blurb { get; set; }
}

/// <summary>An optional add-on with a flat monthly price, independent of the seat count.</summary>
public sealed class LiveCheckoutAddon
{
    /// <summary>Stable id, listed in <see cref="LiveCheckout.SelectedAddonIds"/> while it is chosen.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Add-on name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One short line under the name; null hides it.</summary>
    public string? Description { get; set; }

    /// <summary>Flat price for one month, in the checkout's currency.</summary>
    public decimal MonthlyPrice { get; set; }
}

/// <summary>A billing country: its sales tax and the shape of its postal codes.</summary>
public sealed class LiveCheckoutCountry
{
    /// <summary>ISO 3166 code, matched by <see cref="LiveCheckout.CountryCode"/>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Name shown in the country list.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Tax rate from 0 to 1 (0.21 for 21%); 0 shows the tax row as not charged.</summary>
    public decimal TaxRate { get; set; }

    /// <summary>Name of the tax ("VAT", "GST"); empty falls back to <see cref="LiveCheckoutLabels.TaxFallback"/>.</summary>
    public string? TaxLabel { get; set; }

    /// <summary>Pattern a postal code must match in full (case-insensitive); null accepts any non-empty code.</summary>
    public string? PostalPattern { get; set; }

    /// <summary>Message shown when a postal code does not match <see cref="PostalPattern"/>.</summary>
    public string? PostalHint { get; set; }

    internal bool MatchesPostal(string value)
    {
        if (string.IsNullOrEmpty(PostalPattern))
        {
            return true;
        }
        return Regex.IsMatch(value, "^(?:" + PostalPattern + ")$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
    }
}

/// <summary>An express wallet button above the card form. Wallet buttons carry their own colors, not theme colors.</summary>
public sealed class LiveCheckoutWallet
{
    /// <summary>Stable id, reported in <see cref="LiveCheckoutOrder.WalletId"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Wallet name: the button's accessible name and the receipt's payment method.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Word mark drawn on the button ("Pay"); empty shows only <see cref="Glyph"/>.</summary>
    public string? MarkText { get; set; }

    /// <summary>A small mark drawn before <see cref="MarkText"/> in a 24-unit box; null draws none.</summary>
    public Geometry? Glyph { get; set; }

    /// <summary>When true <see cref="Glyph"/> is filled; otherwise it is stroked like a Lucide icon.</summary>
    public bool IsGlyphFilled { get; set; }

    /// <summary>When true the word mark is set in italics.</summary>
    public bool IsMarkItalic { get; set; }

    /// <summary>Button fill in light themes.</summary>
    public Color Background { get; set; } = Colors.Black;

    /// <summary>Mark color in light themes.</summary>
    public Color Foreground { get; set; } = Colors.White;

    /// <summary>Optional 1px edge; null draws none.</summary>
    public Color? BorderColor { get; set; }

    /// <summary>Button fill in dark themes; null keeps <see cref="Background"/>.</summary>
    public Color? DarkBackground { get; set; }

    /// <summary>Mark color in dark themes; null keeps <see cref="Foreground"/>.</summary>
    public Color? DarkForeground { get; set; }
}

/// <summary>A card the "Use test card" button fills in.</summary>
public sealed class LiveCheckoutTestCard
{
    /// <summary>Card number, with or without spaces.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Expiry as MM/YY.</summary>
    public string Expiry { get; set; } = string.Empty;

    /// <summary>Security code.</summary>
    public string Cvc { get; set; } = string.Empty;
}

/// <summary>The answer of <see cref="LiveCheckout.ValidateCouponAsync"/>.</summary>
public sealed class LiveCheckoutCouponResult
{
    /// <summary>True when the code is accepted.</summary>
    public bool IsValid { get; init; }

    /// <summary>The code as it should be shown (usually upper case).</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Discount in percent (0–100) taken off the subtotal.</summary>
    public decimal Percent { get; init; }

    /// <summary>Why the code was refused; null uses <see cref="LiveCheckoutLabels.CouponNotFound"/>.</summary>
    public string? Message { get; init; }

    /// <summary>An accepted code.</summary>
    public static LiveCheckoutCouponResult Accept(string code, decimal percent) => new() { IsValid = true, Code = code, Percent = percent };

    /// <summary>A refused code.</summary>
    public static LiveCheckoutCouponResult Reject(string code, string? message = null) => new() { IsValid = false, Code = code, Message = message };
}

/// <summary>One row of the order summary.</summary>
public sealed record LiveCheckoutLine(LiveCheckoutLineKind Kind, string Id, string Title, string Detail, decimal Amount);

/// <summary>The money of an order: what the lines add up to, the coupon, the tax and what is charged.</summary>
public readonly record struct LiveCheckoutTotals(decimal Subtotal, decimal Discount, decimal Tax, decimal Total);

/// <summary>Everything a payment handler needs to charge the order.</summary>
public sealed class LiveCheckoutOrder
{
    /// <summary>The chosen plan's id.</summary>
    public string PlanId { get; init; } = string.Empty;

    /// <summary>Seats bought.</summary>
    public int Seats { get; init; }

    /// <summary>The billing period.</summary>
    public LiveCheckoutBilling Billing { get; init; }

    /// <summary>Ids of the chosen add-ons, in list order.</summary>
    public IReadOnlyList<string> AddonIds { get; init; } = Array.Empty<string>();

    /// <summary>The applied coupon, or null.</summary>
    public string? CouponCode { get; init; }

    /// <summary>The applied coupon's percent (0 without a coupon).</summary>
    public decimal CouponPercent { get; init; }

    /// <summary>Billing country code.</summary>
    public string CountryCode { get; init; } = string.Empty;

    /// <summary>Receipt email.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Name on the card.</summary>
    public string CardholderName { get; init; } = string.Empty;

    /// <summary>Street address.</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>City.</summary>
    public string City { get; init; } = string.Empty;

    /// <summary>Postal code.</summary>
    public string PostalCode { get; init; } = string.Empty;

    /// <summary>How the customer pays.</summary>
    public LiveCheckoutPaymentMethod PaymentMethod { get; init; }

    /// <summary>The wallet's id when <see cref="PaymentMethod"/> is <see cref="LiveCheckoutPaymentMethod.Wallet"/>.</summary>
    public string? WalletId { get; init; }

    /// <summary>Card digits without spaces (card payments only). In production use your provider's hosted fields instead.</summary>
    public string CardNumber { get; init; } = string.Empty;

    /// <summary>Card expiry as MM/YY (card payments only).</summary>
    public string Expiry { get; init; } = string.Empty;

    /// <summary>Card security code (card payments only).</summary>
    public string Cvc { get; init; } = string.Empty;

    /// <summary>The recognised card network.</summary>
    public LiveCheckoutCardBrand CardBrand { get; init; }

    /// <summary>The summary rows that were shown.</summary>
    public IReadOnlyList<LiveCheckoutLine> Lines { get; init; } = Array.Empty<LiveCheckoutLine>();

    /// <summary>Subtotal, discount, tax and total.</summary>
    public LiveCheckoutTotals Totals { get; init; }

    /// <summary>ISO 4217 currency code of every amount.</summary>
    public string Currency { get; init; } = "USD";
}

/// <summary>What a payment handler may return to fill the receipt; any value left null is filled by the checkout.</summary>
public sealed class LiveCheckoutReceipt
{
    /// <summary>Order number shown on the receipt; null shows six random digits.</summary>
    public string? OrderNumber { get; set; }

    /// <summary>When the order was paid; null uses the checkout's clock.</summary>
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>How it was paid ("Novacard ending 1111"); null builds it from the card or wallet.</summary>
    public string? PaymentText { get; set; }

    /// <summary>Where the receipt goes; null uses the email field.</summary>
    public string? Email { get; set; }
}
