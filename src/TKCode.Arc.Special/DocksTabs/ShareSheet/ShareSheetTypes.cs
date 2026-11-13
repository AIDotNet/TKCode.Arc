using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Where a <see cref="ShareSheet"/>'s panel is anchored to its button while it opens.</summary>
public enum ShareSheetAlign
{
    /// <summary>Anchored at the button's top end corner (top right in left-to-right text); the panel extends toward the start and downward.</summary>
    End,

    /// <summary>Anchored at the button's top start corner; the panel extends toward the end and downward.</summary>
    Start,

    /// <summary>Anchored at the button's center; the panel expands and collapses symmetrically around it.</summary>
    Center
}

/// <summary>
/// Someone a <see cref="ShareSheet"/> can send to: one of the recent people under the "send to" row. Picking them flies
/// their avatar up into a chip. Change a person by replacing them in the list; edits to a person already on screen are
/// not picked up.
/// </summary>
public sealed class ShareSheetPerson
{
    /// <summary>A stable id your app knows the person by; it also seeds the generated avatar.</summary>
    public string? Id { get; set; }

    /// <summary>The full name: the accessible name of their button, the chip's Remove button and the confirmation.</summary>
    public string? Name { get; set; }

    /// <summary>A picture of the person; null draws a soft gradient seeded by <see cref="Id"/> (or the name).</summary>
    public ImageSource? Avatar { get; set; }

    /// <summary>Anything else you need to tell people apart; the sheet does not read it.</summary>
    public object? Tag { get; set; }

    /// <summary>The first word of <see cref="Name"/>, shown under the avatar and in the chip.</summary>
    public string ShortName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return Id ?? string.Empty;
            }
            var trimmed = Name.Trim();
            var space = trimmed.IndexOf(' ');
            return space > 0 ? trimmed[..space] : trimmed;
        }
    }

    /// <summary>The name to show or announce: <see cref="Name"/>, else <see cref="Id"/>.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id ?? string.Empty : Name!;

    /// <inheritdoc />
    public override string ToString() => DisplayName;
}

/// <summary>One access level in a <see cref="ShareSheet"/>'s list under the link: who can open the link and what they can do.</summary>
public sealed class ShareSheetAccess
{
    /// <summary>The value <see cref="ShareSheet.Access"/> holds while this level is chosen.</summary>
    public string? Value { get; set; }

    /// <summary>The level's name, such as "Only invited people".</summary>
    public string? Label { get; set; }

    /// <summary>A short line under the name, such as "Can view and comment".</summary>
    public string? Description { get; set; }

    /// <summary>A 24 × 24 stroked glyph before the name (drawn at 16 px).</summary>
    public Geometry? Glyph { get; set; }

    /// <inheritdoc />
    public override string ToString() => Label ?? Value ?? string.Empty;
}

/// <summary>
/// A place a <see cref="ShareSheet"/> can post the link to, shown as a pill button under the link. Pressing it runs
/// <see cref="ShareSheet.ChannelHandler"/>; the button spins while it runs and then reads <see cref="DoneLabel"/> or
/// "Failed" for a moment.
/// </summary>
public sealed class ShareSheetChannel
{
    /// <summary>A stable id your app knows the channel by.</summary>
    public string? Id { get; set; }

    /// <summary>The button's label, such as "Email" or "Team chat".</summary>
    public string? Label { get; set; }

    /// <summary>What the button reads after a successful post, such as "Sent" or "Posted"; it takes the place of <see cref="Label"/>, so a similar length keeps the button steady.</summary>
    public string DoneLabel { get; set; } = "Done";

    /// <summary>A 24 × 24 stroked glyph before the label (drawn at 16 px).</summary>
    public Geometry? Glyph { get; set; }

    /// <inheritdoc />
    public override string ToString() => Label ?? Id ?? string.Empty;
}

/// <summary>What a <see cref="ShareSheet"/> sends: the people picked and the access level the link carries.</summary>
public sealed class ShareSheetSubmission
{
    /// <summary>Creates a submission.</summary>
    public ShareSheetSubmission(IReadOnlyList<ShareSheetPerson> people, ShareSheetAccess? access)
    {
        People = people;
        Access = access;
    }

    /// <summary>The people picked, in the order they were picked.</summary>
    public IReadOnlyList<ShareSheetPerson> People { get; }

    /// <summary>The access level chosen under the link; null when the sheet has none.</summary>
    public ShareSheetAccess? Access { get; }
}

/// <summary>Carries what a <see cref="ShareSheet"/> sent (or took back with Undo).</summary>
public sealed class ShareSheetSentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="submission"/>.</summary>
    public ShareSheetSentEventArgs(RoutedEvent routedEvent, ShareSheetSubmission submission) : base(routedEvent)
    {
        Submission = submission;
    }

    /// <summary>The people and the access level.</summary>
    public ShareSheetSubmission Submission { get; }
}

/// <summary>Carries the channel a <see cref="ShareSheet"/> posted to.</summary>
public sealed class ShareSheetChannelEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="channel"/>.</summary>
    public ShareSheetChannelEventArgs(RoutedEvent routedEvent, ShareSheetChannel channel) : base(routedEvent)
    {
        Channel = channel;
    }

    /// <summary>The channel whose button finished.</summary>
    public ShareSheetChannel Channel { get; }
}

