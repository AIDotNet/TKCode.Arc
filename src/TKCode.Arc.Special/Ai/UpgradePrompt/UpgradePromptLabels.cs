namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="UpgradePrompt"/> shows or announces, in English by default. Replace the instance to translate
/// or reword; sentences with values are functions so the word order can change. Counts and amounts arrive already
/// formatted ("2,000", "$192"); <c>unit</c> is the prompt's <see cref="UpgradePrompt.Unit"/> and <c>plan</c> the plan's name.
/// </summary>
public sealed class UpgradePromptLabels
{
    /// <summary>The default English labels.</summary>
    public static UpgradePromptLabels Default { get; } = new();

    /// <summary>The accessible name of the card; the argument is the plan.</summary>
    public Func<string, string> Region { get; init; } = static plan => $"Upgrade to {plan}";

    /// <summary>The title at or over the limit; the arguments are the limit and the unit.</summary>
    public Func<string, string, string> TitleAtLimit { get; init; } = static (limit, unit) => $"You've used all {limit} {unit}";

    /// <summary>The title below the limit; the arguments are what is left and the unit.</summary>
    public Func<string, string, string> TitleBelowLimit { get; init; } = static (left, unit) => $"Only {left} {unit} left";

    /// <summary>The line under the title in the offer; the arguments are the plan, its limit, the unit and the reset date.</summary>
    public Func<string, string, string, string, string> Description { get; init; } =
        static (plan, limit, unit, resetsOn) => $"Upgrade to {plan} for {limit} {unit} a month, or wait until they reset on {resetsOn}.";

    /// <summary>The title of the empty card (no plan to offer).</summary>
    public string EmptyTitle { get; init; } = "No plan to offer";

    /// <summary>The disabled Upgrade button of the empty card.</summary>
    public string EmptyUpgrade { get; init; } = "Upgrade";

    /// <summary>The title once the upgrade succeeded; the argument is the plan.</summary>
    public Func<string, string> SuccessTitle { get; init; } = static plan => $"You're on {plan}";

    /// <summary>The line under the success title.</summary>
    public string SuccessDescription { get; init; } = "Your new limits are active. Pick up right where you left off.";

    /// <summary>The title of the reminder after Maybe later.</summary>
    public string ReminderTitle { get; init; } = "No problem";

    /// <summary>The line under the reminder title; the arguments are the unit and the reset date.</summary>
    public Func<string, string, string> ReminderDescription { get; init; } =
        static (unit, resetsOn) => $"Your {unit} reset on {resetsOn}.";

    /// <summary>The label over the usage meter; the argument is the unit ("AI credits").</summary>
    public Func<string, string> MeterLabel { get; init; } = static unit => $"{Capitalize(unit)} this month";

    /// <summary>The counts beside the meter label; the arguments are the used amount and the limit.</summary>
    public Func<string, string, string> MeterCount { get; init; } = static (used, limit) => $"{used} / {limit}";

    /// <summary>The meter's accessible value; the arguments are the used amount, the limit and the unit.</summary>
    public Func<string, string, string, string> MeterValue { get; init; } =
        static (used, limit, unit) => $"{used} of {limit} {unit} used";

    /// <summary>The meter status at or over the limit.</summary>
    public string LimitReached { get; init; } = "Limit reached";

    /// <summary>The meter status below the limit; the arguments are what is left and the unit.</summary>
    public Func<string, string, string> Left { get; init; } = static (left, unit) => $"{left} {unit} left";

    /// <summary>The meter note at the limit; the argument is the reset date.</summary>
    public Func<string, string> ResetsOn { get; init; } = static date => $"Resets {date}";

    /// <summary>The meter note after the upgrade; the argument is the plan.</summary>
    public Func<string, string> PlanLimit { get; init; } = static plan => $"{plan} limit";

    /// <summary>The period after the rolling price.</summary>
    public string PerMonth { get; init; } = "/ month";

