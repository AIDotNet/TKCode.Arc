using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A running plan built from plain data: Retry puts the failed step back to work, Skip leaves it.</summary>
public partial class AgentPlanMidRunDemo : UserControl
{
    public AgentPlanMidRunDemo()
    {
        InitializeComponent();
        Plan.Model = new AgentModel("Ember 1", ArcIcons.Sparkles);
        Plan.Steps.Add(Finished("Find the slowest ten tasks", 18, "Ran the build with timing", "Found 4 tasks over a minute"));
        Plan.Steps.Add(Finished("Cache the generated sources", 41, "Edited build/cache.json", "Ran 12 tests"));
        var added = Finished("Skip the cache when the lockfile changes", 25, "Edited build/cache.json");
        added.NoteKind = AgentPlanNoteKind.AddedByAgent;
        Plan.Steps.Add(added);
        var failed = new AgentPlanStep(null, "Run the full build and compare the time")
        {
            State = AgentPlanStepState.Failed,
            Error = "The build ran out of disk space on the runner"
        };
        failed.Activities.Add("Ran the build");
        failed.Activities.Add("Measured 3 minutes 10 seconds");
        Plan.Steps.Add(failed);
        Plan.Steps.Add(new AgentPlanStep(null, "Write the result into the contributor guide"));
    }

    private static AgentPlanStep Finished(string title, int seconds, params string[] lines)
    {
        var step = new AgentPlanStep(null, title) { State = AgentPlanStepState.Done, Duration = TimeSpan.FromSeconds(seconds) };
        foreach (var line in lines)
        {
            step.Activities.Add(line);
        }
        return step;
    }

    private void OnStepAction(object? sender, AgentPlanStepActionEventArgs e)
    {
        if (e.Kind == AgentPlanStepActionKind.Retry)
        {
            e.Step.Error = null;
            e.Step.State = AgentPlanStepState.Active;
        }
        else
        {
            e.Step.State = AgentPlanStepState.Skipped;
            Plan.Steps[^1].State = AgentPlanStepState.Active;
        }
    }
}
