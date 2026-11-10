using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How urgent a support ticket is.</summary>
public enum AccountHealthTicketPriority
{
    /// <summary>Can wait.</summary>
    Low,

    /// <summary>The usual.</summary>
    Normal,

    /// <summary>Needs attention soon.</summary>
    High,

    /// <summary>Blocking the customer now.</summary>
    Urgent,
}

/// <summary>Where a support ticket stands.</summary>
public enum AccountHealthTicketStatus
{
    /// <summary>Waiting on your team.</summary>
    Open,

    /// <summary>Waiting on the customer.</summary>
    Pending,

    /// <summary>Done.</summary>
    Solved,
}

/// <summary>How serious a risk is.</summary>
public enum AccountHealthRiskLevel
{
    /// <summary>Worth watching.</summary>
    Low,

    /// <summary>Should be handled this quarter.</summary>
    Medium,

    /// <summary>Puts the renewal in doubt.</summary>
    High,
}

/// <summary>A support ticket of the account.</summary>
public sealed class AccountHealthTicket
{
    /// <summary>Creates an empty ticket.</summary>
    public AccountHealthTicket()
    {
    }

    /// <summary>Creates a ticket from an id, a title, a priority, a status and its age.</summary>
    public AccountHealthTicket(string id, string title, AccountHealthTicketPriority priority, AccountHealthTicketStatus status, string? age = null)
    {
        Id = id;
        Title = title;
        Priority = priority;
        Status = status;
        Age = age;
    }

    /// <summary>The ticket's number as shown ("#4821").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>What the ticket is about.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>How urgent it is.</summary>
    public AccountHealthTicketPriority Priority { get; set; }

    /// <summary>Where it stands.</summary>
    public AccountHealthTicketStatus Status { get; set; }

