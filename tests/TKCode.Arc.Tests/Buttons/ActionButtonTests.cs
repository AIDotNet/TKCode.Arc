using TKCode.Arc.DesignTokens;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;

namespace TKCode.Arc.Tests.Buttons;

public class ActionButtonTests
{
    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var button = new ActionButton();
        var window = TestHelpers.Show(button);
        try
        {
            button.Label.ShouldBe("Save");
            button.PendingLabel.ShouldBe("Saving");
            button.SuccessLabel.ShouldBe("Saved");
            button.ResetAfter.ShouldBe(TimeSpan.FromMilliseconds(2400));
            button.Bounds.Height.ShouldBe(44, 0.5);
            button.Padding.Left.ShouldBe(20);
            TestHelpers.ColorOf(button.Background).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(button.Foreground).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.Background));
            Peer(button).GetName().ShouldBe("Save");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Runs_Pending_Then_Success_Then_Resets()
    {
        var gate = new TaskCompletionSource();
        var runs = 0;
        var button = new ActionButton
        {
            ResetAfter = TimeSpan.FromMilliseconds(60),
            AsyncAction = () =>
            {
                runs++;
                return gate.Task;
            }
        };
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            button.State.ShouldBe(ActionButtonState.Pending);
            button.Classes.ShouldContain(":pending");
            button.DisplayLabel.ShouldBe("Saving");
            // Pending keeps focus and swallows activation; the peer reports it unavailable.
            button.IsFocused.ShouldBeTrue();
            Peer(button).IsEnabled().ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            runs.ShouldBe(1);

            gate.SetResult();
            WaitFor(() => button.State == ActionButtonState.Success).ShouldBeTrue();
            button.DisplayLabel.ShouldBe("Saved");
            Peer(button).GetName().ShouldBe("Save");
            WaitFor(() => button.State == ActionButtonState.Idle).ShouldBeTrue();
            button.DisplayLabel.ShouldBe("Save");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Failure_Returns_To_Idle_And_Reports_The_Exception()
    {
        Exception? reported = null;
        var button = new ActionButton { AsyncAction = async () => { await Task.Yield(); throw new InvalidOperationException("nope"); } };
        button.ActionFailed += (_, e) => reported = e.Exception;
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            WaitFor(() => reported is not null).ShouldBeTrue();
            reported!.Message.ShouldBe("nope");
            button.State.ShouldBe(ActionButtonState.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Command_Without_Async_Action_Succeeds_At_Once_And_Keeps_Result_With_Zero_Reset()
    {
        var executed = 0;
        var button = new ActionButton { ResetAfter = TimeSpan.Zero, Command = new RelayCommand(() => executed++) };
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            executed.ShouldBe(1);
            button.State.ShouldBe(ActionButtonState.Success);
            Pump(80);
            button.State.ShouldBe(ActionButtonState.Success);
            button.Reset();
            button.State.ShouldBe(ActionButtonState.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RelayCommand(Action run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run();
    }
}
