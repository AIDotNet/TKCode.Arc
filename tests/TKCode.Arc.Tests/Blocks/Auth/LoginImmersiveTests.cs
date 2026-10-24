using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Auth;

/// <summary>A manual clock for the page's demo delays: <see cref="Advance"/> completes due delays in order.</summary>
internal sealed class LoginImmersiveManualDelays
{
    private readonly List<(TimeSpan Due, TaskCompletionSource Source)> _pending = new();
    private TimeSpan _now;

    public int Pending => _pending.Count(p => !p.Source.Task.IsCompleted);

    public Task Delay(TimeSpan delay, CancellationToken token)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        token.Register(() => source.TrySetCanceled(token));
        _pending.Add((_now + delay, source));
        return source.Task;
    }

    public void Advance(TimeSpan by)
    {
        var end = _now + by;
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            var next = _pending.Where(p => !p.Source.Task.IsCompleted && p.Due <= end).OrderBy(p => p.Due).FirstOrDefault();
            if (next.Source is null)
            {
                break;
            }
            _now = next.Due;
            _pending.Remove(next);
            next.Source.TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }
        _now = end;
        _pending.RemoveAll(p => p.Source.Task.IsCompleted);
        Dispatcher.UIThread.RunJobs();
    }

    public void Advance(int milliseconds) => Advance(TimeSpan.FromMilliseconds(milliseconds));
}

public class LoginImmersiveTests
{
    internal static (LoginImmersive Block, Window Window, LoginImmersiveManualDelays Delays) Show(
        Action<LoginImmersive>? configure = null, double width = 1000, double height = 760, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var block = new LoginImmersive { IsFullScreen = true };
        var delays = new LoginImmersiveManualDelays();
        block.Delay = delays.Delay;
        configure?.Invoke(block);
        var window = TestHelpers.Show(block, level);
        window.Width = width;
        window.Height = height;
        Settle();
        return (block, window, delays);
    }

    internal static void Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    internal static void Press(Window window, Avalonia.Controls.Button button)
    {
        // Enter activates on key down (like the other block tests, the release is not sent).
        button.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle();
    }

    internal static void Type(Window window, LoginImmersive block, string text)
    {
        var input = block.FormView!.Email!;
        input.Focus(NavigationMethod.Tab);
        input.Text = text;
        Settle();
    }

    internal static void Submit(Window window, LoginImmersive block)
    {
        var input = block.FormView!.Email!;
        input.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle();
    }

