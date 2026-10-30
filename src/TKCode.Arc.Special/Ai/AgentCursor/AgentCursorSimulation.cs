namespace TKCode.Arc;

/// <summary>
/// A document session an <see cref="AgentCursor"/> plays without an agent, from data you give it: the document, the edits
/// the agent makes, the agent and a teammate who reads along, and the muted line under the title. Set it as
/// <see cref="AgentCursor.Simulation"/> while the card has no <see cref="AgentCursor.Blocks"/>,
/// <see cref="AgentCursor.Edits"/> or <see cref="AgentCursor.Run"/>: the card shows the document, plays the edits, says
/// nothing was saved and, with full motion, loops (the teammate accepts after a short pause, then it starts over).
/// </summary>
public sealed class AgentCursorSimulation
{
    /// <summary>The document as title, heading and paragraph blocks with stable ids.</summary>
    public IReadOnlyList<AgentCursorBlock>? Blocks { get; set; }

    /// <summary>The edits the agent makes, in order (rewrite, insert, fix).</summary>
    public IReadOnlyList<AgentCursorEdit>? Edits { get; set; }

    /// <summary>The agent: its name on the cursor, its mark and, from a solid mark brush, its color.</summary>
    public AgentModel? Agent { get; set; }

    /// <summary>The teammate who reads along and accepts when the session loops.</summary>
    public AgentCursorCollaborator? Collaborator { get; set; }

    /// <summary>A muted line under the title, such as the owner and a target date.</summary>
    public string? Meta { get; set; }
}
