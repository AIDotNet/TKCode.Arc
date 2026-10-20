using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Sliders;

public class SliderTests
{
    private static (Slider Slider, Window Window) Show(Slider slider)
    {
        slider.Width = 322; // track = 300px between the 11px insets
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20, 60, 20, 20), Children = { slider } });
        return (slider, window);
    }

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static SliderThumb Thumb(Slider slider, int index) => slider.Rail!.Thumbs[index];

    [AvaloniaFact]
    public void Arrows_Page_And_Home_End_Step_The_Value()
    {
        var (slider, window) = Show(new Slider { Label = "Volume", Value = 50 });
        var commits = new List<double>();
        slider.ValueCommitted += (_, e) => commits.Add(e.Value);
        try
        {
            Thumb(slider, 0).Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight);
            slider.Value.ShouldBe(51);
            Key(window, PhysicalKey.PageUp);
            slider.Value.ShouldBe(61);
            Key(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            slider.Value.ShouldBe(51);
            Key(window, PhysicalKey.End);
            slider.Value.ShouldBe(100);
            Key(window, PhysicalKey.ArrowUp);
            slider.Value.ShouldBe(100);
            Key(window, PhysicalKey.Home);
            slider.Value.ShouldBe(0);
            commits.ShouldBe([51, 61, 51, 100, 0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Shows_The_Bubble_And_Positions_Follow_Values()
    {
        var (slider, window) = Show(new Slider { Label = "Volume", Value = 25 });
        try
        {
            slider.IsBubbleShown(0).ShouldBeFalse();
            slider.ShownPosition(0).ShouldBe(250);
            // Placed by a render transform: a quarter of the 300px track.
            Thumb(slider, 0).RenderTransform!.Value.M31.ShouldBe(75, 0.01);
            Thumb(slider, 0).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            slider.IsBubbleShown(0).ShouldBeTrue();
            AutomationProperties.GetName(Thumb(slider, 0)).ShouldBe("Volume");
            slider.Value = 75;
            Dispatcher.UIThread.RunJobs();
            slider.ShownPosition(0).ShouldBe(750);
            Thumb(slider, 0).Bounds.Width.ShouldBe(22);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Range_Thumbs_Respect_The_Minimum_Gap()
    {
        var (slider, window) = Show(new Slider
        {
            Label = "Price",
            IsRange = true,
            LowerValue = 20,
            UpperValue = 30,
            SmallChange = 5,
            MinStepsBetweenThumbs = 1
        });
        try
        {
            AutomationProperties.GetName(Thumb(slider, 0)).ShouldBe("Price, minimum");
            Thumb(slider, 1).IsVisible.ShouldBeTrue();
            Thumb(slider, 0).Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight);
            slider.LowerValue.ShouldBe(25);
            Key(window, PhysicalKey.ArrowRight);
            slider.LowerValue.ShouldBe(25);
            Key(window, PhysicalKey.End);
            slider.LowerValue.ShouldBe(25);
            Thumb(slider, 1).Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Home);
            slider.UpperValue.ShouldBe(30);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_The_Track_Moves_The_Nearest_Thumb_And_Dragging_Follows()
    {
        var (slider, window) = Show(new Slider { Label = "Volume", Value = 10 });
        try
        {
            var rail = slider.Rail!;
            var origin = rail.TranslatePoint(default, window)!.Value;
            var y = origin.Y + rail.Bounds.Height / 2;
            // 11px inset + 50% of 300px.
            window.MouseDown(new Point(origin.X + 11 + 150, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            slider.Value.ShouldBe(50);
            Thumb(slider, 0).IsFocused.ShouldBeTrue();
            slider.IsBubbleShown(0).ShouldBeTrue();
            window.MouseMove(new Point(origin.X + 11 + 240, y));
            Dispatcher.UIThread.RunJobs();
            slider.Value.ShouldBe(80);
            // Past the end the value clamps.
            window.MouseMove(new Point(origin.X + 400, y));
            Dispatcher.UIThread.RunJobs();
            slider.Value.ShouldBe(100);
            window.MouseUp(new Point(origin.X + 400, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            slider.Value.ShouldBe(100);
            slider.ShownPosition(0).ShouldBe(1000);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Formatter_Drives_Readout_And_Value_Text()
    {
        var (slider, window) = Show(new Slider { Label = "Budget", Value = 1250, Maximum = 5000, SmallChange = 50, ValueFormatter = v => $"${v:N0}" });
        try
        {
            var readout = TestHelpers.Descendant<SliderNumberText>(slider);
            readout.Text.ShouldBe("$1,250");
            readout.Digits.Prefix.ShouldBe("$");
            readout.Digits.Value.ShouldBe(1250);
            AutomationProperties.GetItemStatus(Thumb(slider, 0)).ShouldBe("$1,250");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Number_Text_Splits_Prefix_Number_And_Suffix()
    {
        SliderNumberText.TryParse("$1,250.5 /mo", out var prefix, out var number, out var decimals, out var grouping, out var suffix).ShouldBeTrue();
        (prefix, number, decimals, grouping, suffix).ShouldBe(("$", 1250.5, 1, true, " /mo"));
        SliderNumberText.TryParse("-5 dB", out prefix, out number, out _, out _, out suffix).ShouldBeTrue();
        (prefix, number, suffix).ShouldBe(("", -5.0, " dB"));
        SliderNumberText.TryParse("Low", out _, out _, out _, out _, out _).ShouldBeFalse();
        SliderNumberText.TryParse("1/2", out _, out _, out _, out _, out _).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Math_Matches_Arc()
    {
        SliderMath.Rubber(1000, 9).ShouldBeLessThan(9);
        SliderMath.Rubber(-20, 9).ShouldBe(-(1 - 1 / (20 * 0.55 / 9 + 1)) * 9, 1e-9);
        SliderMath.DecimalsOf(0.25).ShouldBe(2);
        SliderMath.Snap(12.5, 0, 5, 0).ShouldBe(15);
        SliderMath.DefaultLargeStep(0, 100, 1, 0).ShouldBe(10);
        SliderMath.DefaultLargeStep(0, 1, 0.05, 2).ShouldBe(0.1, 1e-9);
    }

    [AvaloniaFact]
    public void Clicking_A_Mark_Label_Moves_There()
    {
        var slider = new Slider { Label = "Speed", Value = 0, Maximum = 2, SmallChange = 0.25 };
        slider.Marks.Add(new SliderMark(0, "0×"));
        slider.Marks.Add(new SliderMark(1, "1×"));
        slider.Marks.Add(new SliderMark(2, "2×"));
        slider.Marks.Add(0.5);
        var (_, window) = Show(slider);
        try
        {
            slider.Classes.Contains(":marks").ShouldBeTrue();
            var labels = TestHelpers.Descendant<SliderMarkLabels>(slider);
            var origin = labels.TranslatePoint(default, window)!.Value;
            var point = new Point(origin.X + 11 + 150, origin.Y + 8);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            slider.Value.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Slider_Thumbs_Expose_Range_Values()
    {
        var slider = new Slider { Label = "Price", IsRange = true, LowerValue = 20, UpperValue = 60, SmallChange = 5 };
        var window = TestHelpers.Show(slider);
        try
        {
            var lower = ControlAutomationPeer.CreatePeerForElement(slider.Rail!.Thumbs[0]);
            lower.GetAutomationControlType().ShouldBe(AutomationControlType.Slider);
            var range = lower.GetProvider<IRangeValueProvider>()!;
            range.Value.ShouldBe(20);
            range.Maximum.ShouldBe(60);
            range.SmallChange.ShouldBe(5);
            range.SetValue(42);
            slider.LowerValue.ShouldBe(40);
        }
        finally
        {
            window.Close();
        }
    }
}
