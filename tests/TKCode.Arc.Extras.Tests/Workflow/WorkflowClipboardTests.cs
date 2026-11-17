using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Workflow;

public class WorkflowClipboardTests
{
    /// <summary>A trigger, a condition with Yes and No outputs, and an approval after Yes, at fixed places (the view is 1:1).</summary>
    private sealed class Sample
    {
        public readonly WorkflowGraph Graph = new();
        public readonly WorkflowNode Trigger = new("trigger", "Refund requested") { HasDefaultInput = false, Position = new Point(176, 0) };
        public readonly WorkflowNode Check = new("check", "Amount over $500?") { Kind = "condition", Position = new Point(176, 110) };
        public readonly WorkflowNode Approve = new("approve", "Manager approval") { Position = new Point(0, 286) };
        public readonly WorkflowPort Yes = new(WorkflowPortDirection.Output, "Yes") { DataType = "refund" };
        public readonly WorkflowPort No = new(WorkflowPortDirection.Output, "No");

        public Sample()
        {
            Check.Icon = Geometry.Parse("M0,0 L10,10");
            Check.Outputs.Add(Yes);
            Check.Outputs.Add(No);
            foreach (var node in new[] { Trigger, Check, Approve })
            {
                Graph.Nodes.Add(node);
            }
            Graph.Connect(Trigger, Check);
            Graph.Connect(Check, Yes, Approve, null);
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

    private static async Task Settle()
    {
        for (var i = 0; i < 5; i++)
        {
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public void A_Group_Records_What_Your_Code_Changes_As_One_Edit()
    {
        var s = new Sample();
        var history = s.Graph.History;
        var added = new WorkflowNode("notify", "Notify the team") { Position = new Point(300, 286) };
        using (history.BeginGroup())
        {
            s.Graph.Nodes.Add(added);
            // Nested groups belong to the outer one.
            using (history.BeginGroup())
            {
                s.Graph.Connect(s.Check, s.No, added, null);
            }
            s.Approve.Position = new Point(22, 286);
            history.CanUndo.ShouldBeFalse();
        }
        history.CanUndo.ShouldBeTrue();

        history.Undo();
        s.Graph.Nodes.ShouldBe([s.Trigger, s.Check, s.Approve]);
        s.Graph.Edges.Count.ShouldBe(2);
        s.Approve.Position.ShouldBe(new Point(0, 286));
        history.CanUndo.ShouldBeFalse();

        history.Redo();
        s.Graph.Nodes.ShouldBe([s.Trigger, s.Check, s.Approve, added]);
        s.Graph.Edges[^1].Target.ShouldBe(added);
        s.Approve.Position.ShouldBe(new Point(22, 286));

        // Removing a step (its edges go with it) and clearing undo back to the same lists, in order.
        using (history.BeginGroup())
        {
            s.Graph.Nodes.Remove(s.Check);
        }
        s.Graph.Edges.ShouldBeEmpty();
        history.Undo();
        s.Graph.Nodes.ShouldBe([s.Trigger, s.Check, s.Approve, added]);
        s.Graph.Edges.Select(e => e.Target).ShouldBe([s.Check, s.Approve, added]);

        // A group that changes nothing records nothing.
        history.Clear();
        using (history.BeginGroup())
        {
        }
        history.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void The_Clipboard_Json_Carries_Steps_Ports_And_Edges()
    {
        var s = new Sample();
        var fragment = WorkflowFragment.Copy([s.Check, s.Approve], s.Graph.Edges);
        var json = fragment.ToJson();
        json.ShouldStartWith("{\"format\":\"tkcode-arc-workflow\",\"version\":1,");
        json.ShouldNotContain("trigger");

        var back = WorkflowFragment.FromJson(json).ShouldNotBeNull();
        back.Nodes.Select(n => n.Title).ShouldBe(["Amount over $500?", "Manager approval"]);
        back.Nodes[0].Kind.ShouldBe("condition");
        back.Nodes[0].Outputs.Select(p => p.Label).ShouldBe(["Yes", "No"]);
        back.Nodes[0].Outputs[0].DataType.ShouldBe("refund");
        var edge = back.Edges.ShouldHaveSingleItem();
        (edge.Source, edge.SourcePort, edge.Target).ShouldBe((back.Nodes[0], back.Nodes[0].Outputs[0], back.Nodes[1]));
        back.Nodes[1].Position.ShouldBe(new Point(0, 286));

        WorkflowFragment.FromJson("Amount over $500?").ShouldBeNull();
        WorkflowFragment.FromJson("{\"format\":\"something-else\",\"version\":1,\"nodes\":[{\"title\":\"x\"}]}").ShouldBeNull();
        WorkflowFragment.FromJson("{\"format\":\"tkcode-arc-workflow\",").ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task Copied_Steps_Paste_Next_To_Themselves_With_New_Ids()
    {
        var (canvas, window, s) = Show();
        var edits = new List<WorkflowEditEventArgs>();
        canvas.GraphEdited += (_, e) => edits.Add(e);
        try
        {
            canvas.Select([s.Check, s.Approve]);
            (await canvas.CopySelectionAsync()).ShouldBeTrue();
            canvas.LastAnnouncement.ShouldBe("Copied 2 steps");

            (await canvas.PasteAsync()).ShouldBeTrue();
            s.Graph.Nodes.Count.ShouldBe(5);
            var check = s.Graph.Nodes[3];
            var approve = s.Graph.Nodes[4];
            (check.Id, approve.Id).ShouldBe(("check-2", "approve-2"));
            // One grid step down and right, selected, with the edge between them on the same output.
            check.Position.ShouldBe(new Point(198, 132));
            approve.Position.ShouldBe(new Point(22, 308));
            canvas.SelectedNodes.ShouldBe([check, approve]);
            var edge = s.Graph.Edges[^1];
            (edge.Source, edge.SourcePort!.Label, edge.Target).ShouldBe((check, "Yes", approve));
            check.Outputs.ShouldNotContain(s.Yes);
            // In the same app the icon comes along.
            check.Icon.ShouldBeSameAs(s.Check.Icon);
            edits[^1].Kind.ShouldBe(WorkflowEditKind.Paste);
            edits[^1].Nodes.ShouldBe([check, approve]);
            canvas.LastAnnouncement.ShouldBe("Pasted 2 steps");

            // Pasting again cascades; one undo takes a paste back.
            (await canvas.PasteAsync()).ShouldBeTrue();
            s.Graph.Nodes[5].Position.ShouldBe(new Point(220, 154));
            s.Graph.Nodes[5].Id.ShouldBe("check-3");
            canvas.Undo();
            s.Graph.Nodes.Count.ShouldBe(5);
            s.Graph.Edges.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Json_Text_From_Elsewhere_Pastes_And_Other_Text_Does_Not()
    {
        var (canvas, window, s) = Show();
        try
        {
            var clipboard = window.Clipboard.ShouldNotBeNull();
            await clipboard.SetTextAsync("not a workflow");
            (await canvas.PasteAsync()).ShouldBeFalse();
            canvas.LastAnnouncement.ShouldBe("Nothing to paste");

            // Copied in another app: its places are far away, so it lands in the middle of the view.
            var far = new WorkflowNode("far", "Far away step") { Position = new Point(5000, 5000) };
            await clipboard.SetTextAsync(WorkflowFragment.Copy([far], []).ToJson());
            (await canvas.PasteAsync()).ShouldBeTrue();
            var pasted = s.Graph.Nodes[^1];
            pasted.Title.ShouldBe("Far away step");
            pasted.Id.ShouldBe("far");
            // 300 × 60 centered in 760 × 600, on the 22 grid.
            pasted.Position.ShouldBe(new Point(220, 264));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Keys_Cut_Paste_And_Duplicate_On_An_Editable_Canvas_Only()
    {
        var (canvas, window, s) = Show();
        try
        {
            canvas.CardFor(s.Approve)!.Focus(NavigationMethod.Tab);
            canvas.Select([s.Approve]);
            Press(window, PhysicalKey.D, RawInputModifiers.Control);
            s.Graph.Nodes.Count.ShouldBe(4);
            s.Graph.Nodes[^1].Position.ShouldBe(new Point(22, 308));
            canvas.SelectedNodes.ShouldBe([s.Graph.Nodes[^1]]);

            canvas.Select([s.Approve]);
            Press(window, PhysicalKey.X, RawInputModifiers.Control);
            await Settle();
            s.Graph.Nodes.ShouldNotContain(s.Approve);
            canvas.LastAnnouncement.ShouldBe("Cut 1 step");
            Press(window, PhysicalKey.V, RawInputModifiers.Control);
            await Settle();
            s.Graph.Nodes[^1].Title.ShouldBe("Manager approval");
        }
        finally
        {
            window.Close();
        }

        (canvas, window, s) = Show(editable: false);
        try
        {
            canvas.Select([s.Check]);
            (await canvas.CopySelectionAsync()).ShouldBeTrue();
            (await canvas.PasteAsync()).ShouldBeFalse();
            canvas.DuplicateSelection().ShouldBe(0);
            (await canvas.CutSelectionAsync()).ShouldBeFalse();
            s.Graph.Nodes.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }
}
