using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ReferralCard"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; counts, amounts and dates are format functions. Amounts arrive already formatted ("$20").
/// </summary>
public sealed class ReferralCardLabels
{
    /// <summary>The English defaults.</summary>
    public static ReferralCardLabels Default { get; } = new();

    /// <summary>The heading built from the formatted reward when <see cref="ReferralCard.Title"/> is not set: "Give $20, get $20".</summary>
    public Func<string, string> Title { get; init; } = reward => $"Give {reward}, get {reward}";

    /// <summary>The line under the heading when <see cref="ReferralCard.Description"/> is not set.</summary>
    public Func<string, string> Description { get; init; } = reward =>
        $"Friends get {reward} off their first month. You get {reward} in credit for each friend who subscribes.";

    /// <summary>The heading when the card has no reward and no <see cref="ReferralCard.Title"/>.</summary>
    public string TitleWithoutReward { get; init; } = "Invite friends";

    /// <summary>The line under the heading when the card has no reward and no <see cref="ReferralCard.Description"/>.</summary>
    public string DescriptionWithoutReward { get; init; } = "Share your invite link, or invite friends by email.";

    /// <summary>The label over the invite link.</summary>
    public string LinkLabel { get; init; } = "Your invite link";

    /// <summary>The Copy button.</summary>
    public string Copy { get; init; } = "Copy";

    /// <summary>The Copy button once it has copied.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The accessible name of the Copy button.</summary>
    public string CopyLink { get; init; } = "Copy invite link";

    /// <summary>Announced after the link was copied.</summary>
    public string LinkCopied { get; init; } = "Invite link copied";

    /// <summary>The name and tooltip of the post share button.</summary>
    public string SharePost { get; init; } = "Share in a post";

    /// <summary>The name and tooltip of the network share button.</summary>
    public string ShareNetwork { get; init; } = "Share with your network";

    /// <summary>The name and tooltip of the email share button.</summary>
    public string ShareEmail { get; init; } = "Share by email";

    /// <summary>Announced after a share the app handled; the argument is the button's name.</summary>
    public Func<string, string> Shared { get; init; } = channel => $"{channel}: opened";

    /// <summary>Announced after a share no handler took: nothing left the card.</summary>
    public string ShareSimulated { get; init; } = "Simulated, nothing was posted.";

    /// <summary>The label over the email field.</summary>
    public string EmailLabel { get; init; } = "Invite by email";

    /// <summary>The placeholder of the empty email field.</summary>
    public string EmailPlaceholder { get; init; } = "friend@example.com";

    /// <summary>The Send button.</summary>
    public string Send { get; init; } = "Send";

    /// <summary>The accessible name of a chip's remove button; the argument is the address.</summary>
    public Func<string, string> RemoveChip { get; init; } = email => $"Remove {email}";

    /// <summary>Added to a chip's name when its address is not valid.</summary>
    public string NotValid { get; init; } = "not a valid address";

    /// <summary>The hint under an empty email field.</summary>
    public string HintIdle { get; init; } = "Press Enter, comma or space after each address.";

    /// <summary>The hint while chips wait to be sent; the argument is how many.</summary>
    public Func<int, string> HintReady { get; init; } = count =>
        count == 1 ? "1 address ready. Press Enter or Send." : $"{count} addresses ready. Press Enter or Send.";

    /// <summary>The hint while the invites go out; the argument is how many.</summary>
    public Func<int, string> HintSending { get; init; } = count => count == 1 ? "Sending 1 invite…" : $"Sending {count} invites…";

    /// <summary>The hint after a send; the argument is how many.</summary>
    public Func<int, string> HintSent { get; init; } = count => count == 1 ? "Invite sent to 1 friend." : $"Invites sent to {count} friends.";

    /// <summary>The hint after a simulated send (no <see cref="ReferralCard.InviteHandler"/>); the argument is how many.</summary>
    public Func<int, string> HintSimulated { get; init; } = count =>
        count == 1 ? "Simulated 1 invite, nothing was sent." : $"Simulated {count} invites, nothing was sent.";

