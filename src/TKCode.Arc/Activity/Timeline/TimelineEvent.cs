using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The status of a system event in a <see cref="Timeline"/> (Arc's <c>tone</c>).</summary>
public enum TimelineTone
{
    /// <summary>A quiet node on the surface (default).</summary>
    Neutral,

    /// <summary>A success tint. Say the outcome in the title too, so it never rests on color.</summary>
    Success,

    /// <summary>A danger tint. Say the outcome in the title too, so it never rests on color.</summary>
    Danger
}

/// <summary>
/// One update in a <see cref="Timeline"/> (Arc's <c>TimelineEvent</c>): who did what and when, with optional context and
/// a detail revealed in place. Events are read when they are added; replace an event to change it.
/// </summary>
public class TimelineEvent
{
    /// <summary>Creates an empty event (XAML).</summary>
    public TimelineEvent()
    {
    }

    /// <summary>Creates an event.</summary>
    public TimelineEvent(string id, DateTimeOffset at, string title, string? actor = null)
    {
        Id = id;
        At = at;
        Title = title;
        Actor = actor;
    }

    /// <summary>Stable identity (expansion, freshness and the announcement follow it).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>When it happened. Events can be given in any order; the newest shows first.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Who did it, shown first in the foreground color.</summary>
    public string? Actor { get; set; }

    /// <summary>What happened, completing the actor: "merged Checkout redesign into main".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Short context under the title, such as a pull request or a build.</summary>
    public string? Meta { get; set; }

    /// <summary>
    /// Revealed in place when the row is expanded: text or any control. Rows without detail are not interactive.
    /// </summary>
    public object? Detail { get; set; }

    /// <summary>Portrait URL for an event by a person.</summary>
    public string? Avatar { get; set; }

    /// <summary>24×24 Lucide icon (<see cref="ArcIcons"/>) for a system event, used when there is no avatar.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>Status of a system event. Always say the outcome in the title too.</summary>
    public TimelineTone Tone { get; set; }

    /// <summary>Whether the row has a detail and can expand.</summary>
    public bool HasDetail => Detail is not null && Detail is not string { Length: 0 };
}

/// <summary>Arguments of <see cref="Timeline.ExpandedChanged"/>.</summary>
public sealed class TimelineExpandedChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TimelineExpandedChangedEventArgs(RoutedEvent routedEvent, TimelineEvent item, bool isExpanded)
        : base(routedEvent)
    {
        Item = item;
        IsExpanded = isExpanded;
    }

    /// <summary>The event whose row opened or closed.</summary>
    public TimelineEvent Item { get; }

    /// <summary>True when the row opened.</summary>
    public bool IsExpanded { get; }
}
