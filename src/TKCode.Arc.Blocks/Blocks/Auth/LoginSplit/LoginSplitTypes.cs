using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a <see cref="LoginSplit"/> is in its sign-in flow.</summary>
public enum LoginSplitStep
{
    /// <summary>The email step: email field, Continue and the single sign-on buttons.</summary>
    Email,

    /// <summary>The password step: the account chip, the password field, Forgot password? and Sign in.</summary>
    Password,

    /// <summary>The account is being opened: the account row and a progress line.</summary>
    Signing,

    /// <summary>Signed in: a greeting, how the person signed in and Sign out.</summary>
    Done
}

/// <summary>How the person signed in.</summary>
public enum LoginSplitMethod
{
    /// <summary>With the account's password.</summary>
    Password,

    /// <summary>With a single sign-on provider (see <see cref="LoginSplitSignedInEventArgs.Provider"/>).</summary>
    Provider
}

/// <summary>The links of a <see cref="LoginSplit"/>.</summary>
public enum LoginSplitLink
{
    /// <summary>"Sign up" in the header.</summary>
    SignUp,

    /// <summary>"Terms" in the footer.</summary>
    Terms,

    /// <summary>"Privacy" in the footer.</summary>
    Privacy
}

/// <summary>The built-in mark a <see cref="LoginSplitProvider"/> button shows before its name.</summary>
public enum LoginSplitProviderMark
{
    /// <summary>No built-in mark (use <see cref="LoginSplitProvider.Icon"/>, or show the name alone).</summary>
    None,

    /// <summary>The four-color Google "G".</summary>
    Google,

    /// <summary>The Apple mark, in the button's text color.</summary>
    Apple,

    /// <summary>The GitHub mark, in the button's text color.</summary>
    GitHub
}

/// <summary>
/// A person who can sign in to a <see cref="LoginSplit"/>: a name, an email and an optional picture. The chip, the account
/// row and the greeting show it; without a picture the avatar shows the initials of the name.
/// </summary>
public sealed class LoginSplitAccount
{
    /// <summary>Creates an empty account (set <see cref="Name"/> and <see cref="Email"/>).</summary>
    public LoginSplitAccount()
    {
    }

    /// <summary>Creates an account with a name and an email.</summary>
    public LoginSplitAccount(string name, string email, ImageSource? picture = null)
    {
        Name = name;
        Email = email;
        Picture = picture;
    }

    /// <summary>Full name ("Rowan Hale"); the avatar's initials and the greeting come from it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Email address; known accounts are matched on it without regard to case.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Photo for the avatar; null shows the initials of <see cref="Name"/>.</summary>
    public ImageSource? Picture { get; set; }

    /// <summary>The first word of <see cref="Name"/>, used in the greeting ("All set, Rowan").</summary>
    public string GreetingName => LoginSplitIdentity.FirstWord(Name);

    /// <summary>
    /// An account for an address nobody listed: the part before the @ split at dots, underscores, plus signs and hyphens,
    /// each piece capitalized ("ada.lovelace@example.com" becomes "Ada Lovelace"). The email is trimmed and lower-cased.
    /// </summary>
    public static LoginSplitAccount FromEmail(string email)
    {
        var normalized = LoginSplitIdentity.Normalize(email);
        return new LoginSplitAccount(LoginSplitIdentity.NameFromEmail(normalized), normalized);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Name} <{Email}>";
}

/// <summary>A single sign-on button of a <see cref="LoginSplit"/>: a name and a mark.</summary>
public sealed class LoginSplitProvider
{
    /// <summary>Creates an empty provider (set <see cref="Name"/>).</summary>
    public LoginSplitProvider()
    {
    }

    /// <summary>Creates a provider with a name and a built-in mark.</summary>
    public LoginSplitProvider(string name, LoginSplitProviderMark mark = LoginSplitProviderMark.None)
    {
        Name = name;
        Mark = mark;
    }

    /// <summary>The provider's name: the button's text and part of its accessible name ("Continue with Google").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The built-in mark shown before the name.</summary>
    public LoginSplitProviderMark Mark { get; set; }

    /// <summary>A custom mark (24 × 24 view box, filled in the button's text color); wins over <see cref="Mark"/>.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>Google, with its four-color mark.</summary>
    public static LoginSplitProvider Google() => new("Google", LoginSplitProviderMark.Google);

    /// <summary>Apple, with its mark.</summary>
    public static LoginSplitProvider Apple() => new("Apple", LoginSplitProviderMark.Apple);

