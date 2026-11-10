---
title: Share access
summary: A document share dialog: invite people by email with a role, change or remove each person's access, choose who else can open the link and copy it.
---

<Demo name="Preview" />

## When to use

`ShareAccess` is the content of the "Share" dialog of a document, a board or a folder. At the top, an address field
turns each typed or pasted email into a chip, a picker sets the role the new people get (viewer, commenter or editor)
and Invite adds them to the list. Below it, everyone with access has a row with their avatar, name and address and a
role button whose menu changes the role or removes the person; the owner's row cannot change. "General access" decides
who else can open the link: only the people listed, anyone in the organization or anyone with the link, each with its
own role. The footer copies the link and closes the dialog.

- **Documents, boards and files** that people share with teammates and guests.
- **Workspaces with an organization**: the middle link setting opens the item to colleagues without a public link.
- **Admin pages** that list who can see a resource.

## When not to use

- **Sharing to other apps** (messages, social posts): use the system share sheet or `ReferralCard`'s share buttons.
- **Managing the members of a whole workspace**, with invitations, seats and billing: use a members table.
- **A link and nothing else**: use `CopyButton` beside a read-only field.

## Usage

`ShareAccess` lives in the Special package: register it with `UseArcSpecial()` next to `UseArc()`. Set the document's
name and link, the organization's name for the middle link setting, and the people. Put it in a `Dialog` or a
`Popover`, or inline in a settings page.

```xml
<arc:ShareAccess DocumentName="Spring trail plan"
                 OrganizationName="Fernhollow"
                 Link="https://notes.example/d/7hq2"
                 CurrentUserEmail="mira@fernhollow.example">
    <arc:ShareAccess.People>
        <arc:ShareAccessPerson Name="Mira Holt" Email="mira@fernhollow.example" Role="Owner" />
        <arc:ShareAccessPerson Name="Tobias Brandt" Email="tobias@fernhollow.example" Role="Editor" />
    </arc:ShareAccess.People>
</arc:ShareAccess>
```

Every change takes effect in the control at once and raises an event for the app to save: `PeopleInvited` (the people
added and their role), `RoleChanged`, `AccessRemoved` and `LinkAccessChanged`. `LinkScope`, `LinkRole` and
`InviteRole` are two-way. `DoneRequested` fires when Done is pressed; close the dialog there.

### Sending invites first

Set `InviteHandler` to send the invites before the people are listed. While it runs the Invite button shows a spinner;
when it throws, its message appears under the address field and the chips stay for another try. The demo below waits
0.9 s and refuses any address containing "blocked".

<Demo name="InviteHandler" />

### Narrow dialogs

Below 460px the role picker and the Invite button move under the address field and the padding steps down to 16px.

<Demo name="Narrow" />

### Reduced motion

Under reduced motion rows and the link role picker fade in and out without moving, and labels cross-fade.

<Demo name="ReducedMotion" />

### Anatomy

1. **Heading**: "Share “name”" in the display font.
2. **Invite row**: the address field (`TagInput`), the role picker (`Select`) and the Invite button.
3. **People**: rows of 52px with a small `Avatar`, the name and the address (or "Invite sent"), and the role button with
   its menu; the list scrolls beyond 260px.
4. **General access**: a round tile with a lock, people or globe icon, the scope picker with a line explaining it, and
   the link role picker when the link is open.
5. **Footer**: `CopyButton` "Copy link" and the Done button.

### Content guidelines

- Name the document as people know it; the heading wraps to two lines.
- Give names where you have them; a row without a name shows the address alone.
- Keep `OrganizationName` short ("Fernhollow", not the legal name): it appears inside "Anyone at …".

### Common questions

**What counts as an address?** Text with one @ and a domain with a dot and a top-level part of at least two letters.
"Name &lt;address&gt;" pasted from a mail client keeps the address. Addresses are compared without regard to case.

**Can I invite from code?** `InviteAsync` does what the button does; `ChangeRole` and `RemovePerson` act on a row.

## API reference

<!-- api: ShareAccess -->

<!-- api: ShareAccessPerson -->

<!-- api: ShareAccessLabels -->

<!-- api: ShareAccessInvitedEventArgs -->

<!-- api: ShareAccessRoleChangedEventArgs -->

<!-- api: ShareAccessRemovedEventArgs -->

<!-- api: ShareAccessLinkChangedEventArgs -->

## Accessibility

- The heading is a level 2 heading. The address field, the pickers and the list are named ("Add people by email",
  "Role for new people", "Who can open the link", "People with access").
- Each role button is named with the person and the role, "Role of Tobias Brandt: Editor"; its menu checks the current
  role and ends with "Remove access".
- Refused addresses show their reason under the field. A polite live region announces invites, role changes, removals
  and failures.
- After a removal by keyboard, focus moves to the next person's role button, or to the address field when none is left.
- The scope tile is decoration; the picker and its line carry the meaning.

### Keyboard

| Key | Action |
|---|---|
| Enter, comma | In the address field, turns the typed address into a chip |
| Enter on an empty field, Ctrl/Cmd+Enter | Sends the invites |
| Backspace | In an empty field, picks the last chip, then removes it |
| Tab | Moves through the field, the role picker, Invite, each role button, the link pickers, Copy link and Done |
| Enter / Space / Down | On a role button or picker, opens its menu |
| Escape | Closes an open menu or picker |

## Motion

- New people rise 6px into a row that grows open; removed people fade while their row closes.
- The role button's label swaps with Arc's label swap; the scope icon pops, its line swaps the same way, and the link
  role picker fades in while its slot widens.
- The chips, pickers, menus and buttons keep their own motion.
- Reduced motion keeps only fades; with motion off everything is instant.

## Responsive behavior

- The dialog is fluid up to 520px. Below 460px the invite row stacks (field on top, role and Invite below) and the
  padding is 16px.
- Names and addresses end with an ellipsis; the role buttons keep their size.
- Every button is at least 36px high and the role menus open on tap; nothing depends on hover.

## Related

- `TagInput`: the address field on its own.
- `Select`: the role and scope pickers.
- `AvatarGroup`: shows who has access in a compact row.
- `CopyButton`: copies a value with feedback.
- `Dialog`: hosts the share dialog.
