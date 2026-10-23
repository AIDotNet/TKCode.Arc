using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiSidePanelPreviewDemo : UserControl
{
    public AiSidePanelPreviewDemo()
    {
        InitializeComponent();
        Panel.Page = AiSidePanelDemoData.Page();
        Panel.Suggestions.AddRange(AiSidePanelDemoData.Suggestions);
        Panel.Responder = new AiSidePanelDemoData.ScriptedResponder();

        // Save the page when a change is applied or undone.
        Panel.ChangeApplied += (_, e) => System.Diagnostics.Debug.WriteLine($"Applied to {e.Change.BlockId}");
    }
}
