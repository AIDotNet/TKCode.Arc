namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AgentApprovals"/> queue shows or reads out, with English defaults. Words built from values
/// are functions, so an app can apply its own grammar. Assign a new instance to <see cref="AgentApprovals.Labels"/> to
/// change them.
/// </summary>
public sealed class AgentApprovalsLabels
{
    /// <summary>The queue's title ("Waiting for approval"), also its accessible name.</summary>
    public string Title { get; init; } = "Waiting for approval";

    /// <summary>The count next to the title ("3 waiting").</summary>
    public Func<int, string> Count { get; init; } = static count => $"{count} waiting";

    /// <summary>The risk words ("Low risk", "Medium risk", "High risk").</summary>
    public Func<AgentApprovalRisk, string> Risk { get; init; } = static risk => risk switch
    {
        AgentApprovalRisk.High => "High risk",
        AgentApprovalRisk.Medium => "Medium risk",
        _ => "Low risk"
    };

    /// <summary>The headings of the detail: "Why", "Details".</summary>
    public string Why { get; init; } = "Why";

    /// <inheritdoc cref="Why"/>
    public string Details { get; init; } = "Details";

    /// <summary>The line under the agent's name, from the agent and the relative time ("Ledger asked 5m ago").</summary>
    public Func<string?, string?, string> Asked { get; init; } = static (agent, time) =>
        time is null ? $"{agent ?? "An agent"} asks" : $"{agent ?? "An agent"} asked {time}";

    /// <summary>A relative time from the time and now ("5m ago").</summary>
    public Func<DateTimeOffset, DateTimeOffset, string> Time { get; init; } = AiAgentFormat.Relative;

    /// <summary>The reason field's placeholder ("Note for the agent (optional)").</summary>
    public string ReasonPlaceholder { get; init; } = "Note for the agent (optional)";

    /// <summary>The check that a high-risk approval asks for ("I checked the details above").</summary>
    public string Confirm { get; init; } = "I checked the details above";

    /// <summary>The buttons: "Approve", "Deny".</summary>
    public string Approve { get; init; } = "Approve";

    /// <inheritdoc cref="Approve"/>
    public string Deny { get; init; } = "Deny";

    /// <summary>The empty queue ("Nothing is waiting. Agents will ask here before they act.").</summary>
    public string Empty { get; init; } = "Nothing is waiting. Agents will ask here before they act.";

    /// <summary>The line after a decision, from the decision and the title ("Approved: Send the quote").</summary>
    public Func<AgentApprovalDecision, string, string> Done { get; init; } = static (decision, title) =>
        $"{(decision == AgentApprovalDecision.Approved ? "Approved" : "Denied")}: {title}";

    /// <summary>The line when the decision could not be sent, from the message ("Could not send: message").</summary>
    public Func<string, string> Failed { get; init; } = static message => $"Could not send: {message}";

    /// <summary>The accessible name of a request row, from title, agent and risk.</summary>
    public Func<string, string?, string, string> RowName { get; init; } =
        static (title, agent, risk) => agent is null ? $"{title}, {risk}" : $"{title}, {agent}, {risk}";
}
