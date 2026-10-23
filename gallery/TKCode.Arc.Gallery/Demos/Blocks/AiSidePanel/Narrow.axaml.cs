using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiSidePanelNarrowDemo : UserControl
{
    public AiSidePanelNarrowDemo()
    {
        InitializeComponent();
        Panel.Page = AiSidePanelDemoData.Page();
        Panel.Suggestions.AddRange(AiSidePanelDemoData.Suggestions);
        Panel.Responder = new AiSidePanelDemoData.ScriptedResponder();
    }
}
