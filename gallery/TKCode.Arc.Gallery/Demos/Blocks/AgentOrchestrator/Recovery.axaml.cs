using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentOrchestratorRecoveryDemo : UserControl
{
    public AgentOrchestratorRecoveryDemo()
    {
        InitializeComponent();
        Orchestrator.Script = AgentOrchestratorSamples.RecoveryScript();
    }
}
