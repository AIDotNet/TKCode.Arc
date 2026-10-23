---
title: Semantic zoom
summary: An organization map you zoom through from teams to projects to a single task, each level showing new detail, with breadcrumbs, a minimap, a guided tour and the numbers of whatever is in view.
---

<Demo name="Preview" />

## When to use

`SemanticZoom` draws a whole organization as one map you can zoom into without losing your place. Teams are panels,
projects are cards inside them, tasks are blocks on the cards, and a task opens into its details and checklist. Each zoom
level shows different information rather than the same picture larger: at the overview you see the teams with their
progress and a gray chip per task (red when blocked); closer in, the team leads and counts, then project titles, health
and due dates, then task titles and due dates, and finally the task's summary, assignee and checklist. The breadcrumbs, the
details panel and the screen-reader text always describe whatever is under the center of the view, so the map tells you
where you are while you fly.

- **A portfolio overview** for a leadership meeting: start at the whole company, fly into the team that is behind, then
  into its off-track project, and read which tasks are blocked.
- **A workspace home page** where everyone sees the same map: the overview answers "how are we doing", the zoom answers
  "what is next for my project" without changing pages.
- **A status walk-through** with the guided tour: the map flies through a team, a project and two tasks on its own while
  someone presents.
- **An onboarding view** that shows a newcomer how the organization is built: who leads which team, which projects each
  team runs, who works on what.
- **A planning check-in** where people tick checklist items right on the map and watch the task, the project and the
  team's numbers update.

## When not to use

- **Browsing a deep hierarchy item by item.** Use `TreeView`; it shows any depth as rows and is faster to scan than a map.
- **Working through tasks.** Use a board or a task list; the map is for orientation, not for moving work.
- **Flat data clustered by an attribute.** Use `CustomerGalaxy`; semantic zoom needs a hierarchy of containers.
- **Exact comparisons across many projects.** Use `DataGrid` or `SortableDataTable`; the map shows one node's numbers at a
  time.
- **A small card or a sidebar.** The map needs at least 400 px of height and room around it for the page to scroll (the
  wheel always zooms the map).

## Usage

Give the control an `Organization`: a tree of `SemanticZoomTeam`s, `SemanticZoomProject`s and `SemanticZoomTask`s. The
layout, the totals (open and blocked tasks, next due date, progress, people) and every label are derived from it, so you
only describe the work itself.

```xml
<arc:SemanticZoom Header="Network map">
    <arc:SemanticZoom.Resources>
        <arc:SemanticZoomPerson x:Key="Rustam" Name="Rustam Aliyev" Role="Fleet lead" />
        <arc:SemanticZoomPerson x:Key="Hana" Name="Hana Sato" Role="Charging specialist" />
    </arc:SemanticZoom.Resources>
    <arc:SemanticZoomOrganization Title="Harbor Line Transit">
        <arc:SemanticZoomTeam Title="Fleet" Description="Vehicles, charging and everything that wears out." Lead="{StaticResource Rustam}">
            <arc:SemanticZoomProject Title="Depot charging bays" Lead="{StaticResource Hana}" Health="AtRisk">
                <arc:SemanticZoomTask Title="Order chargers" Status="InProgress" Assignee="{StaticResource Hana}" Points="3">
                    <arc:SemanticZoomChecklistItem Text="Get three quotes" IsDone="True" />
                    <arc:SemanticZoomChecklistItem Text="Check the grid connection" />
                </arc:SemanticZoomTask>
            </arc:SemanticZoomProject>
        </arc:SemanticZoomTeam>
    </arc:SemanticZoomOrganization>
</arc:SemanticZoom>
```

`SemanticZoom` derives from Avalonia's `TemplatedControl`. The map is drawn by the control itself (one pass per frame, with
cached text and brushes, and nothing drawn while it rests); the breadcrumbs, the buttons, the hover card and the details
panel are ordinary controls. The control sets the pseudo-classes `:fit`, `:touring`, `:side-panel`, `:compact` and
`:interacted` for styling.

### Anatomy

1. **Heading** (`Header`, `Subtitle`, `SubtitleFormat`): a 16 px medium heading and a 13 px muted line. Without a
   `Subtitle` the line reads "5 teams, 22 projects, 151 tasks. Zoom in to see more."; an empty string hides it.
2. **Map**: team panels on a dot grid whose spacing repeats every fourfold zoom, so the background reads as depth.
3. **Breadcrumbs** (`IsBreadcrumbVisible`): every level above the current one is a button that flies back to it.
4. **Tour button** (`IsTourButtonVisible`): starts and stops the guided tour; only its icon in narrow containers.
5. **Hover card**: the name, a status or description line and four numbers of the team, project or task under the
   pointer (or the keyboard cursor).
