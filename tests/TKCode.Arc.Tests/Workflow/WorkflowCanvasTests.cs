using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Workflow;

public class WorkflowCanvasTests
{
    /// <summary>Arc's HeroRelay workflow: trigger → lookup → branch → three compact actions.</summary>
    private static (WorkflowGraph Graph, WorkflowNode[] Nodes) Relay()
    {
        var graph = new WorkflowGraph();
        WorkflowNode[] nodes =
        [
            new("trigger", "Order placed") { Description = "Northwind Labs, $4,800" },
            new("lookup", "Find customer account") { Description = "Plan, seats and owner", Kind = "action" },
            new("branch", "Amount over $1,000") { Description = "Condition" },
            new("notify", "Post to #revenue") { Appearance = WorkflowNodeAppearance.Compact },
            new("task", "Create onboarding task") { Appearance = WorkflowNodeAppearance.Compact },
            new("save", "Save to warehouse") { Appearance = WorkflowNodeAppearance.Compact }
        ];
        foreach (var node in nodes)
        {
            graph.Nodes.Add(node);
        }
        graph.Connect(nodes[0], nodes[1]);
        graph.Connect(nodes[1], nodes[2]);
        graph.Connect(nodes[2], nodes[3]);
        graph.Connect(nodes[2], nodes[4]);
        graph.Connect(nodes[2], nodes[5]);
        return (graph, nodes);
    }

