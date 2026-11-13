using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Sliders;

public class WindowRangeTests
{
    /// <summary>101 points: the domain is 0..100.</summary>
    private static double[] Data(int count = 101)
    {
        var data = new double[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = 100 + i % 7 * 10;
        }
        return data;
    }

    private static (WindowRange Range, Window Window) Show(WindowRange range, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        range.Width = 324; // chart = 300px between the 12px edges, 3px per unit on 0..100 (the test window is 400px wide)
        range.Data ??= Data();
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { range } }, level);
        return (range, window);
    }

    private static Point PlotPoint(WindowRange range, Window window, double x)
    {
        var plot = range.Plot!;
        var origin = plot.TranslatePoint(default, window)!.Value;
        return new Point(origin.X + x, origin.Y + plot.Bounds.Height / 2);
    }

    private static Point ValuePoint(WindowRange range, Window window, double value) =>
        PlotPoint(range, window, range.PixelOfValue(value, range.Plot!.Bounds.Width));

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Drag(Window window, Point from, Point to, bool release = true)
    {
        // Hit testing reads the composition tree, which follows layout on the next render tick.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(from, RawInputModifiers.None);
        window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        if (release)
        {
            window.MouseUp(to, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Without_Values_The_Window_Is_The_Last_Quarter_And_The_Properties_Stay_Null()
    {
        var (range, _) = Show(new WindowRange());
        range.WindowStart.ShouldBe(75);
        range.WindowEnd.ShouldBe(100);
        range.Start.ShouldBeNull();
        range.End.ShouldBeNull();
        range.DomainMinimum.ShouldBe(0);
        range.DomainMaximum.ShouldBe(100);
    }

    [AvaloniaFact]
    public void Values_Snap_To_The_Step_And_Keep_The_Span_Limits()
    {
        var (range, _) = Show(new WindowRange { MinSpan = 6, MaxSpan = 40 });
        range.Start = 97.4;
        // Setting the start keeps it and moves the end; past the domain the start gives way instead.
        range.WindowEnd.ShouldBe(100);
        range.WindowStart.ShouldBe(94);
        // The properties keep what was set; WindowStart and WindowEnd are the window after the limits.
        range.Start.ShouldBe(97.4);
        range.End.ShouldBeNull();
        range.SetRange(10, 90);
        (range.WindowStart, range.WindowEnd).ShouldBe((10, 50));
        range.ChartHeight = 20;
        range.ChartHeight.ShouldBe(64);
        range.ChartHeight = 1000;
        range.ChartHeight.ShouldBe(240);
    }

    [AvaloniaFact]
    public void Readouts_Use_The_Formatters_And_The_Span_Can_Be_Hidden()
    {
        var (range, _) = Show(new WindowRange
        {
            Start = 40,
            End = 69,
            ValueFormatter = v => $"Day {v:0}",
            SpanFormatter = s => $"{s + 1:0} days"
        });
        range.StartReadout!.Text.ShouldBe("Day 40");
        range.SpanReadout!.Text.ShouldBe("30 days");
        range.EndReadout!.Text.ShouldBe("Day 69");
        range.ShowSpan = false;
        range.SpanReadout.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Value_Changes_Report_Old_And_New_Ends_Once()
    {
        var (range, _) = Show(new WindowRange { Start = 20, End = 40 });
        var changes = new List<WindowRangeValueChangedEventArgs>();
        range.ValueChanged += (_, e) => changes.Add(e);
        range.SetRange(50, 80);
        changes.Count.ShouldBe(1);
        (changes[0].OldStart, changes[0].OldEnd, changes[0].NewStart, changes[0].NewEnd).ShouldBe((20, 40, 50, 80));
    }

    [AvaloniaFact]
    public void Keys_Move_The_Window_And_Its_Edges()
    {
        var (range, window) = Show(new WindowRange { Start = 40, End = 60, MinSpan = 5 });
        var committed = 0;
        range.ValueCommitted += (_, _) => committed++;
        range.Plot!.WindowHandle.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.ArrowRight);
        (range.WindowStart, range.WindowEnd).ShouldBe((41, 61));
        Key(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
        (range.WindowStart, range.WindowEnd).ShouldBe((31, 51));
        Key(window, PhysicalKey.PageUp);
        (range.WindowStart, range.WindowEnd).ShouldBe((41, 61));
        Key(window, PhysicalKey.End);
        (range.WindowStart, range.WindowEnd).ShouldBe((80, 100));
        Key(window, PhysicalKey.Home);
        (range.WindowStart, range.WindowEnd).ShouldBe((0, 20));
        committed.ShouldBe(5);

        range.Plot.StartHandle.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.End);
        (range.WindowStart, range.WindowEnd).ShouldBe((15, 20));
        Key(window, PhysicalKey.ArrowRight);
        range.WindowStart.ShouldBe(15); // at the minimum span: the key only nudges

        range.Plot.EndHandle.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.End);
        (range.WindowStart, range.WindowEnd).ShouldBe((15, 100));
        Key(window, PhysicalKey.Home);
        range.WindowEnd.ShouldBe(20);
    }

    [AvaloniaFact]
    public void Each_Part_Is_A_Named_Slider_With_Its_Limits()
    {
        var (range, _) = Show(new WindowRange { Label = "Report", Start = 30, End = 50, MinSpan = 4, ValueFormatter = v => $"D{v:0}" });
        var plot = range.Plot!;
        ControlAutomationPeer.CreatePeerForElement(range).GetName().ShouldBe("Report");
        AutomationProperties.GetName(plot.StartHandle).ShouldBe("Report, start");
        AutomationProperties.GetName(plot.WindowHandle).ShouldBe("Report, window");
        AutomationProperties.GetName(plot.EndHandle).ShouldBe("Report, end");
        AutomationProperties.GetItemStatus(plot.WindowHandle).ShouldBe("D30 to D50");

        var start = (IRangeValueProvider)ControlAutomationPeer.CreatePeerForElement(plot.StartHandle);
        ControlAutomationPeer.CreatePeerForElement(plot.StartHandle).GetAutomationControlType().ShouldBe(AutomationControlType.Slider);
        (start.Minimum, start.Maximum, start.Value).ShouldBe((0, 46, 30));
        var body = (IRangeValueProvider)ControlAutomationPeer.CreatePeerForElement(plot.WindowHandle);
        (body.Minimum, body.Maximum, body.Value).ShouldBe((0, 80, 30));
        var end = (IRangeValueProvider)ControlAutomationPeer.CreatePeerForElement(plot.EndHandle);
        (end.Minimum, end.Maximum, end.Value).ShouldBe((34, 100, 50));
        body.SetValue(60);
        (range.WindowStart, range.WindowEnd).ShouldBe((60, 80));
    }

    [AvaloniaFact]
    public void Dragging_The_Window_Pans_It_And_Commits_Once()
    {
        var (range, window) = Show(new WindowRange { Start = 40, End = 60 });
        var committed = new List<(double, double)>();
        range.ValueCommitted += (_, e) => committed.Add((e.Start, e.End));
        Drag(window, ValuePoint(range, window, 50), ValuePoint(range, window, 62));
        (range.WindowStart, range.WindowEnd).ShouldBe((52, 72));
        committed.ShouldBe([(52, 72)]);
        range.Plot!.WindowHandle.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Dragging_An_Edge_Resizes_Within_The_Minimum_Span()
    {
        var (range, window) = Show(new WindowRange { Start = 40, End = 60, MinSpan = 6 });
        var edge = PlotPoint(range, window, range.PixelOfValue(60, range.Plot!.Bounds.Width) + 6);
        Drag(window, edge, edge.WithX(edge.X - 200));
        (range.WindowStart, range.WindowEnd).ShouldBe((40, 46));
        var width = range.Plot.Bounds.Width;
        Drag(window, PlotPoint(range, window, range.PixelOfValue(40, width) - 6), PlotPoint(range, window, range.PixelOfValue(10, width) - 6));
        (range.WindowStart, range.WindowEnd).ShouldBe((10, 46));
        range.Plot.StartHandle.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Edges_Reach_Fourteen_Pixels_Outward()
    {
        var (range, _) = Show(new WindowRange { Start = 40, End = 60 });
        var plot = range.Plot!;
        var start = range.PixelOfValue(40, plot.Bounds.Width);
        var end = range.PixelOfValue(60, plot.Bounds.Width);
        plot.PartAt(start - 12 - 13).ShouldBe(WindowRangePart.Start);
        plot.PartAt(start - 12 - 15).ShouldBe(WindowRangePart.None);
        plot.PartAt(start + 1).ShouldBe(WindowRangePart.Start);
        plot.PartAt(start + 4).ShouldBe(WindowRangePart.Window);
        plot.PartAt(end + 12 + 13).ShouldBe(WindowRangePart.End);
        plot.PartAt(end + 12 + 15).ShouldBe(WindowRangePart.None);
    }

    [AvaloniaFact]
    public void A_Press_Outside_Centers_The_Window_On_The_Pointer()
    {
        var (range, window) = Show(new WindowRange { Start = 70, End = 90 });
        Drag(window, ValuePoint(range, window, 20), ValuePoint(range, window, 20));
        (range.WindowStart, range.WindowEnd).ShouldBe((10, 30));
        Drag(window, ValuePoint(range, window, 99), ValuePoint(range, window, 99));
        (range.WindowStart, range.WindowEnd).ShouldBe((80, 100));
    }

    [AvaloniaFact]
    public void A_Disabled_Range_Ignores_Input_And_Leaves_The_Tab_Order()
    {
        var (range, window) = Show(new WindowRange { Start = 40, End = 60, IsEnabled = false });
        Drag(window, ValuePoint(range, window, 50), ValuePoint(range, window, 80));
        (range.WindowStart, range.WindowEnd).ShouldBe((40, 60));
        foreach (var handle in range.Plot!.Handles)
        {
            handle.IsTabStop.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public void Ticks_And_The_Live_Dot_Follow_Their_Properties()
    {
        var (range, _) = Show(new WindowRange());
        range.Axis!.IsVisible.ShouldBeFalse();
        range.Ticks = [new WindowRangeTick(25, "Q2"), new WindowRangeTick(50, "Q3")];
        range.Axis.IsVisible.ShouldBeTrue();
        range.Axis.Labels.Count.ShouldBe(2);
        range.IsLive = true;
        range.Pulse.Opacity.ShouldBe(0); // motion is off: no pulse
    }

    [AvaloniaFact]
    public void New_Data_With_As_Many_Points_Morphs_And_A_New_Length_Replaces_The_Chart()
    {
        var (range, _) = Show(new WindowRange(), ArcMotionLevel.Full);
        var data = Data();
        data[^1] = 900;
        range.Data = data;
        range.ShownData[^1].ShouldBeLessThan(900); // morphing toward it
        range.Data = Data(51);
        range.ShownData.Length.ShouldBe(51);
        range.ShownData[^1].ShouldBe(Data(51)[^1]);
        range.DomainMaximum.ShouldBe(50);
    }

    [AvaloniaFact]
    public void Readouts_Roll_For_Keys_But_Not_While_Dragging()
    {
        var (range, window) = Show(new WindowRange { Start = 40, End = 60 }, ArcMotionLevel.Full);
        range.Plot!.WindowHandle.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.ArrowRight);
        range.StartReadout!.RollProgress.ShouldBeLessThan(1);
        Drag(window, ValuePoint(range, window, 51), ValuePoint(range, window, 55), release: false);
        range.StartReadout.RollProgress.ShouldBe(1);
        range.Mode.ShouldBe(WindowRangeMode.Pan);
    }

    [Fact]
    public void The_Curve_Does_Not_Overshoot_A_Peak()
    {
        Span<double> tangents = stackalloc double[3];
        WindowRangeChart.Tangents([10, 0, 10], 5, tangents);
        tangents[1].ShouldBe(0);
        WindowRangeChart.TopOf([0, 89]).ShouldBe(100, 1e-9);
        WindowRangeChart.TopOf([]).ShouldBe(1);
    }
}
