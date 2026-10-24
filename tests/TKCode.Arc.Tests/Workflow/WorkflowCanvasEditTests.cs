using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Workflow;

public class WorkflowCanvasEditTests
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

    private static (WorkflowCanvas Canvas, Window Window, WorkflowNode[] Nodes, WorkflowGraph Graph) Show(
        bool editable = true, double width = 760, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var (graph, nodes) = Relay();
        var canvas = new WorkflowCanvas { Graph = graph, IsEditable = editable };
        var window = TestHelpers.Show(canvas, level);
        window.Width = width;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return (canvas, window, nodes, graph);
    }

    /// <summary>A card's center in window coordinates (the canvas fills the window).</summary>
    private static Point Center(WorkflowCanvas canvas, WorkflowNode node) => canvas.View.WorldToScreen(canvas.DisplayRect(node).Center);

    private static void Click(Window window, Point point, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.MouseDown(point, MouseButton.Left, modifiers);
        window.MouseUp(point, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Drag(Window window, Point from, Vector by, RawInputModifiers modifiers = RawInputModifiers.None, bool release = true)
    {
        window.MouseDown(from, MouseButton.Left, modifiers);
        window.MouseMove(from + new Point(by.X / 3, by.Y / 3), RawInputModifiers.LeftMouseButton | modifiers);
        window.MouseMove(from + by, RawInputModifiers.LeftMouseButton | modifiers);
        if (release)
        {
            window.MouseUp(from + by, MouseButton.Left, modifiers);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Clicks_Select_Ctrl_Toggles_Shift_Adds_And_The_Empty_Canvas_Clears()
    {
        var (canvas, window, nodes, _) = Show(editable: false);
        var changes = new List<WorkflowSelectionChangedEventArgs>();
        canvas.SelectionChanged += (_, e) => changes.Add(e);
        try
        {
            Click(window, Center(canvas, nodes[1]));
            canvas.SelectedNodes.ShouldBe([nodes[1]]);
            Click(window, Center(canvas, nodes[2]), RawInputModifiers.Control);
            canvas.SelectedNodes.ShouldBe([nodes[1], nodes[2]]);
            Click(window, Center(canvas, nodes[1]), RawInputModifiers.Control);
            canvas.SelectedNodes.ShouldBe([nodes[2]]);
            Click(window, Center(canvas, nodes[0]), RawInputModifiers.Shift);
            canvas.SelectedNodes.ShouldBe([nodes[2], nodes[0]]);
            Click(window, new Point(20, 20));
            canvas.SelectedNodes.ShouldBeEmpty();
            changes.Count.ShouldBe(5);
            changes[^1].Removed.ShouldBe([nodes[2], nodes[0]], ignoreOrder: true);

            var card = canvas.CardFor(nodes[2])!;
            canvas.Select([nodes[2]]);
            card.Classes.ShouldContain(WorkflowNodeCard.SelectedPseudoClass);
            TestHelpers.ColorOf(card.StateBorderBrush).ShouldBe(TestHelpers.ColorOf(card.AccentBrush));
            AutomationProperties.GetName(card)!.ShouldEndWith(", selected");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Moves_The_Selection_And_Snaps_It_To_The_Grid()
    {
        var (canvas, window, nodes, _) = Show();
        var edits = new List<WorkflowEditEventArgs>();
        canvas.GraphEdited += (_, e) => edits.Add(e);
        try
        {
            canvas.Select([nodes[1], nodes[2]]);
            Drag(window, Center(canvas, nodes[1]), new Vector(37, 15));
            // (170, 112) + (37, 15) = (207, 127) → the nearest multiples of 22; the branch moves with it.
            nodes[1].Position.ShouldBe(new Point(198, 132));
            nodes[2].Position.ShouldBe(new Point(198, 242));
            nodes[0].Position.ShouldBe(new Point(170, 0));
            var edit = edits.ShouldHaveSingleItem();
            edit.Kind.ShouldBe(WorkflowEditKind.Move);
            edit.Nodes.ShouldBe([nodes[1], nodes[2]], ignoreOrder: true);
            canvas.LastAnnouncement.ShouldBe("Moved 2 steps");
            // The edge follows the card.
            canvas.EdgeVisualFor(canvas.Graph!.Edges[0])!.Points.End.ShouldBe(new Point(198 + 150, 132));

            // Holding Alt skips the grid.
            Drag(window, Center(canvas, nodes[0]), new Vector(5, 3), RawInputModifiers.Alt);
            nodes[0].Position.ShouldBe(new Point(175, 3));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Locked_Steps_Stay_And_A_Read_Only_Canvas_Pans_Instead()
    {
        var (canvas, window, nodes, _) = Show();
        try
        {
            nodes[1].IsLocked = true;
            canvas.Select([nodes[1], nodes[2]]);
            Drag(window, Center(canvas, nodes[2]), new Vector(44, 0));
            nodes[1].Position.ShouldBe(new Point(170, 112));
            // (214, 224) snaps to (220, 220).
            nodes[2].Position.ShouldBe(new Point(220, 220));
            canvas.CardFor(nodes[1])!.Classes.ShouldContain(WorkflowNodeCard.LockedPseudoClass);

            // Dragging a locked card pans the view.
            var view = canvas.View;
            Drag(window, Center(canvas, nodes[1]), new Vector(30, 0));
            nodes[1].Position.ShouldBe(new Point(170, 112));
            canvas.View.Origin.X.ShouldBe(view.Origin.X - 30, 1e-6);

            canvas.IsEditable = false;
            view = canvas.View;
            Drag(window, Center(canvas, nodes[2]), new Vector(0, 40));
            nodes[2].Position.ShouldBe(new Point(220, 220));
            canvas.View.Origin.Y.ShouldBe(view.Origin.Y - 40, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_Dragging_The_Empty_Canvas_Selects_What_It_Touches()
    {
        var (canvas, window, nodes, _) = Show();
        try
        {
            canvas.Select([nodes[0]]);
            // The canvas shows the workflow from (60, 80); the actions sit at y 356–440.
            Drag(window, new Point(40, 420), new Vector(660, 60), RawInputModifiers.Shift);
            canvas.SelectedNodes.ShouldBe([nodes[0], nodes[3], nodes[4], nodes[5]]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Puts_A_Dragged_Step_Back()
    {
        var (canvas, window, nodes, _) = Show();
        var edits = 0;
        canvas.GraphEdited += (_, _) => edits++;
        try
        {
            var start = Center(canvas, nodes[1]);
            Drag(window, start, new Vector(80, 40), release: false);
            canvas.IsMoving.ShouldBeTrue();
            canvas.DisplayPosition(nodes[1]).ShouldBe(new Point(250, 152));
            Press(window, PhysicalKey.Escape);
            canvas.IsMoving.ShouldBeFalse();
            window.MouseUp(start + new Point(80, 40), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            nodes[1].Position.ShouldBe(new Point(170, 112));
            canvas.DisplayPosition(nodes[1]).ShouldBe(new Point(170, 112));
            edits.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Keyboard_Selects_Moves_And_Deletes()
    {
        var (canvas, window, nodes, graph) = Show();
        var edits = new List<WorkflowEditEventArgs>();
        canvas.GraphEdited += (_, e) => edits.Add(e);
        try
        {
            canvas.CardFor(nodes[1])!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            canvas.SelectedNodes.ShouldBe([nodes[1]]);
            Press(window, PhysicalKey.Space);
            canvas.SelectedNodes.ShouldBeEmpty();
            Press(window, PhysicalKey.A, RawInputModifiers.Control);
            canvas.SelectedNodes.Count.ShouldBe(6);
            Press(window, PhysicalKey.Escape);
            canvas.SelectedNodes.ShouldBeEmpty();

            // Move mode: two grid steps right, five down, placed with Enter.
            Press(window, PhysicalKey.M);
            canvas.LastAnnouncement.ShouldBe("Moving: arrow keys move, Enter places, Escape cancels");
            Press(window, PhysicalKey.ArrowRight);
            Press(window, PhysicalKey.ArrowRight);
            Press(window, PhysicalKey.ArrowDown, RawInputModifiers.Shift);
            canvas.CardFor(nodes[1])!.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Enter);
            nodes[1].Position.ShouldBe(new Point(170 + 44, 112 + 110));
            edits.ShouldHaveSingleItem().Nodes.ShouldBe([nodes[1]]);
            canvas.LastAnnouncement.ShouldBe("Moved 1 step");

            // Escape takes a keyboard move back.
            Press(window, PhysicalKey.M);
            Press(window, PhysicalKey.ArrowLeft);
            Press(window, PhysicalKey.Escape);
            nodes[1].Position.ShouldBe(new Point(214, 222));
            canvas.LastAnnouncement.ShouldBe("Move cancelled");

            // Delete removes the selection but not a locked step; focus moves on in reading order.
            nodes[5].IsLocked = true;
            canvas.Select([nodes[3], nodes[4], nodes[5]]);
            canvas.CardFor(nodes[4])!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Delete);
            graph.Nodes.ShouldNotContain(nodes[3]);
            graph.Nodes.ShouldNotContain(nodes[4]);
            graph.Nodes.ShouldContain(nodes[5]);
            graph.Edges.Count.ShouldBe(3);
            canvas.SelectedNodes.ShouldBe([nodes[5]]);
            canvas.CardFor(nodes[5])!.IsFocused.ShouldBeTrue();
            edits[^1].Kind.ShouldBe(WorkflowEditKind.Delete);
            edits[^1].Nodes.ShouldBe([nodes[3], nodes[4]], ignoreOrder: true);
            canvas.LastAnnouncement.ShouldBe("Deleted 2 steps");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Canvases_Ignore_The_Editing_Keys()
    {
        var (canvas, window, nodes, graph) = Show(editable: false);
        try
        {
            canvas.CardFor(nodes[1])!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.M);
            canvas.IsMoving.ShouldBeFalse();
            Press(window, PhysicalKey.Delete);
            graph.Nodes.Count.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Moved_Step_Is_No_Longer_Arranged_By_The_Canvas()
    {
        var (canvas, window, nodes, _) = Show();
        try
        {
            Drag(window, Center(canvas, nodes[0]), new Vector(44, 0));
            nodes[0].Position.ShouldBe(new Point(220, 0));
            // Narrowing arranges only workflows the canvas placed on its own: this one now has a step the person placed.
            window.Width = 400;
            Dispatcher.UIThread.RunJobs();
            canvas.IsNarrow.ShouldBeTrue();
            nodes[0].Position.ShouldBe(new Point(220, 0));
            nodes[1].Position.ShouldBe(new Point(170, 112));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Near_The_Edge_Scrolls_The_View()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (canvas, window, nodes, _) = Show();
        try
        {
            var start = Center(canvas, nodes[5]);
            // Hold the card 6px from the right edge of the 760 wide view.
            Drag(window, start, new Vector(754 - start.X, 0), release: false);
            var origin = canvas.View.Origin.X;
            var before = canvas.DisplayPosition(nodes[5]).X;
            clock.Advance(TimeSpan.FromMilliseconds(500));
            Dispatcher.UIThread.RunJobs();
            canvas.View.Origin.X.ShouldBeGreaterThan(origin + 100);
            // The card stays under the pointer, so it travels with the view.
            canvas.DisplayPosition(nodes[5]).X.ShouldBeGreaterThan(before + 100);
            window.MouseUp(start + new Point(754 - start.X, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            canvas.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
