namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="LinkUnfurl"/> shows or announces, in English by default. Replace the instance (or set its
/// properties) to translate or reword; texts with names in them are functions so the word order can change. The
/// message field's own name and placeholder are <see cref="LinkUnfurl.Label"/> and <see cref="LinkUnfurl.Placeholder"/>.
/// </summary>
public sealed class LinkUnfurlLabels
{
    /// <summary>The description of the message field's keys, read after its name.</summary>
    public string KeyHint { get; set; } =
        "Paste a link to preview it. Alt+Enter shows or folds the preview of the link before the caret. Enter sends, Shift+Enter starts a new line.";

    /// <summary>The send button's accessible name.</summary>
    public string Send { get; set; } = "Send message";

    /// <summary>The send button's accessible name while the message is being sent.</summary>
    public string Sending { get; set; } = "Sending message";

    /// <summary>The short word before the sample buttons (hidden in narrow composers and from assistive technology).</summary>
    public string Try { get; set; } = "Try";

    /// <summary>The accessible name of the group of sample buttons.</summary>
    public string SamplesGroup { get; set; } = "Sample links";

    /// <summary>A sample button that pastes its link: the site, "Add the Ridgeway sample".</summary>
    public Func<string, string> SamplePaste { get; set; } = site => $"Add the {site} sample";

    /// <summary>A sample button whose link is loading: the site, "Ridgeway sample still loading".</summary>
    public Func<string, string> SampleLoading { get; set; } = site => $"{site} sample still loading";

    /// <summary>A sample button whose card is open: the site, "Fold the Ridgeway card".</summary>
    public Func<string, string> SampleCollapse { get; set; } = site => $"Fold the {site} card";

    /// <summary>A sample button whose card is folded: the site, "Open the Ridgeway card".</summary>
    public Func<string, string> SampleShow { get; set; } = site => $"Open the {site} card";

    /// <summary>A loading link's accessible name: the short address, "ridgeway.trails/routes/48213907, preview on its way".</summary>
    public Func<string, string> LinkLoading { get; set; } = address => $"{address}, preview on its way";

    /// <summary>A resolved link's accessible name: the title and the site, "Link to Ridgeway: Seven lakes loop …".</summary>
    public Func<string, string, string> LinkReady { get; set; } = (title, site) => $"Link to {site}: {title}";

    /// <summary>A failed link's accessible name: the short address, "example.org/plan, no preview, select to try again".</summary>
    public Func<string, string> LinkFailed { get; set; } = address => $"{address}, no preview, select to try again";

    /// <summary>A card's accessible name: the site and the title, "Seven lakes loop … on Ridgeway".</summary>
    public Func<string, string, string> Card { get; set; } = (site, title) => $"{title} on {site}";

    /// <summary>A card's collapse button: the title, "Fold card: Seven lakes loop …".</summary>
    public Func<string, string> CollapsePreview { get; set; } = title => $"Fold card: {title}";

    /// <summary>A card's remove button: the title, "Remove link: Seven lakes loop …".</summary>
    public Func<string, string> RemoveLink { get; set; } = title => $"Remove link: {title}";

    /// <summary>The announcement when a link is added: its host, "ridgeway.trails link added, preview on its way.".</summary>
    public Func<string, string> AnnounceAdded { get; set; } = host => $"{host} link added, preview on its way.";

    /// <summary>The announcement when a card opens: the site and the title.</summary>
    public Func<string, string, string> AnnounceShown { get; set; } = (site, title) => $"{title} from {site}, card open.";

    /// <summary>The announcement when a link resolves without opening its card: the site and the title.</summary>
    public Func<string, string, string> AnnounceReady { get; set; } = (site, title) => $"{title} from {site}, card ready.";

    /// <summary>The announcement when a card folds into its link: the title.</summary>
    public Func<string, string> AnnounceCollapsed { get; set; } = title => $"{title}, card folded.";

    /// <summary>The announcement when a link is removed: its title, or null when it had none.</summary>
    public Func<string?, string> AnnounceRemoved { get; set; } = title => title is null ? "Link removed." : $"Link removed. {title}.";

    /// <summary>The announcement when no preview could be found: the host, "No preview for example.org. The link stays as text.".</summary>
    public Func<string, string> AnnounceFailed { get; set; } = host => $"No preview for {host}. The link stays as text.";

    /// <summary>The announcement after the message was sent.</summary>
    public string AnnounceSent { get; set; } = "Message sent.";

    /// <summary>The error under the composer when sending failed and the failure has no message of its own.</summary>
    public string SendFailed { get; set; } = "The message could not be sent. Try again.";
}
