using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentStatusStatesDemo : UserControl
{
    public AgentStatusStatesDemo()
    {
        InitializeComponent();
        var agent = AgentStatusDemoData.CodingAgent;
        var started = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(74);
        Add(AgentStatusRunState.Idle, null, agent, started);
        Add(AgentStatusRunState.Working, new AgentStatusActivity("Checking specs", ArcIcons.Search, 8, 12), agent, started);
        Add(AgentStatusRunState.NeedsInput, new AgentStatusActivity("Needs your approval to deploy", ArcIcons.Search), agent, started);
        Add(AgentStatusRunState.Paused, new AgentStatusActivity("Checking specs", ArcIcons.Search, 8, 12), agent, started);
        Add(AgentStatusRunState.Done, new AgentStatusActivity("5 files touched"), agent, started, DateTimeOffset.UtcNow - TimeSpan.FromSeconds(14));
        Add(AgentStatusRunState.Failed, new AgentStatusActivity("Fetch failed · 2 pages did not load"), AgentStatusDemoData.ResearchAgent, started);
        Add(AgentStatusRunState.Stopped, null, AgentStatusDemoData.ResearchAgent, started, DateTimeOffset.UtcNow - TimeSpan.FromSeconds(7));
    }

    private void Add(AgentStatusRunState state, AgentStatusActivity? activity, AgentModel agent, DateTimeOffset started, DateTimeOffset? ended = null)
    {
        var pill = new AgentStatus
        {
            Status = state,
            Activity = activity,
            Agent = agent,
            StartedAt = started,
            EndedAt = ended,
            SettleAfter = null,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        pill.Steps.Add(new AgentStatusStep("scan", "Scanned 9 modules", AgentStatusStepState.Done, ArcIcons.Search) { Duration = TimeSpan.FromMilliseconds(1917) });
        pill.Steps.Add(new AgentStatusStep("patch", "Patched lib/dates/parse-due.ts", AgentStatusStepState.Done, ArcIcons.Search) { Duration = TimeSpan.FromMilliseconds(2533) });
        var third = state switch
        {
            AgentStatusRunState.Working or AgentStatusRunState.Paused => AgentStatusStepState.Active,
            AgentStatusRunState.NeedsInput => AgentStatusStepState.Waiting,
            AgentStatusRunState.Failed => AgentStatusStepState.Failed,
            AgentStatusRunState.Stopped => AgentStatusStepState.Skipped,
            AgentStatusRunState.Idle => AgentStatusStepState.Skipped,
            _ => AgentStatusStepState.Done
        };
        pill.Steps.Add(new AgentStatusStep("third", state == AgentStatusRunState.Failed ? "2 pages did not load" : "Checking specs", third, ArcIcons.Search)
        {
            StartedAt = started + TimeSpan.FromSeconds(20),
            Duration = third == AgentStatusStepState.Done ? TimeSpan.FromMilliseconds(3316) : null
        });
        pill.Steps.Add(new AgentStatusStep("last", "Deploy", AgentStatusStepState.Pending));
        Stack.Children.Add(pill);
    }
}
