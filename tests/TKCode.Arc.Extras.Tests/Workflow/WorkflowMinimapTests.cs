using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Workflow;

public class WorkflowMinimapTests
{
    /// <summary>Three steps far apart on a 760 × 600 canvas at 100%, with a map in a corner.</summary>
    private static (WorkflowCanvas Canvas, WorkflowMinimap Map, Window Window) Show()
    {
        var graph = new WorkflowGraph();
        graph.Nodes.Add(new WorkflowNode("a", "A") { Position = new Point(0, 0) });
        graph.Nodes.Add(new WorkflowNode("b", "B") { Position = new Point(1500, 0) });
        graph.Nodes.Add(new WorkflowNode("c", "C") { Position = new Point(0, 1000) });
        var canvas = new WorkflowCanvas { Graph = graph, AutoFit = WorkflowAutoFit.None };
        var map = new WorkflowMinimap
        {
            Target = canvas,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
        };
        var window = TestHelpers.Show(new Panel { Children = { canvas, map } });
        window.Width = 760;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return (canvas, map, window);
    }

    [AvaloniaFact]
    public void The_Map_Shows_The_Workflow_And_The_View_And_Moves_The_View()
    {
        var (canvas, map, window) = Show();
        try
        {
            map.Bounds.Size.ShouldBe(new Size(200, 140));
            var m = map.Mapping().ShouldNotBeNull();
            // Everything fits in the map inside its 8px margin: 1800 × 1060 into 184 × 124.
            m.World.ShouldBe(new Rect(0, 0, 1800, 1060));
            m.Scale.ShouldBe(Math.Min(184.0 / 1800, 124.0 / 1060), 1e-9);

            // A press outside the frame centers the view there at once.
            var target = new Point(1650, 30);
            var onMap = new Point(m.Offset.X + target.X * m.Scale, m.Offset.Y + target.Y * m.Scale);
            var at = map.TranslatePoint(onMap, window)!.Value;
            window.MouseDown(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            var center = canvas.View.VisibleWorld(canvas.ViewportSize).Center;
            center.X.ShouldBe(1650, 1);
            center.Y.ShouldBe(30, 1);

            // Dragging moves the view with the pointer: 10px on the map is 10 / scale on the canvas. The map keeps its
            // scale while the drag lasts.
            window.MouseMove(at + new Vector(-10, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            canvas.View.VisibleWorld(canvas.ViewportSize).Center.X.ShouldBe(1650 - 10 / m.Scale, 1);
            map.Mapping()!.Value.Scale.ShouldBe(m.Scale);
            window.MouseUp(at + new Vector(-10, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            // The map takes no focus and screen readers skip it.
            map.Focusable.ShouldBeFalse();
            Avalonia.Automation.AutomationProperties.GetAccessibilityView(map).ShouldBe(Avalonia.Automation.AccessibilityView.Raw);
        }
        finally
        {
            window.Close();
        }
    }
}
