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
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Auth;

public class LoginCenteredTests
{
    /// <summary>A manual clock for the block's waits: delays complete only when <see cref="Advance"/> passes them.</summary>
    private sealed class Clock
    {
        private readonly List<(long Due, TaskCompletionSource Source)> _pending = new();

        public long Now { get; private set; }

        public int PendingCount => _pending.Count;

        public Task Delay(int milliseconds, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled(token);
            }
            var source = new TaskCompletionSource();
            var entry = (Now + milliseconds, source);
            _pending.Add(entry);
            if (token.CanBeCanceled)
            {
                token.Register(() =>
                {
                    _pending.Remove(entry);
                    source.TrySetCanceled(token);
                });
            }
            return source.Task;
        }

        public void Advance(int milliseconds)
        {
            var end = Now + milliseconds;
            Dispatcher.UIThread.RunJobs();
            while (true)
            {
                var next = _pending.Where(p => p.Due <= end).OrderBy(p => p.Due).Cast<(long Due, TaskCompletionSource Source)?>().FirstOrDefault();
                if (next is not { } due)
                {
                    break;
                }
                _pending.Remove(due);
                Now = due.Due;
                due.Source.TrySetResult();
                Dispatcher.UIThread.RunJobs();
            }
            Now = end;
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static (LoginCentered Block, Window Window, Clock Clock) Show(Action<LoginCentered>? configure = null, double width = 1280,
                                                                       double height = 800, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var clock = new Clock();
        var block = new LoginCentered { IsFullScreen = true };
        block.Delay = clock.Delay;
        configure?.Invoke(block);
        var window = TestHelpers.Show(block, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        return (block, window, clock);
    }

    private static void Press(Window window, AvaloniaButton button)
    {
        button.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Window window, string text)
    {
        foreach (var c in text)
        {
            window.KeyTextInput(c.ToString());
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static string? Name(Control control) => AutomationProperties.GetName(control);

    private static LoginCenteredPasskeyView PasskeyView(LoginCentered block) => Assert.IsType<LoginCenteredPasskeyView>(block.View);

    private static LoginCenteredEmailView EmailView(LoginCentered block) => Assert.IsType<LoginCenteredEmailView>(block.View);

    private static LoginCenteredCodeView CodeView(LoginCentered block) => Assert.IsType<LoginCenteredCodeView>(block.View);

    private static LoginCenteredDoneView DoneView(LoginCentered block) => Assert.IsType<LoginCenteredDoneView>(block.View);

    /// <summary>Passkey view → email view (Use email instead), types the address and sends; the clock reaches the code view.</summary>
    private static void ReachCode(LoginCentered block, Window window, Clock clock, string address = "Maya@Northwind.studio ")
    {
        if (block.Step == LoginCenteredStep.Passkey)
        {
            Press(window, PasskeyView(block).SecondaryButton);
        }
        var email = EmailView(block);
        email.Input.Focus();
        Type(window, address);
        Press(window, email.SubmitButton);
        clock.Advance(800);
        Assert.Equal(LoginCenteredStep.Code, block.Step);
    }

    private static void TypeCode(Window window, LoginCenteredCodeView view, string code)
    {
        view.Otp.FocusSlot(view.Otp.Value.Length);
        Dispatcher.UIThread.RunJobs();
        Type(window, code);
    }

    [AvaloniaFact]
    public void Defaults_Are_Arcs_Preview()
    {
        var block = new LoginCentered();
        Assert.Equal("482913", block.DemoCode);
        Assert.Equal("maya@northwind.studio", block.DemoEmail);
        Assert.Equal("#terms", block.TermsHref);
        Assert.Equal("#privacy", block.PrivacyHref);
        Assert.Equal(30, block.ResendSeconds);
        Assert.Equal(LoginCenteredMode.SignIn, block.Mode);
        Assert.False(block.IsFullScreen);

        var (shown, _, _) = Show(b => b.IsFullScreen = false);
        Assert.Equal(LoginCenteredStep.Passkey, shown.Step);
        Assert.Equal(string.Empty, shown.Status);
        Assert.Equal("Sign in", Name(shown));
        Assert.Equal(640, shown.MinHeight);
        Assert.Equal(new Thickness(1), shown.BorderThickness);
        var view = PasskeyView(shown);
        Assert.Equal("Sign in with passkey", Name(view.PasskeyButton));
        Assert.Equal("Use email instead", Name(view.SecondaryButton));
        Assert.Equal(["Continue with Google", "Continue with Apple", "Continue with GitHub"], view.ProviderButtons.Select(b => Name(b) ?? string.Empty).ToArray());
        Assert.Equal("Sign up", Name(shown.ModeSwitch!));
        Assert.Equal("Terms", shown.TermsButton!.Content);
        Assert.Equal("Privacy", shown.PrivacyButton!.Content);
    }

    [AvaloniaFact]
    public void Full_Screen_Drops_The_Frame()
    {
        var (block, _, _) = Show();
        Assert.Contains(LoginCentered.FullScreenPseudoClass, block.Classes);
        Assert.Equal(0, block.MinHeight);
        Assert.Equal(new Thickness(0), block.BorderThickness);
        Assert.Equal(new CornerRadius(0), block.CornerRadius);
    }

    [AvaloniaFact]
    public void Card_Is_At_Most_420_Wide_And_Centered()
    {
        var (block, _, _) = Show();
        var card = TestHelpers.Descendant<Border>(block, "PART_Card");
        Assert.Equal(420, card.Bounds.Width, 1);
        var center = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), block)!.Value;
        Assert.InRange(center.X, 639.5, 640.5);
        Assert.InRange(center.Y, 399.5, 400.5);
    }

    [AvaloniaFact]
    public void Passkey_Waits_Verifies_And_Signs_In()
    {
        var (block, window, clock) = Show();
        var signedIn = new List<LoginCenteredSignedInEventArgs>();
        block.SignedIn += (_, e) => signedIn.Add(e);
        var view = PasskeyView(block);

        Press(window, view.PasskeyButton);
        Assert.Equal(LoginCenteredPasskeyPhase.Waiting, block.PasskeyPhase);
        Assert.Equal("Waiting for your passkey", block.Status);
        Assert.Equal("Waiting for your device", Name(view.PasskeyButton));
        Assert.Equal("Cancel", Name(view.SecondaryButton));

        clock.Advance(1699);
        Assert.Equal(LoginCenteredPasskeyPhase.Waiting, block.PasskeyPhase);
        clock.Advance(1);
        Assert.Equal(LoginCenteredPasskeyPhase.Verified, block.PasskeyPhase);
        Assert.Equal("Passkey verified", block.Status);
        Assert.Equal("Passkey verified", Name(view.PasskeyButton));

        clock.Advance(640);
        Assert.Equal(LoginCenteredStep.Done, block.Step);
        Assert.Equal("Signed in with a passkey", block.Status);
        var e = Assert.Single(signedIn);
        Assert.Equal("maya@northwind.studio", e.Email);
        Assert.Equal(LoginCenteredMethod.Passkey, e.Method);
        Assert.Equal(LoginCenteredMode.SignIn, e.Mode);
        var done = DoneView(block);
        Assert.Equal("Welcome back", done.Title.Text);
        Assert.Equal("Passkey", done.MethodText);
        Assert.Contains("Signed in with a passkey", block.Announcer!.History);
    }

    [AvaloniaFact]
    public void Cancel_Stops_The_Passkey_Request()
    {
        var (block, window, clock) = Show();
        var view = PasskeyView(block);
        Press(window, view.PasskeyButton);
        Press(window, view.SecondaryButton);
        Assert.Equal(LoginCenteredPasskeyPhase.Idle, block.PasskeyPhase);
        Assert.Equal("Passkey request canceled", block.Status);
        clock.Advance(5000);
        Assert.Equal(LoginCenteredStep.Passkey, block.Step);
        Assert.Equal(LoginCenteredPasskeyPhase.Idle, block.PasskeyPhase);
        Assert.True(view.PasskeyButton.IsFocused);
    }

    [AvaloniaFact]
    public void Actions_Are_Ignored_While_The_Device_Is_Asked()
    {
        var (block, window, clock) = Show();
        var view = PasskeyView(block);
        Press(window, view.PasskeyButton);
        Press(window, view.ProviderButtons[0]);
        Assert.False(view.ProviderButtons[0].IsLoading);
        Assert.Equal("Waiting for your passkey", block.Status);
        clock.Advance(1700);
        // Verified: "Use email instead" is aria-disabled.
        Press(window, view.SecondaryButton);
        Assert.Equal(LoginCenteredStep.Passkey, block.Step);
    }

    [AvaloniaFact]
    public void Email_Is_Validated_Then_A_Code_Is_Sent()
    {
        var (block, window, clock) = Show();
        Press(window, PasskeyView(block).SecondaryButton);
        Assert.Equal(LoginCenteredStep.Email, block.Step);
        Assert.Equal("We will email you a sign in code", block.Status);
        var view = EmailView(block);
        Assert.False(view.IsSignUp);
        Assert.Equal("Email", view.Input.Label);
        Assert.Equal("name@example.com", view.Input.Placeholder);
        Assert.True(view.Input.IsFocused);
        Assert.Null(view.Input.ErrorMessage);

        Press(window, view.SubmitButton);
        Assert.Equal("Enter your email address.", view.Input.ErrorMessage);
        Assert.Equal(LoginCenteredStep.Email, block.Step);

        view.Input.Focus();
        Type(window, "maya@north");
        Assert.Equal("Enter a full address, like name@example.com.", view.Input.ErrorMessage);
        Type(window, "wind.studio");
        Assert.Null(view.Input.ErrorMessage);
        Assert.Equal("maya@northwind.studio", block.Email);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(block.IsBusy);
        Assert.True(view.SubmitButton.IsLoading);
        Assert.True(view.Input.IsReadOnly);
        Assert.Equal("Sending a code", block.Status);

        clock.Advance(800);
        Assert.Equal(LoginCenteredStep.Code, block.Step);
        Assert.False(block.IsBusy);
        Assert.Equal("maya@northwind.studio", block.Account);
        Assert.Equal("Code sent to maya@northwind.studio", block.Status);
        Assert.Equal(30, block.ResendIn);
    }

    [AvaloniaFact]
    public void Leaving_A_Filled_Field_Shows_Its_Error_An_Empty_One_Does_Not()
    {
        var (block, window, _) = Show();
        Press(window, PasskeyView(block).SecondaryButton);
        var view = EmailView(block);
        view.Input.Focus();
        view.PasskeyButton!.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(view.Input.ErrorMessage);

        view.Input.Focus();
        Type(window, "abc");
        Assert.Null(view.Input.ErrorMessage);
        view.PasskeyButton.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Enter a full address, like name@example.com.", view.Input.ErrorMessage);
    }

    [AvaloniaFact]
    public void Email_Pattern_Matches_Arcs()
    {
        Assert.True(LoginCentered.IsEmail("maya@northwind.studio"));
        Assert.True(LoginCentered.IsEmail("  a@b.co "));
        Assert.False(LoginCentered.IsEmail("a@b.c"));
        Assert.False(LoginCentered.IsEmail("a b@c.com"));
        Assert.False(LoginCentered.IsEmail("maya@north"));
        Assert.False(LoginCentered.IsEmail(""));
    }

    [AvaloniaFact]
    public void Wrong_Code_Clears_And_Errors_Then_The_Right_One_Signs_In()
    {
        var (block, window, clock) = Show();
        var signedIn = new List<LoginCenteredSignedInEventArgs>();
        block.SignedIn += (_, e) => signedIn.Add(e);
        ReachCode(block, window, clock);
        var view = CodeView(block);
        Assert.Equal("Verification code", view.Otp.Label);
        Assert.Equal("Demo code: 482913", view.Otp.Description);
        Assert.Equal("Verify", Name(view.VerifyButton));

        Press(window, view.VerifyButton);
        Assert.Equal("Enter all 6 digits.", view.Otp.ErrorMessage);

        TypeCode(window, view, "123456");
        Assert.True(block.IsBusy);
        Assert.True(view.VerifyButton.IsLoading);
        Assert.True(view.Otp.IsReadOnly);
        Assert.Equal("Checking code", block.Status);
        clock.Advance(700);
        Assert.Equal(string.Empty, view.Otp.Value);
        Assert.Equal("That code didn't match. Check the latest email and try again.", view.Otp.ErrorMessage);
        Assert.Equal("Code didn't match", block.Status);

        TypeCode(window, view, "4");
        Assert.Null(view.Otp.ErrorMessage);
        TypeCode(window, view, "82913");
        Assert.Equal("Checking code", block.Status);
        clock.Advance(700);
        Assert.True(block.IsVerified);
        Assert.Equal("Code verified", block.Status);
        Assert.Equal("Verified", Name(view.VerifyButton));
        Assert.True(view.ResendButton.IsInert);
        clock.Advance(600);
        Assert.Equal(LoginCenteredStep.Done, block.Step);
        Assert.Equal("Signed in with an email code", block.Status);
        var e = Assert.Single(signedIn);
        Assert.Equal(("maya@northwind.studio", LoginCenteredMethod.EmailCode), (e.Email, e.Method));
        Assert.Equal("Email code", DoneView(block).MethodText);
    }

    [AvaloniaFact]
    public void Resend_Counts_Down_Then_Sends_A_New_Code()
    {
        var (block, window, clock) = Show();
        ReachCode(block, window, clock);
        var view = CodeView(block);
        Assert.True(view.ResendButton.IsInert);
        Assert.Equal("Resend code, available in 30 seconds", Name(view.ResendButton));
        Assert.Equal("0:30", view.Time!.Text);

        clock.Advance(1000);
        Assert.Equal(29, block.ResendIn);
        Assert.Equal("0:29", view.Time!.Text);
        Assert.Equal("Resend code, available in 29 seconds", Name(view.ResendButton));

        // Inert: Enter does nothing.
        Press(window, view.ResendButton);
        Assert.Equal(29, block.ResendIn);

        clock.Advance(29000);
        Assert.Equal(0, block.ResendIn);
        Assert.False(view.ResendButton.IsInert);
        Assert.Equal("Resend code", Name(view.ResendButton));
        Assert.Null(view.Time);

        Press(window, view.ResendButton);
        Assert.Equal(30, block.ResendIn);
        Assert.Equal("New code sent", block.Status);
        clock.Advance(1000);
        Assert.Equal(29, block.ResendIn);
    }

    [AvaloniaFact]
    public void Change_Goes_Back_To_The_Email_With_The_Address_Kept()
    {
        var (block, window, clock) = Show();
        ReachCode(block, window, clock, "maya@northwind.studio");
        Press(window, CodeView(block).ChangeButton);
        Assert.Equal(LoginCenteredStep.Email, block.Step);
        Assert.Equal("Edit your email", block.Status);
        Assert.Equal("maya@northwind.studio", EmailView(block).Input.Text);
        // The countdown stops while the code view is away.
        clock.Advance(5000);
        Assert.Equal(30, block.ResendIn);
    }

    [AvaloniaFact]
    public void Use_A_Passkey_Instead_Returns_To_The_First_View()
    {
        var (block, window, _) = Show();
        Press(window, PasskeyView(block).SecondaryButton);
        Press(window, EmailView(block).PasskeyButton!);
        Assert.Equal(LoginCenteredStep.Passkey, block.Step);
        Assert.Equal("Sign in with your passkey", block.Status);
    }

    [AvaloniaFact]
    public void Sign_Up_Creates_An_Account_With_A_Code()
    {
        var (block, window, clock) = Show();
        var signedIn = new List<LoginCenteredSignedInEventArgs>();
        block.SignedIn += (_, e) => signedIn.Add(e);
        Press(window, block.ModeSwitch!);
        Assert.Equal(LoginCenteredMode.SignUp, block.Mode);
        Assert.Contains(LoginCentered.SignUpPseudoClass, block.Classes);
        Assert.Equal(LoginCenteredStep.Email, block.Step);
        Assert.Equal("Create an account with your email", block.Status);
        Assert.Equal("Sign in", Name(block.ModeSwitch!));
        Assert.Equal("Create an account", Name(block));
        var email = EmailView(block);
        Assert.True(email.IsSignUp);
        Assert.Equal("Work email", email.Input.Label);
        Assert.Null(email.PasskeyButton);

        ReachCode(block, window, clock, "ada@lovelace.dev");
        Assert.Equal("Verify and create account", Name(CodeView(block).VerifyButton));
        TypeCode(window, CodeView(block), "482913");
        clock.Advance(1300);
        Assert.Equal(LoginCenteredStep.Done, block.Step);
        Assert.Equal("Welcome to Arc", DoneView(block).Title.Text);
        Assert.Equal("Account created with an email code", block.Status);
        Assert.Equal(LoginCenteredMode.SignUp, Assert.Single(signedIn).Mode);

        Press(window, block.ModeSwitch!);
        Assert.Equal(LoginCenteredMode.SignIn, block.Mode);
        Assert.Equal(LoginCenteredStep.Passkey, block.Step);
        Assert.Equal("Sign in to your account", block.Status);
    }

    [AvaloniaFact]
    public void Mode_Set_Before_Showing_Opens_The_Sign_Up_Form_Quietly()
    {
        var (block, _, _) = Show(b => b.Mode = LoginCenteredMode.SignUp);
        Assert.Equal(LoginCenteredStep.Email, block.Step);
        Assert.True(EmailView(block).IsSignUp);
        Assert.Equal(string.Empty, block.Status);
    }

    [AvaloniaFact]
    public void Providers_Show_A_Spinner_Then_Sign_In()
    {
        var (block, window, clock) = Show();
        var view = PasskeyView(block);
        Press(window, view.ProviderButtons[0]);
        Assert.True(view.ProviderButtons[0].IsLoading);
        Assert.False(view.ProviderButtons[1].IsLoading);
        Assert.Equal("Opening Google", block.Status);
        Press(window, view.ProviderButtons[1]);
        Assert.False(view.ProviderButtons[1].IsLoading);
        Press(window, view.PasskeyButton);
        Assert.Equal(LoginCenteredPasskeyPhase.Idle, block.PasskeyPhase);
        clock.Advance(900);
        Assert.Equal(LoginCenteredStep.Done, block.Step);
        Assert.Equal(LoginCenteredMethod.Google, block.Method);
        Assert.Equal("Signed in with Google", block.Status);
        Assert.Equal("Google", DoneView(block).MethodText);
    }

    [AvaloniaFact]
    public void Sign_Out_Starts_Over()
    {
        var (block, window, clock) = Show();
        var signedOut = 0;
        block.SignedOut += (_, _) => signedOut++;
        Press(window, block.ModeSwitch!);
        ReachCode(block, window, clock, "ada@lovelace.dev");
        TypeCode(window, CodeView(block), "482913");
        clock.Advance(1300);
        Press(window, DoneView(block).SignOutButton);
        Assert.Equal(LoginCenteredStep.Passkey, block.Step);
        Assert.Equal(LoginCenteredMode.SignIn, block.Mode);
        Assert.Equal(string.Empty, block.Email);
        Assert.Equal("Signed out", block.Status);
        Assert.Equal(1, signedOut);
        Assert.True(PasskeyView(block).PasskeyButton.IsFocused);
    }

    [AvaloniaFact]
    public void Handlers_Replace_The_Simulation()
    {
        var sent = new List<(string, LoginCenteredMode)>();
        var checkedCodes = new List<(string, string)>();
        var (block, window, clock) = Show(b =>
        {
            b.SendCodeAsync = (address, mode, _) =>
            {
                sent.Add((address, mode));
                return Task.CompletedTask;
            };
            b.VerifyCodeAsync = (address, code, _) =>
            {
                checkedCodes.Add((address, code));
                return Task.FromResult(code == "111111");
            };
        });
        Press(window, PasskeyView(block).SecondaryButton);
        var email = EmailView(block);
        email.Input.Focus();
        Type(window, " Ada@Example.com");
        Press(window, email.SubmitButton);
        Assert.Equal(LoginCenteredStep.Code, block.Step);
        Assert.Equal(("ada@example.com", LoginCenteredMode.SignIn), Assert.Single(sent));
        var view = CodeView(block);
        Assert.Null(view.Otp.Description);

        TypeCode(window, view, "482913");
        Assert.Equal("That code didn't match. Check the latest email and try again.", view.Otp.ErrorMessage);
        TypeCode(window, view, "111111");
        Assert.True(block.IsVerified);
        Assert.Equal([("ada@example.com", "482913"), ("ada@example.com", "111111")], checkedCodes);
        clock.Advance(600);
        Assert.Equal(LoginCenteredStep.Done, block.Step);
    }

    [AvaloniaFact]
    public void Failing_Handlers_Say_So()
    {
        var (block, window, _) = Show(b =>
        {
            b.PasskeyAsync = _ => Task.FromException<string>(new InvalidOperationException("no authenticator"));
            b.SendCodeAsync = (_, _, _) => Task.FromException(new InvalidOperationException("smtp down"));
        });
        Press(window, PasskeyView(block).PasskeyButton);
        Assert.Equal(LoginCenteredPasskeyPhase.Idle, block.PasskeyPhase);
        Assert.Equal("Your passkey could not be used", block.Status);

        Press(window, PasskeyView(block).SecondaryButton);
        var email = EmailView(block);
        email.Input.Focus();
        Type(window, "maya@northwind.studio");
        Press(window, email.SubmitButton);
        Assert.Equal(LoginCenteredStep.Email, block.Step);
        Assert.Equal("We couldn't send a code. Try again.", email.Input.ErrorMessage);
        Type(window, "x");
        Assert.Null(email.Input.ErrorMessage);
    }

    [AvaloniaFact]
    public void Passkey_Prompt_Dismissed_By_The_Platform_Counts_As_Canceled()
    {
        var (block, window, _) = Show(b => b.PasskeyAsync = _ => Task.FromException<string>(new OperationCanceledException()));
        Press(window, PasskeyView(block).PasskeyButton);
        Assert.Equal(LoginCenteredPasskeyPhase.Idle, block.PasskeyPhase);
        Assert.Equal("Passkey request canceled", block.Status);
    }

    [AvaloniaFact]
    public void Legal_Links_Raise_Navigate_And_Explain_In_Page_Addresses()
    {
        var (block, window, _) = Show();
        var links = new List<LoginCenteredNavigateEventArgs>();
        block.Navigate += (_, e) => links.Add(e);
        Press(window, block.TermsButton!);
        Assert.Equal((LoginCenteredLink.Terms, "#terms"), (links[0].Link, links[0].Href));
        Assert.Equal("Terms opens here in your app (demo)", block.Status);
        Press(window, block.PrivacyButton!);
        Assert.Equal("Privacy policy opens here in your app (demo)", block.Status);

        block.Navigate += (_, e) => e.Handled = true;
        block.TermsHref = "/terms";
        Press(window, block.TermsButton!);
        Assert.Equal("/terms", links[2].Href);
        Assert.Equal("Privacy policy opens here in your app (demo)", block.Status);
    }

    [AvaloniaFact]
    public void Width_Classes_Follow_The_Blocks_Own_Width()
    {
        var (block, window, _) = Show(width: 1280);
        Assert.DoesNotContain(LoginCentered.NarrowPseudoClass, block.Classes);
        Assert.Equal(LoginCenteredMetrics.StepPadding, block.View!.Padding);

        window.Width = 500;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(LoginCentered.NarrowPseudoClass, block.Classes);
        Assert.DoesNotContain(LoginCentered.TinyPseudoClass, block.Classes);
        Assert.Equal(LoginCenteredMetrics.StepPaddingNarrow, block.View!.Padding);
        Assert.Equal(new CornerRadius(26), TestHelpers.Descendant<Border>(block, "PART_Card").CornerRadius);

        window.Width = 390;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(LoginCentered.TinyPseudoClass, block.Classes);
        Assert.Equal(LoginCenteredMetrics.StepPaddingTiny, block.View!.Padding);
        var heading = block.View.GetVisualDescendants().OfType<LoginCenteredHeading>().First();
        Assert.Equal(28, heading.FontSize);
        Assert.Equal(30.8, heading.LineHeight, 3);
        Assert.Equal(LoginCenteredMetrics.SsoPaddingTiny, PasskeyView(block).ProviderButtons[0].Padding);
        Assert.Equal(12, PasskeyView(block).ProviderButtons[0].FontSize);
        var card = TestHelpers.Descendant<Border>(block, "PART_Card");
        Assert.Equal(390 - 24, card.Bounds.Width, 1);
    }

    [AvaloniaFact]
    public void Labels_Can_Be_Replaced()
    {
        var (block, _, _) = Show();
        block.Labels = new LoginCenteredLabels { PasskeyAction = "Use your passkey", Terms = "Conditions" };
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Use your passkey", Name(PasskeyView(block).PasskeyButton));
        Assert.Equal("Conditions", block.TermsButton!.Content);
    }

    [AvaloniaFact]
    public void Steps_Glide_The_Card_Height_At_Full_Motion()
    {
        var frames = new ManualFrameClock();
        using var _ = frames.Install();
        var (block, window, _) = Show(level: ArcMotionLevel.Full);
        var viewport = block.Viewport!;
        var before = viewport.Bounds.Height;
        Press(window, PasskeyView(block).SecondaryButton);
        window.UpdateLayout();
        Assert.True(viewport.IsGliding);
        Assert.Equal(1, viewport.ExitingCount);
        frames.Advance(TimeSpan.FromMilliseconds(50));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var during = viewport.Bounds.Height;
        Assert.NotEqual(before, during);

        frames.Advance(TimeSpan.FromMilliseconds(1500));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Assert.False(viewport.IsGliding);
        // The old step leaves from an await on its exit motions, which can come back through the thread pool.
        TestHelpers.ShouldDrain(() => viewport.ExitingCount);
        Assert.Equal(block.View!.DesiredSize.Height, viewport.Bounds.Height, 1);
        Assert.Equal(1, block.View.Opacity);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Without_Gliding()
    {
        var frames = new ManualFrameClock();
        using var _ = frames.Install();
        var (block, window, _) = Show(level: ArcMotionLevel.Reduced);
        var viewport = block.Viewport!;
        Press(window, PasskeyView(block).SecondaryButton);
        window.UpdateLayout();
        Assert.False(viewport.IsGliding);
        Assert.Equal(0, viewport.ExitingCount);
        Assert.True(block.View!.Opacity < 1);
        Assert.Null(block.View.Effect);
        frames.Advance(TimeSpan.FromMilliseconds(200));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, block.View.Opacity);
    }

    [AvaloniaFact]
    public void Backdrop_Drifts_With_A_Mouse_Only_At_Full_Motion()
    {
        var frames = new ManualFrameClock();
        using var _ = frames.Install();
        var (block, window, _) = Show(level: ArcMotionLevel.Full);
        window.MouseMove(new Point(0, 0), RawInputModifiers.None);
        frames.Advance(TimeSpan.FromSeconds(3));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, block.Backdrop!.Offset.X, 2);
        Assert.Equal(1, block.Backdrop.Offset.Y, 2);

        ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(new Point(1280, 800), RawInputModifiers.None);
        frames.Advance(TimeSpan.FromSeconds(1));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new Point(0, 0), block.Backdrop.Offset);
    }

