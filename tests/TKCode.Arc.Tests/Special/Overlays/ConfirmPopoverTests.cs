using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Inputs;

/// <summary>
/// The confirm popover: opening from its trigger with focus on Cancel, Escape and the cancel button cancelling with
/// their reasons and focus back on the trigger, confirming (event, command, task), the async handler that keeps the
/// question open on failure, tone and initial focus, and the attached form.
/// </summary>
public class ConfirmPopoverTests
{
    private static void Click(Avalonia.Controls.Button button) =>
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, button));

    private static (ConfirmPopover Confirm, Button Trigger, Window Window) Show(Action<ConfirmPopover>? configure = null)
    {
        var trigger = new Button { Content = "Delete" };
        var confirm = new ConfirmPopover { Trigger = trigger, Title = "Delete these notes?", Message = "They are removed for everyone." };
        configure?.Invoke(confirm);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { confirm } });
        return (confirm, trigger, window);
    }

    [AvaloniaFact]
    public void Defaults_Ask_A_Destructive_Question_With_Cancel_First()
    {
        var confirm = new ConfirmPopover();
        confirm.Title.ShouldBe("Are you sure?");
        confirm.ConfirmText.ShouldBe("Delete");
        confirm.CancelText.ShouldBe("Cancel");
        confirm.IsDestructive.ShouldBeTrue();
        confirm.InitialFocus.ShouldBe(ConfirmPopoverFocus.Cancel);
        confirm.Placement.ShouldBe(PopoverPlacement.Bottom);
        confirm.Icon.ShouldBe(ArcIcons.TriangleAlert);
        var token = new ConfirmPopoverToken();
        token.CalculateTokenValues(false);
        token.PanelWidth.ShouldBe(272);
    }

    [AvaloniaFact]
    public void Trigger_Opens_With_Focus_On_Cancel_And_Escape_Cancels_Back_To_The_Trigger()
    {
        var (confirm, trigger, window) = Show();
        try
        {
            ConfirmPopoverCancelledEventArgs? cancelled = null;
            confirm.Cancelled += (_, e) => cancelled = e;
            trigger.Focus();
            Click(trigger);
            Dispatcher.UIThread.RunJobs();
            confirm.IsOpen.ShouldBeTrue();
            confirm.Host!.IsOpen.ShouldBeTrue();
            confirm.Panel!.CancelButton!.IsFocused.ShouldBeTrue();
            AutomationProperties.GetItemStatus(trigger).ShouldBe("Expanded");
            AutomationProperties.GetHelpText(confirm.Panel.ConfirmButton!).ShouldBe("Delete these notes?");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            confirm.IsOpen.ShouldBeFalse();
            cancelled.ShouldNotBeNull().Reason.ShouldBe(DismissReason.Escape);
            trigger.IsFocused.ShouldBeTrue();
            AutomationProperties.GetItemStatus(trigger).ShouldBe("Collapsed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cancel_Button_Cancels_And_Confirm_Raises_Confirmed_Then_Runs_The_Command()
    {
        var ran = 0;
        var (confirm, trigger, window) = Show(c =>
        {
            c.Command = new RelayCommand(_ => ran++);
        });
        try
        {
            var confirmed = 0;
            var cancelled = 0;
            confirm.Confirmed += (_, _) => confirmed++;
            confirm.Cancelled += (_, _) => cancelled++;

            Click(trigger);
            Dispatcher.UIThread.RunJobs();
            Click(confirm.Panel!.CancelButton!);
            Dispatcher.UIThread.RunJobs();
            confirm.IsOpen.ShouldBeFalse();
            (confirmed, cancelled, ran).ShouldBe((0, 1, 0));

            Click(trigger);
            Dispatcher.UIThread.RunJobs();
            confirm.IsOpen.ShouldBeTrue();
            Click(confirm.Panel.ConfirmButton!);
            Dispatcher.UIThread.RunJobs();
            confirm.IsOpen.ShouldBeFalse();
            (confirmed, cancelled, ran).ShouldBe((1, 1, 1));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ConfirmAsync_Answers_With_The_Choice()
    {
        var (confirm, trigger, window) = Show();
        try
        {
            var first = confirm.ConfirmAsync(trigger);
            Dispatcher.UIThread.RunJobs();
            confirm.IsOpen.ShouldBeTrue();
            confirm.Anchor.ShouldBe(trigger);
            confirm.Confirm();
            Dispatcher.UIThread.RunJobs();
            first.IsCompleted.ShouldBeTrue();
            first.Result.ShouldBeTrue();

            var second = confirm.ConfirmAsync();
            Dispatcher.UIThread.RunJobs();
            confirm.Close();
            Dispatcher.UIThread.RunJobs();
            second.IsCompleted.ShouldBeTrue();
            second.Result.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Handler_Keeps_The_Question_Open_Until_It_Succeeds()
    {
        TaskCompletionSource<bool>? work = null;
        var (confirm, trigger, window) = Show(c => c.ConfirmHandler = () =>
        {
            work = new TaskCompletionSource<bool>();
            return work.Task;
        });
        try
        {
            var confirmed = 0;
            confirm.Confirmed += (_, _) => confirmed++;
            Click(trigger);
            Dispatcher.UIThread.RunJobs();
            var panel = confirm.Panel!;

            Click(panel.ConfirmButton!);
            Dispatcher.UIThread.RunJobs();
            confirm.IsBusy.ShouldBeTrue();
            panel.ConfirmButton!.IsLoading.ShouldBeTrue();
            panel.CancelButton!.IsEnabled.ShouldBeFalse();

            work!.SetResult(false);
            Dispatcher.UIThread.RunJobs();
            confirm.IsBusy.ShouldBeFalse();
            confirm.IsOpen.ShouldBeTrue();
            panel.ConfirmButton.IsLoading.ShouldBeFalse();
            panel.CancelButton.IsEnabled.ShouldBeTrue();
            confirmed.ShouldBe(0);

            Click(panel.ConfirmButton);
            Dispatcher.UIThread.RunJobs();
            work.SetResult(true);
            Dispatcher.UIThread.RunJobs();
            confirm.IsOpen.ShouldBeFalse();
            confirmed.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Safe_Action_Uses_The_Accent_And_Can_Start_On_Confirm()
    {
        var (confirm, trigger, window) = Show(c =>
        {
            c.IsDestructive = false;
            c.InitialFocus = ConfirmPopoverFocus.Confirm;
            c.Icon = null;
        });
        try
        {
            Click(trigger);
            Dispatcher.UIThread.RunJobs();
            var panel = confirm.Panel!;
            panel.ConfirmButton!.Variant.ShouldBe(ButtonVariant.Primary);
            panel.ConfirmButton.IsFocused.ShouldBeTrue();
            panel.Classes.Contains(":destructive").ShouldBeFalse();
            panel.Classes.Contains(":has-icon").ShouldBeFalse();
            panel.Classes.Contains(":has-message").ShouldBeTrue();

            // Left and Right move between the two buttons.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            panel.CancelButton!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Attached_Question_Opens_From_Its_Button_And_Joins_Its_Logical_Tree()
    {
        var target = new Button { Content = "Discard draft" };
        var confirm = new ConfirmPopover { Title = "Discard this draft?" };
        ConfirmPopover.SetAttach(target, confirm);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { target } });
        try
        {
            Click(target);
            Dispatcher.UIThread.RunJobs();
            confirm.IsOpen.ShouldBeTrue();
            confirm.Parent.ShouldBe(target);
            confirm.Anchor.ShouldBe(target);
            confirm.Close();
            Dispatcher.UIThread.RunJobs();
            confirm.IsOpen.ShouldBeFalse();
            confirm.Parent.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _run;

        public RelayCommand(Action<object?> run) => _run = run;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _run(parameter);
    }
}
