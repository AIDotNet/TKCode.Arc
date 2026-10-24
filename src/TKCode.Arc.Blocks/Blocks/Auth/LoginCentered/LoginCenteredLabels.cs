// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): the copy of login-centered.tsx.
namespace TKCode.Arc;

/// <summary>
/// The built-in text of a <see cref="LoginCentered"/>: Arc's English copy. Replace the whole object (with an object
/// initializer) to localize or reword it; the block rebuilds its text when <see cref="LoginCentered.Labels"/> changes.
/// Sentences that carry a value (an address, a provider, seconds) are functions.
/// </summary>
public sealed class LoginCenteredLabels
{
    // ---- frame ----------------------------------------------------------------------------------------------------

    /// <summary>The product name beside the mark in the top bar: "Arc".</summary>
    public string BrandName { get; init; } = "Arc";

    /// <summary>The name of the screen while signing in: "Sign in".</summary>
    public string SignInScreenName { get; init; } = "Sign in";

    /// <summary>The name of the screen while creating an account: "Create an account".</summary>
    public string SignUpScreenName { get; init; } = "Create an account";

    /// <summary>The prompt before the mode switch while signing in: "No account?".</summary>
    public string NoAccountPrompt { get; init; } = "No account?";

    /// <summary>The mode switch while signing in: "Sign up".</summary>
    public string SignUpAction { get; init; } = "Sign up";

    /// <summary>The prompt before the mode switch while creating an account: "Have an account?".</summary>
    public string HaveAccountPrompt { get; init; } = "Have an account?";

    /// <summary>The mode switch while creating an account: "Sign in".</summary>
    public string SignInAction { get; init; } = "Sign in";

    /// <summary>The footer link to the terms: "Terms".</summary>
    public string Terms { get; init; } = "Terms";

    /// <summary>The footer link to the privacy policy: "Privacy".</summary>
    public string Privacy { get; init; } = "Privacy";

    /// <summary>The name of the footer's link group: "Legal".</summary>
    public string LegalName { get; init; } = "Legal";

    /// <summary>The status after an in-page legal link: (Terms or Privacy) ⇒ "Terms opens here in your app (demo)".</summary>
    public Func<LoginCenteredLink, string> LegalStatus { get; init; } = static link =>
        $"{(link == LoginCenteredLink.Terms ? "Terms" : "Privacy policy")} opens here in your app (demo)";

    // ---- passkey step -------------------------------------------------------------------------------------------

    /// <summary>The title of the first view: "Sign in to Arc".</summary>
    public string PasskeyTitle { get; init; } = "Sign in to Arc";

    /// <summary>The sentence under it: "Use the passkey saved on this device. No password needed.".</summary>
    public string PasskeyDescription { get; init; } = "Use the passkey saved on this device. No password needed.";

    /// <summary>The passkey button: "Sign in with passkey".</summary>
    public string PasskeyAction { get; init; } = "Sign in with passkey";

    /// <summary>The passkey button while the device is asked: "Waiting for your device".</summary>
    public string PasskeyWaiting { get; init; } = "Waiting for your device";

    /// <summary>The passkey button once accepted: "Passkey verified". Also the status.</summary>
    public string PasskeyVerified { get; init; } = "Passkey verified";

    /// <summary>The second button of the first view: "Use email instead".</summary>
    public string UseEmail { get; init; } = "Use email instead";

    /// <summary>The second button while the device is asked: "Cancel".</summary>
    public string CancelPasskey { get; init; } = "Cancel";

    /// <summary>The word between the buttons and the providers: "or".</summary>
    public string Divider { get; init; } = "or";

    /// <summary>The name of the provider group: "Single sign-on".</summary>
    public string SingleSignOnName { get; init; } = "Single sign-on";

    /// <summary>A provider button's name: provider ⇒ "Continue with Google".</summary>
    public Func<LoginCenteredProvider, string> ContinueWith { get; init; } = static provider => $"Continue with {ProviderText(provider)}";

    // ---- email step ---------------------------------------------------------------------------------------------

    /// <summary>The title of the email view while signing in: "Sign in with email".</summary>
    public string EmailTitle { get; init; } = "Sign in with email";

    /// <summary>The sentence under it: "We will send a 6 digit code to your inbox.".</summary>
    public string EmailDescription { get; init; } = "We will send a 6 digit code to your inbox.";

    /// <summary>The title of the email view while creating an account: "Create your account".</summary>
    public string SignUpTitle { get; init; } = "Create your account";

    /// <summary>The sentence under it: "Enter your work email. We will send a code to confirm it.".</summary>
    public string SignUpDescription { get; init; } = "Enter your work email. We will send a code to confirm it.";

