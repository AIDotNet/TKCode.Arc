using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Workflow;

public class WorkflowConnectTests
{
    /// <summary>
    /// A trigger into a condition with Yes and No outputs, and two steps after it, at fixed places: the view is 1:1, so
    /// screen and canvas coordinates are the same.
    /// </summary>
    private sealed class Sample
    {
        public readonly WorkflowGraph Graph = new();
        public readonly WorkflowNode Trigger = new("trigger", "Refund requested") { HasDefaultInput = false, Position = new Point(170, 0) };
        public readonly WorkflowNode Check = new("check", "Amount over $500?") { Position = new Point(170, 112) };
        public readonly WorkflowNode Approve = new("approve", "Manager approval") { Position = new Point(0, 300) };
        public readonly WorkflowNode Refund = new("refund", "Issue refund") { Position = new Point(420, 300) };
        public readonly WorkflowPort Yes = new(WorkflowPortDirection.Output, "Yes");
        public readonly WorkflowPort No = new(WorkflowPortDirection.Output, "No");
        public readonly WorkflowEdge First;

        public Sample()
        {
            Check.Outputs.Add(Yes);
            Check.Outputs.Add(No);
            foreach (var node in new[] { Trigger, Check, Approve, Refund })
            {
                Graph.Nodes.Add(node);
            }
            First = Graph.Connect(Trigger, Check);
        }
    }

    private static (WorkflowCanvas Canvas, Window Window, Sample S) Show(bool editable = true)
    {
        var sample = new Sample();
        var canvas = new WorkflowCanvas { Graph = sample.Graph, IsEditable = editable, AutoFit = WorkflowAutoFit.None };
        var window = TestHelpers.Show(canvas);
        window.Width = 760;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return (canvas, window, sample);
    }