    /// <summary>GitHub, with its mark.</summary>
    public static LoginSplitProvider GitHub() => new("GitHub", LoginSplitProviderMark.GitHub);

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// The pauses of the simulated flow a <see cref="LoginSplit"/> plays when no handler is set, and the beats of the
/// sign-in sequence. Defaults are the reference preview's measured timings.
/// </summary>
public sealed class LoginSplitTimings
{
    /// <summary>Simulated account lookup after Continue (650 ms).</summary>
    public TimeSpan Lookup { get; set; } = TimeSpan.FromMilliseconds(650);

    /// <summary>Simulated password check after Sign in (800 ms).</summary>
    public TimeSpan PasswordCheck { get; set; } = TimeSpan.FromMilliseconds(800);

    /// <summary>How long the accepted check stays before the account opens (775 ms).</summary>
    public TimeSpan AcceptedHold { get; set; } = TimeSpan.FromMilliseconds(775);

    /// <summary>How long the account takes to open before the greeting (1.7 s).</summary>
    public TimeSpan Signing { get; set; } = TimeSpan.FromMilliseconds(1700);

    /// <summary>The same under reduced motion (400 ms; every other pause stays).</summary>
    public TimeSpan SigningReduced { get; set; } = TimeSpan.FromMilliseconds(400);

    /// <summary>Simulated single sign-on round trip (1 s).</summary>
    public TimeSpan Provider { get; set; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>Simulated sending of a reset link (900 ms).</summary>
    public TimeSpan ResetSend { get; set; } = TimeSpan.FromMilliseconds(900);

    /// <summary>When the progress line starts after the account step appears (260 ms).</summary>
    public TimeSpan ProgressDelay { get; set; } = TimeSpan.FromMilliseconds(260);

    /// <summary>How long the progress line takes to fill (1.4 s, in-out curve).</summary>
    public TimeSpan ProgressDuration { get; set; } = TimeSpan.FromMilliseconds(1400);
}

/// <summary>Arguments of <see cref="LoginSplit.SignedInEvent"/>.</summary>
public sealed class LoginSplitSignedInEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginSplitSignedInEventArgs(RoutedEvent routedEvent, LoginSplitAccount account, LoginSplitMethod method, LoginSplitProvider? provider)
        : base(routedEvent)
    {
        Account = account;
        Method = method;
        Provider = provider;
    }

    /// <summary>The account that signed in.</summary>
    public LoginSplitAccount Account { get; }

    /// <summary>Password or single sign-on.</summary>
    public LoginSplitMethod Method { get; }

    /// <summary>The single sign-on provider; null for a password.</summary>
    public LoginSplitProvider? Provider { get; }
}

/// <summary>Arguments of <see cref="LoginSplit.SignedOutEvent"/>.</summary>
public sealed class LoginSplitSignedOutEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginSplitSignedOutEventArgs(RoutedEvent routedEvent, LoginSplitAccount account) : base(routedEvent)
    {
        Account = account;
    }

    /// <summary>The account that signed out.</summary>
    public LoginSplitAccount Account { get; }
}

/// <summary>Arguments of <see cref="LoginSplit.StepChangedEvent"/>.</summary>
public sealed class LoginSplitStepChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginSplitStepChangedEventArgs(RoutedEvent routedEvent, LoginSplitStep oldStep, LoginSplitStep newStep) : base(routedEvent)
    {
        OldStep = oldStep;
        NewStep = newStep;
    }

    /// <summary>The step before the change.</summary>
    public LoginSplitStep OldStep { get; }

    /// <summary>The step now shown.</summary>
    public LoginSplitStep NewStep { get; }
}

/// <summary>Arguments of <see cref="LoginSplit.NavigateEvent"/>. Set <c>Handled</c> to keep the block from opening <see cref="Href"/>.</summary>
public sealed class LoginSplitNavigateEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginSplitNavigateEventArgs(RoutedEvent routedEvent, LoginSplitLink link, string? href) : base(routedEvent)
    {
        Link = link;
        Href = href;
    }

    /// <summary>The link that was activated.</summary>
    public LoginSplitLink Link { get; }

    /// <summary>The link's address (<see cref="LoginSplit.SignUpHref"/>, <see cref="LoginSplit.TermsHref"/> or <see cref="LoginSplit.PrivacyHref"/>); null when none is set.</summary>
    public string? Href { get; }
}

/// <summary>Arguments of <see cref="LoginSplit.ResetLinkSentEvent"/>.</summary>
public sealed class LoginSplitResetLinkSentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginSplitResetLinkSentEventArgs(RoutedEvent routedEvent, LoginSplitAccount account, bool isResend) : base(routedEvent)
    {
        Account = account;
        IsResend = isResend;
    }

    /// <summary>The account the link went to.</summary>
    public LoginSplitAccount Account { get; }

    /// <summary>True when the person asked for another link after the countdown.</summary>
    public bool IsResend { get; }
}
