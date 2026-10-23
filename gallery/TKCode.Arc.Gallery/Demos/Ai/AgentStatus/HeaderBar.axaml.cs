using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentStatusHeaderBarDemo : UserControl
{
    private readonly AgentRunSimulation _coding = new(AgentStatusSamples.Coding);
    private readonly AgentRunSimulation _research = new(AgentStatusSamples.Research);

    public AgentStatusHeaderBarDemo()
    {
        InitializeComponent();
        Left.Simulation = _coding;
        Right.Simulation = _research;
    }
}
