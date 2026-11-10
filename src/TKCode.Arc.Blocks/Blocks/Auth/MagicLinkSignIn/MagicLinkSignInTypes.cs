using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What a <see cref="MagicLinkSignIn"/> shows.</summary>
public enum MagicLinkSignInState
{
    /// <summary>The email field and the send button.</summary>
    Email,

    /// <summary>"Check your inbox", with the address, a resend countdown and a way back to the field.</summary>
    Sent,

    /// <summary>The link the person opened has expired; one button sends a new one.</summary>
    Expired
}

/// <summary>What a <see cref="MagicLinkSignIn.LinkSender"/> sends: the address and whether it is a repeat.</summary>
/// <param name="Email">The trimmed address.</param>
/// <param name="IsResend">True for "Resend link" and for a new link after one expired.</param>
public sealed record MagicLinkSignInRequest(string Email, bool IsResend);

/// <summary>Arguments of <see cref="MagicLinkSignIn.LinkRequested"/>: email a sign-in link to <see cref="Email"/>.</summary>
public sealed class MagicLinkSignInLinkEventArgs : RoutedEventArgs
{
    public MagicLinkSignInLinkEventArgs(RoutedEvent routedEvent, string email, bool isResend) : base(routedEvent)
    {
        Email = email;
        IsResend = isResend;
    }

    /// <summary>The trimmed address.</summary>
    public string Email { get; }

    /// <summary>True for "Resend link" and for a new link after one expired.</summary>
    public bool IsResend { get; }
}

/// <summary>Arguments of <see cref="MagicLinkSignIn.StateChanged"/>.</summary>
public sealed class MagicLinkSignInStateChangedEventArgs : RoutedEventArgs
{
    public MagicLinkSignInStateChangedEventArgs(RoutedEvent routedEvent, MagicLinkSignInState oldState, MagicLinkSignInState newState) : base(routedEvent)
    {
        OldState = oldState;
        NewState = newState;
    }

    /// <summary>The state that was shown.</summary>
    public MagicLinkSignInState OldState { get; }

    /// <summary>The state now shown.</summary>
    public MagicLinkSignInState NewState { get; }
}

/// <summary>
/// The built-in text of a <see cref="MagicLinkSignIn"/>. Replace the whole object (with an object initializer) to
/// localize or reword it; the card rebuilds its text when <see cref="MagicLinkSignIn.Labels"/> changes.
/// </summary>
public sealed class MagicLinkSignInLabels
{
    /// <summary>The title of the email state: product name ⇒ "Sign in to Lumenfield".</summary>
    public Func<string, string> Title { get; init; } = static product => $"Sign in to {product}";

    /// <summary>The sentence under the title.</summary>
    public string Description { get; init; } = "We'll email you a link that signs you in. No password to remember.";

    /// <summary>The email field's label.</summary>
    public string EmailLabel { get; init; } = "Work email";

    /// <summary>The email field's placeholder.</summary>
    public string EmailPlaceholder { get; init; } = "you@company.com";

    /// <summary>The error under an address that is not complete.</summary>
    public string EmailInvalid { get; init; } = "Enter an email address like name@company.com.";

    /// <summary>The send button.</summary>
    public string Send { get; init; } = "Email me a link";

    /// <summary>The error when the link could not be sent.</summary>
    public string SendFailed { get; init; } = "We couldn't send the link. Check your connection and try again.";

    /// <summary>The note under the send button.</summary>
    public string Note { get; init; } = "Links work once and only in the browser or app that asked for them.";

    /// <summary>The title of the sent state.</summary>
    public string SentTitle { get; init; } = "Check your inbox";

    /// <summary>The sentence of the sent state: (address, minutes) ⇒ "We sent a sign-in link to … It works for 15 minutes.".</summary>
    public Func<string, int, string> SentDescription { get; init; } = static (email, minutes) =>
        $"We sent a sign-in link to {email}. It works for {minutes} minutes.";

    /// <summary>The hint under the sentence.</summary>
    public string SentHint { get; init; } = "Nothing there? Look in spam or promotions, or wait a moment before sending another.";

    /// <summary>The resend button while it can be pressed.</summary>
    public string Resend { get; init; } = "Resend link";

    /// <summary>The resend button while it waits: "m:ss" ⇒ "Resend in 0:24".</summary>
    public Func<string, string> ResendIn { get; init; } = static time => $"Resend in {time}";

    /// <summary>Said after another link is sent.</summary>
    public string Resent { get; init; } = "A new link is on its way.";

    /// <summary>The button that returns to the field.</summary>
    public string ChangeEmail { get; init; } = "Use a different email";

    /// <summary>The title of the expired state.</summary>
    public string ExpiredTitle { get; init; } = "This link has expired";

    /// <summary>The sentence of the expired state: (address or null, minutes) ⇒ text.</summary>
    public Func<string?, int, string> ExpiredDescription { get; init; } = static (email, minutes) => email is null
        ? $"Sign-in links work once and for {minutes} minutes. Ask for a new one below."
        : $"Sign-in links work once and for {minutes} minutes. We can send a fresh one to {email}.";

    /// <summary>The button of the expired state.</summary>
    public string SendNew { get; init; } = "Send a new link";
}
