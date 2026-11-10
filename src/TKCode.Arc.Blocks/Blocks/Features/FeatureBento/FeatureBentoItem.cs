using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The small live drawing at the top of a <see cref="FeatureBento"/> card.</summary>
public enum FeatureBentoArt
{
    /// <summary>No drawing: the card is text only.</summary>
    None,

    /// <summary>A line chart that keeps drawing itself, with a dot on its newest point (live metrics).</summary>
    Pulse,

    /// <summary>A hub with dots circling it on three rings (integrations, a network).</summary>
    Orbit,

    /// <summary>A terminal line typing a command, then its answer (a CLI, automation).</summary>
    Typing,

    /// <summary>A row of bars rising and falling (usage, audio, load).</summary>
    Bars,

    /// <summary>Two devices passing packets both ways (sync, replication).</summary>
    Sync,

    /// <summary>A shield with a scan line and widening rings (security, compliance).</summary>
    Shield
}

/// <summary>How much of the bento grid a card takes (columns of six, rows of one unit).</summary>
public enum FeatureBentoSize
{
    /// <summary>Two columns, one row.</summary>
    Small,

    /// <summary>Three columns, one row.</summary>
    Medium,

    /// <summary>Four columns, one row.</summary>
    Wide,

    /// <summary>Two columns, two rows.</summary>
    Tall,

    /// <summary>Four columns, two rows.</summary>
    Large
}

/// <summary>
/// One card of a <see cref="FeatureBento"/>: a title, a sentence, a size in the grid and a live drawing. Set the
/// properties before the item is added; later changes are not observed.
/// </summary>
public sealed class FeatureBentoItem
{
    /// <summary>The card's title, also its screen reader name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The sentence under the title.</summary>
    public string? Description { get; set; }

    /// <summary>The drawing at the top of the card.</summary>
    public FeatureBentoArt Art { get; set; } = FeatureBentoArt.Pulse;

    /// <summary>How much of the grid the card takes.</summary>
    public FeatureBentoSize Size { get; set; } = FeatureBentoSize.Small;

    /// <summary>Anything the app wants to keep with the card (an id, a link).</summary>
    public object? Tag { get; set; }
}

/// <summary>Arguments of <see cref="FeatureBento.ItemInvoked"/>.</summary>
public sealed class FeatureBentoItemEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="item"/>.</summary>
    public FeatureBentoItemEventArgs(RoutedEvent routedEvent, FeatureBentoItem item) : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The card that was pressed.</summary>
    public FeatureBentoItem Item { get; }
}

/// <summary>The invented features <see cref="FeatureBento"/> shows with no configuration.</summary>
internal static class FeatureBentoSamples
{
    public static FeatureBentoItem[] Create() =>
    [
        new() { Title = "Live metrics", Art = FeatureBentoArt.Pulse, Size = FeatureBentoSize.Large,
            Description = "Every request, error and signup lands on the chart within a second, so you see a release land as it happens." },
        new() { Title = "Sixty integrations", Art = FeatureBentoArt.Orbit, Size = FeatureBentoSize.Tall,
            Description = "Connect the tools you already pay for. Events flow both ways without glue code." },
        new() { Title = "One command deploys", Art = FeatureBentoArt.Typing, Size = FeatureBentoSize.Medium,
            Description = "Preview, promote and roll back from the terminal or a pull request." },
        new() { Title = "Usage you can read", Art = FeatureBentoArt.Bars, Size = FeatureBentoSize.Medium,
            Description = "Spend and load per team, per day, with alerts before a budget runs out." },
        new() { Title = "Sync everywhere", Art = FeatureBentoArt.Sync, Size = FeatureBentoSize.Wide,
            Description = "Edits made offline merge cleanly when a device comes back, with no conflicts to resolve by hand." },
        new() { Title = "Secure by default", Art = FeatureBentoArt.Shield, Size = FeatureBentoSize.Small,
            Description = "Single sign-on, audit logs and encryption at rest on every plan." },
    ];
}