    /// <summary>Types a valid address, submits, and runs the demo to the Sent step.</summary>
    internal static void SendLink(Window window, LoginImmersive block, LoginImmersiveManualDelays delays, string email = "  Iris.Vance@Fieldnote.App ")
    {
        Type(window, block, email);
        Submit(window, block);
        delays.Advance(900);
        delays.Advance(625);
        Settle();
    }

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    [AvaloniaFact]
    public void Defaults_Show_The_Sign_In_Form_With_Three_Providers()
    {
        var (block, window, _) = Show();
        try
        {
            block.Step.ShouldBe(LoginImmersiveStep.Form);
            block.Mode.ShouldBe(LoginImmersiveMode.SignIn);
            block.BrandName.ShouldBe("Fieldnote");
            block.ResendSeconds.ShouldBe(30);
            block.DefaultEmail.ShouldBe("rowan@fieldnote.app");
            block.PhotoAlignment.ShouldBe(new Point(0.5, 0.58));
            block.IsBusy.ShouldBeFalse();
            block.Account.ShouldBeNull();
            var form = block.FormView!;
            form.SignInHeading!.Title.ShouldBe("Sign in to Fieldnote");
            form.SignUpHeading!.Title.ShouldBe("Start your Fieldnote");
            form.HeadingHost!.Current.ShouldBe(form.SignInHeading);
            form.Email!.Label.ShouldBe("Email");
            form.Email.Placeholder.ShouldBe("name@example.com");
            form.Send!.Content.ShouldBe("Email me a link");
            block.ProviderButtons.Count.ShouldBe(3);
            block.EffectiveProviders.Select(p => p.Name).ShouldBe(["Google", "Apple", "GitHub"]);
            AutomationProperties.GetName(block.ProviderButtons[0]).ShouldBe("Continue with Google");
            AutomationProperties.GetName(form.Providers!).ShouldBe("Continue with another account");
            block.StepHost!.Current.ShouldBe(form);
            block.SentView!.IsVisible.ShouldBeFalse();
            block.DoneView!.IsVisible.ShouldBeFalse();
            block.TermsButton!.Content.ShouldBe("Terms");
            block.PrivacyButton!.Content.ShouldBe("Privacy");
            AutomationProperties.GetName(block).ShouldBe("Sign in to Fieldnote");
            AutomationProperties.GetName(block.ModeSwitch!).ShouldBe("No account? Sign up");
            block.NoteText.ShouldBeNull();
            block.Announcer!.History.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Is_420_Wide_And_Centred_With_The_Measured_Form_Height()
    {
        var (block, window, _) = Show();
        try
        {
            var card = TestHelpers.Descendant<Border>(block, LoginImmersive.CardPart);
            card.Bounds.Width.ShouldBe(420, 0.5);
            var left = card.TranslatePoint(default, block)!.Value.X;
            left.ShouldBe((1000 - 420) / 2.0, 0.5);
            block.FormView!.Bounds.Width.ShouldBe(418, 0.5);
            block.Classes.Contains(":compact").ShouldBeFalse();
            block.Classes.Contains(":card-compact").ShouldBeFalse();
            block.Classes.Contains(":fullscreen").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Framed_Block_Has_Panel_Corners_And_A_Minimum_Height()
    {
        var (block, window, _) = Show(b => b.IsFullScreen = false);
        try
        {
            block.Classes.Contains(":fullscreen").ShouldBeFalse();
            block.MinHeight.ShouldBe(640);
            block.CornerRadius.TopLeft.ShouldBe(26);
            block.IsFullScreen = true;
            Settle();
            block.MinHeight.ShouldBe(0);
            block.CornerRadius.TopLeft.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Block_And_The_Card_Width()
    {
        var (block, window, _) = Show(width: 520);
        try
        {
            block.Classes.Contains(":compact").ShouldBeTrue();
            block.Classes.Contains(":card-compact").ShouldBeFalse();
            window.Width = 440;
            Settle();
            TestHelpers.Descendant<Border>(block, LoginImmersive.CardPart).Bounds.Width.ShouldBe(408, 0.5);
            block.Classes.Contains(":card-compact").ShouldBeFalse();
            window.Width = 400;
            Settle();
            block.Classes.Contains(":card-compact").ShouldBeTrue();
            block.FormView!.IsCompact.ShouldBeTrue();
            block.FormView.SignInHeading!.IsCompact.ShouldBeTrue();
            block.SentView!.IsCompact.ShouldBeTrue();
            window.Width = 900;
            Settle();
            block.Classes.Contains(":compact").ShouldBeFalse();
            block.Classes.Contains(":card-compact").ShouldBeFalse();
            block.FormView.IsCompact.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Submit_Asks_For_The_Email_And_Stays()
    {
        var (block, window, delays) = Show();
        try
        {
            Submit(window, block);
            block.Step.ShouldBe(LoginImmersiveStep.Form);
            block.IsBusy.ShouldBeFalse();
            block.EmailError.ShouldBe("Type your email to continue.");
            block.FormView!.Email!.ErrorMessage.ShouldBe("Type your email to continue.");
            delays.Pending.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Incomplete_Email_Is_Rechecked_As_The_Person_Types()
    {
        var (block, window, _) = Show();
        try
        {
            Type(window, block, "a@b.c");
            Submit(window, block);
            block.EmailError.ShouldBe("That doesn't look like a full email address.");
            Type(window, block, "a@b.co");
            block.EmailError.ShouldBeNull();
            Type(window, block, "");
            block.EmailError.ShouldBe("Type your email to continue.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Field_Checks_Only_When_It_Has_Text()
    {
        var (block, window, _) = Show();
        try
        {
            block.FormView!.Email!.Focus(NavigationMethod.Tab);
            block.FormView.Send!.Focus(NavigationMethod.Tab);
            Settle();
            block.EmailError.ShouldBeNull();
            Type(window, block, "a b@c.de");
            block.FormView.Send.Focus(NavigationMethod.Tab);
            Settle();
            block.EmailError.ShouldBe("That doesn't look like a full email address.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sending_Shows_Busy_Then_Link_Sent_Then_The_Sent_Step()
    {
        var (block, window, delays) = Show();
        var sent = new List<LoginImmersiveLinkSentEventArgs>();
        block.LinkSent += (_, e) => sent.Add(e);
        try
        {
            Type(window, block, "  Iris.Vance@Fieldnote.App ");
            Submit(window, block);
            block.IsBusy.ShouldBeTrue();
            block.Busy.ShouldBe(LoginImmersive.BusyKind.Sending);
            block.FormView!.Send!.IsLoading.ShouldBeTrue();
            block.FormView.Email!.IsReadOnly.ShouldBeTrue();
            block.Announcer!.Announcement.ShouldBe("Sending your link");

            delays.Advance(899);
            block.Busy.ShouldBe(LoginImmersive.BusyKind.Sending);
            delays.Advance(1);
            block.Busy.ShouldBe(LoginImmersive.BusyKind.LinkSentHold);
            block.FormView.Send.IsLoading.ShouldBeFalse();
            block.FormView.Send.Content.ShouldBeOfType<StackPanel>();
            AutomationProperties.GetName(block.FormView.Send).ShouldBe("Link sent");
            block.Announcer.Announcement.ShouldBe("Link sent to iris.vance@fieldnote.app");
            sent.Count.ShouldBe(1);
            sent[0].Email.ShouldBe("iris.vance@fieldnote.app");
            sent[0].IsResend.ShouldBeFalse();
            sent[0].Mode.ShouldBe(LoginImmersiveMode.SignIn);
            block.Step.ShouldBe(LoginImmersiveStep.Form);

            delays.Advance(624);
            block.Step.ShouldBe(LoginImmersiveStep.Form);
            delays.Advance(1);
            Settle();
            block.Step.ShouldBe(LoginImmersiveStep.Sent);
            block.IsBusy.ShouldBeFalse();
            block.Account.ShouldBe("iris.vance@fieldnote.app");
            block.StepHost!.Current.ShouldBe(block.SentView);
            block.SentView!.Address.ShouldBe("iris.vance@fieldnote.app");
            block.SentView.Heading!.Title.ShouldBe("Look in your inbox");
            block.ResendLeft.ShouldBe(30);
            block.NoteText.ShouldBe("Demo: Open mail app signs you in as if you had clicked the link.");
            block.SentView.Heading.IsFocused.ShouldBeTrue();
            AutomationProperties.GetName(block.SentView.Edit!).ShouldBe("Edit the address iris.vance@fieldnote.app");
            AutomationProperties.GetName(block.SentView.Resend!).ShouldBe("Send again, available in 30 seconds");
            block.SentView.Resend!.IsInert.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Form_Button_Says_Email_Me_A_Link_Again_Once_The_Form_Is_Hidden()
    {
        var (block, window, delays) = Show();
        try
        {
            SendLink(window, block, delays);
            block.FormView!.IsVisible.ShouldBeFalse();
            block.FormView.Send!.Content.ShouldBe("Email me a link");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Countdown_Runs_Down_To_Send_Again()
    {
        var (block, window, delays) = Show();
        try
        {
            SendLink(window, block, delays);
            block.Countdown!.Text.ShouldBe("0:30");
            delays.Advance(1000);
            block.ResendLeft.ShouldBe(29);
            block.Countdown!.Text.ShouldBe("0:29");
            AutomationProperties.GetName(block.SentView!.Resend!).ShouldBe("Send again, available in 29 seconds");
            delays.Advance(28_000);
            block.ResendLeft.ShouldBe(1);
            AutomationProperties.GetName(block.SentView.Resend!).ShouldBe("Send again, available in 1 second");
            delays.Advance(1000);
            block.ResendLeft.ShouldBe(0);
            block.Countdown.ShouldBeNull();
            block.SentView.Resend!.IsInert.ShouldBeFalse();
            AutomationProperties.GetName(block.SentView.Resend!).ShouldBe("Send again");
            block.SentView.ResendLabel!.Content.ShouldBeOfType<TextBlock>().Text.ShouldBe("Send again");
            delays.Pending.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Resend_Waits_For_The_Countdown_Then_Sends_At_Once()
    {
        var (block, window, delays) = Show();
        var sent = new List<LoginImmersiveLinkSentEventArgs>();
        block.LinkSent += (_, e) => sent.Add(e);
        try
        {
            SendLink(window, block, delays);
            Press(window, block.SentView!.Resend!);
            sent.Count.ShouldBe(1);
            block.ResendLeft.ShouldBe(30);
            delays.Advance(30_000);
            block.ResendLeft.ShouldBe(0);
            Press(window, block.SentView.Resend!);
            sent.Count.ShouldBe(2);
            sent[1].IsResend.ShouldBeTrue();
            sent[1].Email.ShouldBe("iris.vance@fieldnote.app");
            block.ResendLeft.ShouldBe(30);
            block.Step.ShouldBe(LoginImmersiveStep.Sent);
            block.Announcer!.Announcement.ShouldBe("New link sent");
            block.SentView.Resend!.IsFocused.ShouldBeTrue();
            block.SentView.Resend.IsInert.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Open_Mail_App_Demo_Signs_In_With_The_Link()
    {
        var command = new RecordingCommand();
        var (block, window, delays) = Show(b => b.SignedInCommand = command);
        var signedIn = new List<LoginImmersiveSignedInEventArgs>();
        block.SignedIn += (_, e) => signedIn.Add(e);
        try
        {
            SendLink(window, block, delays);
            Press(window, block.SentView!.OpenMail!);
            block.Busy.ShouldBe(LoginImmersive.BusyKind.OpeningMail);
            block.SentView.OpenMail!.IsLoading.ShouldBeTrue();
            block.Announcer!.Announcement.ShouldBe("Opening your mail app");
            delays.Advance(999);
            block.Step.ShouldBe(LoginImmersiveStep.Sent);
            delays.Advance(1);
            Settle();
            block.Step.ShouldBe(LoginImmersiveStep.Done);
            block.Method.ShouldBe(LoginImmersiveMethod.MagicLink);
            block.Provider.ShouldBeNull();
            block.Account.ShouldBe("iris.vance@fieldnote.app");
            block.NoteText.ShouldBeNull();
            delays.Pending.ShouldBe(0);
            signedIn.Count.ShouldBe(1);
            signedIn[0].Email.ShouldBe("iris.vance@fieldnote.app");
            signedIn[0].Method.ShouldBe(LoginImmersiveMethod.MagicLink);
            command.Parameters.Single().ShouldBe(signedIn[0]);
            var done = block.DoneView!;
            done.Heading!.Title.ShouldBe("Signed in");
            done.Heading.Description.ShouldBe("Link accepted. This device remembers you for 30 days.");
            done.AccountLabel.ShouldBe("Account");
            done.Account.ShouldBe("iris.vance@fieldnote.app");
            done.MethodLabel.ShouldBe("Method");
            done.Method.ShouldBe("Magic link");
            done.SignOut!.Content.ShouldBe("Sign out");
            done.Heading.IsFocused.ShouldBeTrue();
            block.Announcer.Announcement.ShouldBe("Signed in with your link");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Edit_Returns_To_The_Form_With_The_Address_Kept()
    {
        var (block, window, delays) = Show();
        try
        {
            SendLink(window, block, delays);
            Press(window, block.SentView!.Edit!);
            block.Step.ShouldBe(LoginImmersiveStep.Form);
            block.Account.ShouldBeNull();
            block.Email.ShouldBe("  Iris.Vance@Fieldnote.App ");
            block.NoteText.ShouldBeNull();
            block.FormView!.Email!.IsFocused.ShouldBeTrue();
            block.Announcer!.Announcement.ShouldBe("Change your email");
            delays.Pending.ShouldBe(0);
            // Sending again starts a fresh countdown.
            Submit(window, block);
            delays.Advance(1525);
            Settle();
            block.Step.ShouldBe(LoginImmersiveStep.Sent);
            block.ResendLeft.ShouldBe(30);
            block.Countdown!.Text.ShouldBe("0:30");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Provider_Signs_In_Directly_With_The_Field_Or_The_Default_Address()
    {
        var (block, window, delays) = Show();
        var signedIn = new List<LoginImmersiveSignedInEventArgs>();
        block.SignedIn += (_, e) => signedIn.Add(e);
        try
        {
            Type(window, block, "not an address");
            Press(window, (Avalonia.Controls.Button)block.ProviderButtons[2]);
            block.Busy.ShouldBe(LoginImmersive.BusyKind.Provider);
            block.ProviderButtons[2].ShouldBeOfType<Button>().IsLoading.ShouldBeTrue();
            block.ProviderButtons[0].ShouldBeOfType<Button>().IsLoading.ShouldBeFalse();
            block.Announcer!.Announcement.ShouldBe("Opening GitHub");
            delays.Advance(900);
            Settle();
            block.Step.ShouldBe(LoginImmersiveStep.Done);
            block.Method.ShouldBe(LoginImmersiveMethod.Provider);
            block.Provider!.Name.ShouldBe("GitHub");
            block.Account.ShouldBe("rowan@fieldnote.app");
            block.DoneView!.Method.ShouldBe("GitHub");
            block.DoneView.Heading!.Description.ShouldBe("GitHub vouched for you. This device remembers you for 30 days.");
            block.Announcer.Announcement.ShouldBe("Signed in with GitHub");
            signedIn.Single().Provider!.Name.ShouldBe("GitHub");

            block.SignOut();
            Settle();
            Type(window, block, "Juno@Fieldnote.App");
            Press(window, (Avalonia.Controls.Button)block.ProviderButtons[0]);
            delays.Advance(900);
            Settle();
            block.Account.ShouldBe("juno@fieldnote.app");
            block.Provider!.Name.ShouldBe("Google");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void While_Busy_Other_Actions_Are_Ignored()
    {
        var (block, window, delays) = Show();
        try
        {
            Press(window, (Avalonia.Controls.Button)block.ProviderButtons[0]);
            Press(window, (Avalonia.Controls.Button)block.ProviderButtons[1]);
            block.ProviderButtons[1].ShouldBeOfType<Button>().IsLoading.ShouldBeFalse();
            Type(window, block, "a@b.co");
            Submit(window, block);
            block.Busy.ShouldBe(LoginImmersive.BusyKind.Provider);
            delays.Advance(900);
            Settle();
            block.Provider!.Name.ShouldBe("Google");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sign_Out_Returns_To_The_Form_And_Says_So()
    {
        var (block, window, delays) = Show();
        var signedOut = 0;
        block.SignedOut += (_, _) => signedOut++;
        try
        {
            SendLink(window, block, delays);
            Press(window, block.SentView!.OpenMail!);
            delays.Advance(1000);
            Settle();
            Press(window, block.DoneView!.SignOut!);
            block.Step.ShouldBe(LoginImmersiveStep.Form);
            block.Account.ShouldBeNull();
            block.Email.ShouldBe("  Iris.Vance@Fieldnote.App ");
            signedOut.ShouldBe(1);
            block.Announcer!.Announcement.ShouldBe("Signed out");
            block.FormView!.Email!.IsFocused.ShouldBeTrue();
            block.EmailError.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Header_Switch_Flips_The_Mode_And_The_Heading()
    {
        var (block, window, delays) = Show();
        try
        {
            Press(window, block.ModeSwitch!);
            block.Mode.ShouldBe(LoginImmersiveMode.SignUp);
            block.Classes.Contains(":signup").ShouldBeTrue();
            block.ModeSwitch!.IsSignUp.ShouldBeTrue();
            AutomationProperties.GetName(block.ModeSwitch).ShouldBe("Have an account? Sign in");
            block.FormView!.HeadingHost!.Current.ShouldBe(block.FormView.SignUpHeading);
            block.Announcer!.Announcement.ShouldBe("Create an account");
            AutomationProperties.GetName(block).ShouldBe("Start your Fieldnote");

            SendLink(window, block, delays);
            block.Step.ShouldBe(LoginImmersiveStep.Sent);
            Press(window, block.SentView!.OpenMail!);
            delays.Advance(1000);
            Settle();
            block.DoneView!.Heading!.Title.ShouldBe("Account ready");

            Press(window, block.ModeSwitch);
            block.Mode.ShouldBe(LoginImmersiveMode.SignIn);
            block.Step.ShouldBe(LoginImmersiveStep.Form);
            block.FormView.HeadingHost.Current.ShouldBe(block.FormView.SignInHeading);
            block.Announcer.Announcement.ShouldBe("Sign in");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mode_Can_Be_Set_From_Outside()
    {
        var (block, window, _) = Show(b => b.Mode = LoginImmersiveMode.SignUp);
        try
        {
            block.FormView!.HeadingHost!.Current.ShouldBe(block.FormView.SignUpHeading);
            block.ModeSwitch!.IsSignUp.ShouldBeTrue();
            block.Mode = LoginImmersiveMode.SignIn;
            Settle();
            block.FormView.HeadingHost.Current.ShouldBe(block.FormView.SignInHeading);
            block.Announcer!.History.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Legal_Links_Without_An_Address_Show_A_Note()
    {
        var (block, window, _) = Show();
        var navigations = new List<LoginImmersiveNavigateEventArgs>();
        block.Navigate += (_, e) => navigations.Add(e);
        try
        {
            Press(window, block.TermsButton!);
            navigations.Single().Link.ShouldBe(LoginImmersiveLink.Terms);
            navigations[0].Href.ShouldBeNull();
            block.NoteText.ShouldBe("In your app, this opens the terms of service.");
            block.Announcer!.Announcement.ShouldBe("In your app, this opens the terms of service.");
            Press(window, block.PrivacyButton!);
            block.NoteText.ShouldBe("In your app, this opens the privacy policy.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Handled_Navigate_Shows_No_Note()
    {
        var command = new RecordingCommand();
        var (block, window, _) = Show(b =>
        {
            b.TermsHref = "https://fieldnote.app/terms";
            b.NavigateCommand = command;
        });
        block.Navigate += (_, e) => e.Handled = e.Link == LoginImmersiveLink.Privacy;
        try
        {
            Press(window, block.PrivacyButton!);
            block.NoteText.ShouldBeNull();
            command.Parameters.ShouldBeEmpty();
            Press(window, block.TermsButton!);
            block.NoteText.ShouldBeNull();
            command.Parameters.Single().ShouldBeOfType<LoginImmersiveNavigateEventArgs>().Href.ShouldBe("https://fieldnote.app/terms");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Handlers_Replace_The_Demo()
    {
        var sends = new List<(string Email, LoginImmersiveMode Mode)>();
        var mailOpened = new List<string>();
        var (block, window, delays) = Show(b =>
        {
            b.SendLinkAsync = (email, mode, _) =>
            {
                sends.Add((email, mode));
                return Task.CompletedTask;
            };
            b.OpenMailAppAsync = (email, _) =>
            {
                mailOpened.Add(email);
                return Task.FromResult(mailOpened.Count > 1);
            };
        });
        try
        {
            Type(window, block, "teo@fieldnote.app");
            Submit(window, block);
            block.Busy.ShouldBe(LoginImmersive.BusyKind.LinkSentHold);
            sends.Single().ShouldBe(("teo@fieldnote.app", LoginImmersiveMode.SignIn));
            delays.Advance(625);
            Settle();
            block.Step.ShouldBe(LoginImmersiveStep.Sent);
            block.NoteText.ShouldBeNull();
            Press(window, block.SentView!.OpenMail!);
            block.Step.ShouldBe(LoginImmersiveStep.Sent);
            block.IsBusy.ShouldBeFalse();
            Press(window, block.SentView.OpenMail!);
            block.Step.ShouldBe(LoginImmersiveStep.Done);
            mailOpened.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Send_Shows_An_Error_And_Stays_On_The_Form()
    {
        var (block, window, _) = Show(b => b.SendLinkAsync = (_, _, _) => Task.FromException(new InvalidOperationException("down")));
        try
        {
            Type(window, block, "teo@fieldnote.app");
            Submit(window, block);
            block.Step.ShouldBe(LoginImmersiveStep.Form);
            block.IsBusy.ShouldBeFalse();
            block.EmailError.ShouldBe("The link didn't go out. Try again in a moment.");
            block.Announcer!.Announcement.ShouldBe("The link didn't go out. Try again in a moment.");
            Type(window, block, "teo@fieldnote.app ");
            block.EmailError.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Cancelled_Provider_Returns_To_The_Form()
    {
        var (block, window, _) = Show(b => b.SignInWithProviderAsync = (_, _, _) => Task.FromResult<string?>(null));
        try
        {
            Press(window, (Avalonia.Controls.Button)block.ProviderButtons[1]);
            block.Step.ShouldBe(LoginImmersiveStep.Form);
            block.IsBusy.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mail_App_Address_Navigates_And_The_App_Completes_The_Sign_In()
    {
        var (block, window, delays) = Show(b => b.MailAppUri = "mailto:");
        var navigations = new List<LoginImmersiveNavigateEventArgs>();
        block.Navigate += (_, e) =>
        {
            navigations.Add(e);
            e.Handled = true;
        };
        try
        {
            SendLink(window, block, delays);
            block.NoteText.ShouldBeNull();
            Press(window, block.SentView!.OpenMail!);
            navigations.Single().Link.ShouldBe(LoginImmersiveLink.MailApp);
            navigations[0].Href.ShouldBe("mailto:");
            block.Step.ShouldBe(LoginImmersiveStep.Sent);
            block.CompleteSignIn();
            Settle();
            block.Step.ShouldBe(LoginImmersiveStep.Done);
            block.Account.ShouldBe("iris.vance@fieldnote.app");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Clears_Everything_Without_Events()
    {
        var (block, window, delays) = Show();
        var events = 0;
        block.SignedOut += (_, _) => events++;
        try
        {
            SendLink(window, block, delays);
            block.Reset();
            Settle();
            block.Step.ShouldBe(LoginImmersiveStep.Form);
            block.Email.ShouldBe(string.Empty);
            block.FormView!.Email!.Text.ShouldBe(string.Empty);
            block.Account.ShouldBeNull();
            block.NoteText.ShouldBeNull();
            delays.Pending.ShouldBe(0);
            events.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Providers_And_Labels_Show()
    {
        var (block, window, _) = Show(b =>
        {
            b.Providers.Add(new LoginImmersiveProvider("Okta"));
            b.Providers.Add(new LoginImmersiveProvider("Entra"));
            b.BrandName = "Northdesk";
            b.Labels = new LoginImmersiveLabels { SendLink = "Send me a link", SignInTitle = p => $"Welcome to {p}" };
        });
        try
        {
            block.ProviderButtons.Count.ShouldBe(2);
            AutomationProperties.GetName(block.ProviderButtons[1]).ShouldBe("Continue with Entra");
            block.FormView!.Send!.Content.ShouldBe("Send me a link");
            block.FormView.SignInHeading!.Title.ShouldBe("Welcome to Northdesk");
            block.ProvidersSource = [new LoginImmersiveProvider("Only")];
            Settle();
            block.ProviderButtons.Count.ShouldBe(1);
            block.IsProvidersVisible = false;
            Settle();
            block.FormView.Providers!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Email_Property_Is_Two_Way()
    {
        var (block, window, _) = Show(b => b.Email = "juno@fieldnote.app");
        try
        {
            block.FormView!.Email!.Text.ShouldBe("juno@fieldnote.app");
            Type(window, block, "iris@fieldnote.app");
            block.Email.ShouldBe("iris@fieldnote.app");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Headings_Take_Focus_But_Are_Not_Tab_Stops()
    {
        var (block, window, _) = Show();
        try
        {
            var heading = block.FormView!.SignInHeading!;
            heading.Focusable.ShouldBeTrue();
            heading.IsTabStop.ShouldBeFalse();
            block.Glass!.IsHitTestVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Pixel_Access_The_Card_Falls_Back_To_A_Solid_Surface()
    {
        var (block, window, _) = Show();
        try
        {
            block.Backdrop!.FrostBuilds.ShouldBeGreaterThan(0);
            block.Glass!.Backdrop.ShouldBe(block.Backdrop);
            // Headless drawing cannot read pixels back: no frosted copy, so the glass is solid.
            if (block.Backdrop.Frost is null)
            {
                block.Glass.IsFrosted.ShouldBeFalse();
            }
            block.Glass.ReduceTransparency = true;
            block.Glass.IsFrosted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Detaching_Abandons_What_Is_In_Flight()
    {
        var (block, window, delays) = Show();
        try
        {
            Type(window, block, "teo@fieldnote.app");
            Submit(window, block);
            window.Content = null;
            Settle();
            delays.Advance(2000);
            block.Step.ShouldBe(LoginImmersiveStep.Form);
            block.IsBusy.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
