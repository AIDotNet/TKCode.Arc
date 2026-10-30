---
title: Team switcher
summary: A workspace switcher: the trigger shows the current workspace's logo and plan, and the menu searches your workspaces, shows your role and a Cmd/Ctrl+number shortcut for each, and creates or joins one inline.
---

<Demo name="Preview" />

## When to use

`TeamSwitcher` sits at the top of an app sidebar or in a header and moves people between the organizations, teams,
projects or stores they belong to. The trigger shows where they are: the workspace's logo, its name and its plan. The
menu finds a workspace by name, plan or role, shows the role and a keyboard shortcut for each, and creates or joins a
workspace without leaving the page. Switching crossfades the trigger to the new workspace through a short loading state.

- **Multi-tenant apps** where one account belongs to several organizations or teams.
- **Account, project or store pickers** that need search, roles and keyboard shortcuts.
- **Creating a workspace or accepting an invite** without sending people to another page.

## When not to use

- **A short static list in a form**: use `Select` or `SegmentedControl`.
- **Global navigation across pages and commands**: use a command palette.
- **Two or three views of the same data**: use `Tabs`.

## Usage

Add your workspaces and bind the active id to `Value`. Wire `SwitchHandler` to whatever loads a workspace (a session
switch, a route prefetch); the trigger shows the new workspace with a loading state until your task finishes. Every
delegate you leave out is simulated. With no workspaces (and no simulation, below) the trigger says "No workspace" and
the menu lists no workspace above Create workspace and Join with invite (the Empty tab above).

```xml
<arc:TeamSwitcher xmlns:arc="https://github.com/AIDotNet/TKCode.Arc" Value="acme">
    <arc:TeamSwitcherWorkspace Id="acme" Name="Acme" Plan="Enterprise" Members="214" Role="Admin" />
    <arc:TeamSwitcherWorkspace Id="acme-labs" Name="Acme Labs" Plan="Pro" Members="12" Role="Owner" />
</arc:TeamSwitcher>
```

```csharp
switcher.SwitchHandler = (workspace, token) => session.SwitchAsync(workspace.Id, token);
switcher.ValueChanged += (_, e) => navigation.GoHome(e.Id);
```

### Anatomy

1. **Trigger**: the workspace's tile, its name over the plan line ("Enterprise · 214 members") and a chevron that becomes
   a spinner while a workspace loads. It is 52px high and fills its container up to 360px. The menu under it is as wide
   as the trigger, and at least 288px so names stay readable when the switcher sits in a narrow sidebar.
2. **Search**: filters the list by name, plan or role as you type. Focus stays here while you move through the list.
3. **Rows**: six 48px rows show at once and the list scrolls beyond that without a visible scrollbar. Each row has the
   tile, the name and plan line (both truncate), your role, the shortcut and a check on the active workspace.
4. **Actions**: Create workspace and Join with invite, under a hairline.
5. **Highlight**: one quiet block that glides to the row under the pointer or the keyboard.

### Your workspaces

<Demo name="YourWorkspaces" />

Each `TeamSwitcherWorkspace` has an `Id` and a `Name`, and optionally a `Plan`, `Members` and your `Role`. Without a mark
its tile shows the first letter of the name in a square tinted from the theme's accent and series colors (the tint is
picked from the name, so a workspace keeps its color). Give a tenant's brand mark as `Logo` (a filled geometry in a
24 × 24 box) on its `TileBackground`, or its logo image (a bitmap or a `DrawingImage`) as `LogoImage`, which fills the
tile. `TeamSwitcherTile` draws the same tile on its own, for example in a settings page.

### Switching

Picking a row, pressing Enter on it or pressing its shortcut closes the menu and starts the switch. The trigger shows
the new workspace at once with a shimmering "Switching…" and a spinner while `SwitchHandler` runs, then its plan once
the task finishes; `Value` changes and `ValueChanged` is raised only then. If the task throws, the trigger goes back to
the previous workspace and shows the exception's message for a few seconds, and `SwitchFailed` is raised. A newer
switch cancels the token of the older one, and setting `Value` yourself abandons a load in flight.

Call `SwitchToAsync(id)` to switch from code the same way.

### Creating and joining

Create workspace and Join with invite open inline forms that crossfade in place of the list; the panel keeps its height.
When nothing matches the search, Enter opens Create workspace with the search text as the name.

```csharp
switcher.CreateHandler = async (name, token) =>
{
    var created = await api.Workspaces.CreateAsync(name, token);   // throw to show a message under the field
    return new TeamSwitcherWorkspace(created.Id, created.Name, "Free", 1, "Owner");
};
switcher.JoinHandler = (invite, token) => api.Invites.AcceptAsync(invite, token);
switcher.WorkspaceAdded += (_, e) => myWorkspaces.Add(e.Workspace);
```

Return the new workspace to add it and switch to it, or null to just close the menu. The switcher shows a workspace it
created or joined right away; add it to your own list in `WorkspaceAdded` too, the switcher de-duplicates by id. Without
`CreateHandler` the simulated create makes a one-member workspace from the name; without `JoinHandler` the simulated
join refuses every invite unless a simulation lists it.

For a demo or a prototype, leave `Workspaces` empty and give the switcher a `TeamSwitcherSimulation`: its workspaces,
the invites its simulated join accepts (each a `TeamSwitcherSimulationInvite`: a code, matched on its own or at the end
of a link, and the workspace it joins; the join view lists the codes) and the plan and role its simulated create gives.
Your own `Workspaces` set it aside, and setting `Simulation` to null empties the switcher. The preview's workspaces and
invites (ADATUM-4821, TAILSPIN-0917) are invented demo data.

