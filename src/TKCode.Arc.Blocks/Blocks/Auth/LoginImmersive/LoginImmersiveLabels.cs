namespace TKCode.Arc;

/// <summary>
/// Every text a <see cref="LoginImmersive"/> page shows or announces, with English defaults. Sentences that carry a value
/// (the product, an address, a provider, a count of seconds) are functions.
/// </summary>
public sealed class LoginImmersiveLabels
{
    /// <summary>The heading of the form when signing in; the argument is the product name.</summary>
    public Func<string, string> SignInTitle { get; init; } = product => $"Sign in to {product}";

    /// <summary>The sentence under the sign-in heading.</summary>
    public string SignInDescription { get; init; } = "We'll email you a one-time link that signs you in. There's no password to remember.";

    /// <summary>The heading of the form when creating an account; the argument is the product name.</summary>
    public Func<string, string> SignUpTitle { get; init; } = product => $"Start your {product}";

    /// <summary>The sentence under the sign-up heading.</summary>
    public string SignUpDescription { get; init; } = "Tell us your work email and confirm it from your inbox. It takes a minute.";

    /// <summary>The email field's label.</summary>
    public string EmailLabel { get; init; } = "Email";

    /// <summary>The email field's placeholder.</summary>
    public string EmailPlaceholder { get; init; } = "name@example.com";

    /// <summary>The main button of the form.</summary>
    public string SendLink { get; init; } = "Email me a link";

    /// <summary>The main button once the link went out (shown with a check until the Sent step).</summary>
    public string LinkSent { get; init; } = "Link sent";

    /// <summary>The word between the form and the single sign-on buttons.</summary>
    public string Divider { get; init; } = "or";

    /// <summary>The accessible name of the row of single sign-on buttons.</summary>
    public string ProvidersGroupName { get; init; } = "Continue with another account";

    /// <summary>The accessible name of a single sign-on button; the argument is the provider's name.</summary>
    public Func<string, string> ProviderButtonName { get; init; } = provider => $"Continue with {provider}";

    /// <summary>The error of an empty email field.</summary>
    public string EmailRequired { get; init; } = "Type your email to continue.";

    /// <summary>The error of an address that is not complete.</summary>
    public string EmailIncomplete { get; init; } = "That doesn't look like a full email address.";

    /// <summary>The error when <see cref="LoginImmersive.SendLinkAsync"/> fails.</summary>
    public string SendFailed { get; init; } = "The link didn't go out. Try again in a moment.";

    /// <summary>The heading of the Sent step.</summary>
    public string SentTitle { get; init; } = "Look in your inbox";

    /// <summary>The sentence under the Sent heading.</summary>
    public string SentDescription { get; init; } = "The link works on this device only, and it expires after 15 minutes.";

    /// <summary>The button that goes back to change the address.</summary>
    public string EditEmail { get; init; } = "Edit";

    /// <summary>The accessible name of <see cref="EditEmail"/>; the argument is the address.</summary>
    public Func<string, string> EditEmailName { get; init; } = email => $"Edit the address {email}";

    /// <summary>The main button of the Sent step.</summary>
    public string OpenMailApp { get; init; } = "Open mail app";

    /// <summary>The words before the resend button.</summary>
    public string ResendPrompt { get; init; } = "Didn't get it?";

    /// <summary>The resend button while it waits, before the time (m:ss).</summary>
    public string ResendCountdown { get; init; } = "Resend in";

    /// <summary>The resend button once another link can be sent.</summary>
    public string ResendReady { get; init; } = "Send again";

    /// <summary>The accessible name of the waiting resend button; the argument is the seconds left.</summary>
    public Func<int, string> ResendWaitingName { get; init; } =
        seconds => seconds == 1 ? "Send again, available in 1 second" : $"Send again, available in {seconds} seconds";

    /// <summary>The footer note on the Sent step while Open mail app is simulated.</summary>
    public string DemoNote { get; init; } = "Demo: Open mail app signs you in as if you had clicked the link.";

