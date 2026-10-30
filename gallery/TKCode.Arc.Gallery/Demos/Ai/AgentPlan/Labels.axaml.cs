using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The plan's words replaced: a terser vocabulary, a different note and different state words.</summary>
public partial class AgentPlanLabelsDemo : UserControl
{
    public AgentPlanLabelsDemo()
    {
        InitializeComponent();
        Plan.Labels = new AgentPlanLabels
        {
            Heading = "Checklist",
            StepCount = n => n == 1 ? "1 task" : $"{n} tasks",
            Approve = "Go",
            KeepPlanning = "More ideas",
            AddStep = "Add a task",
            Working = n => $"Task {n} in progress",
            Suggested = "Idea",
            DoneIn = total => $"Finished in {total}."
        };
        Plan.Model = AgentPlanDemoData.SmallPlansModel;
        Plan.Simulation = new AgentPlanSimulation
        {
            Steps =
            [
                new("a", "Find every link to the old settings page", new(["Searched for /settings", "Found 14 links"], TimeSpan.FromSeconds(9))),
                new("b", "Rename the route and keep a redirect", new(["Edited routes.ts", "Ran 6 tests"], TimeSpan.FromSeconds(16))),
                new("c", "Update the screenshots in the guide", new(["Edited guide/settings.md"], TimeSpan.FromSeconds(7)))
            ],
            Suggestions = [],
            Summary = "The page now lives at /preferences, and the old address redirects."
        };
    }
}
