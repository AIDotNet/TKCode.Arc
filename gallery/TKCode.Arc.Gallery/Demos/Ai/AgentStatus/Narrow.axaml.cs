using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentStatusNarrowDemo : UserControl
{
    public AgentStatusNarrowDemo()
    {
        InitializeComponent();
        Pill.Agent = AgentStatusSamples.CodingAgent;
        Pill.StartedAt = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(42);
        Pill.Activity = new AgentStatusActivity("Waiting on you to deploy to staging", ArcIcons.Search);
        Pill.Steps.Add(new AgentStatusStep("scan", "Scanned 9 modules", AgentStatusStepState.Done, ArcIcons.Search) { Duration = TimeSpan.FromMilliseconds(1917) });
        Pill.Steps.Add(new AgentStatusStep("patch", "Patched lib/dates/parse-due.ts", AgentStatusStepState.Done, ArcIcons.Search) { Duration = TimeSpan.FromMilliseconds(2533) });
        Pill.Steps.Add(new AgentStatusStep("deploy", "Approval needed to deploy to staging", AgentStatusStepState.Waiting, ArcIcons.CloudUpload) { StartedAt = DateTimeOffset.UtcNow });
    }
}
