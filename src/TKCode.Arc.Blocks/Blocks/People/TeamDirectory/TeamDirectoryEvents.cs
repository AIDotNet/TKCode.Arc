using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="TeamDirectory.PersonSelected"/>: the person picked and the one shown before.</summary>
public sealed class TeamDirectoryPersonSelectedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The block.</param>
    /// <param name="person">The person picked.</param>
    /// <param name="previousPerson">The person whose profile was shown before, if any.</param>
    public TeamDirectoryPersonSelectedEventArgs(RoutedEvent routedEvent, object source, TeamDirectoryPerson person,
        TeamDirectoryPerson? previousPerson)
        : base(routedEvent, source)
    {
        Person = person;
        PreviousPerson = previousPerson;
    }

    /// <summary>The person picked from the list.</summary>
    public TeamDirectoryPerson Person { get; }

    /// <summary>The person whose profile was shown before the pick, or null.</summary>
    public TeamDirectoryPerson? PreviousPerson { get; }
}

/// <summary>Arguments of <see cref="TeamDirectory.EmailCopied"/>: whose address was copied and whether it worked.</summary>
public sealed class TeamDirectoryEmailCopiedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The block.</param>
    /// <param name="person">The person whose address was copied.</param>
    /// <param name="email">The address.</param>
    /// <param name="succeeded">Whether the copy worked.</param>
    /// <param name="error">The exception of a failed copy, when there was one.</param>
    public TeamDirectoryEmailCopiedEventArgs(RoutedEvent routedEvent, object source, TeamDirectoryPerson person, string email,
        bool succeeded, Exception? error)
        : base(routedEvent, source)
    {
        Person = person;
        Email = email;
        Succeeded = succeeded;
        Error = error;
    }

    /// <summary>The person whose address was copied.</summary>
    public TeamDirectoryPerson Person { get; }

    /// <summary>The address that was copied.</summary>
    public string Email { get; }

    /// <summary>True when the address reached the clipboard.</summary>
    public bool Succeeded { get; }

    /// <summary>The exception thrown by <see cref="TeamDirectory.CopyAsync"/>, when a failed copy threw one.</summary>
    public Exception? Error { get; }
}
