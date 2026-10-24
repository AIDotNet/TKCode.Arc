using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Auth;

public class SignupFormTests
{
    private static Window Show(Control control, double width = 600, ArcMotionLevel level = ArcMotionLevel.Off, double height = 900)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>A form that never waits (no 700 ms pacing, no 900 ms simulated provider).</summary>
    private static SignupForm Quick() => new()
    {
        SubmitMinimumDuration = TimeSpan.Zero,
        ProviderSimulationDuration = TimeSpan.Zero
    };

    private static void Fill(SignupForm form, string name, string email, string password)
    {
        form.NameInput!.Text = name;
        form.EmailInput!.Text = email;
        form.PasswordInput!.Text = password;
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Recorder : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    [AvaloniaFact]
    public void Defaults_Are_Arcs_Card()
    {
        var form = Quick();
        var window = Show(form);
        try
        {
            form.Step.ShouldBe(SignupFormStep.Form);
            form.FullName.ShouldBe(string.Empty);
            form.Email.ShouldBe(string.Empty);
            form.Password.ShouldBe(string.Empty);
            form.ProductUpdates.ShouldBeFalse();
            form.IsBusy.ShouldBeFalse();
            form.BusyMethod.ShouldBeNull();
            form.Status.ShouldBe(string.Empty);
            form.ShownError.ShouldBeNull();
            form.PasswordScore.ShouldBe(0);
            form.AreProvidersVisible.ShouldBeTrue();
            form.SubmitMinimumDuration.ShouldBe(TimeSpan.Zero);
            new SignupForm().SubmitMinimumDuration.ShouldBe(TimeSpan.FromMilliseconds(700));
            new SignupForm().ProviderSimulationDuration.ShouldBe(TimeSpan.FromMilliseconds(900));

            // min(100%, 432px) wide, the surface radius.
            form.Bounds.Width.ShouldBe(432, 0.01);
            form.CornerRadius.TopLeft.ShouldBe(34, 0.01);
            AutomationProperties.GetName(form).ShouldBe("Create an Arc account");
            TestHelpers.Descendant<TextBlock>(form, "PART_Title").Text.ShouldBe("Create your account");
            TestHelpers.Descendant<TextBlock>(form, "PART_Description").Text.ShouldBe("Start with your work email. You can invite your team after.");
            form.NameInput!.Label.ShouldBe("Full name");
            form.NameInput.Placeholder.ShouldBe("Jasmine Brooks");
            form.EmailInput!.Label.ShouldBe("Work email");
            form.EmailInput.Placeholder.ShouldBe("name@example.com");
            form.PasswordInput!.Label.ShouldBe("Password");
            form.UpdatesBox!.Content.ShouldBe("Email me product updates");
            form.SubmitButton!.Content.ShouldBe("Create account");
            AutomationProperties.GetName(form.ProviderButton(SignupFormMethod.Google)!).ShouldBe("Sign up with Google");
            AutomationProperties.GetName(form.ProviderButton(SignupFormMethod.Apple)!).ShouldBe("Sign up with Apple");
            AutomationProperties.GetName(form.ProviderButton(SignupFormMethod.GitHub)!).ShouldBe("Sign up with GitHub");
            form.FeedbackLine!.Text.ShouldBe("Use 8 or more characters.");
            form.FeedbackLine.Key.ShouldBe("hint");
            form.Meter!.Level.ShouldBe(0);
            form.ErrorRow!.IsPresent.ShouldBeFalse();
            form.NameInput.ErrorMessage.ShouldBeNull();
            form.Stage!.Current.ShouldBe(0);
            TestHelpers.Descendant<Border>(form, "PART_DoneStep").IsVisible.ShouldBeFalse();
            form.TermsLink!.Content.ShouldBe("Terms");
            form.PrivacyLink!.Content.ShouldBe("Privacy policy");
            form.SignInLink!.Content.ShouldBe("Sign in");
            form.SignInLink.IsStrong.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Geometry_Follows_The_Css_Module()
    {
        var form = Quick();
        var window = Show(form);
        try
        {
            var step = TestHelpers.Descendant<Border>(form, "PART_FormStep");
            step.Padding.ShouldBe(new Thickness(32));
            // The three providers share the 366px content width with 8px gaps.
            var google = form.ProviderButton(SignupFormMethod.Google)!;
            google.Bounds.Width.ShouldBe((366 - 16) / 3.0, 0.5);
            google.Bounds.Height.ShouldBe(44, 0.01);
            var footer = TestHelpers.Descendant<Border>(form, "PART_Footer");
            footer.Bounds.Height.ShouldBe(48, 0.5);
            footer.Padding.ShouldBe(new Thickness(32, 12));
            form.Meter!.Bounds.Width.ShouldBe(4 * 20 + 3 * 3, 0.01);
            form.Meter.Bounds.Height.ShouldBe(3, 0.01);
            // The meter sits at the right edge of the form column.
            var meterRight = form.Meter.TranslatePoint(new Point(form.Meter.Bounds.Width, 0), step)!.Value.X;
            meterRight.ShouldBe(432 - 2 - 32, 1);
            form.SubmitButton!.Bounds.Width.ShouldBe(366, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    [InlineData("abcdefgh", 1)]
    [InlineData("abcdefgH", 2)]
    [InlineData("abcdefgH1", 3)]
    [InlineData("abcdefgH1xyz", 4)]
    [InlineData("aaaaaaaaaaaa", 2)]
    [InlineData("Ab1", 2)]
    [InlineData("abc_def_ghi", 1)]
    [InlineData("abcdefg!", 2)]
    [InlineData("pässwörter", 2)]
    [InlineData("12345678", 2)]
    public void Password_Score_Is_Arcs(string password, int score)
    {
        SignupForm.ScorePassword(password).ShouldBe(score);
    }

    [Theory]
    [InlineData("name@example.com", true)]
    [InlineData("  jasmine@northwind.studio ", true)]
    [InlineData("jasmine@northwind", false)]
    [InlineData("jasmine@northwind.s", false)]
    [InlineData("jas mine@northwind.studio", false)]
    [InlineData("@northwind.studio", false)]
    [InlineData("", false)]
    public void Email_Check_Is_Arcs(string email, bool valid)
    {
        SignupForm.IsValidEmail(email).ShouldBe(valid);
    }

    [AvaloniaFact]
    public void Submitting_An_Empty_Form_Shows_Every_Error_And_Focuses_The_Name()
    {
        var form = Quick();
        var window = Show(form);
        try
        {
            form.Submit();
            Dispatcher.UIThread.RunJobs();
            form.NameInput!.ErrorMessage.ShouldBe("Enter your full name.");
            form.EmailInput!.ErrorMessage.ShouldBe("Enter a full address, like name@example.com.");
            // The password field only turns invalid; its message is the line under it.
            form.PasswordInput!.IsInvalid.ShouldBeTrue();
            form.PasswordInput.ErrorMessage.ShouldBeNull();
            form.FeedbackLine!.Text.ShouldBe("Use at least 8 characters.");
            form.FeedbackLine.Key.ShouldBe("error");
            form.Classes.Contains(SignupForm.PasswordErrorPseudoClass).ShouldBeTrue();
            form.Alert!.History.ShouldContain("Use at least 8 characters.");
            form.NameInput.IsFocused.ShouldBeTrue();
            form.IsBusy.ShouldBeFalse();
            form.Step.ShouldBe(SignupFormStep.Form);

            // From now on every keystroke re-checks.
            form.NameInput.Text = "Jo";
            Dispatcher.UIThread.RunJobs();
            form.NameInput.ErrorMessage.ShouldBeNull();
            form.FullName.ShouldBe("Jo");
            form.EmailInput.Text = "jo@example.com";
            Dispatcher.UIThread.RunJobs();
            form.EmailInput.ErrorMessage.ShouldBeNull();
            // The first invalid field now is the password.
            form.Submit();
            Dispatcher.UIThread.RunJobs();
            form.PasswordInput.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Field_Is_Checked_Once_It_Is_Left_With_Text()
    {
        var form = Quick();
        var window = Show(form);
        try
        {
            // Leaving an empty field says nothing.
            form.NameInput!.Focus();
            form.EmailInput!.Focus();
            Dispatcher.UIThread.RunJobs();
            form.NameInput.ErrorMessage.ShouldBeNull();

            form.NameInput.Focus();
            form.NameInput.Text = "J";
            Dispatcher.UIThread.RunJobs();
            form.NameInput.ErrorMessage.ShouldBeNull();
            form.EmailInput.Focus();
            Dispatcher.UIThread.RunJobs();
            form.NameInput.ErrorMessage.ShouldBe("Enter your full name.");

            form.EmailInput.Text = "jasmine@northwind";
            form.PasswordInput!.Focus();
            Dispatcher.UIThread.RunJobs();
            form.EmailInput.ErrorMessage.ShouldBe("Enter a full address, like name@example.com.");
            form.EmailInput.Text = "jasmine@northwind.studio";
            Dispatcher.UIThread.RunJobs();
            form.EmailInput.ErrorMessage.ShouldBeNull();

            form.PasswordInput.Text = "abc";
            Dispatcher.UIThread.RunJobs();
            form.FeedbackLine!.Text.ShouldBe("Weak password");
            form.PasswordInput.IsInvalid.ShouldBeFalse();
            form.UpdatesBox!.Focus();
            Dispatcher.UIThread.RunJobs();
            form.FeedbackLine.Text.ShouldBe("Use at least 8 characters.");
            form.PasswordInput.IsInvalid.ShouldBeTrue();
            form.Meter!.Level.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Password_Line_Reads_The_Strength()
    {
        var form = Quick();
        var window = Show(form);
        try
        {
            var line = form.FeedbackLine!;
            (string Key, string Text, int Level) Read() => (line.Key!, line.Text, form.Meter!.Level);
            form.Password = "abc";
            Dispatcher.UIThread.RunJobs();
            // A typed password that scores nothing still reads "Weak" (strengthLabel[max(score, 1)]).
            Read().ShouldBe(("s0", "Weak password", 0));
            form.Password = "abcdefgh";
            Dispatcher.UIThread.RunJobs();
            Read().ShouldBe(("s1", "Weak password", 1));
            form.Password = "abcdefgH";
            Dispatcher.UIThread.RunJobs();
            Read().ShouldBe(("s2", "Fair password", 2));
            form.Password = "abcdefgH1";
            Dispatcher.UIThread.RunJobs();
            Read().ShouldBe(("s3", "Good password", 3));
            form.Password = "abcdefgH1xyz";
            Dispatcher.UIThread.RunJobs();
            Read().ShouldBe(("s4", "Strong password", 4));
            form.PasswordScore.ShouldBe(4);
            form.PasswordInput!.Text.ShouldBe("abcdefgH1xyz");
            AutomationProperties.GetHelpText(form.PasswordInput).ShouldBe("Strong password");
            form.Password = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Read().ShouldBe(("hint", "Use 8 or more characters.", 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Meter_Bars_Take_The_Level_Tone()
    {
        SignupFormMeter.ToneOf(0, 0).ShouldBe(DesignTokens.ArcDesignTokenKind.Border);
        SignupFormMeter.ToneOf(0, 1).ShouldBe(DesignTokens.ArcDesignTokenKind.Danger);
        SignupFormMeter.ToneOf(1, 1).ShouldBe(DesignTokens.ArcDesignTokenKind.Border);
        SignupFormMeter.ToneOf(1, 2).ShouldBe(DesignTokens.ArcDesignTokenKind.Warning);
        SignupFormMeter.ToneOf(2, 3).ShouldBe(DesignTokens.ArcDesignTokenKind.Accent);
        SignupFormMeter.ToneOf(3, 4).ShouldBe(DesignTokens.ArcDesignTokenKind.Success);
        var form = Quick();
        var window = Show(form);
        try
        {
            form.Password = "abcdefgH";
            Dispatcher.UIThread.RunJobs();
            var meter = form.Meter!;
            meter.BarColor(0).ShouldBe(ArcTokens.GetColor(meter, DesignTokens.ArcDesignTokenKind.Warning));
            meter.BarColor(1).ShouldBe(ArcTokens.GetColor(meter, DesignTokens.ArcDesignTokenKind.Warning));
            meter.BarColor(2).ShouldBe(ArcTokens.GetColor(meter, DesignTokens.ArcDesignTokenKind.Border));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Valid_Form_Creates_The_Account_And_Shows_The_Completion_Panel()
    {
        var form = Quick();
        SignupFormDetails? received = null;
        form.SubmitAsync = (details, _) =>
        {
            received = details;
            return Task.CompletedTask;
        };
        var created = new List<SignupFormCompletedEventArgs>();
        form.AccountCreated += (_, e) => created.Add(e);
        var command = new Recorder();
        form.AccountCreatedCommand = command;
        var window = Show(form);
        try
        {
            Fill(form, "  Jasmine Brooks ", " jasmine@northwind.studio ", "Northwind26");
            form.UpdatesBox!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            form.ProductUpdates.ShouldBeTrue();
            form.Submit();
            Dispatcher.UIThread.RunJobs();

            received.ShouldNotBeNull();
            received.Name.ShouldBe("Jasmine Brooks");
            received.Email.ShouldBe("jasmine@northwind.studio");
            received.Password.ShouldBe("Northwind26");
            received.ProductUpdates.ShouldBeTrue();

            form.Step.ShouldBe(SignupFormStep.Done);
            form.Classes.Contains(SignupForm.DonePseudoClass).ShouldBeTrue();
            form.SignedUpWith.ShouldBe(SignupFormMethod.Email);
            form.IsBusy.ShouldBeFalse();
            form.Status.ShouldBe("Account created with your email");
            form.StatusLine!.Text.ShouldBe("Account created with your email");
            form.Announcer!.History.ShouldBe(["Creating your account", "Account created with your email"]);
            created.Count.ShouldBe(1);
            created[0].Method.ShouldBe(SignupFormMethod.Email);
            created[0].Name.ShouldBe("Jasmine Brooks");
            created[0].Email.ShouldBe("jasmine@northwind.studio");
            created[0].ProductUpdates.ShouldBeTrue();
            command.Parameters.ShouldBe([created[0]]);

            form.Stage!.Current.ShouldBe(1);
            TestHelpers.Descendant<Border>(form, "PART_FormStep").IsVisible.ShouldBeFalse();
            form.DoneTitle!.Text.ShouldBe("Welcome, Jasmine");
            form.DoneTitle.IsFocused.ShouldBeTrue();
            string.Concat(form.DoneDescription!.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text))
                .ShouldBe("Your account is ready. We sent a confirmation to jasmine@northwind.studio.");
            form.MethodValueText.ShouldBe("Email");
            form.UpdatesValueText.ShouldBe("On");
            form.Mark!.RingProgress.ShouldBe(1, 1e-6);
            form.Mark.CheckProgress.ShouldBe(1, 1e-6);
            form.StartOverButton!.Content.ShouldBe("Start over");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void While_Busy_The_Form_Holds_Still()
    {
        var form = Quick();
        var pending = new TaskCompletionSource();
        CancellationToken token = default;
        form.SubmitAsync = (_, ct) =>
        {
            token = ct;
            return pending.Task;
        };
        var providers = 0;
        form.ProviderSelected += (_, _) => providers++;
        var window = Show(form);
        try
        {
            Fill(form, "Jasmine Brooks", "jasmine@northwind.studio", "Northwind26");
            var submit = form.Submit();
            Dispatcher.UIThread.RunJobs();
            form.IsBusy.ShouldBeTrue();
            form.BusyMethod.ShouldBe(SignupFormMethod.Email);
            form.Classes.Contains(SignupForm.BusyPseudoClass).ShouldBeTrue();
            form.Status.ShouldBe("Creating your account");
            form.SubmitButton!.IsLoading.ShouldBeTrue();
            form.NameInput!.IsReadOnly.ShouldBeTrue();
            form.EmailInput!.IsReadOnly.ShouldBeTrue();
            form.PasswordInput!.IsReadOnly.ShouldBeTrue();
            form.UpdatesBox!.IsEnabled.ShouldBeFalse();
            // Other actions are ignored meanwhile.
            form.ContinueWith(SignupFormMethod.Google);
            Dispatcher.UIThread.RunJobs();
            providers.ShouldBe(0);
            form.BusyMethod.ShouldBe(SignupFormMethod.Email);

            pending.SetResult();
            Dispatcher.UIThread.RunJobs();
            submit.IsCompleted.ShouldBeTrue();
            form.Step.ShouldBe(SignupFormStep.Done);
            form.IsBusy.ShouldBeFalse();
            form.NameInput.IsReadOnly.ShouldBeFalse();
            form.UpdatesBox.IsEnabled.ShouldBeTrue();
            token.IsCancellationRequested.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Submit_Shows_Its_Message_Above_The_Button()
    {
        var form = Quick();
        var fail = true;
        form.SubmitAsync = (_, _) => fail ? Task.FromException(new InvalidOperationException("That email is already registered.")) : Task.CompletedTask;
        var window = Show(form);
        try
        {
            Fill(form, "Jasmine Brooks", "jasmine@northwind.studio", "Northwind26");
            form.Submit();
            Dispatcher.UIThread.RunJobs();
            form.Step.ShouldBe(SignupFormStep.Form);
            form.IsBusy.ShouldBeFalse();
            form.ShownError.ShouldBe("That email is already registered.");
            form.ErrorRow!.IsPresent.ShouldBeTrue();
            form.ErrorRow.ShownText.ShouldBe("That email is already registered.");
            form.Status.ShouldBe("Account not created");
            form.Alert!.History.ShouldContain("That email is already registered.");

            // A server error shows when there is no submit error of the block's own.
            form.ServerError = "Sign ups are paused.";
            form.ShownError.ShouldBe("That email is already registered.");
            fail = false;
            form.Submit();
            Dispatcher.UIThread.RunJobs();
            form.Step.ShouldBe(SignupFormStep.Done);
            form.ShownError.ShouldBe("Sign ups are paused.");
            form.ServerError = null;
            form.ShownError.ShouldBeNull();
            form.ErrorRow.IsPresent.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Exception_Without_A_Message_Shows_The_Fallback()
    {
        var form = Quick();
        form.SubmitAsync = (_, _) => Task.FromException(new CustomEmptyException());
        var window = Show(form);
        try
        {
            Fill(form, "Jasmine Brooks", "jasmine@northwind.studio", "Northwind26");
            form.Submit();
            Dispatcher.UIThread.RunJobs();
            form.ShownError.ShouldBe("We couldn't create your account. Try again.");
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class CustomEmptyException : Exception
    {
        public override string Message => " ";
    }

    [AvaloniaFact]
    public void A_Simulated_Provider_Fills_In_The_Sample_Person()
    {
        var form = Quick();
        var selected = new List<SignupFormMethod>();
        form.ProviderSelected += (_, e) => selected.Add(e.Provider);
        var command = new Recorder();
        form.ProviderCommand = command;
        var window = Show(form);
        try
        {
            form.ContinueWith(SignupFormMethod.Google);
            Dispatcher.UIThread.RunJobs();
            selected.ShouldBe([SignupFormMethod.Google]);
            command.Parameters.ShouldBe([SignupFormMethod.Google]);
            form.Step.ShouldBe(SignupFormStep.Done);
            form.SignedUpWith.ShouldBe(SignupFormMethod.Google);
            form.FullName.ShouldBe("Jasmine Brooks");
            form.Email.ShouldBe("jasmine@northwind.studio");
            form.Announcer!.History.ShouldBe(["Opening Google", "Account created with Google"]);
            form.MethodValueText.ShouldBe("Google");
            form.UpdatesValueText.ShouldBe("Off");
            form.DoneTitle!.Text.ShouldBe("Welcome, Jasmine");

            // A typed name is kept.
            form.Reset();
            form.FullName = "Ada Lovelace";
            form.ContinueWith(SignupFormMethod.GitHub);
            Dispatcher.UIThread.RunJobs();
            form.FullName.ShouldBe("Ada Lovelace");
            form.Email.ShouldBe("jasmine@northwind.studio");
            form.DoneTitle.Text.ShouldBe("Welcome, Ada");
            form.Status.ShouldBe("Account created with GitHub");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Provider_Handler_Replaces_The_Simulation()
    {
        var form = Quick();
        var pending = new TaskCompletionSource();
        SignupFormMethod? asked = null;
        form.ProviderAsync = (provider, _) =>
        {
            asked = provider;
            return pending.Task;
        };
        var window = Show(form);
        try
        {
            form.ProviderButton(SignupFormMethod.Apple)!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            asked.ShouldBe(SignupFormMethod.Apple);
            form.BusyMethod.ShouldBe(SignupFormMethod.Apple);
            form.ProviderButton(SignupFormMethod.Apple)!.IsLoading.ShouldBeTrue();
            form.ProviderButton(SignupFormMethod.Google)!.IsLoading.ShouldBeFalse();
            form.ProviderButton(SignupFormMethod.Google)!.IsPressScaleEnabled.ShouldBeFalse();
            form.SubmitButton!.IsLoading.ShouldBeFalse();
            form.Status.ShouldBe("Opening Apple");
            pending.SetResult();
            Dispatcher.UIThread.RunJobs();
            form.Step.ShouldBe(SignupFormStep.Done);
            form.SignedUpWith.ShouldBe(SignupFormMethod.Apple);
            // A real provider fills nothing in.
            form.FullName.ShouldBe(string.Empty);
            form.DoneTitle!.Text.ShouldBe("Welcome, there");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Start_Over_Empties_The_Form_And_Focuses_The_Name()
    {
        var form = Quick();
        var window = Show(form);
        try
        {
            Fill(form, "Jasmine Brooks", "jasmine@northwind.studio", "Northwind26");
            form.PasswordInput!.IsRevealed = true;
            form.UpdatesBox!.IsChecked = true;
            form.Submit();
            Dispatcher.UIThread.RunJobs();
            form.Step.ShouldBe(SignupFormStep.Done);

            form.StartOverButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            form.Step.ShouldBe(SignupFormStep.Form);
            form.FullName.ShouldBe(string.Empty);
            form.Email.ShouldBe(string.Empty);
            form.Password.ShouldBe(string.Empty);
            form.ProductUpdates.ShouldBeFalse();
            form.NameInput!.Text.ShouldBe(string.Empty);
            form.UpdatesBox.IsChecked.ShouldBe(false);
            form.PasswordInput.IsRevealed.ShouldBeFalse();
            form.NameInput.ErrorMessage.ShouldBeNull();
            form.PasswordInput.IsInvalid.ShouldBeFalse();
            form.FeedbackLine!.Text.ShouldBe("Use 8 or more characters.");
            form.Status.ShouldBe("Start again with new details");
            form.Stage!.Current.ShouldBe(0);
            // The completion panel still says who it welcomed while it leaves.
            form.DoneTitle!.Text.ShouldBe("Welcome, Jasmine");

            // The name gets the focus 60 ms later.
            var until = DateTime.UtcNow.AddSeconds(2);
            while (!form.NameInput.IsFocused && DateTime.UtcNow < until)
            {
                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }
            form.NameInput.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_In_A_Field_Submits()
    {
        var form = Quick();
        var window = Show(form);
        try
        {
            Fill(form, "Jasmine Brooks", "jasmine@northwind.studio", "Northwind26");
            form.PasswordInput!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            form.Step.ShouldBe(SignupFormStep.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Links_Raise_Navigate_And_Otherwise_Say_Where_They_Go()
    {
        var form = Quick();
        var links = new List<SignupFormLink>();
        var handle = false;
        form.Navigate += (_, e) =>
        {
            links.Add(e.Link);
            e.Handled = handle;
        };
        var command = new Recorder();
        form.NavigateCommand = command;
        var window = Show(form);
        try
        {
            form.TermsLink!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            form.Status.ShouldBe("Terms open in your app");
            form.OpenLink(SignupFormLink.Privacy);
            form.Status.ShouldBe("Privacy policy opens in your app");
            form.OpenLink(SignupFormLink.SignIn);
            form.Status.ShouldBe("Sign in opens in your app");
            links.ShouldBe([SignupFormLink.Terms, SignupFormLink.Privacy, SignupFormLink.SignIn]);
            command.Parameters.ShouldBe([SignupFormLink.Terms, SignupFormLink.Privacy, SignupFormLink.SignIn]);
            // The same text twice is not announced twice (React does not re-render).
            form.OpenLink(SignupFormLink.SignIn);
            form.Announcer!.History.Count(t => t == "Sign in opens in your app").ShouldBe(1);

            handle = true;
            form.OpenLink(SignupFormLink.Terms);
            form.Status.ShouldBe("Sign in opens in your app");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Follows_Its_Own_Width()
    {
        var form = Quick();
        var window = Show(form, width: 600);
        try
        {
            form.Classes.Contains(SignupForm.NarrowPseudoClass).ShouldBeFalse();
            form.Classes.Contains(SignupForm.CompactPseudoClass).ShouldBeFalse();
            window.Width = 400;
            Dispatcher.UIThread.RunJobs();
            form.Bounds.Width.ShouldBe(400, 0.01);
            form.Classes.Contains(SignupForm.CompactPseudoClass).ShouldBeTrue();
            form.Classes.Contains(SignupForm.NarrowPseudoClass).ShouldBeFalse();
            form.CornerRadius.TopLeft.ShouldBe(26, 0.01);
            window.Width = 340;
            Dispatcher.UIThread.RunJobs();
            form.Classes.Contains(SignupForm.NarrowPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<Border>(form, "PART_FormStep").Padding.ShouldBe(new Thickness(20, 24));
            TestHelpers.Descendant<TextBlock>(form, "PART_Title").FontSize.ShouldBe(28, 0.01);
            form.Meter!.BarWidth.ShouldBe(14, 0.01);
            TestHelpers.Descendant<Border>(form, "PART_Footer").Padding.ShouldBe(new Thickness(20, 12));
            form.ProviderButton(SignupFormMethod.Google)!.FontSize.ShouldBe(12, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Footer_Wraps_The_Prompt_Under_The_Status_When_Narrow()
    {
        var form = Quick();
        var window = Show(form, width: 330);
        try
        {
            var row = TestHelpers.Descendant<SignupFormFooterPanel>(form);
            var prompt = row.Children[1];
            // 290 - 40 = 250 of room: 192 + 16 + the prompt does not fit.
            row.IsWrapped.ShouldBeTrue();
            prompt.Bounds.X.ShouldBe(0, 0.01);
            prompt.Bounds.Y.ShouldBeGreaterThan(0);
            window.Width = 600;
            Dispatcher.UIThread.RunJobs();
            // Side by side whenever 192 + 16 + the prompt fit (the headless font is wider than Inter).
            row.IsWrapped.ShouldBe(SignupFormFooterPanel.StatusBasis + SignupFormFooterPanel.ColumnGap + prompt.DesiredSize.Width > row.Bounds.Width);

        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Providers_Can_Be_Hidden_And_Labels_Replaced()
    {
        var form = Quick();
        var window = Show(form);
        try
        {
            form.AreProvidersVisible = false;
            form.ProvidersBlock!.IsVisible.ShouldBeFalse();
            form.Labels = new SignupFormLabels
            {
                Title = "Join the studio",
                Submit = "Join",
                StrengthWords = ["Thin", "Okay", "Solid", "Great"],
                StrengthLine = word => word
            };
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<TextBlock>(form, "PART_Title").Text.ShouldBe("Join the studio");
            form.SubmitButton!.Content.ShouldBe("Join");
            form.Password = "abcdefgH1";
            Dispatcher.UIThread.RunJobs();
            form.FeedbackLine!.Text.ShouldBe("Solid");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_The_Card_Abandons_A_Request()
    {
        var form = Quick();
        var pending = new TaskCompletionSource();
        CancellationToken token = default;
        form.SubmitAsync = (_, ct) =>
        {
            token = ct;
            return pending.Task;
        };
        var window = Show(form);
        try
        {
            Fill(form, "Jasmine Brooks", "jasmine@northwind.studio", "Northwind26");
            form.Submit();
            Dispatcher.UIThread.RunJobs();
            form.IsBusy.ShouldBeTrue();
            form.Reset();
            token.IsCancellationRequested.ShouldBeTrue();
            form.IsBusy.ShouldBeFalse();
            pending.SetResult();
            Dispatcher.UIThread.RunJobs();
            form.Step.ShouldBe(SignupFormStep.Form);
        }
        finally
        {
            window.Close();
        }
    }
}
