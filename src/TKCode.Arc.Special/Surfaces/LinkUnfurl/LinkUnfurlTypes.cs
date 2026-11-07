using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// What a link in a <see cref="LinkUnfurl"/> points to, as an unfurl endpoint reports it (Open Graph, oEmbed): the site,
/// a title, an optional description, image and favicon. A preview without an <see cref="Image"/> shows a card without a
/// media area; one without a <see cref="Favicon"/> shows the site's initial on a small square.
/// </summary>
public sealed class LinkUnfurlPreview
{
    /// <summary>Creates an empty preview; set at least <see cref="Url"/>, <see cref="Site"/> and <see cref="Title"/>.</summary>
    public LinkUnfurlPreview()
    {
    }

    /// <summary>Creates a preview of <paramref name="url"/> on <paramref name="site"/> titled <paramref name="title"/>.</summary>
    public LinkUnfurlPreview(string url, string site, string title)
    {
        Url = url;
        Site = site;
        Title = title;
    }

    /// <summary>The address the preview belongs to; the built-in resolver answers a pasted address with the sample of the same address.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The site's name, shown in the card's header and in the accessible names ("Ridgeway").</summary>
    public string Site { get; set; } = string.Empty;

    /// <summary>The page's title: the unfurled link's label and the card's heading, which opens the address.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>A short description under the title (at most two lines are shown); null or empty shows none.</summary>
    public string? Description { get; set; }

    /// <summary>The page's share image, shown at the Open Graph ratio of 1.91 : 1; null shows a card without media.</summary>
    public IImage? Image { get; set; }

    /// <summary>A description of <see cref="Image"/> for assistive technology; null leaves the image unnamed (decorative).</summary>
    public string? ImageAlt { get; set; }

    /// <summary>The site's square icon, drawn at 16px in the link and in the card; null draws the site's initial instead.</summary>
    public IImage? Favicon { get; set; }

    /// <summary>The site name's first letter or digit in upper case (the monogram drawn when there is no favicon).</summary>
    public string Monogram
    {
        get
        {
            foreach (var c in Site)
            {
                if (char.IsLetterOrDigit(c))
                {
                    return char.ToUpperInvariant(c).ToString();
                }
            }
            return "?";
        }
    }
}

/// <summary>Where a link of a <see cref="LinkUnfurl"/> is in its life.</summary>
public enum LinkUnfurlStatus
{
    /// <summary>The preview is being fetched: the link shows its address with a sweep and a globe.</summary>
    Loading,

    /// <summary>The preview arrived: the link shows the favicon and title and can unfurl into its card.</summary>
    Ready,

    /// <summary>No preview could be found: the link keeps its address, has no card and fetches again when clicked.</summary>
    Failed
}

/// <summary>A link in the message of a <see cref="LinkUnfurl"/>: its address, its status and its preview.</summary>
public sealed class LinkUnfurlLink
{
    internal LinkUnfurlLink(string id, string url, string address)
    {
        Id = id;
        Url = url;
        Address = address;
    }

    /// <summary>The link's id within its composer; the methods that act on one link take it.</summary>
    public string Id { get; }

    /// <summary>The address to fetch and open: what was pasted, with <c>https://</c> added before a bare <c>www.</c> address.</summary>
    public string Url { get; }

    /// <summary>The address as it stands in the message text.</summary>
    public string Address { get; }

    /// <summary>Whether the preview is loading, ready or failed.</summary>
    public LinkUnfurlStatus Status { get; internal set; }

    /// <summary>The resolved preview; null while loading and after a failure.</summary>
    public LinkUnfurlPreview? Preview { get; internal set; }

    /// <summary>Whether the link's card is open (or opening) under the message.</summary>
    public bool IsExpanded { get; internal set; }
}

/// <summary>What a <see cref="LinkUnfurl"/> sends: the text, with every link written out as its address, and the previews.</summary>
public sealed class LinkUnfurlMessage
{
    /// <summary>Creates a message.</summary>
    public LinkUnfurlMessage(string text, IReadOnlyList<LinkUnfurlPreview> previews)
    {
        Text = text;
        Previews = previews;
    }

    /// <summary>The message text; links stand in it as the addresses that were pasted or typed.</summary>
    public string Text { get; }

    /// <summary>The previews of the message's links that resolved, in the order of the links.</summary>
    public IReadOnlyList<LinkUnfurlPreview> Previews { get; }
}

/// <summary>Arguments of the link events of <see cref="LinkUnfurl"/>.</summary>
public sealed class LinkUnfurlLinkEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LinkUnfurlLinkEventArgs(RoutedEvent routedEvent, LinkUnfurlLink link)
        : base(routedEvent)
    {
        Link = link;
    }

    /// <summary>The link the event is about, in its new state.</summary>
    public LinkUnfurlLink Link { get; }
}

/// <summary>
/// Arguments of <see cref="LinkUnfurl.SendRequested"/>. Set <see cref="Pending"/> to the task that sends the message: the
/// composer holds until it completes, then clears and confirms; a failed task shows its message under the composer.
/// </summary>
public sealed class LinkUnfurlSendRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LinkUnfurlSendRequestedEventArgs(LinkUnfurlMessage message)
        : base(LinkUnfurl.SendRequestedEvent)
    {
        Message = message;
    }

    /// <summary>The message to send.</summary>
    public LinkUnfurlMessage Message { get; }

    /// <summary>The work that sends the message; null means it was sent at once.</summary>
    public Task? Pending { get; set; }

    /// <summary>Set to true to keep the message in the composer without sending it (no confirmation, nothing cleared).</summary>
    public bool Cancel { get; set; }
}
