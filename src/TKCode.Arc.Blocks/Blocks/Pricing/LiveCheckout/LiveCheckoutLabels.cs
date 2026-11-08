using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every text a <see cref="LiveCheckout"/> shows or announces, with English defaults. Replace the object (or set single
/// properties before the block is shown) to localize it; sentences with numbers are format delegates.
/// </summary>
public sealed class LiveCheckoutLabels
{
    /// <summary>Accessible name of the express wallet row.</summary>
    public string ExpressCheckout { get; set; } = "Express checkout";

    /// <summary>The divider under the wallet buttons.</summary>
    public string OrPayWithCard { get; set; } = "Or pay with card";

    /// <summary>Heading of the plan section.</summary>
    public string PlanHeading { get; set; } = "Plan";

    /// <summary>Heading of the add-on section.</summary>
    public string AddonsHeading { get; set; } = "Add-ons";

    /// <summary>Heading of the billing section.</summary>
    public string BillingHeading { get; set; } = "Billing details";

    /// <summary>Heading of the card section.</summary>
    public string CardHeading { get; set; } = "Card";

    /// <summary>Accessible name of the billing period switch.</summary>
    public string BillingPeriod { get; set; } = "Billing period";

    /// <summary>The monthly segment.</summary>
    public string Monthly { get; set; } = "Monthly";

    /// <summary>The yearly segment.</summary>
    public string Yearly { get; set; } = "Yearly";

    /// <summary>The seat row's label.</summary>
    public string Seats { get; set; } = "Seats";

    /// <summary>Accessible name of the minus button.</summary>
    public string RemoveSeat { get; set; } = "Remove a seat";

    /// <summary>Accessible name of the plus button.</summary>
    public string AddSeat { get; set; } = "Add a seat";

    /// <summary>Unit after a plan's seat price.</summary>
    public string PerSeatMonth { get; set; } = "per seat / mo";

    /// <summary>Unit after an add-on's price.</summary>
    public string PerMonth { get; set; } = "/ mo";

    /// <summary>Email field label.</summary>
    public string Email { get; set; } = "Email";

    /// <summary>Cardholder field label.</summary>
    public string CardholderName { get; set; } = "Name on card";

    /// <summary>Country field label.</summary>
    public string Country { get; set; } = "Country";

    /// <summary>Address field label.</summary>
    public string Address { get; set; } = "Address";

    /// <summary>City field label.</summary>
    public string City { get; set; } = "City";

    /// <summary>Postal code field label.</summary>
    public string PostalCode { get; set; } = "Postal code";

    /// <summary>Card number field label.</summary>
    public string CardNumber { get; set; } = "Card number";

    /// <summary>Expiry field label.</summary>
    public string Expiry { get; set; } = "Expiry";

    /// <summary>Security code field label.</summary>
    public string Cvc { get; set; } = "CVC";

    /// <summary>Placeholder of the card number.</summary>
    public string CardNumberPlaceholder { get; set; } = "1234 1234 1234 1234";

    /// <summary>Placeholder of the expiry.</summary>
    public string ExpiryPlaceholder { get; set; } = "MM/YY";

    /// <summary>Placeholder of the security code.</summary>
    public string CvcPlaceholder { get; set; } = "123";

    /// <summary>The button that fills in the test card.</summary>
    public string UseTestCard { get; set; } = "Use test card";

    /// <summary>The pay button: <c>Pay</c> before the rolling total.</summary>
    public string Pay { get; set; } = "Pay";

    /// <summary>The pay button while the payment runs.</summary>
    public string Processing { get; set; } = "Charging card…";

    /// <summary>Heading of the summary.</summary>
    public string OrderSummary { get; set; } = "Order summary";

    /// <summary>The narrow summary bar while folded.</summary>
    public string ShowSummary { get; set; } = "Show order summary";

    /// <summary>The narrow summary bar while open.</summary>
    public string HideSummary { get; set; } = "Hide order summary";

    /// <summary>Title of the plan row from the plan name.</summary>
    public Func<string, string> PlanLine { get; set; } = name => $"{name} plan";

    /// <summary>Detail of the plan row from seats, formatted seat price and months: "8 seats × $22.40 × 12 months".</summary>
    public Func<int, string, int, string> PlanDetail { get; set; } = (seats, price, months) =>
        $"{seats} {(seats == 1 ? "seat" : "seats")} × {price} × {months} {(months == 1 ? "month" : "months")}";

    /// <summary>Detail of an add-on row billed yearly.</summary>
    public string BilledYearly { get; set; } = "Billed yearly";

    /// <summary>Detail of an add-on row billed monthly.</summary>
    public string BilledMonthly { get; set; } = "Billed monthly";

    /// <summary>The button that opens the coupon field.</summary>
    public string AddCoupon { get; set; } = "Add coupon";

    /// <summary>Placeholder of the coupon field.</summary>
    public string CouponPlaceholder { get; set; } = "Code";

    /// <summary>Accessible name of the coupon field.</summary>
    public string CouponField { get; set; } = "Coupon code";

    /// <summary>The coupon field's submit button.</summary>
    public string Apply { get; set; } = "Apply";

    /// <summary>Accessible name of the chip's remove button from the code.</summary>
    public Func<string, string> RemoveCoupon { get; set; } = code => $"Remove {code}";

    /// <summary>The chip's discount from the formatted percent ("15%").</summary>
    public Func<string, string> CouponOff { get; set; } = percent => $"{percent} off";

    /// <summary>Message when a coupon is refused and the handler gave none.</summary>
    public Func<string, string> CouponNotFound { get; set; } = code => $"No coupon called {code}.";

    /// <summary>Subtotal row.</summary>
    public string Subtotal { get; set; } = "Subtotal";

    /// <summary>Name of the discount row, followed by the code.</summary>
    public string Discount { get; set; } = "Discount";

    /// <summary>Tax row name from the tax name and the formatted rate: "VAT 21%".</summary>
    public Func<string, string, string> TaxLine { get; set; } = (name, rate) => $"{name} {rate}";

    /// <summary>Tax row name when a country has no tax name, or charges none.</summary>
    public string TaxFallback { get; set; } = "Tax";

    /// <summary>Tax value when nothing is charged.</summary>
    public string TaxNotCharged { get; set; } = "Not charged";

    /// <summary>Grand total before payment.</summary>
    public string DueToday { get; set; } = "Due today";

    /// <summary>Grand total on the receipt.</summary>
    public string PaidToday { get; set; } = "Paid today";

    /// <summary>Receipt heading.</summary>
    public string PaymentSuccessful { get; set; } = "You're all set";

    /// <summary>Receipt sentence from plan name and seats.</summary>
    public Func<string, int, string> ReceiptSentence { get; set; } = (plan, seats) =>
        $"{plan} is now live for {seats} {(seats == 1 ? "seat" : "seats")}.";

    /// <summary>Receipt row: order number.</summary>
    public string OrderNumber { get; set; } = "Order";

    /// <summary>Receipt row: payment method.</summary>
    public string PaidWith { get; set; } = "Paid with";

    /// <summary>Receipt row: date.</summary>
    public string Date { get; set; } = "Date";

    /// <summary>Receipt row: receipt email.</summary>
    public string ReceiptSentTo { get; set; } = "Receipt sent to";

    /// <summary>Payment method text from network name and last four digits.</summary>
    public Func<string, string, string> CardEnding { get; set; } = (brand, last4) => $"{brand} ending {last4}";

    /// <summary>Name of a card whose network is not recognised.</summary>
    public string GenericCard { get; set; } = "Card";

    /// <summary>Display name of a card network.</summary>
    public Func<LiveCheckoutCardBrand, string> BrandName { get; set; } = brand => brand switch
    {
        LiveCheckoutCardBrand.Novacard => "Novacard",
        LiveCheckoutCardBrand.Meridian => "Meridian",
        LiveCheckoutCardBrand.Atlas => "Atlas",
        _ => string.Empty
    };

    /// <summary>The receipt download button.</summary>
    public string ReceiptPdf { get; set; } = "Receipt PDF";

    /// <summary>The download button after it was pressed.</summary>
    public string ReceiptSaved { get; set; } = "Downloaded";

    /// <summary>The button that starts again.</summary>
    public string StartOver { get; set; } = "Start over";

    /// <summary>The line under the pay button; empty hides it.</summary>
    public string SecureNote { get; set; } = "Demo checkout. Nothing is charged.";

    /// <summary>Message when the handler failed without a message of its own.</summary>
    public string PaymentFailed { get; set; } = "The payment did not go through. Try again.";

    /// <summary>Announcement when a coupon is applied, from the code.</summary>
    public Func<string, string> CouponAppliedAnnouncement { get; set; } = code => $"Coupon {code} applied.";

    /// <summary>Announcement when a coupon is removed, from the code.</summary>
    public Func<string, string> CouponRemovedAnnouncement { get; set; } = code => $"Coupon {code} removed.";

    /// <summary>Error: email empty.</summary>
    public string EmailMissing { get; set; } = "Enter your email address";

    /// <summary>Error: email malformed.</summary>
    public string EmailInvalid { get; set; } = "Use an address like name@company.com";

    /// <summary>Error: cardholder name empty or one letter.</summary>
    public string NameMissing { get; set; } = "Enter the name printed on the card";

    /// <summary>Error: address shorter than three characters.</summary>
    public string AddressMissing { get; set; } = "Enter a street address";

    /// <summary>Error: city empty.</summary>
    public string CityMissing { get; set; } = "Enter a city";

    /// <summary>Error: postal code empty.</summary>
    public string PostalMissing { get; set; } = "Enter a postal code";

    /// <summary>Error: postal code does not match a country without its own hint.</summary>
    public string PostalMismatch { get; set; } = "Check the postal code";

    /// <summary>Error: card number empty.</summary>
    public string CardMissing { get; set; } = "Enter your card number";

    /// <summary>Error: card number too short.</summary>
    public string CardShort { get; set; } = "This card number is too short";

    /// <summary>Error: card number fails its checksum.</summary>
    public string CardInvalid { get; set; } = "Check the card number for typos";

    /// <summary>Error: expiry empty or incomplete.</summary>
    public string ExpiryIncomplete { get; set; } = "Use the MM/YY format";

    /// <summary>Error: month outside 01–12.</summary>
    public string ExpiryMonth { get; set; } = "Months run from 01 to 12";

    /// <summary>Error: expiry in the past.</summary>
    public string ExpiryPast { get; set; } = "This card has expired";

    /// <summary>Error: three-digit security code missing.</summary>
    public string CvcThree { get; set; } = "The 3 digits on the back";

    /// <summary>Error: four-digit security code missing.</summary>
    public string CvcFour { get; set; } = "The 4 digits on the front";

    /// <summary>The culture amounts, percents and dates are formatted with.</summary>
    public CultureInfo Culture { get; set; } = CultureInfo.GetCultureInfo("en-US");

    internal string Error(LiveCheckoutFieldError error, LiveCheckoutCountry? country) => error switch
    {
        LiveCheckoutFieldError.EmailMissing => EmailMissing,
        LiveCheckoutFieldError.EmailInvalid => EmailInvalid,
        LiveCheckoutFieldError.NameMissing => NameMissing,
        LiveCheckoutFieldError.AddressMissing => AddressMissing,
        LiveCheckoutFieldError.CityMissing => CityMissing,
        LiveCheckoutFieldError.PostalMissing => PostalMissing,
        LiveCheckoutFieldError.PostalMismatch => string.IsNullOrEmpty(country?.PostalHint) ? PostalMismatch : country!.PostalHint!,
        LiveCheckoutFieldError.CardMissing => CardMissing,
        LiveCheckoutFieldError.CardShort => CardShort,
        LiveCheckoutFieldError.CardInvalid => CardInvalid,
        LiveCheckoutFieldError.ExpiryMissing or LiveCheckoutFieldError.ExpiryIncomplete => ExpiryIncomplete,
        LiveCheckoutFieldError.ExpiryMonth => ExpiryMonth,
        LiveCheckoutFieldError.ExpiryPast => ExpiryPast,
        LiveCheckoutFieldError.CvcThree => CvcThree,
        LiveCheckoutFieldError.CvcFour => CvcFour,
        _ => string.Empty
    };
}
