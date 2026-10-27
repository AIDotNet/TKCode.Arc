namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="InvitePeople"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; texts with values are functions so that word order can change.
/// </summary>
public sealed class InvitePeopleLabels
{
    #region Field

    /// <summary>The field's placeholder while it has no chips.</summary>
    public string Placeholder { get; init; } = "Type a name or an email address";

    /// <summary>The field's placeholder once it has chips.</summary>
    public string MorePlaceholder { get; init; } = "Add more";

    /// <summary>The field's accessible name.</summary>
    public string FieldName { get; init; } = "Email or name";

    /// <summary>The accessible name of the list of chips.</summary>
    public string ChipsName { get; init; } = "People to invite";

    /// <summary>The hint under the field.</summary>
    public string Hint { get; init; } = "Use commas to add several, or paste a list";

    /// <summary>The button that pastes <see cref="InvitePeople.SampleText"/>.</summary>
    public string PasteSample { get; init; } = "Paste a sample list";

    /// <summary>The heading over the people suggested for an empty field.</summary>
    public string Suggested { get; init; } = "Suggested";

    /// <summary>The accessible name of the suggestion list.</summary>
    public string SuggestionsName { get; init; } = "Suggestions";

    /// <summary>The second line of the "invite by email" row.</summary>
    public string InviteByEmail { get; init; } = "Invite by email";

    /// <summary>The row shown when nothing matches; the argument is the query.</summary>
    public Func<string, string> NoMatch { get; init; } = q => $"Nobody matches “{q}”. Type a full email address to invite them.";

    /// <summary>The note of a suggestion for someone already on the team.</summary>
    public string OnTeam { get; init; } = "In the team";

    /// <summary>The note of a suggestion for someone with a pending invite.</summary>
    public string Invited { get; init; } = "Invited";

    /// <summary>The accessible name of a chip's role button; the arguments are the person and the role.</summary>
    public Func<string, string, string> RoleButtonName { get; init; } = (who, role) => $"Role for {who}: {role}";

    /// <summary>The accessible name of a chip's remove button; the argument is the chip's text.</summary>
    public Func<string, string> RemoveName { get; init; } = x => $"Remove {x}";

    /// <summary>The accessible name of an invalid chip; the arguments are its text and the reason.</summary>
    public Func<string, string, string> EditName { get; init; } = (x, reason) => $"Edit {x}. {reason}";

    #endregion

    #region Errors

    /// <summary>The reason for an entry without an @.</summary>
    public Func<string, string> ErrorNotEmail { get; init; } = x => $"“{x}” doesn’t look like an email address";

    /// <summary>The reason for an entry that starts with @.</summary>
    public Func<string, string> ErrorMissingLocal { get; init; } = x => $"“{x}” needs a name before the @";

    /// <summary>The reason for an entry that ends with @.</summary>
    public Func<string, string> ErrorMissingDomain { get; init; } = x => $"“{x}” has nothing after the @";

    /// <summary>The reason for a domain without a dot; the arguments are the entry and an example domain.</summary>
    public Func<string, string, string> ErrorIncompleteDomain { get; init; } =
        (x, example) => $"The domain of “{x}” is incomplete, for example {example}";

    /// <summary>The reason for any other malformed address.</summary>
    public Func<string, string> ErrorInvalid { get; init; } = x => $"“{x}” is not a valid email address";

    /// <summary>The reason for someone already on the team; the argument is the name.</summary>
    public Func<string, string> ErrorAlreadyMember { get; init; } = name => $"{name} is already in the team";

    /// <summary>The reason for someone with a pending invite; the argument is the name.</summary>
    public Func<string, string> ErrorAlreadyInvited { get; init; } = name => $"{name} has an invite waiting already";

    /// <summary>The error panel's title; the argument is the number of invalid entries.</summary>
    public Func<int, string> ErrorsTitle { get; init; } = n => n == 1 ? "1 entry can’t be invited" : $"{n} entries can’t be invited";

    /// <summary>The error panel's button that removes every invalid chip.</summary>
    public string RemoveInvalid { get; init; } = "Remove invalid";

    /// <summary>The error panel's last line.</summary>
    public string ErrorsHelp { get; init; } = "Select a chip to correct it";

    #endregion

    #region Seats

    /// <summary>The word between the seat count and the limit.</summary>
    public string SeatsOf { get; init; } = "of";

    /// <summary>The word after the limit.</summary>
    public string Seats { get; init; } = "seats";

    /// <summary>The plan's name on the seat line; the argument is the plan's name.</summary>
    public Func<string, string> PlanName { get; init; } = name => $"{name} plan";

