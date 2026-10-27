---
title: Invite people
summary: Invite people by name or email into a workspace, with a role for each, a seat meter against the plan, and avatars that fly from the suggestions into chips and on into a pending list.
---

<Demo name="Preview" />

## When to use

`InvitePeople` is the form behind an "Invite people" button: one field that takes names and addresses, a check on seats,
and the invites that are still waiting. People found in the directory become chips with their portrait and a role;
anything that cannot be invited becomes a red chip that says why. Send moves the chips into the pending list, where each
invite can be sent again or revoked, and an accepted invite leaves the list and joins the team avatars at the top.

- **Inviting several people at once.** Type a few letters to pick people from the directory, type an address, or paste
  a whole list from a spreadsheet or an email: commas, semicolons and line breaks separate the entries.
- **Seats that cost money.** The meter counts members, pending invites and the new chips against the plan. When the
  count runs over, Send is held back and the next plan is offered right there.
- **A dialog or a settings page.** Framed, the block is a card at most 600 wide; flat, it sits inside a surface of your
  own.

## When not to use

- **Managing the people who already joined.** Use `Team members` for roles, removal and seat changes of members.
- **Editing what a role can do.** Use `Roles and permissions`.
- **Plain tags.** When the entries are not people (labels, keywords, recipients without a directory), use `TagInput`.

## Usage

The block derives from Avalonia's `TemplatedControl`. People are `InvitePeoplePerson` objects (name, email, job title and
an optional portrait; without one the avatar shows initials). `Directory` holds everyone who can be found, `Members` the
team and `PendingInvites` the invites that are waiting; a directory person who is in the team or has an invite shows as
unavailable in the suggestions. `Roles` and `Plans` fill the role menu and the seat meter.

```xml
<arc:InvitePeople Title="Invite people"
                  Description="Bring people into Lumen Labs by name or email"
                  WorkspaceName="Lumen Labs">
    <arc:InvitePeople.Roles>
        <arc:InvitePeopleRole Name="Admin" Description="People, billing and settings" />
        <arc:InvitePeopleRole Name="Member" Description="Works on every project" IsDefault="True" />
        <arc:InvitePeopleRole Name="Guest" Description="Sees only shared projects" />
    </arc:InvitePeople.Roles>
    <arc:InvitePeople.Plans>
        <arc:InvitePeoplePlan Name="Basic" SeatLimit="10" />
        <arc:InvitePeoplePlan Name="Growth" SeatLimit="25" />
    </arc:InvitePeople.Plans>
    <arc:InvitePeoplePerson Name="Clara Weiss" Email="clara.weiss@lumen.example" Title="Brand designer" />
    <arc:InvitePeoplePerson Name="Mateus Rocha" Email="mateus.rocha@lumen.example" Title="Backend engineer" />
</arc:InvitePeople>
```

### Anatomy

- **Header**: `Title`, `Description` and the team's avatars (five at most, then a "+N" pill). In a narrow block the
  avatars move under the title.
- **Field**: the chips and a text box that wrap onto as many lines as they need; the field's height follows on a spring.
  Under it a hint, and a button that pastes `SampleText` when you set one.
- **Error panel**: opens when chips cannot be invited, with one line per chip and a button that removes them all.
- **Seats**: "8 of 10 seats", the plan's name, a meter with three segments (members, pending, new) and a legend. Over the
  limit the count and the new segment turn red and a line offers the next plan.
- **Footer**: the reset button (when `IsResetVisible`) and Send, whose label counts the chips.
- **Pending**: the invites, newest first, each with its status, a resend button and a revoke button.

### Adding people

Typing filters the directory by name or email (the job title is not searched); people who can be invited come first and
unavailable ones after them, five at most (`MaxSuggestions`). An empty field shows four people to start with
(`SuggestedCount`). A complete address that is not in the directory offers "Invite by email". Enter or a click picks the
highlighted person; Enter without one, a comma or a semicolon turns the text into chips.

Pasting a list (or calling `AddText`) recognizes directory names and addresses, new addresses and `Name <address>`; it
skips duplicates and adds entries that cannot be invited as red chips. The hint under the field shows a summary for a few
seconds. Selecting a red chip puts its text back into the field to fix it.

### Sending

Set `SendAction` to the work Send starts. It receives the new pending rows (status `Sending`) and a cancellation token.
Call `MarkDelivered` for each invite as it goes out; the rows that are still sending when the task completes are marked
sent. When someone accepts, call `MarkJoined`: the row says "Joined", leaves a moment later, and the person joins
`Members` with their avatar flying into the header. An exception takes the rows back and returns the chips to the field.

```csharp
// Your mail call; here it only waits.
static Task SendInviteAsync(string email, string? role, CancellationToken token) => Task.Delay(300, token);

invite.SendAction = async (request, token) =>
{
    foreach (var pending in request.Invites)
    {
        await SendInviteAsync(pending.Person!.Email!, pending.Role?.Name, token);
        invite.MarkDelivered(pending);
    }
};
invite.InviteJoined += (_, e) => log.Info($"{e.Invite.Person?.DisplayName} joined");
```

