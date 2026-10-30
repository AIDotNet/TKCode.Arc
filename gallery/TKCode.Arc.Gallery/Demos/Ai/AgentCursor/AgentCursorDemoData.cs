using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented session the agent cursor demos play: a short launch brief for an invented product, an invented teammate
/// and three edits (a rewrite of the summary, a heading added before the rollout paragraph and a typo fixed in it). The
/// agent is named after a real model from <see cref="AiModelCatalog"/>, drawn with a neutral mark. Demo content only.
/// </summary>
internal static class AgentCursorDemoData
{
    public const string Meta = "Maya Ortiz · Target date Nov 14";

    /// <summary>The agent: a real model's name, a neutral mark and a fixed warm color for its cursor, selection and tint.</summary>
    public static AgentModel Agent { get; } =
        new(AiModelCatalog.ClaudeOpus55.Name, ArcIcons.Sparkles, new ImmutableSolidColorBrush(Color.FromRgb(0xE0, 0x7A, 0x45)));

    /// <summary>The teammate who reads along.</summary>
    public static AgentCursorCollaborator Collaborator { get; } = new("Maya Ortiz");

    /// <summary>The launch brief.</summary>
    public static IReadOnlyList<AgentCursorBlock> Blocks { get; } =
    [
        new("title", AgentCursorBlockKind.Title, "Launch brief: Shared inbox"),
        new("summary-heading", AgentCursorBlockKind.Heading, "Summary"),
        new("summary", AgentCursorBlockKind.Paragraph,
            "Shared inbox is a new place where the whole team is able to answer customer email together. In this release we are adding assignment, internal notes and a status for every thread, so that nothing gets lost when somebody happens to be out of the office."),
        new("goals-heading", AgentCursorBlockKind.Heading, "Goals"),
        new("goals", AgentCursorBlockKind.Paragraph,
            "Cut the time to a first reply for teams of five to twenty people, and make it obvious who owns each conversation."),
        new("rollout", AgentCursorBlockKind.Paragraph,
            "Beta workspaces get it on Nov 3 and everyone on Nov 14, with a short in-app tour and a help center artcle."),
    ];

    /// <summary>The agent's three edits.</summary>
    public static IReadOnlyList<AgentCursorEdit> Edits { get; } =
    [
        AgentCursorEdit.Rewrite("summary",
            "Shared inbox lets a team answer customer email together. This release adds assignment, internal notes and a status on every thread, so nothing slips when someone is away.",
            "Tightening the summary"),
        AgentCursorEdit.Insert("goals", AgentCursorBlockKind.Heading, "Rollout", "rollout-heading", "Adding a Rollout heading"),
        AgentCursorEdit.Fix("rollout", "artcle", "article", "Fixing a typo"),
    ];

    /// <summary>The model named on the agent of the "your document" demo.</summary>
    public static string YourAgentName => AiModelCatalog.Gemini35FlashLite.Name;
}