    /// <summary>The legend entry of members; the argument is the count.</summary>
    public Func<int, string> LegendMembers { get; init; } = n => n == 1 ? "1 member" : $"{n} members";

    /// <summary>The legend entry of pending invites.</summary>
    public Func<int, string> LegendPending { get; init; } = n => $"{n} pending";

    /// <summary>The legend entry of new chips.</summary>
    public Func<int, string> LegendNew { get; init; } = n => $"{n} new";

    /// <summary>The meter's accessible name.</summary>
    public string MeterName { get; init; } = "Seats used";

    /// <summary>The meter's value text; the arguments are the seats used, the limit and how many are over (0 when not).</summary>
    public Func<int, int, int, string> MeterValueText { get; init; } =
        (total, limit, over) => over > 0 ? $"{total} of {limit} seats, {over} over the limit" : $"{total} of {limit} seats";

    /// <summary>The words after the rolling number of the over-limit line ("seats above the … limit"); the arguments are the count and the plan.</summary>
    public Func<int, string, string> OverLimit { get; init; } =
        (n, plan) => n == 1 ? $"seat above the {plan} limit" : $"seats above the {plan} limit";

    /// <summary>The upgrade button; the argument is the next plan's name.</summary>
    public Func<string, string> Upgrade { get; init; } = plan => $"Upgrade to {plan}";

    /// <summary>The line shown instead of the upgrade button on the largest plan.</summary>
    public string RemoveToSend { get; init; } = "Remove a few people to send";

    #endregion

    #region Footer and pending

    /// <summary>The verb before the rolling count on the send button.</summary>
    public string SendVerb { get; init; } = "Send";

    /// <summary>The noun after the count, singular.</summary>
    public string InviteOne { get; init; } = "invite";

    /// <summary>The noun after the count, plural.</summary>
    public string InviteOther { get; init; } = "invites";

    /// <summary>The send button's accessible name; the argument is the number of valid chips.</summary>
    public Func<int, string> SendName { get; init; } =
        n => n == 0 ? "Send invites" : n == 1 ? "Send 1 invite" : $"Send {n} invites";

    /// <summary>The reset button's accessible name.</summary>
    public string ResetName { get; init; } = "Reset";

    /// <summary>The pending section's heading.</summary>
    public string Pending { get; init; } = "Pending";

    /// <summary>The line of an empty pending list.</summary>
    public string NoPending { get; init; } = "No invites waiting";

    /// <summary>The status of an invite being sent.</summary>
    public string Sending { get; init; } = "Sending";

    /// <summary>The status of a sent invite; the argument is <see cref="InvitePeoplePendingInvite.SentText"/> or null.</summary>
    public Func<string?, string> Sent { get; init; } = when => when is null ? "Sent just now" : $"Sent {when}";

    /// <summary>The status of an accepted invite.</summary>
    public string Joined { get; init; } = "Joined";

    /// <summary>The resend button.</summary>
    public string Resend { get; init; } = "Resend";

    /// <summary>The resend button for a moment after it was pressed.</summary>
    public string Resent { get; init; } = "Sent again";

    /// <summary>The resend button's accessible name; the argument is the person.</summary>
    public Func<string, string> ResendName { get; init; } = who => $"Resend the invite to {who}";

    /// <summary>The resend button's accessible name right after a resend.</summary>
    public Func<string, string> ResentName { get; init; } = who => $"Invite sent to {who} again";

    /// <summary>The revoke button's accessible name; the argument is the person.</summary>
    public Func<string, string> RevokeName { get; init; } = who => $"Revoke the invite to {who}";

    /// <summary>The team avatars' accessible name; the argument is the number of people.</summary>
    public Func<int, string> TeamName { get; init; } = n => n == 1 ? "Team, 1 person" : $"Team, {n} people";

    /// <summary>The hidden text of the "+N" pill; the argument is N.</summary>
    public Func<int, string> MorePeople { get; init; } = n => $"and {n} more";

    #endregion

    #region Announcements

    /// <summary>A suggestion became a chip.</summary>
    public Func<string, string> Added { get; init; } = who => $"Added {who}";

    /// <summary>Part of a paste summary: people added.</summary>
    public Func<int, string> AddedCount { get; init; } = n => n == 1 ? "Added 1 person" : $"Added {n} people";

    /// <summary>Part of a paste summary: entries to fix.</summary>
    public Func<int, string> FixCount { get; init; } = n => n == 1 ? "1 entry to fix" : $"{n} entries to fix";

