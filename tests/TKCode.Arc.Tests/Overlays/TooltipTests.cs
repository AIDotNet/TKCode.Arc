using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Overlays;

public class TooltipTests
{
    [AvaloniaFact]
    public void Programmatic_Open_Shows_The_Bubble_With_Its_Text()
    {
        var target = new Button { Content = "Share" };
        Tooltip.SetTip(target, "Copy link");
        var window = TestHelpers.Show(new StackPanel { Margin = new Avalonia.Thickness(60), Children = { target } });
        try
        {
            Tooltip.SetIsOpen(target, true);
            Dispatcher.UIThread.RunJobs();
            var popup = Tooltip.GetPopup(target);
            popup.ShouldNotBeNull();
            popup!.IsOpen.ShouldBeTrue();
            ((Tooltip)popup.Surface!).Content.ShouldBe("Copy link");
            Tooltip.SetTip(target, "Copied");
            Dispatcher.UIThread.RunJobs();
            ((Tooltip)popup.Surface!).Content.ShouldBe("Copied");
            Tooltip.SetIsOpen(target, false);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }
}
