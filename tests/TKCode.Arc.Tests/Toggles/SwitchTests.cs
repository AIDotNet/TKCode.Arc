using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Toggles;

public class SwitchTests
{
    [AvaloniaFact]
    public void Checked_Moves_Thumb_To_The_End_Of_Its_Travel()
    {
        var toggle = new Switch { Content = "Notifications" };
        Motion.ArcMotion.SetLevel(toggle, Motion.ArcMotionLevel.Off);
        var window = TestHelpers.Show(toggle);
        try
        {
            toggle.ThumbMargin.Left.ShouldBe(0);
            toggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            toggle.ThumbMargin.Left.ShouldBe(18);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Data_Context_Moves_The_Thumb_At_Once_But_A_Toggle_Glides()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var toggle = new Switch { Content = "Notifications" };
        toggle.Bind(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(nameof(Item.On)));
        var host = new Border { Child = toggle, DataContext = new Item() };
        var window = TestHelpers.Show(host, Motion.ArcMotionLevel.Full);
        try
        {
            toggle.Classes.Contains(Motion.ArcMotion.FullPseudoClass).ShouldBeTrue();
            // A reused detail view gets the next item: not a toggle, no slide.
            host.DataContext = new Item { On = true };
            Dispatcher.UIThread.RunJobs();
            toggle.IsChecked.ShouldBe(true);
            toggle.ThumbMargin.Left.ShouldBe(18, 0.01);
            toggle.Classes.Contains(Motion.ArcMotion.FullPseudoClass).ShouldBeTrue();
            // A real toggle still glides.
            toggle.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            toggle.ThumbMargin.Left.ShouldBeGreaterThan(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Turning_Off_From_A_Press_Slides_Straight_To_The_Start()
    {
        var toggle = new Switch
        {
            Content = "Notifications",
            IsChecked = true,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        var window = TestHelpers.Show(toggle, Motion.ArcMotionLevel.Full);
        try
        {
            var point = new Point(20, 12);
            window.MouseDown(point, MouseButton.Left);
            TestHelpers.PumpUntil(() => toggle.ThumbMargin.Left < 13.5, 2000).ShouldBeTrue();
            window.MouseUp(point, MouseButton.Left);
            toggle.IsChecked.ShouldBe(false);
            // The press shifted the thumb toward the start; the release must keep heading there, not glide back to the
            // checked spot first (the off position once fell back to the default, which a running transition missed).
            var released = toggle.ThumbMargin.Left;
            var highest = released;
            TestHelpers.PumpUntil(() =>
            {
                highest = Math.Max(highest, toggle.ThumbMargin.Left);
                return toggle.ThumbMargin.Left < 0.5;
            }, 5000).ShouldBeTrue();
            highest.ShouldBeLessThan(released + 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class Item
    {
        public bool On { get; set; }
    }
}