    /// <summary>How long it has been open, as shown ("3d"); null leaves it out.</summary>
    public string? Age { get; set; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>Something that could cost the renewal, with how serious it is.</summary>
public sealed class AccountHealthRisk
{
    /// <summary>Creates an empty risk.</summary>
    public AccountHealthRisk()
    {
    }

    /// <summary>Creates a risk from a title, a detail and a level.</summary>
    public AccountHealthRisk(string title, string? detail, AccountHealthRiskLevel level)
    {
        Title = title;
        Detail = detail;
        Level = level;
    }

    /// <summary>The risk in a few words.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>A line of evidence; null leaves it out.</summary>
    public string? Detail { get; set; }

    /// <summary>How serious it is.</summary>
    public AccountHealthRiskLevel Level { get; set; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>A recommended action and how many health points it is expected to win back.</summary>
public sealed class AccountHealthAction
{
    /// <summary>Creates an empty action.</summary>
    public AccountHealthAction()
    {
    }

    /// <summary>Creates an action from a title, a detail and its expected impact in points.</summary>
    public AccountHealthAction(string title, string? detail, int impact, bool isDone = false)
    {
        Title = title;
        Detail = detail;
        Impact = impact;
        IsDone = isDone;
    }

    /// <summary>What to do.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Why, or how; null leaves it out.</summary>
    public string? Detail { get; set; }

    /// <summary>The health points it is expected to add once done.</summary>
    public int Impact { get; set; }

    /// <summary>Whether it is done; the block writes it when the action is ticked.</summary>
    public bool IsDone { get; set; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>A usage measure of the account, one value per week, oldest first.</summary>
public sealed class AccountHealthUsageMetric
{
    /// <summary>Creates an empty metric.</summary>
    public AccountHealthUsageMetric()
    {
    }

    /// <summary>Creates a metric from a name and its weekly values.</summary>
    public AccountHealthUsageMetric(string name, IEnumerable<double> values, string? unit = null)
    {
        Name = name;
        Values = values.ToList();
        Unit = unit;
    }

    /// <summary>The metric's name ("Active seats").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The unit after values ("seats"); null shows plain numbers.</summary>
    public string? Unit { get; set; }

    /// <summary>One value per week, oldest first; the last one is this week.</summary>
    public List<double> Values { get; set; } = new();

    /// <summary>What the contract allows (seats bought, a quota); null draws no limit.</summary>
    public double? Limit { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>Arguments of <see cref="AccountHealth.ActionToggled"/>.</summary>
public sealed class AccountHealthActionToggledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AccountHealthActionToggledEventArgs(RoutedEvent routedEvent, AccountHealthAction action, int projectedScore)
        : base(routedEvent)
    {
        Action = action;
        ProjectedScore = projectedScore;
    }

    /// <summary>The action that was ticked or cleared (its <see cref="AccountHealthAction.IsDone"/> is the new value).</summary>
    public AccountHealthAction Action { get; }

    /// <summary>The score expected once every done action has had its effect.</summary>
    public int ProjectedScore { get; }
}

/// <summary>The words <see cref="AccountHealth"/> shows and reads out; every one has an English default.</summary>
public sealed class AccountHealthLabels
{
    /// <summary>The health tile's heading.</summary>
    public string Health { get; init; } = "Health score";

    /// <summary>The bands' names: healthy (75 and up), watch (50 to 74), at risk (below 50).</summary>
    public IReadOnlyList<string> Bands { get; init; } = ["Healthy", "Watch", "At risk"];

    /// <summary>The change line; {0} is the signed change, {1} the number of weeks.</summary>
    public string ChangeFormat { get; init; } = "{0} in {1} weeks";

    /// <summary>The renewal tile's heading.</summary>
    public string Renewal { get; init; } = "Renewal";

    /// <summary>The days-left line; {0} is the number of days.</summary>
    public string DaysLeftFormat { get; init; } = "in {0} days";

    /// <summary>The line when the renewal date has passed; {0} is the number of days.</summary>
    public string OverdueFormat { get; init; } = "{0} days overdue";

    /// <summary>The renewal tile's line when no date is known.</summary>
    public string NoRenewal { get; init; } = "No date";

    /// <summary>The tickets tile's heading.</summary>
    public string Tickets { get; init; } = "Open tickets";

    /// <summary>The urgent count; {0} is the number.</summary>
    public string UrgentFormat { get; init; } = "{0} urgent";

    /// <summary>The usage section's heading.</summary>
    public string Usage { get; init; } = "Usage";

    /// <summary>The usage line against the limit; {0} is this week's value, {1} the limit.</summary>
    public string OfLimitFormat { get; init; } = "{0} of {1}";

    /// <summary>The prefix of the week labels on the usage chart ("W" gives W1, W2, …).</summary>
    public string WeekPrefix { get; init; } = "W";

    /// <summary>The ticket list's heading.</summary>
    public string TicketList { get; init; } = "Support tickets";

    /// <summary>The priorities' names, in the order of <see cref="AccountHealthTicketPriority"/>.</summary>
    public IReadOnlyList<string> Priorities { get; init; } = ["Low", "Normal", "High", "Urgent"];

    /// <summary>The statuses' names, in the order of <see cref="AccountHealthTicketStatus"/>.</summary>
    public IReadOnlyList<string> Statuses { get; init; } = ["Open", "Pending", "Solved"];

    /// <summary>The risk list's heading.</summary>
    public string Risks { get; init; } = "Risks";

    /// <summary>The risk levels' names, in the order of <see cref="AccountHealthRiskLevel"/>.</summary>
    public IReadOnlyList<string> RiskLevels { get; init; } = ["Low", "Medium", "High"];

    /// <summary>The actions list's heading.</summary>
    public string Actions { get; init; } = "Recommended actions";

    /// <summary>The impact of an action; {0} is the points.</summary>
    public string ImpactFormat { get; init; } = "+{0} pts";

    /// <summary>The projection line; {0} is the score now, {1} the projected score.</summary>
    public string ProjectionFormat { get; init; } = "With the done actions: {0} → {1}";

    /// <summary>The line shown in an empty list.</summary>
    public string Nothing { get; init; } = "Nothing here.";
}
