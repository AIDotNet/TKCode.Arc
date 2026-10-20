using Avalonia.Controls;
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
}
