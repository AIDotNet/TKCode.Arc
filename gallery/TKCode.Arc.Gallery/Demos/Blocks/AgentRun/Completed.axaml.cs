using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentRunCompletedDemo : UserControl
{
    public AgentRunCompletedDemo()
    {
        InitializeComponent();
        // A run your backend finished earlier: the sample's steps, all done.
        Run.Steps = AgentRunSamples.CompletedSteps();
        Run.Cost = AgentRunSamples.TotalCost;
        Run.Tokens = AgentRunSamples.TotalTokens;
    }
}