    /// <summary>The field's label while signing in: "Email".</summary>
    public string EmailLabel { get; init; } = "Email";

    /// <summary>The field's label while creating an account: "Work email".</summary>
    public string WorkEmailLabel { get; init; } = "Work email";

    /// <summary>The field's placeholder: "name@example.com".</summary>
    public string EmailPlaceholder { get; init; } = "name@example.com";

    /// <summary>The submit button while signing in: "Send code".</summary>
    public string SendCode { get; init; } = "Send code";

    /// <summary>The submit button while creating an account: "Create account".</summary>
    public string CreateAccount { get; init; } = "Create account";

    /// <summary>The way back to the first view: "Use a passkey instead".</summary>
    public string UsePasskey { get; init; } = "Use a passkey instead";

    /// <summary>The error for an empty field: "Enter your email address.".</summary>
    public string EmptyEmail { get; init; } = "Enter your email address.";

    /// <summary>The error for an incomplete address: "Enter a full address, like name@example.com.".</summary>
    public string InvalidEmail { get; init; } = "Enter a full address, like name@example.com.";

    /// <summary>The error when sending the code failed (a <see cref="LoginCentered.SendCodeAsync"/> threw; Arc's preview never fails).</summary>
    public string SendFailed { get; init; } = "We couldn't send a code. Try again.";

    // ---- code step ----------------------------------------------------------------------------------------------

    /// <summary>The title of the code view: "Check your email".</summary>
    public string CodeTitle { get; init; } = "Check your email";

    /// <summary>The words before the address: "Enter the code sent to".</summary>
    public string CodeSentTo { get; init; } = "Enter the code sent to";

    /// <summary>The inline button after the address: "Change".</summary>
    public string ChangeEmail { get; init; } = "Change";

    /// <summary>The name of the inline button for screen readers: "Change email".</summary>
    public string ChangeEmailName { get; init; } = "Change email";

    /// <summary>The label of the code field: "Verification code".</summary>
    public string CodeLabel { get; init; } = "Verification code";

    /// <summary>
    /// The helper copy under the code field: the simulated code (null when <see cref="LoginCentered.VerifyCodeAsync"/>
    /// checks codes) ⇒ "Demo code: 482913"; null shows no helper copy.
    /// </summary>
    public Func<string?, string?> CodeDescription { get; init; } = static code => code is null ? null : $"Demo code: {code}";

    /// <summary>The submit button while signing in: "Verify".</summary>
    public string Verify { get; init; } = "Verify";

    /// <summary>The submit button while creating an account: "Verify and create account".</summary>
    public string VerifyAndCreate { get; init; } = "Verify and create account";

    /// <summary>The submit button once the code is accepted: "Verified".</summary>
    public string Verified { get; init; } = "Verified";

    /// <summary>The error for a short code: "Enter all 6 digits.".</summary>
    public string IncompleteCode { get; init; } = "Enter all 6 digits.";

    /// <summary>The error for a wrong code: "That code didn't match. Check the latest email and try again.".</summary>
    public string CodeMismatch { get; init; } = "That code didn't match. Check the latest email and try again.";

    /// <summary>The error when checking failed (a <see cref="LoginCentered.VerifyCodeAsync"/> threw; Arc's preview never fails).</summary>
    public string VerifyFailed { get; init; } = "We couldn't check the code. Try again.";

    /// <summary>The resend button while it waits, before the rolling time: "Resend code in".</summary>
    public string ResendIn { get; init; } = "Resend code in";

    /// <summary>The resend button once available: "Resend code".</summary>
    public string Resend { get; init; } = "Resend code";

    /// <summary>The resend button's name while it waits: seconds ⇒ "Resend code, available in 29 seconds".</summary>
    public Func<int, string> ResendInName { get; init; } = static seconds => $"Resend code, available in {seconds} seconds";

    // ---- done step ----------------------------------------------------------------------------------------------

    /// <summary>The title after signing in: "Welcome back".</summary>
    public string WelcomeBack { get; init; } = "Welcome back";

    /// <summary>The title after creating an account: "Welcome to Arc".</summary>
    public string WelcomeNew { get; init; } = "Welcome to Arc";

    /// <summary>The words before the address after signing in: "You are signed in as".</summary>
    public string SignedInAs { get; init; } = "You are signed in as";

    /// <summary>The words before the address after creating an account: "Your account is ready.".</summary>
    public string AccountReady { get; init; } = "Your account is ready.";

    /// <summary>The first detail's term: "Method".</summary>
    public string MethodTerm { get; init; } = "Method";

    /// <summary>The second detail's term: "This device".</summary>
    public string DeviceTerm { get; init; } = "This device";

