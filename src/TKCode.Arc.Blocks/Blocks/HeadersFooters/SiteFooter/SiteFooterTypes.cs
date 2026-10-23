// Ported from Arc's free site-footer block (MIT, Copyright (c) 2026 Elia Kuratli): the data types of site-footer.tsx.
using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>The three layouts of a <see cref="SiteFooter"/>.</summary>
public enum SiteFooterVariant
{
    /// <summary>Link columns beside the brand and a newsletter signup, then the legal row.</summary>
    Columns,

    /// <summary>One quiet pair of rows: brand with links, then the copyright line with status and social links.</summary>
    Minimal,

    /// <summary>Like <see cref="Columns"/> without the newsletter, closing with a large fading arch in the brand gradient.</summary>
    Logo
}

/// <summary>The color of the status dot of a <see cref="SiteFooter"/>.</summary>
public enum SiteStatusTone
{
    /// <summary>Everything works (green).</summary>
    Success,

    /// <summary>Degraded (amber).</summary>
    Warning,

    /// <summary>An outage (red).</summary>
    Danger
}

/// <summary>Where in a <see cref="SiteFooter"/> a pressed link sits.</summary>
public enum SiteFooterNavigationKind
{
    /// <summary>The brand name.</summary>
    Brand,

    /// <summary>A link of a column, or of the one-row list of the Minimal layout.</summary>
    Column,

    /// <summary>A legal link beside the copyright.</summary>
    Legal,

    /// <summary>A social link.</summary>
    Social,

    /// <summary>The system status.</summary>
    Status
}

/// <summary>One link of a <see cref="SiteFooter"/>. A link without <see cref="Href"/> only raises the footer's navigate event.</summary>
public sealed class SiteFooterLink : AvaloniaObject
{
    /// <summary>The text of the link.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SiteFooterLink, string>(nameof(Label), string.Empty);

    /// <summary>The destination. An absolute http, https or mailto address is opened when the navigate event is not handled.</summary>
    public static readonly StyledProperty<string?> HrefProperty =
        AvaloniaProperty.Register<SiteFooterLink, string?>(nameof(Href));

    /// <summary>Whether the link leaves the site: it shows an outward arrow.</summary>
    public static readonly StyledProperty<bool> IsExternalProperty =
        AvaloniaProperty.Register<SiteFooterLink, bool>(nameof(IsExternal));

    /// <summary>Creates an empty link.</summary>
    public SiteFooterLink()
    {
    }

    /// <summary>Creates a link with a label and an optional destination.</summary>
    public SiteFooterLink(string label, string? href = null, bool isExternal = false)
    {
        Label = label;
        Href = href;
        IsExternal = isExternal;
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="HrefProperty"/>
    public string? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <inheritdoc cref="IsExternalProperty"/>
    public bool IsExternal { get => GetValue(IsExternalProperty); set => SetValue(IsExternalProperty, value); }
}

/// <summary>A titled column of links in a <see cref="SiteFooter"/>.</summary>
public sealed class SiteFooterColumn : AvaloniaObject
{
    /// <summary>The heading of the column.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SiteFooterColumn, string>(nameof(Title), string.Empty);

    private readonly AvaloniaList<SiteFooterLink> _links = new();

    /// <summary>Creates an empty column.</summary>
    public SiteFooterColumn()
    {
    }

    /// <summary>Creates a column with a title and links.</summary>
    public SiteFooterColumn(string title, params SiteFooterLink[] links)
    {
        Title = title;
        _links.AddRange(links);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The links of the column, top to bottom.</summary>
    [Content]
    public AvaloniaList<SiteFooterLink> Links => _links;
}

/// <summary>Arguments of <see cref="SiteFooter.NavigateEvent"/>: a link or the status was pressed.</summary>
public sealed class SiteFooterNavigateEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SiteFooterNavigateEventArgs(RoutedEvent routedEvent, object? source, SiteFooterLink link, SiteFooterNavigationKind kind)
        : base(routedEvent, source)
    {
        Link = link;
        Kind = kind;
    }

    /// <summary>The pressed link (for the brand and the status, a link made from their text and address).</summary>
    public SiteFooterLink Link { get; }

    /// <summary>Where the link sits.</summary>
    public SiteFooterNavigationKind Kind { get; }
}

/// <summary>Arguments of <see cref="SiteFooter.SubscribedEvent"/>: the newsletter signup succeeded.</summary>
public sealed class SiteFooterSubscribedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SiteFooterSubscribedEventArgs(RoutedEvent routedEvent, object? source, string email)
        : base(routedEvent, source)
    {
        Email = email;
    }

    /// <summary>The address that was subscribed (trimmed).</summary>
    public string Email { get; }
}

/// <summary>The newsletter's current hint, error or confirmation: one line under the input.</summary>
internal enum SiteFooterMessageTone
{
    Hint,
    Error,
    Success
}

/// <summary>One line of the newsletter's message slot (record: equal messages do not swap).</summary>
internal sealed record SiteFooterMessage(string Text, SiteFooterMessageTone Tone);
