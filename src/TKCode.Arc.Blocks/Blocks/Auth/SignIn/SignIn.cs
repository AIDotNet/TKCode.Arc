// Ported from Arc's free sign-in block (MIT, Copyright (c) 2026 Elia Kuratli): sign-in.tsx (props, copy and state).
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A sign in card that morphs from an email address to a six digit code to the signed in account, with a passkey button
/// and single sign-on buttons (Arc's free sign-in block, the "Email code" variant of Login). Each step slides in from the
/// side it is heading to while the card's height follows on a spring; the account's avatar grows from the code step into
/// the signed in state, where a ring draws around it and a check pops in.
/// </summary>
/// <remarks>
/// <para>
/// The card does no authentication itself. Connect <see cref="SendCodeAsync"/> (send a code, optionally return the
/// account), <see cref="VerifyCodeAsync"/> (check it) and <see cref="SignInWithAsync"/> (passkey and providers) to your
/// auth provider and listen to <see cref="SignedIn"/>. Without handlers it simulates Arc's preview: sending takes 800 ms,
/// checking 700 ms and accepts <see cref="DemoCode"/>, a passkey takes 1100 ms and a provider 900 ms.
/// </para>
/// <para>
/// The email is checked as Arc does: after the field loses focus with text in it, or from the first Continue on; common
/// domain typos ("gmial.com") offer a one-click fix. A wrong code clears the slots, shakes them and says so; the code can
/// be sent again after <see cref="ResendSeconds"/>. Every step change and error is announced politely and shown in the
/// footer. The card is at most 432 wide; at 380 or less its padding, heading and provider buttons tighten.
/// </para>
/// Ported from Arc's free sign-in block (MIT, Copyright (c) 2026 Elia Kuratli).
/// </remarks>
[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(ViewportPart, typeof(SignInViewport))]
[TemplatePart(StepsPart, typeof(SignInStepHost))]
[TemplatePart(StatusPart, typeof(SignInSwap))]
[TemplatePart(SignUpPromptPart, typeof(TextBlock))]
[TemplatePart(SignUpPart, typeof(SignInTextButton))]
[TemplatePart(SwitchPart, typeof(Control))]
[TemplatePart(AnnouncerPart, typeof(AuthAnnouncer))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, BusyPseudoClass, EmailPseudoClass, CodePseudoClass, DonePseudoClass)]
public partial class SignIn : TemplatedControl
{
    public const string CardPart = "PART_Card";
    public const string ViewportPart = "PART_Viewport";
    public const string StepsPart = "PART_Steps";
    public const string StatusPart = "PART_Status";
    public const string SignUpPromptPart = "PART_SignUpPrompt";
    public const string SignUpPart = "PART_SignUp";
    public const string SwitchPart = "PART_Switch";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the card is at most 380 wide: tighter padding, a smaller heading and smaller provider buttons.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the card is offered at most 448 (Arc: a viewport of at most 480): softer panel corners.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while something is being sent or checked.</summary>
    public const string BusyPseudoClass = ":busy";

    /// <summary>Set on the email step.</summary>
    public const string EmailPseudoClass = ":email";

    /// <summary>Set on the code step.</summary>
    public const string CodePseudoClass = ":code";

    /// <summary>Set once signed in.</summary>
    public const string DonePseudoClass = ":done";

    /// <summary>The card is narrow at or below this width (Arc's container query).</summary>
    internal const double NarrowWidth = 380;

    /// <summary>The card's corners soften when it is offered at most this width (Arc's 480px viewport less two 16px gutters).</summary>
    internal const double CompactWidth = 448;

    #region Behaviour

