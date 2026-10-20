using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Messages;

public class ToastStackTests
{
    private static (Window Window, ToastStack Stack) ShowStack(ToastStack? stack = null)
    {
        stack ??= new ToastStack();
        var root = new Grid { Children = { new Border(), stack } };
        var window = TestHelpers.Show(root);
        window.Width = 800;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, stack);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Toasts_Stack_With_Depth_And_Peek()
    {
        var (window, stack) = ShowStack();
        try
        {
            stack.Show(new ToastOptions { Title = "First" });
            stack.Show(new ToastOptions { Title = "Second", Kind = ToastKind.Success });
            stack.Show(new ToastOptions { Title = "Third", Kind = ToastKind.Error, Description = "Try again." });
            Settle(window);
            stack.Count.ShouldBe(3);
            var items = stack.Panel!.Present;
            items[0].Title.ShouldBe("Third");
            items[0].LiftY.ShouldBe(0);
            items[0].DepthScale.ShouldBe(1);
            items[1].LiftY.ShouldBe(-14);
            items[1].DepthScale.ShouldBe(0.95, 0.001);
            items[2].LiftY.ShouldBe(-28);
            items[2].DepthScale.ShouldBe(0.9, 0.001);
            // Tucked toasts take the front toast's height and hide their content.
            items[1].CardHeight.ShouldBe(items[0].NaturalHeight, 0.5);
            items[1].ContentOpacity.ShouldBe(0);
            items[1].Classes.ShouldContain(":tucked");
            items[0].Classes.ShouldContain(":front");
            stack.Panel.ListWidth.ShouldBe(360);
            stack.Panel.ListRect.Right.ShouldBe(800 - 24, 0.5);
            stack.Panel.ListRect.Bottom.ShouldBe(600 - 24, 0.5);
            stack.Panel.ListHeight.ShouldBe(items[0].NaturalHeight + 28, 0.5);
            AutomationProperties.GetName(stack).ShouldBe("Notifications (Alt+T)");
            stack.AssertiveRegion!.LastAnnouncement.ShouldBe("Error: Third. Try again.");
            stack.PoliteRegion!.LastAnnouncement.ShouldBe("Success: Second");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Fans_The_Stack_Out_And_Pauses_Timers()
    {
        var (window, stack) = ShowStack();
        try
        {
            stack.Show(new ToastOptions { Title = "First" });
            stack.Show(new ToastOptions { Title = "Second" });
            Settle(window);
            var items = stack.Panel!.Present;
            items[0].IsTimerRunning.ShouldBeTrue();
            var rect = stack.Panel.ListRect;
            window.MouseMove(rect.Center);
            Settle(window);
            stack.IsExpanded.ShouldBeTrue();
            stack.IsPaused.ShouldBeTrue();
            items[0].IsTimerRunning.ShouldBeFalse();
            items[1].LiftY.ShouldBe(-(items[0].NaturalHeight + 12), 0.5);
            items[1].DepthScale.ShouldBe(1);
            items[1].ContentOpacity.ShouldBe(1);
            window.MouseMove(new Point(10, 10));
            Settle(window);
            stack.IsExpanded.ShouldBeFalse();
            items[0].IsTimerRunning.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Visible_Toasts_Limit_Hides_Older_Ones_And_Limit_Drops_Oldest()
    {
        var (window, stack) = ShowStack(new ToastStack { VisibleToasts = 2, Limit = 3 });
        try
        {
            for (var i = 0; i < 5; i++)
            {
                stack.Show(new ToastOptions { Title = $"Toast {i}" });
            }
            Settle(window);
            stack.Count.ShouldBe(3);
            var items = stack.Panel!.Present;
            items.Count.ShouldBe(3);
            items[0].Title.ShouldBe("Toast 4");
            items[2].IsInert.ShouldBeTrue();
            items[2].Opacity.ShouldBe(0);
            items[1].IsInert.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Update_Morphs_In_Place_And_Loading_Has_No_Timer()
    {
        var (window, stack) = ShowStack();
        try
        {
            var id = stack.Show(new ToastOptions { Title = "Uploading", Kind = ToastKind.Loading });
            Settle(window);
            var item = stack.Panel!.Present[0];
            item.IsLoading.ShouldBeTrue();
            item.IsTimerRunning.ShouldBeFalse();
            stack.Update(id, new ToastUpdate { Kind = ToastKind.Success, Title = "Uploaded" });
            Settle(window);
            stack.Count.ShouldBe(1);
            stack.Panel.Present[0].ShouldBeSameAs(item);
            item.IsLoading.ShouldBeFalse();
            item.Title.ShouldBe("Uploaded");
            item.Glyph.ShouldBe(MessagesIcons.CircleCheck);
            item.IsTimerRunning.ShouldBeTrue();
            // Reusing an id updates instead of stacking.
            stack.Show(new ToastOptions { Id = id, Title = "Uploaded again" });
            stack.Count.ShouldBe(1);
            item.Title.ShouldBe("Uploaded again");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Action_Runs_Then_Closes_Unless_It_Updates()
    {
        var (window, stack) = ShowStack();
        try
        {
            var ran = 0;
            var id = stack.Show(new ToastOptions { Title = "Archived", Action = new ToastAction("Undo", _ => ran++) });
            Settle(window);
            var item = stack.Panel!.Present[0];
            item.HasAction.ShouldBeTrue();
            item.ActionButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle(window);
            ran.ShouldBe(1);
            stack.Count.ShouldBe(0);

            var undo = stack.Show(new ToastOptions
            {
                Title = "Deleted",
                Action = new ToastAction("Undo", toastId => stack.Update(toastId, new ToastUpdate { Title = "Restored", RemoveAction = true }))
            });
            Settle(window);
            stack.Panel.Present[0].ActionButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle(window);
            stack.Count.ShouldBe(1);
            stack.Panel.Present[0].Title.ShouldBe("Restored");
            stack.Panel.Present[0].HasAction.ShouldBeFalse();
            stack.Dismiss(undo);
            Settle(window);
            stack.Count.ShouldBe(0);
            stack.Panel.Children.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Alt_T_Focuses_The_Front_Toast_And_Escape_Dismisses()
    {
        var (window, stack) = ShowStack();
        try
        {
            stack.Show(new ToastOptions { Title = "Older" });
            stack.Show(new ToastOptions { Title = "Newer" });
            Settle(window);
            window.KeyPressQwerty(PhysicalKey.T, RawInputModifiers.Alt);
            Settle(window);
            stack.IsExpanded.ShouldBeTrue();
            var front = stack.Panel!.Present[0];
            front.CloseButton!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle(window);
            stack.Count.ShouldBe(1);
            stack.Panel.Present[0].Title.ShouldBe("Older");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Show_Uses_The_Placed_Stack_Or_Creates_One()
    {
        var (window, stack) = ShowStack();
        try
        {
            var id = Toast.Show(stack, "Saved", ToastKind.Success);
            stack.Count.ShouldBe(1);
            Toast.Dismiss(stack, id);
            stack.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }

        var host = new Border();
        var plain = TestHelpers.Show(host);
        try
        {
            var created = ToastHost.GetStack(host);
            created.IsAutoCreated.ShouldBeTrue();
            ToastHost.GetService(host).ShouldBeSameAs(created);
            Toast.Show(host, "Hello", ToastKind.Info, description: "From the overlay");
            Dispatcher.UIThread.RunJobs();
            plain.UpdateLayout();
            created.Count.ShouldBe(1);
            created.Bounds.Width.ShouldBeGreaterThan(0);
        }
        finally
        {
            plain.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Windows_Center_The_Stack()
    {
        var (window, stack) = ShowStack(new ToastStack { Position = ToastStackPosition.BottomLeft });
        try
        {
            window.Width = 400;
            stack.Show(new ToastOptions { Title = "Saved" });
            Settle(window);
            var rect = stack.Panel!.ListRect;
            rect.Width.ShouldBe(360, 0.5);
            rect.X.ShouldBe(20, 0.5);
            rect.Bottom.ShouldBe(window.Height - 16, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
