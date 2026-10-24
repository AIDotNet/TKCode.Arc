// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): the types of login-centered.tsx.
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The card's view in a <see cref="LoginCentered"/> (Arc's <c>Step</c>).</summary>
public enum LoginCenteredStep
{
    /// <summary>The first view: sign in with a passkey, switch to email, or continue with a provider.</summary>
    Passkey,

    /// <summary>The email form (sign in, or the first step of sign up).</summary>
    Email,

    /// <summary>The six digit code sent to the address.</summary>
    Code,

    /// <summary>Signed in (or account created): the account, the method and Sign out.</summary>
    Done
}

/// <summary>Whether a <see cref="LoginCentered"/> signs a person in or creates an account (Arc's <c>Mode</c>).</summary>
public enum LoginCenteredMode
{
    /// <summary>Sign in to an existing account (default).</summary>
    SignIn,

    /// <summary>Create an account with a work email and a code.</summary>
    SignUp
}

/// <summary>How a person signed in (Arc's <c>LoginMethod</c>).</summary>
public enum LoginCenteredMethod
{
    /// <summary>The passkey saved on the device.</summary>
    Passkey,

    /// <summary>A six digit code sent by email.</summary>
    EmailCode,

    /// <summary>Single sign-on with Google.</summary>
    Google,

    /// <summary>Single sign-on with Apple.</summary>
    Apple,

    /// <summary>Single sign-on with GitHub.</summary>
    GitHub
}

/// <summary>A single sign-on provider of a <see cref="LoginCentered"/> (Arc's <c>Provider</c>).</summary>
public enum LoginCenteredProvider
{
    /// <summary>Google (drawn with its four-colour mark).</summary>
    Google,

    /// <summary>Apple (monochrome mark).</summary>
    Apple,

    /// <summary>GitHub (monochrome mark).</summary>
    GitHub
}

/// <summary>The passkey request of a <see cref="LoginCentered"/> (Arc's <c>PasskeyPhase</c>).</summary>
public enum LoginCenteredPasskeyPhase
{
    /// <summary>Nothing asked yet.</summary>
    Idle,

    /// <summary>The device is being asked; the button shows two pulse rings and Cancel replaces "Use email instead".</summary>
    Waiting,

    /// <summary>The passkey was accepted; the card moves to the signed-in view a moment later.</summary>
    Verified
}

/// <summary>One of the footer links of a <see cref="LoginCentered"/>.</summary>
public enum LoginCenteredLink
{
    /// <summary>The Terms link.</summary>
    Terms,

    /// <summary>The Privacy link.</summary>
    Privacy
}

/// <summary>Arguments of <see cref="LoginCentered.SignedInEvent"/> (Arc's <c>onSignIn(email, method, mode)</c>).</summary>
public sealed class LoginCenteredSignedInEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginCenteredSignedInEventArgs(RoutedEvent routedEvent, string email, LoginCenteredMethod method, LoginCenteredMode mode)
        : base(routedEvent)
    {
        Email = email;
        Method = method;
        Mode = mode;
    }

    /// <summary>The account's address: the one the person typed (trimmed, lower case) or the one a passkey or provider resolved to.</summary>
    public string Email { get; }

    /// <summary>How the person signed in.</summary>
    public LoginCenteredMethod Method { get; }

    /// <summary><see cref="LoginCenteredMode.SignUp"/> when an account was created, otherwise <see cref="LoginCenteredMode.SignIn"/>.</summary>
    public LoginCenteredMode Mode { get; }
}

/// <summary>
/// Arguments of <see cref="LoginCentered.NavigateEvent"/>: a footer link was activated. Set <c>Handled</c> to open the
/// destination yourself; otherwise an absolute <c>http</c>, <c>https</c> or <c>mailto</c> address opens with the
/// platform launcher, and an in-page address (<c>#terms</c>, Arc's default) only says in the status line where it would go.
/// </summary>
public sealed class LoginCenteredNavigateEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginCenteredNavigateEventArgs(RoutedEvent routedEvent, LoginCenteredLink link, string? href)
        : base(routedEvent)
    {
        Link = link;
        Href = href;
    }

    /// <summary>Which link was activated.</summary>
    public LoginCenteredLink Link { get; }

    /// <summary>The link's address (<see cref="LoginCentered.TermsHref"/> or <see cref="LoginCentered.PrivacyHref"/>).</summary>
    public string? Href { get; }
}
