using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Workflow;

public class WorkflowVirtualizationTests
{
    /// <summary>400 steps in a column 110 apart: far more than a 600px view shows.</summary>
    private static (WorkflowCanvas Canvas, Avalonia.Controls.Window Window, WorkflowGraph Graph) Show(int count)
    {
        var graph = new WorkflowGraph();
        for (var i = 0; i < count; i++)
        {
            graph.Nodes.Add(new WorkflowNode($"n{i}", $"Step {i}") { Position = new Point(0, i * 110) });
        }
        var canvas = new WorkflowCanvas { Graph = graph, AutoFit = WorkflowAutoFit.None, IsEditable = true };
        var window = TestHelpers.Show(canvas);
        window.Width = 760;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return (canvas, window, graph);
    }

    [AvaloniaFact]
    public void A_Large_Workflow_Realizes_Only_The_Cards_Near_The_View()
    {
        var (canvas, window, graph) = Show(400);
        try
        {
            var realized = graph.Nodes.Count(n => canvas.CardFor(n)!.IsRealized);
            realized.ShouldBeInRange(5, 40);
            canvas.CardFor(graph.Nodes[399])!.IsRealized.ShouldBeFalse();
            // Unrealized cards still have the size their step would have, so the content bounds are right.
            canvas.ContentBounds().Height.ShouldBe(399 * 110 + 60);

            // Scrolling brings the far steps in.
            canvas.ScrollIntoView(graph.Nodes[399], animate: false);
            Dispatcher.UIThread.RunJobs();
            canvas.CardFor(graph.Nodes[399])!.IsRealized.ShouldBeTrue();
            canvas.CardFor(graph.Nodes[399])!.Bounds.Size.ShouldBe(new Size(300, 60));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Reaches_A_Step_Out_Of_The_View_And_A_Small_Workflow_Realizes_All()
    {
        var (canvas, window, graph) = Show(400);
        try
        {
            canvas.FocusNodeForTest(graph.Nodes[399]).ShouldBeTrue();
            canvas.CardFor(graph.Nodes[399])!.IsRealized.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }

        (canvas, window, graph) = Show(50);
        try
        {
            graph.Nodes.ShouldAllBe(n => canvas.CardFor(n)!.IsRealized);
        }
        finally
        {
            window.Close();
        }
    }
}
