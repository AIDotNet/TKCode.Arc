using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class OnboardingIllustrationCustomStepsDemo : UserControl
{
    public OnboardingIllustrationCustomStepsDemo()
    {
        InitializeComponent();
        Card.Steps.AddRange(OnboardingIllustrationDemoData.ResearchSteps());
        // The labels hold functions for sentences with numbers, so they are set in code.
        Card.Labels = OnboardingIllustrationDemoData.ResearchLabels();
    }
}
