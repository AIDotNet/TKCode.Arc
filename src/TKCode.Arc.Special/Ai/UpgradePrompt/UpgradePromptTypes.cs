using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The billing interval of an <see cref="UpgradePrompt"/>.</summary>
public enum UpgradePromptInterval
{
    /// <summary>Billed every month at the monthly price.</summary>
    Monthly,

    /// <summary>Billed once a year at the yearly per month price times twelve.</summary>
    Yearly
}

/// <summary>What an <see cref="UpgradePrompt"/> shows. All states share one cell, so the card keeps its size.</summary>
public enum UpgradePromptState
{
    /// <summary>The offer: the limit, the price with the billing toggle, the unlocks, Maybe later and Upgrade.</summary>
    Offer,

    /// <summary>The upgrade is running: Upgrade shows a spinner and stays focusable; the toggle and Maybe later are off.</summary>
    Processing,

    /// <summary>The upgrade failed: the message shows under the price and Upgrade reads Try again.</summary>
    Error,

    /// <summary>The plan is active: the meter settles to the new limit and the new limits are listed with Continue.</summary>
    Success,

    /// <summary>After Maybe later: a calm reminder of when the limit resets, with See Pro again.</summary>
    Reminder
}

/// <summary>One unlock an <see cref="UpgradePromptPlan"/> lists in the offer, such as more credits or a better model.</summary>
public sealed class UpgradePromptFeature
{
    /// <summary>Creates an empty feature; set <see cref="Label"/> before showing it.</summary>
    public UpgradePromptFeature()
    {
    }

    /// <summary>Creates a feature with an id, a label and, optionally, a mark and its color.</summary>
    public UpgradePromptFeature(string id, string label, Geometry? mark = null, IBrush? markBrush = null)
    {
        Id = id;
        Label = label;
        Mark = mark;
        MarkBrush = markBrush;
    }

    /// <summary>A stable id for the feature.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The unlock in a few words ("2,000 AI credits a month"). Kept to one line.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>A 24 × 24 stroked glyph before the label (a Lucide icon or a model's mark); null draws a check.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>The mark's color; null uses the accent.</summary>
    public IBrush? MarkBrush { get; set; }

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>One of the new limits an <see cref="UpgradePrompt"/> lists once the upgrade succeeded.</summary>
public sealed class UpgradePromptLimit
{
    /// <summary>Creates an empty limit.</summary>
    public UpgradePromptLimit()
    {
    }

    /// <summary>Creates a limit with a label, a value and, optionally, a mark shown before the value.</summary>
    public UpgradePromptLimit(string label, string value, Geometry? mark = null, IBrush? markBrush = null)
    {
        Label = label;
        Value = value;
        Mark = mark;
        MarkBrush = markBrush;
    }

    /// <summary>What the limit is about ("AI credits").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The new value ("2,000 a month").</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>An optional 24 × 24 stroked glyph before the value, such as a model's mark.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>The mark's color; null uses the text color.</summary>
    public IBrush? MarkBrush { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{Label}: {Value}";
}

/// <summary>
/// The plan an <see cref="UpgradePrompt"/> offers: its name, the monthly price and the per month price when billed yearly
/// (the savings badge and the yearly charge derive from them), the new limit, the unlocks and the new limits.
/// </summary>
public sealed class UpgradePromptPlan
{
    /// <summary>The plan's name ("Pro").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The price per month when billed monthly.</summary>
    public decimal MonthlyPrice { get; set; }

    /// <summary>The price per month when billed yearly; the yearly charge is twelve times this.</summary>
    public decimal YearlyPrice { get; set; }

    /// <summary>The ISO 4217 currency code ("USD" writes "$", "EUR" "€", "GBP" "£", "JPY" "¥"; others are written before the amount).</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>The plan's limit, in the same unit as the prompt's <see cref="UpgradePrompt.Limit"/>.</summary>
    public double Limit { get; set; }

    /// <summary>The unlocks listed in the offer, ideally three.</summary>
    public IList<UpgradePromptFeature> Features { get; set; } = new List<UpgradePromptFeature>();

    /// <summary>The new limits listed after the upgrade.</summary>
    public IList<UpgradePromptLimit> Limits { get; set; } = new List<UpgradePromptLimit>();

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>What the person chose: the plan, the interval, the amount charged today and its currency.</summary>
public sealed class UpgradePromptSelection
{
    /// <summary>Creates a selection.</summary>
    public UpgradePromptSelection(UpgradePromptPlan plan, UpgradePromptInterval interval, decimal amount, string currency)
    {
        Plan = plan;
        Interval = interval;
        Amount = amount;
        Currency = currency;
    }

    /// <summary>The plan.</summary>
    public UpgradePromptPlan Plan { get; }

    /// <summary>The billing interval.</summary>
    public UpgradePromptInterval Interval { get; }

    /// <summary>
    /// The amount charged today: the monthly price, or twelve times the yearly per month price. Display only; charge the plan
    /// and interval on the server.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>The ISO 4217 currency code of <see cref="Amount"/>.</summary>
    public string Currency { get; }
}

/// <summary>Arguments of <see cref="UpgradePrompt.IntervalChanged"/>.</summary>
public sealed class UpgradePromptIntervalChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public UpgradePromptIntervalChangedEventArgs(RoutedEvent routedEvent, UpgradePromptInterval interval) : base(routedEvent)
    {
        Interval = interval;
    }

    /// <summary>The interval now chosen.</summary>
    public UpgradePromptInterval Interval { get; }
}

/// <summary>Arguments of <see cref="UpgradePrompt.Upgraded"/>.</summary>
public sealed class UpgradePromptUpgradedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public UpgradePromptUpgradedEventArgs(RoutedEvent routedEvent, UpgradePromptSelection selection) : base(routedEvent)
    {
        Selection = selection;
    }

    /// <summary>What was upgraded to.</summary>
    public UpgradePromptSelection Selection { get; }
}

/// <summary>Arguments of <see cref="UpgradePrompt.UpgradeFailed"/>.</summary>
public sealed class UpgradePromptFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public UpgradePromptFailedEventArgs(RoutedEvent routedEvent, UpgradePromptSelection selection, Exception error) : base(routedEvent)
    {
        Selection = selection;
        Error = error;
    }

    /// <summary>What the person tried to upgrade to.</summary>
    public UpgradePromptSelection Selection { get; }

    /// <summary>The exception the upgrade handler threw; its message shows under the price.</summary>
    public Exception Error { get; }
}
