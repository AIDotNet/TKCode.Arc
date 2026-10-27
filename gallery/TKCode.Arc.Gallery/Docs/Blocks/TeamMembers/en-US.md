---
title: Team members
summary: The members page of a workspace: invite by typing or pasting addresses that fly into pending invites as the seat count rolls, change roles, search, and remove people with an inline confirm.
---

<Demo name="Preview" />

## When to use

`TeamMembers` is the people page of a workspace or team settings screen. It answers three questions in one place: who
has access and with which role, who has been invited but has not joined yet, and how many seats are left. Inviting is
fast: type or paste addresses, pick a role, send. The sent addresses fly from the field into the pending list and the seat
count rolls up, so the person sees exactly where their invites went.

- **Workspace or organization settings.** Members with their role, last activity and presence, the owner marked, the
  current user marked "You".
- **Inviting several people at once.** Addresses become chips on Enter, a comma, a semicolon or a space, and a pasted
  list is split in one go. Typos stay in the field with an error; addresses that already belong to someone are refused
  with the reason.
- **Plans with a seat limit.** Members and pending invites each take a seat. The send button refuses to go over the free
  seats and says how many addresses to take out; a full workspace turns the seat bar to the warning color.

## When not to use

- **A people directory to browse.** For profiles, departments and contact details use the Team directory block; this
  block manages access.
- **Defining what each role may do.** Use the Roles and permissions block. Here a role is a name with a short line of
  text, picked from a menu.
- **A general chip input.** For tags or recipients outside an invite flow use `TagInput`.
- **Large organizations.** Rows are not virtualized. Show a page of members at a time (and search on the server) when
  there are hundreds.

## Usage

The block derives from Avalonia's `TemplatedControl`. Members are `TeamMembersPerson` objects and pending invites
`TeamMembersInvite` objects, both `AvaloniaObject`s you can bind to. Roles are keys (`"admin"`, `"member"`, `"viewer"` by
default) described by `TeamMembersRoleOption`s; the owner has no role menu.

```xml
<arc:TeamMembers Title="Members"
                 Description="Choose who can work in the Northwind Lab workspace."
                 WorkspaceName="Northwind Lab"
                 SeatLimit="10">
    <arc:TeamMembersPerson Name="Lena Hart" Email="lena@northwind.example" IsOwner="True" IsCurrentUser="True"
                           IsOnline="True" LastActive="Active now" />
    <arc:TeamMembersPerson Name="Theo Brandt" Email="theo@northwind.example" Role="admin" LastActive="Active now" />
    <arc:TeamMembersPerson Name="Ines Kowal" Email="ines@northwind.example" Role="member" LastActive="Active 3 hours ago" />
</arc:TeamMembers>
```

Without handlers every action takes effect at once. Connect each one to your server with an async handler; the block shows
a busy state while it runs and keeps the old state when it throws:

```csharp
public interface IMembersApi
{
    Task InviteAsync(string workspace, IReadOnlyList<string> emails, string? role, CancellationToken cancel);
    Task ResendAsync(string inviteId, CancellationToken cancel);
    Task RevokeAsync(string inviteId, CancellationToken cancel);
    Task RemoveAsync(string memberId, CancellationToken cancel);
    Task SetRoleAsync(string memberId, string? role, CancellationToken cancel);
}

members.SendInvitesAction = async (request, cancel) =>
    await api.InviteAsync(workspaceId, request.Emails, request.Role, cancel);
members.ResendInviteAction = (invite, cancel) => api.ResendAsync(invite.Id!, cancel);
members.RevokeInviteAction = (invite, cancel) => api.RevokeAsync(invite.Id!, cancel);
members.RemoveMemberAction = (person, cancel) => api.RemoveAsync(person.Id!, cancel);
members.ChangeRoleAction = (change, cancel) => api.SetRoleAsync(change.Member.Id!, change.NewRole, cancel);
```

### Anatomy

- **Header**: `Title`, `Description` and the seat meter ("7 of 10 seats" over a 4px bar). Hide the meter with
  `IsSeatMeterVisible`.
- **Invite form**: the address field with its chips, the "Invite as" role picker (`InviteRole`, two-way) and the send
  button, which reads "Send invite" or "Send 3 invites". The line under the field shows a hint, a seat warning or an
  error. `IsInviteVisible` hides the whole form.
- **Members**: a heading with the count, the search field (`IsSearchVisible`), and one row per member: avatar with
  presence dot, name and email, last active, the role menu (or "Owner") and a remove button.
- **Pending invites**: a heading with the count and one row per invite: a dashed avatar with initials from the address,
  the address, "role · detail" and the Resend and Revoke buttons.
- **Footer**: the result of the last action ("2 invites sent as Member"). It stays until the next action; `StatusText`
  holds it. `IsFooterVisible` hides the footer but the results are still announced.

### Inviting

Each committed piece of text is judged on its own, ignoring case:

1. Not a complete address (one @, a dotted domain, a last part of at least two letters): it stays in the field, the field
   turns red and the line under it says so. Typing again clears the error.
2. Already a chip: dropped without a word.
3. A member's address: dropped, and the line names the member.
4. A pending invite's address: dropped, with an error.
5. Anything else becomes a chip at the end.

`EmailValidator` replaces the address check. Backspace on an empty field removes the last chip and Enter on an empty field
sends. A valid address still being typed counts in the button's label and becomes a chip when the invites are sent.
`AddEmails`, `RemovePendingEmail`, `ClearPendingEmails` and `SendInvitesAsync` do the same from code; `PendingEmails` lists
the chips.

`SendInvitesAction` receives a `TeamMembersSendRequest` with the addresses, the role key and the `TeamMembersInvite`
objects the block is about to add (fill in their `Id` if your server returns one). When it finishes, the invites join the
pending list and `InvitesSent` is raised.