```csharp
switcher.Simulation = new TeamSwitcherSimulation
{
    Workspaces = [new TeamSwitcherWorkspace("acme", "Acme", "Enterprise", 214, "Admin")],
    Invites = [new TeamSwitcherSimulationInvite { Code = "ACME-1234", Workspace = new TeamSwitcherWorkspace("acme-labs", "Acme Labs") }],
    CreatedPlan = "Free",
    CreatedRole = "Owner",
};
```

### Shortcuts

<Demo name="Sidebar" />

The first nine workspaces get Cmd+1 to 9 on macOS and Ctrl+1 to 9 elsewhere. They work while focus is inside the
switcher; `UsesGlobalShortcuts="True"` makes them work anywhere in the window.

### Disabled

<Demo name="Disabled" />

A disabled switcher keeps showing the workspace, does not open and ignores its shortcuts.

### Your own words

`Labels` holds every word the switcher shows or says. Replace the object to localize or reword:

```csharp
switcher.Labels = new TeamSwitcherLabels
{
    Search = "Arbeitsbereich suchen…",
    CreateWorkspace = "Arbeitsbereich erstellen",
    PlanLine = (plan, members) => $"{plan} · {members} Mitglieder",
};
```

### Reduced motion

With reduced motion the menu, the trigger's parts and the forms trade places with plain fades, and the highlight moves
without gliding. With motion off everything changes at once.

### Content guidelines

- Name workspaces the way people know them, not by their id.
- Plans are one word ("Pro", "Enterprise"); roles too ("Owner", "Admin", "Member", "Guest").
- Error messages say what happened and what to do: "That invite has expired. Ask for a new one."

### Best practices

- Keep the switch task short and let the trigger show the loading state; do not block the page.
- Put the switcher where the menu has about 450px below it, or where it can overflow.
- Keep the order of the list stable so the shortcuts stay the same.

### Common questions

**Can the app open the menu?** Set `IsOpen`; that opens it without moving focus. `Open()` opens it and focuses the search.

**What does a simulated action do?** With no `SwitchHandler`, `CreateHandler` or `JoinHandler` it waits `SimulatedDelay` (650ms)
and announces that nothing was sent.

## API reference

<!-- api: TeamSwitcher -->

<!-- api: TeamSwitcherWorkspace -->

<!-- api: TeamSwitcherTile -->

<!-- api: TeamSwitcherLabels -->

<!-- api: TeamSwitcherSimulation -->

<!-- api: TeamSwitcherSimulationInvite -->

<!-- api: TeamSwitcherValueChangedEventArgs -->

<!-- api: TeamSwitcherSwitchFailedEventArgs -->

<!-- api: TeamSwitcherWorkspaceAddedEventArgs -->

<!-- api: TeamSwitcherOpenChangedEventArgs -->

<!-- api: TeamSwitcherAddKind -->

## Accessibility

- The trigger is a button that reports whether the menu is open. Its name reads the workspace and its plan ("Northwind,
  Enterprise plan"), and its status says "Switching…" while a workspace loads.
- The search field keeps focus while the arrows move through the list; its help text names the highlighted row, so
  screen readers hear it without focus leaving the field.
- The active workspace is marked with a check and "current" in its name, not by color alone. Errors pair an alert icon
  with text.
- A polite live region announces a switch starting, its result, and when a simulated action did the work ("Simulated,
  nothing was sent"). With no workspace the trigger is named "No workspace".
- Closing with Escape or by choosing gives focus back to the trigger. Tabbing out or clicking outside closes the menu.

### Keyboard

| Keys | Action |
|---|---|
| Enter, Space, Down arrow | On the trigger, opens the menu and moves focus to the search field |
| Typing | Filters by name, plan or role; with no match, Enter opens Create workspace with the name filled in |
| Down arrow, Up arrow | Move the highlight through the workspaces and the actions, wrapping at the ends |
| Home, End | Jump to the first or last item when the search is empty |
| Enter | Switches to the highlighted workspace, or opens Create workspace or Join with invite |
| Cmd/Ctrl+1 to 9 | Switches to the workspace at that place in the list (inside the switcher, or anywhere with global shortcuts) |
| Escape | Goes back from a form, then clears the search, then closes and returns focus to the trigger |
| Tab | Leaves the menu and closes it |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Menu opens | Fades and scales in from 98% over 180ms | Fades over 120ms | At once |
| Menu closes | Fades and scales to 98% over 120ms | Fades over 120ms | At once |
| Highlight moves | Glides on a bounce-free 0.24s spring; appears in place, never slides in from the top | Jumps | Jumps |
| Switching | The logo, name and plan line each crossfade: out over 100ms, in over 160ms rising 2px; then a shimmer and a spinner | Fades over 120ms, no shimmer | At once |
| Create or join opens | The views crossfade (100ms out, 160ms in) shifting 6px in the direction of travel | Fades over 120ms | At once |

Nothing resizes: the trigger keeps its height through every state, and the menu's panel keeps its height when you search
or open a form.

## Related

- `DropdownMenu` for a focused list of actions anchored to a trigger.
- `Popover` for a small anchored surface.
- `Avatar` for a compact marker of a person.
