using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="SpendAlert"/>. Replace the whole object to localize the block; entries with numbers are
/// functions that receive already formatted values.
/// </summary>
public sealed class SpendAlertLabels
{
    /// <summary>Culture for amounts and percentages (en-US by default).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>After the spent amount: "spent".</summary>
    public string Spent { get; init; } = "spent";

    /// <summary>Under the spent amount: budget ⇒ "of a $600.00 budget".</summary>
    public Func<string, string> OfBudget { get; init; } = budget => $"of a {budget} budget";

    /// <summary>What is left: (amount, days) ⇒ "$187.20 left · 12 days to go".</summary>
    public Func<string, int, string> Left { get; init; } = (amount, days) =>
        days > 0 ? $"{amount} left · {days} {(days == 1 ? "day" : "days")} to go" : $"{amount} left";

    /// <summary>Over the budget: (amount) ⇒ "$24.50 over budget".</summary>
    public Func<string, string> OverBy { get; init; } = amount => $"{amount} over budget";

    /// <summary>The pace line: projected amount ⇒ "On pace for $598.40 by the end of the period".</summary>
    public Func<string, string> Pace { get; init; } = amount => $"On pace for {amount} by the end of the period";

    /// <summary>Status pill text by status.</summary>
    public Func<SpendAlertStatus, string> Status { get; init; } = status => status switch
    {
        SpendAlertStatus.AtRisk => "At risk",
        SpendAlertStatus.Warning => "Alert reached",
        SpendAlertStatus.Over => "Over budget",
        _ => "On track",
    };

    /// <summary>Heading of the editor: "Alerts".</summary>
    public string AlertsHeading { get; init; } = "Alerts";

    /// <summary>Under the heading: "Get a notification when spending reaches a share of the budget.".</summary>
    public string AlertsHint { get; init; } = "Get a notification when spending reaches a share of the budget.";

    /// <summary>Label of the budget field: "Budget".</summary>
    public string Budget { get; init; } = "Budget";

    /// <summary>Text before a threshold's field: "Alert at".</summary>
    public string AlertAt { get; init; } = "Alert at";

    /// <summary>After a threshold's field: "of budget".</summary>
    public string OfBudgetSuffix { get; init; } = "of budget";

    /// <summary>The add button: "Add alert".</summary>
    public string AddAlert { get; init; } = "Add alert";

    /// <summary>Screen reader name of a threshold's switch: percent ⇒ "Alert at 80%".</summary>
    public Func<string, string> ToggleName { get; init; } = percent => $"Alert at {percent}";

    /// <summary>Screen reader name of a threshold's field: "Alert share of budget, percent".</summary>
    public string ShareFieldName { get; init; } = "Alert share of budget, percent";

    /// <summary>Screen reader name of a threshold's remove button: percent ⇒ "Remove the 80% alert".</summary>
    public Func<string, string> RemoveName { get; init; } = percent => $"Remove the {percent} alert";

    /// <summary>The banner's title: (percent, budget name) ⇒ "You've used 82% of your Dining out budget".</summary>
    public Func<string, string, string> BannerTitle { get; init; } = (percent, name) => $"You've used {percent} of your {name} budget";

    /// <summary>The banner's line: (alert percent, left) ⇒ "Your 80% alert was reached. $108.00 is left.".</summary>
    public Func<string, string, string> BannerBody { get; init; } = (alert, left) => $"Your {alert} alert was reached. {left} is left.";

    /// <summary>The banner's line once over budget: over ⇒ "You are $24.50 over budget.".</summary>
    public Func<string, string> BannerOver { get; init; } = over => $"You are {over} over budget.";

    /// <summary>The banner's dismiss button: "Dismiss".</summary>
    public string Dismiss { get; init; } = "Dismiss";

    /// <summary>Screen reader name of the meter: (spent, budget, percent) ⇒ "$412.80 of $600.00 spent, 69%".</summary>
    public Func<string, string, string, string> MeterName { get; init; } = (spent, budget, percent) => $"{spent} of {budget} spent, {percent}";
}
