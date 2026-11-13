using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What kind of action an agent asks to take; it picks the request's glyph.</summary>
public enum AgentApprovalKind
{
    /// <summary>Send a message outside the team.</summary>
    Email,

    /// <summary>Move money.</summary>
    Payment,

    /// <summary>Ship code or change a live system.</summary>
    Deploy,

    /// <summary>Change or delete stored data.</summary>
    DataChange,

    /// <summary>Grant or use an access right.</summary>
    Access,

    /// <summary>Anything else.</summary>
    Other
}

/// <summary>How much harm the action could do if it is wrong.</summary>
public enum AgentApprovalRisk
{
    /// <summary>Easy to undo.</summary>
    Low,

    /// <summary>Visible to others or costly to undo.</summary>
    Medium,

    /// <summary>Hard or impossible to undo; approving asks for an extra check.</summary>
    High
}

/// <summary>A person's answer to a request.</summary>
public enum AgentApprovalDecision
{
    /// <summary>The agent may go ahead.</summary>
    Approved,

    /// <summary>The agent must not.</summary>
    Denied
}

/// <summary>One fact of a request ("To" → "billing@client.example").</summary>
/// <param name="Label">What the fact is.</param>
/// <param name="Value">Its value, shown as written.</param>
public sealed record AgentApprovalDetail(string Label, string Value);

/// <summary>An action an agent wants to take and needs a person to approve.</summary>
public class AgentApprovalRequest : AvaloniaObject
{
    /// <summary>Your id of the request, handed back with the decision.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<AgentApprovalRequest, string?>(nameof(Id));

    /// <summary>The agent asking.</summary>
    public static readonly StyledProperty<string?> AgentProperty =
        AvaloniaProperty.Register<AgentApprovalRequest, string?>(nameof(Agent));

    /// <summary>What kind of action it is.</summary>
    public static readonly StyledProperty<AgentApprovalKind> KindProperty =
        AvaloniaProperty.Register<AgentApprovalRequest, AgentApprovalKind>(nameof(Kind));

    /// <summary>The action in a line ("Send the renewal quote to the client").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AgentApprovalRequest, string>(nameof(Title), string.Empty);

    /// <summary>Why the agent wants to do it, in its own words.</summary>
    public static readonly StyledProperty<string?> ContextProperty =
        AvaloniaProperty.Register<AgentApprovalRequest, string?>(nameof(Context));

    /// <summary>The facts of the action (recipient, amount, environment, ...).</summary>
    public static readonly StyledProperty<IReadOnlyList<AgentApprovalDetail>?> DetailsProperty =
        AvaloniaProperty.Register<AgentApprovalRequest, IReadOnlyList<AgentApprovalDetail>?>(nameof(Details));

    /// <summary>How risky the action is.</summary>
    public static readonly StyledProperty<AgentApprovalRisk> RiskProperty =
        AvaloniaProperty.Register<AgentApprovalRequest, AgentApprovalRisk>(nameof(Risk));

    /// <summary>Why it has that risk ("Cannot be recalled once sent").</summary>
    public static readonly StyledProperty<string?> RiskNoteProperty =
        AvaloniaProperty.Register<AgentApprovalRequest, string?>(nameof(RiskNote));

    /// <summary>When the agent asked.</summary>
    public static readonly StyledProperty<DateTimeOffset?> RequestedProperty =
        AvaloniaProperty.Register<AgentApprovalRequest, DateTimeOffset?>(nameof(Requested));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="AgentProperty"/>
    public string? Agent { get => GetValue(AgentProperty); set => SetValue(AgentProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public AgentApprovalKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ContextProperty"/>
    public string? Context { get => GetValue(ContextProperty); set => SetValue(ContextProperty, value); }

    /// <inheritdoc cref="DetailsProperty"/>
    public IReadOnlyList<AgentApprovalDetail>? Details { get => GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }

    /// <inheritdoc cref="RiskProperty"/>
    public AgentApprovalRisk Risk { get => GetValue(RiskProperty); set => SetValue(RiskProperty, value); }

    /// <inheritdoc cref="RiskNoteProperty"/>
    public string? RiskNote { get => GetValue(RiskNoteProperty); set => SetValue(RiskNoteProperty, value); }

    /// <inheritdoc cref="RequestedProperty"/>
    public DateTimeOffset? Requested { get => GetValue(RequestedProperty); set => SetValue(RequestedProperty, value); }
}

/// <summary>Event data of <see cref="AgentApprovals.Decided"/>: the request, the answer and the reason given.</summary>
public sealed class AgentApprovalDecidedEventArgs(RoutedEvent routedEvent, AgentApprovalRequest request, AgentApprovalDecision decision,
    string? reason) : RoutedEventArgs(routedEvent)
{
    /// <summary>The request.</summary>
    public AgentApprovalRequest Request { get; } = request;

    /// <summary>Approved or denied.</summary>
    public AgentApprovalDecision Decision { get; } = decision;

    /// <summary>The note the person wrote; null when none.</summary>
    public string? Reason { get; } = reason;
}