    /// <summary>The note under a yearly price; the argument is the yearly charge ("$192").</summary>
    public Func<string, string> BilledYearly { get; init; } = static amount => $"{amount} billed yearly";

    /// <summary>The note under a monthly price.</summary>
    public string BilledMonthly { get; init; } = "Billed monthly, cancel any time";

    /// <summary>The accessible price; the arguments are the per month price and the billing note.</summary>
    public Func<string, string, string> PriceText { get; init; } = static (price, note) => $"{price} a month, {note}";

    /// <summary>The Monthly option of the billing toggle.</summary>
    public string Monthly { get; init; } = "Monthly";

    /// <summary>The Yearly option of the billing toggle.</summary>
    public string Yearly { get; init; } = "Yearly";

    /// <summary>The accessible name of the billing toggle.</summary>
    public string BillingPeriod { get; init; } = "Billing period";

    /// <summary>The badge on Yearly; the argument is the saving in percent.</summary>
    public Func<int, string> SavePercent { get; init; } = static percent => $"Save {percent}%";

    /// <summary>The heading over the unlocks; the argument is the plan.</summary>
    public Func<string, string> Unlocks { get; init; } = static plan => $"{plan} unlocks";

    /// <summary>The heading over the new limits.</summary>
    public string NewLimits { get; init; } = "Your new limits";

    /// <summary>The note under the new limits; the argument is the amount charged ("$192").</summary>
    public Func<string, string> Charged { get; init; } = static amount => $"Charged {amount} today. A receipt is on its way.";

    /// <summary>The note under the new limits when the upgrade was simulated.</summary>
    public string SimulatedCharge { get; init; } = "Simulated upgrade: nothing was charged.";

    /// <summary>The reminder's main line; the argument is the unit.</summary>
    public Func<string, string> ReminderLine { get; init; } =
        static unit => $"Your work is saved, and your {unit} come back on their own.";

    /// <summary>The reminder's second line; the argument is the plan.</summary>
    public Func<string, string> ReminderHint { get; init; } = static plan => $"{plan} is one click away whenever you need more.";

    /// <summary>The Upgrade button; the argument is the plan.</summary>
    public Func<string, string> Upgrade { get; init; } = static plan => $"Upgrade to {plan}";

    /// <summary>The Upgrade button while the upgrade runs.</summary>
    public string Upgrading { get; init; } = "Upgrading…";

    /// <summary>The Upgrade button after an error.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The dismiss button.</summary>
    public string MaybeLater { get; init; } = "Maybe later";

    /// <summary>The button of the success state.</summary>
    public string Continue { get; init; } = "Continue";

    /// <summary>The button of the reminder; the argument is the plan.</summary>
    public Func<string, string> SeeAgain { get; init; } = static plan => $"See {plan} again";

    /// <summary>The message when the upgrade failed without a message of its own.</summary>
    public string GenericError { get; init; } = "The upgrade didn't go through. Try again.";

    /// <summary>Announced when the upgrade starts; the argument is the plan.</summary>
    public Func<string, string> AnnounceUpgrading { get; init; } = static plan => $"Upgrading to {plan}…";

    /// <summary>Announced when a simulated upgrade starts; the argument is the plan.</summary>
    public Func<string, string> AnnounceSimulating { get; init; } =
        static plan => $"Simulating the upgrade to {plan}. Nothing will be charged.";

    /// <summary>Announced when the upgrade succeeded; the arguments are the plan, its limit and the unit.</summary>
    public Func<string, string, string, string> AnnounceUpgraded { get; init; } =
        static (plan, limit, unit) => $"Upgraded to {plan}. You now have {limit} {unit} a month.";

    /// <summary>Announced when the upgrade failed; the argument is the message.</summary>
    public Func<string, string> AnnounceFailed { get; init; } = static message => $"Upgrade failed. {message}";

    /// <summary>Announced after Maybe later; the argument is the reset date.</summary>
    public Func<string, string> AnnounceReminder { get; init; } = static resetsOn => $"Upgrade dismissed. Your limit resets on {resetsOn}.";

    private static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
