using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PromptStudioCompareDemo : UserControl
{
    public PromptStudioCompareDemo()
    {
        InitializeComponent();
        Studio.Versions.AddRange(PromptStudioDemoData.Versions());
        Studio.Variables.AddRange(PromptStudioDemoData.Variables);
        Studio.TestCases.AddRange(PromptStudioDemoData.TestCases());
        Studio.Models.AddRange(PromptStudioDemoData.Models());
        Studio.OpenVersion = Studio.Versions[0];
        Studio.ProductionVersion = Studio.Versions[1];

        // Open straight in the compare view: v4 against the version in production.
        Studio.CompareVersion = Studio.Versions[1];
        Studio.IsCompareMode = true;
        Studio.RunHandler = new PromptStudioDemoData.Runner().Run;
    }
}
