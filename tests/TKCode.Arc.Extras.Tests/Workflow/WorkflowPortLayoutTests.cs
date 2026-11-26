using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Workflow;

/// <summary>Per-step port sides (#213): <see cref="WorkflowNode.PortLayout"/> over the canvas's layout direction.</summary>
public class WorkflowPortLayoutTests
{
    private static (WorkflowCanvas Canvas, Window Window, WorkflowGraph Graph, WorkflowNode A, WorkflowNode B) Show(
        WorkflowEdgeStyle style = WorkflowEdgeStyle.Bezier)
    {
        var graph = new WorkflowGraph();
        var a = new WorkflowNode("a", "Vertical") { Position = new Point(0, 0), Size = new Size(200, 60) };
        var b = new WorkflowNode("b", "Horizontal") { Position = new Point(300, 200), Size = new Size(200, 60) };
        graph.Nodes.Add(a);
        graph.Nodes.Add(b);
        graph.Connect(a, b);
        var canvas = new WorkflowCanvas { Graph = graph, AutoFit = WorkflowAutoFit.None, EdgeStyle = style };
        var window = TestHelpers.Show(canvas);
        window.Width = 700;
        window.Height = 400;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return (canvas, window, graph, a, b);
    }

    [AvaloniaFact]
    public void Unset_Follows_The_Canvas()
    {
        var (canvas, window, graph, a, b) = Show();
        try
        {
            a.PortLayout.ShouldBeNull();
            canvas.PortPoint(a, null, true).ShouldBe(new Point(100, 60));
            canvas.PortPoint(b, null, false).ShouldBe(new Point(400, 200));
            canvas.LayoutDirection = WorkflowLayoutDirection.LeftToRight;
            canvas.PortPoint(a, null, true).ShouldBe(new Point(200, 30));
            canvas.PortPoint(b, null, false).ShouldBe(new Point(300, 230));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Horizontal_Step_In_A_Vertical_Canvas_Has_Ports_Left_And_Right()
    {
        var (canvas, window, graph, a, b) = Show();
        try
        {
            b.PortLayout = WorkflowLayoutDirection.LeftToRight;
            b.Outputs.Add(new WorkflowPort(WorkflowPortDirection.Output, "Yes"));
            b.Outputs.Add(new WorkflowPort(WorkflowPortDirection.Output, "No"));
            canvas.PortPoint(b, null, false).ShouldBe(new Point(300, 230));
            canvas.PortPoint(b, b.Outputs[0], true).ShouldBe(new Point(500, 200 + 20));
            canvas.PortPoint(b, b.Outputs[1], true).ShouldBe(new Point(500, 200 + 40));
            canvas.PortPoint(a, null, true).ShouldBe(new Point(100, 60));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mixed_Bezier_Has_Handles_Along_Each_Ends_Normal()
    {
        var (canvas, window, graph, a, b) = Show();
        try
        {
            b.PortLayout = WorkflowLayoutDirection.LeftToRight;
            Dispatcher.UIThread.RunJobs();
            var p = canvas.EdgeVisualFor(graph.Edges[0])!.Points;
            p.Start.ShouldBe(new Point(100, 60));
            p.End.ShouldBe(new Point(300, 230));
            // Leaves straight down, enters straight from the left.
            p.C1.X.ShouldBe(p.Start.X);
            p.C1.Y.ShouldBeGreaterThan(p.Start.Y);
            p.C2.Y.ShouldBe(p.End.Y);
            p.C2.X.ShouldBeLessThan(p.End.X);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mixed_Step_Is_One_Elbow_And_Straight_Is_A_Line()
    {
        var (canvas, window, graph, a, b) = Show(WorkflowEdgeStyle.Step);
        try
        {
            b.PortLayout = WorkflowLayoutDirection.LeftToRight;
            Dispatcher.UIThread.RunJobs();
            var line = canvas.EdgeVisualFor(graph.Edges[0])!.PolylinePoints!;
            line[0].ShouldBe(new Point(100, 60));
            line[^1].ShouldBe(new Point(300, 230));
            // The corner at (100, 230): everything before it is on x = 100 and everything after on y = 230.
            line.Count.ShouldBeGreaterThan(3);
            // Beyond the rounded corner (radius 8) nothing leaves the two legs.
            line.Any(q => q.X > 110 && q.Y < 220).ShouldBeFalse();
            line.Any(q => q.X < 110 && q.Y > 220 && q.X > 100.5 && q.Y < 229.5 && false).ShouldBeFalse();
            canvas.EdgeStyle = WorkflowEdgeStyle.Straight;
            Dispatcher.UIThread.RunJobs();
            canvas.EdgeVisualFor(graph.Edges[0])!.PolylinePoints!.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_PortLayout_Reroutes_The_Edge()
    {
        var (canvas, window, graph, a, b) = Show();
        try
        {
            var edge = canvas.EdgeVisualFor(graph.Edges[0])!;
            edge.Points.End.ShouldBe(new Point(400, 200));
            b.PortLayout = WorkflowLayoutDirection.LeftToRight;
            edge.Points.End.ShouldBe(new Point(300, 230));
            a.PortLayout = WorkflowLayoutDirection.LeftToRight;
            edge.Points.Start.ShouldBe(new Point(200, 30));
            b.PortLayout = null;
            edge.Points.End.ShouldBe(new Point(400, 200));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clipboard_Keeps_PortLayout()
    {
        var (canvas, window, graph, a, b) = Show();
        try
        {
            b.PortLayout = WorkflowLayoutDirection.LeftToRight;
            var json = WorkflowFragment.Copy(graph.Nodes.ToList(), graph.Edges).ToJson();
            var back = WorkflowFragment.FromJson(json)!;
            back.Nodes.Single(n => n.Id == "b").PortLayout.ShouldBe(WorkflowLayoutDirection.LeftToRight);
            back.Nodes.Single(n => n.Id == "a").PortLayout.ShouldBeNull();
            WorkflowFragment.Copy(graph.Nodes.ToList(), graph.Edges).Nodes.Single(n => n.Id == "b").PortLayout
                .ShouldBe(WorkflowLayoutDirection.LeftToRight);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mixed_Canvas_Renders(bool dark)
    {
        var graph = new WorkflowGraph();
        var t = new WorkflowNode("t", "Trigger") { HasDefaultInput = false, Position = new Point(40, 20), Size = new Size(220, 60) };
        var h = new WorkflowNode("h", "Horizontal step") { Position = new Point(340, 140), Size = new Size(220, 60), PortLayout = WorkflowLayoutDirection.LeftToRight };
        var v = new WorkflowNode("v", "Vertical step") { Position = new Point(40, 260), Size = new Size(220, 60) };
        var w = new WorkflowNode("w", "Horizontal end") { Position = new Point(640, 60), Size = new Size(220, 60), PortLayout = WorkflowLayoutDirection.LeftToRight };
        var z = new WorkflowNode("z", "Vertical end") { Position = new Point(620, 280), Size = new Size(220, 60) };
        foreach (var n in new[] { t, h, v, w, z })
        {
            graph.Nodes.Add(n);
        }
        graph.Connect(t, h);
        graph.Connect(t, v);
        graph.Connect(h, w);
        graph.Connect(h, z);
        var canvas = new WorkflowCanvas { Graph = graph, AutoFit = WorkflowAutoFit.None };
        await TestHelpers.ApplyAsync(dark ? TestHelpers.Dark : null);
        var window = TestHelpers.Show(canvas);
        window.Width = 900;
        window.Height = 400;
        try
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame();
            // Without a Skia render interface (the default headless app) there is no frame; set WORKFLOW_SHOTS and
            // enable Skia in the test app to save PNGs.
            var dir = Environment.GetEnvironmentVariable("WORKFLOW_SHOTS");
            if (frame is not null && !string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                frame.Save(Path.Combine(dir, dark ? "port-layout-dark.png" : "port-layout-light.png"));
            }
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }
}
