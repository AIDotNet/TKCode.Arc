using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Docking;

/// <summary>
/// Spec 14: a layout of 200 panes in 40 groups loads in under 120ms (desktop, warm). A headless test run is slower than a
/// desktop app and shares the machine, so the assertion here is a loose ceiling that catches regressions of the kind
/// "every change refreshes every pane"; the measured numbers (and a plain-controls baseline) are printed for the record.
/// </summary>
public class DockPerfTests
{
    /// <summary>Four columns of ten groups of five panes; the middle two columns are the document area.</summary>
    internal static DockWorkspace Large()
    {
        var columns = new List<DockNode>();
        for (var c = 0; c < 4; c++)
        {
            var groups = new List<DockNode>();
            for (var g = 0; g < 10; g++)
            {
                var group = new DockGroup();
                for (var p = 0; p < 5; p++)
                {
                    var id = $"c{c}g{g}p{p}";
                    group.Panes.Add(c is 1 or 2
                        ? DockSample.Document("doc:" + id, id + ".md")
                        : DockSample.Tool(id, "Tool " + id, c == 0 ? DockSide.Left : DockSide.Right));
                }
                groups.Add(group);
            }
            columns.Add(new DockSplit(Orientation.Vertical, [.. groups]));
        }
        var documents = new DockDocumentArea { Root = new DockSplit(Orientation.Horizontal, columns[1], columns[2]), Size = 50 };
        columns[0].Size = 25;
        columns[3].Size = 25;
        return new DockWorkspace { Root = new DockSplit(Orientation.Horizontal, columns[0], documents, columns[3]) };
    }

    private static double ColdLoad(out Window window, out DockLayout layout)
    {
        var watch = Stopwatch.StartNew();
        layout = new DockLayout { Workspace = Large() };
        window = new Window { Content = layout, Width = 1600, Height = 1000 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds;
    }

    [AvaloniaFact]
    public void Two_Hundred_Panes_In_Forty_Groups_Load_Quickly()
    {
        // The first run pays for JIT; the spec's target is a warm start.
        var first = ColdLoad(out var w0, out _);
        w0.Close();
        var samples = new List<double>();
        DockLayout? layout = null;
        Window? window = null;
        for (var i = 0; i < 3; i++)
        {
            window?.Close();
            samples.Add(ColdLoad(out window, out layout));
        }
        try
        {
            layout!.Workspace.AllPanes().Count().ShouldBe(200);
            layout.Workspace.Groups().Count().ShouldBe(40);
            var json = layout.SaveLayout();
            var load = Stopwatch.StartNew();
            layout.LoadLayout(json).ShouldBeTrue();
            window!.UpdateLayout();
            load.Stop();

            // A structural change (a pane moves) on the large layout.
            var pane = layout.Workspace.FindPane("c0g0p0")!;
            var move = Stopwatch.StartNew();
            layout.Move(pane, DockTarget.Edge(DockPosition.Bottom));
            window.UpdateLayout();
            move.Stop();

            var visuals = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(layout).Count();
            // Baseline: the same window with the 40 cards drawn as plain borders (no dock).
            var plain = Stopwatch.StartNew();
            var grid = new UniformGrid { Columns = 4 };
            for (var i = 0; i < 40; i++)
            {
                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                for (var t = 0; t < 5; t++)
                {
                    stack.Children.Add(new Border { Child = new TextBlock { Text = "Tab " + t } });
                }
                grid.Children.Add(new Border { Child = new DockPanel { Children = { stack, new TextBlock { Text = "content" } } } });
            }
            var plainWindow = new Window { Content = grid, Width = 1600, Height = 1000 };
            plainWindow.Show();
            Dispatcher.UIThread.RunJobs();
            plainWindow.UpdateLayout();
            plain.Stop();
            plainWindow.Close();
            Console.WriteLine($"PERFB plain window with 40 bordered stacks of 5 text tabs: {plain.Elapsed.TotalMilliseconds:F1} ms");
            var warm = samples.Min();
            Console.WriteLine($"PERF dock 200 panes / 40 groups ({visuals} visuals): first {first:F1} ms, warm {string.Join(" / ", samples.Select(s => s.ToString("F1")))} ms, LoadLayout {load.Elapsed.TotalMilliseconds:F1} ms, Move {move.Elapsed.TotalMilliseconds:F1} ms");
            // A loose ceiling (headless runners are slow and often loaded): catches work that grows faster than the layout.
            warm.ShouldBeLessThan(1000);
        }
        finally
        {
            window?.Close();
        }
    }
}
