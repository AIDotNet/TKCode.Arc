namespace TKCode.Arc;

/// <summary>
/// The words of the support chat an <see cref="AgentHandoffSimulation"/> plays, in the order they come: the customer opens,
/// the assistant answers, the customer asks for a person, the assistant hands off with a <see cref="Summary"/>, the customer
/// adds a note, the person (<see cref="Agent"/>) joins, greets, resolves and closes. The replies at the end answer the
/// customer's own actions (Cancel, a typed message). A line left null is skipped. Share one script between simulations; it
/// is only read.
/// </summary>
public sealed class AgentHandoffScript
{
    /// <summary>The customer's first message.</summary>
    public string? CustomerOpening { get; set; }

    /// <summary>The assistant's answer, streamed in chunks.</summary>
    public string? AssistantAnswer { get; set; }

    /// <summary>The customer asking for a person.</summary>
    public string? CustomerRequest { get; set; }

    /// <summary>The assistant's message as it hands off, streamed in chunks.</summary>
    public string? AssistantHandoff { get; set; }

    /// <summary>What the assistant passes to the person, shown in the handoff card.</summary>
    public AgentHandoffSummary? Summary { get; set; }

    /// <summary>A note the customer adds while the transfer waits.</summary>
    public string? CustomerNote { get; set; }

    /// <summary>The person who joins.</summary>
    public AgentHandoffAgent? Agent { get; set; }

    /// <summary>The person's first message.</summary>
    public string? AgentGreeting { get; set; }

    /// <summary>The person's message that resolves the issue.</summary>
    public string? AgentResolution { get; set; }

    /// <summary>Said to screen readers with the resolution, such as "Refund issued.".</summary>
    public string? ResolutionNotice { get; set; }

    /// <summary>The customer's thanks.</summary>
    public string? CustomerThanks { get; set; }

    /// <summary>The person's closing message, before the chat is resolved.</summary>
    public string? AgentClosing { get; set; }

    /// <summary>The assistant's reply when the customer cancels the transfer.</summary>
    public string? AssistantCancelled { get; set; }

    /// <summary>The assistant's reply to a message the customer types.</summary>
    public string? AssistantReply { get; set; }

    /// <summary>The person's reply to a message the customer types.</summary>
    public string? AgentReply { get; set; }
}
