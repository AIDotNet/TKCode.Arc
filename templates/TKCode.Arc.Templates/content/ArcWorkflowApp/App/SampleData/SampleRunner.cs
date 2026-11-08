using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.SampleData;

/// <summary>Walks the workflow from its triggers without doing anything: each step runs for a moment, a condition takes its first output.</summary>
public sealed class SampleRunner : IWorkflowRunner
{
    /// <summary>How long each step "runs".</summary>
    public static TimeSpan Pace { get; set; } = TimeSpan.FromMilliseconds(450);

    public async Task RunAsync(WorkflowGraph graph, CancellationToken cancellation)
    {
        graph.ResetRun();
        foreach (var node in graph.Nodes)
        {
            node.State = WorkflowNodeState.Pending;
        }
        var queue = new Queue<WorkflowNode>(graph.Nodes.Where(n => !graph.IncomingEdges(n).Any()));
        var visited = new HashSet<WorkflowNode>();
        while (queue.TryDequeue(out var node))
        {
            if (!visited.Add(node))
            {
                continue;
            }
            node.State = WorkflowNodeState.Running;
            var started = DateTime.UtcNow;
            await Task.Delay(Pace, cancellation);
            node.Duration = DateTime.UtcNow - started;
            node.State = WorkflowNodeState.Done;
            // A step with several outputs (a condition) follows its first; the steps only the others reach are skipped.
            var first = node.Outputs.Count > 0 ? node.Outputs[0] : null;
            foreach (var edge in graph.OutgoingEdges(node))
            {
                if (first is null || edge.SourcePort == first)
                {
                    queue.Enqueue(edge.Target);
                }
            }
        }
        foreach (var node in graph.Nodes.Where(n => n.State == WorkflowNodeState.Pending))
        {
            node.State = WorkflowNodeState.Skipped;
        }
    }
}