    /// <summary>The heading of the Done step after signing in.</summary>
    public string DoneTitle { get; init; } = "Signed in";

    /// <summary>The heading of the Done step after creating an account.</summary>
    public string DoneTitleSignUp { get; init; } = "Account ready";

    /// <summary>The sentence of the Done step after the emailed link.</summary>
    public string DoneDescriptionLink { get; init; } = "Link accepted. This device remembers you for 30 days.";

    /// <summary>The sentence of the Done step after a provider; the argument is the provider's name.</summary>
    public Func<string, string> DoneDescriptionProvider { get; init; } =
        provider => $"{provider} vouched for you. This device remembers you for 30 days.";

    /// <summary>The first detail row's label (the address).</summary>
    public string DetailsAccount { get; init; } = "Account";

    /// <summary>The second detail row's label (how the person signed in).</summary>
    public string DetailsMethod { get; init; } = "Method";

    /// <summary>The method shown for the emailed link.</summary>
    public string MethodMagicLink { get; init; } = "Magic link";

    /// <summary>The button of the Done step.</summary>
    public string SignOut { get; init; } = "Sign out";

    /// <summary>The header's question while signing in.</summary>
    public string SignUpPrompt { get; init; } = "No account?";

    /// <summary>The header's action while signing in.</summary>
    public string SignUpAction { get; init; } = "Sign up";

    /// <summary>The header's question while creating an account.</summary>
    public string SignInPrompt { get; init; } = "Have an account?";

    /// <summary>The header's action while creating an account.</summary>
    public string SignInAction { get; init; } = "Sign in";

    /// <summary>The footer's terms link.</summary>
    public string Terms { get; init; } = "Terms";

    /// <summary>The footer's privacy link.</summary>
    public string Privacy { get; init; } = "Privacy";

    /// <summary>The accessible name of the footer's links.</summary>
    public string LegalGroupName { get; init; } = "Legal";

    /// <summary>The terms' name in <see cref="DocumentNote"/>.</summary>
    public string TermsDocument { get; init; } = "terms of service";

    /// <summary>The privacy policy's name in <see cref="DocumentNote"/>.</summary>
    public string PrivacyDocument { get; init; } = "privacy policy";

    /// <summary>The footer note of a terms or privacy link without an address; the argument is the document's name.</summary>
    public Func<string, string> DocumentNote { get; init; } = document => $"In your app, this opens the {document}.";

    /// <summary>Announced while the link is being sent.</summary>
    public string SendingStatus { get; init; } = "Sending your link";

    /// <summary>Announced when the link went out; the argument is the address.</summary>
    public Func<string, string> SentStatus { get; init; } = email => $"Link sent to {email}";

    /// <summary>Announced when another link went out.</summary>
    public string ResentStatus { get; init; } = "New link sent";

    /// <summary>Announced while the mail app opens.</summary>
    public string OpeningMailStatus { get; init; } = "Opening your mail app";

    /// <summary>Announced when the emailed link signed the person in.</summary>
    public string SignedInLinkStatus { get; init; } = "Signed in with your link";

    /// <summary>Announced while a provider opens; the argument is its name.</summary>
    public Func<string, string> OpeningProviderStatus { get; init; } = provider => $"Opening {provider}";

    /// <summary>Announced when a provider signed the person in; the argument is its name.</summary>
    public Func<string, string> SignedInProviderStatus { get; init; } = provider => $"Signed in with {provider}";

    /// <summary>Announced when Edit returns to the form.</summary>
    public string EditStatus { get; init; } = "Change your email";

    /// <summary>Announced after Sign out.</summary>
    public string SignedOutStatus { get; init; } = "Signed out";

    /// <summary>Announced when the header switches to creating an account.</summary>
    public string SignUpStatus { get; init; } = "Create an account";

    /// <summary>Announced when the header switches back to signing in.</summary>
    public string SignInStatus { get; init; } = "Sign in";
}