    /// <summary>The error when typed text is not an email address; the argument is the text.</summary>
    public Func<string, string> ErrorInvalid { get; init; } = text => $"{text} is not a valid email address.";

    /// <summary>The error when Send is pressed while a chip is not valid.</summary>
    public string ErrorFixChips { get; init; } = "Fix or remove the addresses marked as not valid.";

    /// <summary>The error when an address is already a chip; the argument is the address.</summary>
    public Func<string, string> ErrorDuplicate { get; init; } = email => $"{email} is already in the list.";

    /// <summary>The error when an address was invited before; the argument is the address.</summary>
    public Func<string, string> ErrorAlreadyInvited { get; init; } = email => $"{email} was already invited.";

    /// <summary>The error when a send would hold too many addresses; the argument is the limit.</summary>
    public Func<int, string> ErrorLimit { get; init; } = max => $"You can send up to {max} invites at a time.";

    /// <summary>The error when Send is pressed with nothing to send.</summary>
    public string ErrorEmpty { get; init; } = "Add at least one email address.";

    /// <summary>The error when the send failed without a message of its own.</summary>
    public string ErrorSend { get; init; } = "Could not send the invites. Nothing was sent.";

    /// <summary>The funnel's first number: everyone invited.</summary>
    public string Invited { get; init; } = "Invited";

    /// <summary>The funnel's second number: friends who signed up (including those who paid).</summary>
    public string SignedUp { get; init; } = "Signed up";

    /// <summary>The funnel's third number: the credit earned so far.</summary>
    public string Earned { get; init; } = "Earned";

    /// <summary>The stage word of a row that is still an invite.</summary>
    public string StageInvited { get; init; } = "Invited";

    /// <summary>The stage word of a friend who signed up.</summary>
    public string StageSignedUp { get; init; } = "Signed up";

    /// <summary>The stage word of a friend who paid; the argument is the formatted reward ("Earned $20").</summary>
    public Func<string, string> StageEarned { get; init; } = reward => $"Earned {reward}";

    /// <summary>The milestone line before the goal: paid referrals still needed and the reward.</summary>
    public Func<int, string, string> MilestoneToGo { get; init; } = (remaining, reward) =>
        remaining == 1 ? $"1 more paid referral to unlock {reward}" : $"{remaining} more paid referrals to unlock {reward}";

    /// <summary>The milestone line once the goal is reached; the argument is the reward.</summary>
    public Func<string, string> MilestoneUnlocked { get; init; } = reward => $"{reward} unlocked";

    /// <summary>The value text of the milestone bar: paid referrals and the goal ("4 of 5 paid referrals").</summary>
    public Func<int, int, string> MilestoneValue { get; init; } = (paid, goal) => $"{paid} of {goal} paid referrals";

    /// <summary>The accessible name of the milestone bar.</summary>
    public string MilestoneName { get; init; } = "Milestone progress";

    /// <summary>Announced when the milestone unlocks; the argument is the reward.</summary>
    public Func<string, string> MilestoneAnnouncement { get; init; } = reward => $"Milestone unlocked: {reward}";

    /// <summary>The heading and accessible name of the list of people.</summary>
    public string ListLabel { get; init; } = "Your invites";

    /// <summary>The line in an empty list.</summary>
    public string ListEmpty { get; init; } = "No invites yet. Friends you invite show up here.";

    /// <summary>Announced when a friend moves on; the arguments are the name (or address) and the stage word.</summary>
    public Func<string, string, string> StageAnnouncement { get; init; } = (who, stage) => $"{who}: {stage}";

    /// <summary>The date of a row ("Oct 27").</summary>
    public Func<DateTimeOffset, string> Date { get; init; } = date =>
        date.ToLocalTime().ToString("MMM d", CultureInfo.GetCultureInfo("en-US"));
}
