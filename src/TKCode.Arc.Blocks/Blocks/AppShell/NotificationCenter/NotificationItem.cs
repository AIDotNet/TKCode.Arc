// Ported from Arc's free notification-center block (MIT, Copyright (c) 2026 Elia Kuratli).
using AtomUI.Controls;
using Avalonia;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The kind of a system update; picks the icon of a <see cref="NotificationItem"/> without an actor.</summary>
public enum NotificationTone
{
    /// <summary>A message or general update (Lucide MessageCircle in the accent color).</summary>
    Info,

    /// <summary>Something finished well (Lucide CircleCheck in the success color).</summary>
    Success,

    /// <summary>Something needs attention (Lucide TriangleAlert in the warning color).</summary>
    Warning
}

/// <summary>Which notifications a <see cref="NotificationCenter"/> lists.</summary>
public enum NotificationCenterView
{
    /// <summary>Every notification, read or not.</summary>
    All,

    /// <summary>Only the unread notifications.</summary>
    Unread
}

/// <summary>
/// One update in a <see cref="NotificationCenter"/>: a title, an optional description, a time label and its read state.
/// An update from a person shows their <see cref="Avatar"/>; a system update shows an icon for its <see cref="Tone"/>.
/// </summary>
public class NotificationItem : AvaloniaObject
{
    /// <summary>Stable id. Rows are matched by id, so a refreshed list with new objects keeps its rows in place.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<NotificationItem, string>(nameof(Id), string.Empty);

    /// <summary>The headline, one line (cut with an ellipsis).</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<NotificationItem, string>(nameof(Title), string.Empty);

    /// <summary>The body: previewed on one line under the title and shown in full when the row is expanded.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<NotificationItem, string?>(nameof(Description));

    /// <summary>The time as display text, such as "2m" or "Yesterday".</summary>
    public static readonly StyledProperty<string> TimeProperty =
        AvaloniaProperty.Register<NotificationItem, string>(nameof(Time), string.Empty);

    /// <summary>Whether the update has been read (two-way); the notification center changes it when you mark it.</summary>
    public static readonly StyledProperty<bool> IsReadProperty =
        AvaloniaProperty.Register<NotificationItem, bool>(nameof(IsRead), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The icon of an update without an actor: Info (default), Success or Warning.</summary>
    public static readonly StyledProperty<NotificationTone> ToneProperty =
        AvaloniaProperty.Register<NotificationItem, NotificationTone>(nameof(Tone));

    /// <summary>The person behind the update; when set, the row shows their avatar instead of the tone icon.</summary>
    public static readonly StyledProperty<string?> ActorNameProperty =
        AvaloniaProperty.Register<NotificationItem, string?>(nameof(ActorName));

    /// <summary>The actor's photo (a URL works in XAML); initials show while it loads or when it fails.</summary>
    public static readonly StyledProperty<ImageSource?> ActorSourceProperty =
        AvaloniaProperty.Register<NotificationItem, ImageSource?>(nameof(ActorSource));

    /// <summary>Any value you want back in the events, such as the record id in your store.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<NotificationItem, object?>(nameof(Tag));

    /// <summary>Stable id. Rows are matched by id, so a refreshed list with new objects keeps its rows in place.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The headline, one line (cut with an ellipsis).</summary>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The body: previewed on one line under the title and shown in full when the row is expanded.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>The time as display text, such as "2m" or "Yesterday".</summary>
    public string Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    /// <summary>Whether the update has been read (two-way); the notification center changes it when you mark it.</summary>
    public bool IsRead { get => GetValue(IsReadProperty); set => SetValue(IsReadProperty, value); }

    /// <summary>The icon of an update without an actor: Info (default), Success or Warning.</summary>
    public NotificationTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>The person behind the update; when set, the row shows their avatar instead of the tone icon.</summary>
    public string? ActorName { get => GetValue(ActorNameProperty); set => SetValue(ActorNameProperty, value); }

    /// <summary>The actor's photo (a URL works in XAML); initials show while it loads or when it fails.</summary>
    public ImageSource? ActorSource { get => GetValue(ActorSourceProperty); set => SetValue(ActorSourceProperty, value); }

    /// <summary>Any value you want back in the events, such as the record id in your store.</summary>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>True when the update comes from a person (a name or a photo is set).</summary>
    internal bool HasActor => !string.IsNullOrWhiteSpace(ActorName) || ActorSource is not null;

    public override string ToString() => Title;
}

/// <summary>Arguments of <see cref="NotificationCenter.ReadChanged"/>.</summary>
public sealed class NotificationReadChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="item"/>, now read or unread.</summary>
    public NotificationReadChangedEventArgs(RoutedEvent routedEvent, NotificationItem item, bool isRead)
        : base(routedEvent)
    {
        Item = item;
        IsRead = isRead;
    }