    /// <summary>The second detail's value: "Remembered for 30 days".</summary>
    public string DeviceValue { get; init; } = "Remembered for 30 days";

    /// <summary>The method as the first detail shows it: Passkey ⇒ "Passkey", EmailCode ⇒ "Email code", Google ⇒ "Google".</summary>
    public Func<LoginCenteredMethod, string> MethodName { get; init; } = static method => method switch
    {
        LoginCenteredMethod.Passkey => "Passkey",
        LoginCenteredMethod.EmailCode => "Email code",
        LoginCenteredMethod.Google => "Google",
        LoginCenteredMethod.Apple => "Apple",
        _ => "GitHub"
    };

    /// <summary>The button of the last view: "Sign out".</summary>
    public string SignOut { get; init; } = "Sign out";

    // ---- status line (also announced) ---------------------------------------------------------------------------

    /// <summary>"Waiting for your passkey".</summary>
    public string StatusWaiting { get; init; } = "Waiting for your passkey";

    /// <summary>"Passkey request canceled".</summary>
    public string StatusPasskeyCanceled { get; init; } = "Passkey request canceled";

    /// <summary>When a <see cref="LoginCentered.PasskeyAsync"/> threw (Arc's preview never fails): "Your passkey could not be used".</summary>
    public string StatusPasskeyFailed { get; init; } = "Your passkey could not be used";

    /// <summary>"We will email you a sign in code".</summary>
    public string StatusUseEmail { get; init; } = "We will email you a sign in code";

    /// <summary>A provider was pressed: provider ⇒ "Opening Google".</summary>
    public Func<LoginCenteredProvider, string> StatusOpening { get; init; } = static provider => $"Opening {ProviderText(provider)}";

    /// <summary>When a <see cref="LoginCentered.SignInWithProviderAsync"/> threw (Arc's preview never fails): provider ⇒ "Google sign in didn't finish".</summary>
    public Func<LoginCenteredProvider, string> StatusProviderFailed { get; init; } = static provider => $"{ProviderText(provider)} sign in didn't finish";

    /// <summary>"Sending a code".</summary>
    public string StatusSending { get; init; } = "Sending a code";

    /// <summary>The code was sent: address ⇒ "Code sent to maya@northwind.studio".</summary>
    public Func<string, string> StatusCodeSent { get; init; } = static address => $"Code sent to {address}";

    /// <summary>"Checking code".</summary>
    public string StatusChecking { get; init; } = "Checking code";

    /// <summary>"Code didn't match".</summary>
    public string StatusMismatch { get; init; } = "Code didn't match";

    /// <summary>"Code verified".</summary>
    public string StatusVerified { get; init; } = "Code verified";

    /// <summary>"New code sent".</summary>
    public string StatusNewCode { get; init; } = "New code sent";

    /// <summary>"Edit your email".</summary>
    public string StatusEditEmail { get; init; } = "Edit your email";

    /// <summary>"Sign in with your passkey".</summary>
    public string StatusUsePasskey { get; init; } = "Sign in with your passkey";

    /// <summary>Switched back to signing in: "Sign in to your account".</summary>
    public string StatusSignIn { get; init; } = "Sign in to your account";

    /// <summary>Switched to creating an account: "Create an account with your email".</summary>
    public string StatusSignUp { get; init; } = "Create an account with your email";

    /// <summary>"Signed out".</summary>
    public string StatusSignedOut { get; init; } = "Signed out";

    /// <summary>
    /// The status of the last view: (mode, method) ⇒ "Signed in with a passkey", "Signed in with an email code",
    /// "Account created with Google".
    /// </summary>
    public Func<LoginCenteredMode, LoginCenteredMethod, string> StatusComplete { get; init; } = static (mode, method) =>
        (mode == LoginCenteredMode.SignUp ? "Account created" : "Signed in") + " with " + MethodPhrase(method);

    /// <summary>The visible name of a provider: "Google", "Apple" or "GitHub".</summary>
    public Func<LoginCenteredProvider, string> ProviderName { get; init; } = ProviderText;

    private static string MethodPhrase(LoginCenteredMethod method) => method switch
    {
        LoginCenteredMethod.EmailCode => "an email code",
        LoginCenteredMethod.Passkey => "a passkey",
        LoginCenteredMethod.Google => "Google",
        LoginCenteredMethod.Apple => "Apple",
        _ => "GitHub"
    };

    private static string ProviderText(LoginCenteredProvider provider) => provider switch
    {
        LoginCenteredProvider.Google => "Google",
        LoginCenteredProvider.Apple => "Apple",
        _ => "GitHub"
    };
}
