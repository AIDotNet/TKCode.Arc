using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Messages;

public class ToastTests
{
    [AvaloniaFact]
    public void Default_Toast_Is_416_Wide_With_Success_Check()
    {
        var toast = new Toast { Title = "Changes saved" };
        var window = TestHelpers.Show(new StackPanel { Children = { toast } });
        window.Width = 800;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        try
        {
            toast.Bounds.Width.ShouldBe(416, 0.5);
            var check = TestHelpers.Descendant<TKCode.Arc.Primitives.StrokeDrawGlyph>(toast);
            check.Progress.ShouldBe(1);
            check.StrokeThickness.ShouldBe(2.25);
            var close = toast.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Close");
            close.Bounds.Width.ShouldBe(32, 0.5);
            AutomationProperties.GetName(close).ShouldBe("Dismiss notification");
            AutomationProperties.GetLiveSetting(toast).ShouldBe(AutomationLiveSetting.Polite);
            toast.IsShowing.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Container_Shrinks_The_Toast()
    {
        var toast = new Toast { Title = "Changes saved" };
        var window = TestHelpers.Show(new StackPanel { Width = 300, Children = { toast } });
        try
        {
            toast.Bounds.Width.ShouldBe(300, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Close_Button_Closes_With_Reason_And_Collapses()
    {
        var toast = new Toast { Title = "Changes saved", Description = "Synced to all devices." };
        var window = TestHelpers.Show(new StackPanel { Children = { toast } });
        try
        {
            ToastCloseReason? reason = null;
            toast.Closed += (_, e) => reason = e.Reason;
            var close = toast.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Close");
            close.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reason.ShouldBe(ToastCloseReason.Dismissed);
            toast.IsOpen.ShouldBeFalse();
            window.UpdateLayout();
            toast.Bounds.Height.ShouldBe(0, 0.5);
            toast.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            toast.Bounds.Height.ShouldBeGreaterThan(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Swipe_Past_Threshold_Dismisses()
    {
        var toast = new Toast { Title = "Changes saved", Duration = TimeSpan.Zero };
        var window = TestHelpers.Show(new StackPanel { Children = { toast } }, Motion.ArcMotionLevel.Full);
        try
        {
            ToastCloseReason? reason = null;
            toast.Closed += (_, e) => reason = e.Reason;
            var start = new Point(100, 30);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(10, 0));
            window.MouseMove(start + new Point(60, 0));
            window.MouseMove(start + new Point(120, 0));
            window.MouseUp(start + new Point(120, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            reason.ShouldBe(ToastCloseReason.Swiped);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Short_Swipe_Springs_Back()
    {
        var toast = new Toast { Title = "Changes saved", Duration = TimeSpan.Zero };
        var window = TestHelpers.Show(new StackPanel { Children = { toast } }, Motion.ArcMotionLevel.Full);
        try
        {
            var start = new Point(100, 30);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(10, 0));
            window.MouseMove(start + new Point(30, 0));
            toast.OffsetX.ShouldBe(20, 0.5);
            Thread.Sleep(80);
            window.MouseUp(start + new Point(30, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            toast.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Swipe_Tracker_Estimates_Velocity_And_Projects()
    {
        var tracker = new ToastSwipeTracker();
        tracker.Begin(new Point(0, 0), 0);
        tracker.Move(new Point(2, 0), 0).ShouldBeFalse();
        tracker.Move(new Point(10, 1), 10).ShouldBeTrue();
        tracker.Move(new Point(30, 1), 20);
        tracker.Move(new Point(50, 1), 30);
        tracker.RawOffset.ShouldBe(40);
        tracker.End(31, cancelled: false).ShouldBe(2000, 1);
        ToastSwipeTracker.Project(1000).ShouldBe(99, 0.01);
        ToastSwipeTracker.RubberBand(100, 300).ShouldBeLessThan(100);

        tracker.Begin(new Point(0, 0), 0);
        tracker.Move(new Point(1, 10), 5);
        tracker.IsTracking.ShouldBeFalse();
    }
}
