using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

public class DialogToastTests
{
    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = new Window { Content = content, Width = 400, Height = 600 };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Border Part(Control root, string name)
        => root.GetVisualDescendants().OfType<Border>().Single(b => b.Name == name);

    private static Point CenterOf(Control control, Visual to)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), to)!.Value;

    private static void Tap(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Dispatcher timers only fire inside a running frame, so this pushes one.</summary>
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.PushFrame(frame);
    }

    private static bool WaitFor(Func<bool> condition, int milliseconds = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return true;
            }
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Pump(10);
        }
        Dispatcher.UIThread.RunJobs();
        return condition();
    }

    private static Dialog NewDialog(int actions, bool open = true)
    {
        var dialog = new Dialog { Title = "Delete photo?", Message = "This cannot be undone.", IsOpen = open };
        for (var i = 0; i < actions; i++)
        {
            dialog.Items.Add(new Button { Content = i == 0 ? "Cancel" : i == 1 ? "OK" : "Action " + i, Variant = ButtonVariant.Plain });
        }
        return dialog;
    }

    [AvaloniaFact]
    public void Dialog_Closed_Does_Not_Catch_Touches_And_Open_Shows_Panel_And_Scrim()
    {
        var dialog = NewDialog(2, open: false);
        var window = Show(new Page { Content = new TextBlock { Text = "Body" }, Overlay = dialog });
        try
        {
            dialog.IsHitTestVisible.ShouldBeFalse();
            Part(dialog, "PART_Panel").IsVisible.ShouldBeFalse();

            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            dialog.IsHitTestVisible.ShouldBeTrue();
            Part(dialog, "PART_Panel").IsVisible.ShouldBeTrue();
            Part(dialog, "PART_Panel").Opacity.ShouldBe(1);
            Part(dialog, "PART_Scrim").Opacity.ShouldBe(1);
            dialog.Presence.ShouldBe(1);

            dialog.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            dialog.IsHitTestVisible.ShouldBeFalse();
            Part(dialog, "PART_Panel").IsVisible.ShouldBeFalse();
            dialog.Presence.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dialog_Scrim_Tap_And_Escape_Close_It_Only_When_Dismissible()
    {
        var dialog = NewDialog(2);
        var window = Show(new Page { Overlay = dialog });
        try
        {
            var corner = new Point(8, 8);
            Tap(window, corner);
            dialog.IsOpen.ShouldBeTrue();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            dialog.IsOpen.ShouldBeTrue();

            dialog.IsDismissible = true;
            Tap(window, corner);
            dialog.IsOpen.ShouldBeFalse();

            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            dialog.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dialog_Tap_Inside_The_Panel_Does_Not_Dismiss()
    {
        var dialog = NewDialog(2);
        dialog.IsDismissible = true;
        var window = Show(new Page { Overlay = dialog });
        try
        {
            Tap(window, CenterOf(Part(dialog, "PART_Panel"), window) - new Point(0, 40));
            dialog.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dialog_Action_Runs_Its_Handler_And_Closes_Unless_CloseOnAction_Is_Off()
    {
        var dialog = NewDialog(2);
        var confirm = (Button)dialog.Items[1]!;
        var clicks = 0;
        confirm.Click += (_, _) => clicks++;
        var window = Show(new Page { Overlay = dialog });
        try
        {
            dialog.CloseOnAction = false;
            Tap(window, CenterOf(confirm, window));
            clicks.ShouldBe(1);
            dialog.IsOpen.ShouldBeTrue();

            dialog.CloseOnAction = true;
            Tap(window, CenterOf(confirm, window));
            clicks.ShouldBe(2);
            dialog.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dialog_IsDestructive_Colors_The_Action_With_Danger()
    {
        var plain = new Button { Content = "Cancel", Variant = ButtonVariant.Plain };
        var destructive = new Button { Content = "Delete", Variant = ButtonVariant.Plain };
        Dialog.SetIsDestructive(destructive, true);
        var filled = new Button { Content = "Delete all" };
        Dialog.SetIsDestructive(filled, true);
        var dialog = new Dialog { Title = "Delete?", IsOpen = true, Items = { plain, destructive, filled } };
        var window = Show(new Page { Overlay = dialog });
        try
        {
            var normal = ((ISolidColorBrush)plain.Foreground!).Color;
            var danger = ((ISolidColorBrush)destructive.Foreground!).Color;
            danger.ShouldNotBe(normal, $"{plain.Foreground} {destructive.Foreground} {filled.Background} tokenDanger={ArcTokens.GetColor(destructive, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Danger)} {destructive.GetValue(Dialog.IsDestructiveProperty)}");
            ((ISolidColorBrush)filled.Background!).Color.ShouldBe(danger);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dialog_Two_Short_Actions_Share_A_Row_And_Three_Or_Wide_Ones_Stack()
    {
        var two = NewDialog(2);
        var three = NewDialog(3);
        var wide = new Dialog { Title = "Wide", IsOpen = true };
        wide.Items.Add(new Button { Content = new string('A', 40) });
        wide.Items.Add(new Button { Content = new string('B', 40) });
        var window = Show(new Page { Overlay = new Grid { Children = { two, three, wide } } });
        try
        {
            var info = $"panel={Part(two, "PART_Panel").Bounds} b0={((Control)two.Items[0]!).Bounds} b0d={((Control)two.Items[0]!).DesiredSize} b1={((Control)two.Items[1]!).Bounds}";
            two.IsActionsStacked.ShouldBeFalse(info);
            ((Control)two.Items[0]!).Bounds.Y.ShouldBe(((Control)two.Items[1]!).Bounds.Y);
            three.IsActionsStacked.ShouldBeTrue();
            ((Control)three.Items[1]!).Bounds.Y.ShouldBeGreaterThan(((Control)three.Items[0]!).Bounds.Y);
            wide.IsActionsStacked.ShouldBeTrue();
            ((Control)three.Items[0]!).Bounds.Width.ShouldBeGreaterThan(((Control)two.Items[0]!).Bounds.Width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dialog_Is_Interruptible_And_Scales_From_094_Under_Full_Motion()
    {
        var dialog = NewDialog(2, open: false);
        var window = Show(new Page { Overlay = dialog }, ArcMotionLevel.Full);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            // The presence springs from 0: the first frame is not the end state.
            dialog.Presence.ShouldBeLessThan(1);
            var scale = ((ScaleTransform)Part(dialog, "PART_Panel").RenderTransform!).ScaleX;
            scale.ShouldBeInRange(0.94 - 0.001, 1.06);
            dialog.IsHitTestVisible.ShouldBeTrue();

            // Closed while still appearing, it reverses from where it is and keeps catching touches until the exit lands.
            dialog.IsOpen = false;
            dialog.IsHitTestVisible.ShouldBeTrue();
            WaitFor(() => !dialog.IsHitTestVisible).ShouldBeTrue("the exit never finished");
            dialog.Presence.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dialog_Reduced_Motion_Fades_Without_Scaling()
    {
        var dialog = NewDialog(2, open: false);
        var window = Show(new Page { Overlay = dialog }, ArcMotionLevel.Reduced);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            ((ScaleTransform)Part(dialog, "PART_Panel").RenderTransform!).ScaleX.ShouldBe(1);
            WaitFor(() => dialog.Presence >= 0.999).ShouldBeTrue();
            Part(dialog, "PART_Panel").Opacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    private static Toast NewToast(out Window window, ArcMotionLevel level = ArcMotionLevel.Off, Control? footer = null)
    {
        var toast = new Toast();
        window = Show(new Page { Content = new TextBlock { Text = "Body" }, Footer = footer, Overlay = toast }, level);
        return toast;
    }

    [AvaloniaFact]
    public void Toast_Shows_A_Message_Near_The_Bottom_And_Does_Not_Block_The_Page()
    {
        var toast = NewToast(out var window);
        try
        {
            toast.IsHitTestVisible.ShouldBeTrue();
            toast.Show("Saved", TimeSpan.Zero);
            Dispatcher.UIThread.RunJobs();
            toast.Count.ShouldBe(1);
            toast.Messages.ShouldBe(["Saved"]);
            var visual = toast.ToastVisuals.Single();
            visual.Opacity.ShouldBe(1);
            visual.TranslatePoint(new Point(0, visual.Bounds.Height), window)!.Value.Y.ShouldBe(600 - 16, 1);

            // An empty part of the overlay does not catch a touch.
            var hit = window.GetVisualAt(new Point(20, 100));
            hit?.GetSelfAndVisualAncestors().OfType<Toast>().Any().ShouldBeFalse(
                "the toast host's empty area took the hit");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Sits_Above_The_Footer_And_The_Safe_Area()
    {
        var footer = new Border { Height = 50 };
        var toast = new Toast();
        var page = new Page { Footer = footer, Overlay = toast, UsePlatformSafeArea = false, SafeArea = new Thickness(0, 0, 0, 20) };
        var window = Show(page);
        try
        {
            toast.Show("Saved", TimeSpan.Zero);
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            var visual = toast.ToastVisuals.Single();
            visual.TranslatePoint(new Point(0, visual.Bounds.Height), window)!.Value.Y.ShouldBe(600 - 20 - 50 - 16, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Stack_Keeps_The_Newest_MaxVisible_And_Replace_Keeps_One()
    {
        var toast = NewToast(out var window);
        try
        {
            toast.MaxVisible = 2;
            toast.Show("One", TimeSpan.Zero);
            toast.Show("Two", TimeSpan.Zero);
            toast.Show("Three", TimeSpan.Zero);
            Dispatcher.UIThread.RunJobs();
            toast.Messages.ShouldBe(["Two", "Three"]);

            toast.Policy = ToastPolicy.Replace;
            toast.Show("Four", TimeSpan.Zero);
            Dispatcher.UIThread.RunJobs();
            toast.Messages.ShouldBe(["Four"]);
            toast.ToastVisuals.Count.ShouldBe(1);

            toast.DismissAll();
            Dispatcher.UIThread.RunJobs();
            toast.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Leaves_By_Itself_After_Its_Duration()
    {
        var toast = NewToast(out var window);
        try
        {
            toast.Show("Short", TimeSpan.FromMilliseconds(150));
            toast.Count.ShouldBe(1);
            WaitFor(() => toast.ToastVisuals.Count == 0).ShouldBeTrue("the toast never left");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Countdown_Is_Held_While_A_Finger_Is_Down_And_Resumes_On_Release()
    {
        var toast = NewToast(out var window);
        try
        {
            toast.Show("Held", TimeSpan.FromMilliseconds(300));
            Dispatcher.UIThread.RunJobs();
            var point = CenterOf(toast.ToastVisuals.Single(), window);
            window.MouseDown(point, MouseButton.Left);
            WaitFor(() => false, 600);
            toast.Count.ShouldBe(1, "the countdown ran while the finger was down");

            window.MouseUp(point, MouseButton.Left);
            WaitFor(() => toast.ToastVisuals.Count == 0).ShouldBeTrue("the countdown did not resume");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Swipe_Past_The_Threshold_Dismisses_And_A_Short_One_Does_Not()
    {
        var toast = NewToast(out var window);
        try
        {
            toast.Show("Swipe me", TimeSpan.Zero);
            Dispatcher.UIThread.RunJobs();
            var start = CenterOf(toast.ToastVisuals.Single(), window);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(10, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Point(30, 0), RawInputModifiers.LeftMouseButton);
            Thread.Sleep(80);
            window.MouseUp(start + new Point(30, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            toast.Count.ShouldBe(1);

            start = CenterOf(toast.ToastVisuals.Single(), window);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(10, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start - new Point(150, 0), RawInputModifiers.LeftMouseButton);
            Thread.Sleep(80);
            window.MouseUp(start - new Point(150, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            toast.Count.ShouldBe(0);
            WaitFor(() => toast.ToastVisuals.Count == 0).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Is_Animated_Under_Full_Motion_And_Jumps_Under_Off()
    {
        var toast = NewToast(out var window, ArcMotionLevel.Full);
        try
        {
            toast.Show("Hello", TimeSpan.Zero);
            var visual = toast.ToastVisuals.Single();
            // The first frame is not the end state: it is still fading in and rising.
            visual.Opacity.ShouldBeLessThan(1);
            WaitFor(() => visual.Opacity >= 0.999).ShouldBeTrue("the toast never finished appearing");

            toast.DismissAll();
            toast.Count.ShouldBe(0);
            toast.ToastVisuals.Count.ShouldBe(1, "a leaving toast stays until its exit lands");
            WaitFor(() => toast.ToastVisuals.Count == 0).ShouldBeTrue("the toast never left");
        }
        finally
        {
            window.Close();
        }

        var instant = NewToast(out var window2, ArcMotionLevel.Off);
        try
        {
            instant.Show("Hello", TimeSpan.Zero);
            instant.ToastVisuals.Single().Opacity.ShouldBe(1);
            instant.DismissAll();
            instant.ToastVisuals.Count.ShouldBe(0);
        }
        finally
        {
            window2.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Shown_Before_Its_Template_Exists_Appears_Once_It_Does()
    {
        var toast = new Toast();
        toast.Show("Saved");
        toast.Count.ShouldBe(0);
        var window = new Window { Content = new Page { Overlay = toast }, Width = 400, Height = 600 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            toast.Messages.ShouldBe(["Saved"]);
        }
        finally
        {
            window.Close();
        }
    }
}
