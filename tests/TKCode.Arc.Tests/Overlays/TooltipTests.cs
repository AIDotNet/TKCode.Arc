using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

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

    [AvaloniaFact]
    public void A_Control_As_Tip_Is_Shown_Itself_Not_Its_Type_Name()
    {
        var target = new Button { Content = "Share" };
        var tip = new TextBlock { Text = "Copy link" };
        Tooltip.SetTip(target, tip);
        var window = TestHelpers.Show(new StackPanel { Margin = new Avalonia.Thickness(60), Children = { target } });
        try
        {
            Tooltip.SetIsOpen(target, true);
            Dispatcher.UIThread.RunJobs();
            var bubble = (Tooltip)Tooltip.GetPopup(target)!.Surface!;
            var shown = bubble.GetVisualDescendants().OfType<TextBlock>().ToList();
            shown.ShouldContain(tip);
            shown.ShouldNotContain(t => t.Text == typeof(TextBlock).ToString());
            Tooltip.SetIsOpen(target, false);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }
}
