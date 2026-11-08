namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="CreditTopUp"/>. Replace the whole object to reword or localize the card. Entries with
/// <c>{0}</c> / <c>{1}</c> / <c>{2}</c> receive already formatted counts and amounts (the culture is
/// <see cref="CreditTopUp.CultureName"/>); where a number rolls, the text around <c>{0}</c> stays still.
/// </summary>
public sealed class CreditTopUpLabels
{
    /// <summary>The card's accessible name: "Buy credits".</summary>
    public string CardName { get; init; } = "Buy credits";

    /// <summary>Above the balance: "Balance".</summary>
    public string BalanceTitle { get; init; } = "Balance";

    /// <summary>After the balance and in amounts: "credits".</summary>
    public string Unit { get; init; } = "credits";

    /// <summary>Health badge below the threshold: "Low balance".</summary>
    public string HealthLow { get; init; } = "Low balance";

    /// <summary>Health badge at or above the threshold: "On track".</summary>
    public string HealthOk { get; init; } = "On track";

    /// <summary>Days left and the daily average: "≈ {0} days at {1}/day".</summary>
    public string DaysLeftFormat { get; init; } = "≈ {0} days at {1}/day";

    /// <summary>Days left when the balance lasts less than a day: "Runs out today at {1}/day".</summary>
    public string DaysLeftNoneFormat { get; init; } = "Runs out today at {1}/day";

    /// <summary>Under the chart at rest, with the number of days: "Past {0} days".</summary>
    public string ChartIdleFormat { get; init; } = "Past {0} days";

    /// <summary>Under the chart while a day is pointed at, with its date and credits: "{0}: {1}".</summary>
    public string ChartReadoutFormat { get; init; } = "{0}: {1}";

    /// <summary>The date of the last day in the readout: "Today".</summary>
    public string Today { get; init; } = "Today";

    /// <summary>The date pattern of the readout (.NET custom format): "MMM d".</summary>
    public string ChartDateFormat { get; init; } = "MMM d";

    /// <summary>The chart's accessible name, with the days and the average: "Daily usage over the past {0} days, averaging {1} credits a day.".</summary>
    public string ChartNameFormat { get; init; } = "Daily usage over the past {0} days, averaging {1} credits a day.";

    /// <summary>Title of the pack section: "Top up".</summary>
    public string AddTitle { get; init; } = "Top up";

    /// <summary>The rate beside the title, with the price and the unit count: "{0} per {1}".</summary>
    public string RateFormat { get; init; } = "{0} per {1}";

    /// <summary>A pack's accessible name, with the credits and the price: "{0} credits, {1}".</summary>
    public string PackNameFormat { get; init; } = "{0} credits, {1}";

    /// <summary>A pack's accessible name with a bonus, with the credits, the price and the percent: "{0} credits, {1}, {2}% bonus".</summary>
    public string PackBonusNameFormat { get; init; } = "{0} credits, {1}, {2}% bonus";

    /// <summary>A pack's bonus badge, with the percent: "+{0}%".</summary>
    public string BonusBadgeFormat { get; init; } = "+{0}%";

    /// <summary>Title of the slider: "Pick an amount".</summary>
    public string CustomTitle { get; init; } = "Pick an amount";

    /// <summary>The slider's value, beside its title, in its bubble and read by screen readers: "{0} credits".</summary>
    public string CustomValueFormat { get; init; } = "{0} credits";

    /// <summary>Title of the auto top-up setting: "Auto refill".</summary>
    public string AutoTitle { get; init; } = "Auto refill";

    /// <summary>Under the auto top-up title: "Uses the amount above".</summary>
    public string AutoSubtitle { get; init; } = "Uses the amount above";

    /// <summary>Label of the threshold choices: "Refill below".</summary>
    public string ThresholdTitle { get; init; } = "Refill below";

    /// <summary>The card row's first line, with the brand and the last four digits: "{0} •••• {1}".</summary>
    public string CardTitleFormat { get; init; } = "{0} •••• {1}";

    /// <summary>The card row's second line, with the expiry (MM/YYYY): "Valid thru {0}".</summary>
    public string CardExpiresFormat { get; init; } = "Valid thru {0}";

    /// <summary>The card row when no card is saved: "No saved card".</summary>
    public string NoCard { get; init; } = "No saved card";

    /// <summary>The button that cycles the saved cards: "Change".</summary>
    public string Change { get; init; } = "Change";

    /// <summary>Its accessible name, with the number of saved cards: "Change card, {0} saved".</summary>
    public string ChangeNameFormat { get; init; } = "Change card, {0} saved";

    /// <summary>The total in the checkout, with the credits including bonus: "{0} credits".</summary>
    public string TotalFormat { get; init; } = "{0} credits";

    /// <summary>Under the total when a bonus applies: "{0} bonus included".</summary>
    public string BonusIncludedFormat { get; init; } = "{0} bonus included";

    /// <summary>Under the total below every bonus tier, with the first tier's amount: "Bonus from {0}".</summary>
    public string BonusStartsFormat { get; init; } = "Bonus from {0}";

    /// <summary>The buy button: "Add credits".</summary>
    public string Buy { get; init; } = "Add credits";

    /// <summary>The buy button's accessible name while charging: "Processing".</summary>
    public string Processing { get; init; } = "Processing";

    /// <summary>The buy button after a purchase: "Added".</summary>
    public string Done { get; init; } = "Added";

    /// <summary>The buy button after a failure: "Retry".</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The status line at rest without a purchase handler (simulated): "Test mode. Credits don't expire.".</summary>
    public string FooterSimulated { get; init; } = "Test mode. Credits don't expire.";

    /// <summary>The status line at rest with a purchase handler: "Credits don't expire.".</summary>
    public string FooterLive { get; init; } = "Credits don't expire.";

    /// <summary>The status line while charging, with the brand and the last four digits: "Charging {0} ••{1}".</summary>
    public string ChargingFormat { get; init; } = "Charging {0} ••{1}";

    /// <summary>The status line after a simulated purchase, with the credits added: "{0} credits added. Test mode, no charge.".</summary>
    public string DoneSimulatedFormat { get; init; } = "{0} credits added. Test mode, no charge.";

    /// <summary>The status line after a purchase, with the credits added: "{0} credits added.".</summary>
    public string DoneLiveFormat { get; init; } = "{0} credits added.";

    /// <summary>The status line when a failure has no message: "Payment didn't go through. Try again.".</summary>
    public string ErrorFallback { get; init; } = "Payment didn't go through. Try again.";

    /// <summary>The notice when auto top-up is on or its threshold changes, with the threshold: "Auto refill below {0} credits".</summary>
    public string AutoOnFormat { get; init; } = "Auto refill below {0} credits";

    /// <summary>The notice when auto top-up is turned off: "Auto refill paused".</summary>
    public string AutoOff { get; init; } = "Auto refill paused";

    /// <summary>The default labels.</summary>
    public static CreditTopUpLabels Default { get; } = new();
}
