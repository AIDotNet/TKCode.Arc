using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class OnboardingChecklistPartlyDoneDemo : UserControl
{
    public OnboardingChecklistPartlyDoneDemo()
    {
        InitializeComponent();
        // Restores what the person finished earlier: these two start in the completed row.
        Checklist.Completed = ["name", "photo"];
    }
}
