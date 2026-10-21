using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="UsageBilling"/>. Replace the whole object to localize the block; parameterized
/// entries are functions that receive the already formatted amounts and counts. Dates and amounts use
/// <see cref="Culture"/>.
/// </summary>
public sealed class UsageBillingLabels
{
    /// <summary>Culture for dates, amounts and counts (default en-US).</summary>
    public CultureInfo Culture { get; init; } = SettingsFormat.DefaultCulture;

    /// <summary>Header of the summary card: "This billing period".</summary>
    public string PeriodTitle { get; init; } = "This billing period";

    /// <summary>Days left after today: "11 days left", "1 day left", "Last day".</summary>
    public Func<int, string> DaysLeft { get; init; } = days => days switch
    {
        <= 0 => "Last day",
        1 => "1 day left",
        _ => $"{days} days left"
    };

    /// <summary>Shown instead of the days left once the period is over: "Period ended".</summary>
    public string PeriodEnded { get; init; } = "Period ended";

    /// <summary>Label above the amount spent: "Spent so far".</summary>
    public string SpentSoFar { get; init; } = "Spent so far";

    /// <summary>The projected total for the period: amount ⇒ "Projected $110.09".</summary>
    public Func<string, string> Projected { get; init; } = amount => $"Projected {amount}";

    /// <summary>Label of the limit progress and header of the limit setting: "Spending limit".</summary>
    public string SpendingLimit { get; init; } = "Spending limit";

    /// <summary>Under the limit progress: (current, limit) ⇒ "$102.96 of $250.00 limit".</summary>
    public Func<string, string, string> LimitProgress { get; init; } = (current, limit) => $"{current} of {limit} limit";

    /// <summary>Shown in place of the limit progress when no limit applies: "No spending limit".</summary>
    public string NoLimit { get; init; } = "No spending limit";

    /// <summary>The button that turns the limit on from the summary: "Set a limit".</summary>
    public string SetLimit { get; init; } = "Set a limit";

    /// <summary>Badge once the spend passes the alert threshold: "Near limit".</summary>
    public string NearLimit { get; init; } = "Near limit";

    /// <summary>Badge once the spend reaches the limit: "Limit reached".</summary>
    public string LimitReached { get; init; } = "Limit reached";

    /// <summary>The daily spend chart's accessible name: "Daily spend".</summary>
    public string DailySpend { get; init; } = "Daily spend";

    /// <summary>The chart headline at rest, over the average: "Average daily spend".</summary>
    public string DailyAverage { get; init; } = "Average daily spend";

    /// <summary>The chart headline while a day is read: "Spent".</summary>
    public string DaySpend { get; init; } = "Spent";

    /// <summary>The category column of the chart's data table: "Date".</summary>
    public string Day { get; init; } = "Date";

    /// <summary>Header of the meters card: "Usage".</summary>
    public string Usage { get; init; } = "Usage";

    /// <summary>A meter's usage: (used, included, unit) ⇒ "1.2M of 1M calls included".</summary>
    public Func<string, string, string, string> MeterUsage { get; init; } =
        (used, included, unit) => $"{used} of {included} {unit} included".Replace("  ", " ");

    /// <summary>A meter without an included amount: (used, unit) ⇒ "3.4K min".</summary>
    public Func<string, string, string> MeterUsageOnly { get; init; } = (used, unit) => $"{used} {unit}".TrimEnd();

    /// <summary>The overage in a meter bar's accessible name: amount ⇒ "240K over".</summary>
    public Func<string, string> Overage { get; init; } = amount => $"{amount} over";

    /// <summary>A meter's charge while usage stays within the included amount: "Included".</summary>
    public string Included { get; init; } = "Included";

    /// <summary>A meter's rate: (price, per) ⇒ "$0.40 / 10K calls".</summary>
    public Func<string, string, string> Rate { get; init; } = (price, per) => $"{price} / {per}";

    /// <summary>The footer of the meters card, before the metered total: "Usage charges so far".</summary>
    public string UsageTotal { get; init; } = "Usage charges so far";

    /// <summary>Description of the limit setting: "Cap the total bill for one billing period."</summary>
    public string SpendingLimitDescription { get; init; } = "Cap the total bill for one billing period.";

    /// <summary>Label of the limit amount field: "Limit amount".</summary>
    public string LimitAmount { get; init; } = "Limit amount";

    /// <summary>What happens near and at the limit: alert percentage ⇒ "We email the owners at 80% and pause usage-based features at 100%."</summary>
    public Func<int, string> LimitNote { get; init; } =
        percent => $"We email the owners at {percent}% and pause usage-based features at 100%.";
}
