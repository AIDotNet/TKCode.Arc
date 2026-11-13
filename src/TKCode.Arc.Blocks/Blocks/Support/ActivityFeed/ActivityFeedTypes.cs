namespace TKCode.Arc;

/// <summary>The kind of thing that happened in an <see cref="ActivityFeed"/>; the filter chips and the icons follow it.</summary>
public enum ActivityKind
{
    /// <summary>Someone commented or replied.</summary>
    Comment,

    /// <summary>Something was created, edited, moved or closed.</summary>
    Change,

    /// <summary>A build, release or deploy.</summary>
    Deploy,

    /// <summary>Someone joined, left or changed role.</summary>
    Member,

    /// <summary>Anything else.</summary>
    Other
}

/// <summary>
/// One thing that happened in a workspace, for an <see cref="ActivityFeed"/>: who did what to what, and when. Items are
/// read when they are added; replace an item to change it.
/// </summary>
public sealed class ActivityFeedItem
{
    /// <summary>Creates an empty item (XAML).</summary>
    public ActivityFeedItem()
    {
    }

    /// <summary>Creates an item.</summary>
    public ActivityFeedItem(string id, DateTimeOffset at, string? actor, string action, ActivityKind kind)
    {
        Id = id;
        At = at;
        Actor = actor;
        Action = action;
        Kind = kind;
    }

    /// <summary>Stable identity.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>When it happened; the feed groups by day and shows the newest first.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Who did it; null for the system (a deploy bot, a schedule).</summary>
    public string? Actor { get; set; }

    /// <summary>What they did, completing the actor: "moved Pricing page to Review".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Short context under the action, such as the project or a branch; null shows none.</summary>
    public string? Target { get; set; }

    /// <summary>The kind, for the filter and the icon.</summary>
    public ActivityKind Kind { get; set; }

    /// <summary>A portrait URL for the actor; null shows the kind's icon.</summary>
    public string? Avatar { get; set; }

    /// <summary>Revealed in place when the row is expanded (a comment's text, a deploy's log line); null makes the row static.</summary>
    public object? Detail { get; set; }

    /// <summary>The outcome of a system event (a failed deploy is <see cref="TimelineTone.Danger"/>); say it in the action too.</summary>
    public TimelineTone Tone { get; set; }
}
