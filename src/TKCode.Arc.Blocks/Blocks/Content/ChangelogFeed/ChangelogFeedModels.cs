// Ported from Arc's free changelog-feed block (MIT, Copyright (c) 2026 Elia Kuratli): the data model of changelog-feed.tsx. The sample entries are original.
using TKCode.Arc.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The kind of change of a <see cref="ChangelogEntry"/>.</summary>
public enum ChangelogKind
{
    /// <summary>Something new.</summary>
    New,

    /// <summary>Something better.</summary>
    Improved,

    /// <summary>Something repaired.</summary>
    Fixed
}

/// <summary>Where the subscribe control of a <see cref="ChangelogFeed"/> stands.</summary>
public enum ChangelogSubscribeState
{
    /// <summary>The Subscribe pill.</summary>
    Idle,

    /// <summary>The email field.</summary>
    Editing,

    /// <summary>The confirmation with Undo.</summary>
    Done
}

/// <summary>A month of the month bar of a <see cref="ChangelogFeed"/>.</summary>
public sealed class ChangelogMonth
{
    /// <summary>The key that <see cref="ChangelogEntry.Month"/> uses, such as "2026-09".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The full label: the month, a space and the year ("September 2026").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The short label in the month nav ("Sep").</summary>
    public string ShortLabel { get; set; } = string.Empty;
}

/// <summary>Media inside an open entry: a <see cref="ChangelogPhoto"/> or a <see cref="ChangelogCode"/>.</summary>
public abstract class ChangelogMedia
{
    private protected ChangelogMedia()
    {
    }
}

/// <summary>A picture with a caption, cropped to 16:10.</summary>
public sealed class ChangelogPhoto : ChangelogMedia
{
    /// <summary>The picture as an image object.</summary>
    public IImage? Image { get; set; }

    /// <summary>The picture as an address or file. Used when <see cref="Image"/> is null.</summary>
    public ImageSource? ImageSource { get; set; }

    /// <summary>The text alternative.</summary>
    public string? Alt { get; set; }

    /// <summary>The line under the picture.</summary>
    public string? Caption { get; set; }
}

/// <summary>A snippet with its file name and a copy button.</summary>
public sealed class ChangelogCode : ChangelogMedia
{
    /// <summary>The file name in the snippet's head.</summary>
    public string File { get; set; } = string.Empty;

    /// <summary>The text of the snippet.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>One release note of a <see cref="ChangelogFeed"/>.</summary>
public sealed class ChangelogEntry
{
    /// <summary>A stable id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The <see cref="ChangelogMonth.Key"/> it belongs to.</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>The date as shown ("Sep 18").</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>The version, without the leading v.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>The kind of change.</summary>
    public ChangelogKind Kind { get; set; }

    /// <summary>The title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One line under the title.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>The list that opens under the row.</summary>
    public IReadOnlyList<string> Details { get; set; } = [];

    /// <summary>A picture or a snippet under the list.</summary>
    public ChangelogMedia? Media { get; set; }
}

/// <summary>Arguments of <see cref="ChangelogFeed.Subscribed"/> and <see cref="ChangelogFeed.SubscriptionUndone"/>.</summary>
public sealed class ChangelogSubscribeEventArgs : RoutedEventArgs
{
    internal ChangelogSubscribeEventArgs(RoutedEvent routedEvent, object source, string email) : base(routedEvent, source) => Email = email;

    /// <summary>The address, trimmed.</summary>
    public string Email { get; }
}

/// <summary>Original sample notes of an invented product.</summary>
internal static class ChangelogFeedSamples
{
    public const string Title = "Changelog";
    public const string Subtitle = "Larkspur 3.9 shipped on September 22, 2026";
    public const string EndNote = "That is everything since Larkspur 3.4 in June.";

    public static IReadOnlyList<ChangelogMonth> Months() =>
    [
        new() { Key = "2026-09", Label = "September 2026", ShortLabel = "Sep" },
        new() { Key = "2026-08", Label = "August 2026", ShortLabel = "Aug" },
        new() { Key = "2026-07", Label = "July 2026", ShortLabel = "Jul" },
        new() { Key = "2026-06", Label = "June 2026", ShortLabel = "Jun" },
    ];

    private const string RetrySnippet = """
        const hook = larkspur.hooks.create({
          url: "https://example.com/hooks/orders",
          retry: { attempts: 6, backoff: "exponential" },
        });
        """;

    private const string ImportSnippet = """
        name,sku,price
        Linen shirt,LS-204,68.00
        Oak tray,OT-117,42.50
        """;

