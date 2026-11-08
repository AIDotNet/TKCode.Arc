using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.AdvancedCharts;

public class FunnelChartTests
{
    private static FunnelChart Single(double width = 800) => new()
    {
        Width = width,
        Label = "Trial",
        Stages = new AvaloniaList<FunnelStage>
        {
            new("visit", "Visit", 1000.0),
            new("signup", "Signup", 400.0),
            new("paid", "Paid", 100.0)
        }
    };

    private static FunnelChart Segmented(double width = 800) => new()
    {
        Width = width,
        Label = "Trial",
        Segments = new AvaloniaList<FunnelSegment> { new("web", "Web"), new("app", "App") },
        Stages = new AvaloniaList<FunnelStage>
        {
            new("visit", "Visit", 600, 200),
            new("signup", "Signup", 300, 100),
            new("paid", "Paid", 60, 50)
        }
    };

    private static void WithChart(FunnelChart chart, Action<FunnelChart, Window> test)
    {
        var window = TestHelpers.Show(chart);
        window.Width = Math.Max(chart.Width, 900);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        try
        {
            test(chart, window);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Retention_Is_From_Previous_And_From_First()
    {
        var model = new FunnelModel([new("a", "A", 1000.0), new("b", "B", 400.0), new("c", "C", 100.0)], []);
        model.FromPrevious("total", 0).ShouldBe(1);
        model.FromPrevious("total", 1).ShouldBe(0.4);
        model.FromPrevious("total", 2).ShouldBe(0.25);
        model.FromFirst("total", 2).ShouldBe(0.1);
        model.DropOff("total", 0).ShouldBe(0);
        model.DropOff("total", 1).ShouldBe(600);
        model.ExitChance(0, 1).ShouldBe(0.6, 1e-12);
    }

    [AvaloniaFact]
    public void A_Stage_Larger_Than_The_Previous_One_Is_Capped()
    {
        var model = new FunnelModel([new("a", "A", 100.0), new("b", "B", 250.0), new("c", "C", 80.0)], []);
        model.CountAt("total", 1).ShouldBe(100);
        model.DropOff("total", 1).ShouldBe(0);
        model.FromPrevious("total", 1).ShouldBe(1);
        model.CountAt("total", 2).ShouldBe(80);
        model.FromFirst("total", 2).ShouldBe(0.8);
    }

    [AvaloniaFact]
    public void Segments_Are_Capped_On_Their_Own_And_Sum_To_The_Total()
    {
        var model = new FunnelModel(
            [new("a", "A", 100, 50), new("b", "B", 60, 70)],
            [new("x", "X"), new("y", "Y")]);
        model.LayerCountAt(1, 1).ShouldBe(50);
        model.CountAt("total", 1).ShouldBe(110);
        model.CountAt("x", 1).ShouldBe(60);
    }

    [AvaloniaFact]
    public void Zero_Negative_And_Missing_Values_Count_As_Zero_Without_Dividing_By_Zero()
    {
        var model = new FunnelModel([new("a", "A", 100.0), new("b", "B", -5.0), new("c", "C", double.NaN), new("d", "D", 10.0)], []);
        model.CountAt("total", 1).ShouldBe(0);
        model.CountAt("total", 2).ShouldBe(0);
        model.CountAt("total", 3).ShouldBe(0);
        model.FromPrevious("total", 1).ShouldBe(0);
        model.FromPrevious("total", 2).ShouldBe(0);
        model.FromFirst("total", 3).ShouldBe(0);
        model.ExitChance(0, 1).ShouldBe(1);
        FunnelModel.Retention(5, 0).ShouldBe(0);
        new FunnelModel([new("a", "A", 0.0)], []).FromPrevious("total", 0).ShouldBe(0);
    }

    [AvaloniaFact]
    public void Band_Fills_At_The_First_Step_And_Tapers_With_The_Counts()
    {
        WithChart(Single(), (chart, _) =>
        {
            chart.LayerThickness(0, 0).ShouldBe(1, 1e-9);
            chart.LayerThickness(0, 1).ShouldBe(0.4, 1e-9);
            chart.LayerThickness(0, 2).ShouldBe(0.1, 1e-9);
        });
    }

    [AvaloniaFact]
    public void Split_View_Opens_A_Gap_That_Comes_Out_Of_The_Band()
    {
        WithChart(Segmented(), (chart, _) =>
        {
            chart.LayerGap(0, 0).ShouldBe(0, 1e-9);
            chart.SplitAmount.ShouldBe(0);
            chart.View = "split";
            chart.ResolvedView.ShouldBe("split");
            chart.SplitAmount.ShouldBe(1);
            var gap = chart.SplitGap / chart.BandHeight;
            chart.LayerGap(0, 0).ShouldBe(gap, 1e-9);
            (chart.LayerThickness(0, 0) + chart.LayerThickness(1, 0) + gap).ShouldBe(1, 1e-9);
            chart.CountText(0).ShouldBe("800");
        });
    }

    [AvaloniaFact]
    public void A_Segment_Key_Isolates_And_Rescales_That_Segment()
    {
        WithChart(Segmented(), (chart, _) =>
        {
            chart.View = "app";
            chart.ResolvedView.ShouldBe("app");
            chart.LayerThickness(1, 0).ShouldBe(1, 1e-9);
            chart.LayerThickness(1, 1).ShouldBe(0.5, 1e-9);
            chart.LayerThickness(0, 0).ShouldBe(0, 1e-9);
            chart.CountText(0).ShouldBe("200");
            chart.RateText(2).ShouldBe("50%");
            chart.DropText(2).ShouldBe("↓ 50");
        });
    }

    [AvaloniaFact]
    public void Unknown_Views_Show_The_Total_And_Split_Needs_Segments()
    {
        WithChart(Segmented(), (chart, _) =>
        {
            chart.View = "nope";
            chart.ResolvedView.ShouldBe("total");
            chart.CountText(0).ShouldBe("800");
        });
        WithChart(Single(), (chart, _) =>
        {
            chart.View = "split";
            chart.ResolvedView.ShouldBe("total");
            chart.SplitAmount.ShouldBe(0);
        });
    }

    [AvaloniaFact]
    public void Morphs_Update_The_Same_Geometry_Object()
    {
        WithChart(Segmented(), (chart, _) =>
        {
            var geometry = chart.LayerGeometry(1).ShouldNotBeNull();
            var before = geometry.Bounds;
            chart.View = "app";
            chart.LayerGeometry(1).ShouldBeSameAs(geometry);
            geometry.Bounds.ShouldNotBe(before);
            geometry.Bounds.Height.ShouldBe(chart.BandHeight, 0.01);
        });
    }

    [AvaloniaFact]
    public void Orientation_Switches_Below_420()
    {
        var chart = Single(419);
        WithChart(chart, (c, window) =>
        {
            c.IsVertical.ShouldBeTrue();
            c.Width = 420;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            c.IsVertical.ShouldBeFalse();
            c.Orientation = FunnelOrientation.Vertical;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            c.IsVertical.ShouldBeTrue();
            c.Orientation = FunnelOrientation.Horizontal;
            c.Width = 300;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            c.IsVertical.ShouldBeFalse();
            c.Bounds.Height.ShouldBeGreaterThan(c.BandHeight);
        });
    }

    [AvaloniaFact]
    public void Keyboard_Model()
    {
        WithChart(Single(), (chart, window) =>
        {
            chart.Focus(NavigationMethod.Tab);
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBeNull();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
        });
    }

    [AvaloniaFact]
    public void Pointer_Reads_The_Column_Under_It_And_Leaving_Clears()
    {
        WithChart(Single(600), (chart, window) =>
        {
            chart.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            chart.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.MouseMove(new Point(450, 40));
            chart.ActiveIndex.ShouldBe(2);
            chart.IsTooltipOpen.ShouldBeTrue();
            window.MouseMove(new Point(50, 40));
            chart.ActiveIndex.ShouldBe(0);
            window.MouseMove(new Point(800, 40));
            chart.ActiveIndex.ShouldBeNull();
            chart.IsTooltipOpen.ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void Event_Reports_The_Step_And_Its_Rates()
    {
        WithChart(Single(), (chart, _) =>
        {
            var seen = new List<FunnelActiveIndexChangedEventArgs>();
            chart.ActiveIndexChanged += (_, e) => seen.Add(e);
            chart.ActiveIndex = 1;
            seen.Count.ShouldBe(1);
            seen[0].Index.ShouldBe(1);
            seen[0].Stage!.Key.ShouldBe("signup");
            seen[0].Count.ShouldBe(400);
            seen[0].RetentionFromPrevious.ShouldBe(0.4);
            seen[0].RetentionFromFirst.ShouldBe(0.4);
            chart.ActiveIndex = 2;
            seen[1].RetentionFromPrevious.ShouldBe(0.25);
            seen[1].RetentionFromFirst.ShouldBe(0.1);
            chart.ActiveIndex = null;
            seen[2].Index.ShouldBeNull();
            seen[2].Stage.ShouldBeNull();
            seen[2].Count.ShouldBe(0);
        });
    }

    [AvaloniaFact]
    public void An_Index_Outside_The_Stages_Clears_The_Reading()
    {
        WithChart(Single(), (chart, _) =>
        {
            chart.ActiveIndex = 1;
            chart.ActiveIndex = 99;
            chart.ActiveIndex.ShouldBeNull();
            chart.IsTooltipOpen.ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void An_Index_Set_Before_The_Stages_Is_Read_Once_They_Arrive()
    {
        var chart = new FunnelChart { Width = 800, ActiveIndex = 1 };
        chart.Stages.Add(new FunnelStage("a", "A", 10.0));
        chart.Stages.Add(new FunnelStage("b", "B", 5.0));
        WithChart(chart, (c, _) =>
        {
            c.ActiveIndex.ShouldBe(1);
            c.IsHighlightShown.ShouldBeTrue();
            c.IsTooltipOpen.ShouldBeTrue();
            c.ActiveIndex = null;
            c.IsHighlightShown.ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void Automation_Name_Announces_Step_Count_And_Both_Rates()
    {
        WithChart(Single(), (chart, _) =>
        {
            chart.Focusable.ShouldBeTrue();
            AutomationProperties.GetName(chart).ShouldBe("Trial");
            chart.ActiveIndex = 2;
            AutomationProperties.GetName(chart).ShouldBe("Step 3 of 3, Paid: 100, 25% of the previous step, 10% of the first step");
            chart.Summary.ShouldContain("Step 2 of 3, Signup: 400, 40% of the previous step, 40% of the first step");
            chart.ActiveIndex = null;
            AutomationProperties.GetName(chart).ShouldBe("Trial");
        });
    }

    [AvaloniaFact]
    public void Empty_State_When_The_First_Step_Is_Zero()
    {
        var chart = new FunnelChart { Width = 800, Stages = new AvaloniaList<FunnelStage> { new("a", "A", 0.0), new("b", "B", 10.0) } };
        WithChart(chart, (c, window) =>
        {
            c.IsEmptyView.ShouldBeTrue();
            c.Focusable.ShouldBeFalse();
            c.EmptyLabel.ShouldBe("No data");
            c.ActiveIndex = 0;
            c.ActiveIndex.ShouldBeNull();
            c.Summary.ShouldContain("No data");
            c.Stages = new AvaloniaList<FunnelStage> { new("a", "A", 5.0) };
            c.IsEmptyView.ShouldBeFalse();
            c.Focusable.ShouldBeTrue();
        });
        WithChart(new FunnelChart { Width = 800 }, (c, _) => c.IsEmptyView.ShouldBeTrue());
    }

    [AvaloniaFact]
    public void A_Segment_Without_Counts_Shows_The_Empty_State_In_Its_Own_View()
    {
        var chart = Segmented();
        chart.Segments.Add(new FunnelSegment("none", "None"));
        WithChart(chart, (c, _) =>
        {
            c.ActiveIndex = 1;
            c.View = "none";
            c.IsEmptyView.ShouldBeTrue();
            c.ActiveIndex.ShouldBeNull();
            c.View = "total";
            c.IsEmptyView.ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void Formatter_And_Unit()
    {
        WithChart(Single(), (chart, _) =>
        {
            chart.CountText(0).ShouldBe("1,000");
            chart.Unit = "users";
            chart.CountText(0).ShouldBe("1,000 users");
            chart.Unit = "%";
            chart.CountText(1).ShouldBe("400%");
            chart.ValueFormatter = value => $"<{value}>";
            chart.CountText(1).ShouldBe("<400>");
            chart.DropText(1).ShouldBe("↓ <600>");
            chart.RateText(1).ShouldBe("40%");
            chart.ActiveIndex = 1;
            AutomationProperties.GetName(chart)!.ShouldContain("Signup: <400>");
        });
    }

    [AvaloniaFact]
    public void Tooltip_Stays_Inside_The_Chart_And_Flips()
    {
        WithChart(Single(600), (chart, _) =>
        {
            chart.ActiveIndex = 0;
            var first = chart.TooltipBox.ShouldNotBeNull();
            first.X.ShouldBeGreaterThanOrEqualTo(200);
            chart.ActiveIndex = 2;
            var last = chart.TooltipBox.ShouldNotBeNull();
            (last.Right).ShouldBeLessThanOrEqualTo(400);
            new Rect(chart.Bounds.Size).Contains(last).ShouldBeTrue();
        });
        WithChart(Single(300), (chart, _) =>
        {
            chart.IsVertical.ShouldBeTrue();
            chart.ActiveIndex = 0;
            chart.TooltipBox!.Value.Y.ShouldBeGreaterThanOrEqualTo(100);
            chart.ActiveIndex = 2;
            chart.TooltipBox!.Value.Bottom.ShouldBeLessThanOrEqualTo(200);
            new Rect(chart.Bounds.Size).Contains(chart.TooltipBox!.Value).ShouldBeTrue();
        });
    }

    [AvaloniaFact]
    public void Dots_Are_Off_When_Motion_Is_Off_Or_Flow_Disabled()
    {
        WithChart(Single(), (chart, _) =>
        {
            chart.IsFlowActive.ShouldBeTrue();
            chart.IsFlowRunning.ShouldBeFalse();
            chart.IsMorphing.ShouldBeFalse();
            chart.IsFlowEnabled = false;
            chart.IsFlowActive.ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void Dots_Leave_At_The_Real_Drop_Off()
    {
        var model = new FunnelModel(
            [new("a", "A", 800, 200), new("b", "B", 400, 150), new("c", "C", 100, 30)],
            [new("x", "X"), new("y", "Y")]);
        var sim = new FunnelFlowSim(11);
        sim.SetModel(model, "total");
        const int runs = 200_000;
        foreach (var layer in new[] { 0, 1 })
        {
            var atFirst = 0;
            var atSecond = 0;
            for (var i = 0; i < runs; i++)
            {
                var exit = sim.RollExit(layer);
                atFirst += exit == 1 ? 1 : 0;
                atSecond += exit == 2 ? 1 : 0;
            }
            var survivedFirst = runs - atFirst;
            ((double)atFirst / runs).ShouldBe(model.ExitChance(layer, 1), 0.01);
            ((double)atSecond / survivedFirst).ShouldBe(model.ExitChance(layer, 2), 0.01);
        }
    }

    [AvaloniaFact]
    public void About_Forty_Dots_Stay_Active_And_Hidden_Segments_Leave()
    {
        var model = new FunnelModel(
            [new("a", "A", 800, 200), new("b", "B", 700, 180), new("c", "C", 650, 160), new("d", "D", 600, 150)],
            [new("x", "X"), new("y", "Y")]);
        var sim = new FunnelFlowSim(3);
        sim.SetModel(model, "total");
        sim.WarmUp();
        sim.AliveCount.ShouldBeGreaterThanOrEqualTo(35);
        sim.AliveCount.ShouldBeLessThanOrEqualTo(FunnelFlowSim.Capacity);
        sim.SetModel(model, "y");
        for (var i = 0; i < 30; i++)
        {
            sim.Step(0.05);
        }
        var dots = sim.Dots;
        for (var i = 0; i < dots.Length; i++)
        {
            if (dots[i].IsAlive && dots[i].Layer == 0)
            {
                dots[i].Leaving.ShouldBeGreaterThanOrEqualTo(0);
            }
        }
    }

    [AvaloniaFact]
    public void Theme_Tokens_Apply()
    {
        WithChart(Single(), (chart, _) =>
        {
            chart.SplitGap.ShouldBe(6);
            chart.HeaderGap.ShouldBe(10);
            chart.DotRadius.ShouldBe(2.5);
            chart.BandHeight.ShouldBe(200);
        });
    }

    [AvaloniaFact]
    public void Reduced_Motion_Applies_Views_At_Once_Without_Dots()
    {
        var chart = Segmented();
        var window = TestHelpers.Show(chart, ArcMotionLevel.Reduced);
        window.Width = 900;
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            chart.View = "split";
            chart.IsMorphing.ShouldBeFalse();
            chart.SplitAmount.ShouldBe(1);
            chart.IsFlowRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Morphs_Instead_Of_Jumping()
    {
        var chart = Segmented();
        var window = TestHelpers.Show(chart, ArcMotionLevel.Full);
        window.Width = 900;
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            chart.View = "split";
            chart.IsMorphing.ShouldBeTrue();
            chart.SplitAmount.ShouldBeLessThan(1);
            chart.IsFlowActive.ShouldBeTrue();
            chart.IsFlowRunning.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
