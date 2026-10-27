---
title: Roles and permissions
summary: A matrix of roles and permissions with group checkboxes that ripple, locked owner access, role copies and a review bar that saves every change at once.
---

<Demo name="Preview" />

## When to use

`RolesPermissions` is the access page of a workspace's settings: roles across the top, permissions down the side in
groups, and one switch where they meet. Nothing is applied while the person edits. Each changed cell gets a small dot,
the role's header gets one too, and a review bar slides up with the count, the roles the changes touch and how many
people they reach. The person saves everything at once, or throws it away.

- **Role-based access in a product's settings.** Owner, admin, member and guest style roles over a few groups of
  permissions (documents, team, billing, security).
- **Reviewing a change before it lands.** The bar says "Lead and Member · reaches 13 people" before anything is saved,
  so the person sees the reach of a change while making it.
- **Shaping a new role from an existing one.** Copy a role from its header menu, adjust the copy's column, and save the
  new role with the rest.

## When not to use

- **Assigning people to roles.** The matrix decides what a role may do, not who holds it. Use `Team members` for the
  people and their roles.
- **A handful of independent settings.** A few on/off options read better as switch rows on a `Settings page`.
- **Hundreds of permissions.** Every cell is drawn, none are virtualized. Group long lists and start most groups folded
  (`IsExpanded="False"`), or split the page.
- **A person's own security.** Passwords, two-step sign-in and sessions belong in `Security settings`.

## Usage

The block derives from Avalonia's `TemplatedControl`. Roles are `RolesPermissionsRole` objects (a key, a name, the member
count and a line for its menu; `IsLocked` for the owner), groups are `RolesPermissionsGroup` objects holding
`RolesPermissionsItem` permissions, and the grants are a set of `RolesPermissionsGrant` pairs of role key and permission key.
`SavedGrants` is the saved state; `Grants` is the current one and is written back on every change.

```xml
<arc:RolesPermissions Title="Roles &amp; access" WorkspaceName="Northwind Studio" MemberCount="31"
                      RolesSource="{Binding Roles}" GroupsSource="{Binding Groups}"
                      SavedGrants="{Binding SavedGrants}" Grants="{Binding Grants, Mode=TwoWay}" />
```

```csharp
matrix.Roles.Add(new RolesPermissionsRole("founder", "Founder", 1, "Lena Hart", isLocked: true));
matrix.Roles.Add(new RolesPermissionsRole("lead", "Lead", 4, "Omar Diaz, Priya Rao and 2 others"));
matrix.Groups.Add(new RolesPermissionsGroup("team", "Team",
    new("team.invite", "Send invitations"), new("team.roles", "Assign roles"), new("team.remove", "Remove people")));
matrix.SavedGrants = new HashSet<RolesPermissionsGrant> { new("lead", "team.invite") };
```

### Anatomy

- **Header**: `Title`, a subtitle with the workspace and two counts that roll when they change (members from
  `MemberCount`, or the number of `Members`; roles from the columns), and the members' avatars on the right. When the
  avatars do not fit beside the titles they move under them.
- **Header row**: the "Permission" caption over the label column and one button per role: its name (a lock leads a locked
  role), its member count, and a chevron that turns while its menu is open.
- **Group rows**: a disclosure button with the group's name and permission count, and one checkbox per role that is on
  when every permission of the group is on and mixed when only some are.
- **Permission rows**: the permission's name (it wraps when the column is narrow) and one switch per role.
- **Footer**: two legends, the lock and the unsaved dot.
- **Review bar**: the count, the roles and the people they reach, the Discard and Save buttons. It opens under the
  footer when there is something to save.

### Cells, groups and locked roles

A cell switches with a click, **Space** or **Enter**. A group checkbox turns every permission of the group on when it is
off or mixed, and off when it is on; the cells that change follow one another 60 ms apart, in group order. A locked role's
cells are always on: pressing one shakes the box and announces why. A cell switched back to its saved value is no longer
a change.

`GrantChanged` reports every cell that changes, with the role, the permission, the new value, whether it is now unsaved
and what changed it (a cell, a group, an undo, Discard or code). `GroupToggled` follows a group checkbox. `SetGrant`,
`ToggleGroup`, `IsGranted` and `IsUnsaved` do the same from code.

### Copying and removing roles

Each header opens a menu with the role's name and description. **Copy role** inserts "Guest (copy)" right of the source
("Guest (copy 2)" when that name is taken) with the source's permissions; its column springs open and the focus moves to
its header. A copy is new until it is saved: it reads "New role", counts as one change, and its own edits count and show
dots as well. **Undo edits** puts the role's cells back; **Remove role** (new roles only) closes the column again. Copies
are inserted into `Roles`, or into `RolesSource` when that is a list you can add to. `RoleDuplicated`, `RoleDeleted` and
`ChangesReverted` report each step; `DuplicateRole`, `DeleteRole` and `RevertRole` do it from code.

### Saving

Save awaits `SaveHandler` with a `RolesPermissionsSaveRequest`: every grant after the save, the cells that changed, the
roles and the new roles, and the count the bar showed. While it runs the button spins and the matrix takes no changes (a
person who tries hears "Saving, one moment"). Then the dots leave together, the bar reads "Access updated" with a check for
`SavedHoldDuration` (1.6 s) and closes, and `SavedGrants` holds the new baseline. Discard throws every change away at once,
the cells rippling back row by row.

