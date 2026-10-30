namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AgentCursor"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; texts that hold a name or a count are format functions.
/// </summary>
public sealed class AgentCursorLabels
{
    /// <summary>The English defaults.</summary>
    public static AgentCursorLabels Default { get; } = new();

    /// <summary>The agent's name when the agent has none (on its cursor and in the statuses).</summary>
    public string Agent { get; init; } = "Agent";

    /// <summary>The status before a run, from the agent's name: "Agent can edit this doc".</summary>
    public Func<string, string> Idle { get; init; } = agent => agent + " can edit this doc";

    /// <summary>The status while the agent reads, from its name: "Agent is reading".</summary>
    public Func<string, string> Reading { get; init; } = agent => agent + " is reading";

    /// <summary>The status of a rewrite with no description of its own.</summary>
    public string Rewriting { get; init; } = "Rewriting a paragraph";

    /// <summary>The status of an inserted heading with no description of its own.</summary>
    public string AddingHeading { get; init; } = "Adding a heading";

    /// <summary>The status of an inserted paragraph with no description of its own.</summary>
    public string AddingParagraph { get; init; } = "Adding a paragraph";

    /// <summary>The status of a fix with no description of its own.</summary>
    public string FixingTypo { get; init; } = "Fixing a typo";

    /// <summary>The status once the edits are done, from their count: "3 edits ready".</summary>
    public Func<int, string> Ready { get; init; } = count => count == 1 ? "1 edit ready" : count + " edits ready";

    /// <summary>The status after the changes were accepted.</summary>
    public string Accepted { get; init; } = "Changes accepted";

    /// <summary>The status after the collaborator accepted the changes, from their first name: "Maya accepted the changes".</summary>
    public Func<string, string> AcceptedBy { get; init; } = name => name + " accepted the changes";

    /// <summary>The status after the changes were undone.</summary>
    public string Reverted { get; init; } = "Changes undone";

    /// <summary>The status after Stop.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The status when the agent had nothing to change.</summary>
    public string Empty { get; init; } = "No changes needed";

    /// <summary>The status when the edits could not be fetched.</summary>
    public string Error { get; init; } = "Couldn't get the edits";

    /// <summary>The review bar's count: "3 changes".</summary>
    public Func<int, string> Changes { get; init; } = count => count == 1 ? "1 change" : count + " changes";

    /// <summary>What follows the count in the review bar on a wide card, from the agent's name: "by Agent".</summary>
    public Func<string, string> By { get; init; } = agent => "by " + agent;

    /// <summary>The review bar's Undo button.</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>The review bar's Accept button.</summary>
    public string Accept { get; init; } = "Accept changes";

    /// <summary>The header's button before a run when it does not start on its own, from the agent's name: "Ask Agent".</summary>
    public Func<string, string> Ask { get; init; } = agent => "Ask " + agent;

    /// <summary>The header's button while the agent works.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The header's button after a run.</summary>
    public string RunAgain { get; init; } = "Run again";

    /// <summary>The header's button after a failed run.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>Added to every announcement of a simulated run.</summary>
    public string Simulated { get; init; } = "Simulated, nothing was saved";

    /// <summary>The accessible name of the document.</summary>
    public string Document { get; init; } = "Document";

    /// <summary>The accessible name of the review bar.</summary>
    public string Review { get; init; } = "Review changes";
}
