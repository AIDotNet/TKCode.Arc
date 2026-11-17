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

/// <summary>A manual clock for the flow's pauses: <see cref="Advance"/> completes the delays that fall due, in order.</summary>
internal sealed class LoginSplitDelays
{
    private readonly List<Entry> _pending = new();

    private sealed record Entry(TimeSpan Due, TaskCompletionSource Source);

    public TimeSpan Now { get; private set; }

    /// <summary>Every delay asked for so far (tests check the flow's pauses).</summary>
    public List<TimeSpan> Requested { get; } = new();

    public int Pending => _pending.Count(e => !e.Source.Task.IsCompleted);

    public Task Delay(TimeSpan time, CancellationToken token)
    {
        Requested.Add(time);
        var source = new TaskCompletionSource();
        token.Register(() => source.TrySetCanceled(token));
        _pending.Add(new Entry(Now + time, source));
        return source.Task;
    }

    public void Advance(double milliseconds)
    {
        var end = Now + TimeSpan.FromMilliseconds(milliseconds);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            var next = _pending.Where(e => !e.Source.Task.IsCompleted && e.Due <= end).OrderBy(e => e.Due).FirstOrDefault();
            if (next is null)
            {
                break;
            }
            Now = next.Due;
            next.Source.TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }
        Now = end;
        _pending.RemoveAll(e => e.Source.Task.IsCompleted);
        Dispatcher.UIThread.RunJobs();
    }
}

