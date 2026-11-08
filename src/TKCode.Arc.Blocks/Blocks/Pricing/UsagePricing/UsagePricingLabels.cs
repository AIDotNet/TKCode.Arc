using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="UsagePricing"/>. Replace the whole object to localize the block; parameterized entries
/// are functions that receive already formatted counts, usage and amounts. A sentence that carries an animated amount or
/// plan name ("{amount} saved per year", "{plan} seats") may put it anywhere: the block splits the sentence around it.
/// </summary>
public sealed class UsagePricingLabels
{
    /// <summary>Culture for amounts, counts and usage figures (default en-US).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The heading: "Plan your monthly cost".</summary>
    public string Title { get; init; } = "Plan your monthly cost";

    /// <summary>The paragraph under the heading.</summary>
    public string Description { get; init; } =
        "Seats are billed monthly. Every plan bundles some API calls; past that you pay a fixed price per extra million.";

    /// <summary>Label of the seat slider: "Team size".</summary>
    public string SeatsLabel { get; init; } = "Team size";

    /// <summary>Label of the usage slider: "API calls each month".</summary>
    public string UsageLabel { get; init; } = "API calls each month";

    /// <summary>The monthly segment: "Monthly".</summary>
    public string Monthly { get; init; } = "Monthly";

    /// <summary>The yearly segment: "Annual".</summary>
    public string Yearly { get; init; } = "Annual";

    /// <summary>Accessible name of the billing period switch: "Billing period".</summary>
    public string BillingPeriodName { get; init; } = "Billing period";

    /// <summary>Next to the billing switch: amount ⇒ "$960 saved per year".</summary>
    public Func<string, string> Saving { get; init; } = amount => $"{amount} saved per year";

    /// <summary>Heading of the breakdown: "Cost breakdown".</summary>
    public string BreakdownTitle { get; init; } = "Cost breakdown";

    /// <summary>The seats row: plan ⇒ "Studio seats".</summary>
    public Func<string, string> SeatsRow { get; init; } = plan => $"{plan} seats";

    /// <summary>Under the seats row: (seats, seat price) ⇒ "16 × $26".</summary>
    public Func<string, string, string> SeatsDetail { get; init; } = (seats, price) => $"{seats} × {price}";

    /// <summary>The usage row: "API calls".</summary>
    public string UsageRow { get; init; } = "API calls";

    /// <summary>Under the usage row while it stays inside the plan: (used, included) ⇒ "600k of 1M bundled".</summary>
    public Func<string, string, string> UsageBundled { get; init; } = (used, included) => $"{used} of {included} bundled";

    /// <summary>Under the usage row past the plan: (used, extra, rate) ⇒ "12M used, 4M extra at $5 per million".</summary>
    public Func<string, string, string, string> UsageOver { get; init; } =
        (used, extra, rate) => $"{used} used, {extra} extra at {rate} per million";

    /// <summary>The usage row's amount while nothing is charged for usage: "Bundled".</summary>
    public string BundledValue { get; init; } = "Bundled";

    /// <summary>The discount row shown under yearly billing: "Annual billing".</summary>
    public string YearlyRow { get; init; } = "Annual billing";

    /// <summary>Under the discount row: (seats, discount) ⇒ "16 × $5 off per seat".</summary>
    public Func<string, string, string> YearlyDetail { get; init; } = (seats, discount) => $"{seats} × {discount} off per seat";

    /// <summary>The total row: "Estimated total".</summary>
    public string TotalRow { get; init; } = "Estimated total";

    /// <summary>Under the total row: "Monthly, excluding tax".</summary>
    public string TotalDetail { get; init; } = "Monthly, excluding tax";

    /// <summary>After the card's price: "per month".</summary>
    public string PerMonth { get; init; } = "per month";

    /// <summary>Under the price under monthly billing: "Charged monthly, calls included".</summary>
    public string BilledMonthly { get; init; } = "Charged monthly, calls included";

    /// <summary>Under the price under yearly billing: seats per year ⇒ "$4,032 per year for seats, calls monthly".</summary>
    public Func<string, string> BilledYearly { get; init; } = amount => $"{amount} per year for seats, calls monthly";

    /// <summary>Reason for the first plan: (seat limit, usage limit) ⇒ "Fits up to 4 seats and 1.5M calls a month".</summary>
    public Func<string, string, string> ReasonWithin { get; init; } = (seats, usage) => $"Fits up to {seats} seats and {usage} calls a month";

    /// <summary>Reason when only the seats pass the previous plan: seat limit ⇒ "Suggested beyond 36 seats".</summary>
    public Func<string, string> ReasonSeats { get; init; } = seats => $"Suggested beyond {seats} seats";

    /// <summary>Reason when only the usage passes the previous plan: usage limit ⇒ "Suggested beyond 20M calls a month".</summary>
    public Func<string, string> ReasonUsage { get; init; } = usage => $"Suggested beyond {usage} calls a month";

    /// <summary>Reason when both pass the previous plan: (seat limit, usage limit) ⇒ "Suggested beyond 4 seats and 1.5M calls".</summary>
    public Func<string, string, string> ReasonBoth { get; init; } = (seats, usage) => $"Suggested beyond {seats} seats and {usage} calls";

    /// <summary>Accessible name of the feature list: plan ⇒ "Included in Studio".</summary>
    public Func<string, string> IncludedIn { get; init; } = plan => $"Included in {plan}";

    /// <summary>The call to action: plan ⇒ "Try Studio".</summary>
    public Func<string, string> StartWith { get; init; } = plan => $"Try {plan}";

    /// <summary>The button once the trial is on: "Trial is on".</summary>
    public string TrialStarted { get; init; } = "Trial is on";

    /// <summary>Under the button before the trial: "Free for 14 days, then billed as shown".</summary>
    public string TrialNote { get; init; } = "Free for 14 days, then billed as shown";

    /// <summary>Under the button once the trial is on, before the undo link: "Trial is on.".</summary>
    public string TrialStartedNote { get; init; } = "Trial is on.";

    /// <summary>The link that turns the trial off again: "Undo".</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>Spoken when the plan or the reason changes: (plan, reason) ⇒ "Studio. Suggested beyond 4 seats and 1.5M calls.".</summary>
    public Func<string, string, string> Announcement { get; init; } = (plan, reason) => $"{plan}. {reason}.";
}
