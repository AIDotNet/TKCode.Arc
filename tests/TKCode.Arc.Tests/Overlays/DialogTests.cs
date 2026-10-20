using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Overlays;

public class DialogTests
{
    private static (Window Window, Dialog Dialog, Button Opener) Setup(Action<Dialog>? configure = null)
    {
        var opener = new Button { Content = "Open" };
        var dialog = new Dialog { Title = "Rename project", Description = "Pick a short name.", Content = new TextBox { Text = "Atlas" } };
        configure?.Invoke(dialog);
        var window = TestHelpers.Show(new StackPanel { Children = { opener, dialog } });
        opener.Focus();
        return (window, dialog, opener);
    }

    [AvaloniaFact]
    public void Declared_Dialog_Takes_No_Space_And_Mounts_Its_Panel_On_The_Overlay_Layer()
    {
        var (window, dialog, _) = Setup();
        try
        {
            dialog.IsVisible.ShouldBeFalse();
            dialog.Surface.ShouldBeNull();
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = dialog.Surface.ShouldBeOfType<DialogSurface>();
            dialog.Presence!.State.ShouldBe(ModalState.Open);
            var host = dialog.Presence.Host;
            host.GetVisualParent().ShouldNotBeNull();
            surface.IsEffectivelyVisible.ShouldBeTrue();
            // Arc: min(viewport − 32, 440); the test window is 400 wide.
            surface.Bounds.Width.ShouldBe(368, 0.5);
            surface.Title.ShouldBe("Rename project");
            // Content stays in the dialog's logical tree, rendered inside the surface.
            ((Control)dialog.Content!).Parent.ShouldBe(dialog);
            ((Control)dialog.Content!).IsAttachedToVisualTree().ShouldBeTrue();
            host.Scrim.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_Moves_Focus_In_And_Closing_Returns_It()
    {
        var (window, dialog, opener) = Setup();
        try
        {
            opener.IsFocused.ShouldBeTrue();
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var focused = TopLevel.GetTopLevel(opener)!.FocusManager!.GetFocusedElement();
            dialog.Presence!.Contains(focused!).ShouldBeTrue();
            dialog.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            opener.IsFocused.ShouldBeTrue();
            dialog.Presence.State.ShouldBe(ModalState.Closed);
            dialog.Presence.Host.GetVisualParent().ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Closes_And_Can_Be_Cancelled()
    {
        var (window, dialog, _) = Setup();
        try
        {
            var cancel = true;
            OverlayClosingEventArgs? closing = null;
            dialog.Closing += (_, e) =>
            {
                closing = e;
                e.Cancel = cancel;
            };
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            closing.ShouldNotBeNull();
            closing!.Reason.ShouldBe(DismissReason.Escape);
            dialog.IsOpen.ShouldBeTrue();
            cancel = false;
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeFalse();
            dialog.LastCloseReason.ShouldBe(DismissReason.Escape);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Close_Button_And_Result_Buttons_Close_With_A_Result()
    {
        var save = new Button { Content = "Save" };
        Dialog.SetResult(save, "saved");
        var (window, dialog, _) = Setup(d => d.Actions = save);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            save.Parent.ShouldBe(dialog);
            save.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, save));
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeFalse();
            dialog.CloseResult.ShouldBe("saved");
            dialog.LastCloseReason.ShouldBe(DismissReason.Action);

            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var close = dialog.Surface!.CloseButton!;
            Avalonia.Automation.AutomationProperties.GetName(close).ShouldBe("Close dialog");
            close.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, close));
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeFalse();
            dialog.LastCloseReason.ShouldBe(DismissReason.CloseButton);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrim_Press_Closes_Unless_Disabled()
    {
        var (window, dialog, _) = Setup(d => d.CloseOnOutsidePress = false);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(new Point(4, 4), MouseButton.Left);
            window.MouseUp(new Point(4, 4), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeTrue();
            dialog.CloseOnOutsidePress = true;
            window.MouseDown(new Point(4, 4), MouseButton.Left);
            window.MouseUp(new Point(4, 4), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeFalse();
            dialog.LastCloseReason.ShouldBe(DismissReason.PressOutside);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ShowAsync_From_Code_Returns_The_Clicked_Result()
    {
        var anchor = new Border();
        var window = TestHelpers.Show(anchor);
        try
        {
            var task = Dialog.ShowAsync(anchor, "Delete project", null, "This cannot be undone.",
                new DialogAction("Cancel", false), new DialogAction("Delete", true, ButtonVariant.Danger));
            Dispatcher.UIThread.RunJobs();
            task.IsCompleted.ShouldBeFalse();
            var delete = window.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Delete"));
            delete.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, delete));
            Dispatcher.UIThread.RunJobs();
            (await task).ShouldBe(true);
            ModalPresence.GetModals(window).Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stacked_Dialogs_Only_The_Topmost_Answers_Escape()
    {
        var inner = new Dialog { Title = "Inner" };
        var (window, outer, _) = Setup(d => d.Content = new StackPanel { Children = { new TextBox(), inner } });
        try
        {
            outer.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            inner.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            ModalPresence.GetModals(window).Count.ShouldBe(2);
            inner.Presence!.IsTopmost.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            inner.IsOpen.ShouldBeFalse();
            outer.IsOpen.ShouldBeTrue();
            outer.Presence!.IsTopmost.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sizes_Change_The_Panel_Width_Within_The_Viewport()
    {
        var (window, dialog, _) = Setup(d => d.Size = ControlSize.Small);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            dialog.Surface!.Bounds.Width.ShouldBe(360, 0.5);
            dialog.Size = ControlSize.Large;
            Dispatcher.UIThread.RunJobs();
            // The test window is 400 wide: 560 is capped at the viewport minus 32.
            dialog.Surface.Bounds.Width.ShouldBe(368, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