6. **Minimap** (`IsMinimapVisible`): appears once you leave the overview; press or drag it to move the view.
7. **Hint** (`IsHintVisible`): a muted gesture hint that fades away after the first interaction.
8. **Zoom bar** (`IsZoomControlsVisible`): zoom out, the zoom readout, zoom in, and show everything (disabled while
   everything is in view).
9. **Details panel** (`DetailPanelPlacement`, also usable on its own as `SemanticZoomPanel`): the kind and name of the
   node in view, its open and blocked tasks and next due date, its progress, and its people.

### The data

| Type | What it holds |
|---|---|
| `SemanticZoomOrganization` | The root: `Title` and its `Teams` |
| `SemanticZoomTeam` | `Title`, `Description` (shown in its hover card), `Lead` and its `Projects` |
| `SemanticZoomProject` | `Title`, `Description` (used in generated task summaries), `Lead`, `Health`, `Due` and its `Tasks` |
| `SemanticZoomTask` | `Title`, `Status`, `Assignee`, `Reviewer`, `Due`, `Points`, `CommentCount`, `Summary` and its `Checklist` |
| `SemanticZoomChecklistItem` | `Text` and `IsDone` |
| `SemanticZoomPerson` | `Name`, `Role`, an optional `Avatar` picture and optional `Initials` |

Every node exposes its totals as read-only properties: `OpenTasks`, `BlockedTasks`, `DoneTasks`, `TaskCount`, `NextDue`
(the earliest due date of an open task), `Progress` (a task's share of ticked checklist items, 1 when done; projects, teams
and the organization average every task below them), `People` (leads first, then assignees, without repeats),
`ProjectCount` and `NeedAttention` (projects not on track). They update as soon as anything below them changes.

A task without a `Summary` gets one generated from its status, assignee, due date and project. People without an
`Avatar` show their initials on a muted circle.

### Moving around

Scroll or pinch to zoom about the pointer (1× shows everything, up to 53× by default, `MaxZoom`), drag to pan (the view
glides on after a flick and springs back from the edges), and click a team, project or task to fly into it. A click on the
node you are already in, or one of its ancestors, does nothing; the next click goes one level deeper or sideways. Flights
first ease out a little and then in, and take longer the farther they go.

The current node (`Scope`, two-way) is not a separate selection: it is always the deepest node under the center of the
view that the zoom allows (teams from 1.1×, projects from 2.4×, tasks from 18×). Set `Scope` or call `FlyTo` to fly
somewhere; `FlyOut`, `ResetView` and `ZoomBy` cover the rest. `ScopeChanged` fires during flights and pans too, with
`IsUserInitiated` telling a person's input apart from code and the tour.

### Ticking checklist items

Zoomed far into a task, its checklist appears; click an item, or move the keyboard cursor onto it and press Enter, to tick
or untick it. Ticking the last open item marks the task done; unticking an item of a done task puts it back in review.
The task's block, its project's card, the team and the details panel all update at once, with the numbers rolling to their
new values. `ChecklistItemToggled` reports every change.

### The guided tour

The tour button (or `ToggleTourCommand`, `StartTour`, `IsTourPlaying`) flies through a sequence of stops and waits
`TourDwell` (1.5 s) at each. By default it visits the first team, one of its projects, two tasks of its next project, a
second team and one of its projects, and returns to the whole map. Set `TourStops` for your own route; the organization as
a stop means everything. Any manual input stops the tour where it is.

<Demo name="GuidedTour" />

### Layout and container width

The map is 48% of the control's width high, between 400 and 580 px (or `StageHeight`). Above 900 px inside, the details
panel moves beside the map (264 px); under it otherwise, in two columns. In containers narrower than 600 px the map is 420
px high, the tour button shows only its icon, the minimap shrinks, the hint moves above the zoom bar and the panel uses one
column. The layout of the map itself never changes with the width: narrow containers show it smaller.

<Demo name="SidePanel" />

<Demo name="Narrow" />

### Your own data

The whole tree can be written in XAML, as here, or built in code and assigned to `Organization`. Teams are packed into
rows that share one card height, and each row is stretched to the widest one, so any mix of team sizes stays tidy.

<Demo name="YourData" />

### Texts and formats

Every label, button name and announcement is in `Texts` (`SemanticZoomTexts`, English defaults): kinds and panel labels,
status and health names, the lines drawn on the map, the generated summaries, the hover card rows, the tour and zoom
button names, the hint, and the screen-reader announcements. Assign a new instance to change them. Dates ("Sep 23") and
percentages ("59%") come from `Formatter` (`ISemanticZoomFormatter`; `SemanticZoomFormatter` has a `Culture`).

## API reference

<!-- api: SemanticZoom -->

<!-- api: SemanticZoomPanel -->

<!-- api: SemanticZoomOrganization -->

<!-- api: SemanticZoomTeam -->

<!-- api: SemanticZoomProject -->

<!-- api: SemanticZoomTask -->

