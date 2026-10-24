// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): the copy of signup-form.tsx.
namespace TKCode.Arc;

/// <summary>
/// The built-in text of a <see cref="SignupForm"/>: Arc's English copy. Replace the whole object (with an object
/// initializer) to localize or reword it; the block rebuilds its text when <see cref="SignupForm.Labels"/> changes.
/// Sentences that carry a value (a provider, a name, a strength word) are functions.
/// </summary>
public sealed class SignupFormLabels
{
    // ---- frame ----------------------------------------------------------------------------------------------------

    /// <summary>The accessible name of the block: "Create an Arc account".</summary>
    public string RegionName { get; init; } = "Create an Arc account";

    // ---- form step ------------------------------------------------------------------------------------------------

    /// <summary>The heading of the form: "Create your account".</summary>
    public string Title { get; init; } = "Create your account";

    /// <summary>The line under the heading: "Start with your work email. You can invite your team after.".</summary>
    public string Description { get; init; } = "Start with your work email. You can invite your team after.";

    /// <summary>The name of the group of provider buttons: "Sign up with".</summary>
    public string ProvidersName { get; init; } = "Sign up with";

    /// <summary>The accessible name of a provider button: provider name ⇒ "Sign up with Google".</summary>
    public Func<string, string> ProviderButtonName { get; init; } = static provider => $"Sign up with {provider}";

    /// <summary>The word in the divider between the providers and the form: "or".</summary>
    public string Divider { get; init; } = "or";

    /// <summary>The label of the name field: "Full name".</summary>
    public string NameLabel { get; init; } = "Full name";

    /// <summary>The placeholder of the name field: "Jasmine Brooks".</summary>
    public string NamePlaceholder { get; init; } = "Jasmine Brooks";

    /// <summary>The label of the email field: "Work email".</summary>
    public string EmailLabel { get; init; } = "Work email";

    /// <summary>The placeholder of the email field: "name@example.com".</summary>
    public string EmailPlaceholder { get; init; } = "name@example.com";

    /// <summary>The label of the password field: "Password".</summary>
    public string PasswordLabel { get; init; } = "Password";

    /// <summary>The hint under an empty password: "Use 8 or more characters.".</summary>
    public string PasswordHint { get; init; } = "Use 8 or more characters.";

    /// <summary>The strength words for scores 1 to 4: "Weak", "Fair", "Good", "Strong" (a typed score of 0 reads as "Weak").</summary>
    public IReadOnlyList<string> StrengthWords { get; init; } = ["Weak", "Fair", "Good", "Strong"];

    /// <summary>The line under a typed password: strength word ⇒ "Weak password".</summary>
    public Func<string, string> StrengthLine { get; init; } = static word => $"{word} password";

    /// <summary>The label of the opt-in: "Email me product updates".</summary>
    public string ProductUpdatesLabel { get; init; } = "Email me product updates";

    /// <summary>The submit button: "Create account".</summary>
    public string Submit { get; init; } = "Create account";

    /// <summary>The agreement line before the first link: "By creating an account you agree to the ".</summary>
    public string LegalLead { get; init; } = "By creating an account you agree to the ";

    /// <summary>The first link of the agreement line: "Terms".</summary>
    public string Terms { get; init; } = "Terms";

    /// <summary>The words between the two links: " and ".</summary>
    public string LegalJoin { get; init; } = " and ";

    /// <summary>The second link of the agreement line: "Privacy policy".</summary>
    public string Privacy { get; init; } = "Privacy policy";

    /// <summary>The end of the agreement line: ".".</summary>
    public string LegalEnd { get; init; } = ".";

    // ---- validation -----------------------------------------------------------------------------------------------

    /// <summary>The error of a name shorter than 2 characters: "Enter your full name.".</summary>
    public string NameError { get; init; } = "Enter your full name.";

    /// <summary>The error of a malformed address: "Enter a full address, like name@example.com.".</summary>
    public string EmailError { get; init; } = "Enter a full address, like name@example.com.";

    /// <summary>The error of a password shorter than 8 characters: "Use at least 8 characters.".</summary>
    public string PasswordError { get; init; } = "Use at least 8 characters.";

    /// <summary>The submit error when the handler fails without a message: "We couldn't create your account. Try again.".</summary>
    public string SubmitFailed { get; init; } = "We couldn't create your account. Try again.";

