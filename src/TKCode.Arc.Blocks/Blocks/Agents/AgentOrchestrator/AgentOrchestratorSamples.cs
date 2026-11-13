using K = TKCode.Arc.OrchestratorMessageKind;
using S = TKCode.Arc.OrchestratorAgentStatus;

namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="AgentOrchestrator"/>, used when the card has no agents of its own: a lead called
/// Atlas writes a launch brief for an invented travel mug with four sub-agents (Scout, Ledger, Quill and Lens).
/// </summary>
public static class AgentOrchestratorSamples
{
    /// <summary>The sample goal given to the lead.</summary>
    public const string Goal = "Draft a one-page launch brief for the Tern 2 travel mug";

    /// <summary>Five fresh sample agents, the lead first.</summary>
    public static IReadOnlyList<OrchestratorAgent> Agents() =>
    [
        new OrchestratorAgent { Id = "atlas", Name = "Atlas", Role = "Lead planner", Model = "Aurora Prime 3" },
        new OrchestratorAgent { Id = "scout", Name = "Scout", Role = "Market research", Model = "Aurora Swift 2" },
        new OrchestratorAgent { Id = "ledger", Name = "Ledger", Role = "Pricing", Model = "Aurora Swift 2" },
        new OrchestratorAgent { Id = "quill", Name = "Quill", Role = "Copywriting", Model = "Halden 7B" },
        new OrchestratorAgent { Id = "lens", Name = "Lens", Role = "Fact check", Model = "Aurora Swift 2" },
    ];

    /// <summary>The sample run, about 19 seconds at speed 1: plan, delegate, a question between two sub-agents, results, the brief.</summary>
    public static IReadOnlyList<OrchestratorStep> Script() =>
    [
        OrchestratorStep.Set(300, "atlas", S.Thinking, "Reading the goal"),
        OrchestratorStep.Set(1100, "atlas", S.Thinking, "Splitting the goal into four tasks"),
        OrchestratorStep.Send(900, "atlas", "scout", K.Delegate, "Find what three rival mugs cost and promise"),
        OrchestratorStep.Send(500, "atlas", "ledger", K.Delegate, "Suggest a launch price and a bundle offer"),
        OrchestratorStep.Send(500, "atlas", "quill", K.Delegate, "Write a headline and three short selling points"),
        OrchestratorStep.Set(400, "atlas", S.Waiting, "Waiting on three agents"),
        OrchestratorStep.Send(1300, "scout", "atlas", K.Progress, "Checked two of three rival stores"),
        OrchestratorStep.Send(800, "quill", "atlas", K.Progress, "Drafting headline options"),
        OrchestratorStep.Send(900, "ledger", "scout", K.Question, "What do the rival mugs cost?"),
        OrchestratorStep.Send(1200, "scout", "ledger", K.Answer, "Between 28 and 36 dollars before shipping"),
        OrchestratorStep.Send(900, "scout", "atlas", K.Result, "Rivals cost 28 to 36 dollars and all promise six hours of heat"),
        OrchestratorStep.Send(700, "atlas", "lens", K.Delegate, "Check every claim in the selling points"),
        OrchestratorStep.Send(1400, "quill", "atlas", K.Result, "Headline: Coffee that keeps up with you"),
        OrchestratorStep.Send(800, "lens", "quill", K.Question, "Can we say eight hours of heat?"),
        OrchestratorStep.Send(1100, "quill", "lens", K.Answer, "Yes: the lab test held 60 degrees for eight hours"),
        OrchestratorStep.Send(900, "ledger", "atlas", K.Result, "Launch at 32 dollars, 15% off a pair"),
        OrchestratorStep.Send(900, "lens", "atlas", K.Progress, "Two of four claims checked"),
        OrchestratorStep.Send(1300, "lens", "atlas", K.Result, "All four claims hold; one needs a footnote"),
        OrchestratorStep.Set(500, "atlas", S.Working, "Putting the brief together"),
        OrchestratorStep.Set(1800, "atlas", S.Done, null, "Brief ready: headline, price, four checked claims and a launch plan"),
    ];

    /// <summary>
    /// A run in which the fact check fails and the lead hands it to another agent: Lens reports an error, Atlas gives the
    /// check to Scout, and the run still completes.
    /// </summary>
    public static IReadOnlyList<OrchestratorStep> RecoveryScript() =>
    [
        OrchestratorStep.Set(300, "atlas", S.Thinking, "Splitting the goal into four tasks"),
        OrchestratorStep.Send(900, "atlas", "quill", K.Delegate, "Write a headline and three short selling points"),
        OrchestratorStep.Send(500, "atlas", "lens", K.Delegate, "Check every claim in the selling points"),
        OrchestratorStep.Send(500, "atlas", "ledger", K.Delegate, "Suggest a launch price and a bundle offer"),
        OrchestratorStep.Set(400, "atlas", S.Waiting, "Waiting on three agents"),
        OrchestratorStep.Send(1200, "lens", "atlas", K.Progress, "Opening the lab report"),
        OrchestratorStep.Send(1400, "lens", "atlas", K.Error, "The lab report link is broken, so no claim can be checked"),
        OrchestratorStep.Set(600, "atlas", S.Thinking, "Finding another way to check the claims"),
        OrchestratorStep.Send(900, "atlas", "scout", K.Delegate, "Check the claims against the product page instead"),
        OrchestratorStep.Send(1300, "quill", "atlas", K.Result, "Headline: Coffee that keeps up with you"),
        OrchestratorStep.Send(900, "ledger", "atlas", K.Result, "Launch at 32 dollars, 15% off a pair"),
        OrchestratorStep.Send(1400, "scout", "atlas", K.Result, "Three claims match the product page; one is removed"),
        OrchestratorStep.Set(500, "atlas", S.Working, "Putting the brief together"),
        OrchestratorStep.Set(1600, "atlas", S.Done, null, "Brief ready with three checked claims"),
    ];
}