    /// <summary>Part of a paste summary: duplicates skipped.</summary>
    public Func<int, string> DuplicateCount { get; init; } = n => n == 1 ? "skipped 1 duplicate" : $"skipped {n} duplicates";

    /// <summary>A paste that added nothing new.</summary>
    public string NothingAdded { get; init; } = "Nothing new to add";

    /// <summary>A chip was removed.</summary>
    public Func<string, string> Removed { get; init; } = x => $"Removed {x}";

    /// <summary>The last chip is marked by the first Backspace.</summary>
    public Func<string, string> Armed { get; init; } = x => $"Press Backspace again to remove {x}";

    /// <summary>An invalid chip went back into the field.</summary>
    public Func<string, string> Editing { get; init; } = x => $"Editing {x}";

    /// <summary>A chip's role changed; the arguments are the person and the role.</summary>
    public Func<string, string, string> RoleChanged { get; init; } = (who, role) => $"{who} will join as {role}";

    /// <summary>Send was pressed while chips need fixing; the argument is how many.</summary>
    public Func<int, string> FixBeforeSending { get; init; } =
        n => n == 1 ? "Fix or remove 1 entry before sending" : $"Fix or remove {n} entries before sending";

    /// <summary>Send was pressed over the limit; the arguments are the seats over and the plan.</summary>
    public Func<int, string, string> OverBeforeSending { get; init; } =
        (n, plan) => n == 1
            ? $"This is 1 seat more than the {plan} plan has. Upgrade or remove 1 person."
            : $"This is {n} seats more than the {plan} plan has. Upgrade or remove {n} people.";

    /// <summary>Invites are on their way.</summary>
    public Func<int, string> SendingCount { get; init; } = n => n == 1 ? "Sending 1 invite" : $"Sending {n} invites";

    /// <summary>Invites were delivered.</summary>
    public Func<int, string> SentCount { get; init; } = n => n == 1 ? "1 invite sent" : $"{n} invites sent";

    /// <summary>Sending failed; the chips are back in the field.</summary>
    public string SendFailed { get; init; } = "The invites could not be sent. Your list is back in the field.";

    /// <summary>Someone accepted; the arguments are the person and the workspace (may be empty).</summary>
    public Func<string, string, string> JoinedAnnouncement { get; init; } =
        (who, workspace) => string.IsNullOrEmpty(workspace) ? $"{who} accepted and joined" : $"{who} accepted and joined {workspace}";

    /// <summary>An invite was sent again.</summary>
    public Func<string, string> ResentAnnouncement { get; init; } = who => $"Invite sent to {who} again";

    /// <summary>Resending failed.</summary>
    public Func<string, string> ResendFailed { get; init; } = who => $"The invite to {who} could not be sent again";

    /// <summary>An invite was revoked.</summary>
    public Func<string, string> Revoked { get; init; } = who => $"Revoked the invite to {who}. 1 seat is free again.";

    /// <summary>Revoking failed and the invite is back.</summary>
    public Func<string, string> RevokeFailed { get; init; } = who => $"The invite to {who} could not be revoked";

    /// <summary>The plan changed; the arguments are the plan and its seats.</summary>
    public Func<string, int, string> Upgraded { get; init; } = (plan, seats) => $"Now on the {plan} plan with {seats} seats";

    /// <summary>Upgrading failed.</summary>
    public string UpgradeFailed { get; init; } = "The plan could not be changed";

    /// <summary>Remove invalid removed chips.</summary>
    public Func<int, string> RemovedInvalid { get; init; } = n => n == 1 ? "Removed 1 entry" : $"Removed {n} entries";

    /// <summary>The block was reset.</summary>
    public string ResetDone { get; init; } = "Back to the start";

    #endregion

    /// <summary>The reason text for an invalid entry.</summary>
    public string Reason(InvitePeopleEntry entry, string exampleDomain) => entry.Error switch
    {
        InvitePeopleEntryError.NotEmail => ErrorNotEmail(entry.Text),
        InvitePeopleEntryError.MissingLocal => ErrorMissingLocal(entry.Text),
        InvitePeopleEntryError.MissingDomain => ErrorMissingDomain(entry.Text),
        InvitePeopleEntryError.IncompleteDomain => ErrorIncompleteDomain(entry.Text, exampleDomain),
        InvitePeopleEntryError.Invalid => ErrorInvalid(entry.Text),
        InvitePeopleEntryError.AlreadyMember => ErrorAlreadyMember(entry.Text),
        InvitePeopleEntryError.AlreadyInvited => ErrorAlreadyInvited(entry.Text),
        _ => string.Empty
    };
}
