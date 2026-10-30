using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An app's own document, edits, agent and teammate; the accepted document is stored and passed back.</summary>
public partial class AgentCursorYourDocumentDemo : UserControl
{
    public AgentCursorYourDocumentDemo()
    {
        InitializeComponent();
        Card.Blocks =
        [
            new AgentCursorBlock("title", AgentCursorBlockKind.Title, "Weekly update"),
            new AgentCursorBlock("shipped", AgentCursorBlockKind.Paragraph,
                "This week the team shipped the new onboarding flow and also fixed two crashes that happened on start for some people."),
            new AgentCursorBlock("next", AgentCursorBlockKind.Paragraph, "Next week we plan to recieve feedback from five customers."),
        ];
        Card.Edits =
        [
            AgentCursorEdit.Rewrite("shipped", "We shipped the new onboarding flow and fixed two crashes on start.", "Making it shorter"),
            AgentCursorEdit.Insert("shipped", AgentCursorBlockKind.Heading, "Next week", "next-heading"),
            AgentCursorEdit.Fix("next", "recieve", "collect", "Choosing a better word"),
        ];
        // An agent of your own: its name, a 24 × 24 stroked mark and the color of its cursor, selection and tint.
        Card.Agent = new AgentModel(AgentCursorDemoData.YourAgentName, ArcIcons.Brain, new ImmutableSolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xD6)));
        Card.Collaborator = new AgentCursorCollaborator("Leo Park");
    }

    private void OnAccepted(object? sender, AgentCursorDocumentEventArgs e)
    {
        // Store the document and pass it back: an equal document does not reset the card.
        Card.Blocks = e.Blocks.ToList();
        Log.Text = $"Accepted: {e.Blocks.Count} blocks saved.";
    }

    private void OnUndone(object? sender, AgentCursorDocumentEventArgs e) => Log.Text = $"Undone: back to {e.Blocks.Count} blocks.";
}
