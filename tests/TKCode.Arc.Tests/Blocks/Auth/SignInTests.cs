using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Auth;

public class SignInTests
{
    private static Window Show(SignIn signIn, double width = 520, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(signIn, level);
        window.Width = width;
        window.Height = 720;
        Pump();
        return window;
    }

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A card whose simulated waits finish at once.</summary>
    private static SignIn Card(Action<SignIn>? configure = null)
    {
        var signIn = new SignIn { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        signIn.Delay = static (_, _) => Task.CompletedTask;
        configure?.Invoke(signIn);
        return signIn;
    }

    private static SignInEmailStep EmailStep(SignIn s) => (SignInEmailStep)s.CurrentStep!;

    private static SignInCodeStep CodeStep(SignIn s) => (SignInCodeStep)s.CurrentStep!;

    private static SignInDoneStep DoneStep(SignIn s) => (SignInDoneStep)s.CurrentStep!;

    private static void Click(Avalonia.Controls.Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Pump();
    }

    private static void TypeInto(Window window, Control target, string text)
    {
        target.Focus();
        Pump();
        foreach (var c in text)
        {
            window.KeyTextInput(c.ToString());
            Dispatcher.UIThread.RunJobs();
        }
        Pump();
    }

    private static void ToCode(SignIn s, string email = "emma@northwind.studio")
    {
        s.Email = email;
        Pump();
        s.SubmitEmail();
        Pump();
    }

    private sealed class Recorder : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    [AvaloniaFact]
    public void Defaults_Are_Arcs_Preview()
    {
        var s = Card();
        var window = Show(s);
        try
        {
            s.Step.ShouldBe(SignInStep.Email);
            s.DemoCode.ShouldBe("123456");
            s.ResendSeconds.ShouldBe(30);
            s.Title.ShouldBe("Sign in to Arc");
            s.Status.ShouldBe(string.Empty);
            AutomationProperties.GetName(s).ShouldBe("Sign in");
            s.Card!.MaxWidth.ShouldBe(432);
            s.Card.Bounds.Width.ShouldBe(432, 0.5);
            s.Card.CornerRadius.TopLeft.ShouldBe(34);
            var step = EmailStep(s);
            step.Title.Text.ShouldBe("Sign in to Arc");
            step.Description.Text.ShouldBe("Enter your email and we will send you a 6 digit code.");
            step.Email.Label.ShouldBe("Email");
            step.Email.Placeholder.ShouldBe("name@example.com");
            step.Continue.Content.ShouldBe("Continue");
            step.DividerText.Text.ShouldBe("or");
            step.PasskeyText.Text.ShouldBe("Sign in with a passkey");
            step.ProviderButtons.Select(p => p.Provider.Name).ShouldBe(["Google", "Apple", "GitHub"]);
            step.ProviderButtons.Select(p => AutomationProperties.GetName(p.Button)).ShouldBe(["Continue with Google", "Continue with Apple", "Continue with GitHub"]);
            AutomationProperties.GetName(step.Providers).ShouldBe("Single sign-on");
            s.SignUpButton!.Text.ShouldBe("Sign up");
            // Arc's step padding 32, heading 36px display type.
            step.Padding.ShouldBe(new Thickness(32));
            step.Title.FontSize.ShouldBe(36);
            s.IsNarrow.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Email_Checks_Follow_Arcs_Rules()
    {
        SignIn.IsEmailAddress("name@example.com").ShouldBeTrue();
        SignIn.IsEmailAddress("  name@example.com ").ShouldBeTrue();
        SignIn.IsEmailAddress("name@example.c").ShouldBeFalse();
        SignIn.IsEmailAddress("name@example").ShouldBeFalse();
        SignIn.IsEmailAddress("na me@example.com").ShouldBeFalse();
        SignIn.IsEmailAddress("").ShouldBeFalse();
        SignIn.SuggestEmail("Emma@Gmial.com").ShouldBe("emma@gmail.com");
        SignIn.SuggestEmail("a@hotmial.com").ShouldBe("a@hotmail.com");
        SignIn.SuggestEmail("a@yaho.com").ShouldBe("a@yahoo.com");
        SignIn.SuggestEmail("a@gmail.com").ShouldBe(string.Empty);
        SignIn.SuggestEmail("a@b@gmial.com").ShouldBe(string.Empty);
        SignIn.SuggestEmail("@gmial.com").ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Continue_With_An_Empty_Field_Asks_For_The_Address_And_Stays()
    {
        var s = Card();
        var window = Show(s);
        try
        {
            Click(EmailStep(s).Continue);
            s.Step.ShouldBe(SignInStep.Email);
            s.EmailError.ShouldBe("Enter your email address.");
            EmailStep(s).Email.ErrorMessage.ShouldBe("Enter your email address.");
            EmailStep(s).Email.IsFocused.ShouldBeTrue();
            // From the first attempt on, the message follows every keystroke.
            s.Email = "emma";
            s.EmailError.ShouldBe("Enter a full address, like name@example.com.");
            s.Email = "emma@northwind.studio";
            s.EmailError.ShouldBe(string.Empty);
            EmailStep(s).Email.ErrorMessage.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Field_Is_Checked_Once_It_Loses_Focus_With_Text()
    {
        var s = Card();
        var window = Show(s);
        try
        {
            var step = EmailStep(s);
            TypeInto(window, step.Email, "emma");
            s.Email.ShouldBe("emma");
            s.EmailError.ShouldBe(string.Empty);
            step.Continue.Focus();
            Pump();
            s.EmailError.ShouldBe("Enter a full address, like name@example.com.");
            step.Email.ErrorMessage.ShouldBe("Enter a full address, like name@example.com.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Domain_Typo_Offers_A_Fix_That_Tab_Reaches()
    {
        var s = Card();
        var window = Show(s);
        try
        {
            var step = EmailStep(s);
            TypeInto(window, step.Email, "emma@gmial.com");
            s.Suggestion.ShouldBe(string.Empty);
            step.Suggestion.IsExpanded.ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Pump();
            s.Suggestion.ShouldBe("emma@gmail.com");
            step.Suggestion.IsExpanded.ShouldBeTrue();
            step.SuggestionBefore.Text.ShouldBe("Did you mean ");
            step.SuggestionButton.Text.ShouldBe("emma@gmail.com");
            step.SuggestionAfter.Text.ShouldBe("?");
            AutomationProperties.GetName(step.SuggestionButton).ShouldBe("Use emma@gmail.com");
            // The browser blurs before it picks the next stop: the new suggestion is the next stop.
            step.SuggestionButton.IsFocused.ShouldBeTrue();
            Click(step.SuggestionButton);
            s.Email.ShouldBe("emma@gmail.com");
            step.Email.Text.ShouldBe("emma@gmail.com");
            s.Status.ShouldBe("Email changed to emma@gmail.com");
            s.Suggestion.ShouldBe(string.Empty);
            step.Email.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Continue_Sends_The_Code_And_Shows_The_Account()
    {
        var pending = new TaskCompletionSource();
        var s = Card(c => c.Delay = (_, _) => pending.Task);
        var window = Show(s);
        try
        {
            s.Email = "  Emma@Northwind.studio ";
            Click(EmailStep(s).Continue);
            s.IsBusy.ShouldBeTrue();
            s.Status.ShouldBe("Sending a code");
            EmailStep(s).Continue.IsLoading.ShouldBeTrue();
            EmailStep(s).Email.IsReadOnly.ShouldBeTrue();
            // A second press, a passkey or a provider while busy does nothing.
            s.SignInWith(SignInMethod.Passkey);
            EmailStep(s).Passkey.IsLoading.ShouldBeFalse();
            pending.SetResult();
            Pump();
            s.IsBusy.ShouldBeFalse();
            s.Step.ShouldBe(SignInStep.Code);
            s.Status.ShouldBe("Code sent");
            s.Account!.Name.ShouldBe("Emma Collins");
            s.Account.Email.ShouldBe("emma@northwind.studio");
            s.ResendIn.ShouldBe(30);
            var code = CodeStep(s);
            code.Title.Text.ShouldBe("Check your email");
            code.Description.Text.ShouldBe("Enter the 6 digit code we sent. It expires in 10 minutes.");
            code.IdentityEmail.Text.ShouldBe("emma@northwind.studio");
            code.Avatar.Initials.ShouldBe("EC");
            code.Change.Content.ShouldBe("Change");
            AutomationProperties.GetName(code.Change).ShouldBe("Change email");
            code.Code.Label.ShouldBe("Verification code");
            code.Code.Description.ShouldBe("For this demo, the code is 123456.");
            code.Verify.Content.ShouldBe("Verify");
            code.Resend.IsWaiting.ShouldBeTrue();
            code.Time!.Text.ShouldBe("0:30");
            AutomationProperties.GetName(code.Resend).ShouldBe("Resend code, available in 30 seconds");
            s.Announcer!.History.ShouldBe(["Sending a code", "Code sent"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Accounts_Follow_Arcs_AccountFor()
    {
        var known = SignInAccount.CreateSamples();
        SignInAccount.ForEmail("jane.doe@acme.io", known).Name.ShouldBe("Jane Doe");
        SignInAccount.ForEmail("jane_doe+news@acme.io", known).Name.ShouldBe("Jane Doe");
        SignInAccount.ForEmail("MARCUS@northwind.studio", known).Name.ShouldBe("Marcus Johnson");
        SignInAccount.ForEmail("marcus.j@other.io", known).Email.ShouldBe("marcus.j@other.io");
        SignInAccount.ForEmail("---@acme.io", known).Name.ShouldBe("Arc member");
        SignInAccount.ForEmail("x@acme.io", null).Name.ShouldBe("X");
        var photo = new DrawingImage();
        var withPhoto = new SignInAccount("Ava Mitchell", "ava@northwind.studio", photo);
        SignInAccount.ForEmail("ava@home.net", [withPhoto]).Photo.ShouldBeSameAs(photo);
        new SignInAccount("Sofia Ramirez", "s@x.io").FirstName.ShouldBe("Sofia");
    }

    [AvaloniaFact]
    public void An_Unknown_Address_Gets_A_Name_And_Initials()
    {
        var s = Card();
        var window = Show(s);
        try
        {
            ToCode(s, "jane.doe@acme.io");
            s.Account!.Name.ShouldBe("Jane Doe");
            CodeStep(s).Avatar.Initials.ShouldBe("JD");
            CodeStep(s).Avatar.Photo.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Verify_Checks_The_Code_Like_Arc()
    {
        var signedIn = new List<SignInSignedInEventArgs>();
        var command = new Recorder();
        var s = Card(c => c.SignedInCommand = command);
        s.SignedIn += (_, e) => signedIn.Add(e);
        var window = Show(s);
        try
        {
            ToCode(s);
            // Incomplete: asks for all digits and keeps the step.
            s.Verify("123");
            s.CodeError.ShouldBe("Enter all 6 digits.");
            CodeStep(s).Code.ErrorMessage.ShouldBe("Enter all 6 digits.");
            // Wrong: clears the slots, says so and focuses the first slot.
            s.Verify("123999");
            s.Step.ShouldBe(SignInStep.Code);
            s.CodeText.ShouldBe(string.Empty);
            CodeStep(s).Code.Value.ShouldBe(string.Empty);
            s.CodeError.ShouldBe("That code didn't match. Check the latest email and try again.");
            s.Status.ShouldBe("Code didn't match");
            CodeStep(s).Code.Slots[0].IsFocused.ShouldBeTrue();
            // Right: signed in with an email code.
            s.Verify("123456");
            Pump();
            s.Step.ShouldBe(SignInStep.Done);
            s.Method.ShouldBe(SignInMethod.EmailCode);
            s.Status.ShouldBe("Signed in with an email code");
            signedIn.Count.ShouldBe(1);
            signedIn[0].Account.Name.ShouldBe("Emma Collins");
            signedIn[0].Method.ToString().ShouldBe("Email code");
            command.Parameters.ShouldBe([s.Account]);
            var done = DoneStep(s);
            done.Title.Text.ShouldBe("Welcome back, Emma");
            done.Email.Text.ShouldBe("emma@northwind.studio");
            done.MethodLabel.Text.ShouldBe("Signed in with");
            done.MethodValue.Text.ShouldBe("Email code");
            done.DeviceLabel.Text.ShouldBe("This browser");
            done.DeviceValue.Text.ShouldBe("Remembered for 30 days");
            done.SignOut.Content.ShouldBe("Sign out");
            done.Avatar.Initials.ShouldBe("EC");
            done.Ring.Value.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_The_Last_Digit_Checks_The_Code_And_Typing_Again_Clears_The_Error()
    {
        var s = Card();
        var window = Show(s);
        try
        {
            ToCode(s);
            var otp = CodeStep(s).Code;
            otp.FocusSlot(0);
            Pump();
            foreach (var c in "000000")
            {
                window.KeyTextInput(c.ToString());
                Dispatcher.UIThread.RunJobs();
            }
            Pump();
            s.CodeError.ShouldBe("That code didn't match. Check the latest email and try again.");
            window.KeyTextInput("1");
            Pump();
            s.CodeText.ShouldBe("1");
            s.CodeError.ShouldBe(string.Empty);
            otp.ErrorMessage.ShouldBeNull();
            foreach (var c in "23456")
            {
                window.KeyTextInput(c.ToString());
                Dispatcher.UIThread.RunJobs();
            }
            Pump();
            s.Step.ShouldBe(SignInStep.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_In_The_Code_Verifies_What_Is_Typed()
    {
        var s = Card();
        var window = Show(s);
        try
        {
            ToCode(s);
            CodeStep(s).Code.FocusSlot(0);
            Pump();
            window.KeyTextInput("1");
            Pump();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            s.CodeError.ShouldBe("Enter all 6 digits.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void While_Checking_The_Code_Edits_Are_Ignored_And_Verify_Shows_Progress()
    {
        var pending = new TaskCompletionSource();
        var s = Card();
        var window = Show(s);
        try
        {
            ToCode(s);
            s.Delay = (_, _) => pending.Task;
            s.Verify("123456");
            s.IsBusy.ShouldBeTrue();
            s.Status.ShouldBe("Checking code");
            CodeStep(s).Verify.IsLoading.ShouldBeTrue();
            CodeStep(s).Code.IsReadOnly.ShouldBeTrue();
            s.ChangeEmail();
            s.Step.ShouldBe(SignInStep.Code);
            pending.SetResult();
            Pump();
            s.Step.ShouldBe(SignInStep.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Resend_Timer_Rolls_Down_And_A_New_Code_Restarts_It()
    {
        var s = Card(c => c.ResendSeconds = 3);
        var window = Show(s);
        try
        {
            ToCode(s);
            var code = CodeStep(s);
            code.Time!.Text.ShouldBe("0:03");
            s.Resend();
            s.Status.ShouldBe("Code sent");
            s.TickResend();
            code.Time.Text.ShouldBe("0:02");
            AutomationProperties.GetName(code.Resend).ShouldBe("Resend code, available in 2 seconds");
            s.TickResend();
            s.TickResend();
            s.ResendIn.ShouldBe(0);
            code.Resend.IsWaiting.ShouldBeFalse();
            AutomationProperties.GetName(code.Resend).ShouldBe("Resend code");
            ((TextBlock)code.ResendSwap.Current!).Text.ShouldBe("Resend code");
            s.Verify("12");
            s.CodeError.ShouldNotBeEmpty();
            Click(code.Resend);
            s.ResendIn.ShouldBe(3);
            s.CodeError.ShouldBe(string.Empty);
            s.Status.ShouldBe("New code sent");
            code.Resend.IsWaiting.ShouldBeTrue();
            code.Time!.Text.ShouldBe("0:03");
            code.Code.Slots[0].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rolling_Time_Uses_Arcs_Format()
    {
        SignInRollingTime.Format(30).ShouldBe("0:30");
        SignInRollingTime.Format(9).ShouldBe("0:09");
        SignInRollingTime.Format(75).ShouldBe("1:15");
        SignInRollingTime.Format(-4).ShouldBe("0:00");
    }

    [AvaloniaFact]
    public void Change_Goes_Back_To_The_Email_With_It_Selected()
    {
        var s = Card();
        var window = Show(s);
        try
        {
            ToCode(s);
            Click(CodeStep(s).Change);
            Pump();
            s.Step.ShouldBe(SignInStep.Email);
            s.Status.ShouldBe("Edit your email");
            s.Email.ShouldBe("emma@northwind.studio");
            var input = EmailStep(s).Email;
            input.Text.ShouldBe("emma@northwind.studio");
            input.IsFocused.ShouldBeTrue();
            input.SelectionStart.ShouldBe(0);
            input.SelectionEnd.ShouldBe("emma@northwind.studio".Length);
            // Going back resets the attempt but not the first touch: an empty field shows no error until the next try.
            s.Email = string.Empty;
            s.EmailError.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Passkey_And_A_Provider_Sign_In_To_The_First_Teammate()
    {
        var pending = new TaskCompletionSource();
        var s = Card(c => c.Delay = (_, _) => pending.Task);
        var window = Show(s);
        try
        {
            Click(EmailStep(s).Passkey);
            s.Status.ShouldBe("Waiting for your passkey");
            EmailStep(s).Passkey.IsLoading.ShouldBeTrue();
            pending.SetResult();
            Pump();
            s.Step.ShouldBe(SignInStep.Done);
            s.Method.ShouldBe(SignInMethod.Passkey);
            s.Account!.Name.ShouldBe("Emma Collins");
            s.Status.ShouldBe("Signed in with Passkey");
            DoneStep(s).MethodValue.Text.ShouldBe("Passkey");

            s.SignOut();
            Pump();
            pending = new TaskCompletionSource();
            var github = EmailStep(s).ProviderButtons.Single(p => p.Provider.Name == "GitHub");
            Click(github.Button);
            s.Status.ShouldBe("Opening GitHub");
            github.Button.IsLoading.ShouldBeTrue();
            EmailStep(s).ProviderButtons[0].Button.IsLoading.ShouldBeFalse();
            pending.SetResult();
            Pump();
            s.Method.ShouldBe(SignInMethod.FromProvider("GitHub"));
            s.Status.ShouldBe("Signed in with GitHub");
            DoneStep(s).MethodValue.Text.ShouldBe("GitHub");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sign_Out_Returns_To_The_Email_With_The_Address()
    {
        var signedOut = new List<SignInSignedOutEventArgs>();
        var s = Card();
        s.SignedOut += (_, e) => signedOut.Add(e);
        var window = Show(s);
        try
        {
            ToCode(s, "jane.doe@acme.io");
            s.Verify("123456");
            Pump();
            DoneStep(s).Title.IsFocused.ShouldBeTrue();
            KeyboardNavigation.GetIsTabStop(DoneStep(s).Title).ShouldBeFalse();
            Click(DoneStep(s).SignOut);
            Pump();
            s.Step.ShouldBe(SignInStep.Email);
            s.Email.ShouldBe("jane.doe@acme.io");
            s.Status.ShouldBe("Signed out");
            signedOut.Single().Account.Name.ShouldBe("Jane Doe");
            EmailStep(s).Email.IsFocused.ShouldBeTrue();
            s.EmailError.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sign_Up_Raises_An_Event_And_Says_So_When_Nobody_Handles_It()
    {
        var command = new Recorder();
        var s = Card(c => c.SignUpCommand = command);
        var window = Show(s);
        try
        {
            Click(s.SignUpButton!);
            s.Status.ShouldBe("Sign up opens in your app");
            command.Parameters.Count.ShouldBe(1);
            s.Email = "x";
            var handled = Card();
            handled.SignUpRequested += (_, e) => e.Handled = true;
            var other = Show(handled);
            Click(handled.SignUpButton!);
            handled.Status.ShouldBe(string.Empty);
            other.Close();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Handlers_Replace_The_Simulation()
    {
        var sent = new List<string>();
        var checkedCodes = new List<(string, string)>();
        var s = Card(c =>
        {
            c.SendCodeAsync = (email, _) =>
            {
                sent.Add(email);
                return Task.FromResult<SignInAccount?>(new SignInAccount("Robin Park", email));
            };
            c.VerifyCodeAsync = (account, code, _) =>
            {
                checkedCodes.Add((account.Email, code));
                return Task.FromResult(code == "424242");
            };
        });
        var window = Show(s);
        try
        {
            ToCode(s, " robin@park.dev ");
            sent.ShouldBe(["robin@park.dev"]);
            s.Account!.Name.ShouldBe("Robin Park");
            // A real check shows no demo hint.
            CodeStep(s).Code.Description.ShouldBeNull();
            s.Verify("123456");
            s.Step.ShouldBe(SignInStep.Code);
            s.Verify("424242");
            s.Step.ShouldBe(SignInStep.Done);
            checkedCodes.ShouldBe([("robin@park.dev", "123456"), ("robin@park.dev", "424242")]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Failing_Handlers_Keep_The_Step_And_Say_So()
    {
        var s = Card(c =>
        {
            c.SendCodeAsync = (_, _) => Task.FromException<SignInAccount?>(new InvalidOperationException());
            c.SignInWithAsync = (_, _) => Task.FromException<SignInAccount?>(new InvalidOperationException());
        });
        var window = Show(s);
        try
        {
            ToCode(s);
            s.Step.ShouldBe(SignInStep.Email);
            s.IsBusy.ShouldBeFalse();
            s.EmailError.ShouldBe("We couldn't send a code. Try again.");
            // Editing the address clears the send error.
            s.Email = "emma@northwind.studios";
            s.EmailError.ShouldBe(string.Empty);
            s.SignInWith(SignInMethod.FromProvider("Apple"));
            s.Step.ShouldBe(SignInStep.Email);
            s.Status.ShouldBe("Couldn't sign in with Apple. Try again.");

            s.SendCodeAsync = null;
            s.VerifyCodeAsync = (_, _, _) => Task.FromException<bool>(new InvalidOperationException());
            ToCode(s);
            s.Verify("123456");
            s.Step.ShouldBe(SignInStep.Code);
            s.CodeError.ShouldBe("We couldn't check the code. Try again.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Abandons_A_Pending_Send()
    {
        CancellationToken seen = default;
        var pending = new TaskCompletionSource<SignInAccount?>();
        var s = Card(c => c.SendCodeAsync = (_, token) =>
        {
            seen = token;
            return pending.Task;
        });
        var window = Show(s);
        try
        {
            ToCode(s);
            s.IsBusy.ShouldBeTrue();
            s.Reset();
            seen.IsCancellationRequested.ShouldBeTrue();
            s.IsBusy.ShouldBeFalse();
            s.Email.ShouldBe(string.Empty);
            pending.SetResult(null);
            Pump();
            s.Step.ShouldBe(SignInStep.Email);
            s.Status.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copy_And_Hint_Can_Be_Changed()
    {
        var s = Card(c =>
        {
            c.Title = "Sign in to Northwind";
            c.DemoCode = "000111";
            c.ContinueLabel = "Send code";
        });
        var window = Show(s);
        try
        {
            EmailStep(s).Title.Text.ShouldBe("Sign in to Northwind");
            EmailStep(s).Continue.Content.ShouldBe("Send code");
            s.ContinueLabel = "Email me a code";
            EmailStep(s).Continue.Content.ShouldBe("Email me a code");
            ToCode(s);
            CodeStep(s).Code.Description.ShouldBe("For this demo, the code is 000111.");
            s.CodeHint = "Check your spam folder too.";
            CodeStep(s).Code.Description.ShouldBe("Check your spam folder too.");
            s.WelcomeFormat = "Hello {0}!";
            s.Verify("000111");
            Pump();
            DoneStep(s).Title.Text.ShouldBe("Hello Emma!");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Providers_And_Passkey_Can_Be_Customised_Or_Hidden()
    {
        var s = Card(c => c.Providers.Add(new SignInProvider("Okta")));
        var window = Show(s);
        try
        {
            var step = EmailStep(s);
            step.ProviderButtons.Select(p => p.Provider.Name).ShouldBe(["Okta"]);
            step.ProviderButtons[0].Glyph.IsVisible.ShouldBeFalse();
            s.Providers.Add(new SignInProvider("Google", SignInProviderMark.Google));
            Pump();
            step.ProviderButtons.Count.ShouldBe(2);
            step.ProviderButtons[1].Glyph.IsVisible.ShouldBeTrue();
            s.IsProvidersVisible = false;
            step.Providers.IsVisible.ShouldBeFalse();
            step.Divider.IsVisible.ShouldBeTrue();
            s.IsPasskeyVisible = false;
            step.Passkey.IsVisible.ShouldBeFalse();
            step.Divider.IsVisible.ShouldBeFalse();
            step.Alternatives.IsVisible.ShouldBeFalse();
            s.IsSignUpVisible = false;
            TestHelpers.Descendant<Control>(s, "PART_Switch").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Follows_Its_Own_Width()
    {
        var s = Card(c => c.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch);
        var window = Show(s, width: 360);
        try
        {
            s.IsNarrow.ShouldBeTrue();
            s.IsCompact.ShouldBeTrue();
            s.Classes.Contains(":narrow").ShouldBeTrue();
            s.Card!.CornerRadius.TopLeft.ShouldBe(26);
            var step = EmailStep(s);
            step.IsNarrow.ShouldBeTrue();
            step.Padding.ShouldBe(new Thickness(20, 24));
            step.Title.FontSize.ShouldBe(28);
            step.ProviderButtons[0].Button.FontSize.ShouldBe(12);
            step.ProviderButtons[0].Content.Spacing.ShouldBe(6);
            var footer = TestHelpers.Descendant<SignInFooterPanel>(s);
            footer.IsWrapped.ShouldBeTrue();

            window.Width = 470;
            Pump();
            s.IsNarrow.ShouldBeFalse();
            s.IsCompact.ShouldBeFalse();
            s.Card.CornerRadius.TopLeft.ShouldBe(34);
            step.Padding.ShouldBe(new Thickness(32));
            step.ProviderButtons[0].Content.Spacing.ShouldBe(8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Motion_The_Old_Step_Goes_At_Once()
    {
        var s = Card();
        var window = Show(s);
        try
        {
            ToCode(s);
            s.StepHost!.Children.Count.ShouldBe(1);
            s.StepHost.Exiting.Count.ShouldBe(0);
            s.Viewport!.IsGliding.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Steps_Slide_In_The_Direction_Of_Travel_While_The_Height_Glides()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var s = Card();
        var window = Show(s, level: ArcMotionLevel.Full);
        try
        {
            var email = EmailStep(s);
            ToCode(s);
            var code = CodeStep(s);
            s.StepHost!.Exiting.ShouldContain(email);
            s.Viewport!.IsGliding.ShouldBeTrue();
            code.Motion.Offset.X.ShouldBe(28, 0.01);
            code.Opacity.ShouldBe(0, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Pump();
            // Smooth spring (critically damped, visualDuration .4): about 38% of the way after 100ms; the old step fades
            // over 180ms on the standard curve.
            code.Motion.Offset.X.ShouldBeInRange(15, 20);
            email.Motion.Offset.X.ShouldBeInRange(-10, -5);
            email.Opacity.ShouldBeLessThan(0.2);
            clock.Advance(TimeSpan.FromMilliseconds(700));
            Pump();
            code.Motion.Offset.X.ShouldBe(0, 0.05);
            code.Opacity.ShouldBe(1, 0.001);
            s.StepHost.Children.ShouldNotContain(email);
            s.Viewport.IsGliding.ShouldBeFalse();

            // Back to the email: it comes in from the left and the code step leaves to the right.
            s.ChangeEmail();
            var back = EmailStep(s);
            back.Motion.Offset.X.ShouldBe(-28, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Pump();
            code.Motion.Offset.X.ShouldBeGreaterThan(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_The_New_Step_And_Drops_The_Old()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var s = Card();
        var window = Show(s, level: ArcMotionLevel.Reduced);
        try
        {
            ToCode(s);
            var code = CodeStep(s);
            s.StepHost!.Children.Count.ShouldBe(1);
            code.Motion.Offset.X.ShouldBe(0);
            code.Opacity.ShouldBe(0, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            code.Opacity.ShouldBeInRange(0.3, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            code.Opacity.ShouldBe(1, 0.001);
            s.Viewport!.IsGliding.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Avatar_Grows_From_The_Code_Step_Into_The_Signed_In_State()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var s = Card();
        var window = Show(s, level: ArcMotionLevel.Full);
        try
        {
            ToCode(s);
            clock.Advance(TimeSpan.FromMilliseconds(1000));
            Pump();
            var code = CodeStep(s);
            s.Verify("123456");
            Pump();
            var done = DoneStep(s);
            var morph = s.AvatarMorph.ShouldNotBeNull();
            // The signed in step is simply there; its avatar starts on the small one, invisible.
            done.Motion.Offset.X.ShouldBe(0);
            done.Opacity.ShouldBe(1);
            done.Avatar.Opacity.ShouldBe(0, 0.001);
            var start = done.Avatar.RenderTransform!.Value;
            start.M11.ShouldBe(32.0 / 88, 0.01);
            // The ring and the badge wait; the three groups start below and blurred.
            done.Ring.Value.ShouldBe(0, 0.001);
            done.Badge.Opacity.ShouldBe(0, 0.001);
            done.Heading.Opacity.ShouldBe(0, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(120));
            Pump();
            // circOut over the first half of the flight.
            done.Avatar.Opacity.ShouldBeInRange(0.5, 1);
            code.Avatar.Opacity.ShouldBeGreaterThan(0.8);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            Pump();
            done.Avatar.Opacity.ShouldBe(1, 0.001);
            done.Avatar.RenderTransform!.Value.ShouldBe(Matrix.Identity);
            done.Ring.Value.ShouldBe(1, 0.001);
            done.Badge.Opacity.ShouldBe(1, 0.01);
            done.Heading.Opacity.ShouldBe(1, 0.001);
            s.AvatarMorph.ShouldBeNull();
            morph.Value.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Passkey_Avatar_Grows_From_Seventy_Percent()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var s = Card();
        var window = Show(s, level: ArcMotionLevel.Full);
        try
        {
            s.SignInWith(SignInMethod.Passkey);
            Pump();
            var done = DoneStep(s);
            s.AvatarMorph.ShouldBeNull();
            done.Avatar.Opacity.ShouldBe(0, 0.001);
            done.Avatar.RenderTransform!.Value.M11.ShouldBe(0.7, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(1200));
            Pump();
            done.Avatar.Opacity.ShouldBe(1, 0.001);
            done.Avatar.RenderTransform!.Value.M11.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Status_Line_Swaps_And_Is_Announced()
    {
        var s = Card();
        var window = Show(s);
        try
        {
            ToCode(s);
            ((TextBlock)s.StatusSwap!.Current!).Text.ShouldBe("Code sent");
            s.Verify("1");
            s.Announcer!.History.ShouldBe(["Sending a code", "Code sent"]);
            s.Verify("999999");
            s.Announcer.History[^2..].ShouldBe(["Checking code", "Code didn't match"]);
            ((TextBlock)s.StatusSwap.Current!).Text.ShouldBe("Code didn't match");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Email_Is_Two_Way()
    {
        var s = Card(c => c.Email = "ava@northwind.studio");
        var window = Show(s);
        try
        {
            EmailStep(s).Email.Text.ShouldBe("ava@northwind.studio");
            EmailStep(s).Email.Text = "ava@home.net";
            Pump();
            s.Email.ShouldBe("ava@home.net");
        }
        finally
        {
            window.Close();
        }
    }
}