/// <summary>Event data for a <see cref="ShareSheet"/> copy: the link and the access level selected when it was copied.</summary>
public sealed class ShareSheetLinkCopiedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ShareSheetLinkCopiedEventArgs(RoutedEvent routedEvent, string link, ShareSheetAccess? access) : base(routedEvent)
    {
        Link = link;
        Access = access;
    }

    /// <summary>The link that is now on the clipboard.</summary>
    public string Link { get; }

    /// <summary>The access level chosen when it was copied; null when the sheet has none.</summary>
    public ShareSheetAccess? Access { get; }
}

/// <summary>
/// The words a <see cref="ShareSheet"/> shows and announces. Set the properties you want to change (for another language,
/// say) and assign the object to <see cref="ShareSheet.Labels"/>; the sheet reads them when it builds or updates a part.
/// </summary>
public sealed class ShareSheetLabels
{
    /// <summary>The panel's title from the item's name.</summary>
    public Func<string, string> Title { get; set; } = name => string.IsNullOrEmpty(name) ? "Share" : $"Share “{name}”";

    /// <summary>The close button's accessible name.</summary>
    public string Close { get; set; } = "Close";

    /// <summary>The copy button's label.</summary>
    public string Copy { get; set; } = "Copy";

    /// <summary>The copy button's label right after a copy.</summary>
    public string Copied { get; set; } = "Copied";

    /// <summary>The copy button's label when the clipboard refused the link.</summary>
    public string CopyRetry { get; set; } = "Retry";

    /// <summary>The copy button's accessible name.</summary>
    public string CopyLink { get; set; } = "Copy link";

    /// <summary>The accessible name of the access list.</summary>
    public string AccessList { get; set; } = "Link access";

    /// <summary>The accessible name of the row of channel buttons.</summary>
    public string Channels { get; set; } = "Post to";

    /// <summary>The label a channel shows when its post failed.</summary>
    public string ChannelFailed { get; set; } = "Failed";

    /// <summary>The small heading over the chips of the people picked.</summary>
    public string Recipients { get; set; } = "Recipients";

    /// <summary>The hint in the empty chips row.</summary>
    public string RecipientsHint { get; set; } = "Tap a person to add them";

    /// <summary>The accessible name of the row of recent people.</summary>
    public string People { get; set; } = "Suggested people";

    /// <summary>A chip's Remove button, from the person's name.</summary>
    public Func<string, string> Remove { get; set; } = name => $"Remove {name}";

    /// <summary>The send button with nobody picked.</summary>
    public string Send { get; set; } = "Send";

    /// <summary>
    /// The send button with people picked, as a format whose <c>{0}</c> marks where the rolling count sits.
    /// </summary>
    public Func<int, string> SendTo { get; set; } = count => count == 1 ? "Send to {0} person" : "Send to {0} people";

    /// <summary>The send button while sending.</summary>
    public string Sending { get; set; } = "Sending";

    /// <summary>The line over the send button when sending failed.</summary>
    public string SendFailed { get; set; } = "Not sent. Check the connection and try again.";

    /// <summary>The confirmation's title from the people it went to.</summary>
    public Func<IReadOnlyList<ShareSheetPerson>, string> SentTitle { get; set; } = people => people.Count switch
    {
        0 => "Shared",
        1 => $"Shared with {people[0].DisplayName}",
        2 => $"Shared with {people[0].ShortName} and {people[1].ShortName}",
        _ => $"Shared with {people[0].ShortName} and {people.Count - 1} others"
    };

    /// <summary>The line under the confirmation's title from the access level.</summary>
    public Func<ShareSheetAccess?, string> SentDetail { get; set; } = access =>
    {
        if (access is null)
        {
            return string.Empty;
        }
        var description = access.Description;
        if (string.IsNullOrEmpty(description))
        {
            return access.Label ?? string.Empty;
        }
        var lowered = char.ToLowerInvariant(description[0]) + description[1..];
        return string.IsNullOrEmpty(access.Label) ? lowered : $"{access.Label} · {lowered}";
    };

    /// <summary>The confirmation's Undo button.</summary>
    public string Undo { get; set; } = "Undo";

    /// <summary>The confirmation's Done button.</summary>
    public string Done { get; set; } = "Done";

    /// <summary>Announced after a copy.</summary>
    public string LinkCopiedAnnouncement { get; set; } = "Link copied to the clipboard";

    /// <summary>Announced when the clipboard refused the link.</summary>
    public string CopyFailedAnnouncement { get; set; } = "The link could not be copied";

    /// <summary>Announced when a person is picked.</summary>
    public Func<string, string> AddedAnnouncement { get; set; } = name => $"Added {name}";

    /// <summary>Announced when a person is taken out.</summary>
    public Func<string, string> RemovedAnnouncement { get; set; } = name => $"Removed {name}";

    /// <summary>Announced when Send is pressed with nobody picked.</summary>
    public string NobodyAnnouncement { get; set; } = "Add at least one person first";

    /// <summary>Announced when a channel finished, from its label and the word it shows.</summary>
    public Func<string, string, string> ChannelAnnouncement { get; set; } = (channel, result) => $"{channel}: {result}";

    /// <summary>Announced when Undo takes a share back.</summary>
    public string UndoneAnnouncement { get; set; } = "Share taken back; nothing went out";
}
