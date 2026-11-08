using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="PricingCalculator"/>. Replace the whole object to localize the block; entries with
/// numbers are functions that receive the already formatted amounts and counts. Formats with a <c>{0}</c> keep the
/// amount rolling in place, so they are format strings instead of functions. Amounts and counts use <see cref="Culture"/>.
/// </summary>
public sealed class PricingCalculatorLabels
{
    /// <summary>Culture for amounts and counts (default en-US).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Label of the seat slider: "Team size".</summary>
    public string TeamSize { get; init; } = "Team size";

    /// <summary>The unit after a seat count: 1 ⇒ "seat", otherwise "seats".</summary>
    public Func<int, string> SeatUnit { get; init; } = seats => seats == 1 ? "seat" : "seats";

    /// <summary>A seat count with its unit (formatted count, seats) ⇒ "20 seats", "1 seat".</summary>
    public Func<string, int, string> SeatCount { get; init; } = (count, seats) => seats == 1 ? $"{count} seat" : $"{count} seats";

    /// <summary>Accessible name of the minus button: "Remove a seat".</summary>
    public string RemoveSeat { get; init; } = "Remove a seat";

    /// <summary>Accessible name of the plus button: "Add a seat".</summary>
    public string AddSeat { get; init; } = "Add a seat";

    /// <summary>Accessible name of the billing switch: "Billing period".</summary>
    public string BillingPeriod { get; init; } = "Billing period";

    /// <summary>The monthly option: "Monthly".</summary>
    public string Monthly { get; init; } = "Monthly";

    /// <summary>The yearly option: "Annual".</summary>
    public string Yearly { get; init; } = "Annual";

    /// <summary>The note after the yearly option (yearly discount as a whole percent) ⇒ "−20%"; empty hides it.</summary>
    public Func<int, string> YearlyNote { get; init; } = percent => $"−{percent}%";

    /// <summary>The badge of the recommended plan: "Best fit".</summary>
    public string Recommended { get; init; } = "Best fit";

    /// <summary>First line of the per-seat unit: "per seat".</summary>
    public string PerSeat { get; init; } = "per seat";

    /// <summary>Second line of the per-seat unit: "per month".</summary>
    public string PerMonth { get; init; } = "per month";

    /// <summary>After a card's total with yearly billing (seat count text) ⇒ "paid yearly for 20 seats".</summary>
    public Func<string, string> CardTotalYearly { get; init; } = seats => $"paid yearly for {seats}";

    /// <summary>After a card's total with monthly billing (seat count text) ⇒ "each month for 20 seats".</summary>
    public Func<string, string> CardTotalMonthly { get; init; } = seats => $"each month for {seats}";

    /// <summary>The button of a card that is not selected (plan name) ⇒ "Pick Studio".</summary>
    public Func<string, string> Pick { get; init; } = plan => $"Pick {plan}";

    /// <summary>The button of the selected card (after a check mark): "Current pick".</summary>
    public string Selected { get; init; } = "Current pick";

    /// <summary>First line of a capped plan above its cap (plan name, cap text) ⇒ "Solo covers up to 8 seats".</summary>
    public Func<string, string, string> LimitTitle { get; init; } = (plan, cap) => $"{plan} covers up to {cap}";

    /// <summary>Second line of a capped plan above its cap (seat count text) ⇒ "You chose 20 seats.".</summary>
    public Func<string, string> LimitDetail { get; init; } = seats => $"You chose {seats}.";

    /// <summary>The button of a capped plan above its cap (cap text) ⇒ "Switch to 8 seats".</summary>
    public Func<string, string> UseCap { get; init; } = cap => $"Switch to {cap}";

    /// <summary>A volume line without its own text (discount per seat) ⇒ "Volume rate, $3 off every seat".</summary>
    public Func<string, string> VolumeDiscount { get; init; } = amount => $"Volume rate, {amount} off every seat";

    /// <summary>Accessible name of a card's feature list (plan name) ⇒ "Studio features".</summary>
    public Func<string, string> PlanFeatures { get; init; } = plan => $"{plan} features";

    /// <summary>First line of the totals (plan name, seat count text) ⇒ "Studio, 20 seats".</summary>
    public Func<string, string, string> TotalsPlan { get; init; } = (plan, seats) => $"{plan}, {seats}";

    /// <summary>After the total with yearly billing: "per year".</summary>
    public string PerYear { get; init; } = "per year";

    /// <summary>After the total with monthly billing: "per month".</summary>
    public string PerMonthTotal { get; init; } = "per month";

    /// <summary>Rate line with yearly billing (seat price) ⇒ "$20 per seat each month, paid annually".</summary>
    public Func<string, string> RateYearly { get; init; } = price => $"{price} per seat each month, paid annually";

    /// <summary>Rate line with monthly billing (seat price) ⇒ "$25 per seat each month".</summary>
    public Func<string, string> RateMonthly { get; init; } = price => $"{price} per seat each month";

    /// <summary>The savings line with yearly billing; <c>{0}</c> is the rolling amount: "You keep {0} a year".</summary>
    public string SavingsFormat { get; init; } = "You keep {0} a year";

    /// <summary>The button offering yearly billing while billed monthly (savings) ⇒ "Keep $1,200 with annual billing".</summary>
    public Func<string, string> SwitchToYearly { get; init; } = amount => $"Keep {amount} with annual billing";

    /// <summary>The trial button (plan name) ⇒ "Try Studio free".</summary>
    public Func<string, string> StartTrial { get; init; } = plan => $"Try {plan} free";

    /// <summary>The trial button once the trial started (after a check mark): "Trial is live".</summary>
    public string TrialStarted { get; init; } = "Trial is live";

    /// <summary>Announced when a plan is picked (plan name) ⇒ "Studio picked".</summary>
    public Func<string, string> AnnouncePicked { get; init; } = plan => $"{plan} picked";

    /// <summary>Announced when billing turns monthly: "Billed monthly".</summary>
    public string AnnounceMonthly { get; init; } = "Billed monthly";

    /// <summary>Announced when billing turns yearly: "Billed annually, prices are per seat per month".</summary>
    public string AnnounceYearly { get; init; } = "Billed annually, prices are per seat per month";

    /// <summary>Announced once a trial started (plan name, seat count text) ⇒ "Studio trial is live for 20 seats.".</summary>
    public Func<string, string, string> AnnounceTrial { get; init; } = (plan, seats) => $"{plan} trial is live for {seats}.";

    /// <summary>Announced when the trial request failed: "The trial could not start. Try again.".</summary>
    public string AnnounceTrialFailed { get; init; } = "The trial could not start. Try again.";
}
