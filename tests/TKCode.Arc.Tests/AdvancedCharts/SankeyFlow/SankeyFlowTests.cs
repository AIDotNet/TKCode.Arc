using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.AdvancedCharts;

public class SankeyFlowTests
{
    /// <summary>Two sources into a balanced middle node, split to two ends: 100 in all.</summary>
    private static SankeyFlowLink[] Balanced() =>
    [
        new("a", "m", 60),
        new("b", "m", 40),
        new("m", "x", 75),
        new("m", "y", 25)
    ];

    private static SankeyFlow Chart(SankeyFlowLink[] links, double width = 640) => new()
    {
        Width = width,
        Label = "Journey",
        Links = links,
        Nodes = [new("a", "Alpha"), new("b", "Beta"), new("m", "Middle"), new("x", "Xray"), new("y", "Yankee")]
    };

    private static int Index(SankeyFlow chart, string id) => chart.Model.Nodes.First(n => n.Id == id).Index;

    [AvaloniaFact]
    public void Columns_Are_Inferred_From_Links_And_Explicit_Ones_Win()
    {
        var model = SankeyFlowModel.Build([new("x", "X"), new("late", "Late", 3)], [new("a", "m", 1), new("m", "x", 1), new("a", "x", 1), new("x", "late", 1)]);
        model.Nodes.First(n => n.Id == "a").Column.ShouldBe(0);
        model.Nodes.First(n => n.Id == "m").Column.ShouldBe(1);
        // The longest path decides, not the direct link from a.
        model.Nodes.First(n => n.Id == "x").Column.ShouldBe(2);
        model.Nodes.First(n => n.Id == "late").Column.ShouldBe(3);
        model.ColumnCount.ShouldBe(4);

        var pinned = SankeyFlowModel.Build([new("b", "B", 5)], [new("a", "b", 1)]);
        pinned.ColumnCount.ShouldBe(6);
        pinned.Columns[3].ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Links_Resolve_Unknown_Ends_Repeats_And_Bad_Values()
    {
        var model = SankeyFlowModel.Build([new("a", "Alpha"), new("a", "Duplicate")],
            [new("a", "b", 2), new("a", "b", 3), new("b", "b", 9), new("a", "c", double.NaN), new("a", "d", -1), new("a", "e", 0)]);
        model.Nodes.Count.ShouldBe(2);
        model.Nodes[0].Label.ShouldBe("Alpha");
        model.Nodes[1].Label.ShouldBe("b");
        model.Links.Count.ShouldBe(1);
        model.Links[0].Value.ShouldBe(5);
        model.Total.ShouldBe(5);
    }

    [AvaloniaFact]
    public void Nodes_And_Ribbons_Are_Sized_By_Value_On_One_Scale()
    {
        var chart = Chart(Balanced());
        var window = TestHelpers.Show(chart);
        try
        {
            var placement = chart.Placement!;
            placement.Scale.ShouldBeGreaterThan(0);
            placement.Nodes[Index(chart, "a")].Extent.ShouldBe(60 * placement.Scale, 1e-6);
            placement.Nodes[Index(chart, "b")].Extent.ShouldBe(40 * placement.Scale, 1e-6);
            placement.Nodes[Index(chart, "m")].Extent.ShouldBe(100 * placement.Scale, 1e-6);
            for (var i = 0; i < chart.Model.Links.Count; i++)
            {
                placement.Links[i].Thick.ShouldBe(chart.Model.Links[i].Value * placement.Scale, 1e-6);
            }
            // A node's ribbons fill it exactly and do not overlap.
            var stacked = chart.Model.Nodes[Index(chart, "m")].InLinks.Select(l => placement.Links[l]).OrderBy(l => l.C1).ToArray();
            (stacked[1].C1 - stacked[0].C1).ShouldBe((stacked[0].Thick + stacked[1].Thick) / 2, 1e-6);
            placement.Nodes[Index(chart, "a")].Main.ShouldBeLessThan(placement.Nodes[Index(chart, "m")].Main);
            placement.Nodes[Index(chart, "m")].Main.ShouldBeLessThan(placement.Nodes[Index(chart, "x")].Main);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tracing_Conserves_The_Share_At_Every_Stage()
    {
        var model = SankeyFlowModel.Build(null, Balanced());
        var trace = model.TraceNode(model.Nodes.First(n => n.Id == "a").Index);
        trace.Amount.ShouldBe(60);
        // All of a goes through m, then splits by m's own proportions.
        var flowOf = (string source, string target) => trace.LinkFlow[model.Links.First(l => model.Nodes[l.Source].Id == source && model.Nodes[l.Target].Id == target).Index];
        flowOf("a", "m").ShouldBe(60, 1e-9);
        flowOf("b", "m").ShouldBe(0);
        flowOf("m", "x").ShouldBe(45, 1e-9);
        flowOf("m", "y").ShouldBe(15, 1e-9);
        (flowOf("m", "x") + flowOf("m", "y")).ShouldBe(trace.Amount, 1e-9);

        // Tracing a downstream ribbon splits upstream the same way.
        var ribbon = model.TraceLink(model.Links.First(l => model.Nodes[l.Target].Id == "y").Index);
        ribbon.Amount.ShouldBe(25);
        ribbon.LinkFlow[0].ShouldBe(15, 1e-9);
        ribbon.LinkFlow[1].ShouldBe(10, 1e-9);
        ribbon.LinkFlow[2].ShouldBe(0);
        ribbon.NodeFlow.Sum().ShouldBe(15 + 10 + 25 + 25, 1e-9);
    }

    [AvaloniaFact]
    public void Tracing_Scales_With_A_Node_That_Loses_Flow()
    {
        var model = SankeyFlowModel.Build(null, [new("a", "m", 100), new("m", "x", 50)]);
        var trace = model.TraceNode(0);
        trace.LinkFlow[1].ShouldBe(50, 1e-9);
        var half = model.TraceLink(1);
        half.LinkFlow[0].ShouldBe(50, 1e-9);
    }

    [AvaloniaFact]
    public void Cycles_Do_Not_Hang_Or_Crash()
    {
        var links = new SankeyFlowLink[] { new("a", "b", 10), new("b", "c", 8), new("c", "a", 4), new("c", "d", 4), new("b", "a", 2) };
        var model = SankeyFlowModel.Build(null, links);
        model.Links.Count(l => l.IsBack).ShouldBeGreaterThan(0);
        model.Total.ShouldBeGreaterThan(0);
        for (var i = 0; i < model.Nodes.Count; i++)
        {
            var trace = model.TraceNode(i);
            trace.LinkFlow.ShouldAllBe(f => double.IsFinite(f) && f >= 0);
            trace.NodeFlow.Zip(model.Nodes).ShouldAllBe(pair => pair.First <= pair.Second.Value + 1e-9);
        }
        var chart = new SankeyFlow { Width = 640, Links = links };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Placement!.Links.ShouldAllBe(l => double.IsFinite(l.C0) && double.IsFinite(l.C1));
            var back = chart.Model.Links.First(l => l.IsBack).Index;
            chart.Placement.Links[back].M1.ShouldBeLessThan(chart.Placement.Links[back].M0);
            chart.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.Reading.ShouldNotBeNull();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Walks_Columns_And_Nodes_And_Escape_Clears()
    {
        var chart = Chart(Balanced());
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Focus(NavigationMethod.Tab);
            var first = chart.Placement!.Columns[0];
            chart.KeyNode.ShouldBe(first[0]);
            chart.Traced.ShouldNotBeNull();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.KeyNode.ShouldBe(first[1]);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.KeyNode.ShouldBe(first[1]);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.KeyNode.ShouldBe(Index(chart, "m"));
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.Model.Nodes[chart.KeyNode].Column.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.KeyNode.ShouldBe(Index(chart, "m"));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.KeyNode.ShouldBe(first[0]);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.KeyNode.ShouldBe(-1);
            chart.Traced.ShouldBeNull();
            chart.ActiveTrace.ShouldBeNull();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.KeyNode.ShouldBe(first[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Upright_Layout_Below_420_Remaps_The_Arrows()
    {
        var chart = Chart(Balanced(), 360);
        var window = TestHelpers.Show(chart);
        try
        {
            chart.IsUpright.ShouldBeTrue();
            chart.Focus(NavigationMethod.Tab);
            var first = chart.Placement!.Columns[0];
            chart.KeyNode.ShouldBe(first[0]);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.KeyNode.ShouldBe(first[1]);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.KeyNode.ShouldBe(Index(chart, "m"));
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            chart.Model.Nodes[chart.KeyNode].Column.ShouldBe(0);
            chart.Width = 500;
            Dispatcher.UIThread.RunJobs();
            chart.IsUpright.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Name_Announces_Value_Share_And_Destinations()
    {
        var chart = Chart(Balanced());
        chart.Unit = " people";
        var window = TestHelpers.Show(chart);
        try
        {
            AutomationProperties.GetName(chart).ShouldBe("Journey");
            chart.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            AutomationProperties.GetName(chart).ShouldBe("Middle: 100 people, 100% of the total, flows to Xray 75 people, Yankee 25 people");
            chart.TotalLabel = "all signups";
            AutomationProperties.GetName(chart).ShouldStartWith("Middle: 100 people, 100% of all signups");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            AutomationProperties.GetName(chart).ShouldEndWith("no further destinations");
            chart.ValueFormatter = v => $"{v:0} u";
            chart.Summary.ShouldContain("Middle 100 u, 100%");
            chart.DescribeLink(0).ShouldBe("Alpha to Middle: 60 u, 60% of all signups");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            AutomationProperties.GetName(chart).ShouldBe("Journey");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_And_Tap_Pick_Nodes_And_Ribbons()
    {
        var chart = Chart(Balanced());
        var window = TestHelpers.Show(chart);
        try
        {
            var node = chart.Placement!.Nodes[Index(chart, "m")];
            var onNode = new Point(node.Main + node.Thickness / 2, node.CrossCenter);
            chart.Pick(onNode).ShouldBe(new SankeyFlow.TraceTarget(Index(chart, "m"), -1));
            var ribbon = chart.Placement.Links[0];
            var onRibbon = new Point(ribbon.MainAt(0.5), ribbon.CrossAt(0.5));
            chart.Pick(onRibbon).ShouldBe(new SankeyFlow.TraceTarget(-1, 0));
            chart.Pick(new Point(1, 1)).ShouldBeNull();

            chart.Tap(onRibbon);
            chart.Traced.ShouldBe(new SankeyFlow.TraceTarget(-1, 0));
            chart.Reading.ShouldBe("Alpha to Middle: 60, 60% of the total");
            chart.Tap(onRibbon);
            chart.Traced.ShouldBeNull();
            chart.Tap(onNode);
            chart.ActiveTrace!.Amount.ShouldBe(100);
            chart.Tap(new Point(1, 1));
            chart.Traced.ShouldBeNull();

            var position = chart.TranslatePoint(onNode, window)!.Value;
            window.MouseMove(position, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            chart.Traced.ShouldBe(new SankeyFlow.TraceTarget(Index(chart, "m"), -1));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_State_Takes_No_Focus_And_Data_Fills_It()
    {
        var chart = new SankeyFlow { Width = 640, Label = "Journey", PlotHeight = 200, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        chart.EmptyLabel.ShouldBe("No flows yet");
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Focusable.ShouldBeFalse();
            chart.Bounds.Height.ShouldBe(200);
            chart.Summary.ShouldBe("Journey. No flows yet.");
            chart.Pick(new Point(5, 5)).ShouldBeNull();
            chart.Links!.Add(new SankeyFlowLink("a", "b", 3));
            Dispatcher.UIThread.RunJobs();
            chart.Focusable.ShouldBeTrue();
            chart.Placement!.Nodes.Length.ShouldBe(2);
            chart.Links = [];
            Dispatcher.UIThread.RunJobs();
            chart.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Particle_Toggle_And_Property_Stay_In_Step()
    {
        var chart = Chart(Balanced());
        var changes = new List<bool>();
        chart.ParticlesChanged += (_, e) => changes.Add(e.IsEnabled);
        var window = TestHelpers.Show(chart, ArcMotionLevel.Full);
        try
        {
            chart.IsToggleShown.ShouldBeTrue();
            chart.ParticleToggle!.IsChecked.ShouldBe(true);
            chart.ParticleToggle.IsChecked = false;
            chart.IsParticlesEnabled.ShouldBeFalse();
            changes.ShouldBe([false]);
            chart.IsParticleLoopRunning.ShouldBeFalse();
            chart.IsParticlesEnabled = true;
            chart.ParticleToggle.IsChecked.ShouldBe(true);
            changes.ShouldBe([false, true]);
            chart.IsParticleToggleVisible = false;
            Dispatcher.UIThread.RunJobs();
            chart.IsToggleShown.ShouldBeFalse();
            chart.ParticleToggleLabel = "Dots";
            chart.ParticleToggle.Content.ShouldBe("Dots");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Removes_Particles_And_The_Toggle()
    {
        var chart = Chart(Balanced());
        var window = TestHelpers.Show(chart, ArcMotionLevel.Reduced);
        try
        {
            chart.IsToggleShown.ShouldBeFalse();
            chart.IsDrawingParticles.ShouldBeFalse();
            chart.IsParticleLoopRunning.ShouldBeFalse();
            chart.IsMorphing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Grows_In_And_Particles_Are_Capped_And_Allocate_Nothing()
    {
        var links = new List<SankeyFlowLink>();
        for (var s = 0; s < 18; s++)
        {
            for (var t = 0; t < 18; t++)
            {
                links.Add(new SankeyFlowLink($"s{s}", $"t{t}", 1 + (s * 7 + t * 3) % 11));
            }
        }
        var chart = new SankeyFlow { Width = 760, Links = links };
        var window = TestHelpers.Show(chart, ArcMotionLevel.Full);
        try
        {
            chart.Model.Links.Count.ShouldBe(324);
            var total = 0;
            for (var i = 0; i < chart.Model.Links.Count; i++)
            {
                chart.ParticleCountOf(i).ShouldBeLessThanOrEqualTo(SankeyFlow.MaxParticlesPerRibbon);
                total += chart.ParticleCountOf(i);
            }
            total.ShouldBeLessThan(800);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var sampled = 0;
            for (var frame = 0; frame < 60; frame++)
            {
                sampled += chart.SampleParticles(frame / 60.0);
            }
            (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
            sampled.ShouldBe(total * 60);
        }
        finally
        {
            window.Close();
        }

        var heavy = Chart([new("a", "m", 100000), new("b", "m", 1)]);
        var second = TestHelpers.Show(heavy);
        try
        {
            heavy.ParticleCountOf(0).ShouldBe(SankeyFlow.MaxParticlesPerRibbon);
        }
        finally
        {
            second.Close();
        }
    }

    [AvaloniaFact]
    public void Data_Change_Keeps_Survivors_And_Drops_What_Left()
    {
        var chart = Chart(Balanced());
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            var key = chart.Model.Nodes[chart.KeyNode].Id;
            chart.Links = [new("a", "m", 10), new("m", "x", 10), new("m", "y", 5)];
            Dispatcher.UIThread.RunJobs();
            chart.Model.Nodes[chart.KeyNode].Id.ShouldBe(key);
            chart.Placement!.Links.Length.ShouldBe(3);
            chart.ActiveTrace.ShouldNotBeNull();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layout_Stays_Quick_And_Deterministic_At_A_Few_Hundred_Links()
    {
        var links = new List<SankeyFlowLink>();
        var random = new Random(11);
        for (var i = 0; i < 400; i++)
        {
            var stage = random.Next(4);
            links.Add(new SankeyFlowLink($"n{stage}-{random.Next(12)}", $"n{stage + 1}-{random.Next(12)}", 1 + random.Next(40)));
        }
        var options = new SankeyFlowLayoutOptions(0, 900, 0, 340, 12, 10);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var model = SankeyFlowModel.Build(null, links);
        var first = SankeyFlowLayout.Arrange(model, options);
        watch.Stop();
        watch.ElapsedMilliseconds.ShouldBeLessThan(1000);
        var second = SankeyFlowLayout.Arrange(SankeyFlowModel.Build(null, links), options);
        second.Nodes.ShouldBe(first.Nodes);
        second.Links.ShouldBe(first.Links);
        first.Nodes.ShouldAllBe(n => n.Cross >= -1e-6 && n.Cross + n.Extent <= 340 + 1e-6);
    }

    [Fact]
    public void Particles_stay_off_text()
    {
        List<Avalonia.Rect> keepOut = [new(10, 10, 20, 10)];
        Assert.True(SankeyFlow.IsKeptOut(keepOut, new Avalonia.Point(15, 15)));
        Assert.False(SankeyFlow.IsKeptOut(keepOut, new Avalonia.Point(5, 15)));
        Assert.False(SankeyFlow.IsKeptOut([], new Avalonia.Point(15, 15)));
    }
}
