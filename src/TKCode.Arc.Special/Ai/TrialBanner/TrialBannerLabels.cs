using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="TrialBanner"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; lines that carry values are format functions. Dates are local calendar days.
/// </summary>
public sealed class TrialBannerLabels
{
    private static readonly CultureInfo s_english = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The English defaults.</summary>
    public static TrialBannerLabels Default { get; } = new();

    /// <summary>A number of days: "1 day", "9 days".</summary>
    public Func<int, string> Days { get; init; } = days => days == 1 ? "1 day" : days.ToString(CultureInfo.InvariantCulture) + " days";

    /// <summary>A date in the copy: "Nov 5".</summary>
    public Func<DateOnly, string> Date { get; init; } = date => date.ToString("MMM d", s_english);

    /// <summary>The end day in the warning copy, for an end two to six days away: "Thursday".</summary>
    public Func<DateOnly, string> Weekday { get; init; } = date => date.ToString("dddd", s_english);

    /// <summary>The end day in the warning copy when the trial ends today.</summary>
    public string EndsToday { get; init; } = "today";

    /// <summary>The end day in the warning copy when the trial ends tomorrow.</summary>
    public string EndsTomorrow { get; init; } = "tomorrow";

    /// <summary>The name of the banner and card region, from the plan: "Pro trial".</summary>
    public Func<string, string> Region { get; init; } = plan => plan + " trial";

    /// <summary>The title, card heading and accessible name when there is no trial to show (no days and no end date).</summary>
    public string EmptyTitle { get; init; } = "No trial in progress";

    /// <summary>The pill's label when there is no trial to show.</summary>
    public string EmptyPill { get; init; } = "No trial";

    /// <summary>The workspace in the copy when <see cref="TrialBanner.Workspace"/> is empty.</summary>
    public string DefaultWorkspace { get; init; } = "Your workspace";

    /// <summary>The banner's first line while days are left, from the days and the plan: "9 days left in your Pro trial".</summary>
    public Func<string, string, string> ActiveTitle { get; init; } = (days, plan) => $"{days} left in your {plan} trial";

    /// <summary>The banner's second line while days are left, from what is lost and the end date.</summary>
    public Func<string, string, string> ActiveDetail { get; init; } = (loss, date) => $"Keep {loss} after {date}.";

    /// <summary>The banner's first line in the last days, from the plan and the end day: "Your Pro trial ends Thursday".</summary>
    public Func<string, string, string> WarningTitle { get; init; } = (plan, day) => $"Your {plan} trial ends {day}";

    /// <summary>The banner's second line in the last days, from the days left (0 = less than a day) and what is lost.</summary>
    public Func<int, string, string> WarningDetail { get; init; } = (days, loss) =>
        (days <= 0 ? "Less than a day left" : days == 1 ? "1 day left" : $"{days} days left") + $" to keep {loss}.";

    /// <summary>The banner's first line once the trial has ended, from the plan.</summary>
    public Func<string, string> ExpiredTitle { get; init; } = plan => $"Your {plan} trial has ended";

    /// <summary>The banner's second line once the trial has ended, from the workspace (the English default leaves it to the card) and what was lost.</summary>
    public Func<string, string, string> ExpiredDetail { get; init; } =
        (workspace, loss) => $"Add a payment method to get {loss} back.";

    /// <summary>The banner's first line once a payment method is on file, from the plan.</summary>
    public Func<string, string> UpgradedTitle { get; init; } = plan => $"You're on {plan}";

    /// <summary>The banner's second line once a payment method is on file, from the workspace and what it keeps.</summary>
    public Func<string, string, string> UpgradedDetail { get; init; } =
        (workspace, loss) => $"Payment method on file. {workspace} keeps {loss}.";

    /// <summary>The card's subtitle while days are left, from the days and the end date: "9 days left · ends Nov 5".</summary>
    public Func<string, string, string> CardActive { get; init; } = (days, date) => $"{days} left · ends {date}";

    /// <summary>The card's subtitle in the last days, from the end day and date: "Ends Thursday, Nov 5".</summary>
    public Func<string, string, string> CardWarning { get; init; } =
        (day, date) => $"Ends {day}, {date}";

