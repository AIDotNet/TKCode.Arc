using System.Windows.Input;
using Avalonia;
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

/// <summary>
/// One message of an <see cref="AnnouncementBar"/>. Its properties can be bound in XAML (for example a localized
/// message); the bar follows later changes.
/// </summary>
public sealed class Announcement : AvaloniaObject
{
    /// <summary>Stable id, reported to events.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<Announcement, string>(nameof(Id), string.Empty);

    /// <summary>The message text.</summary>
    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<Announcement, string>(nameof(Message), string.Empty);

    /// <summary>Optional call to action after the message.</summary>
    public static readonly StyledProperty<AnnouncementAction?> ActionProperty =
        AvaloniaProperty.Register<Announcement, AnnouncementAction?>(nameof(Action));

    /// <summary>Optional live countdown after the message.</summary>
    public static readonly StyledProperty<AnnouncementCountdown?> CountdownProperty =
        AvaloniaProperty.Register<Announcement, AnnouncementCountdown?>(nameof(Countdown));

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="MessageProperty"/>
    public string Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <inheritdoc cref="ActionProperty"/>
    public AnnouncementAction? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <inheritdoc cref="CountdownProperty"/>
    public AnnouncementCountdown? Countdown { get => GetValue(CountdownProperty); set => SetValue(CountdownProperty, value); }
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
