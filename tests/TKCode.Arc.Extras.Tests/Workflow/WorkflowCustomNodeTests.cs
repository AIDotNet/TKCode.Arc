using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Workflow;

public class WorkflowCustomNodeTests
{
    /// <summary>A trigger into an email step; the email step's template has a text box under a 40px tall block.</summary>
    private static (WorkflowCanvas Canvas, Window Window, WorkflowNode Trigger, WorkflowNode Email) Show()
    {
        var graph = new WorkflowGraph();
        var trigger = new WorkflowNode("trigger", "Order placed") { Position = new Point(0, 0) };
        var email = new WorkflowNode("email", "Email the customer") { Kind = "email", Position = new Point(0, 110) };
        graph.Nodes.Add(trigger);
        graph.Nodes.Add(email);
        graph.Connect(trigger, email);
        var canvas = new WorkflowCanvas { Graph = graph, IsEditable = true, AutoFit = WorkflowAutoFit.None };
        canvas.NodeTemplates.Add(new FuncDataTemplate<WorkflowNode>(n => n.Kind == "email", (n, _) => new StackPanel
        {
            Children =
            {
                new Border { Name = "Block", Height = 40 },
                new Input { Name = "To", Text = "ada@example.com" }
            }
        }));
        var window = TestHelpers.Show(canvas);
        window.Width = 760;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return (canvas, window, trigger, email);
    }

    private static T Find<T>(Visual root, string name) where T : Control
        => (T)Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<Control>().First(c => c.Name == name);

    [Fact]
    public void A_Node_Template_Matches_Its_Kind()
    {
        var template = new WorkflowNodeTemplate { Kind = "email" };
        template.Match(new WorkflowNode("a", "A") { Kind = "email" }).ShouldBeTrue();
        template.Match(new WorkflowNode("b", "B") { Kind = "webhook" }).ShouldBeFalse();
        template.Match("email").ShouldBeFalse();
        new WorkflowNodeTemplate().Match(new WorkflowNode("c", "C")).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Matching_Template_Replaces_The_Cards_Content_And_The_Card_Fits_It()
    {
        var (canvas, window, trigger, email) = Show();
        try
        {
            var card = canvas.CardFor(email)!;
            card.IsCustom.ShouldBeTrue();
            canvas.CardFor(trigger)!.IsCustom.ShouldBeFalse();
            Find<Input>(card, "To").Text.ShouldBe("ada@example.com");
            // Padded 12 above and below, 1px frame: as tall as the content, at least 60.
            var height = card.DesiredSize.Height;
            height.ShouldBeGreaterThan(60);
            var edge = canvas.EdgeVisualFor(canvas.Graph!.Edges[0])!;
            canvas.PortPoint(email, null, true).Y.ShouldBe(110 + height, 0.01);

            // The card grows with its content, and edges leaving it follow.
            var next = new WorkflowNode("next", "Next") { Position = new Point(0, 400) };
            canvas.Graph.Nodes.Add(next);
            canvas.Graph.Connect(email, next);
            Find<Border>(card, "Block").Height = 80;
            Dispatcher.UIThread.RunJobs();
            card.DesiredSize.Height.ShouldBe(height + 40, 0.01);
            canvas.EdgeVisualFor(canvas.Graph.Edges[1])!.Points.Start.Y.ShouldBe(110 + height + 40, 0.01);
            edge.Points.End.Y.ShouldBe(110);

            // A step that changes kind changes content.
            email.Kind = "note";
            Dispatcher.UIThread.RunJobs();
            card.IsCustom.ShouldBeFalse();
            card.DesiredSize.Height.ShouldBe(60);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Controls_In_A_Card_Keep_Their_Keys_And_Double_Clicks()
    {
        var (canvas, window, trigger, email) = Show();
        var activated = 0;
        canvas.NodeActivated += (_, _) => activated++;
        try
        {
            var box = Find<Input>(canvas.CardFor(email)!, "To");
            box.Focus();
            Dispatcher.UIThread.RunJobs();
            foreach (var key in new[] { PhysicalKey.C, PhysicalKey.M, PhysicalKey.Space, PhysicalKey.Delete })
            {
                window.KeyPressQwerty(key, RawInputModifiers.None);
                window.KeyReleaseQwerty(key, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
            }
            canvas.IsConnecting.ShouldBeFalse();
            canvas.IsMoving.ShouldBeFalse();
            canvas.SelectedNodes.ShouldBeEmpty();
            canvas.Graph!.Nodes.ShouldContain(email);

            var at = box.TranslatePoint(new Point(10, box.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            activated.ShouldBe(0);
            email.Position.ShouldBe(new Point(0, 110));
        }
        finally
        {
            window.Close();
        }
    }
}
