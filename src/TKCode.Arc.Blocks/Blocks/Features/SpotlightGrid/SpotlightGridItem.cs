using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One feature card of a <see cref="SpotlightGrid"/>: an icon, a title and a sentence or two. Set the properties before
/// the item is added; later changes are not observed.
/// </summary>
public sealed class SpotlightGridItem
{
    /// <summary>The card's title, also its screen reader name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The sentence under the title.</summary>
    public string? Description { get; set; }

    /// <summary>A 24×24 stroked icon (for example one of <c>ArcIcons</c>); null leaves the icon tile out.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>A short label in the card's corner ("New", "Beta"); null leaves it out.</summary>
    public string? Badge { get; set; }

    /// <summary>Anything the app wants to keep with the card (an id, a link).</summary>
    public object? Tag { get; set; }
}

/// <summary>Arguments of <see cref="SpotlightGrid.ItemInvoked"/>.</summary>
public sealed class SpotlightGridItemEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="item"/>.</summary>
    public SpotlightGridItemEventArgs(RoutedEvent routedEvent, SpotlightGridItem item) : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The card that was pressed.</summary>
    public SpotlightGridItem Item { get; }
}

/// <summary>The invented features <see cref="SpotlightGrid"/> shows with no configuration.</summary>
internal static class SpotlightGridSamples
{
    public static SpotlightGridItem[] Create() =>
    [
        new() { Title = "Instant search", Icon = ArcIcons.Search,
            Description = "Every document, comment and file answers in under 50 ms, typos included." },
        new() { Title = "Live cursors", Icon = ArcIcons.Users,
            Description = "See who is editing what, and follow a teammate through a review with one click." },
        new() { Title = "Branch previews", Icon = ArcIcons.GitBranch, Badge = "New",
            Description = "Every change gets its own link, so feedback lands before anything merges." },
        new() { Title = "Audit trail", Icon = ArcIcons.ShieldCheck,
            Description = "Who changed which setting, when and why, kept for as long as you need it." },
        new() { Title = "Offline first", Icon = ArcIcons.Cloud,
            Description = "Keep working on a train. Changes sync and merge the moment you are back." },
        new() { Title = "Automations", Icon = ArcIcons.Zap,
            Description = "Turn the busywork into rules: assign, label and notify without writing code." },
    ];
}
