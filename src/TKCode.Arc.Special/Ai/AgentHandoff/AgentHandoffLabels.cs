namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AgentHandoff"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; texts that name the person are format functions of their first name.
/// </summary>
public sealed class AgentHandoffLabels
{
    /// <summary>The English defaults.</summary>
    public static AgentHandoffLabels Default { get; } = new();

    /// <summary>The header title when none is set.</summary>
    public string Title { get; init; } = "Support chat";

    /// <summary>The status while the assistant answers.</summary>
    public string AssistantStatus { get; init; } = "AI assistant";

    /// <summary>The status while transferring.</summary>
    public string Transferring { get; init; } = "Transferring";

    /// <summary>The status once a person has joined: "With Jordan".</summary>
    public Func<string, string> With { get; init; } = name => "With " + name;

    /// <summary>The status once resolved.</summary>
    public string Resolved { get; init; } = "Resolved";

    /// <summary>The status when nobody picked the transfer up.</summary>
    public string Unavailable { get; init; } = "No one available";

    /// <summary>The quick reply under the assistant's last answer.</summary>
    public string TalkToPerson { get; init; } = "Talk to a person";

    /// <summary>The Thinking line while the assistant is typing.</summary>
    public string Thinking { get; init; } = "Thinking";

    /// <summary>The name of the typing dots: "Jordan is typing".</summary>
    public Func<string, string> IsTyping { get; init; } = name => name + " is typing";

    /// <summary>The handoff card's title while transferring.</summary>
    public string CardTransferring { get; init; } = "Transferring you to a person";

    /// <summary>The handoff card's title once a person has joined: "Jordan joined from Help desk".</summary>
    public Func<string, string, string> CardConnected { get; init; } = (name, provider) => name + " joined from " + provider;

    /// <summary>The handoff card's title when resolved: "Resolved by Jordan".</summary>
    public Func<string, string> CardResolved { get; init; } = name => "Resolved by " + name;

    /// <summary>The handoff card's title when nobody picked the transfer up.</summary>
    public string CardUnavailable { get; init; } = "No one is available right now";

    /// <summary>The line under the card's title while transferring: "Typical reply 2 min".</summary>
    public Func<string, string> TypicalReply { get; init; } = estimate => "Typical reply " + estimate;

    /// <summary>The line under the card's title when nobody picked the transfer up.</summary>
    public string UnavailableHint { get; init; } = "Your summary is saved. Try again, or keep chatting with the assistant.";

    /// <summary>The line under the card's title once a person has joined: the provider it moved to.</summary>
    public Func<string, string> ViaProvider { get; init; } = provider => "Conversation moved to " + provider;

    /// <summary>The Cancel button on the card while transferring.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The Try again button on the card when nobody is available.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The summary toggle.</summary>
    public string SummaryShared { get; init; } = "Summary shared with the team";

    /// <summary>The name of the summary toggle when it is closed.</summary>
    public string ShowSummary { get; init; } = "Show summary";

    /// <summary>The name of the summary toggle when it is open.</summary>
    public string HideSummary { get; init; } = "Hide summary";

    /// <summary>The join step: "Jordan Reyes joined".</summary>
    public Func<string, string> Joined { get; init; } = name => name + " joined";

    /// <summary>The resolved step at the end of the thread.</summary>
    public string ResolvedStep { get; init; } = "Conversation resolved";

    /// <summary>The name of the person when no agent is given.</summary>
    public string AgentFallback { get; init; } = "Support agent";

    /// <summary>The composer placeholder while the assistant answers.</summary>
    public string PlaceholderAssistant { get; init; } = "Ask a question";

    /// <summary>The composer placeholder while transferring: notes for the team.</summary>
    public string PlaceholderTransferring { get; init; } = "Add a note for the team";

    /// <summary>The composer placeholder once a person has joined: "Message Jordan".</summary>
    public Func<string, string> PlaceholderAgent { get; init; } = name => "Message " + name;

    /// <summary>The composer placeholder when nobody is available.</summary>
    public string PlaceholderUnavailable { get; init; } = "Message the assistant";

    /// <summary>The name of the composer's text box.</summary>
    public string Composer { get; init; } = "Message";

    /// <summary>The name of the send button.</summary>
    public string Send { get; init; } = "Send";

    /// <summary>The name of the thread (a log that keyboard users can scroll).</summary>
    public string Thread { get; init; } = "Conversation";

    /// <summary>Said once when the built-in simulation starts.</summary>
    public string SimulatedNotice { get; init; } = "Simulated support chat. The messages are a demo.";

    /// <summary>Said when the transfer starts.</summary>
    public string AnnounceTransferring { get; init; } = "Transferring you to a person.";

    /// <summary>Said when a person joins: "Jordan Reyes joined the chat.".</summary>
    public Func<string, string> AnnounceConnected { get; init; } = name => name + " joined the chat.";

    /// <summary>Said when the conversation is resolved.</summary>
    public string AnnounceResolved { get; init; } = "Conversation resolved.";

    /// <summary>Said when nobody picked the transfer up.</summary>
    public string AnnounceUnavailable { get; init; } = "No one is available right now.";

    /// <summary>Said when the transfer is cancelled and the assistant is back.</summary>
    public string AnnounceAssistant { get; init; } = "You are chatting with the AI assistant.";
}