    private static void Drag(Window window, Point from, Point to, RawInputModifiers modifiers = RawInputModifiers.None, bool release = true)
    {
        window.MouseDown(from, MouseButton.Left, modifiers);
        window.MouseMove(from + (to - from) / 3, RawInputModifiers.LeftMouseButton | modifiers);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton | modifiers);
        if (release)
        {
            window.MouseUp(to, MouseButton.Left, modifiers);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void The_Workflow_Checks_Each_Connection()
    {
        var s = new Sample();
        var g = s.Graph;
        g.CanConnect(s.Check, s.Yes, s.Approve, null, out var reason).ShouldBeTrue();
        reason.ShouldBeNull();
        g.CanConnect(s.Check, s.Yes, s.Check, null, out reason).ShouldBeFalse();
        reason.ShouldBe("Can't connect a step to itself");
        // The trigger takes no input.
        g.CanConnect(s.Check, s.No, s.Trigger, null, out reason).ShouldBeFalse();
        reason.ShouldBe("Connect an output to an input");
        g.CanConnect(s.Trigger, null, s.Check, null, out reason).ShouldBeFalse();
        reason.ShouldBe("Already connected");
        s.Yes.MaxConnections = 1;
        g.Connect(s.Check, s.Yes, s.Approve, null);
        g.CanConnect(s.Check, s.Yes, s.Refund, null, out reason).ShouldBeFalse();
        reason.ShouldBe("This port takes no more connections");
        var money = new WorkflowPort(WorkflowPortDirection.Input) { DataType = "money" };
        s.Refund.Inputs.Add(money);
        s.No.DataType = "text";
        g.CanConnect(s.Check, s.No, s.Refund, money, out reason).ShouldBeFalse();
        reason.ShouldBe("Port types don't match");
        // Back into the start of the path closes a loop.
        g.CanConnect(s.Approve, null, s.Check, null, out reason).ShouldBeFalse();
        reason.ShouldBe("This would create a loop");
        g.AllowCycles = true;
        g.CanConnect(s.Approve, null, s.Check, null, out _).ShouldBeTrue();
        // Ports must belong to their step and face the right way.
        Should.Throw<ArgumentException>(() => new WorkflowEdge(s.Approve, s.Yes, s.Refund, null));
        Should.Throw<ArgumentException>(() => s.Approve.Inputs.Add(new WorkflowPort(WorkflowPortDirection.Output)));
    }

    [AvaloniaFact]
    public void Ports_Spread_Along_The_Card_And_Edges_Leave_From_Them()
    {
        var (canvas, window, s) = Show();
        try
        {
            // Two outputs at a third and two thirds of the bottom; the implicit input at the top center.
            canvas.PortPoint(s.Check, s.Yes, true).ShouldBe(new Point(270, 172));
            canvas.PortPoint(s.Check, s.No, true).ShouldBe(new Point(370, 172));
            canvas.PortPoint(s.Check, null, false).ShouldBe(new Point(320, 112));
            var edge = s.Graph.Connect(s.Check, s.No, s.Refund, null);
            Dispatcher.UIThread.RunJobs();
            var points = canvas.EdgeVisualFor(edge)!.Points;
            points.Start.ShouldBe(new Point(370, 172));
            points.End.ShouldBe(new Point(570, 300));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_From_An_Output_Onto_A_Step_Connects_Them()
    {
        var (canvas, window, s) = Show();
        var edits = new List<WorkflowEditEventArgs>();
        canvas.GraphEdited += (_, e) => edits.Add(e);
        try
        {
            Drag(window, new Point(270, 172), new Point(150, 330));
            var edge = s.Graph.Edges.Last();
            edge.Source.ShouldBe(s.Check);
            edge.SourcePort.ShouldBe(s.Yes);
            edge.Target.ShouldBe(s.Approve);
            edge.TargetPort.ShouldBeNull();
            var edit = edits.ShouldHaveSingleItem();
            edit.Kind.ShouldBe(WorkflowEditKind.Connect);
            edit.Edges.ShouldBe([edge]);
            canvas.LastAnnouncement.ShouldBe("Connected Amount over $500? to Manager approval");
            canvas.IsConnecting.ShouldBeFalse();

            // Within 24px of an input port the line snaps to it.
            Drag(window, new Point(370, 172), new Point(585, 285));
            s.Graph.Edges.Last().Target.ShouldBe(s.Refund);
            s.Graph.History.CanUndo.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Refused_Connections_Are_Not_Made()
    {
        var (canvas, window, s) = Show();
        var dropped = new List<WorkflowConnectionDroppedEventArgs>();
        canvas.ConnectionDroppedOnCanvas += (_, e) => dropped.Add(e);
        canvas.ConnectionRequested += (_, e) =>
        {
            if (e.TargetNode == s.Refund)
            {
                e.Cancel = true;
                e.Reason = "Refunds need an approval first";
            }
        };
        try
        {
            // The app's own rule.
            Drag(window, new Point(270, 172), new Point(570, 330));
            s.Graph.Edges.Count.ShouldBe(1);
            canvas.LastAnnouncement.ShouldBe("Can't connect: Refunds need an approval first");

            // The trigger takes no input: it fades while the line is held, and nothing connects.
            Drag(window, new Point(370, 172), new Point(320, 30), release: false);
            canvas.CardFor(s.Trigger)!.Classes.ShouldContain(WorkflowNodeCard.RefusingPseudoClass);
            window.MouseUp(new Point(320, 30), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            canvas.CardFor(s.Trigger)!.Classes.ShouldNotContain(WorkflowNodeCard.RefusingPseudoClass);
            s.Graph.Edges.Count.ShouldBe(1);

            // Let go on the empty canvas: the app hears where.
            Drag(window, new Point(370, 172), new Point(700, 520));
            var drop = dropped.ShouldHaveSingleItem();
            drop.SourceNode.ShouldBe(s.Check);
            drop.SourcePort.ShouldBe(s.No);
            drop.Position.ShouldBe(new Point(700, 520));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Edge_Can_Be_Picked_Up_From_Its_Input_And_Moved_Or_Dropped()
    {
        var (canvas, window, s) = Show();
        var edits = new List<WorkflowEditEventArgs>();
        canvas.GraphEdited += (_, e) => edits.Add(e);
        try
        {
            // Pick up trigger → check at the check's input and drop it on the approval: it now ends there.
            Drag(window, new Point(320, 112), new Point(150, 330));
            s.Graph.Edges.ShouldNotContain(s.First);
            var moved = s.Graph.Edges.ShouldHaveSingleItem();
            moved.Source.ShouldBe(s.Trigger);
            moved.Target.ShouldBe(s.Approve);
            edits.Select(e => e.Kind).ShouldBe([WorkflowEditKind.Disconnect, WorkflowEditKind.Connect]);
            // One undo puts it back.
            canvas.Undo();
            s.Graph.Edges.ShouldBe([s.First]);

            // Dropped on the empty canvas, it is removed.
            Drag(window, new Point(320, 112), new Point(700, 520));
            s.Graph.Edges.ShouldBeEmpty();
            edits[^1].Kind.ShouldBe(WorkflowEditKind.Disconnect);
            canvas.LastAnnouncement.ShouldBe("Disconnected Refund requested from Amount over $500?");
            canvas.Undo();
            s.Graph.Edges.ShouldBe([s.First]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Edges_Are_Selected_By_Clicking_And_Deleted_With_The_Selection()
    {
        var (canvas, window, s) = Show();
        var edits = new List<WorkflowEditEventArgs>();
        canvas.GraphEdited += (_, e) => edits.Add(e);
        try
        {
            // The edge runs straight down from (320, 60) to (320, 112).
            window.MouseDown(new Point(322, 86), MouseButton.Left);
            window.MouseUp(new Point(322, 86), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            canvas.SelectedEdges.ShouldBe([s.First]);
            canvas.SelectedNodes.ShouldBeEmpty();
            canvas.EdgeVisualFor(s.First)!.Selected.ShouldBeTrue();

            canvas.CardFor(s.Check)!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Delete);
            s.Graph.Edges.ShouldBeEmpty();
            s.Graph.Nodes.Count.ShouldBe(4);
            edits[^1].Kind.ShouldBe(WorkflowEditKind.Delete);
            edits[^1].Edges.ShouldBe([s.First]);
            canvas.LastAnnouncement.ShouldBe("Deleted 1 connection");

            Press(window, PhysicalKey.Z, RawInputModifiers.Control);
            s.Graph.Edges.ShouldBe([s.First]);
            edits[^1].Kind.ShouldBe(WorkflowEditKind.Undo);
            canvas.LastAnnouncement.ShouldBe("Undone");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Undo_And_Redo_Move_And_Delete()
    {
        var (canvas, window, s) = Show();
        try
        {
            Drag(window, new Point(150, 330), new Point(194, 330));
            s.Approve.Position.ShouldBe(new Point(44, 308));
            canvas.CardFor(s.Approve)!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Z, RawInputModifiers.Control);
            s.Approve.Position.ShouldBe(new Point(0, 300));
            s.Graph.History.CanRedo.ShouldBeTrue();
            Press(window, PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
            s.Approve.Position.ShouldBe(new Point(44, 308));

            // Deleting a step takes its edges; undo brings both back in their places.
            canvas.Select([s.Check]);
            canvas.DeleteSelection();
            s.Graph.Nodes.ShouldNotContain(s.Check);
            s.Graph.Edges.ShouldBeEmpty();
            canvas.Undo();
            s.Graph.Nodes.IndexOf(s.Check).ShouldBe(1);
            s.Graph.Edges.ShouldBe([s.First]);
            canvas.Redo();
            s.Graph.Nodes.ShouldNotContain(s.Check);
            canvas.Undo();

            // Code that changes the workflow directly is not recorded.
            s.Graph.History.Clear();
            s.Graph.Connect(s.Check, s.Yes, s.Approve, null);
            s.Graph.History.CanUndo.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Keyboard_Connects_A_Chosen_Output_To_A_Chosen_Step()
    {
        var (canvas, window, s) = Show();
        try
        {
            canvas.CardFor(s.Check)!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.C);
            canvas.IsConnecting.ShouldBeTrue();
            canvas.LastAnnouncement.ShouldBe("Output Yes: left and right choose an output, Enter continues");
            Press(window, PhysicalKey.ArrowRight);
            canvas.LastAnnouncement!.ShouldStartWith("Output No");
            Press(window, PhysicalKey.Enter);
            // The first step after the condition in reading order, then the next one.
            canvas.LastAnnouncement!.ShouldStartWith("Connecting from Amount over $500? to Manager approval");
            Press(window, PhysicalKey.ArrowRight);
            canvas.LastAnnouncement.ShouldBe("Issue refund");
            Press(window, PhysicalKey.Enter);
            var edge = s.Graph.Edges.Last();
            (edge.Source, edge.SourcePort, edge.Target).ShouldBe((s.Check, s.No, s.Refund));
            canvas.IsConnecting.ShouldBeFalse();

            // Escape leaves the mode without connecting.
            Press(window, PhysicalKey.C);
            Press(window, PhysicalKey.Enter);
            Press(window, PhysicalKey.Escape);
            canvas.IsConnecting.ShouldBeFalse();
            canvas.LastAnnouncement.ShouldBe("Connection cancelled");
            s.Graph.Edges.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Connecting_By_Hand_Keeps_The_Cards_Where_They_Are()
    {
        // A workflow the canvas arranged: the loose step sits beside the start, in the first rank.
        var graph = new WorkflowGraph();
        var start = new WorkflowNode("start", "Start");
        var check = new WorkflowNode("check", "Check");
        var loose = new WorkflowNode("loose", "Loose");
        foreach (var node in new[] { start, check, loose })
        {
            graph.Nodes.Add(node);
        }
        graph.Connect(start, check);
        var canvas = new WorkflowCanvas { Graph = graph, IsEditable = true, AutoFit = WorkflowAutoFit.None };
        var window = TestHelpers.Show(canvas);
        window.Width = 760;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var places = graph.Nodes.Select(n => n.Position).ToList();
            Drag(window, canvas.PortPoint(check, null, true), canvas.PortPoint(loose, null, false));
            graph.Edges.Count.ShouldBe(2);
            graph.Nodes.Select(n => n.Position).ShouldBe(places);
            canvas.Undo();
            graph.Edges.Count.ShouldBe(1);
            graph.Nodes.Select(n => n.Position).ShouldBe(places);

            // Your code connecting it arranges the workflow again: the step moves below the check.
            graph.Connect(check, loose);
            Dispatcher.UIThread.RunJobs();
            loose.Position.Y.ShouldBeGreaterThan(check.Position.Y);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Canvases_Do_Not_Connect()
    {
        var (canvas, window, s) = Show(editable: false);
        try
        {
            Drag(window, new Point(270, 172), new Point(150, 330));
            s.Graph.Edges.Count.ShouldBe(1);
            canvas.CardFor(s.Check)!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.C);
            canvas.IsConnecting.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
