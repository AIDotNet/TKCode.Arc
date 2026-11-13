using Avalonia.Collections;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A top-level destination of a <see cref="MorphNav"/>. With <see cref="Links"/> or a <see cref="Feature"/> it opens a
/// panel; without them it is a plain link that raises <see cref="MorphNav.Navigate"/> when chosen. Edits to an item that is
/// already on screen are picked up when it is added again or its panel opens next.
/// </summary>
public sealed class MorphNavItem
{
    /// <summary>Identifies the item; <see cref="MorphNav.Current"/> names the current item by this value.</summary>
    public string? Value { get; set; }

    /// <summary>The text in the bar, also the accessible name of the item and of its panel.</summary>
    public string? Label { get; set; }

    /// <summary>Where a plain item leads (a route or address); passed on in <see cref="MorphNavNavigateEventArgs.Href"/>.</summary>
    public string? Href { get; set; }

    /// <summary>The links of the item's panel, in reading order.</summary>
    [Content]
    public AvaloniaList<MorphNavLink> Links { get; } = new();

    /// <summary>
    /// Link columns in the panel: 1 or 2. 0 (default) uses two columns for more than three links and one otherwise. Below
    /// 420 px of window width panels always use one column.
    /// </summary>
    public int Columns { get; set; }

    /// <summary>
    /// Content beside the links, such as a <see cref="MorphNavStory"/> or your own card. Choosing a link or a button in it
    /// closes the panel.
    /// </summary>
    public object? Feature { get; set; }

    /// <summary>Template for <see cref="Feature"/> when it is data rather than a control.</summary>
    public IDataTemplate? FeatureTemplate { get; set; }

    /// <summary>
    /// Content under the links, behind a hairline: a <see cref="MorphNavLink"/> shows as a short link with an arrow; other
    /// content is shown as it is, in small secondary text.
    /// </summary>
    public object? Footer { get; set; }

    /// <summary>Template for <see cref="Footer"/> when it is data rather than a control.</summary>
    public IDataTemplate? FooterTemplate { get; set; }

    /// <summary>Whether the item opens a panel (it has links or a feature).</summary>
    public bool HasPanel => Links.Count > 0 || Feature is not null;

    /// <inheritdoc />
    public override string ToString() => Label ?? Value ?? string.Empty;
}

/// <summary>A link in a <see cref="MorphNavItem"/>'s panel, or a footer link.</summary>
public sealed class MorphNavLink
{
    /// <summary>The link's text, also its accessible name.</summary>
    public string? Label { get; set; }

    /// <summary>A short line under the label; null shows the label alone. Panels wrap it to two lines.</summary>
    public string? Description { get; set; }

    /// <summary>A 24 × 24 stroked glyph before the label (for example one of <c>ArcIcons</c>); null leaves it out.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>Where the link leads (a route or address); passed on in <see cref="MorphNavNavigateEventArgs.Href"/>.</summary>
    public string? Href { get; set; }

    /// <inheritdoc />
    public override string ToString() => Label ?? string.Empty;
}

/// <summary>
/// A feature card for a <see cref="MorphNavItem.Feature"/>: a picture, a title and a line under it. Choosing it raises
/// <see cref="MorphNav.Navigate"/> like a link.
/// </summary>
public sealed class MorphNavStory
{
    /// <summary>The card's title, also its accessible name.</summary>
    public string? Title { get; set; }

    /// <summary>A short line under the title.</summary>
    public string? Description { get; set; }

    /// <summary>The picture at the top of the card, cropped to 16:10; null leaves it out.</summary>
    public IImage? Image { get; set; }

    /// <summary>Where the card leads (a route or address); passed on in <see cref="MorphNavNavigateEventArgs.Href"/>.</summary>
    public string? Href { get; set; }

    /// <inheritdoc />
    public override string ToString() => Title ?? string.Empty;
}

/// <summary>
/// An entry the <see cref="MorphNav"/> search can find. Search matches the label first, then words of the label, then the
/// description, group and keywords.
/// </summary>
public sealed class MorphNavSearchEntry
{
    /// <summary>The result's text, also its accessible name.</summary>
    public string? Label { get; set; }

    /// <summary>A short line under the label.</summary>
    public string? Description { get; set; }

    /// <summary>A group name shown at the end of the result, such as the section it belongs to.</summary>
    public string? Group { get; set; }

    /// <summary>A 24 × 24 stroked glyph before the label; null leaves it out.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>Where the result leads (a route or address); passed on in <see cref="MorphNavNavigateEventArgs.Href"/>.</summary>
    public string? Href { get; set; }

    /// <summary>More words the entry should be found by, separated by spaces; never shown.</summary>
    public string? Keywords { get; set; }

    /// <inheritdoc />
    public override string ToString() => Label ?? string.Empty;
}

/// <summary>Carries the destination a <see cref="MorphNav"/> was asked to go to.</summary>
public sealed class MorphNavNavigateEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MorphNavNavigateEventArgs(RoutedEvent routedEvent, string label, string? href, string? section, object? destination)
        : base(routedEvent)
    {
        Label = label;
        Href = href;
        Section = section;
        Destination = destination;
    }

    /// <summary>The text of what was chosen.</summary>
    public string Label { get; }

    /// <summary>Where it leads, when it has an address.</summary>
    public string? Href { get; }

    /// <summary>
    /// The <see cref="MorphNavItem.Value"/> of the top-level item it belongs to (a link's item, or the plain item itself); null
    /// for the brand and search results.
    /// </summary>
    public string? Section { get; }

    /// <summary>
    /// What was chosen: a <see cref="MorphNavItem"/>, <see cref="MorphNavLink"/>, <see cref="MorphNavStory"/> or
    /// <see cref="MorphNavSearchEntry"/>; null for the brand.
    /// </summary>
    public object? Destination { get; }
}
