using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Heroes;

public class HeroSignupTests
{
    private static Window Show(HeroSignup hero, double width = 1000)
    {
        var window = TestHelpers.Show(hero);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private sealed class Recorder : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    [AvaloniaFact]
    public void Defaults_Show_The_Sample_Hero()
    {
        var hero = new HeroSignup();
        var window = Show(hero);
        try
        {
            hero.Phase.ShouldBe(HeroSignupPhase.Idle);
            hero.Title.ShouldBe("Release notes your whole team reads");
            hero.EmailBox.ShouldNotBeNull().Placeholder.ShouldBe("you@company.com");
            hero.SubmitButton.ShouldNotBeNull().Content.ShouldBe("Join the waitlist");
            hero.Faces.ShouldNotBeNull().Names.Count.ShouldBe(5);
            TestHelpers.Descendant<HeroSignupBackdrop>(hero).DotCount.ShouldBeGreaterThan(50);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Empty_And_Malformed_Addresses_Show_Their_Own_Errors()
    {
        var sent = 0;
        var hero = new HeroSignup { SubmitAsync = (_, _) => { sent++; return Task.CompletedTask; } };
        var window = Show(hero);
        try
        {
            await hero.Submit();
            hero.EmailBox!.IsInvalid.ShouldBeTrue();
            hero.EmailBox.ErrorMessage.ShouldBe(hero.EmptyMessage);
            hero.Announcer!.Announcement.ShouldBe(hero.EmptyMessage);

            hero.Email = "not-an-address";
            Dispatcher.UIThread.RunJobs();
            // After the first submit every keystroke checks again.
            hero.EmailBox.ErrorMessage.ShouldBe(hero.FormatMessage);

            hero.Email = "ada@studio.dev";
            Dispatcher.UIThread.RunJobs();
            hero.EmailBox.IsInvalid.ShouldBeFalse();
            sent.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Before_The_First_Submit_Only_Clears_Errors()
    {
        var hero = new HeroSignup();
        var window = Show(hero);
        try
        {
            hero.Email = "x";
            Dispatcher.UIThread.RunJobs();
            hero.EmailBox!.IsInvalid.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Valid_Address_Is_Sent_Confirmed_And_Reported()
    {
        var command = new Recorder();
        string? sentTo = null;
        var hero = new HeroSignup
        {
            SubmitAsync = (email, _) => { sentTo = email; return Task.CompletedTask; },
            SubmitCommand = command
        };
        HeroSignupSubmittedEventArgs? raised = null;
        hero.Submitted += (_, e) => raised = e;
        var window = Show(hero);
        try
        {
            hero.Email = "  ada@studio.dev ";
            await hero.Submit();
            await Settle();
            sentTo.ShouldBe("ada@studio.dev");
            hero.Phase.ShouldBe(HeroSignupPhase.Done);
            hero.SubmittedEmail.ShouldBe("ada@studio.dev");
            hero.DoneText.ShouldBe("We sent a confirmation to ada@studio.dev.");
            raised.ShouldNotBeNull().Email.ShouldBe("ada@studio.dev");
            command.Parameters.ShouldBe(["ada@studio.dev"]);
            hero.Classes.Contains(":done").ShouldBeTrue();
            hero.ResetButton!.IsTabStop.ShouldBeTrue();
            hero.EmailBox!.IsTabStop.ShouldBeFalse();

            hero.Reset();
            hero.Phase.ShouldBe(HeroSignupPhase.Idle);
            hero.Email.ShouldBe(string.Empty);
            hero.SubmittedEmail.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Failed_Send_Keeps_The_Address_And_Shows_The_Failure()
    {
        var hero = new HeroSignup { SubmitAsync = (_, _) => throw new InvalidOperationException("down") };
        var window = Show(hero);
        try
        {
            hero.Email = "ada@studio.dev";
            await hero.Submit();
            await Settle();
            hero.Phase.ShouldBe(HeroSignupPhase.Idle);
            hero.Email.ShouldBe("ada@studio.dev");
            hero.EmailBox!.ErrorMessage.ShouldBe(hero.FailedMessage);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Sending_Locks_The_Field_And_Shows_The_Spinner()
    {
        var gate = new TaskCompletionSource();
        var hero = new HeroSignup { SubmitAsync = (_, _) => gate.Task };
        var window = Show(hero);
        try
        {
            hero.Email = "ada@studio.dev";
            var pending = hero.Submit();
            Dispatcher.UIThread.RunJobs();
            hero.Phase.ShouldBe(HeroSignupPhase.Sending);
            hero.EmailBox!.IsReadOnly.ShouldBeTrue();
            hero.SubmitButton!.IsLoading.ShouldBeTrue();
            hero.SubmitButton.Content.ShouldBe("Joining");
            gate.SetResult();
            await pending;
            hero.Phase.ShouldBe(HeroSignupPhase.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Enter_In_The_Field_Submits()
    {
        var sent = 0;
        var hero = new HeroSignup { SubmitAsync = (_, _) => { sent++; return Task.CompletedTask; } };
        var window = Show(hero);
        try
        {
            hero.Email = "ada@studio.dev";
            hero.FocusEmail();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await Settle();
            sent.ShouldBe(1);
            hero.Phase.ShouldBe(HeroSignupPhase.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Width_Sets_The_Compact_And_Narrow_Layouts_And_Stacks_The_Form()
    {
        var hero = new HeroSignup();
        var window = Show(hero, 420);
        try
        {
            hero.Classes.Contains(":compact").ShouldBeTrue();
            hero.Classes.Contains(":narrow").ShouldBeTrue();
            TestHelpers.Descendant<HeroSignupFormPanel>(hero).IsStacked.ShouldBeTrue();
            window.Width = 1100;
            Dispatcher.UIThread.RunJobs();
            hero.Classes.Contains(":compact").ShouldBeFalse();
            TestHelpers.Descendant<HeroSignupFormPanel>(hero).IsStacked.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Proof_Row_Follows_Its_Properties()
    {
        var hero = new HeroSignup { ProofNames = ["One Person", "Two People"] };
        var window = Show(hero);
        try
        {
            hero.Faces!.Names.ShouldBe(["One Person", "Two People"]);
            hero.ProofText = null;
            hero.Classes.Contains(":no-proof").ShouldBeTrue();
            hero.Faces.Names.Count.ShouldBe(0);
            hero.Alignment = HeroSignupAlignment.Start;
            hero.Classes.Contains(":start").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
