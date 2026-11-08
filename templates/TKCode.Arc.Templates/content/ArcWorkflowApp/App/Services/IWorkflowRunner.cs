using TKCode.Arc;

namespace ArcStarterApp.Services;

/// <summary>
/// Runs a workflow. The runner sets each step's <see cref="WorkflowNode.State"/> (Running, Done, Failed, Skipped) as it
/// goes and the canvas shows it. SampleData/SampleRunner.cs only waits a moment per step; call your engine instead.
/// </summary>
public interface IWorkflowRunner
{
    Task RunAsync(WorkflowGraph graph, CancellationToken cancellation);
}
