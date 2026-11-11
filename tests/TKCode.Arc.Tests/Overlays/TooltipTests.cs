using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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

    private static Point Center(Control target, Window window)
        => target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

    private static bool OpensSoon(Control target) => TestHelpers.PumpUntil(() => Tooltip.GetPopup(target)?.IsOpen == true, 3000);

    [AvaloniaFact]
    public void ShowOnDisabled_Shows_The_Tip_Over_A_Disabled_Control()
    {
        // Issue #110: a disabled button could not say why it cannot be used.
        var target = new Button { Content = "Publish", IsEnabled = false };
        Tooltip.SetTip(target, "Add a title first");
        Tooltip.SetShowOnDisabled(target, true);
        Tooltip.SetShowDelay(target, TimeSpan.Zero);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { target } });
        try
        {
            window.MouseMove(new Point(5, 5));
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(Center(target, window));
            OpensSoon(target).ShouldBeTrue();
            ((Tooltip)Tooltip.GetPopup(target)!.Surface!).Content.ShouldBe("Add a title first");

            window.MouseMove(new Point(5, 5));
            Dispatcher.UIThread.RunJobs();
            Tooltip.GetPopup(target)!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_ShowOnDisabled_A_Disabled_Control_Shows_No_Tip()
    {
        var target = new Button { Content = "Publish", IsEnabled = false };
        Tooltip.SetTip(target, "Add a title first");
        Tooltip.SetShowDelay(target, TimeSpan.Zero);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { target } });
        try
        {
            window.MouseMove(new Point(5, 5));
            window.MouseMove(Center(target, window));
            OpensSoon(target).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Enabled_Control_With_ShowOnDisabled_Uses_Its_Own_Pointer_Events()
    {
        var target = new Button { Content = "Publish", IsEnabled = false };
        Tooltip.SetTip(target, "Publish now");
        Tooltip.SetShowOnDisabled(target, true);
        Tooltip.SetShowDelay(target, TimeSpan.Zero);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { target } });
        try
        {
            target.IsEnabled = true;
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(new Point(5, 5));
            window.MouseMove(Center(target, window));
            OpensSoon(target).ShouldBeTrue();
            window.MouseMove(new Point(5, 5));
            Dispatcher.UIThread.RunJobs();
            Tooltip.GetPopup(target)!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