    public static IReadOnlyList<ChangelogEntry> Entries() =>
    [
        Entry("board-covers", "2026-09", "Sep 22", "3.9.0", ChangelogKind.New, "Cover pictures for boards", "Give each board a picture so the right one is easy to spot in a long list.",
            ["Pick from your files or from a set of drawn covers.", "Covers crop themselves to fit the card."],
            new ChangelogPhoto { Image = BlockSampleArt.Scene(210), Alt = "An abstract blue landscape.", Caption = "A drawn cover on the Field trips board" }),
        Entry("bulk-move", "2026-09", "Sep 22", "3.9.0", ChangelogKind.Improved, "Move many cards at once", "Select with shift and drag the whole group to another list.",
            ["The group keeps its order.", "Undo puts every card back."]),
        Entry("double-send", "2026-09", "Sep 11", "3.8.2", ChangelogKind.Fixed, "Reminders no longer send twice", "A retry after a timeout could repeat a reminder. Each one now has a single delivery key.",
            ["Affected about 0.2% of reminders since 3.8.0.", "No action is needed."]),
        Entry("retries", "2026-09", "Sep 3", "3.8.0", ChangelogKind.New, "Retries for outgoing hooks", "A failed request now tries again on its own, with growing pauses in between.",
            ["Choose up to six attempts per hook.", "Every attempt shows up in the delivery log."],
            new ChangelogCode { File = "hooks.ts", Code = RetrySnippet }),
        Entry("timeline", "2026-08", "Aug 27", "3.7.0", ChangelogKind.New, "A timeline for every project", "See dates and owners along one line without leaving the board.",
            ["Drag a bar to move its dates.", "Weekends stay visible but quiet."],
            new ChangelogPhoto { Image = BlockSampleArt.Scene(40, 1), Alt = "An abstract amber landscape.", Caption = "The timeline of a sample project" }),
        Entry("search-speed", "2026-08", "Aug 27", "3.7.0", ChangelogKind.Improved, "Search answers in half the time", "A new index made typical queries twice as fast on large workspaces.",
            ["Median query time dropped from 180 ms to 85 ms.", "Results no longer jump while you type."]),
        Entry("stale-badge", "2026-08", "Aug 13", "3.6.3", ChangelogKind.Fixed, "Unread badges clear themselves", "Opening a card on one device now clears its badge on the others.",
            ["The badge used to wait for a reload.", "Older clients catch up on their next sync."]),
        Entry("shortcuts", "2026-08", "Aug 4", "3.6.0", ChangelogKind.Improved, "More of the editor by keyboard", "Reorder, indent and archive without reaching for the mouse.",
            ["Press Alt and an arrow to move a line.", "Press Tab to indent it.", "Press E to archive the card."]),
        Entry("templates", "2026-07", "Jul 23", "3.5.0", ChangelogKind.New, "Starter layouts", "Begin a board from a layout your team already likes.",
            ["Save any board as a starter.", "Starters copy lists, not cards."],
            new ChangelogPhoto { Image = BlockSampleArt.Scene(150, 2), Alt = "An abstract green landscape.", Caption = "A starter for weekly planning" }),
        Entry("calendar-zone", "2026-07", "Jul 9", "3.4.2", ChangelogKind.Fixed, "Due dates respect your time zone", "A task due on Friday no longer shows Thursday for people further west.",
            ["Dates are now stored without a time.", "Existing tasks were corrected on July 9."]),
        Entry("csv-import", "2026-07", "Jul 2", "3.4.1", ChangelogKind.Improved, "Imports check before they run", "Upload a file and see what will change before anything does.",
            ["Rows with problems are listed with their line number."],
            new ChangelogCode { File = "catalog.csv", Code = ImportSnippet }),
        Entry("guest-links", "2026-06", "Jun 25", "3.4.0", ChangelogKind.New, "Read-only guest links", "Share one board with someone outside your workspace without a seat.",
            ["Links can expire after a day, a week or a month.", "Turn a link off at any time."]),
        Entry("export-names", "2026-06", "Jun 16", "3.3.2", ChangelogKind.Fixed, "Exports keep accented names", "Names with accents now open correctly in every spreadsheet app.",
            ["Files are written as UTF-8 with a byte order mark."]),
    ];

    private static ChangelogEntry Entry(string id, string month, string date, string version, ChangelogKind kind, string title, string summary, string[] details, ChangelogMedia? media = null) => new()
    {
        Id = id,
        Month = month,
        Date = date,
        Version = version,
        Kind = kind,
        Title = title,
        Summary = summary,
        Details = details,
        Media = media
    };
}
