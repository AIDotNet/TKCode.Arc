namespace TKCode.Arc;

/// <summary>
/// Every built-in text of <see cref="LoginSplit"/> (English defaults): headings, field labels, buttons, errors, the status
/// line and what screen readers hear. Replace the whole object through <see cref="LoginSplit.Labels"/> to localize or
/// rename; texts with values are functions.
/// </summary>
public sealed class LoginSplitLabels
{
    /// <summary>Accessible name of the whole block, from the product name ("Sign in to Fieldnote").</summary>
    public Func<string, string> Region { get; init; } = product => $"Sign in to {product}";

    /// <summary>Text before the sign-up link in the header.</summary>
    public string NoAccount { get; init; } = "No account?";

    /// <summary>The sign-up link in the header.</summary>
    public string SignUp { get; init; } = "Sign up";

    /// <summary>Heading of the email step.</summary>
    public string EmailTitle { get; init; } = "Good to see you";

    /// <summary>Sentence under the heading of the email step.</summary>
    public string EmailSubtitle { get; init; } = "Pick up where your team left off.";

    /// <summary>Label of the email field.</summary>
    public string EmailLabel { get; init; } = "Email";

    /// <summary>Placeholder of the email field.</summary>
    public string EmailPlaceholder { get; init; } = "you@company.com";

    /// <summary>The email step's submit button.</summary>
    public string Continue { get; init; } = "Continue";

    /// <summary>The word in the divider above the single sign-on buttons.</summary>
    public string Or { get; init; } = "or";

    /// <summary>Accessible name of the group of single sign-on buttons.</summary>
    public string ProvidersGroup { get; init; } = "Single sign-on";

    /// <summary>Accessible name of a single sign-on button, from the provider's name.</summary>
    public Func<string, string> ContinueWith { get; init; } = provider => $"Continue with {provider}";

    /// <summary>Heading of the password step.</summary>
    public string PasswordTitle { get; init; } = "Type your password";

    /// <summary>Accessible name of the account chip that goes back to the email step, from the email.</summary>
    public Func<string, string> ChangeEmail { get; init; } = email => $"{email}, change email";

    /// <summary>Label of the password field.</summary>
    public string PasswordLabel { get; init; } = "Password";

    /// <summary>The reset link button at the end of the password label.</summary>
    public string ForgotPassword { get; init; } = "Forgot password?";

    /// <summary>The reset link button while the link is on its way.</summary>
    public string SendingLink { get; init; } = "Sending…";

    /// <summary>The reset link button once the link went out.</summary>
    public string LinkSent { get; init; } = "Link sent";

    /// <summary>Hint under the password field from the demo password; null hides it. Only shown while no <see cref="LoginSplit.VerifyPasswordAsync"/> is set.</summary>
    public Func<string, string>? DemoPasswordHint { get; init; } = password => $"Demo password: {password}.";

    /// <summary>The reset notice: <c>{0}</c> is replaced by the email, shown in the foreground color.</summary>
    public string ResetSentNotice { get; init; } = "We emailed {0}.";

    /// <summary>The words before the countdown on the resend button.</summary>
    public string ResendIn { get; init; } = "Resend in";

    /// <summary>The resend button once the countdown ran out.</summary>
    public string ResendLink { get; init; } = "Resend link";

    /// <summary>Accessible name of the resend button while it counts down, from the seconds left.</summary>
    public Func<int, string> ResendAvailableIn { get; init; } = seconds =>
        seconds == 1 ? "Resend link, available in 1 second" : $"Resend link, available in {seconds} seconds";

    /// <summary>The password step's submit button.</summary>
    public string SignIn { get; init; } = "Sign in";

    /// <summary>Accessible name of the submit button once the password was accepted.</summary>
    public string PasswordAccepted { get; init; } = "Password accepted";

    /// <summary>Heading while the account opens, from the product name.</summary>
    public Func<string, string> SigningTitle { get; init; } = product => $"Opening {product}…";

