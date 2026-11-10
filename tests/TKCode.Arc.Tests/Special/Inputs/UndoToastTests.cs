using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Inputs;

/// <summary>
/// The undo toast: show, undo, dismiss, replace and time out with their reasons and task results, the countdown that
/// holds under the pointer and with focus inside, the undo shortcut, Escape, accessibility and the empty footprint.
/// </summary>
public class UndoToastTests
{
    private static (UndoToast Toast, Window Window) Show()
    {
        var toast = new UndoToast();
        var window = TestHelpers.Show(new Panel { Children = { toast } });
        return (toast, window);
    }

    private static void Click(Avalonia.Controls.Button button) =>
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, button));

    [AvaloniaFact]
    public void Closed_Toast_Takes_No_Space_And_Has_Defaults()
    {
        var (toast, window) = Show();
        try
        {
            toast.IsOpen.ShouldBeFalse();
            toast.Bounds.Height.ShouldBe(0);
            toast.Duration.ShouldBe(TimeSpan.FromSeconds(5));
            toast.UndoText.ShouldBe("Undo");
            toast.IsCountdownVisible.ShouldBeTrue();
            toast.IsShortcutEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Undo_Raises_Undone_And_Answers_True()
    {
        var (toast, window) = Show();
        try
        {
            var undone = 0;
            UndoToastClosedEventArgs? closed = null;
            toast.Undone += (_, _) => undone++;
            toast.Closed += (_, e) => closed = e;
            var answer = toast.ShowAsync("Conversation archived");
            Dispatcher.UIThread.RunJobs();
            toast.IsOpen.ShouldBeTrue();
            toast.IsShowing.ShouldBeTrue();
            toast.Bounds.Height.ShouldBeGreaterThan(0);
            AutomationProperties.GetName(toast).ShouldBe("Conversation archived");
            AutomationProperties.GetLiveSetting(toast).ShouldBe(AutomationLiveSetting.Polite);
            var undo = TestHelpers.Descendant<Button>(toast, UndoToast.UndoPart);
            AutomationProperties.GetName(undo).ShouldBe("Undo: Conversation archived");
            TestHelpers.Descendant<TextBlock>(toast, UndoToast.SecondsPart).Text.ShouldBe("5");

            Click(undo);
            Dispatcher.UIThread.RunJobs();
            undone.ShouldBe(1);
            closed.ShouldNotBeNull().Reason.ShouldBe(UndoToastCloseReason.Undone);
            closed.Message.ShouldBe("Conversation archived");
            answer.IsCompleted.ShouldBeTrue();
            answer.Result.ShouldBeTrue();
            toast.IsOpen.ShouldBeFalse();
            toast.Bounds.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dismiss_And_Replace_Let_The_Action_Stand()
    {
        var (toast, window) = Show();
        try
        {
            var reasons = new List<UndoToastCloseReason>();
            toast.Closed += (_, e) => reasons.Add(e.Reason);
            var first = toast.ShowAsync("Moved to Archive");
            Dispatcher.UIThread.RunJobs();
            var second = toast.ShowAsync("Marked as read");
            Dispatcher.UIThread.RunJobs();
            first.Result.ShouldBeFalse();
            toast.IsShowing.ShouldBeTrue();
            toast.Message.ShouldBe("Marked as read");

            Click(TestHelpers.Descendant<Button>(toast, UndoToast.ClosePart));
            Dispatcher.UIThread.RunJobs();
            second.Result.ShouldBeFalse();
            reasons.ShouldBe([UndoToastCloseReason.Replaced, UndoToastCloseReason.Dismissed]);

            toast.Show("Label removed");
            Dispatcher.UIThread.RunJobs();
            toast.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            reasons[^1].ShouldBe(UndoToastCloseReason.Dismissed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Countdown_Runs_Out_And_The_Action_Stands()
    {
        var (toast, window) = Show();
        try
        {
            UndoToastClosedEventArgs? closed = null;
            toast.Closed += (_, e) => closed = e;
            var answer = toast.ShowAsync("Card removed", TimeSpan.FromMilliseconds(80));
            TestHelpers.PumpUntil(() => closed is not null).ShouldBeTrue();
            closed!.Reason.ShouldBe(UndoToastCloseReason.TimedOut);
            answer.Result.ShouldBeFalse();
            toast.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Countdown_Holds_Under_The_Pointer_And_With_Focus_Inside()
    {
        var (toast, window) = Show();
        try
        {
            toast.Show("Conversation archived", TimeSpan.FromSeconds(30));
            Dispatcher.UIThread.RunJobs();
            toast.Timeline!.IsRunning.ShouldBeTrue();

            var inside = toast.TranslatePoint(new Point(20, toast.Bounds.Height / 2), window)!.Value;
            window.MouseMove(inside);
            Dispatcher.UIThread.RunJobs();
            toast.IsPaused.ShouldBeTrue();
            toast.Classes.Contains(":paused").ShouldBeTrue();
            toast.Timeline.IsRunning.ShouldBeFalse();
            var held = toast.Remaining;

            window.MouseMove(new Point(390, 290));
            Dispatcher.UIThread.RunJobs();
            toast.IsPaused.ShouldBeFalse();
            toast.Timeline.IsRunning.ShouldBeTrue();
            toast.Remaining.ShouldBeLessThanOrEqualTo(held);

            TestHelpers.Descendant<Button>(toast, UndoToast.UndoPart).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            toast.IsPaused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Undo_Shortcut_Undoes_And_Escape_Inside_Dismisses()
    {
        var field = new TextBox();
        var toast = new UndoToast();
        var other = new Button { Content = "Elsewhere" };
        var window = TestHelpers.Show(new StackPanel { Children = { field, other, toast } });
        try
        {
            var reasons = new List<UndoToastCloseReason>();
            toast.Closed += (_, e) => reasons.Add(e.Reason);
            var gesture = Application.Current?.PlatformSettings?.HotkeyConfiguration.Undo.FirstOrDefault();
            var modifiers = gesture?.KeyModifiers.HasFlag(KeyModifiers.Meta) == true ? RawInputModifiers.Meta : RawInputModifiers.Control;

            // A text field keeps the shortcut for its own undo.
            toast.Show("Conversation archived");
            field.Focus();
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Z, modifiers);
            Dispatcher.UIThread.RunJobs();
            reasons.ShouldBeEmpty();

            other.Focus();
            window.KeyPressQwerty(PhysicalKey.Z, modifiers);
            Dispatcher.UIThread.RunJobs();
            reasons.ShouldBe([UndoToastCloseReason.Undone]);

            toast.Show("Label removed");
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<Button>(toast, UndoToast.UndoPart).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reasons.ShouldBe([UndoToastCloseReason.Undone, UndoToastCloseReason.Dismissed]);

            // Off: the shortcut does nothing.
            toast.IsShortcutEnabled = false;
            toast.Show("Label removed");
            other.Focus();
            window.KeyPressQwerty(PhysicalKey.Z, modifiers);
            Dispatcher.UIThread.RunJobs();
            toast.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hidden_Countdown_Still_Runs()
    {
        var toast = new UndoToast { IsCountdownVisible = false };
        var window = TestHelpers.Show(new Panel { Children = { toast } });
        try
        {
            toast.Show("12 messages marked as read");
            Dispatcher.UIThread.RunJobs();
            toast.Classes.Contains(":countdown").ShouldBeFalse();
            TestHelpers.Descendant<Panel>(toast, "PART_Countdown").IsVisible.ShouldBeFalse();
            toast.Timeline!.IsRunning.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