```csharp
matrix.SaveHandler = async (request, cancellation) =>
{
    // Send request.Grants (and request.AddedRoles) to your server; throw to report a failure.
    await Task.Delay(900, cancellation);
};
matrix.Saved += (_, e) => Debug.WriteLine($"{e.Request.ChangeCount} access changes saved");
```

An exception keeps every change: the bar reads "Couldn't save" with the message (or `ErrorFormatter`'s text), Save becomes
"Try again", and the next edit clears the failure. `CancelSave` cancels the handler's token and returns to the review.

<Demo name="Failure" />

### Bound state and events

`Grants` is two-way: bind it to keep a model in step, or write a new set to change the cells from outside.
`PendingChangeCount`, `HasUnsavedChanges` and `SaveState` follow the review bar. Folding a group writes its `IsExpanded`
back, so a page can fold or unfold every group.

<Demo name="Bound" />

### Framed or flat

`IsFramed` (the default) draws a card with a 1px border and a 16 radius, as on this page. Set it to false for a full
settings page: no frame, and a page margin of 32 (16 below 540).

<Demo name="Flat" />

### Narrow containers

The block reads its own content width. From 760 down, the label column needs 200 and the roles 92 each; below 540, 148 and
80, the group counts and header chevrons go, the permission names shrink to 12 and wrap, and the bar drops the word
"pending". When the roles do not fit, they scroll sideways under a sticky label column that shows its edge while scrolled;
a copied role scrolls itself into view.

<Demo name="Narrow" />

### Other words

Every visible and announced text is in `Labels`, a `RolesPermissionsLabels` object with English defaults: the subtitle
format, "1 person" and "{0} people", the menu rows, the copy names, the legends, the review bar's words and formats,
the screen reader names and every announcement. Replace it with an object initializer that sets only what changes.

## API reference

<!-- api: RolesPermissions -->

<!-- api: RolesPermissionsRole -->

<!-- api: RolesPermissionsGroup -->

<!-- api: RolesPermissionsItem -->

<!-- api: RolesPermissionsGrant -->

<!-- api: RolesPermissionsSaveRequest -->

<!-- api: RolesPermissionsSaveState -->

<!-- api: RolesPermissionsLabels -->

## Accessibility

- The matrix is one **Tab** stop. It starts on the first role header and remembers the last part the person worked on;
  **Tab** then moves on to the review bar's buttons.
- **Left** and **Right** move along a row and stop at its ends; on a group row, **Left** from the first checkbox reaches
  the group's button.
- **Up** and **Down** move to the row above or below in the same column, snapping to the nearest column when the row has
  none there (from a group button down to the first role); folded rows are skipped.
- **Home** and **End** go to the first and last part of the row; **Ctrl+Home** and **Ctrl+End** to the first role header
  and the last cell of the matrix.
- **Space** or **Enter** switches a cell, folds a group, or opens a role's menu. **Escape** closes the menu and returns
  to its header.
- After Save and Discard the focus goes back to the last cell worked on; after a copy to the new role's header; after
  Remove role to the header on the left.
- The matrix is a data grid named by the title. Group checkboxes are tri-state checkboxes named "Lead: all Team
  permissions"; permission cells are switches named "Lead: Send invitations". Locked cells add ", always on"; unsaved
  cells add ", unsaved".
- A role header is a button with a menu, named by the role and its member count, plus ", permissions are fixed" for a
  locked role and ", unsaved changes" when it has changes. A group button reports whether it is expanded.
- Every switch, group toggle, copy, removal, undo, discard and save is announced politely in a sentence; a failed save
  is announced assertively. The review bar is a region named "Unsaved changes".
- The header's avatars are one group named "Workspace members".
- Hover is never needed: the column band and the row tint follow the keyboard focus as well as the pointer.

## Motion

- **Cells**: the box fills in 160 ms and the check draws itself in about 100 ms. The unsaved dot pops from 0.3 on a quick
  spring. A pointer press presses the box to 0.88 and lets it back on the CSS spring. Group checkboxes ripple their cells
  60 ms apart; Discard and Undo edits ripple 30 ms per row down the whole matrix; a save clears every dot at once.
- **Locked cells** shake sideways (3, −2, 1 px) over 330 ms.
- **Column band**: fades in over 160 ms on the hovered or focused role and slides between roles on the smooth spring.
  The hovered row tints in 120 ms, its sticky label too.
- **Groups** fold and unfold on the smooth spring with a short fade; the chevron turns on a slightly bouncy spring.
- **Copies** open their column from zero width on the smooth spring as they fade in; a removed role fades and closes.
- **Review bar**: its space opens in 240 ms while the bar rises from 24 px below on the smooth spring and fades in. The
  count rolls, "change" and "changes" morph letter by letter, and the second line swaps with a short blur. The Save
  button spins while saving, then morphs to "Saved" as its width springs.
- Reduced motion and Off apply every change at once, as Arc's reduced mode does.

## Related

- `Team members`, `Invite people` and `Team directory`, the other People blocks.
- `Settings page` for a page of independent switches with a save bar.
- `Checkbox` and `Switch` for single controls of the same look.
- `DropdownMenu` and `AvatarGroup`, used by the role headers and the header.