    /// <summary>The card's subtitle once the trial has ended, from the end date.</summary>
    public Func<string, string> CardExpired { get; init; } = date => $"Ended {date}";

    /// <summary>The card's subtitle once a payment method is on file, from the plan.</summary>
    public Func<string, string> CardUpgraded { get; init; } = plan => $"Upgraded to {plan}";

    /// <summary>The line above the feature list while the trial runs.</summary>
    public string KeepLead { get; init; } = "Add a payment method to keep:";

    /// <summary>The line above the feature list once the trial has ended, from the workspace.</summary>
    public Func<string, string> LostLead { get; init; } = workspace => $"{workspace} lost access to:";

    /// <summary>The line above the feature list once a payment method is on file, from the workspace.</summary>
    public Func<string, string> KeptLead { get; init; } = workspace => $"{workspace} keeps:";

    /// <summary>The pill's label while days are left, from the days: "9 days left".</summary>
    public Func<string, string> PillActive { get; init; } = days => days + " left";

    /// <summary>The pill's label in the last days, from the end day: "Ends Thursday".</summary>
    public Func<string, string> PillWarning { get; init; } = day => "Ends " + day;

    /// <summary>The pill's label once the trial has ended.</summary>
    public string PillExpired { get; init; } = "Trial ended";

    /// <summary>The pill's label once a payment method is on file, from the plan.</summary>
    public Func<string, string> PillUpgraded { get; init; } = plan => plan;

    /// <summary>What the pill does, after its label: it opens the card (pill variant) or the banner again (folded).</summary>
    public string OpenDetails { get; init; } = "Show trial details";

    /// <summary>The primary action.</summary>
    public string AddPayment { get; init; } = "Add payment method";

    /// <summary>The primary action after checkout failed.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The secondary action.</summary>
    public string ComparePlans { get; init; } = "Compare plans";

    /// <summary>The action once a payment method is on file.</summary>
    public string ManageBilling { get; init; } = "Manage billing";

    /// <summary>The snooze button's name.</summary>
    public string Snooze { get; init; } = "Snooze";

    /// <summary>The dismiss button's name.</summary>
    public string Dismiss { get; init; } = "Dismiss";

    /// <summary>The snooze menu's rows.</summary>
    public string SnoozeTomorrow { get; init; } = "Until tomorrow";

    /// <summary>The snooze menu's row for three days.</summary>
    public string SnoozeThreeDays { get; init; } = "For 3 days";

    /// <summary>The snooze menu's row that waits for the warning, from the first day of the warning: "Until Nov 2".</summary>
    public Func<string, string> SnoozeUntilWarning { get; init; } = date => $"Until {date}, when the warning starts";

    /// <summary>The line while checkout opens.</summary>
    public string Opening { get; init; } = "Opening checkout…";

    /// <summary>The line after checkout failed.</summary>
    public string Failed { get; init; } = "Checkout didn't open. Nothing was charged.";

    /// <summary>The short note when Compare plans has no handler.</summary>
    public string PlansNote { get; init; } = "Your plans page would open here (simulated).";

    /// <summary>The short note when Manage billing has no handler.</summary>
    public string BillingNote { get; init; } = "Your billing portal would open here (simulated).";

    /// <summary>Announced when a payment method is on file, from the plan and whether the call was simulated.</summary>
    public Func<string, bool, string> AnnounceUpgraded { get; init; } =
        (plan, simulated) => $"Payment method added. You're on {plan}." + (simulated ? " (Simulated.)" : string.Empty);

    /// <summary>Announced when checkout failed.</summary>
    public string AnnounceFailed { get; init; } = "Checkout didn't open. Try again.";

    /// <summary>Announced after a snooze, from the date it ends.</summary>
    public Func<string, string> AnnounceSnoozed { get; init; } = date => $"Trial banner snoozed until {date}.";

    /// <summary>Announced after a dismiss.</summary>
    public string AnnounceDismissed { get; init; } = "Trial banner dismissed.";
}
