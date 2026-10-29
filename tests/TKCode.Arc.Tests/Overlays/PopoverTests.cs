using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Overlays;

public class PopoverTests
{
    [AvaloniaFact]
    public void Trigger_Click_Toggles_And_Focus_Moves_In_And_Back()
    {
        var trigger = new Button { Content = "Share", Variant = ButtonVariant.Secondary };
        var field = new TextBox();
        var popover = new Popover { Trigger = trigger, Content = new StackPanel { Children = { field } } };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Children = { popover } });
        try
        {
            trigger.IsPressScaleEnabled.ShouldBeFalse();
            trigger.Focus();
            trigger.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, trigger));
            Dispatcher.UIThread.RunJobs();
            popover.IsOpen.ShouldBeTrue();
            popover.Popup!.IsOpen.ShouldBeTrue();
            popover.Popup.MarginToAnchor.ShouldBe(6);
            field.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            popover.IsOpen.ShouldBeFalse();
            popover.Popup.IsOpen.ShouldBeFalse();
            trigger.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Attached_Popover_Opens_On_Any_Control_And_Closes_On_Outside_Press()
    {
        var target = new Button { Content = "Filters" };
        var popover = new Popover { Content = "Anyone with the link can view.", Placement = PopoverPlacement.Top };
        Popover.SetAttach(target, popover);
        var other = new Border { Width = 200, Height = 80, Background = Avalonia.Media.Brushes.Transparent };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40, 120, 40, 0), Children = { target, other } });
        try
        {
            target.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, target));
            Dispatcher.UIThread.RunJobs();
            popover.IsOpen.ShouldBeTrue();
            popover.Parent.ShouldBe(target);
            popover.Surface!.Content.ShouldBe("Anyone with the link can view.");
            var surfaceBottom = popover.Surface.TranslatePoint(new Point(0, popover.Surface.Bounds.Height), window)!.Value.Y;
            var targetTop = target.TranslatePoint(new Point(0, 0), window)!.Value.Y;
            surfaceBottom.ShouldBeLessThanOrEqualTo(targetTop);
            var point = other.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            popover.IsOpen.ShouldBeFalse();
            popover.Parent.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShowAt_Anchors_Code_Content_To_Any_Control()
    {
        var anchor = new Border { Width = 40, Height = 40 };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { anchor } });
        try
        {
            var popover = new Popover { Content = "Reactions" };
            OverlayClosedEventArgs? closed = null;
            popover.Closed += (_, e) => closed = e;
            popover.ShowAt(anchor);
            Dispatcher.UIThread.RunJobs();
            popover.IsOpen.ShouldBeTrue();
            popover.Anchor.ShouldBe(anchor);
            popover.CloseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            popover.IsOpen.ShouldBeFalse();
            closed!.Reason.ShouldBe(DismissReason.Programmatic);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inside_A_Dialog_Focus_Can_Enter_The_Popover_And_A_Scrim_Press_Only_Closes_The_Popover()
    {
        var trigger = new Button { Content = "Share" };
        var field = new TextBox();
        var popover = new Popover { Trigger = trigger, Content = new StackPanel { Children = { field } } };
        var dialog = new Dialog { Title = "Settings", Content = popover };
        var window = TestHelpers.Show(new StackPanel { Children = { dialog } });
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            trigger.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, trigger));
            Dispatcher.UIThread.RunJobs();
            popover.IsOpen.ShouldBeTrue();
            field.IsFocused.ShouldBeTrue();
            window.MouseDown(new Point(3, 3), MouseButton.Left);
            window.MouseUp(new Point(3, 3), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            popover.IsOpen.ShouldBeFalse();
            dialog.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Exit_Plays_On_The_Popup_Frame_Before_It_Closes_And_A_Reopen_Reverses()
    {
        var trigger = new Button { Content = "Share" };
        var popover = new Popover { Trigger = trigger, Content = "Anyone with the link can view." };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Children = { popover } }, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            popover.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var actor = popover.Surface!.GetVisualAncestors().OfType<ArcPopupFrame>().First();
            actor.Opacity.ShouldBe(0, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            actor.Opacity.ShouldBeGreaterThan(0.2);
            clock.Advance(TimeSpan.FromSeconds(1));
            actor.Opacity.ShouldBe(1, 0.001);
            popover.IsOpen = false;
            clock.Advance(TimeSpan.FromMilliseconds(60));
            popover.Popup!.IsOpen.ShouldBeTrue();
            var mid = actor.Opacity;
            mid.ShouldBeLessThan(1);
            popover.IsOpen = true;
            clock.Advance(TimeSpan.FromMilliseconds(16));
            actor.Opacity.ShouldBeGreaterThanOrEqualTo(mid);
            popover.IsOpen = false;
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            popover.Popup.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