    /// <summary>Sentence while the account opens.</summary>
    public string SigningDescription { get; init; } = "Getting your notes and recent projects ready.";

    /// <summary>Heading once signed in, from the first name.</summary>
    public Func<string, string> DoneTitle { get; init; } = first => $"All set, {first}";

    /// <summary>Sentence once signed in, from the method's name (<see cref="PasswordMethod"/> or the provider's name).</summary>
    public Func<string, string> DoneDescription { get; init; } = method => $"Signed in with {method}. Hand off to your app here.";

    /// <summary>The password as a method in sentences ("Signed in with your password").</summary>
    public string PasswordMethod { get; init; } = "your password";

    /// <summary>The button of the signed-in step.</summary>
    public string SignOut { get; init; } = "Sign out";

    /// <summary>The footer link to the terms.</summary>
    public string Terms { get; init; } = "Terms";

    /// <summary>The footer link to the privacy policy.</summary>
    public string Privacy { get; init; } = "Privacy";

    /// <summary>Accessible name of the footer's links.</summary>
    public string Legal { get; init; } = "Legal";

    /// <summary>Error when Continue is pressed with an empty email.</summary>
    public string EmailRequired { get; init; } = "Type your email to continue.";

    /// <summary>Error when the email is not a complete address.</summary>
    public string EmailInvalid { get; init; } = "That doesn't look like a full email address.";

    /// <summary>Error when Sign in is pressed with an empty password.</summary>
    public string PasswordRequired { get; init; } = "Type your password.";

    /// <summary>Error when the password is wrong.</summary>
    public string PasswordWrong { get; init; } = "That password didn't work. Check it, or use Forgot password.";

    /// <summary>Error when a handler failed (it threw).</summary>
    public string SomethingWentWrong { get; init; } = "Something went wrong. Try again.";

    /// <summary>Status while the account is looked up.</summary>
    public string FindingAccount { get; init; } = "Finding your account";

    /// <summary>Status on the password step, from the email.</summary>
    public Func<string, string> PasswordFor { get; init; } = email => $"Password for {email}";

    /// <summary>Status while the password is checked.</summary>
    public string CheckingPassword { get; init; } = "Checking password";

    /// <summary>Status when the password was wrong.</summary>
    public string PasswordIncorrect { get; init; } = "Password didn't match";

    /// <summary>Status when the password was accepted.</summary>
    public string PasswordOk { get; init; } = "Password OK";

    /// <summary>Status while the account opens, from the email.</summary>
    public Func<string, string> SigningInAs { get; init; } = email => $"Signing in as {email}";

    /// <summary>Status once signed in, from the method's name.</summary>
    public Func<string, string> SignedInWith { get; init; } = method => $"Signed in with {method}";

    /// <summary>Status while single sign-on opens, from the provider's name.</summary>
    public Func<string, string> OpeningProvider { get; init; } = provider => $"Opening {provider}";

    /// <summary>Status while the reset link is sent.</summary>
    public string SendingResetLink { get; init; } = "Sending a reset link";

    /// <summary>Status once the reset link went out, from the email.</summary>
    public Func<string, string> ResetLinkSentTo { get; init; } = email => $"Reset link sent to {email}";

    /// <summary>Status after another reset link was asked for.</summary>
    public string ResetLinkResent { get; init; } = "New reset link sent";

    /// <summary>Status when the person goes back to change the email.</summary>
    public string ChangingEmail { get; init; } = "Change your email";

    /// <summary>Status after Sign out.</summary>
    public string SignedOut { get; init; } = "Signed out";

    /// <summary>Status when a link without an address is used: where it would go in a real app.</summary>
    public Func<LoginSplitLink, string> LinkInApp { get; init; } = link => link switch
    {
        LoginSplitLink.SignUp => "In your app, this goes to sign-up.",
        LoginSplitLink.Terms => "In your app, this goes to your terms.",
        _ => "In your app, this goes to your privacy policy."
    };
}
