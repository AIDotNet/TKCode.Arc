using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The card's step in a <see cref="LoginImmersive"/> page.</summary>
public enum LoginImmersiveStep
{
    /// <summary>The email field, Email me a link and the single sign-on buttons.</summary>
    Form,

    /// <summary>The link is on its way: the envelope, the address with Edit, Open mail app and the resend countdown.</summary>
    Sent,

    /// <summary>Signed in: the success mark, the account and method, and Sign out.</summary>
    Done
}

/// <summary>Whether a <see cref="LoginImmersive"/> page signs a person in or creates an account.</summary>
public enum LoginImmersiveMode
{
    /// <summary>Sign in to an existing account (the default).</summary>
    SignIn,

    /// <summary>Create an account: the heading and the header switch change; the flow is the same.</summary>
    SignUp
}

/// <summary>How the person signed in.</summary>
public enum LoginImmersiveMethod
{
    /// <summary>The emailed one-time link.</summary>
    MagicLink,

    /// <summary>A single sign-on provider (see <see cref="LoginImmersiveSignedInEventArgs.Provider"/>).</summary>
    Provider
}

/// <summary>A destination a <see cref="LoginImmersive"/> page hands to the app.</summary>
public enum LoginImmersiveLink
{
    /// <summary>The terms of service in the footer.</summary>
    Terms,

    /// <summary>The privacy policy in the footer.</summary>
    Privacy,

    /// <summary>The mail app (Open mail app with <see cref="LoginImmersive.MailAppUri"/> set).</summary>
    MailApp
}

/// <summary>Arguments of <see cref="LoginImmersive.SignedIn"/>.</summary>
public sealed class LoginImmersiveSignedInEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginImmersiveSignedInEventArgs(RoutedEvent routedEvent, string email, LoginImmersiveMethod method,
        LoginImmersiveProvider? provider, LoginImmersiveMode mode)
        : base(routedEvent)
    {
        Email = email;
        Method = method;
        Provider = provider;
        Mode = mode;
    }

    /// <summary>The signed-in address (trimmed, lower case).</summary>
    public string Email { get; }

    /// <summary>The emailed link or a single sign-on provider.</summary>
    public LoginImmersiveMethod Method { get; }

    /// <summary>The provider when <see cref="Method"/> is <see cref="LoginImmersiveMethod.Provider"/>; otherwise null.</summary>
    public LoginImmersiveProvider? Provider { get; }

    /// <summary>Sign in or sign up, as the page was when the person finished.</summary>
    public LoginImmersiveMode Mode { get; }
}

/// <summary>Arguments of <see cref="LoginImmersive.LinkSent"/>.</summary>
public sealed class LoginImmersiveLinkSentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginImmersiveLinkSentEventArgs(RoutedEvent routedEvent, string email, bool isResend, LoginImmersiveMode mode)
        : base(routedEvent)
    {
        Email = email;
        IsResend = isResend;
        Mode = mode;
    }

    /// <summary>The address the link went to (trimmed, lower case).</summary>
    public string Email { get; }

    /// <summary>True when the person asked for another link from the Sent step.</summary>
    public bool IsResend { get; }

    /// <summary>Sign in or sign up.</summary>
    public LoginImmersiveMode Mode { get; }
}

/// <summary>
/// Arguments of <see cref="LoginImmersive.Navigate"/>. Set <see cref="RoutedEventArgs.Handled"/> to open the destination
/// yourself; otherwise the page opens <see cref="Href"/> with the platform launcher when it is an absolute web or mail
/// address.
/// </summary>
public sealed class LoginImmersiveNavigateEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginImmersiveNavigateEventArgs(RoutedEvent routedEvent, LoginImmersiveLink link, string? href)
        : base(routedEvent)
    {
        Link = link;
        Href = href;
    }

    /// <summary>Which destination was chosen.</summary>
    public LoginImmersiveLink Link { get; }

    /// <summary>Its address (<see cref="LoginImmersive.TermsHref"/>, <see cref="LoginImmersive.PrivacyHref"/> or <see cref="LoginImmersive.MailAppUri"/>).</summary>
    public string? Href { get; }
}

/// <summary>
/// How long the built-in simulation waits at each beat (the measured beats of the reference preview). Only used for
/// actions whose handler (<see cref="LoginImmersive.SendLinkAsync"/>, <see cref="LoginImmersive.OpenMailAppAsync"/>,
/// <see cref="LoginImmersive.SignInWithProviderAsync"/>) is not set, except <see cref="LinkSentHold"/>, which always
/// applies.
/// </summary>
public sealed class LoginImmersiveTimings
{
    /// <summary>Sending the link (900 ms).</summary>
    public TimeSpan SendLink { get; init; } = TimeSpan.FromMilliseconds(900);

    /// <summary>How long the button says Link sent before the Sent step shows (625 ms).</summary>
    public TimeSpan LinkSentHold { get; init; } = TimeSpan.FromMilliseconds(625);

    /// <summary>Open mail app in the demo, until the link counts as followed (1000 ms).</summary>
    public TimeSpan OpenMailApp { get; init; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>A single sign-on provider answering (900 ms).</summary>
    public TimeSpan Provider { get; init; } = TimeSpan.FromMilliseconds(900);
}
