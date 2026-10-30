using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Edits fetched from a (simulated) agent through Run: Stop cancels the request, a failure offers Retry.</summary>
public partial class AgentCursorFromYourAgentDemo : UserControl
{
    public AgentCursorFromYourAgentDemo()
    {
        InitializeComponent();
        Card.Blocks =
        [
            new AgentCursorBlock("title", AgentCursorBlockKind.Title, "Support macro: refunds"),
            new AgentCursorBlock("intro", AgentCursorBlockKind.Paragraph,
                "Thank you so much for reaching out to us about your refund, we really do appreciate you taking the time to write to our team today."),
            new AgentCursorBlock("steps", AgentCursorBlockKind.Paragraph, "Refunds go back to the original payment method within five buisness days."),
        ];
        Card.Agent = AgentCursorDemoData.Agent;
        Card.Collaborator = AgentCursorDemoData.Collaborator;
        Card.Run = FetchEditsAsync;
    }

    /// <summary>Stands in for a call to your agent: waits a moment, then returns its edits (or fails when asked to).</summary>
    private async Task<IReadOnlyList<AgentCursorEdit>> FetchEditsAsync(CancellationToken cancellation)
    {
        var fail = FailNext.IsChecked == true;
        await Task.Delay(1100, cancellation);
        if (fail)
        {
            FailNext.IsChecked = false;
            throw new InvalidOperationException("The agent did not answer in time.");
        }
        return
        [
            AgentCursorEdit.Rewrite("intro", "Thanks for writing in about your refund.", "Shortening the greeting"),
            AgentCursorEdit.Fix("steps", "buisness", "business"),
        ];
    }

    private void OnPhaseChanged(object? sender, AgentCursorPhaseChangedEventArgs e) => Log.Text = $"Phase: {e.Phase}";
}
