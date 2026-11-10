using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Sliders;

public class RangeHistogramSliderTests
{
    private static readonly double[] s_data = [5, 12, 18, 25, 25, 33, 41, 47, 52, 60, 68, 75, 81, 88, 95, 100];

    private static (RangeHistogramSlider Slider, Window Window) Show(RangeHistogramSlider slider, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        slider.Width = 322; // track = 300px between the 11px insets
        slider.Data ??= s_data;
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { slider } }, level);
        return (slider, window);
    }

    /// <summary>A point in the window on the track row, at a value of the 0..100 scale.</summary>
    private static Point TrackPoint(RangeHistogramSlider slider, Window window, double value)
    {
        var rail = slider.Rail!;
        var origin = rail.TranslatePoint(default, window)!.Value;
        return new Point(origin.X + rail.ToPixel(slider.ToPosition(value)), origin.Y + rail.TrackCenterY);
    }

    private static Point ChartPoint(RangeHistogramSlider slider, Window window, double value)
    {
        var rail = slider.Rail!;
        return TrackPoint(slider, window, value).WithY(rail.TranslatePoint(default, window)!.Value.Y + 10);
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Drag(Window window, Point from, Point to)
    {
        window.MouseMove(from, RawInputModifiers.None);
        window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
    }

    private static void RenderFrame()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Release(Window window, Point at)
    {
        window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void Histogram_Counts_Equal_Bars_And_The_Last_Keeps_The_Maximum()
    {
        RangeHistogramSlider.Histogram([0, 9.9, 10, 19, 20, 25, -1], 0, 20, 2).ShouldBe([2, 3]);
        RangeHistogramSlider.Histogram(null, 0, 20, 3).ShouldBe([0, 0, 0]);
        // No span: everything inside falls in the first bar instead of dividing by zero.
        RangeHistogramSlider.Histogram([4, 4], 4, 4, 2).ShouldBe([2, 0]);
    }

    [AvaloniaFact]
    public void The_Scale_Comes_From_The_Data_And_Values_Default_To_Its_Ends()
    {
        var (slider, window) = Show(new RangeHistogramSlider { Label = "Price", Data = [12.5, 80.2] });
        try
        {
            slider.ScaleMinimum.ShouldBe(0);
            slider.ScaleMaximum.ShouldBe(81);
            slider.ValueOf(0).ShouldBe(0);
            slider.ValueOf(1).ShouldBe(81);
            slider.LowerValue.ShouldBeNull();
            slider.Counts.Count.ShouldBe(32);

            slider.Data = [-7.5, 20];
            slider.ScaleMinimum.ShouldBe(-8);
            slider.ScaleMaximum.ShouldBe(20);

            slider.Minimum = 10;
            slider.Maximum = 15;
            slider.ScaleMinimum.ShouldBe(10);
            slider.ScaleMaximum.ShouldBe(15);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Default_Geometry_Follows_Arc()
    {
        var (slider, window) = Show(new RangeHistogramSlider { Label = "Price" });
        try
        {
            var rail = slider.Rail!;
            rail.ChartHeight.ShouldBe(76);
            rail.RowHeight.ShouldBe(36);
            rail.Bounds.Height.ShouldBe(112);
            rail.TrackThickness.ShouldBe(4);
            rail.ThumbDiameter.ShouldBe(22);
            rail.TrackWidth.ShouldBe(300);
            rail.TrackCenterY.ShouldBe(76 + 18);
            rail.Thumbs[0].Bounds.Y.ShouldBe(76 + 18 - 11);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Readout_Shows_Both_Formatted_Ends()
    {
        var (slider, window) = Show(new RangeHistogramSlider { Label = "Price", LowerValue = 20, UpperValue = 80, ValueFormatter = v => $"${v:N0}" });
        try
        {
            var texts = slider.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            texts.ShouldContain("$20");
            texts.ShouldContain("–");
            texts.ShouldContain("$80");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Step_And_Large_Moves_Walk_Bar_Edges()
    {
        var committed = new List<(double, double)>();
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, Bins = 10, LowerValue = 23, UpperValue = 80 };
        slider.ValueCommitted += (_, e) => committed.Add((e.LowerValue, e.UpperValue));
        var (_, window) = Show(slider);
        try
        {
            slider.Rail!.Thumbs[0].Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            slider.LowerValue.ShouldBe(24);
            Press(window, PhysicalKey.ArrowLeft);
            slider.LowerValue.ShouldBe(23);
            // The next edge up from 23 is 30, then 40; down from 40 is 30.
            Press(window, PhysicalKey.PageUp);
            slider.LowerValue.ShouldBe(30);
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            slider.LowerValue.ShouldBe(40);
            Press(window, PhysicalKey.PageDown);
            slider.LowerValue.ShouldBe(30);
            committed.Count.ShouldBe(5);
            committed[^1].ShouldBe((30, 80));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Home_And_End_Go_To_The_Limits_Of_That_Thumb()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 60, MinStepsBetweenThumbs = 2 };
        var (_, window) = Show(slider);
        try
        {
            slider.Rail!.Thumbs[0].Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.End);
            slider.LowerValue.ShouldBe(58);
            Press(window, PhysicalKey.Home);
            slider.LowerValue.ShouldBe(0);
            slider.Rail!.Thumbs[1].Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Home);
            slider.UpperValue.ShouldBe(2);
            Press(window, PhysicalKey.End);
            slider.UpperValue.ShouldBe(100);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Key_At_A_Limit_Commits_Nothing()
    {
        var committed = 0;
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 0, UpperValue = 60 };
        slider.ValueCommitted += (_, _) => committed++;
        var (_, window) = Show(slider);
        try
        {
            slider.Rail!.Thumbs[0].Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowLeft);
            slider.LowerValue.ShouldBe(0);
            committed.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Press_On_The_Chart_Moves_The_Nearest_Thumb()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80, SnapToBins = false };
        var (_, window) = Show(slider);
        try
        {
            var at = ChartPoint(slider, window, 70);
            window.MouseMove(at, RawInputModifiers.None);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            slider.UpperValue.ShouldBe(70);
            slider.LowerValue.ShouldBe(20);
            slider.Classes.Contains(RangeHistogramSlider.DraggingPseudoClass).ShouldBeTrue();
            Release(window, at);
            slider.Classes.Contains(RangeHistogramSlider.DraggingPseudoClass).ShouldBeFalse();
            slider.UpperValue.ShouldBe(70);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Grabbed_Thumb_Keeps_Its_Offset()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80, SnapToBins = false };
        var (_, window) = Show(slider);
        try
        {
            // 3 px right of the thumb's center (1 value per 3 px); dragging by 30 px moves the thumb by 10, not to the pointer.
            var from = TrackPoint(slider, window, 20) + new Point(3, 0);
            Drag(window, from, from + new Point(30, 0));
            slider.LowerValue.ShouldBe(30);
            Release(window, from + new Point(30, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thumbs_Never_Cross()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 40, MinStepsBetweenThumbs = 5, SnapToBins = false };
        var (_, window) = Show(slider);
        try
        {
            var from = TrackPoint(slider, window, 20);
            var to = TrackPoint(slider, window, 90);
            Drag(window, from, to);
            slider.LowerValue.ShouldBe(35);
            slider.UpperValue.ShouldBe(40);
            Release(window, to);
            slider.LowerValue.ShouldBe(35);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Release_Near_A_Bar_Edge_Settles_On_It_Only_With_SnapToBins()
    {
        // 10 bars over 300px: an edge every 30px; 22 is 6px from the edge at 20, 25 is 15px from the nearest edge.
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, Bins = 10, LowerValue = 50, UpperValue = 90 };
        var (_, window) = Show(slider);
        try
        {
            var from = TrackPoint(slider, window, 50);
            var near = TrackPoint(slider, window, 22);
            Drag(window, from, near);
            slider.LowerValue.ShouldBe(22);
            Release(window, near);
            slider.LowerValue.ShouldBe(20);

            from = TrackPoint(slider, window, 20);
            var far = TrackPoint(slider, window, 25);
            Drag(window, from, far);
            Release(window, far);
            slider.LowerValue.ShouldBe(25);

            slider.SnapToBins = false;
            from = TrackPoint(slider, window, 25);
            var close = TrackPoint(slider, window, 42);
            Drag(window, from, close);
            Release(window, close);
            slider.LowerValue.ShouldBe(42);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stacked_Thumbs_Wait_For_The_First_Move()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 50, UpperValue = 51, SnapToBins = false };
        var (_, window) = Show(slider);
        try
        {
            var from = TrackPoint(slider, window, 50.5);
            window.MouseMove(from, RawInputModifiers.None);
            window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            slider.Classes.Contains(RangeHistogramSlider.DraggingPseudoClass).ShouldBeFalse();
            // Moving left takes the lower thumb, whichever one was on top.
            var to = from - new Point(30, 0);
            window.MouseMove(to, RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            slider.LowerValue!.Value.ShouldBeLessThan(50);
            slider.UpperValue.ShouldBe(51);
            Release(window, to);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bars_Are_One_Geometry_The_Frames_Reuse()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80 };
        var (_, window) = Show(slider);
        try
        {
            RenderFrame();
            var bars = slider.Rail!.BarsGeometry.ShouldNotBeNull();
            slider.LowerValue = 30;
            RenderFrame();
            slider.Rail.BarsGeometry.ShouldBeSameAs(bars);
            slider.Data = [1, 2, 3];
            RenderFrame();
            slider.Rail.BarsGeometry.ShouldNotBeSameAs(bars);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Names_Each_Thumb_And_Reports_Its_Reach()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 60, ValueFormatter = v => $"${v:N0}" };
        var (_, window) = Show(slider);
        try
        {
            var thumbs = slider.Rail!.Thumbs;
            AutomationProperties.GetName(thumbs[0]).ShouldBe("Price, minimum");
            AutomationProperties.GetName(thumbs[1]).ShouldBe("Price, maximum");
            AutomationProperties.GetItemStatus(thumbs[1]).ShouldBe("$60");

            var range = ControlAutomationPeer.CreatePeerForElement(thumbs[0]).GetProvider<IRangeValueProvider>()!;
            range.Minimum.ShouldBe(0);
            range.Maximum.ShouldBe(59);
            range.Value.ShouldBe(20);
            range.SetValue(35);
            slider.LowerValue.ShouldBe(35);

            slider.LowerThumbName = "From";
            AutomationProperties.GetName(thumbs[0]).ShouldBe("From");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Takes_No_Input()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80, IsEnabled = false };
        var (_, window) = Show(slider);
        try
        {
            slider.Rail!.Thumbs[0].IsTabStop.ShouldBeFalse();
            var at = TrackPoint(slider, window, 50);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            slider.LowerValue.ShouldBe(20);
            slider.UpperValue.ShouldBe(80);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Lifts_The_Held_Thumb_And_Raises_Its_Guide_Then_Settles()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80, SnapToBins = false };
        var (_, window) = Show(slider, ArcMotionLevel.Full);
        try
        {
            var from = TrackPoint(slider, window, 20);
            var to = TrackPoint(slider, window, 40);
            Drag(window, from, to);
            TestHelpers.PumpUntil(() => Math.Abs(slider.LiftOf(0) - RangeHistogramSlider.LiftScale) < 0.001 && slider.GuideOf(0).Opacity == 1 && slider.GuideOf(0).Scale == 1, 3000).ShouldBeTrue();
            slider.GuideOf(1).Opacity.ShouldBe(0);
            Release(window, to);
            TestHelpers.PumpUntil(() => Math.Abs(slider.LiftOf(0) - 1) < 0.001 && slider.GuideOf(0).Opacity == 0 && Math.Abs(slider.ShownPosition(0) - 400) < 0.01, 3000).ShouldBeTrue();
            slider.LowerValue.ShouldBe(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Past_A_Limit_The_Thumb_Gives_At_Most_The_Stretch_And_Comes_Back()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80, SnapToBins = false };
        var (_, window) = Show(slider, ArcMotionLevel.Full);
        try
        {
            var from = TrackPoint(slider, window, 20);
            var to = from - new Point(400, 0);
            Drag(window, from, to);
            slider.LowerValue.ShouldBe(0);
            var pixels = slider.ShownPosition(0) / SliderMath.Span * slider.Rail!.TrackWidth;
            pixels.ShouldBeLessThan(0);
            pixels.ShouldBeGreaterThan(-RangeHistogramSlider.Stretch);
            Release(window, to);
            TestHelpers.PumpUntil(() => Math.Abs(slider.ShownPosition(0)) < 0.01, 3000).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Has_No_Stretch()
    {
        var slider = new RangeHistogramSlider { Label = "Price", Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80 };
        var (_, window) = Show(slider, ArcMotionLevel.Reduced);
        try
        {
            var from = TrackPoint(slider, window, 20);
            Drag(window, from, from - new Point(400, 0));
            slider.ShownPosition(0).ShouldBe(0);
            slider.GuideOf(0).Scale.ShouldBe(1);
            slider.GuideOf(0).Opacity.ShouldBe(1);
            Release(window, from - new Point(400, 0));
            slider.GuideOf(0).Opacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}
