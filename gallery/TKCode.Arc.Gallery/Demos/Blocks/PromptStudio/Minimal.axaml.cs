using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PromptStudioMinimalDemo : UserControl
{
    public PromptStudioMinimalDemo()
    {
        InitializeComponent();
        Studio.Draft = new PromptTemplate("You greet people warmly in one sentence.", "Greet {{name}} who just joined the {{team}} team.");
        var testCase = new PromptTestCase { Id = "one", Label = "New hire" };
        testCase.Values["name"] = "Sam";
        testCase.Values["team"] = "support";
        Studio.TestCases.Add(testCase);
        Studio.SelectedTestCase = testCase;
    }
}
