using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="HeroSignup"/> is in its signup.</summary>
public enum HeroSignupPhase
{
    /// <summary>Waiting for an address (also after a failed send, with the address kept).</summary>
    Idle,

    /// <summary>The address is being sent; the field is read-only and the button shows a spinner.</summary>
    Sending,

    /// <summary>The address was accepted; the confirmation replaced the form.</summary>
    Done
}

/// <summary>How a <see cref="HeroSignup"/> lines up its content.</summary>
public enum HeroSignupAlignment
{
    /// <summary>Centered in the section (the default).</summary>
    Center,

    /// <summary>Lined up on the start edge, for a hero with a picture beside it.</summary>
    Start
}

/// <summary>Arguments of <see cref="HeroSignup.Submitted"/>.</summary>
public sealed class HeroSignupSubmittedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="email"/>.</summary>
    public HeroSignupSubmittedEventArgs(RoutedEvent routedEvent, string email) : base(routedEvent)
    {
        Email = email;
    }

    /// <summary>The accepted address, trimmed.</summary>
    public string Email { get; }
}
