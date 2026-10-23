using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PromptStudioPreviewDemo : UserControl
{
    public PromptStudioPreviewDemo()
    {
        InitializeComponent();
        Studio.Versions.AddRange(PromptStudioDemoData.Versions());
        Studio.Variables.AddRange(PromptStudioDemoData.Variables);
        Studio.TestCases.AddRange(PromptStudioDemoData.TestCases());
        Studio.Models.AddRange(PromptStudioDemoData.Models());
        Studio.OpenVersion = Studio.Versions[0];
        Studio.ProductionVersion = Studio.Versions[0];
        Studio.SelectedTestCase = Studio.TestCases[0];
        Studio.SelectedModel = Studio.Models[1];

        // Runs go through your handler; the "Out of policy" case fails once so Retry can be tried.
        Studio.RunHandler = new PromptStudioDemoData.Runner().Run;
    }
}
