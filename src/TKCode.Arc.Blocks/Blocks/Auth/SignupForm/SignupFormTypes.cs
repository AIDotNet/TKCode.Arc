// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): the types of signup-form.tsx.
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The view a <see cref="SignupForm"/> shows (Arc's <c>Step</c>).</summary>
public enum SignupFormStep
{
    /// <summary>The account form: single sign-on, name, email, password, product updates.</summary>
    Form,

    /// <summary>The completion panel: the drawn check, the welcome, how the account was made, Start over.</summary>
    Done
}

/// <summary>How an account was created (Arc's <c>"Email" | Provider</c>).</summary>
public enum SignupFormMethod
{
    /// <summary>The email and password form.</summary>
    Email,

    /// <summary>Single sign-on with Google.</summary>
    Google,

    /// <summary>Single sign-on with Apple.</summary>
    Apple,

    /// <summary>Single sign-on with GitHub.</summary>
    GitHub
}

/// <summary>One of the text links of a <see cref="SignupForm"/>.</summary>
public enum SignupFormLink
{
    /// <summary>"Terms" in the agreement line.</summary>
    Terms,

    /// <summary>"Privacy policy" in the agreement line.</summary>
    Privacy,

    /// <summary>"Sign in" in the footer.</summary>
    SignIn
}

/// <summary>The details of a new account (Arc's <c>SignupDetails</c>), passed to <see cref="SignupForm.SubmitAsync"/>.</summary>
public sealed class SignupFormDetails
{
    /// <summary>Creates the details.</summary>
    public SignupFormDetails(string name, string email, string password, bool productUpdates)
    {
        Name = name;
        Email = email;
        Password = password;
        ProductUpdates = productUpdates;
    }

    /// <summary>The full name, trimmed (at least 2 characters).</summary>
    public string Name { get; }

    /// <summary>The address, trimmed; it passed the basic format check.</summary>
    public string Email { get; }

    /// <summary>The password as typed (8 or more characters).</summary>
    public string Password { get; }

    /// <summary>Whether "Email me product updates" was checked.</summary>
    public bool ProductUpdates { get; }
}

/// <summary>Arguments of <see cref="SignupForm.AccountCreatedEvent"/>: the completion panel is about to show.</summary>
public sealed class SignupFormCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SignupFormCompletedEventArgs(RoutedEvent routedEvent, SignupFormMethod method, string name, string email, bool productUpdates)
        : base(routedEvent)
    {
        Method = method;
        Name = name;
        Email = email;
        ProductUpdates = productUpdates;
    }

    /// <summary>How the account was created.</summary>
    public SignupFormMethod Method { get; }

    /// <summary>The full name, trimmed.</summary>
    public string Name { get; }

    /// <summary>The address, trimmed.</summary>
    public string Email { get; }

    /// <summary>Whether product updates were requested.</summary>
    public bool ProductUpdates { get; }
}

/// <summary>Arguments of <see cref="SignupForm.ProviderSelectedEvent"/> (Arc's <c>onProvider</c>).</summary>
public sealed class SignupFormProviderEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SignupFormProviderEventArgs(RoutedEvent routedEvent, SignupFormMethod provider)
        : base(routedEvent)
    {
        Provider = provider;
    }

    /// <summary>The provider whose button was pressed (never <see cref="SignupFormMethod.Email"/>).</summary>
    public SignupFormMethod Provider { get; }
}

/// <summary>
/// Arguments of <see cref="SignupForm.NavigateEvent"/>: a text link was activated. Set <c>Handled</c> when you open the
/// destination; otherwise the footer status says where the link would go, as in Arc's preview.
/// </summary>
public sealed class SignupFormNavigateEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SignupFormNavigateEventArgs(RoutedEvent routedEvent, SignupFormLink link)
        : base(routedEvent)
    {
        Link = link;
    }

    /// <summary>Which link was activated.</summary>
    public SignupFormLink Link { get; }
}