    private static Window Show(WorkflowCanvas canvas, double width = 760, double height = 600, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(canvas, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [Fact]
    public void The_Graph_Refuses_Foreign_Edges_And_Drops_The_Edges_Of_Removed_Steps()
    {
        var (graph, nodes) = Relay();
        Should.Throw<ArgumentException>(() => graph.Connect(nodes[0], new WorkflowNode("stray", "Stray")));
        graph.Nodes.Remove(nodes[2]);
        graph.Edges.Count.ShouldBe(1);
        graph.Edges[0].Target.ShouldBe(nodes[1]);
        graph.Nodes.Clear();
        graph.Edges.ShouldBeEmpty();
    }

    [Fact]
    public void ResetRun_Gets_Every_Step_Ready_For_A_New_Run()
    {
        var (graph, nodes) = Relay();
        nodes[1].State = WorkflowNodeState.Done;
        nodes[1].Result = "Enterprise";
        nodes[1].Duration = TimeSpan.FromMilliseconds(118);
        nodes[4].State = WorkflowNodeState.Failed;
        nodes[4].Error = "Timed out";
        graph.Edges[0].State = WorkflowEdgeState.Active;
        graph.Edges[1].State = WorkflowEdgeState.Idle;
        graph.ResetRun();
        nodes.ShouldAllBe(n => n.State == WorkflowNodeState.Pending && n.Result == null && n.Duration == null && n.Error == null);
        graph.Edges[0].State.ShouldBe(WorkflowEdgeState.Auto);
        // An edge the app keeps quiet stays quiet.
        graph.Edges[1].State.ShouldBe(WorkflowEdgeState.Idle);
    }

    [Fact]
    public void Edges_Follow_Their_Steps()
    {
        var source = new WorkflowNode();
        var target = new WorkflowNode();
        var edge = new WorkflowEdge(source, target);
        WorkflowCanvas.Derive(edge).ShouldBe((false, false));
        source.State = WorkflowNodeState.Done;
        target.State = WorkflowNodeState.Pending;
        WorkflowCanvas.Derive(edge).ShouldBe((false, false));
        foreach (var reached in new[] { WorkflowNodeState.Running, WorkflowNodeState.Done, WorkflowNodeState.Failed })
        {
            target.State = reached;
            WorkflowCanvas.Derive(edge).ShouldBe((true, false), reached.ToString());
        }
        target.State = WorkflowNodeState.Skipped;
        WorkflowCanvas.Derive(edge).ShouldBe((false, true));
        target.State = WorkflowNodeState.Running;
        source.State = WorkflowNodeState.Skipped;
        WorkflowCanvas.Derive(edge).ShouldBe((false, true));
        // The edge's own state wins.
        edge.State = WorkflowEdgeState.Active;
        WorkflowCanvas.Derive(edge).ShouldBe((true, false));
        edge.State = WorkflowEdgeState.Idle;
        WorkflowCanvas.Derive(edge).ShouldBe((false, false));
        edge.State = WorkflowEdgeState.Skipped;
        WorkflowCanvas.Derive(edge).ShouldBe((false, true));
    }

    [AvaloniaFact]
    public void Steps_Without_A_Position_Are_Arranged_Like_Arcs_Workflow()
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            // hero-relay.tsx LAYOUTS.wide.
            nodes.Select(n => n.Position).ShouldBe([
                new Point(170, 0), new Point(170, 112), new Point(170, 224),
                new Point(0, 356), new Point(220, 356), new Point(440, 356)
            ]);
            canvas.CardFor(nodes[0])!.Bounds.Size.ShouldBe(new Size(300, 60));
            canvas.CardFor(nodes[3])!.Bounds.Size.ShouldBe(new Size(200, 84));
            canvas.CardFor(nodes[3])!.Classes.ShouldContain(WorkflowNodeCard.CompactPseudoClass);
            canvas.ContentBounds().ShouldBe(new Rect(0, 0, 640, 440));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Workflow_The_Canvas_Arranged_Is_Arranged_Again_When_It_Grows()
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            var extra = new WorkflowNode("extra", "Send receipt");
            graph.Nodes.Add(extra);
            graph.Connect(nodes[5], extra);
            Dispatcher.UIThread.RunJobs();
            // A fifth rank, centered under the step it follows.
            nodes[0].Position.ShouldBe(new Point(170, 0));
            extra.Position.ShouldBe(new Point(390, 356 + 84 + 52));
            canvas.CardFor(extra).ShouldNotBeNull();
            graph.Nodes.Remove(extra);
            Dispatcher.UIThread.RunJobs();
            canvas.CardFor(extra).ShouldBeNull();
            canvas.EdgeVisualFor(graph.Edges[0]).ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_Steps_Go_Below_Steps_Your_Code_Placed()
    {
        var (graph, nodes) = Relay();
        Point[] places = [new(170, 0), new(170, 112), new(170, 224), new(0, 356), new(220, 356), new(440, 356)];
        for (var i = 0; i < nodes.Length; i++)
        {
            nodes[i].Position = places[i];
        }
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            var extra = new WorkflowNode("extra", "Send receipt");
            graph.Nodes.Add(extra);
            graph.Connect(nodes[5], extra);
            Dispatcher.UIThread.RunJobs();
            nodes.Select(n => n.Position).ShouldBe(places);
            extra.Position.ShouldBe(new Point(0, 440 + 52));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShrinkToFit_Shows_Actual_Size_And_Shrinks_Like_A_Screenshot()
    {
        var (graph, _) = Relay();
        // Without the narrow layout, which would take over under 520.
        var canvas = new WorkflowCanvas { Graph = graph, NarrowBelowWidth = 0 };
        var window = Show(canvas, 760, 600);
        try
        {
            canvas.Zoom.ShouldBe(1);
            // Centered: (760 − 640) / 2 = 60 from the left.
            canvas.View.WorldToScreen(new Point(0, 0)).X.ShouldBe(60, 1e-6);
            window.Width = 400;
            Dispatcher.UIThread.RunJobs();
            // (400 − 2 × 24) / 640.
            canvas.Zoom.ShouldBe(352.0 / 640, 1e-9);
            // Fit grows it as far as the height allows: (600 − 48) / 440.
            canvas.AutoFit = WorkflowAutoFit.Fit;
            window.Width = 2000;
            Dispatcher.UIThread.RunJobs();
            canvas.Zoom.ShouldBe(552.0 / 440, 1e-9);
            window.Height = 2000;
            Dispatcher.UIThread.RunJobs();
            canvas.Zoom.ShouldBe(canvas.MaxZoom);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void In_Unlimited_Height_The_Canvas_Is_As_Tall_As_The_Fitted_Workflow()
    {
        var (graph, _) = Relay();
        var canvas = new WorkflowCanvas
        {
            Graph = graph, NarrowBelowWidth = 0, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        var window = TestHelpers.Show(new ScrollViewer { Content = canvas });
        window.Width = 400;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var zoom = 352.0 / 640;
            canvas.Bounds.Height.ShouldBe(440 * zoom + 48, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_The_View_Stops_Fitting_Until_FitToContent()
    {
        var (graph, _) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            canvas.Zoom = 1.5;
            canvas.Offset = new Point(100, 50);
            window.Width = 700;
            Dispatcher.UIThread.RunJobs();
            canvas.Zoom.ShouldBe(1.5);
            canvas.View.Origin.ShouldBe(new Point(100, 50));
            canvas.Zoom = 9;
            canvas.Zoom.ShouldBe(2);
            canvas.FitToContent();
            Dispatcher.UIThread.RunJobs();
            canvas.Zoom.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Card_Shows_Its_Steps_Result_Error_Time_And_State_In_Words()
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            var lookup = canvas.CardFor(nodes[1])!;
            lookup.SubtitleText.ShouldBe("Plan, seats and owner");
            lookup.DisplayState.ShouldBe(WorkflowNodeState.Idle);
            lookup.StateMark!.Bounds.Width.ShouldBe(0);

            nodes[1].Duration = TimeSpan.FromMilliseconds(118);
            nodes[1].Result = "Enterprise, 212 seats";
            nodes[1].State = WorkflowNodeState.Done;
            lookup.SubtitleText.ShouldBe("Enterprise, 212 seats");
            lookup.StateMark!.Meta.ShouldBe("118 ms");
            lookup.Classes.ShouldContain(WorkflowNodeCard.DonePseudoClass);
            AutomationProperties.GetName(lookup).ShouldBe("Find customer account, action, Done, 118 ms, Enterprise, 212 seats");

            nodes[4].Error = "Task service timed out";
            nodes[4].State = WorkflowNodeState.Failed;
            canvas.CardFor(nodes[4])!.SubtitleText.ShouldBe("Task service timed out");

            var skipped = canvas.CardFor(nodes[3])!;
            nodes[3].State = WorkflowNodeState.Skipped;
            skipped.SubtitleText.ShouldBe("Skipped");
            skipped.StateOpacity.ShouldBe(0.5);
            skipped.StateShadow.Count.ShouldBe(0);
            canvas.EdgeVisualFor(graph.Edges[2])!.Skipped.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Running_Card_Mixes_The_Accent_Into_Its_Border_And_Fill()
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            var card = canvas.CardFor(nodes[2])!;
            var border = TestHelpers.ColorOf(card.BorderBrush);
            var surface = TestHelpers.ColorOf(card.Background);
            var accent = TestHelpers.ColorOf(card.AccentBrush);
            TestHelpers.ColorOf(card.StateBorderBrush).ShouldBe(border);
            nodes[2].State = WorkflowNodeState.Running;
            TestHelpers.ColorOf(card.StateBorderBrush).ShouldBe(Oklch.MixOklab(border, accent, 0.7));
            TestHelpers.ColorOf(card.StateBackground).ShouldBe(Oklch.MixOklab(surface, accent, 0.04));
            card.Classes.ShouldContain(WorkflowNodeCard.RunningPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Data_Draws_Before_The_Next_Step_Shows_As_Running()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (graph, nodes) = Relay();
        graph.ResetRun();
        nodes[0].State = WorkflowNodeState.Done;
        nodes[1].State = WorkflowNodeState.Running;
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas, level: ArcMotionLevel.Full);
        try
        {
            // What was already done when the canvas appeared is drawn at once.
            canvas.EdgeVisualFor(graph.Edges[0])!.Progress!.Value.ShouldBe(1);
            var branch = canvas.CardFor(nodes[2])!;
            var edge = canvas.EdgeVisualFor(graph.Edges[1])!;

            // The run finishes the lookup and starts the branch at once.
            nodes[1].State = WorkflowNodeState.Done;
            nodes[2].State = WorkflowNodeState.Running;
            edge.Lit.ShouldBeTrue();
            branch.DisplayState.ShouldBe(WorkflowNodeState.Pending);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            // Half the 400ms on the in-out curve (the manual clock may run one frame past).
            edge.Progress!.Value.ShouldBeInRange(0.4, 0.7);
            branch.DisplayState.ShouldBe(WorkflowNodeState.Pending);
            nodes[2].State.ShouldBe(WorkflowNodeState.Running);
            clock.Advance(TimeSpan.FromMilliseconds(260));
            edge.Progress.Value.ShouldBe(1);
            branch.DisplayState.ShouldBe(WorkflowNodeState.Running);

            // A reset clears the drawing at once.
            graph.ResetRun();
            edge.Progress.Value.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Full_Motion_Or_Queueing_The_Run_Shows_As_It_Is()
    {
        foreach (var (level, queue) in new[] { (ArcMotionLevel.Reduced, true), (ArcMotionLevel.Off, true), (ArcMotionLevel.Full, false) })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            var (graph, nodes) = Relay();
            graph.ResetRun();
            var canvas = new WorkflowCanvas { Graph = graph, QueueRunVisuals = queue };
            var window = Show(canvas, level: level);
            try
            {
                nodes[0].State = WorkflowNodeState.Done;
                nodes[1].State = WorkflowNodeState.Running;
                canvas.CardFor(nodes[1])!.DisplayState.ShouldBe(WorkflowNodeState.Running, $"{level} {queue}");
                if (level != ArcMotionLevel.Full)
                {
                    canvas.EdgeVisualFor(graph.Edges[0])!.Progress!.Value.ShouldBe(1, level.ToString());
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void The_Canvas_Names_Itself_Unless_The_App_Does()
    {
        var (graph, _) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            AutomationProperties.GetName(canvas).ShouldBe("Workflow, 6 steps");
            AutomationProperties.SetName(canvas, "Order pipeline");
            graph.Nodes.RemoveAt(5);
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(canvas).ShouldBe("Order pipeline");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Durations_Read_Like_Arcs()
    {
        WorkflowText.Duration(TimeSpan.FromMilliseconds(118)).ShouldBe("118 ms");
        WorkflowText.Duration(TimeSpan.FromMilliseconds(1240)).ShouldBe("1.2 s");
        WorkflowText.Duration(TimeSpan.FromSeconds(65)).ShouldBe("1 min 05 s");
        WorkflowText.CanvasName(1).ShouldBe("Workflow, 1 step");
    }

    [AvaloniaFact]
    public void A_Workflow_That_Outlives_Its_Canvas_Does_Not_Keep_It()
    {
        var (graph, nodes) = Relay();
        var reference = TestHelpers.ShowAndClose(() => new WorkflowCanvas { Graph = graph },
            _ => nodes[1].State = WorkflowNodeState.Running);
        TestHelpers.Collect(reference);
        reference.IsAlive.ShouldBeFalse();
        graph.Nodes.Count.ShouldBe(6);
    }
}
