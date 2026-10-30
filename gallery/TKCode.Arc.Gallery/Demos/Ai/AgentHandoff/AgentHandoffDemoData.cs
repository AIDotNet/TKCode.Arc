namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented support chat the agent handoff demos play: a store, an order and a person, all made up. The assistant's
/// model is a real one from <see cref="AiModelCatalog"/>.
/// </summary>
internal static class AgentHandoffDemoData
{
    public const string Title = "Order NW-48213";

    public const string Subtitle = "Northwind Home support";

    public const string ReplyEstimate = "2 min";

    /// <summary>The model behind the demo's assistant (Google's Gemini 3.8 Flash; checked on <see cref="AiModelCatalog.CheckedOn"/>).</summary>
    public static readonly string AssistantModel = AiModelCatalog.Gemini38Flash.Name;

    /// <summary>The chat the simulation plays.</summary>
    public static readonly AgentHandoffScript Script = new()
    {
        CustomerOpening = "Hi, my standing desk arrived today and one of the legs is cracked.",
        AssistantAnswer =
            "I'm sorry about that. I found order NW-48213, the Oslo standing desk delivered this morning. " +
            "I can ship a replacement leg, which usually arrives in two days.",
        CustomerRequest = "I'd rather have a full refund. Can I talk to a person?",
        AssistantHandoff = "Of course. I'll pass this to our support team with a summary, so you won't have to explain it again.",
        Summary = new AgentHandoffSummary(
            "Standing desk arrived with a cracked leg. The customer wants a full refund instead of a replacement part.",
            new AgentHandoffField("Order", "NW-48213"),
            new AgentHandoffField("Item", "Oslo standing desk"),
            new AgentHandoffField("Asks for", "Full refund")),
        CustomerNote = "I have photos of the damage if they help.",
        Agent = new AgentHandoffAgent("Jordan Reyes", "Support specialist", "14:05"),
        AgentGreeting = "Hi, I'm Jordan. I've read the summary, so no need to repeat anything. Sorry the desk arrived damaged.",
        AgentResolution =
            "I've issued a full refund of $649 to your card. You'll see it in 3 to 5 business days, and we'll collect the desk for free.",
        ResolutionNotice = "Refund issued.",
        CustomerThanks = "That was quick, thank you!",
        AgentClosing = "Happy to help. I'll mark this as resolved; reply here any time if something comes up.",
        AssistantCancelled = "No problem, I've cancelled the transfer. I'm still here if you need anything.",
        AssistantReply = "Thanks for the details. If you'd like, I can bring in a person from our team.",
        AgentReply = "Thanks, I've added that to your case.",
    };
}