<!-- api: SemanticZoomChecklistItem -->

<!-- api: SemanticZoomPerson -->

## Accessibility

- The map is one focusable element with the role "zoomable map". Its name sums up the organization and the node in view
  ("Map of Harbor Line Transit: 5 teams, 22 projects, 151 tasks. Showing Fleet. …"), and its help text lists the keys.
- A polite live region announces the node in view once the camera settles ("Now showing Fleet team: 5 projects, 31 open
  tasks, 52% done."), the item under the keyboard cursor, ticked and unticked checklist items with the task's new count
  and status, and the tour starting and stopping.
- A hidden table lists every project with its team, health, progress, open and blocked tasks and due date, so a screen
  reader can read the whole portfolio without zooming.
- The breadcrumbs are a named group of buttons; the tour button reports whether it is pressed; the zoom buttons are named
  and the fit button is disabled and skipped by Tab while everything is in view; the panel is a named group and its
  avatar stack is named with everyone's names.
- Rolling numbers are read once, with their final value. Health and status never rely on color alone: the hover card and
  the map name them in text.

### Keyboard

| Keys | Where | Action |
|---|---|---|
| `Tab` | Anywhere | Map, breadcrumbs, tour button, zoom buttons |
| `ArrowLeft` / `ArrowRight` / `ArrowUp` / `ArrowDown` | Map | Move the cursor to the nearest item that way among the current node's children and siblings (in a task, its checklist items); the first press picks the first child |
| `Enter`, `Space` | Map | Fly into the cursor's item, or tick the checklist item under it |
| `Escape` | Map | Stop the tour; clear the cursor; pressed again, zoom out one level |
| `Backspace` | Map | Zoom out one level |
| `+`, `=` / `−` | Map | Zoom in or out by 1.5 about the center |
| `0`, `Home` | Map | Show the whole organization |

## Motion

- Flights follow a smooth zoom-and-pan path (zoom out a little, travel, zoom in) on an ease-in-out curve, in
  460 ms plus 380 ms per unit of path length: about 0.6 s into a team, 1.1 s from a project into a task.
- Wheel, pinch, button and key zooms follow a critically damped spring (about 0.2 s to settle) and keep the point under
  the pointer still. Zooming past the limits stretches a little and springs back; dragging past the edges stretches and
  springs back on release; a flick glides on and slows down over about a second.
- Every level of detail fades in over a window of on-screen size, and eases over about 140 ms when the zoom jumps. Nodes
  outside the one in view dim (teams to 42%, neighbouring projects and tasks to 50%); hover outlines fade in about 0.1 s.
- The hover card fades in and grows from 96%; the minimap rises in when you leave the overview; the hint fades out after
  the first interaction; breadcrumbs fade in (240 ms) and out (120 ms); the panel's name cross-fades and its numbers roll
  digit by digit; the progress bar eases to its new width; new avatars fade in.
- The tour waits 1.5 s at each stop. At reduced motion the camera jumps to every stop instead and each stop lasts 2.4 s.
- Reduced motion lands every flight, zoom, glide, dimming and fade at once and drops the interface transitions; Off does
  the same. Checklist ticks and the totals update at every level.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Flight to a node, back, fit | Zoom-and-pan path, 460 + 380·S ms | Lands at once | Immediate |
| Wheel, pinch, button or key zoom | Spring, about 0.2 s, anchored | Lands at once | Immediate |
| Drag past the edge, then release | Rubber band, springs back | Rubber band, lands inside | Rubber band, lands inside |
| Flick | Glides on (τ ≈ 0.27 s) | Stops | Stops |
| Level of detail | Fades by on-screen size, eased over 140 ms | By on-screen size | By on-screen size |
| Dimming and hover outline | Eased (140 ms / 90 ms) | At once | Immediate |
| Hover card | Fades in, grows from 96% | Fades | Immediate |
| Minimap | Fades in, rises 6 px | Appears | Immediate |
| Breadcrumbs | Fade in 240 ms, out 120 ms | Change at once | Immediate |
| Panel numbers and bar | Digits roll, bar eases (580 ms) | Change at once | Immediate |
| Tour | Flies, 1.5 s at each stop | Jumps, 2.4 s at each stop | Jumps, 2.4 s at each stop |

## Related

- `TreeView` for browsing any hierarchy as rows.
- `CustomerGalaxy` for flat data grouped into clusters by an attribute.
- `DataGrid` and `SortableDataTable` for comparing projects side by side.
- `Breadcrumb` for a path outside a map.

| Component | Choose it when |
|---|---|
| `TreeView` | People expand and collapse a deep hierarchy to find one item |
| `CustomerGalaxy` | Thousands of items are grouped by an attribute, not nested |
| `DataGrid` | People need to read, sort and edit the projects themselves |
| `MetricCard` | A dashboard tile shows one number and its change |