Send is held back (it stays focusable and says why) while chips need fixing or the seats run over; pressing it shakes the
red chips or the over-limit line instead. `ResendAction`, `RevokeAction` and `UpgradeAction` work the same way for the
row buttons and the upgrade button. The demo below fails every second send, and revoking Theo's invite fails, so the row
comes back.

<Demo name="Failure" />

### State and events

`Entries` lists the chips, `SeatsUsed` and `IsOverLimit` can be bound, and `CurrentPlan` is two-way. `EntriesChanged`,
`RoleChanged`, `InvitesSent`, `SendFailed`, `InviteJoined`, `InviteResent`, `InviteRevoked` and `PlanUpgraded` report what
happened. `Reset()` puts the block back as it first appeared and raises `ResetPerformed`.

<Demo name="EventLog" />

### Framed or flat

`IsFramed` (the default) draws a card at most 600 wide with a 1px border and a 16 radius, with lines above the footer and
the pending list. Set it to false to place the block inside a dialog or a pane of your own.

<Demo name="Flat" />

### Narrow containers

The block reads its own width. At 480 or less the paddings tighten from 20 to 16; when the content is narrower than 440
the pending rows put their status under the name and the suggestions leave out the job titles.

<Demo name="Narrow" />

### Other words

Every text is in `Labels` (`InvitePeopleLabels`), in English by default: placeholders, the hint, the error reasons, the
seat line, the button names and every announcement. Texts with values are functions, so word order can change.

## API reference

<!-- api: InvitePeople -->

<!-- api: InvitePeoplePerson -->

<!-- api: InvitePeopleRole -->

<!-- api: InvitePeoplePlan -->

<!-- api: InvitePeoplePendingInvite -->

<!-- api: InvitePeopleEntry -->

<!-- api: InvitePeopleSendRequest -->

## Accessibility

- The text box is named "Email or name" and described by the hint. The suggestion list is not focusable: the text box
  keeps the focus while the highlight moves, and unavailable people say why.
- A red chip's button is named "Edit {entry}. {reason}"; the remove buttons say which chip they remove; a role button says
  whose role it is and what it is now.
- The seat meter is named "Seats used" and its value reads "8 of 10 seats" (with how many are over the limit).
- Send is named by its count ("Send 3 invites"). When it is held back it describes the reason: the error panel's title or
  the seat count over the limit.
- After a revoke the focus moves to the Pending heading. The team avatars are one group ("Team, 6 people"); the "+N" pill
  reads "and N more".
- A polite live region announces each step: added and removed people, the paste summary, a marked chip, role changes,
  why Send is held back, sending and sent, an accepted invite, a resend, a revoke, an upgrade and a reset.

### Keyboard

- **Arrow Down** in the field opens the suggestions (the starting list when it is empty); **Arrow Up** and **Arrow Down**
  move the highlight and skip unavailable people. **Enter** adds the highlighted person, or turns the text into chips when
  nothing is highlighted. **Comma** and **semicolon** turn the text into chips. **Escape** closes the list, then clears
  the text.
- **Backspace** in an empty field marks the last chip; a second **Backspace** removes it. Typing anything clears the mark.
- **Tab** goes through each chip's role and remove buttons, then the field, the paste button, the error panel's button,
  the upgrade button, reset, Send and each pending row's resend and revoke buttons.
- On a role button, **Enter**, **Space** or **Arrow Down** opens the role menu with the focus on the current role.
  **Arrow Up** and **Arrow Down** wrap, **Home** and **End** jump, **Enter** or **Space** picks, and **Escape** closes;
  the focus returns to the role button.

## Motion

- **Avatars fly**: from the suggestion into the new chip, from the chip into its pending row when you send, and from an
  accepted row into the team in the header, each on one spring (about 600 ms) while the source is already gone.
- **Layout**: chips and the text box spring to new places as chips come and go, the field's height and everything under
  it follow on a smooth spring, and pending rows open and close the same way (a closing row also fades in 130 ms).
- **Suggestions**: the list rises 4 px and grows from 0.98; the highlight glides between rows. The role menu enters the
  same way from its button.
- **Numbers and words**: seat counts, the pending count and Send's count roll; words that change (a role, the plan, Send's
  noun, a row's status, "Sent again") cross-fade with a short blur while their width springs.
- **Refusals**: a red chip shakes once when it appears, and the red chips (or the over-limit line) shake when Send is
  pressed while it is held back: 6, 6, 4, 4, 2 px over 420 ms.
- Reduced motion places everything at once and keeps only the short fades and color changes; there are no flights, no
  shakes and no springs. Off applies every change at once.

## Related

- `Team members`, `Roles and permissions`, `Team directory` and `Profile page`, the other People blocks.
- `TagInput` for plain tags, `Avatar` and `AvatarGroup` for portraits, `UsageMeter` for a standalone meter.
