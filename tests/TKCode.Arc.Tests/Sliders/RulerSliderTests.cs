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

public class RulerSliderTests
{
    private static (RulerSlider Ruler, Window Window) Show(RulerSlider ruler, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        ruler.Width = 402; // tape = 400 between the 1px edges
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { ruler } }, level);
        return (ruler, window);
    }

    private static Point TapeCenter(RulerSlider ruler, Window window)
    {
        var tape = ruler.Tape!;
        return tape.TranslatePoint(new Point(tape.Bounds.Width / 2, tape.Bounds.Height / 2), window)!.Value;
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

    [AvaloniaFact]
    public void Default_Geometry_Follows_Arc()
    {
        var (ruler, window) = Show(new RulerSlider { Label = "Height", Value = 40 });
        try
        {
            var tape = ruler.Tape!;
            tape.Bounds.Height.ShouldBe(70);
            tape.SmallTickHeight.ShouldBe(12);
            tape.HalfTickHeight.ShouldBe(20);
            tape.MajorTickHeight.ShouldBe(28);
            tape.NeedleHeight.ShouldBe(36);
            ruler.Spacing.ShouldBe(10);
            ruler.Ticks.ShouldBe(10);
            ruler.TapeOffset.ShouldBe(400);
            ruler.TapeLength.ShouldBe(1000);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Step_Jump_And_Commit()
    {
        var committed = new List<double>();
        var ruler = new RulerSlider { Label = "Height", Value = 50 };
        ruler.ValueCommitted += (_, e) => committed.Add(e.Value);
        var (_, window) = Show(ruler);
        try
        {
            ruler.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            ruler.Value.ShouldBe(51);
            Press(window, PhysicalKey.ArrowDown);
            ruler.Value.ShouldBe(50);
            // The large step defaults to one labelled tick.
            Press(window, PhysicalKey.PageUp);
            ruler.Value.ShouldBe(60);
            Press(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            ruler.Value.ShouldBe(50);
            Press(window, PhysicalKey.End);
            ruler.Value.ShouldBe(100);
            Press(window, PhysicalKey.ArrowRight);
            ruler.Value.ShouldBe(100);
            Press(window, PhysicalKey.Home);
            ruler.Value.ShouldBe(0);
            committed.ShouldBe([51, 50, 60, 50, 100, 0]);

            ruler.LargeChange = 25;
            Press(window, PhysicalKey.PageUp);
            ruler.Value.ShouldBe(25);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Moves_The_Tape_One_To_One_The_Opposite_Way()
    {
        var committed = new List<double>();
        var ruler = new RulerSlider { Label = "Height", Value = 50 };
        ruler.ValueCommitted += (_, e) => committed.Add(e.Value);
        var (_, window) = Show(ruler);
        try
        {
            var from = TapeCenter(ruler, window);
            var to = from - new Point(123, 0);
            Drag(window, from, to);
            ruler.ShownValue.ShouldBe(62.3, 0.001);
            ruler.Value.ShouldBe(62);
            ruler.Classes.Contains(RulerSlider.DraggingPseudoClass).ShouldBeTrue();
            committed.ShouldBeEmpty();
            Release(window, to);
            ruler.Classes.Contains(RulerSlider.DraggingPseudoClass).ShouldBeFalse();
            ruler.ShownValue.ShouldBe(62, 0.001);
            committed.ShouldBe([62]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Tap_Brings_The_Tapped_Tick_To_The_Needle()
    {
        var committed = new List<double>();
        var ruler = new RulerSlider { Label = "Height", Value = 50 };
        ruler.ValueCommitted += (_, e) => committed.Add(e.Value);
        var (_, window) = Show(ruler);
        try
        {
            var at = TapeCenter(ruler, window) + new Point(82, 0);
            window.MouseMove(at, RawInputModifiers.None);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            ruler.Value.ShouldBe(50);
            Release(window, at);
            ruler.Value.ShouldBe(58);
            committed.ShouldBe([58]);
            ruler.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Vertical_Drag_Is_Left_To_The_Page()
    {
        var committed = 0;
        var ruler = new RulerSlider { Label = "Height", Value = 50 };
        ruler.ValueCommitted += (_, _) => committed++;
        var (_, window) = Show(ruler);
        try
        {
            var from = TapeCenter(ruler, window) + new Point(40, 0);
            var to = from + new Point(0, 30);
            Drag(window, from, to);
            Release(window, to);
            ruler.Value.ShouldBe(50);
            committed.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Full_Motion_The_Tape_Stops_At_The_Ends()
    {
        var ruler = new RulerSlider { Label = "Height", Value = 5 };
        var (_, window) = Show(ruler, ArcMotionLevel.Reduced);
        try
        {
            var from = TapeCenter(ruler, window);
            var to = from + new Point(300, 0);
            Drag(window, from, to);
            ruler.ShownValue.ShouldBe(0);
            ruler.Value.ShouldBe(0);
            Release(window, to);
            ruler.Value.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Past_An_End_The_Band_Gives_At_Most_44_Pixels_And_Pulls_Home()
    {
        var committed = new List<double>();
        var ruler = new RulerSlider { Label = "Height", Value = 5 };
        ruler.ValueCommitted += (_, e) => committed.Add(e.Value);
        var (_, window) = Show(ruler, ArcMotionLevel.Full);
        try
        {
            var from = TapeCenter(ruler, window);
            var to = from + new Point(300, 0);
            Drag(window, from, to);
            ruler.Value.ShouldBe(0);
            ruler.TapeOffset.ShouldBeLessThan(-1);
            ruler.TapeOffset.ShouldBeGreaterThan(-RulerSlider.Stretch);
            Release(window, to);
            TestHelpers.PumpUntil(() => committed.Count == 1, 3000).ShouldBeTrue();
            ruler.TapeOffset.ShouldBe(0, 0.01);
            committed.ShouldBe([0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Flick_Coasts_Onto_A_Tick_And_Rolls_The_Value_On_The_Way()
    {
        var committed = new List<double>();
        var changes = new List<double>();
        var ruler = new RulerSlider { Label = "Height", Value = 50 };
        ruler.ValueCommitted += (_, e) => committed.Add(e.Value);
        var (_, window) = Show(ruler, ArcMotionLevel.Full);
        try
        {
            ruler.ValueChanged += (_, e) => changes.Add(e.NewValue);
            // 50 ticks a second lands 0.42 s of travel further: 21 ticks.
            ruler.Release(50 * RulerSlider.UnitsPerTick);
            ruler.IsCoasting.ShouldBeTrue();
            TestHelpers.PumpUntil(() => committed.Count == 1, 5000).ShouldBeTrue();
            committed.ShouldBe([71]);
            ruler.ShownValue.ShouldBe(71, 0.001);
            ruler.IsCoasting.ShouldBeFalse();
            changes.Count.ShouldBeGreaterThan(1);
            changes.ShouldBe(changes.OrderBy(v => v).ToList());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Flick_Past_The_End_Lands_On_The_End()
    {
        var committed = new List<double>();
        var ruler = new RulerSlider { Label = "Height", Value = 95 };
        ruler.ValueCommitted += (_, e) => committed.Add(e.Value);
        var (_, window) = Show(ruler, ArcMotionLevel.Full);
        try
        {
            ruler.Release(200 * RulerSlider.UnitsPerTick);
            TestHelpers.PumpUntil(() => committed.Count == 1, 5000).ShouldBeTrue();
            committed.ShouldBe([100]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_A_Coasting_Tape_Catches_It()
    {
        var committed = new List<double>();
        var ruler = new RulerSlider { Label = "Height", Value = 20 };
        ruler.ValueCommitted += (_, e) => committed.Add(e.Value);
        var (_, window) = Show(ruler, ArcMotionLevel.Full);
        try
        {
            ruler.Release(100 * RulerSlider.UnitsPerTick);
            var at = TapeCenter(ruler, window);
            window.MouseMove(at, RawInputModifiers.None);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            ruler.IsCoasting.ShouldBeFalse();
            committed.ShouldBeEmpty();
            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Outside_Value_Moves_The_Tape_And_Shows_On_A_Tick()
    {
        var committed = 0;
        var ruler = new RulerSlider { Label = "Height", Value = 10, SmallChange = 5 };
        ruler.ValueCommitted += (_, _) => committed++;
        var (_, window) = Show(ruler);
        try
        {
            ruler.Value = 42;
            Dispatcher.UIThread.RunJobs();
            ruler.ShownValue.ShouldBe(40);
            ruler.Readout!.Text.ShouldBe("40");
            committed.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Horizontal_Scrolling_Moves_The_Tape_And_Vertical_Is_Left_To_The_Page()
    {
        var ruler = new RulerSlider { Label = "Height", Value = 50 };
        var (_, window) = Show(ruler);
        try
        {
            var at = TapeCenter(ruler, window);
            window.MouseMove(at, RawInputModifiers.None);
            window.MouseWheel(at, new Vector(0, -3), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            ruler.Value.ShouldBe(50);
            // Content moving left brings higher values under the needle: 1 unit is 50 px, or 5 ticks.
            window.MouseWheel(at, new Vector(-1, 0), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            ruler.Value.ShouldBe(55);
            ruler.ShownValue.ShouldBe(55);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tick_Labels_Carry_Only_The_Decimals_Their_Spacing_Needs()
    {
        var ruler = new RulerSlider { Label = "Weight", Minimum = 30, Maximum = 150, SmallChange = 0.5, Value = 72.5 };
        var (_, window) = Show(ruler);
        try
        {
            ruler.MajorValue(1).ShouldBe(35);
            ruler.FormatLabel(35).ShouldBe("35");
            ruler.FormatValue(72.5).ShouldBe("72.5");
            ruler.MajorCount.ShouldBe(25);

            ruler.MajorEvery = 5;
            ruler.FormatLabel(32.5).ShouldBe("32.5");
            ruler.FormatLabel(35).ShouldBe("35");
            ruler.MajorValue(1).ShouldBe(32.5);

            ruler.ValueFormatter = v => $"{v:0} kg";
            ruler.FormatLabel(35).ShouldBe("35 kg");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Frames_Reuse_The_Ticks_And_A_Long_Tape_Keeps_Few_Labels()
    {
        var ruler = new RulerSlider { Label = "Distance", Maximum = 100000, Value = 500 };
        var (_, window) = Show(ruler);
        try
        {
            RenderFrame();
            var tape = ruler.Tape!;
            var ticks = tape.SmallTicks.ShouldNotBeNull();
            tape.HasCachedLabel(50).ShouldBeTrue();
            for (var value = 1000; value < 90000; value += 1000)
            {
                ruler.Value = value;
                RenderFrame();
            }
            tape.SmallTicks.ShouldBeSameAs(ticks);
            tape.CachedLabelCount.ShouldBeLessThanOrEqualTo(70);

            // A new rhythm draws new ticks.
            ruler.MajorEvery = 4;
            RenderFrame();
            tape.SmallTicks.ShouldNotBeSameAs(ticks);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Reports_A_Slider_And_Speaks_The_Unit()
    {
        var ruler = new RulerSlider { Label = "Height", Unit = "cm", Minimum = 120, Maximum = 220, Value = 172 };
        var (_, window) = Show(ruler);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(ruler);
            peer.GetName().ShouldBe("Height");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Slider);
            var range = peer.GetProvider<IRangeValueProvider>()!;
            range.Minimum.ShouldBe(120);
            range.Maximum.ShouldBe(220);
            range.Value.ShouldBe(172);
            AutomationProperties.GetItemStatus(ruler).ShouldBe("172 cm");
            range.SetValue(180.4);
            ruler.Value.ShouldBe(180);
            AutomationProperties.GetItemStatus(ruler).ShouldBe("180 cm");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Takes_No_Input()
    {
        var ruler = new RulerSlider { Label = "Height", Value = 50, IsEnabled = false };
        var (_, window) = Show(ruler);
        try
        {
            ruler.IsEffectivelyEnabled.ShouldBeFalse();
            var at = TapeCenter(ruler, window) + new Point(50, 0);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            ruler.Value.ShouldBe(50);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Shows_The_Ring()
    {
        var ruler = new RulerSlider { Label = "Height", Value = 50 };
        var (_, window) = Show(ruler);
        try
        {
            ruler.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.IsFocusRingVisible(ruler).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Minimum_Above_The_Current_Maximum_Can_Be_Set_First()
    {
        var ruler = new RulerSlider { Label = "Height" };
        Should.NotThrow(() =>
        {
            ruler.BeginInit();
            ruler.Minimum = 360;
            ruler.Maximum = 900;
            ruler.Value = 600;
            ruler.EndInit();
        });
        var (_, window) = Show(ruler);
        try
        {
            ruler.ShownValue.ShouldBe(600);
        }
        finally
        {
            window.Close();
        }
    }
}
