using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Sliders;

public class ElasticSliderTests
{
    private static (ElasticSlider Slider, Window Window) Show(ElasticSlider slider, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        slider.Width = 324; // track = 300px between the 12px insets
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20, 60, 20, 20), Children = { slider } }, level);
        return (slider, window);
    }

    private static (Point Origin, double Y) RailPoint(ElasticSlider slider, Window window)
    {
        var rail = slider.Rail!;
        var origin = rail.TranslatePoint(default, window)!.Value;
        return (origin, origin.Y + rail.Bounds.Height / 2);
    }

    [AvaloniaFact]
    public void A_Minimum_Above_The_Current_Maximum_Can_Be_Set_First()
    {
        // The XAML loader sets attributes in order inside BeginInit / EndInit, where RangeBase does not coerce yet.
        var slider = new ElasticSlider { Label = "Width" };
        Should.NotThrow(() =>
        {
            slider.BeginInit();
            slider.Minimum = 360;
            slider.Maximum = 900;
            slider.Value = 600;
            slider.EndInit();
        });
        var (_, window) = Show(slider);
        try
        {
            slider.Value.ShouldBe(600);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Past_The_End_Stretches_And_Release_Springs_Back()
    {
        var (slider, window) = Show(new ElasticSlider { Label = "Volume", Value = 50 }, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var (origin, y) = RailPoint(slider, window);
            window.MouseDown(new Point(origin.X + 12 + 150, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            slider.IsTagShown.ShouldBeTrue();
            window.MouseMove(new Point(origin.X + 12 + 400, y));
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(500));
            slider.Value.ShouldBe(100);
            // 100px past the end: rubber(100, 26) = (1 - 1 / (100 * 0.55 / 26 + 1)) * 26.
            slider.Over.ShouldBe((1 - 1 / (100 * 0.55 / 26 + 1)) * 26, 0.001);
            slider.ThumbScale.ShouldBe(1.28, 0.01);
            window.MouseUp(new Point(origin.X + 12 + 400, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(2));
            slider.Over.ShouldBe(0, 0.01);
            slider.ThumbScale.ShouldBe(1, 0.01);
            slider.Value.ShouldBe(100);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Detents_Hold_The_Thumb_Within_Seven_Pixels()
    {
        var slider = new ElasticSlider { Label = "Zoom", Value = 0 };
        slider.Marks.Add(new SliderMark(50, "50%"));
        var (_, window) = Show(slider);
        try
        {
            var (origin, y) = RailPoint(slider, window);
            // 5px left of the 50 mark (1.5 units on a 300px track) snaps into it.
            window.MouseDown(new Point(origin.X + 12 + 145, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            slider.Value.ShouldBe(50);
            window.MouseMove(new Point(origin.X + 12 + 135, y));
            Dispatcher.UIThread.RunJobs();
            slider.Value.ShouldBe(45);
            window.MouseUp(new Point(origin.X + 12 + 135, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            slider.Classes.Contains(":marks").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Step_And_Show_The_Tag_And_Hide_The_Readout()
    {
        var commits = new List<double>();
        var (slider, window) = Show(new ElasticSlider { Label = "Brightness", Value = 40, ValueFormatter = v => $"{v:0}%" });
        slider.ValueCommitted += (_, e) => commits.Add(e.Value);
        try
        {
            var thumb = slider.Rail!.Thumbs[0];
            AutomationProperties.GetName(thumb).ShouldBe("Brightness");
            thumb.RenderTransform!.Value.M31.ShouldBe(120, 0.01);
            thumb.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            slider.Value.ShouldBe(31);
            slider.IsTagShown.ShouldBeTrue();
            slider.Classes.Contains(":tag").ShouldBeTrue();
            slider.ValueText.ShouldBe("31%");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            slider.Value.ShouldBe(100);
            commits.ShouldBe([41, 31, 100]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Velocity_Tracker_Ignores_Paused_Releases()
    {
        var tracker = new SliderVelocityTracker();
        tracker.Add(1000, 0);
        tracker.Add(1016, 10);
        tracker.Add(1032, 20);
        tracker.Velocity(1040).ShouldBe(625, 0.001);
        tracker.Velocity(1100).ShouldBe(0);
    }
}
