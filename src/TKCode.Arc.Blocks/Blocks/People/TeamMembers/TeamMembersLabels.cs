namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="TeamMembers"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; texts with values are functions so that word order can change. Role names come from
/// <see cref="TeamMembersRoleOption.Label"/>.
/// </summary>
public sealed class TeamMembersLabels
{
    /// <summary>The label above the invite field.</summary>
    public string InviteHeading { get; init; } = "Invite people";

    /// <summary>The invite field's placeholder (shown while it holds no chip and no text).</summary>
    public string Placeholder { get; init; } = "name@company.com";

    /// <summary>The line under the invite field while there is nothing to warn about.</summary>
    public string Hint { get; init; } = "Press Enter, a comma or a space after each address. Paste a list to add it at once.";

    /// <summary>The screen reader name of the invite field.</summary>
    public string EmailFieldName { get; init; } = "Email addresses to invite";

    /// <summary>The screen reader name of the role picker next to the invite field.</summary>
    public string InviteAsName { get; init; } = "Invite as";

    /// <summary>The send button's text for zero or one invite.</summary>
    public string SendOne { get; init; } = "Send invite";

    /// <summary>The send button's text for two or more invites; the argument is the count.</summary>
    public Func<int, string> SendMany { get; init; } = count => $"Send {count} invites";

    /// <summary>The members section's heading.</summary>
    public string MembersHeading { get; init; } = "Members";

    /// <summary>The pending invites section's heading.</summary>
    public string PendingHeading { get; init; } = "Pending invites";

    /// <summary>The search field's placeholder.</summary>
    public string SearchPlaceholder { get; init; } = "Find by name or email";

    /// <summary>The screen reader name of the search field.</summary>
    public string SearchName { get; init; } = "Search members and invites";

    /// <summary>The owner's role text (no menu).</summary>
    public string Owner { get; init; } = "Owner";

    /// <summary>The word after the current user's name.</summary>
    public string You { get; init; } = "You";

    /// <summary>The seat meter's visible text after the used count; the argument is the seat limit.</summary>
    public Func<int, string> SeatsOf { get; init; } = limit => $"of {limit} seats";

    /// <summary>The seat meter's screen reader value; the arguments are the used seats and the limit.</summary>
    public Func<int, int, string> SeatsValue { get; init; } = (used, limit) => $"{used} of {limit} seats used";

    /// <summary>The seat meter's screen reader name.</summary>
    public string SeatsName { get; init; } = "Seats";

    /// <summary>The error for a piece that is not a complete address; the argument is the piece.</summary>
    public Func<string, string> Invalid { get; init; } = text => $"“{text}” is missing part of an email address";

    /// <summary>The error for a member's address; the argument is the member's name.</summary>
    public Func<string, string> AlreadyMember { get; init; } = name => $"{name} is already on the team";

    /// <summary>The error for an address with a pending invite; the argument is the address.</summary>
    public Func<string, string> AlreadyInvited { get; init; } = email => $"{email} already has an invite waiting";

    /// <summary>The warning when there are more chips than free seats; the arguments are the free seats and how many chips to drop.</summary>
    public Func<int, int, string> OverLimit { get; init; } = (left, extra) =>
        $"Only {left} {(left == 1 ? "seat is" : "seats are")} free. Take out {extra} {(extra == 1 ? "address" : "addresses")} to send";

    /// <summary>The warning when every seat is taken; the argument is the seat limit.</summary>
    public Func<int, string> Full { get; init; } = limit => $"All {limit} seats are in use";

    /// <summary>The warning when every seat is taken and the field holds chips; the argument is the seat limit.</summary>
    public Func<int, string> FullWithChips { get; init; } = limit =>
        $"All {limit} seats are in use. Remove a member or revoke an invite first";

    /// <summary>The members section's empty row; the argument is the search text.</summary>
    public Func<string, string> NoMembersMatch { get; init; } = query => $"Nobody matches “{query}”";

    /// <summary>The button in the members section's empty row.</summary>
    public string ClearSearch { get; init; } = "Clear search";

    /// <summary>The pending section's empty row while searching.</summary>
    public string NoInvitesMatch { get; init; } = "No invites match the search";

    /// <summary>The pending section's empty row when there are no invites at all; null hides the row.</summary>
    public string? NoInvites { get; init; }

    /// <summary>The confirm title of a removal; the argument is the member's name.</summary>
    public Func<string, string> RemoveTitle { get; init; } = name => $"Remove {name}?";

    /// <summary>The confirm line of a removal; the arguments are the member's name and the workspace name (may be null).</summary>
    public Func<string, string?, string> RemoveDetail { get; init; } = (_, workspace) =>
        string.IsNullOrWhiteSpace(workspace) ? "Their access ends immediately" : $"Their access to {workspace} ends immediately";