    /// <summary>The notification that was marked.</summary>
    public NotificationItem Item { get; }

    /// <summary>The new read state.</summary>
    public bool IsRead { get; }
}

/// <summary>Arguments of <see cref="NotificationCenter.Dismissed"/>.</summary>
public sealed class NotificationDismissedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for the dismissed <paramref name="item"/>.</summary>
    public NotificationDismissedEventArgs(RoutedEvent routedEvent, NotificationItem item)
        : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The notification that was dismissed.</summary>
    public NotificationItem Item { get; }
}

/// <summary>
/// Every word a <see cref="NotificationCenter"/> shows or announces besides its <see cref="NotificationCenter.Label"/>.
/// Each has Arc's English default; words with a count or a name are functions, so an app can apply its own plural rules.
/// Assign a new instance to <see cref="NotificationCenter.Labels"/> to change them.
/// </summary>
public sealed class NotificationCenterLabels
{
    /// <summary>Accessible name of the bell ("Notifications, 3 unread"); gets the label and the unread count.</summary>
    public Func<string, int, string> Trigger { get; set; } =
        static (label, unread) => unread > 0 ? $"{label}, {unread} unread" : label;

    /// <summary>The line under the heading ("3 updates waiting for you", "You’re all caught up").</summary>
    public Func<int, string> Summary { get; set; } =
        static unread => unread > 0 ? $"{unread} update{(unread == 1 ? "" : "s")} waiting for you" : "You’re all caught up";

    /// <summary>Accessible name of the close button ("Close notifications").</summary>
    public string Close { get; set; } = "Close notifications";

    /// <summary>Accessible name of the All / Unread switch ("Show notifications").</summary>
    public string ViewSwitch { get; set; } = "Show notifications";

    /// <summary>The All view button ("All").</summary>
    public string All { get; set; } = "All";

    /// <summary>The Unread view button ("Unread").</summary>
    public string Unread { get; set; } = "Unread";

    /// <summary>Accessible name of the list in the All view ("All notifications").</summary>
    public string AllList { get; set; } = "All notifications";

    /// <summary>Accessible name of the list in the Unread view ("Unread notifications").</summary>
    public string UnreadList { get; set; } = "Unread notifications";

    /// <summary>The bulk action in the toolbar ("Mark all read").</summary>
    public string MarkAllRead { get; set; } = "Mark all read";

    /// <summary>Added to an unread row's accessible name ("unread").</summary>
    public string UnreadState { get; set; } = "unread";

    /// <summary>Ends a collapsed row's accessible name ("Show details").</summary>
    public string ShowDetails { get; set; } = "Show details";

    /// <summary>Ends an expanded row's accessible name ("Hide details").</summary>
    public string HideDetails { get; set; } = "Hide details";

    /// <summary>Preview of an update from a person without a description ("From Mia Chen").</summary>
    public Func<string, string> FromActor { get; set; } = static name => $"From {name}";

    /// <summary>Preview of a system update without a description ("View update details").</summary>
    public string ViewDetails { get; set; } = "View update details";

    /// <summary>Details of an update from a person without a description ("Mia Chen shared an update with you.").</summary>
    public Func<string, string> SharedUpdate { get; set; } = static name => $"{name} shared an update with you.";

    /// <summary>Details of a system update without a description ("This update is ready to review.").</summary>
    public string ReadyToReview { get; set; } = "This update is ready to review.";

    /// <summary>The row action on an unread update ("Mark read").</summary>
    public string MarkRead { get; set; } = "Mark read";

    /// <summary>The row action on a read update ("Mark unread").</summary>
    public string MarkUnread { get; set; } = "Mark unread";

    /// <summary>The row action that removes the update ("Dismiss").</summary>
    public string Dismiss { get; set; } = "Dismiss";

    /// <summary>Empty title in the All view ("All clear").</summary>
    public string EmptyTitle { get; set; } = "All clear";

    /// <summary>Empty description in the All view ("New updates will appear here.").</summary>
    public string EmptyDescription { get; set; } = "New updates will appear here.";

    /// <summary>Empty title in the Unread view ("Nothing unread").</summary>
    public string UnreadEmptyTitle { get; set; } = "Nothing unread";

    /// <summary>Empty description in the Unread view ("You’ve seen every update.").</summary>
    public string UnreadEmptyDescription { get; set; } = "You’ve seen every update.";

    /// <summary>The link from an empty Unread view to the All view ("View all updates").</summary>
    public string ViewAll { get; set; } = "View all updates";

    /// <summary>The footer count: "{0} read", where {0} is the rolling number.</summary>
    public string ReadCount { get; set; } = "{0} read";

    /// <summary>The footer action that dismisses every read update ("Clear read").</summary>
    public string ClearRead { get; set; } = "Clear read";
}