### Seats

`SeatsUsed` is the members plus the pending invites, `SeatsRemaining` what is left of `SeatLimit`. Only the chips count
against the free seats: with more chips than free seats the send button is disabled and the line asks to take some out;
with every seat taken the bar turns to the warning color and the line says so. Revoking an invite or removing a member
frees a seat at once.

### Bound collections and events

`MembersSource` and `InvitesSource` take your own collections and follow their changes. When they are writable lists the
block adds sent invites and removes revoked invites and removed members itself; for a read-only collection add the invites
in an `InvitesSent` handler. Every action raises an event with what changed (`InvitesSent`, `InviteResent`,
`InviteRevoked`, `MemberRemoved`, `MemberRoleChanged`) and runs the matching command. `SearchText` and `InviteRole` are
two-way.

<Demo name="Bound" />

### When the server says no

A handler that throws keeps the previous state and raises `ActionFailed` with the action, its subject and the message:

- **Send**: the chips stay, the button comes back and the line under the field shows the error (as does the footer).
- **Resend**: the button goes back to Resend and the footer shows the error.
- **Revoke** and **remove**: the row stays (the confirmation stays open) and the footer shows the error.
- **Role change**: the new role shows at once and goes back if the handler throws.

The text is the exception's message, `ErrorFormatter`'s text, or a built-in sentence when there is no message. A
cancellation (the block leaving the window) is not reported.

<Demo name="Failures" />

### Removing people

The remove button turns the row into a confirmation in place: the row tints, "Remove Ines Kowal?" and a line about losing
access replace the name, and Cancel and Remove replace the columns. Only one row asks at a time. Escape or Cancel go back
and return the focus to the remove button; after a removal the focus moves to the next row's remove button.
`RequestRemove`, `CancelRemove` and `RemoveMemberAsync` (no confirmation) do the same from code.

### Narrow containers

The block reads its own width. At 640 or less the last-active text moves under the email; at 600 the header and the
invite row stack, the search takes its own line and the paddings tighten from 20 to 16; at 480 the pending invites' buttons
move under the address; at 420 each member row splits into the person on top and last active, role and remove below.

<Demo name="Narrow" />

### Read only

`IsReadOnly` shows the list to people who cannot manage it: no invite form, roles as text, no remove, resend or revoke
buttons. The search still works. `IsFramed="False"` lays the block flat on the page instead of in a card.

<Demo name="ReadOnly" />

### Words

Every text the block shows or announces is in `Labels`, a `TeamMembersLabels` with English defaults; texts with values are
functions so the word order can change in another language. Role names and descriptions come from `Roles`.

## API reference

<!-- api: TeamMembers -->

<!-- api: TeamMembersPerson -->

<!-- api: TeamMembersInvite -->

<!-- api: TeamMembersRoleOption -->

<!-- api: TeamMembersLabels -->

<!-- api: TeamMembersSendRequest -->

<!-- api: TeamMembersRoleChange -->

## Accessibility

### Keyboard

| Keys | Action |
| --- | --- |
| Enter | In the field: turns the text into a chip, or sends when the field is empty. |
| , ; Space | In the field: turns the text into a chip. |
| Backspace | In an empty field: removes the last chip. |
| Enter, Space, Up, Down | On a role picker: opens the menu with the current role focused. |
| Up, Down, Home, End | In the role menu: moves; Enter or Space chooses, Escape closes. |
| Escape | In a row asking to confirm a removal: cancels it. In the search: clears it. |

### Screen readers and focus

- The block is a group named by its title, which is a level 2 heading; the two section headings are level 3 and name
  their lists.
- The seat bar is a read-only progress bar named "Seats" whose description reads "7 of 10 seats used".
- The field is named by `Labels.EmailFieldName`. Each chip is a list item named by its address, with a remove button
  named "Remove … from the invite list".
- The line under the field is announced politely when it warns or errs; every action's result is announced politely, and
  failures assertively.
- Role pickers are combo boxes named "Role for <name>" (or "Invite as") whose value is the role; menu items report which
  one is chosen.
- Remove, Resend and Revoke are named after the person or the address; after a resend the button says the invite went
  out again.
- Focus moves to Cancel when a confirmation opens, back to the remove button when it closes, and on to the next row after
  a removal or a revoke.

## Motion

- **Chips** pop in (scale 0.9 → 1 on a quick spring with a 100 ms fade and blur); the input and the other chips slide to
  their new places on a smooth spring. A removed chip shrinks and fades in 130 ms.
- **Sending**: the button spins while the handler runs. Then each address flies from its chip to its new pending row on a
  spring (visual duration 0.5 s, no bounce) on a pill whose tint fades out; the new rows grow on the smooth spring, the
  dashed avatars pop in 170 ms later and 100 ms apart, and the seat count and the counts roll.
- **Rows** enter and leave by growing and shrinking on the smooth spring (leaving rows fade in 120 ms). Search collapses
  the rows that do not match and brings them back with a 200 ms fade.
- **Text** that changes in place (the line under the field, a pending row's detail, the footer) lifts away with a blur and
  the new text rises in; the line under the field also springs its height. A changed role morphs letter by letter.
- **Seats**: the bar's fill follows the smooth spring and turns to the warning color in 240 ms.
- **Role menu**: opens from its trigger with a short scale and fade and closes faster; the chevron turns over.
- Reduced motion keeps the send button's spinner and short cross-fades; chips, rows, flights, counts and the bar jump.
  Off applies every change at once.

## Related

- Invite people, Team directory and Roles and permissions, the other People blocks.
- `Avatar`, `SearchField`, `TextMorph` and `Button`, the controls the block is made of.
- `TagInput` for chips outside an invite flow.
