using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiSidePanelProgrammaticDemo : UserControl
{
    public AiSidePanelProgrammaticDemo()
    {
        InitializeComponent();
        Panel.Page = AiSidePanelDemoData.Page();
        Panel.Responder = new AiSidePanelDemoData.ScriptedResponder();

        // The panel can be driven from code: ask, open, close, undo.
        Shorten.Click += async (_, _) => await Panel.AskAsync("Shorten the summary");
        Toggle.Click += (_, _) => Panel.IsOpen = !Panel.IsOpen;
        Undo.Click += (_, _) => Panel.Undo();
    }
}