    /// <summary>The code the simulated email contains ("123456"); used only while <see cref="VerifyCodeAsync"/> is null.</summary>
    public static readonly StyledProperty<string> DemoCodeProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(DemoCode), "123456");

    /// <summary>The email address in the field; two-way. Signing out puts the account's address back.</summary>
    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(Email), string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: (_, v) => v ?? string.Empty);

    /// <summary>Seconds before a new code can be sent (30).</summary>
    public static readonly StyledProperty<int> ResendSecondsProperty =
        AvaloniaProperty.Register<SignIn, int>(nameof(ResendSeconds), 30, coerce: (_, v) => Math.Max(0, v));

    /// <summary>Whether the passkey button shows (true).</summary>
    public static readonly StyledProperty<bool> IsPasskeyVisibleProperty =
        AvaloniaProperty.Register<SignIn, bool>(nameof(IsPasskeyVisible), true);

    /// <summary>Whether the single sign-on buttons show (true).</summary>
    public static readonly StyledProperty<bool> IsProvidersVisibleProperty =
        AvaloniaProperty.Register<SignIn, bool>(nameof(IsProvidersVisible), true);

    /// <summary>Whether the footer's "No account? Sign up" shows (true).</summary>
    public static readonly StyledProperty<bool> IsSignUpVisibleProperty =
        AvaloniaProperty.Register<SignIn, bool>(nameof(IsSignUpVisible), true);

    /// <summary>The known accounts; when set, it is used instead of <see cref="Accounts"/>.</summary>
    public static readonly StyledProperty<IEnumerable<SignInAccount>?> AccountsSourceProperty =
        AvaloniaProperty.Register<SignIn, IEnumerable<SignInAccount>?>(nameof(AccountsSource));

    /// <summary>The single sign-on providers; when set, it is used instead of <see cref="Providers"/>.</summary>
    public static readonly StyledProperty<IEnumerable<SignInProvider>?> ProvidersSourceProperty =
        AvaloniaProperty.Register<SignIn, IEnumerable<SignInProvider>?>(nameof(ProvidersSource));

    /// <summary>Runs after <see cref="SignedIn"/>; its parameter is the account.</summary>
    public static readonly StyledProperty<ICommand?> SignedInCommandProperty =
        AvaloniaProperty.Register<SignIn, ICommand?>(nameof(SignedInCommand));

    /// <summary>Runs after <see cref="SignUpRequested"/> when "Sign up" is pressed.</summary>
    public static readonly StyledProperty<ICommand?> SignUpCommandProperty =
        AvaloniaProperty.Register<SignIn, ICommand?>(nameof(SignUpCommand));

    /// <summary>Sign in succeeded (Arc's <c>onSignIn(account, method)</c>).</summary>
    public static readonly RoutedEvent<SignInSignedInEventArgs> SignedInEvent =
        RoutedEvent.Register<SignIn, SignInSignedInEventArgs>(nameof(SignedIn), RoutingStrategies.Bubble);

    /// <summary>"Sign out" was pressed on the signed in step.</summary>
    public static readonly RoutedEvent<SignInSignedOutEventArgs> SignedOutEvent =
        RoutedEvent.Register<SignIn, SignInSignedOutEventArgs>(nameof(SignedOut), RoutingStrategies.Bubble);

    /// <summary>"Sign up" was pressed. Mark it handled when you open sign up; otherwise the card says "Sign up opens in your app".</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SignUpRequestedEvent =
        RoutedEvent.Register<SignIn, RoutedEventArgs>(nameof(SignUpRequested), RoutingStrategies.Bubble);

    #endregion

    #region State

    private SignInStep _step;
    private SignInAccount? _account;
    private SignInMethod? _method;
    private bool _isBusy;
    private string _status = string.Empty;

    /// <summary>The step showing: Email, Code or Done (read only).</summary>
    public static readonly DirectProperty<SignIn, SignInStep> StepProperty =
        AvaloniaProperty.RegisterDirect<SignIn, SignInStep>(nameof(Step), o => o.Step);

    /// <summary>The account being signed in to (code step) or signed in (done), or null (read only).</summary>
    public static readonly DirectProperty<SignIn, SignInAccount?> AccountProperty =
        AvaloniaProperty.RegisterDirect<SignIn, SignInAccount?>(nameof(Account), o => o.Account);

    /// <summary>How the account signed in, once it has (read only).</summary>
    public static readonly DirectProperty<SignIn, SignInMethod?> MethodProperty =
        AvaloniaProperty.RegisterDirect<SignIn, SignInMethod?>(nameof(Method), o => o.Method);

    /// <summary>True while a code is being sent or checked, or a passkey or provider is pending (read only).</summary>
    public static readonly DirectProperty<SignIn, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<SignIn, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>The last status line ("Code sent"), also announced to screen readers (read only).</summary>
    public static readonly DirectProperty<SignIn, string> StatusProperty =
        AvaloniaProperty.RegisterDirect<SignIn, string>(nameof(Status), o => o.Status);

    #endregion

    #region Copy: email step

    /// <summary>The heading of the email step ("Sign in to Arc").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(Title), "Sign in to Arc");

    /// <summary>The line under the heading of the email step.</summary>
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(Description), "Enter your email and we will send you a 6 digit code.");

    /// <summary>The label of the email field ("Email").</summary>
    public static readonly StyledProperty<string> EmailLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(EmailLabel), "Email");

    /// <summary>The placeholder of the email field ("name@example.com").</summary>
    public static readonly StyledProperty<string> EmailPlaceholderProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(EmailPlaceholder), "name@example.com");

    /// <summary>The label of the button that sends the code ("Continue").</summary>
    public static readonly StyledProperty<string> ContinueLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(ContinueLabel), "Continue");

    /// <summary>The word in the divider ("or").</summary>
    public static readonly StyledProperty<string> DividerLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(DividerLabel), "or");

    /// <summary>The label of the passkey button ("Sign in with a passkey").</summary>
    public static readonly StyledProperty<string> PasskeyLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(PasskeyLabel), "Sign in with a passkey");

    /// <summary>The accessible name of the group of provider buttons ("Single sign-on").</summary>
    public static readonly StyledProperty<string> SingleSignOnLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SingleSignOnLabel), "Single sign-on");

    /// <summary>The accessible name of a provider button; {0} is the provider ("Continue with {0}").</summary>
    public static readonly StyledProperty<string> ProviderLabelFormatProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(ProviderLabelFormat), "Continue with {0}");

    /// <summary>The error for an empty email field.</summary>
    public static readonly StyledProperty<string> EmptyEmailMessageProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(EmptyEmailMessage), "Enter your email address.");

    /// <summary>The error for an incomplete address.</summary>
    public static readonly StyledProperty<string> InvalidEmailMessageProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(InvalidEmailMessage), "Enter a full address, like name@example.com.");

    /// <summary>The typo suggestion; {0} is the corrected address, shown as a button ("Did you mean {0}?").</summary>
    public static readonly StyledProperty<string> SuggestionFormatProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SuggestionFormat), "Did you mean {0}?");

    /// <summary>The accessible name of the suggestion button; {0} is the address ("Use {0}").</summary>
    public static readonly StyledProperty<string> UseSuggestionLabelFormatProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(UseSuggestionLabelFormat), "Use {0}");

    /// <summary>The error when <see cref="SendCodeAsync"/> fails.</summary>
    public static readonly StyledProperty<string> SendFailedMessageProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SendFailedMessage), "We couldn't send a code. Try again.");

    #endregion

    #region Copy: code step

    /// <summary>The heading of the code step ("Check your email").</summary>
    public static readonly StyledProperty<string> CodeTitleProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(CodeTitle), "Check your email");

    /// <summary>The line under the heading of the code step.</summary>
    public static readonly StyledProperty<string> CodeDescriptionProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(CodeDescription), "Enter the 6 digit code we sent. It expires in 10 minutes.");

    /// <summary>The label of the button that goes back to the email ("Change").</summary>
    public static readonly StyledProperty<string> ChangeEmailLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(ChangeEmailLabel), "Change");

    /// <summary>The accessible name of that button ("Change email").</summary>
    public static readonly StyledProperty<string> ChangeEmailAccessibleNameProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(ChangeEmailAccessibleName), "Change email");

    /// <summary>The label of the code field ("Verification code").</summary>
    public static readonly StyledProperty<string> CodeLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(CodeLabel), "Verification code");

    /// <summary>
    /// Helper copy under the code. Null shows <see cref="DemoCodeHintFormat"/> while the check is simulated (no
    /// <see cref="VerifyCodeAsync"/>) and nothing otherwise.
    /// </summary>
    public static readonly StyledProperty<string?> CodeHintProperty =
        AvaloniaProperty.Register<SignIn, string?>(nameof(CodeHint));

    /// <summary>The helper copy of the simulated check; {0} is <see cref="DemoCode"/> ("For this demo, the code is {0}.").</summary>
    public static readonly StyledProperty<string> DemoCodeHintFormatProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(DemoCodeHintFormat), "For this demo, the code is {0}.");

    /// <summary>The label of the button that checks the code ("Verify").</summary>
    public static readonly StyledProperty<string> VerifyLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(VerifyLabel), "Verify");

    /// <summary>The error for an incomplete code ("Enter all 6 digits.").</summary>
    public static readonly StyledProperty<string> IncompleteCodeMessageProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(IncompleteCodeMessage), "Enter all 6 digits.");

    /// <summary>The error for a wrong code.</summary>
    public static readonly StyledProperty<string> CodeMismatchMessageProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(CodeMismatchMessage), "That code didn't match. Check the latest email and try again.");

    /// <summary>The error when <see cref="VerifyCodeAsync"/> fails.</summary>
    public static readonly StyledProperty<string> VerifyFailedMessageProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(VerifyFailedMessage), "We couldn't check the code. Try again.");

    /// <summary>The resend button once a new code can be sent ("Resend code").</summary>
    public static readonly StyledProperty<string> ResendLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(ResendLabel), "Resend code");

    /// <summary>The words before the countdown ("Resend code in").</summary>
    public static readonly StyledProperty<string> ResendCountdownLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(ResendCountdownLabel), "Resend code in");

    /// <summary>The accessible name while waiting; {0} is the seconds ("Resend code, available in {0} seconds").</summary>
    public static readonly StyledProperty<string> ResendAvailableInFormatProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(ResendAvailableInFormat), "Resend code, available in {0} seconds");

    #endregion

    #region Copy: done step and footer

    /// <summary>The welcome; {0} is the first name ("Welcome back, {0}").</summary>
    public static readonly StyledProperty<string> WelcomeFormatProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(WelcomeFormat), "Welcome back, {0}");

    /// <summary>The label of the method row ("Signed in with").</summary>
    public static readonly StyledProperty<string> SignedInWithLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SignedInWithLabel), "Signed in with");

    /// <summary>The label of the second row ("This browser").</summary>
    public static readonly StyledProperty<string> DeviceLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(DeviceLabel), "This browser");

    /// <summary>The value of the second row ("Remembered for 30 days").</summary>
    public static readonly StyledProperty<string> DeviceValueProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(DeviceValue), "Remembered for 30 days");

    /// <summary>The label of the sign out button ("Sign out").</summary>
    public static readonly StyledProperty<string> SignOutLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SignOutLabel), "Sign out");

    /// <summary>The name of the email code method ("Email code").</summary>
    public static readonly StyledProperty<string> EmailCodeMethodLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(EmailCodeMethodLabel), "Email code");

    /// <summary>The name of the passkey method ("Passkey").</summary>
    public static readonly StyledProperty<string> PasskeyMethodLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(PasskeyMethodLabel), "Passkey");

    /// <summary>The name of an account whose address gives none ("Arc member").</summary>
    public static readonly StyledProperty<string> FallbackNameProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(FallbackName), "Arc member");

    /// <summary>The words before the sign up button ("No account?").</summary>
    public static readonly StyledProperty<string> SignUpPromptProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SignUpPrompt), "No account?");

    /// <summary>The label of the sign up button ("Sign up").</summary>
    public static readonly StyledProperty<string> SignUpLabelProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SignUpLabel), "Sign up");

    #endregion

    #region Copy: status

    /// <summary>Status while a code is sent ("Sending a code").</summary>
    public static readonly StyledProperty<string> SendingStatusProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SendingStatus), "Sending a code");

    /// <summary>Status once the code is sent ("Code sent").</summary>
    public static readonly StyledProperty<string> CodeSentStatusProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(CodeSentStatus), "Code sent");

    /// <summary>Status after the suggestion is taken; {0} is the address ("Email changed to {0}").</summary>
    public static readonly StyledProperty<string> EmailChangedStatusFormatProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(EmailChangedStatusFormat), "Email changed to {0}");

    /// <summary>Status while the code is checked ("Checking code").</summary>
    public static readonly StyledProperty<string> CheckingStatusProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(CheckingStatus), "Checking code");

    /// <summary>Status after a wrong code ("Code didn't match").</summary>
    public static readonly StyledProperty<string> MismatchStatusProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(MismatchStatus), "Code didn't match");

    /// <summary>Status after a new code is sent ("New code sent").</summary>
    public static readonly StyledProperty<string> NewCodeStatusProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(NewCodeStatus), "New code sent");

    /// <summary>Status after going back to the email ("Edit your email").</summary>
    public static readonly StyledProperty<string> EditEmailStatusProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(EditEmailStatus), "Edit your email");

    /// <summary>Status while a passkey is pending ("Waiting for your passkey").</summary>
    public static readonly StyledProperty<string> PasskeyWaitingStatusProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(PasskeyWaitingStatus), "Waiting for your passkey");

    /// <summary>Status while a provider is pending; {0} is the provider ("Opening {0}").</summary>
    public static readonly StyledProperty<string> OpeningProviderStatusFormatProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(OpeningProviderStatusFormat), "Opening {0}");

    /// <summary>Status once signed in; {0} is the method ("Signed in with {0}").</summary>
    public static readonly StyledProperty<string> SignedInStatusFormatProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SignedInStatusFormat), "Signed in with {0}");

    /// <summary>How the signed in status names the email code method ("an email code").</summary>
    public static readonly StyledProperty<string> EmailCodeStatusMethodProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(EmailCodeStatusMethod), "an email code");

    /// <summary>Status when a passkey or provider fails; {0} is the method ("Couldn't sign in with {0}. Try again.").</summary>
    public static readonly StyledProperty<string> SignInFailedStatusFormatProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SignInFailedStatusFormat), "Couldn't sign in with {0}. Try again.");

    /// <summary>Status after signing out ("Signed out").</summary>
    public static readonly StyledProperty<string> SignedOutStatusProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SignedOutStatus), "Signed out");

    /// <summary>Status when nobody handles <see cref="SignUpRequested"/> ("Sign up opens in your app").</summary>
    public static readonly StyledProperty<string> SignUpStatusProperty =
        AvaloniaProperty.Register<SignIn, string>(nameof(SignUpStatus), "Sign up opens in your app");

    #endregion

    private readonly AvaloniaList<SignInAccount> _accounts = new();
    private readonly AvaloniaList<SignInProvider> _providers = new();

    /// <summary>
    /// Known accounts (Arc's teammates): an address whose first part matches one's first name signs in to it, keeping
    /// its name and photo. Empty uses Arc's five sample teammates (without photos). Ignored while <see cref="AccountsSource"/> is set.
    /// </summary>
    [Content]
    public AvaloniaList<SignInAccount> Accounts => _accounts;

    /// <summary>The single sign-on providers. Empty uses Arc's three (Google, Apple, GitHub). Ignored while <see cref="ProvidersSource"/> is set.</summary>
    public AvaloniaList<SignInProvider> Providers => _providers;

    /// <inheritdoc cref="AccountsSourceProperty"/>
    public IEnumerable<SignInAccount>? AccountsSource { get => GetValue(AccountsSourceProperty); set => SetValue(AccountsSourceProperty, value); }

    /// <inheritdoc cref="ProvidersSourceProperty"/>
    public IEnumerable<SignInProvider>? ProvidersSource { get => GetValue(ProvidersSourceProperty); set => SetValue(ProvidersSourceProperty, value); }

    /// <summary>
    /// Sends a code to the trimmed address (Continue and Resend). Return the account to show, or null to derive it from
    /// the address and the known accounts. Throw to stay on the email step with <see cref="SendFailedMessage"/>. The token
    /// is cancelled when the card is reset or removed. Null simulates a send that takes 800 ms.
    /// </summary>
    public Func<string, CancellationToken, Task<SignInAccount?>>? SendCodeAsync { get; set; }

    /// <summary>
    /// Checks a complete code for the account; return true to sign in, false for a wrong code. Throw to show
    /// <see cref="VerifyFailedMessage"/>. Null simulates a 700 ms check against <see cref="DemoCode"/>.
    /// </summary>
    public Func<SignInAccount, string, CancellationToken, Task<bool>>? VerifyCodeAsync { get; set; }

    /// <summary>
    /// Signs in with a passkey or a provider (connect WebAuthn and OAuth here). Return the account, or null for the first
    /// known account. Throw to stay put and say so. Null simulates 1100 ms for a passkey and 900 ms for a provider.
    /// </summary>
    public Func<SignInMethod, CancellationToken, Task<SignInAccount?>>? SignInWithAsync { get; set; }

    /// <inheritdoc cref="DemoCodeProperty"/>
    public string DemoCode { get => GetValue(DemoCodeProperty); set => SetValue(DemoCodeProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="ResendSecondsProperty"/>
    public int ResendSeconds { get => GetValue(ResendSecondsProperty); set => SetValue(ResendSecondsProperty, value); }

    /// <inheritdoc cref="IsPasskeyVisibleProperty"/>
    public bool IsPasskeyVisible { get => GetValue(IsPasskeyVisibleProperty); set => SetValue(IsPasskeyVisibleProperty, value); }

    /// <inheritdoc cref="IsProvidersVisibleProperty"/>
    public bool IsProvidersVisible { get => GetValue(IsProvidersVisibleProperty); set => SetValue(IsProvidersVisibleProperty, value); }

    /// <inheritdoc cref="IsSignUpVisibleProperty"/>
    public bool IsSignUpVisible { get => GetValue(IsSignUpVisibleProperty); set => SetValue(IsSignUpVisibleProperty, value); }

    /// <inheritdoc cref="SignedInCommandProperty"/>
    public ICommand? SignedInCommand { get => GetValue(SignedInCommandProperty); set => SetValue(SignedInCommandProperty, value); }

    /// <inheritdoc cref="SignUpCommandProperty"/>
    public ICommand? SignUpCommand { get => GetValue(SignUpCommandProperty); set => SetValue(SignUpCommandProperty, value); }

    /// <inheritdoc cref="SignedInEvent"/>
    public event EventHandler<SignInSignedInEventArgs>? SignedIn
    {
        add => AddHandler(SignedInEvent, value);
        remove => RemoveHandler(SignedInEvent, value);
    }

    /// <inheritdoc cref="SignedOutEvent"/>
    public event EventHandler<SignInSignedOutEventArgs>? SignedOut
    {
        add => AddHandler(SignedOutEvent, value);
        remove => RemoveHandler(SignedOutEvent, value);
    }

    /// <inheritdoc cref="SignUpRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? SignUpRequested
    {
        add => AddHandler(SignUpRequestedEvent, value);
        remove => RemoveHandler(SignUpRequestedEvent, value);
    }

    /// <inheritdoc cref="StepProperty"/>
    public SignInStep Step
    {
        get => _step;
        private set => SetAndRaise(StepProperty, ref _step, value);
    }

    /// <inheritdoc cref="AccountProperty"/>
    public SignInAccount? Account
    {
        get => _account;
        private set => SetAndRaise(AccountProperty, ref _account, value);
    }

    /// <inheritdoc cref="MethodProperty"/>
    public SignInMethod? Method
    {
        get => _method;
        private set => SetAndRaise(MethodProperty, ref _method, value);
    }

    /// <inheritdoc cref="IsBusyProperty"/>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetAndRaise(IsBusyProperty, ref _isBusy, value);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public string Status
    {
        get => _status;
        private set => SetAndRaise(StatusProperty, ref _status, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="EmailLabelProperty"/>
    public string EmailLabel { get => GetValue(EmailLabelProperty); set => SetValue(EmailLabelProperty, value); }

    /// <inheritdoc cref="EmailPlaceholderProperty"/>
    public string EmailPlaceholder { get => GetValue(EmailPlaceholderProperty); set => SetValue(EmailPlaceholderProperty, value); }

    /// <inheritdoc cref="ContinueLabelProperty"/>
    public string ContinueLabel { get => GetValue(ContinueLabelProperty); set => SetValue(ContinueLabelProperty, value); }

    /// <inheritdoc cref="DividerLabelProperty"/>
    public string DividerLabel { get => GetValue(DividerLabelProperty); set => SetValue(DividerLabelProperty, value); }

    /// <inheritdoc cref="PasskeyLabelProperty"/>
    public string PasskeyLabel { get => GetValue(PasskeyLabelProperty); set => SetValue(PasskeyLabelProperty, value); }

    /// <inheritdoc cref="SingleSignOnLabelProperty"/>
    public string SingleSignOnLabel { get => GetValue(SingleSignOnLabelProperty); set => SetValue(SingleSignOnLabelProperty, value); }

    /// <inheritdoc cref="ProviderLabelFormatProperty"/>
    public string ProviderLabelFormat { get => GetValue(ProviderLabelFormatProperty); set => SetValue(ProviderLabelFormatProperty, value); }

    /// <inheritdoc cref="EmptyEmailMessageProperty"/>
    public string EmptyEmailMessage { get => GetValue(EmptyEmailMessageProperty); set => SetValue(EmptyEmailMessageProperty, value); }

    /// <inheritdoc cref="InvalidEmailMessageProperty"/>
    public string InvalidEmailMessage { get => GetValue(InvalidEmailMessageProperty); set => SetValue(InvalidEmailMessageProperty, value); }

    /// <inheritdoc cref="SuggestionFormatProperty"/>
    public string SuggestionFormat { get => GetValue(SuggestionFormatProperty); set => SetValue(SuggestionFormatProperty, value); }

    /// <inheritdoc cref="UseSuggestionLabelFormatProperty"/>
    public string UseSuggestionLabelFormat { get => GetValue(UseSuggestionLabelFormatProperty); set => SetValue(UseSuggestionLabelFormatProperty, value); }

    /// <inheritdoc cref="SendFailedMessageProperty"/>
    public string SendFailedMessage { get => GetValue(SendFailedMessageProperty); set => SetValue(SendFailedMessageProperty, value); }

    /// <inheritdoc cref="CodeTitleProperty"/>
    public string CodeTitle { get => GetValue(CodeTitleProperty); set => SetValue(CodeTitleProperty, value); }

    /// <inheritdoc cref="CodeDescriptionProperty"/>
    public string CodeDescription { get => GetValue(CodeDescriptionProperty); set => SetValue(CodeDescriptionProperty, value); }

    /// <inheritdoc cref="ChangeEmailLabelProperty"/>
    public string ChangeEmailLabel { get => GetValue(ChangeEmailLabelProperty); set => SetValue(ChangeEmailLabelProperty, value); }

    /// <inheritdoc cref="ChangeEmailAccessibleNameProperty"/>
    public string ChangeEmailAccessibleName { get => GetValue(ChangeEmailAccessibleNameProperty); set => SetValue(ChangeEmailAccessibleNameProperty, value); }

    /// <inheritdoc cref="CodeLabelProperty"/>
    public string CodeLabel { get => GetValue(CodeLabelProperty); set => SetValue(CodeLabelProperty, value); }

    /// <inheritdoc cref="CodeHintProperty"/>
    public string? CodeHint { get => GetValue(CodeHintProperty); set => SetValue(CodeHintProperty, value); }

    /// <inheritdoc cref="DemoCodeHintFormatProperty"/>
    public string DemoCodeHintFormat { get => GetValue(DemoCodeHintFormatProperty); set => SetValue(DemoCodeHintFormatProperty, value); }

    /// <inheritdoc cref="VerifyLabelProperty"/>
    public string VerifyLabel { get => GetValue(VerifyLabelProperty); set => SetValue(VerifyLabelProperty, value); }

    /// <inheritdoc cref="IncompleteCodeMessageProperty"/>
    public string IncompleteCodeMessage { get => GetValue(IncompleteCodeMessageProperty); set => SetValue(IncompleteCodeMessageProperty, value); }

    /// <inheritdoc cref="CodeMismatchMessageProperty"/>
    public string CodeMismatchMessage { get => GetValue(CodeMismatchMessageProperty); set => SetValue(CodeMismatchMessageProperty, value); }

    /// <inheritdoc cref="VerifyFailedMessageProperty"/>
    public string VerifyFailedMessage { get => GetValue(VerifyFailedMessageProperty); set => SetValue(VerifyFailedMessageProperty, value); }

    /// <inheritdoc cref="ResendLabelProperty"/>
    public string ResendLabel { get => GetValue(ResendLabelProperty); set => SetValue(ResendLabelProperty, value); }

    /// <inheritdoc cref="ResendCountdownLabelProperty"/>
    public string ResendCountdownLabel { get => GetValue(ResendCountdownLabelProperty); set => SetValue(ResendCountdownLabelProperty, value); }

    /// <inheritdoc cref="ResendAvailableInFormatProperty"/>
    public string ResendAvailableInFormat { get => GetValue(ResendAvailableInFormatProperty); set => SetValue(ResendAvailableInFormatProperty, value); }

    /// <inheritdoc cref="WelcomeFormatProperty"/>
    public string WelcomeFormat { get => GetValue(WelcomeFormatProperty); set => SetValue(WelcomeFormatProperty, value); }

    /// <inheritdoc cref="SignedInWithLabelProperty"/>
    public string SignedInWithLabel { get => GetValue(SignedInWithLabelProperty); set => SetValue(SignedInWithLabelProperty, value); }

    /// <inheritdoc cref="DeviceLabelProperty"/>
    public string DeviceLabel { get => GetValue(DeviceLabelProperty); set => SetValue(DeviceLabelProperty, value); }

    /// <inheritdoc cref="DeviceValueProperty"/>
    public string DeviceValue { get => GetValue(DeviceValueProperty); set => SetValue(DeviceValueProperty, value); }

    /// <inheritdoc cref="SignOutLabelProperty"/>
    public string SignOutLabel { get => GetValue(SignOutLabelProperty); set => SetValue(SignOutLabelProperty, value); }

    /// <inheritdoc cref="EmailCodeMethodLabelProperty"/>
    public string EmailCodeMethodLabel { get => GetValue(EmailCodeMethodLabelProperty); set => SetValue(EmailCodeMethodLabelProperty, value); }

    /// <inheritdoc cref="PasskeyMethodLabelProperty"/>
    public string PasskeyMethodLabel { get => GetValue(PasskeyMethodLabelProperty); set => SetValue(PasskeyMethodLabelProperty, value); }

    /// <inheritdoc cref="FallbackNameProperty"/>
    public string FallbackName { get => GetValue(FallbackNameProperty); set => SetValue(FallbackNameProperty, value); }

    /// <inheritdoc cref="SignUpPromptProperty"/>
    public string SignUpPrompt { get => GetValue(SignUpPromptProperty); set => SetValue(SignUpPromptProperty, value); }

    /// <inheritdoc cref="SignUpLabelProperty"/>
    public string SignUpLabel { get => GetValue(SignUpLabelProperty); set => SetValue(SignUpLabelProperty, value); }

    /// <inheritdoc cref="SendingStatusProperty"/>
    public string SendingStatus { get => GetValue(SendingStatusProperty); set => SetValue(SendingStatusProperty, value); }

    /// <inheritdoc cref="CodeSentStatusProperty"/>
    public string CodeSentStatus { get => GetValue(CodeSentStatusProperty); set => SetValue(CodeSentStatusProperty, value); }

    /// <inheritdoc cref="EmailChangedStatusFormatProperty"/>
    public string EmailChangedStatusFormat { get => GetValue(EmailChangedStatusFormatProperty); set => SetValue(EmailChangedStatusFormatProperty, value); }

    /// <inheritdoc cref="CheckingStatusProperty"/>
    public string CheckingStatus { get => GetValue(CheckingStatusProperty); set => SetValue(CheckingStatusProperty, value); }

    /// <inheritdoc cref="MismatchStatusProperty"/>
    public string MismatchStatus { get => GetValue(MismatchStatusProperty); set => SetValue(MismatchStatusProperty, value); }

    /// <inheritdoc cref="NewCodeStatusProperty"/>
    public string NewCodeStatus { get => GetValue(NewCodeStatusProperty); set => SetValue(NewCodeStatusProperty, value); }

    /// <inheritdoc cref="EditEmailStatusProperty"/>
    public string EditEmailStatus { get => GetValue(EditEmailStatusProperty); set => SetValue(EditEmailStatusProperty, value); }

    /// <inheritdoc cref="PasskeyWaitingStatusProperty"/>
    public string PasskeyWaitingStatus { get => GetValue(PasskeyWaitingStatusProperty); set => SetValue(PasskeyWaitingStatusProperty, value); }

    /// <inheritdoc cref="OpeningProviderStatusFormatProperty"/>
    public string OpeningProviderStatusFormat { get => GetValue(OpeningProviderStatusFormatProperty); set => SetValue(OpeningProviderStatusFormatProperty, value); }

    /// <inheritdoc cref="SignedInStatusFormatProperty"/>
    public string SignedInStatusFormat { get => GetValue(SignedInStatusFormatProperty); set => SetValue(SignedInStatusFormatProperty, value); }

    /// <inheritdoc cref="EmailCodeStatusMethodProperty"/>
    public string EmailCodeStatusMethod { get => GetValue(EmailCodeStatusMethodProperty); set => SetValue(EmailCodeStatusMethodProperty, value); }

    /// <inheritdoc cref="SignInFailedStatusFormatProperty"/>
    public string SignInFailedStatusFormat { get => GetValue(SignInFailedStatusFormatProperty); set => SetValue(SignInFailedStatusFormatProperty, value); }

    /// <inheritdoc cref="SignedOutStatusProperty"/>
    public string SignedOutStatus { get => GetValue(SignedOutStatusProperty); set => SetValue(SignedOutStatusProperty, value); }

    /// <inheritdoc cref="SignUpStatusProperty"/>
    public string SignUpStatus { get => GetValue(SignUpStatusProperty); set => SetValue(SignUpStatusProperty, value); }

    static SignIn()
    {
        FocusableProperty.OverrideDefaultValue<SignIn>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<SignIn>(AutomationLandmarkType.Region);
        AutomationProperties.NameProperty.OverrideDefaultValue<SignIn>("Sign in");
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SignInAutomationPeer(this);

    /// <summary>The card is a named region (Arc's <c>section aria-label="Sign in"</c>).</summary>
    private sealed class SignInAutomationPeer(SignIn owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
