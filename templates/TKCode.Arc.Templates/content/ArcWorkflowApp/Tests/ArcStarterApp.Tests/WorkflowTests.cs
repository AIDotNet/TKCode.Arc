using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ArcStarterApp.Models;
using ArcStarterApp.SampleData;
using ArcStarterApp.Services;
using ArcStarterApp.ViewModels;
using TKCode.Arc;

namespace ArcStarterApp.Tests;

public class WorkflowTests
{
    public WorkflowTests()
    {
        // Every test starts from the sample workflow.
        TestApp.Workflow.Json = null;
    }

    [AvaloniaFact]
    public void The_sample_survives_a_round_trip()
    {
        var document = SampleWorkflow.Create();

        var again = WorkflowSerializer.ToDocument(WorkflowSerializer.ToGraph(document), document.Name);

        Assert.Equal(document.Steps.Select(s => s.Id), again.Steps.Select(s => s.Id));
        Assert.Equal(document.Connections.Count, again.Connections.Count);
        Assert.Contains(again.Connections, c => c.From == "check" && c.Output == 1 && c.To == "refund");
    }

    [AvaloniaFact]
    public void A_new_step_follows_the_selected_one_and_is_saved()
    {
        var viewModel = App.Services.GetRequiredService<MainViewModel>();
        viewModel.Selected = viewModel.Graph.FindNode("notify");

        var step = viewModel.AddStep(StepKinds.Find("wait"), null);

        Assert.Contains(viewModel.Graph.OutgoingEdges(viewModel.Graph.FindNode("notify")!), e => e.Target == step);
        Assert.Equal(7, WorkflowStore.FromJson(TestApp.Workflow.Json!)!.Steps.Count);
    }

    [AvaloniaFact]
    public async Task A_run_takes_the_first_branch_of_a_condition()
    {
        SampleRunner.Pace = TimeSpan.Zero;
        var viewModel = App.Services.GetRequiredService<MainViewModel>();

        await viewModel.RunCommand.ExecuteAsync(null);

        Assert.Equal(WorkflowNodeState.Done, viewModel.Graph.FindNode("approve")!.State);
        Assert.Equal(WorkflowNodeState.Done, viewModel.Graph.FindNode("notify")!.State);
    }
}
