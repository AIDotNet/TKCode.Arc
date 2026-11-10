using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentRunFailedDemo : UserControl
{
    public AgentRunFailedDemo()
    {
        InitializeComponent();
        var steps = new ObservableCollection<AgentRunStep>
        {
            new() { Kind = AgentRunStepKind.Thinking, Title = "Plan the rename", Status = AgentRunStepStatus.Done, Duration = TimeSpan.FromSeconds(1.9),
                    Text = "Two tables and eleven queries use the old names. A migration renames the tables, then the queries follow." },
            new() { Kind = AgentRunStepKind.FileEdit, Title = "Write the migration", Status = AgentRunStepStatus.Done, Duration = TimeSpan.FromSeconds(3.1),
                    Files = [new AgentRunFile("db/migrations/0042_rename_billing.sql", AgentRunFileChange.Created, 18)] },
            new() { Kind = AgentRunStepKind.ToolCall, Title = "Run the migration on the test database", ToolName = "run_command",
                    Input = "make migrate DB=test", Status = AgentRunStepStatus.Failed, Duration = TimeSpan.FromSeconds(6.4),
                    Output = "error: relation \"invoice_lines\" is locked by another session" }
        };
        Run.Steps = steps;
        Run.Cost = 0.0182m;
        Run.Tokens = 9_420;
        Run.FailureMessage = "The test database was locked by another session.";
        Log.Text = "Press Retry to see the event.";
        Run.RetryRequested += (_, _) =>
        {
            // Your backend would start a new run here; this demo only resets the step.
            Log.Text = "RetryRequested: starting a new run";
            steps[2].Status = AgentRunStepStatus.Running;
            steps[2].Output = null;
            Run.State = AgentRunState.Running;
        };
        Run.StopRequested += (_, _) => Log.Text = "StopRequested: the run is cancelled";
    }
}
