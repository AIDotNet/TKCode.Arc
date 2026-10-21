using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
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

    private sealed class Item
    {
        public bool On { get; set; }
    }
}