    /// <summary>The confirm row's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The confirm row's remove button.</summary>
    public string Remove { get; init; } = "Remove";

    /// <summary>The pending row's resend button.</summary>
    public string Resend { get; init; } = "Resend";

    /// <summary>The resend button's text after a resend (a check mark leads it).</summary>
    public string Sent { get; init; } = "Sent";

    /// <summary>The pending row's revoke button.</summary>
    public string Revoke { get; init; } = "Revoke";

    /// <summary>A pending row's detail text, after the role; the arguments are the role name and the invite's detail.</summary>
    public Func<string, string?, string> InviteDetail { get; init; } = (role, detail) =>
        string.IsNullOrWhiteSpace(detail) ? role : $"{role} · {detail}";

    /// <summary>The detail the block writes on new invites.</summary>
    public string InvitedJustNow { get; init; } = "Invited just now";

    /// <summary>The detail the block writes after a resend.</summary>
    public string ResentJustNow { get; init; } = "Resent just now";

    /// <summary>The footer's text until the first action.</summary>
    public string StatusIdle { get; init; } = "Changes take effect immediately";

    /// <summary>The status after one invite went out; the arguments are the address and the role name.</summary>
    public Func<string, string, string> StatusInvitedOne { get; init; } = (email, role) => $"Invite sent to {email} as {role}";

    /// <summary>The status after several invites went out; the arguments are the count and the role name.</summary>
    public Func<int, string, string> StatusInvitedMany { get; init; } = (count, role) => $"{count} invites sent as {role}";

    /// <summary>The status after a chip was removed; the argument is the address.</summary>
    public Func<string, string> StatusChipRemoved { get; init; } = email => $"Took {email} off the invite list";

    /// <summary>The status after a revoke; the argument is the address.</summary>
    public Func<string, string> StatusRevoked { get; init; } = email => $"Invite to {email} withdrawn";

    /// <summary>The status after a resend; the argument is the address.</summary>
    public Func<string, string> StatusResent { get; init; } = email => $"Invite sent again to {email}";

    /// <summary>The status after a removal; the arguments are the member's name and the workspace name (may be null).</summary>
    public Func<string, string?, string> StatusMemberRemoved { get; init; } = (name, workspace) =>
        string.IsNullOrWhiteSpace(workspace) ? $"{name} was removed" : $"{name} was removed from {workspace}";

    /// <summary>The status after a role change; the arguments are the member's name and the new role name.</summary>
    public Func<string, string, string> StatusRoleChanged { get; init; } = (name, role) => $"{name} now has the {role} role";

    /// <summary>The error when sending failed and the handler gave no message.</summary>
    public string SendFailed { get; init; } = "The invites did not go out. Try again.";

    /// <summary>The status when a resend failed; the argument is the address.</summary>
    public Func<string, string> ResendFailed { get; init; } = email => $"Could not resend the invite to {email}";

    /// <summary>The status when a revoke failed; the argument is the address.</summary>
    public Func<string, string> RevokeFailed { get; init; } = email => $"Could not withdraw the invite to {email}";

    /// <summary>The status when a removal failed; the argument is the member's name.</summary>
    public Func<string, string> RemoveFailed { get; init; } = name => $"Could not remove {name}";

    /// <summary>The status when a role change failed (the role goes back); the argument is the member's name.</summary>
    public Func<string, string> RoleChangeFailed { get; init; } = name => $"Could not change the role of {name}";

    /// <summary>The screen reader name of a member's role trigger; the argument is the member's name.</summary>
    public Func<string, string> RoleFor { get; init; } = name => $"Role for {name}";

    /// <summary>The screen reader name of a member's remove button; the argument is the member's name.</summary>
    public Func<string, string> RemoveName { get; init; } = name => $"Remove {name}";

    /// <summary>The screen reader name of a chip's remove button; the argument is the address.</summary>
    public Func<string, string> RemoveChipName { get; init; } = email => $"Remove {email} from the invite list";

    /// <summary>The screen reader name of a resend button; the argument is the address.</summary>
    public Func<string, string> ResendName { get; init; } = email => $"Resend the invite to {email}";

    /// <summary>The screen reader name of a resend button after a resend; the argument is the address.</summary>
    public Func<string, string> SentName { get; init; } = email => $"Sent, the invite went to {email} again";

    /// <summary>The screen reader name of a revoke button; the argument is the address.</summary>
    public Func<string, string> RevokeName { get; init; } = email => $"Revoke the invite to {email}";

    /// <summary>Added to an avatar's screen reader name when the person is online.</summary>
    public string Online { get; init; } = "online";
}
