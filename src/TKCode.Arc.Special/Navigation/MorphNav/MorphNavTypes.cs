using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One link of a <see cref="MorphNav"/>: its text, an optional line under it and an optional glyph. Change a link by
/// replacing it in its section; edits to a link already on screen are not picked up.
/// </summary>
public sealed class MorphNavLink
{
    /// <summary>The link's text, also its accessible name.</summary>
    public string? Text { get; set; }

    /// <summary>A short line under the text; null shows the text alone.</summary>
    public string? Description { get; set; }

    /// <summary>A 24 × 24 stroked glyph drawn in a small tile before the text; null leaves the tile out.</summary>
    public Geometry? Glyph { get; set; }

    /// <summary>Anything you need to tell the links apart, such as a route; the nav does not read it.</summary>
    public object? Tag { get; set; }

    /// <inheritdoc />
    public override string ToString() => Text ?? string.Empty;
}

/// <summary>A titled group of links in a <see cref="MorphNav"/>'s open panel.</summary>
public sealed class MorphNavSection
{
    /// <summary>The section's heading, drawn small above its links; null shows the links alone.</summary>
    public string? Header { get; set; }

    /// <summary>The section's links, in order.</summary>
    [Content]
    public AvaloniaList<MorphNavLink> Links { get; } = new();
}

/// <summary>Carries the link a <see cref="MorphNav"/> opened.</summary>
public sealed class MorphNavLinkEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="link"/>.</summary>
    public MorphNavLinkEventArgs(RoutedEvent routedEvent, MorphNavLink link) : base(routedEvent)
    {
        Link = link;
    }

    /// <summary>The link that was chosen.</summary>
    public MorphNavLink Link { get; }
}
