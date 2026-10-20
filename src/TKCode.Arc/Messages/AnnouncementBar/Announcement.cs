using System.Windows.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Call to action of an <see cref="Announcement"/>: a link when <see cref="Href"/> is set, otherwise a button.</summary>
public sealed class AnnouncementAction
{
    /// <summary>The label (an arrow follows it).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Opened with the system launcher when the action is invoked.</summary>
    public Uri? Href { get; set; }

    /// <summary>Optional command run when the action is invoked.</summary>
    public ICommand? Command { get; set; }

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter { get; set; }
}

/// <summary>A live countdown shown after the message (for example the end of a sale).</summary>
public sealed class AnnouncementCountdown
{
    /// <summary>The moment the countdown reaches zero.</summary>
    public DateTimeOffset To { get; set; }

    /// <summary>Optional label before the time, such as "Ends in".</summary>
    public string? Label { get; set; }
}

/// <summary>One message of an <see cref="AnnouncementBar"/>.</summary>
public sealed class Announcement
{
    /// <summary>Stable id, reported to events.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The message text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional call to action after the message.</summary>
    public AnnouncementAction? Action { get; set; }

    /// <summary>Optional live countdown after the message.</summary>
    public AnnouncementCountdown? Countdown { get; set; }
}

/// <summary>Look of an <see cref="AnnouncementBar"/>.</summary>
public enum AnnouncementBarTone
{
    /// <summary>Muted surface with a bottom border.</summary>
    Neutral,

    /// <summary>Foreground fill with background text.</summary>
    Inverted
}

/// <summary>Arguments of the <see cref="AnnouncementBar"/> events that concern one message.</summary>
public class AnnouncementEventArgs : RoutedEventArgs
{
    public AnnouncementEventArgs(RoutedEvent routedEvent, Announcement announcement, int index)
        : base(routedEvent)
    {
        Announcement = announcement;
        Index = index;
    }

    /// <summary>The message concerned.</summary>
    public Announcement Announcement { get; }

    /// <summary>Its index in the bar's messages.</summary>
    public int Index { get; }
}
