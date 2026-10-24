using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Workflow;

public class WorkflowCanvasViewTests
{
    private static (WorkflowGraph Graph, WorkflowNode[] Nodes) Relay()
    {
        var graph = new WorkflowGraph();
        WorkflowNode[] nodes =
        [
            new("trigger", "Order placed"),
            new("lookup", "Find customer account"),
            new("branch", "Amount over $1,000"),
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

    private static Window Show(Control content, double width = 760, double height = 600, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(content, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static FocusManager? Focus(Window window) => window.FocusManager as FocusManager;

    [AvaloniaFact]
    public void A_Narrow_Canvas_Uses_Arcs_Phone_Layout_And_Widening_Restores_The_Wide_One()
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        // 344 + 2 × 24 padding: the narrow workflow at actual size.
        var window = Show(canvas, 392);
        try
        {
            canvas.IsNarrow.ShouldBeTrue();
            // hero-relay.tsx LAYOUTS.narrow.
            nodes.Select(n => n.Position).ShouldBe([
                new Point(0, 0), new Point(0, 78), new Point(0, 156),
                new Point(0, 244), new Point(118, 244), new Point(236, 244)
            ]);
            canvas.CardFor(nodes[0])!.Bounds.Size.ShouldBe(new Size(344, 52));
            canvas.CardFor(nodes[3])!.Bounds.Size.ShouldBe(new Size(108, 72));
            canvas.Zoom.ShouldBe(1);

            window.Width = 760;
            Dispatcher.UIThread.RunJobs();
            canvas.IsNarrow.ShouldBeFalse();
            nodes[0].Position.ShouldBe(new Point(170, 0));
            nodes[3].Position.ShouldBe(new Point(0, 356));
            canvas.CardFor(nodes[0])!.Bounds.Size.ShouldBe(new Size(300, 60));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrowing_Keeps_Steps_Your_Code_Placed()
    {
        var (graph, nodes) = Relay();
        for (var i = 0; i < nodes.Length; i++)
        {
            nodes[i].Position = new Point(i * 40, i * 100);
        }
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas, 400);
        try
        {
            canvas.IsNarrow.ShouldBeTrue();
            nodes.Select(n => n.Position).ShouldBe(Enumerable.Range(0, nodes.Length).Select(i => new Point(i * 40, i * 100)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Wheel_Pans_And_Zooms_About_The_Pointer()
    {
        var (graph, _) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            var before = canvas.View;
            // One notch down moves the content up 50px.
            window.MouseWheel(new Point(300, 300), new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            canvas.View.Origin.Y.ShouldBe(before.Origin.Y + 50 / before.Zoom, 1e-6);

            var anchor = new Point(200, 150);
            var under = canvas.View.ScreenToWorld(anchor);
            window.MouseWheel(anchor, new Vector(0, 1), RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            canvas.Zoom.ShouldBe(before.Zoom * Math.Exp(0.22), 1e-6);
            canvas.View.WorldToScreen(under).X.ShouldBe(anchor.X, 1e-6);
            canvas.View.WorldToScreen(under).Y.ShouldBe(anchor.Y, 1e-6);

            // The view no longer fits itself after the person moved it.
            var view = canvas.View;
            window.Width = 700;
            Dispatcher.UIThread.RunJobs();
            canvas.View.ShouldBe(view);

            canvas.IsPanZoomEnabled = false;
            window.MouseWheel(new Point(300, 300), new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            canvas.View.ShouldBe(view);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Pans_The_View()
    {
        var (graph, _) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            var before = canvas.View;
            var start = new Point(80, 500);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(3, 0), RawInputModifiers.LeftMouseButton);
            canvas.IsPanning.ShouldBeFalse();
            window.MouseMove(start + new Point(40, 20), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            canvas.IsPanning.ShouldBeTrue();
            canvas.Classes.ShouldContain(WorkflowCanvas.PanningPseudoClass);
            window.MouseUp(start + new Point(40, 20), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            canvas.IsPanning.ShouldBeFalse();
            var moved = canvas.View.WorldToScreen(before.ScreenToWorld(start));
            moved.X.ShouldBe(start.X + 40, 1e-6);
            moved.Y.ShouldBe(start.Y + 20, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Steps_And_Alt_Follows_The_Connections()
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        var activated = new List<WorkflowNode>();
        canvas.NodeActivated += (_, e) => activated.Add(e.Node);
        try
        {
            canvas.CardFor(nodes[1])!.Focus(NavigationMethod.Tab).ShouldBeTrue();
            Press(window, PhysicalKey.ArrowDown);
            canvas.CardFor(nodes[2])!.IsFocused.ShouldBeTrue();
            // The next steps of the branch: the one straight below first, then across them, wrapping.
            Press(window, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            canvas.CardFor(nodes[4])!.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
            canvas.CardFor(nodes[5])!.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
            canvas.CardFor(nodes[3])!.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            canvas.CardFor(nodes[2])!.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Home);
            canvas.CardFor(nodes[0])!.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.End);
            canvas.CardFor(nodes[5])!.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowLeft);
            canvas.CardFor(nodes[4])!.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Enter);
            activated.ShouldBe([nodes[4]]);
            canvas.CardFor(nodes[4])!.Classes.ShouldContain(":focus-visible");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Context_Menu_Request_And_A_Double_Click_Come_From_The_Card()
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        var requested = new List<object?>();
        var activated = new List<WorkflowNode>();
        canvas.ContextRequested += (_, e) => requested.Add(e.Source);
        canvas.NodeActivated += (_, e) => activated.Add(e.Node);
        try
        {
            var card = canvas.CardFor(nodes[1])!;
            card.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.F10, RawInputModifiers.Shift);
            requested.ShouldHaveSingleItem().ShouldBe(card);

            var center = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(center, MouseButton.Left);
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            activated.ShouldBe([nodes[1]]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Zoom_Fit_And_Reset()
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            canvas.CardFor(nodes[2])!.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            var center = canvas.View.WorldToScreen(canvas.DisplayRect(nodes[2]).Center);
            Press(window, PhysicalKey.Equal);
            canvas.Zoom.ShouldBe(1.25, 1e-9);
            // Zooming keeps the focused step where it was.
            var after = canvas.View.WorldToScreen(canvas.DisplayRect(nodes[2]).Center);
            after.X.ShouldBe(center.X, 1e-6);
            after.Y.ShouldBe(center.Y, 1e-6);
            Press(window, PhysicalKey.Minus);
            Press(window, PhysicalKey.Minus);
            canvas.Zoom.ShouldBe(0.8, 1e-9);
            Press(window, PhysicalKey.Digit0, RawInputModifiers.Control);
            canvas.Zoom.ShouldBe(1, 1e-9);
            canvas.ZoomTo(0.5);
            Press(window, PhysicalKey.Digit1, RawInputModifiers.Shift);
            canvas.Zoom.ShouldBe(1, 1e-9);
            canvas.View.WorldToScreen(new Point(0, 0)).X.ShouldBe(60, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Animated_Zooms_And_Arranging_Ease_Out_To_The_End()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas, level: ArcMotionLevel.Full);
        try
        {
            // Zoomed in and panned far, then back to 100%: follow the view's top-left corner frame by frame.
            canvas.ZoomTo(2, new Point(700, 560), animate: false);
            var corner = canvas.View.ScreenToWorld(new Point(0, 0));
            var steps = Follow(clock, () => canvas.View.WorldToScreen(corner), () => canvas.ZoomTo(1));
            canvas.IsViewAnimating.ShouldBeFalse();
            canvas.Zoom.ShouldBe(1, 1e-9);
            EasesOutToTheEnd(steps);

            // Arranging scattered cards ends the same way.
            nodes[0].Position = new Point(600, 600);
            Dispatcher.UIThread.RunJobs();
            var moves = Follow(clock, () => canvas.DisplayPosition(nodes[0]), () => canvas.ArrangeNodes());
            canvas.DisplayPosition(nodes[0]).ShouldBe(new Point(170, 0));
            EasesOutToTheEnd(moves);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>How far <paramref name="point"/> moves in each of the 60 frames (one second) after <paramref name="start"/>.</summary>
    private static List<double> Follow(ManualFrameClock clock, Func<Point> point, Action start)
    {
        var at = point();
        start();
        var steps = new List<double>();
        for (var i = 0; i < 60; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60), TimeSpan.FromMilliseconds(1000.0 / 60));
            var next = point();
            steps.Add(Math.Sqrt((next.X - at.X) * (next.X - at.X) + (next.Y - at.Y) * (next.Y - at.Y)));
            at = next;
        }
        return steps;
    }

    /// <summary>
    /// Over within 400ms, slowing down to the end: no frame after the fastest moves farther than the one before it. (A
    /// spring crawled by fractions of a pixel for a third of a second, then jumped the rest.)
    /// </summary>
    private static void EasesOutToTheEnd(List<double> steps)
    {
        steps.Skip(24).ShouldAllBe(step => step == 0);
        for (var i = steps.IndexOf(steps.Max()) + 1; i < steps.Count; i++)
        {
            steps[i].ShouldBeLessThanOrEqualTo(steps[i - 1] + 1e-9);
        }
    }

    [AvaloniaFact]
    public void ScrollIntoView_Pans_Only_As_Far_As_Needed()
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas, 760, 300);
        try
        {
            canvas.ZoomTo(1.5, new Point(0, 0), animate: false);
            var save = canvas.DisplayRect(nodes[5]);
            canvas.View.WorldToScreen(save).Bottom.ShouldBeGreaterThan(300);
            canvas.ScrollIntoView(nodes[5], animate: false);
            var shown = canvas.View.WorldToScreen(save);
            shown.Bottom.ShouldBe(300 - 24, 1e-6);
            shown.Right.ShouldBeLessThanOrEqualTo(760 - 24 + 1e-6);
            var view = canvas.View;
            canvas.ScrollIntoView(nodes[5], animate: false);
            canvas.View.ShouldBe(view);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Zoom_Bar_Zooms_Its_Canvas_And_Shows_The_Zoom()
    {
        var (graph, _) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var bar = new WorkflowZoomControls { Target = canvas };
        var window = Show(new Panel { Children = { canvas, bar } });
        try
        {
            bar.ReadoutText.ShouldBe("100%");
            Click(bar, WorkflowZoomControls.ZoomInPart);
            canvas.Zoom.ShouldBe(1.25, 1e-9);
            bar.ReadoutText.ShouldBe("125%");
            Click(bar, WorkflowZoomControls.ResetPart);
            canvas.Zoom.ShouldBe(1, 1e-9);
            canvas.ZoomTo(canvas.MinZoom);
            TestHelpers.Descendant<WorkflowZoomButton>(bar, WorkflowZoomControls.ZoomOutPart).IsEnabled.ShouldBeFalse();
            Click(bar, WorkflowZoomControls.FitPart);
            canvas.Zoom.ShouldBe(1, 1e-9);
            Avalonia.Automation.AutomationProperties.GetName(TestHelpers.Descendant<WorkflowZoomButton>(bar, WorkflowZoomControls.ZoomInPart))
                .ShouldBe("Zoom in");
        }
        finally
        {
            window.Close();
        }
    }

    private static void Click(WorkflowZoomControls bar, string part)
    {
        TestHelpers.Descendant<WorkflowZoomButton>(bar, part).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Left_To_Right_Set_Before_The_Canvas_Shows_Uses_The_Card_Sizes()
    {
        var (graph, nodes) = Relay();
        // Set after the workflow and before the canvas is in a window, when the cards have no size yet.
        var canvas = new WorkflowCanvas { Graph = graph };
        canvas.LayoutDirection = WorkflowLayoutDirection.LeftToRight;
        // Wide from the start: Show's window opens 400 wide, and the narrow layout would arrange the steps again.
        var window = new Window { Content = canvas, Width = 1240, Height = 520 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            // Ranks run across, 52 apart (72 after the fan-out), each after the 300 wide card before it.
            nodes[1].Position.ShouldBe(new Point(nodes[0].Position.X + 352, nodes[0].Position.Y));
            nodes[2].Position.ShouldBe(new Point(nodes[1].Position.X + 352, nodes[1].Position.Y));
            nodes[3].Position.X.ShouldBe(nodes[2].Position.X + 372);

            // Switching back arranges top to bottom again.
            canvas.LayoutDirection = WorkflowLayoutDirection.TopToBottom;
            Dispatcher.UIThread.RunJobs();
            nodes[1].Position.ShouldBe(new Point(170, 112));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Edge_Styles_Shape_The_Edges()
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph, EdgeStyle = WorkflowEdgeStyle.Straight };
        var window = Show(canvas);
        try
        {
            // Branch (170, 224, 300 × 60) to the first action (0, 356, 200 × 84).
            var edge = canvas.EdgeVisualFor(graph.Edges[2])!;
            edge.PolylinePoints.ShouldBe([new Point(320, 284), new Point(100, 356)]);

            canvas.EdgeStyle = WorkflowEdgeStyle.Step;
            Dispatcher.UIThread.RunJobs();
            var step = edge.PolylinePoints!;
            step[0].ShouldBe(new Point(320, 284));
            step[^1].ShouldBe(new Point(100, 356));
            // It turns halfway down, with rounded corners of 8.
            step.ShouldContain(p => Math.Abs(p.Y - 320) < 1e-9 && p.X < 320 - 7 && p.X > 100 + 7);
            step.ShouldAllBe(p => p.X >= 100 - 1e-9 && p.X <= 320 + 1e-9 && p.Y >= 284 - 1e-9 && p.Y <= 356 + 1e-9);

            canvas.EdgeStyle = WorkflowEdgeStyle.Bezier;
            Dispatcher.UIThread.RunJobs();
            edge.PolylinePoints.ShouldBeNull();
            edge.Points.C1.ShouldBe(new Point(320, 284 + 36));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arranging_Glides_The_Cards_And_The_Edges_Follow()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas, level: ArcMotionLevel.Full);
        try
        {
            nodes[0].Position = new Point(600, 600);
            Dispatcher.UIThread.RunJobs();
            canvas.ArrangeNodes();
            // The step is already where it ends; its card is on its way.
            nodes[0].Position.ShouldBe(new Point(170, 0));
            canvas.DisplayPosition(nodes[0]).ShouldBe(new Point(600, 600));
            clock.Advance(TimeSpan.FromMilliseconds(150));
            var mid = canvas.DisplayPosition(nodes[0]);
            mid.X.ShouldBeInRange(170.0, 599.0);
            canvas.EdgeVisualFor(graph.Edges[0])!.Points.Start.X.ShouldBe(mid.X + 150, 1e-6);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            canvas.DisplayPosition(nodes[0]).ShouldBe(new Point(170, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Hear_Failures_And_The_End_Of_A_Run()
    {
        var (graph, nodes) = Relay();
        graph.ResetRun();
        var canvas = new WorkflowCanvas { Graph = graph };
        var window = Show(canvas);
        try
        {
            ControlAutomationPeer.CreatePeerForElement(canvas).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            ControlAutomationPeer.CreatePeerForElement(canvas.CardFor(nodes[0])!).GetAutomationControlType()
                .ShouldBe(AutomationControlType.ListItem);
            Avalonia.Automation.AutomationProperties.GetHelpText(canvas).ShouldNotBeNullOrEmpty();

            foreach (var node in nodes.Take(3))
            {
                node.State = WorkflowNodeState.Done;
            }
            nodes[3].State = WorkflowNodeState.Done;
            nodes[4].Error = "Task service timed out";
            nodes[4].State = WorkflowNodeState.Failed;
            canvas.LastAnnouncement.ShouldBe("Create onboarding task failed: Task service timed out");
            nodes[5].State = WorkflowNodeState.Skipped;
            canvas.LastAnnouncement.ShouldBe("Run finished: 4 done, 1 failed, 1 skipped");
        }
        finally
        {
            window.Close();
        }
    }
}