    // ---- completion step ------------------------------------------------------------------------------------------

    /// <summary>The completion heading: first name ⇒ "Welcome, Jasmine".</summary>
    public Func<string, string> Welcome { get; init; } = static firstName => $"Welcome, {firstName}";

    /// <summary>The first name used when the name is empty: "there".</summary>
    public string NoName { get; init; } = "there";

    /// <summary>The completion line before the address: "Your account is ready. We sent a confirmation to ".</summary>
    public string ConfirmationLead { get; init; } = "Your account is ready. We sent a confirmation to ";

    /// <summary>The completion line after the address: ".".</summary>
    public string ConfirmationEnd { get; init; } = ".";

    /// <summary>The first detail row: "Signed up with".</summary>
    public string SignedUpWith { get; init; } = "Signed up with";

    /// <summary>The second detail row: "Product updates".</summary>
    public string ProductUpdates { get; init; } = "Product updates";

    /// <summary>The value of an enabled opt-in: "On".</summary>
    public string On { get; init; } = "On";

    /// <summary>The value of a disabled opt-in: "Off".</summary>
    public string Off { get; init; } = "Off";

    /// <summary>The name of a sign-in method as shown in the details: method ⇒ "Email", "Google", "Apple", "GitHub".</summary>
    public Func<SignupFormMethod, string> MethodName { get; init; } = static method => method switch
    {
        SignupFormMethod.Google => "Google",
        SignupFormMethod.Apple => "Apple",
        SignupFormMethod.GitHub => "GitHub",
        _ => "Email"
    };

    /// <summary>The button back to an empty form: "Start over".</summary>
    public string StartOver { get; init; } = "Start over";

    // ---- footer ---------------------------------------------------------------------------------------------------

    /// <summary>The prompt before the sign-in link: "Have an account?".</summary>
    public string HaveAccount { get; init; } = "Have an account?";

    /// <summary>The sign-in link: "Sign in".</summary>
    public string SignIn { get; init; } = "Sign in";

    // ---- status line (also read by screen readers) ----------------------------------------------------------------

    /// <summary>While the form is sent: "Creating your account".</summary>
    public string StatusCreating { get; init; } = "Creating your account";

    /// <summary>After an account was created: method ⇒ "Account created with your email" / "Account created with Google".</summary>
    public Func<SignupFormMethod, string> StatusCreated { get; init; } = static method => method switch
    {
        SignupFormMethod.Google => "Account created with Google",
        SignupFormMethod.Apple => "Account created with Apple",
        SignupFormMethod.GitHub => "Account created with GitHub",
        _ => "Account created with your email"
    };

    /// <summary>After a failed submit: "Account not created".</summary>
    public string StatusFailed { get; init; } = "Account not created";

    /// <summary>While a provider opens: provider name ⇒ "Opening Google".</summary>
    public Func<string, string> StatusOpening { get; init; } = static provider => $"Opening {provider}";

    /// <summary>After Start over: "Start again with new details".</summary>
    public string StatusStartOver { get; init; } = "Start again with new details";

    /// <summary>When nobody handles the Terms link: "Terms open in your app".</summary>
    public string StatusTerms { get; init; } = "Terms open in your app";

    /// <summary>When nobody handles the Privacy policy link: "Privacy policy opens in your app".</summary>
    public string StatusPrivacy { get; init; } = "Privacy policy opens in your app";

    /// <summary>When nobody handles the Sign in link: "Sign in opens in your app".</summary>
    public string StatusSignIn { get; init; } = "Sign in opens in your app";

    // ---- simulated provider ---------------------------------------------------------------------------------------

    /// <summary>The name a simulated provider fills in when the field is empty: "Jasmine Brooks".</summary>
    public string SampleName { get; init; } = "Jasmine Brooks";

    /// <summary>The address a simulated provider fills in when the field is empty: "jasmine@northwind.studio".</summary>
    public string SampleEmail { get; init; } = "jasmine@northwind.studio";

    internal string StrengthWord(int score)
    {
        var words = StrengthWords;
        if (words.Count == 0)
        {
            return string.Empty;
        }
        return words[Math.Clamp(score, 1, words.Count) - 1];
    }
}