    [AvaloniaFact]
    public void Rings_And_Pulse_Follow_The_Source()
    {
        Assert.Equal((224d, 1d, false), LoginCenteredBackdrop.Ring(0));
        Assert.Equal((344d, 1.68, true), (LoginCenteredBackdrop.Ring(2).Radius, Math.Round(LoginCenteredBackdrop.Ring(2).Depth, 2), LoginCenteredBackdrop.Ring(2).IsMajor));
        Assert.Equal(1004, LoginCenteredBackdrop.Ring(13).Radius);

        Assert.Equal((0.7, 0.6), LoginCenteredPulse.RingAt(0, 0));
        Assert.Equal(0, LoginCenteredPulse.RingAt(1, 0.3).Opacity);
        Assert.Equal((0.7, 0.6), LoginCenteredPulse.RingAt(1, 0.6));
        Assert.Equal(0, LoginCenteredPulse.RingAt(0, 1.2).Opacity);
        Assert.True(LoginCenteredPulse.RingAt(0, 0.6).Scale > 1.4);

        Assert.Equal("0:30", LoginCenteredRollingTime.Format(30));
        Assert.Equal("0:05", LoginCenteredRollingTime.Format(5));
        Assert.Equal("1:00", LoginCenteredRollingTime.Format(60));
    }

    [AvaloniaFact]
    public void Status_Line_Swaps_And_Is_Announced()
    {
        var (block, window, _) = Show();
        Assert.Null(block.StatusLine!.Current);
        Press(window, PasskeyView(block).SecondaryButton);
        var line = Assert.IsType<TextBlock>(block.StatusLine.Current);
        Assert.Equal("We will email you a sign in code", line.Text);
        Assert.Equal("We will email you a sign in code", block.Announcer!.Announcement);
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(block.Announcer));
    }
}
