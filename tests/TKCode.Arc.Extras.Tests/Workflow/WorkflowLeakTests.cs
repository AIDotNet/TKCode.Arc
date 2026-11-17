using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Workflow;

/// <summary>
/// A workflow often outlives the canvas that shows it (a view-model's graph, the undo history): nothing in the graph may keep
/// a closed canvas, or the controls around it, alive.
/// </summary>
public class WorkflowLeakTests
{

    private static WorkflowGraph Graph(int count)
    {
        var graph = new WorkflowGraph();
        for (var i = 0; i < count; i++)
        {
            graph.Nodes.Add(new WorkflowNode($"n{i}", $"Step {i}") { Position = new Point(0, i * 110) });
        }
        for (var i = 1; i < count; i++)
        {
            graph.Connect(graph.Nodes[i - 1], graph.Nodes[i]);
        }
        return graph;
    }

    /// <summary>Shows a canvas on <paramref name="graph"/>, lets <paramref name="use"/> work it, closes everything.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] Over(WorkflowGraph graph, Action<WorkflowCanvas> use, bool withMap = true)
    {
        var canvas = new WorkflowCanvas { Graph = graph, IsEditable = true, AutoFit = WorkflowAutoFit.None };
        var panel = new Panel { Children = { canvas } };
        var map = withMap ? new WorkflowMinimap { Target = canvas } : null;
        var bar = withMap ? new WorkflowZoomControls { Target = canvas } : null;
        if (map is not null && bar is not null)
        {
            panel.Children.Add(map);
            panel.Children.Add(bar);
        }
        var window = TestHelpers.Show(panel);
        window.Width = 760;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        use(canvas);
        Dispatcher.UIThread.RunJobs();
        window.Content = null;
        window.Close();
        Dispatcher.UIThread.RunJobs();
        return map is null ? [new WeakReference(canvas)] : [new WeakReference(canvas), new WeakReference(map), new WeakReference(bar)];
    }

    private static void ShouldBeCollected(params WeakReference[][] groups)
    {
        var all = groups.SelectMany(g => g).ToArray();
        // The motion scheduler lets go of a stopped driver on its next frame; Collect ticks one each round.
        TestHelpers.Collect(all);
        all.Where(r => r.IsAlive).Select(r => r.Target?.GetType().Name).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_Closed_Canvas_Is_Not_Kept_By_Its_Workflow()
    {
        var graph = Graph(6);
        var refs = Over(graph, _ => { });
        ShouldBeCollected(refs);
        GC.KeepAlive(graph);
    }

    [AvaloniaFact]
    public void Deleting_Undoing_And_Connecting_Leave_Nothing_Behind()
    {
        var graph = Graph(6);
        var refs = Over(graph, canvas =>
        {
            // Deleted steps stay in the undo history, with whatever listens to them.
            canvas.Select([graph.Nodes[2], graph.Nodes[3]]);
            canvas.DeleteSelection();
            canvas.Undo();
            canvas.Redo();
            graph.Connect(graph.Nodes[0], graph.Nodes[^1]);
            using (graph.History.BeginGroup())
            {
                graph.Nodes.Add(new WorkflowNode("late", "Late") { Position = new Point(300, 0) });
            }
            graph.Nodes.RemoveAt(graph.Nodes.Count - 1);
            graph.Nodes[0].Title = "Renamed";
            // A running step spins its mark; closing the page mid-run must let go of it too.
            graph.Nodes[0].State = WorkflowNodeState.Running;
        });
        ShouldBeCollected(refs);
        graph.History.CanUndo.ShouldBeTrue();
        GC.KeepAlive(graph);
    }

    [AvaloniaFact]
    public void A_Large_Virtualized_Workflow_Leaves_Nothing_Behind()
    {
        var graph = Graph(400);
        var refs = Over(graph, canvas =>
        {
            canvas.FocusNodeForTest(graph.Nodes[399]);
            canvas.ScrollIntoView(graph.Nodes[200], animate: false);
            canvas.Select([graph.Nodes[399]]);
            canvas.DeleteSelection();
        });
        ShouldBeCollected(refs);
        GC.KeepAlive(graph);
    }

    [AvaloniaFact]
    public void Swapping_The_Workflow_Lets_The_Old_One_Go()
    {
        var oldGraph = Graph(5);
        WeakReference graphRef = new(oldGraph);
        var canvasRefs = Over(Graph(3), canvas =>
        {
            canvas.Graph = oldGraph;
            canvas.Select([oldGraph.Nodes[1]]);
            canvas.DeleteSelection();
            canvas.Graph = Graph(2);
        });
        // The canvas let go of the old workflow (this test holds it, so look at who holds whom the other way).
        ShouldBeCollected(canvasRefs);
        GC.KeepAlive(oldGraph);
        graphRef.IsAlive.ShouldBeTrue();
    }
}