public class LoginSplitTests
{
    internal static (Window Window, LoginSplit Block, LoginSplitDelays Delays) Show(
        LoginSplit? block = null, double width = 1440, double height = 900, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        block ??= Sample();
        var delays = new LoginSplitDelays();
        block.Delay = delays.Delay;
        var window = TestHelpers.Show(block, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return (window, block, delays);
    }

    internal static LoginSplit Sample()
    {
        var block = new LoginSplit { IsFullScreen = true };
        block.Accounts.Add(new LoginSplitAccount("Rowan Hale", "rowan@fieldnote.app"));
        block.Accounts.Add(new LoginSplitAccount("Iris Vance", "iris@fieldnote.app"));
        return block;
    }

    private static LoginSplitEmailView EmailView(LoginSplit block) => (LoginSplitEmailView)block.CurrentView!;

    private static LoginSplitPasswordView PasswordView(LoginSplit block) => (LoginSplitPasswordView)block.CurrentView!;

    private static LoginSplitSessionView SessionView(LoginSplit block) => (LoginSplitSessionView)block.CurrentView!;

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    private static void Press(Avalonia.Controls.Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Activate(Window window, Avalonia.Controls.Button button)
    {
        button.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static void ToPassword(LoginSplit block, LoginSplitDelays delays, string email = "abc.def@example.com")
    {
        EmailView(block).EmailInput.Text = email;
        Press(EmailView(block).ContinueButton);
        delays.Advance(650);
    }

    private static void SignInWith(LoginSplit block, LoginSplitDelays delays, string password)
    {
        PasswordView(block).PasswordField.Text = password;
        Press(PasswordView(block).SubmitButton);
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
    public void Defaults_Show_The_Email_Step_With_Three_Providers()
    {
        var (window, block, _) = Show(new LoginSplit());
        try
        {
            block.Step.ShouldBe(LoginSplitStep.Email);
            block.IsFullScreen.ShouldBeFalse();
            block.BrandName.ShouldBe("Fieldnote");
            block.DemoPassword.ShouldBe("fieldnote-demo");
            block.ResendSeconds.ShouldBe(30);
            block.Providers.Select(p => p.Name).ShouldBe(["Google", "Apple", "GitHub"]);
            block.Account.ShouldBeNull();
            block.StatusText.ShouldBeNull();
            var view = EmailView(block);
            view.EmailInput.Label.ShouldBe("Email");
            view.ContinueButton.Content.ShouldBe("Continue");
            view.ProviderButtons.Count.ShouldBe(3);
            view.ProviderButtons.Select(AutomationProperties.GetName).ShouldBe(["Continue with Google", "Continue with Apple", "Continue with GitHub"]);
            AutomationProperties.GetName(view.ProviderGroup!).ShouldBe("Single sign-on");
            AutomationProperties.GetName(block).ShouldBe("Sign in to Fieldnote");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Framed_And_Full_Screen_Frames()
    {
        var (window, block, _) = Show(new LoginSplit(), width: 744, height: 700);
        try
        {
            block.MinHeight.ShouldBe(640);
            block.CornerRadius.TopLeft.ShouldBe(26);
            block.BorderThickness.Left.ShouldBe(1);
            block.IsFullScreen = true;
            Dispatcher.UIThread.RunJobs();
            block.Classes.Contains(LoginSplit.FullScreenPseudoClass).ShouldBeTrue();
            block.CornerRadius.TopLeft.ShouldBe(0);
            block.BorderThickness.Left.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(1440, true, true, false, 5, 6)]
    [InlineData(1100, true, true, false, 5, 6)]
    [InlineData(1099, false, true, false, 1, 1)]
    [InlineData(760, false, true, false, 1, 1)]
    [InlineData(759, false, false, false, 1, 0)]
    [InlineData(479, false, false, true, 1, 0)]
    [InlineData(390, false, false, true, 1, 0)]
    public void Breakpoints_Follow_The_Blocks_Own_Width(double width, bool wide, bool split, bool compact, double form, double photo)
    {
        var (window, block, _) = Show(width: width);
        try
        {
            block.Classes.Contains(LoginSplit.WidePseudoClass).ShouldBe(wide);
            block.Classes.Contains(LoginSplit.SplitPseudoClass).ShouldBe(split);
            block.Classes.Contains(LoginSplit.StackedPseudoClass).ShouldBe(!split);
            block.Classes.Contains(LoginSplit.CompactPseudoClass).ShouldBe(compact);
            var layout = TestHelpers.Descendant<Grid>(block, LoginSplit.LayoutPart);
            layout.ColumnDefinitions[0].Width.Value.ShouldBe(form);
            layout.ColumnDefinitions[1].Width.Value.ShouldBe(photo);
            TestHelpers.Descendant<Border>(block, LoginSplit.PhotoSidePart).IsVisible.ShouldBe(split);
            var header = TestHelpers.Descendant<DockPanel>(block, "PART_Header");
            header.Margin.ShouldBe(compact ? new Thickness(20, 20, 20, 0) : new Thickness(32, 24, 32, 0));
            var viewport = block.Viewport!;
            viewport.Bounds.Width.ShouldBeLessThanOrEqualTo(384.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wide_Columns_Split_Five_To_Six()
    {
        var (window, block, _) = Show(width: 1440);
        try
        {
            var form = TestHelpers.Descendant<Grid>(block, "PART_FormSide");
            form.Bounds.Width.ShouldBe(1440 * 5 / 11.0, 0.6);
            block.Viewport!.Bounds.Width.ShouldBe(384, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Email_Shows_The_Required_Error_And_Keeps_Focus()
    {
        var (window, block, delays) = Show();
        try
        {
            Press(EmailView(block).ContinueButton);
            Dispatcher.UIThread.RunJobs();
            var input = EmailView(block).EmailInput;
            input.ErrorMessage.ShouldBe("Type your email to continue.");
            input.IsInvalid.ShouldBeTrue();
            block.Step.ShouldBe(LoginSplitStep.Email);
            block.IsBusy.ShouldBeFalse();
            delays.Requested.ShouldBeEmpty();
            Focused(window).ShouldBe(input);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void After_A_Submit_The_Email_Is_Checked_As_It_Is_Typed()
    {
        var (window, block, _) = Show();
        try
        {
            var input = EmailView(block).EmailInput;
            input.Text = "a@b.c";
            Press(EmailView(block).ContinueButton);
            input.ErrorMessage.ShouldBe("That doesn't look like a full email address.");
            input.Text = "";
            input.ErrorMessage.ShouldBe("Type your email to continue.");
            input.Text = "a@b.co";
            input.ErrorMessage.ShouldBeNull();
            input.IsInvalid.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_An_Incomplete_Email_Shows_The_Error_But_An_Empty_One_Does_Not()
    {
        var (window, block, _) = Show();
        try
        {
            var view = EmailView(block);
            view.EmailInput.Focus();
            view.ContinueButton.Focus();
            Dispatcher.UIThread.RunJobs();
            view.EmailInput.ErrorMessage.ShouldBeNull();

            view.EmailInput.Focus();
            view.EmailInput.Text = "ada@";
            view.ContinueButton.Focus();
            Dispatcher.UIThread.RunJobs();
            view.EmailInput.ErrorMessage.ShouldBe("That doesn't look like a full email address.");
            view.EmailInput.Text = "ada@example.com";
            view.EmailInput.ErrorMessage.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Continue_Looks_The_Account_Up_Then_Shows_The_Password_Step()
    {
        var (window, block, delays) = Show();
        var steps = new List<(LoginSplitStep, LoginSplitStep)>();
        block.StepChanged += (_, e) => steps.Add((e.OldStep, e.NewStep));
        try
        {
            var view = EmailView(block);
            view.EmailInput.Text = "  Abc.Def@Example.COM ";
            Press(view.ContinueButton);
            block.IsBusy.ShouldBeTrue();
            block.Classes.Contains(LoginSplit.BusyPseudoClass).ShouldBeTrue();
            view.ContinueButton.IsLoading.ShouldBeTrue();
            view.EmailInput.IsReadOnly.ShouldBeTrue();
            block.StatusText.ShouldBe("Finding your account");
            block.Announcer!.History.ShouldContain("Finding your account");
            delays.Requested.ShouldBe([TimeSpan.FromMilliseconds(650)]);

            delays.Advance(649);
            block.Step.ShouldBe(LoginSplitStep.Email);
            delays.Advance(1);
            block.Step.ShouldBe(LoginSplitStep.Password);
            block.IsBusy.ShouldBeFalse();
            block.Email.ShouldBe("abc.def@example.com");
            block.Account!.Name.ShouldBe("Abc Def");
            block.Account.Email.ShouldBe("abc.def@example.com");
            steps.ShouldBe([(LoginSplitStep.Email, LoginSplitStep.Password)]);
            var password = PasswordView(block);
            password.Chip.Email.ShouldBe("abc.def@example.com");
            AutomationProperties.GetName(password.Chip).ShouldBe("abc.def@example.com, change email");
            password.PasswordField.Description.ShouldBe("Demo password: fieldnote-demo.");
            block.StatusText.ShouldBe("Password for abc.def@example.com");
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(password.PasswordField);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_In_The_Email_Field_Submits()
    {
        var (window, block, delays) = Show();
        try
        {
            var input = EmailView(block).EmailInput;
            input.Focus();
            input.Text = "iris@fieldnote.app";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.IsBusy.ShouldBeTrue();
            delays.Advance(650);
            block.Step.ShouldBe(LoginSplitStep.Password);
            block.Account!.Name.ShouldBe("Iris Vance");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Known_Accounts_Match_Without_Regard_To_Case()
    {
        var (window, block, delays) = Show();
        try
        {
            ToPassword(block, delays, "ROWAN@fieldnote.app");
            block.Account.ShouldBeSameAs(block.Accounts[0]);
            PasswordView(block).Chip.DisplayName.ShouldBe("Rowan Hale");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Busy_Ignores_Other_Actions()
    {
        var (window, block, delays) = Show();
        try
        {
            var view = EmailView(block);
            view.EmailInput.Text = "ada@example.com";
            Press(view.ContinueButton);
            Press(view.ProviderButtons[0]);
            Press(view.ContinueButton);
            view.ProviderButtons[0].IsLoading.ShouldBeFalse();
            delays.Requested.Count.ShouldBe(1);
            delays.Advance(650);
            block.Step.ShouldBe(LoginSplitStep.Password);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Password_Shows_The_Required_Error()
    {
        var (window, block, delays) = Show();
        try
        {
            ToPassword(block, delays);
            Press(PasswordView(block).SubmitButton);
            PasswordView(block).PasswordField.ErrorMessage.ShouldBe("Type your password.");
            block.IsBusy.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Wrong_Password_Shows_The_Error_Selects_It_And_Clears_On_Typing()
    {
        var (window, block, delays) = Show();
        try
        {
            ToPassword(block, delays);
            SignInWith(block, delays, "nope");
            block.IsBusy.ShouldBeTrue();
            PasswordView(block).SubmitButton.IsLoading.ShouldBeTrue();
            block.StatusText.ShouldBe("Checking password");
            delays.Advance(800);
            var field = PasswordView(block).PasswordField;
            field.ErrorMessage.ShouldBe("That password didn't work. Check it, or use Forgot password.");
            block.StatusText.ShouldBe("Password didn't match");
            block.IsBusy.ShouldBeFalse();
            PasswordView(block).SubmitButton.IsLoading.ShouldBeFalse();
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(field);
            field.SelectionStart.ShouldBe(0);
            field.SelectionEnd.ShouldBe(4);
            field.Text = "x";
            field.ErrorMessage.ShouldBeNull();
            block.Step.ShouldBe(LoginSplitStep.Password);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Demo_Password_Signs_In_Through_Accepted_And_Signing()
    {
        var (window, block, delays) = Show();
        var command = new RecordingCommand();
        block.SignedInCommand = command;
        LoginSplitSignedInEventArgs? signedIn = null;
        block.SignedIn += (_, e) => signedIn = e;
        try
        {
            ToPassword(block, delays, "abcx.iod@x.io");
            SignInWith(block, delays, "fieldnote-demo");
            delays.Advance(800);
            var password = PasswordView(block);
            password.SubmitButton.IsLoading.ShouldBeFalse();
            password.SubmitButton.Content.ShouldBeOfType<LoginSplitCheck>();
            AutomationProperties.GetName(password.SubmitButton).ShouldBe("Password accepted");
            password.SubmitSlot.IsCollapsed.ShouldBeTrue();
            block.StatusText.ShouldBe("Password OK");
            block.Step.ShouldBe(LoginSplitStep.Password);

            delays.Advance(775);
            block.Step.ShouldBe(LoginSplitStep.Signing);
            block.IsBusy.ShouldBeTrue();
            var session = SessionView(block);
            ((TextBlock)session.Title.Content!).Text.ShouldBe("Opening Fieldnote…");
            session.Avatar.DisplayName.ShouldBe("Abcx Iod");
            session.Avatar.Initials.ShouldBe("AI");
            block.StatusText.ShouldBe("Signing in as abcx.iod@x.io");
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(session.Title);
            signedIn.ShouldBeNull();

            // Motion is off: the 400 ms wait of reduced motion applies (the progress line has nothing to show).
            delays.Requested.Last().ShouldBe(TimeSpan.FromMilliseconds(400));
            delays.Advance(400);
            block.Step.ShouldBe(LoginSplitStep.Done);
            block.IsBusy.ShouldBeFalse();
            ((TextBlock)session.Title.Content!).Text.ShouldBe("All set, Abcx");
            ((TextBlock)session.Description.Content!).Text.ShouldBe("Signed in with your password. Hand off to your app here.");
            session.ProgressRow.IsOpen.ShouldBeFalse();
            session.SignOutRow.IsOpen.ShouldBeTrue();
            session.SignOutButton.IsVisible.ShouldBeTrue();
            block.StatusText.ShouldBe("Signed in with your password");
            signedIn.ShouldNotBeNull();
            signedIn.Account.Email.ShouldBe("abcx.iod@x.io");
            signedIn.Method.ShouldBe(LoginSplitMethod.Password);
            signedIn.Provider.ShouldBeNull();
            command.Parameters.ShouldBe([signedIn]);
            Focused(window).ShouldBe(session.Title);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Waits_The_Whole_Signing_Time()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, block, delays) = Show(level: ArcMotionLevel.Full);
        try
        {
            ToPassword(block, delays);
            clock.Advance(TimeSpan.FromSeconds(1));
            SignInWith(block, delays, "fieldnote-demo");
            delays.Advance(800 + 775);
            block.Step.ShouldBe(LoginSplitStep.Signing);
            delays.Requested.Last().ShouldBe(TimeSpan.FromMilliseconds(1700));
            var progress = SessionView(block).Progress;
            clock.Advance(TimeSpan.FromMilliseconds(250));
            progress.Progress.ShouldBe(0, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(710));
            progress.Progress.ShouldBe(0.5, 0.05);
            clock.Advance(TimeSpan.FromMilliseconds(800));
            progress.Progress.ShouldBe(1, 0.001);
            delays.Advance(1699);
            block.Step.ShouldBe(LoginSplitStep.Signing);
            delays.Advance(1);
            block.Step.ShouldBe(LoginSplitStep.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Shortens_Only_The_Signing_Wait()
    {
        var (window, block, delays) = Show(level: ArcMotionLevel.Reduced);
        try
        {
            ToPassword(block, delays);
            SignInWith(block, delays, "fieldnote-demo");
            delays.Advance(800 + 775);
            delays.Requested.ShouldBe(new[] { 650, 800, 775, 400 }.Select(ms => TimeSpan.FromMilliseconds(ms)).ToArray());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Single_Sign_On_Skips_The_Password()
    {
        var (window, block, delays) = Show();
        LoginSplitSignedInEventArgs? signedIn = null;
        block.SignedIn += (_, e) => signedIn = e;
        try
        {
            var view = EmailView(block);
            var github = view.ProviderButtons[2];
            Press(github);
            github.IsLoading.ShouldBeTrue();
            view.EmailInput.IsReadOnly.ShouldBeTrue();
            block.StatusText.ShouldBe("Opening GitHub");
            delays.Advance(1000);
            block.Step.ShouldBe(LoginSplitStep.Signing);
            block.Account.ShouldBeSameAs(block.Accounts[0]);
            delays.Advance(400);
            block.Step.ShouldBe(LoginSplitStep.Done);
            ((TextBlock)SessionView(block).Title.Content!).Text.ShouldBe("All set, Rowan");
            ((TextBlock)SessionView(block).Description.Content!).Text.ShouldBe("Signed in with GitHub. Hand off to your app here.");
            block.StatusText.ShouldBe("Signed in with GitHub");
            signedIn!.Method.ShouldBe(LoginSplitMethod.Provider);
            signedIn.Provider!.Name.ShouldBe("GitHub");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Single_Sign_On_Uses_A_Valid_Email_Or_The_Default_Account()
    {
        var (window, block, delays) = Show();
        try
        {
            EmailView(block).EmailInput.Text = "Juno.Park@fieldnote.app";
            Press(EmailView(block).ProviderButtons[0]);
            delays.Advance(1000);
            block.Account!.Name.ShouldBe("Juno Park");
            block.Account.Email.ShouldBe("juno.park@fieldnote.app");
        }
        finally
        {
            window.Close();
        }

        var custom = Sample();
        custom.DefaultAccount = new LoginSplitAccount("Teo Marsh", "teo@fieldnote.app");
        (window, block, delays) = Show(custom);
        try
        {
            EmailView(block).EmailInput.Text = "not an email";
            Press(EmailView(block).ProviderButtons[1]);
            delays.Advance(1000);
            block.Account.ShouldBeSameAs(custom.DefaultAccount);
        }
        finally
        {
            window.Close();
        }

        (window, block, delays) = Show(new LoginSplit());
        try
        {
            Press(EmailView(block).ProviderButtons[1]);
            delays.Advance(1000);
            block.Account!.Name.ShouldBe("Rowan Hale");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Forgot_Password_Sends_A_Link_And_Counts_Down_To_Resend()
    {
        var (window, block, delays) = Show();
        var sent = new List<bool>();
        block.ResetLinkSent += (_, e) => sent.Add(e.IsResend);
        try
        {
            ToPassword(block, delays);
            var view = PasswordView(block);
            Press(view.ForgotButton);
            view.ForgotButton.IsInert.ShouldBeTrue();
            AutomationProperties.GetName(view.ForgotButton).ShouldBe("Sending…");
            block.StatusText.ShouldBe("Sending a reset link");
            view.Notice.IsOpen.ShouldBeFalse();
            Press(view.ForgotButton);
            delays.Requested.Count(t => t == TimeSpan.FromMilliseconds(900)).ShouldBe(1);

            delays.Advance(900);
            AutomationProperties.GetName(view.ForgotButton).ShouldBe("Link sent");
            view.ForgotButton.IsInert.ShouldBeTrue();
            view.Notice.IsOpen.ShouldBeTrue();
            view.NoticeText.Inlines!.Select(i => ((Avalonia.Controls.Documents.Run)i).Text).ShouldBe(["We emailed ", "abc.def@example.com", "."]);
            block.StatusText.ShouldBe("Reset link sent to abc.def@example.com");
            sent.ShouldBe([false]);
            block.ResendLeft.ShouldBe(30);
            view.ResendButton.IsInert.ShouldBeTrue();
            AutomationProperties.GetName(view.ResendButton).ShouldBe("Resend link, available in 30 seconds");
            view.Countdown!.Text.ShouldBe("0:30");

            Press(view.ResendButton);
            sent.Count.ShouldBe(1);

            delays.Advance(1000);
            block.ResendLeft.ShouldBe(29);
            view.Countdown!.Text.ShouldBe("0:29");
            delays.Advance(28_000);
            AutomationProperties.GetName(view.ResendButton).ShouldBe("Resend link, available in 1 second");
            delays.Advance(1000);
            block.ResendLeft.ShouldBe(0);
            view.ResendButton.IsInert.ShouldBeFalse();
            AutomationProperties.GetName(view.ResendButton).ShouldBe("Resend link");
            view.Countdown.ShouldBeNull();

            view.ResendButton.Focus(NavigationMethod.Tab);
            Press(view.ResendButton);
            sent.ShouldBe([false, true]);
            block.ResendLeft.ShouldBe(30);
            block.StatusText.ShouldBe("New reset link sent");
            Focused(window).ShouldBe(view.ResendButton);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Chip_Goes_Back_To_The_Email_Step_Keeping_The_Email()
    {
        var (window, block, delays) = Show();
        try
        {
            ToPassword(block, delays, "teo@fieldnote.app");
            Press(PasswordView(block).Chip);
            block.Step.ShouldBe(LoginSplitStep.Email);
            block.Account.ShouldBeNull();
            EmailView(block).EmailInput.Text.ShouldBe("teo@fieldnote.app");
            EmailView(block).EmailInput.ErrorMessage.ShouldBeNull();
            block.StatusText.ShouldBe("Change your email");
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(EmailView(block).EmailInput);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sign_Out_Returns_To_The_Email_Step_With_The_Accounts_Email()
    {
        var (window, block, delays) = Show();
        LoginSplitAccount? signedOut = null;
        block.SignedOut += (_, e) => signedOut = e.Account;
        try
        {
            ToPassword(block, delays, "iris@fieldnote.app");
            SignInWith(block, delays, "fieldnote-demo");
            delays.Advance(800 + 775 + 400);
            block.Step.ShouldBe(LoginSplitStep.Done);
            Activate(window, SessionView(block).SignOutButton);
            block.Step.ShouldBe(LoginSplitStep.Email);
            signedOut!.Name.ShouldBe("Iris Vance");
            block.Email.ShouldBe("iris@fieldnote.app");
            EmailView(block).EmailInput.Text.ShouldBe("iris@fieldnote.app");
            block.StatusText.ShouldBe("Signed out");
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(EmailView(block).EmailInput);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Links_Without_An_Address_Say_Where_They_Would_Go()
    {
        var (window, block, _) = Show();
        var links = new List<LoginSplitLink>();
        block.Navigate += (_, e) => links.Add(e.Link);
        var command = new RecordingCommand();
        block.NavigateCommand = command;
        try
        {
            Press(TestHelpers.Descendant<LoginSplitTextButton>(block, LoginSplit.SignUpPart));
            block.StatusText.ShouldBe("In your app, this goes to sign-up.");
            Press(TestHelpers.Descendant<LoginSplitTextButton>(block, LoginSplit.TermsPart));
            block.StatusText.ShouldBe("In your app, this goes to your terms.");
            Press(TestHelpers.Descendant<LoginSplitTextButton>(block, LoginSplit.PrivacyPart));
            block.StatusText.ShouldBe("In your app, this goes to your privacy policy.");
            links.ShouldBe([LoginSplitLink.SignUp, LoginSplitLink.Terms, LoginSplitLink.Privacy]);
            command.Parameters.ShouldBe([LoginSplitLink.SignUp, LoginSplitLink.Terms, LoginSplitLink.Privacy]);
            block.Announcer!.History.Last().ShouldBe("In your app, this goes to your privacy policy.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Handled_Link_Shows_No_Status()
    {
        var (window, block, _) = Show();
        block.Navigate += (_, e) => e.Handled = true;
        try
        {
            Press(TestHelpers.Descendant<LoginSplitTextButton>(block, LoginSplit.TermsPart));
            block.StatusText.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Handlers_Replace_The_Simulation()
    {
        var block = Sample();
        string? looked = null;
        block.LookupAccountAsync = (email, _) =>
        {
            looked = email;
            return Task.FromResult<LoginSplitAccount?>(new LoginSplitAccount("Mira Holt", email));
        };
        block.VerifyPasswordAsync = (_, password, _) => Task.FromResult(password == "open sesame");
        var (window, _, delays) = Show(block);
        try
        {
            EmailView(block).EmailInput.Text = "Mira@Example.com";
            Press(EmailView(block).ContinueButton);
            Dispatcher.UIThread.RunJobs();
            looked.ShouldBe("mira@example.com");
            block.Step.ShouldBe(LoginSplitStep.Password);
            block.Account!.Name.ShouldBe("Mira Holt");
            PasswordView(block).PasswordField.Description.ShouldBeNull();
            SignInWith(block, delays, "fieldnote-demo");
            Dispatcher.UIThread.RunJobs();
            PasswordView(block).PasswordField.ErrorMessage.ShouldNotBeNull();
            SignInWith(block, delays, "open sesame");
            Dispatcher.UIThread.RunJobs();
            PasswordView(block).SubmitSlot.IsCollapsed.ShouldBeTrue();
            delays.Requested.ShouldBe([TimeSpan.FromMilliseconds(775)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failing_Handler_Shows_An_Error_And_Ends_Busy()
    {
        var block = Sample();
        block.LookupAccountAsync = (_, _) => Task.FromException<LoginSplitAccount?>(new InvalidOperationException("down"));
        var (window, _, _) = Show(block);
        try
        {
            EmailView(block).EmailInput.Text = "ada@example.com";
            Press(EmailView(block).ContinueButton);
            Dispatcher.UIThread.RunJobs();
            block.Step.ShouldBe(LoginSplitStep.Email);
            block.IsBusy.ShouldBeFalse();
            EmailView(block).ContinueButton.IsLoading.ShouldBeFalse();
            EmailView(block).EmailInput.IsReadOnly.ShouldBeFalse();
            EmailView(block).EmailInput.ErrorMessage.ShouldBe("Something went wrong. Try again.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Cancels_And_Returns_To_An_Empty_Email_Step()
    {
        var (window, block, delays) = Show();
        try
        {
            ToPassword(block, delays);
            SignInWith(block, delays, "fieldnote-demo");
            block.Reset();
            block.Step.ShouldBe(LoginSplitStep.Email);
            block.IsBusy.ShouldBeFalse();
            block.Email.ShouldBe(string.Empty);
            block.StatusText.ShouldBeNull();
            delays.Advance(5000);
            block.Step.ShouldBe(LoginSplitStep.Email);
            EmailView(block).EmailInput.Text.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Email_Property_And_Field_Stay_In_Sync()
    {
        var (window, block, _) = Show();
        try
        {
            block.Email = "ada@example.com";
            EmailView(block).EmailInput.Text.ShouldBe("ada@example.com");
            EmailView(block).EmailInput.Text = "grace@example.com";
            block.Email.ShouldBe("grace@example.com");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Labels_And_Providers_Are_Used()
    {
        var block = new LoginSplit
        {
            BrandName = "Northdesk",
            Labels = new LoginSplitLabels { EmailTitle = "Welcome", Continue = "Next", SignUp = "Join" }
        };
        block.Providers.Clear();
        block.Providers.Add(new LoginSplitProvider("Okta"));
        var (window, _, _) = Show(block);
        try
        {
            EmailView(block).ContinueButton.Content.ShouldBe("Next");
            EmailView(block).ProviderButtons.Count.ShouldBe(1);
            AutomationProperties.GetName(EmailView(block).ProviderButtons[0]).ShouldBe("Continue with Okta");
            AutomationProperties.GetName(TestHelpers.Descendant<LoginSplitTextButton>(block, LoginSplit.SignUpPart)).ShouldBe("Join");
            AutomationProperties.GetName(block).ShouldBe("Sign in to Northdesk");
            block.Providers.Clear();
            Dispatcher.UIThread.RunJobs();
            EmailView(block).ProviderButtons.ShouldBeEmpty();
            EmailView(block).Divider.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Order_Runs_Through_Header_Step_And_Footer()
    {
        var (window, block, _) = Show();
        try
        {
            var signUp = TestHelpers.Descendant<LoginSplitTextButton>(block, LoginSplit.SignUpPart);
            signUp.Focus(NavigationMethod.Tab);
            var order = new List<IInputElement?>();
            for (var i = 0; i < 7; i++)
            {
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                order.Add(Focused(window));
            }
            var view = EmailView(block);
            order[0].ShouldBe(view.EmailInput);
            order[1].ShouldBe(view.ContinueButton);
            order[2].ShouldBe(view.ProviderButtons[0]);
            order[3].ShouldBe(view.ProviderButtons[1]);
            order[4].ShouldBe(view.ProviderButtons[2]);
            order[5].ShouldBe(TestHelpers.Descendant<LoginSplitTextButton>(block, LoginSplit.TermsPart));
            order[6].ShouldBe(TestHelpers.Descendant<LoginSplitTextButton>(block, LoginSplit.PrivacyPart));
        }
        finally
        {
            window.Close();
        }
    }
}
