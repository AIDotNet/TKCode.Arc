using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class OnboardingChecklistWorkspaceDemo : UserControl
{
    public OnboardingChecklistWorkspaceDemo()
    {
        InitializeComponent();
        foreach (var step in Checklist.Steps)
        {
            if (step.ActionLabel is not null)
            {
                // Stands in for the real work: the step is checked off when the task completes.
                step.Action = () => Task.Delay(900);
            }
        }
    }
}
