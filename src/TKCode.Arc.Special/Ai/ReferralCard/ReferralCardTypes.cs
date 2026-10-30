using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How far one invited friend has come in a referral program.</summary>
public enum ReferralStage
{
    /// <summary>The invite went out; the friend has not signed up yet.</summary>
    Invited,

    /// <summary>The friend created an account but has not paid yet.</summary>
    SignedUp,

    /// <summary>The friend subscribed, so both sides got the reward.</summary>
    Earned
}

/// <summary>Where a <see cref="ReferralCard"/> share button sends the invite link.</summary>
public enum ReferralShareChannel
{
    /// <summary>A public post on a social network (a send glyph).</summary>
    Post,

    /// <summary>A share to a professional network or a group (a people glyph).</summary>
    Network,

    /// <summary>An email from the person's own mail app (a mail glyph and a <c>mailto:</c> link).</summary>
    Email
}

/// <summary>One person invited through a <see cref="ReferralCard"/>.</summary>
public sealed class Referral
{
    /// <summary>A stable id; the card keeps a row per id, so a stage change updates the row in place.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The friend's name once known (after they sign up). Null shows the email in its place.</summary>
    public string? Name { get; init; }

    /// <summary>The address the invite went to.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>A photo for the avatar. Null shows the initials of the name or the email.</summary>
    public ImageSource? Avatar { get; init; }

    /// <summary>How far the friend has come.</summary>
    public ReferralStage Stage { get; init; }

    /// <summary>When the invite went out; the row shows its date.</summary>
    public DateTimeOffset InvitedAt { get; init; }

    /// <summary>A copy of this referral at another stage (same id, so the row updates in place).</summary>
    public Referral WithStage(ReferralStage stage) => new()
    {
        Id = Id,
        Name = Name,
        Email = Email,
        Avatar = Avatar,
        Stage = stage,
        InvitedAt = InvitedAt
    };

    /// <summary>A copy of this referral with a name (a friend who signed up).</summary>
    public Referral WithName(string? name) => new()
    {
        Id = Id,
        Name = name,
        Email = Email,
        Avatar = Avatar,
        Stage = Stage,
        InvitedAt = InvitedAt
    };
}

/// <summary>The bonus a <see cref="ReferralCard"/> unlocks after a number of paid referrals.</summary>
public sealed class ReferralMilestone
{
    /// <summary>Paid referrals needed. The bar draws one segment each, up to 12 (a larger goal is drawn in 12 equal parts).</summary>
    public int Goal { get; init; } = 5;

    /// <summary>The reward in a few words ("3 months free").</summary>
    public string Reward { get; init; } = string.Empty;

    /// <summary>An optional line shown once it is unlocked ("Applied to your next invoice").</summary>
    public string? Detail { get; init; }
}

/// <summary>Arguments of <see cref="ReferralCard.LinkCopied"/>.</summary>
public sealed class ReferralLinkEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ReferralLinkEventArgs(RoutedEvent routedEvent, string link) : base(routedEvent)
    {
        Link = link;
    }

    /// <summary>The full invite link that was copied.</summary>
    public string Link { get; }
}

/// <summary>
/// Arguments of <see cref="ReferralCard.ShareRequested"/>. Open <see cref="Url"/> and set <c>Handled</c>; when no handler
/// does, the card treats the share as simulated and says so.
/// </summary>
public sealed class ReferralShareEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ReferralShareEventArgs(RoutedEvent routedEvent, ReferralShareChannel channel, string url, string link, string? message)
        : base(routedEvent)
    {
        Channel = channel;
        Url = url;
        Link = link;
        Message = message;
    }

    /// <summary>The share button that was pressed.</summary>
    public ReferralShareChannel Channel { get; }

    /// <summary>A ready share URL (see <see cref="ReferralCard.BuildShareUrl"/>).</summary>
    public string Url { get; }

    /// <summary>The invite link.</summary>
    public string Link { get; }

    /// <summary>The text that goes with the link, or null.</summary>
    public string? Message { get; }
}

/// <summary>Arguments of <see cref="ReferralCard.InvitesSent"/>.</summary>
public sealed class ReferralInvitesEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ReferralInvitesEventArgs(RoutedEvent routedEvent, IReadOnlyList<string> emails, bool isSimulated) : base(routedEvent)
    {
        Emails = emails;
        IsSimulated = isSimulated;
    }

    /// <summary>The addresses that were sent, in the order they were added.</summary>
    public IReadOnlyList<string> Emails { get; }

    /// <summary>True when the card has no <see cref="ReferralCard.InviteHandler"/> and only simulated the send.</summary>
    public bool IsSimulated { get; }
}

/// <summary>Arguments of <see cref="ReferralCard.MilestoneReached"/>.</summary>
public sealed class ReferralMilestoneEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ReferralMilestoneEventArgs(RoutedEvent routedEvent, ReferralMilestone milestone) : base(routedEvent)
    {
        Milestone = milestone;
    }

    /// <summary>The milestone that was reached.</summary>
    public ReferralMilestone Milestone { get; }
}
