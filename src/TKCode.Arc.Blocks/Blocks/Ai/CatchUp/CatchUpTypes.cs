using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The groups of a <see cref="CatchUp"/>, in the order they are shown.</summary>
public enum CatchUpKind
{
    /// <summary>Something worth knowing: a launch, a number, a change of plan.</summary>
    Highlight,

    /// <summary>Someone asked for the person or named them.</summary>
    Mention,

    /// <summary>Something was decided.</summary>
    Decision
}

/// <summary>Which items a <see cref="CatchUp"/> lists.</summary>
public enum CatchUpFilter
{
    /// <summary>Every group.</summary>
    All,

    /// <summary>Only highlights.</summary>
    Highlights,

    /// <summary>Only mentions.</summary>
    Mentions,

    /// <summary>Only decisions.</summary>
    Decisions
}

/// <summary>One thing that happened while the person was away.</summary>
public sealed record CatchUpItem
{
    /// <summary>Your identifier of the item; read state is kept by it, so it must be unique.</summary>
    public required string Id { get; init; }

    /// <summary>The group it belongs to.</summary>
    public CatchUpKind Kind { get; init; }

    /// <summary>What happened, in one line.</summary>
    public required string Title { get; init; }

    /// <summary>A sentence of detail; null leaves it out.</summary>
    public string? Detail { get; init; }

    /// <summary>Who did it; shown as a generated avatar (initials). Null shows the group's glyph.</summary>
    public string? Author { get; init; }

    /// <summary>Where it happened ("#launch-room", "Q4 plan").</summary>
    public string? Source { get; init; }

    /// <summary>When it happened, already formatted ("2h ago", "Tue 4:30 PM").</summary>
    public string? Time { get; init; }

    /// <summary>Your link to the item, handed back by <see cref="CatchUp.ItemInvoked"/>.</summary>
    public string? Link { get; init; }

    /// <summary>Whether it starts read.</summary>
    public bool IsRead { get; init; }
}

/// <summary>Arguments of <see cref="CatchUp.ItemInvoked"/>.</summary>
public sealed class CatchUpItemEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CatchUpItemEventArgs(RoutedEvent routedEvent, CatchUpItem item) : base(routedEvent) => Item = item;

    /// <summary>The item that was opened.</summary>
    public CatchUpItem Item { get; }
}

/// <summary>Arguments of <see cref="CatchUp.ItemsRead"/>.</summary>
public sealed class CatchUpReadEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CatchUpReadEventArgs(RoutedEvent routedEvent, IReadOnlyList<CatchUpItem> items, bool isRead) : base(routedEvent)
    {
        Items = items;
        IsRead = isRead;
    }

    /// <summary>The items whose read state changed.</summary>
    public IReadOnlyList<CatchUpItem> Items { get; }

    /// <summary>True when they were marked read, false when marked unread.</summary>
    public bool IsRead { get; }
}

/// <summary>
/// Every word a <see cref="CatchUp"/> shows or reads out, with English defaults. Assign a new instance to
/// <see cref="CatchUp.Labels"/>.
/// </summary>
public sealed record CatchUpLabels
{
    /// <summary>The heading ("While you were away").</summary>
    public string Title { get; init; } = "While you were away";

    /// <summary>The mark-all button ("Mark all as read").</summary>
    public string MarkAllRead { get; init; } = "Mark all as read";

    /// <summary>The group names, in <see cref="CatchUpKind"/> order.</summary>
    public IReadOnlyList<string> Groups { get; init; } = ["Highlights", "Mentions", "Decisions"];

    /// <summary>The "All" filter.</summary>
    public string All { get; init; } = "All";

    /// <summary>A filter's text from its name and unread count ("Mentions 3"; no count when 0).</summary>
    public Func<string, int, string> Filter { get; init; } = static (name, unread) => unread > 0 ? $"{name} {unread}" : name;

    /// <summary>The line when everything is read ("You are all caught up.").</summary>
    public string CaughtUp { get; init; } = "You are all caught up.";

    /// <summary>The line when a filter lists nothing ("Nothing here.").</summary>
    public string Empty { get; init; } = "Nothing here.";

    /// <summary>Accessible name of an item's read button, from its title.</summary>
    public Func<string, string> MarkRead { get; init; } = static title => $"Mark \"{title}\" as read";

    /// <summary>Accessible name of an item's unread button, from its title.</summary>
    public Func<string, string> MarkUnread { get; init; } = static title => $"Mark \"{title}\" as unread";

    /// <summary>The unread state read out with an item ("unread").</summary>
    public string Unread { get; init; } = "unread";

    /// <summary>The unread count under the heading ("5 unread").</summary>
    public Func<int, string> UnreadCount { get; init; } = static count => $"{count} unread";
}

/// <summary>Invented sample data for <see cref="CatchUp"/>: its defaults.</summary>
public static class CatchUpSamples
{
    /// <summary>When the person left.</summary>
    public const string Since = "Since Friday, 6:10 PM";

    /// <summary>The sample summary.</summary>
    public const string Summary =
        "The mobile beta shipped to 2,000 testers and crash reports are down by half. Priya needs your review on the pricing page by Wednesday, and the team agreed to move the launch to November 24.";

    /// <summary>Seven items over the three groups, two already read.</summary>
    public static IReadOnlyList<CatchUpItem> Items() =>
    [
        new() { Id = "h1", Kind = CatchUpKind.Highlight, Title = "Mobile beta reached 2,000 testers",
            Detail = "Crash-free sessions rose from 97.1% to 99.3% after Saturday's fix.", Source = "#mobile", Time = "Sat 11:20 AM", Link = "chat:mobile/8812" },
        new() { Id = "h2", Kind = CatchUpKind.Highlight, Title = "Support backlog under 40 for the first time",
            Source = "Support weekly", Time = "Sun 9:05 AM", Link = "doc:support-weekly", IsRead = true },
        new() { Id = "h3", Kind = CatchUpKind.Highlight, Title = "New design tokens are live in the app",
            Detail = "Spacing and color changes landed behind no flag.", Author = "Tomás Ferreira", Source = "#design", Time = "Mon 8:40 AM", Link = "chat:design/301" },
        new() { Id = "m1", Kind = CatchUpKind.Mention, Title = "Priya asked you to review the pricing page",
            Detail = "\"Could you check the yearly prices before Wednesday?\"", Author = "Priya Raman", Source = "Pricing refresh", Time = "Sat 3:02 PM", Link = "doc:pricing#c12" },
        new() { Id = "m2", Kind = CatchUpKind.Mention, Title = "Jonas tagged you in the incident notes",
            Author = "Jonas Weber", Source = "Incident 412", Time = "Sun 10:47 PM", Link = "doc:incident-412" },
        new() { Id = "d1", Kind = CatchUpKind.Decision, Title = "Launch moves to November 24",
            Detail = "Two more days for the payment migration; marketing dates follow.", Author = "Mei Lin", Source = "Launch sync", Time = "Mon 9:30 AM", Link = "doc:launch-sync" },
        new() { Id = "d2", Kind = CatchUpKind.Decision, Title = "Dark mode ships on by default",
            Author = "Tomás Ferreira", Source = "#design", Time = "Sat 1:15 PM", Link = "chat:design/288", IsRead = true },
    ];
}
