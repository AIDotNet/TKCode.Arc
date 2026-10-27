---
title: Customer galaxy
summary: Every customer as a dot sized by revenue and colored by health, turning as a galaxy; group them into clusters, fly in, and read the totals.
---

<Demo name="Preview" />

## When to use

`CustomerGalaxy` draws every customer account as one dot: the dot's area grows with the account's monthly recurring
revenue (MRR) and its color shows the account's health. In the overview the dots form a tilted, slowly turning spiral, with
the largest accounts packed in the core and the long tail of small ones trailing in two arms. Pick a grouping and every
dot springs into one packed cluster per value, labelled with its MRR and its number of accounts. A totals panel beside the
stage (under it in narrow containers) adds up everything, or the cluster you fly into, and lists the clusters with a bar
that splits each one by health. Use it when the question is where revenue and risk sit across thousands of accounts.

- **A customer success home page**, where the team opens the week by grouping by health and flying into the at-risk
  cluster. The big red dots are the renewals to call first; hovering one shows its owner and a one-line note.
- **A revenue review**, where finance groups by plan and reads how much of the MRR the largest tier carries, then switches
  the list to accounts to see how many customers each tier has.
- **A regional sales dashboard**, grouped by region, where each manager flies into their own cluster and steps through
  its accounts from the largest down with the arrow keys.
- **A product analytics page** that groups customers by industry to see which markets bring large accounts and which
  bring many small ones, and where the growing ones are.
- **A board deck screen** that stays on the overview: the turning galaxy with its totals reads at a glance and invites
  someone to click in.

## When not to use

- **Comparing a handful of numbers.** Use `BarChart` or `DonutChart`; a galaxy pays off with hundreds or thousands of
  accounts, not with five.
- **Reading exact values of many accounts side by side.** Use `Table` or `DataGrid`; the galaxy shows one
  account at a time in its card.
- **Trends over time.** Use `LineChart` or `Streamgraph`. The galaxy is a snapshot: its clusters do not move with dates.
- **A small widget in a sidebar or a card.** The block needs at least 360 px of stage height to be read; use
  `MetricCard` with a `Sparkline` instead.
- **Showing parts of a whole by one dimension only.** Use `Treemap` or `WaffleChart`, which keep exact areas without
  packing gaps.

## Usage

Give the control its accounts and the groupings you want as tabs. Each `GalaxyAccount` carries a name, its MRR, its
health key and, in `Groups`, its value for every grouping. The control lays out the spiral and the clusters, adds up the
totals and builds the list itself.

```xml
<arc:CustomerGalaxy Title="Where the revenue sits"
                    ItemsSource="{Binding Accounts}">
    <arc:GalaxyGrouping Key="plan" Header="Plan">
        <arc:GalaxyGroupDefinition Key="solo" Label="Solo" />
        <arc:GalaxyGroupDefinition Key="team" Label="Team" />
        <arc:GalaxyGroupDefinition Key="business" Label="Business" />
        <arc:GalaxyGroupDefinition Key="corporate" Label="Corporate" />
    </arc:GalaxyGrouping>
    <arc:GalaxyGrouping Key="region" Header="Region" />
    <arc:GalaxyGrouping Key="health" Header="Health" />
</arc:CustomerGalaxy>
```

`CustomerGalaxy` derives from Avalonia's `TemplatedControl`. The stage is drawn by the control itself (one dot per
account, batched by color and dimming level), so it stays fast with thousands of accounts; the labels, the card, the
buttons and the panel are ordinary controls. The control sets the pseudo-classes `:grouped`, `:cluster-focused`,
`:row-hover`, `:side` and `:empty` for styling.

### Anatomy

1. **Title and subtitle** (`Title`, `Subtitle`, `SubtitleFormat`): a 16 px medium title and a 14 px muted line. Without a
   `Subtitle` the line reads "2,400 accounts. Dot size shows MRR, color shows health."; an empty string hides it.
2. **Grouping tabs** (`Groupings`, `AllText`): "All" and one tab per grouping on a muted 34 px track; a surface pill glides
   to the chosen tab. The row scrolls sideways when the container is narrower than its tabs.
3. **Legend** (`ShowLegend`, `SizeLegendText`): one colored dot per health state, a divider, and a small and a large muted
   dot for size. It sits right of the tabs when both fit and moves under them otherwise.
4. **Stage**: the dots on the surface color. In the overview the disc is squashed to 68% of its height and front dots are
   drawn up to 12% larger, so it reads as a disc tilted toward you.
5. **Cluster labels**: the cluster's name over its MRR (rolling) and number of accounts, 10 px above each cluster.
6. **Card** (`AccountTooltipTemplate`): the account's name, its grouping values, MRR, owner, health and note, beside the
   pointer or pinned next to the selected dot.
7. **Gesture hint** (`ShowHint`, `HintText`): a muted line in the stage's lower left corner. It fades out while a cluster
   is focused or a list row is hovered.
8. **Zoom bar** (`ShowZoomControls`): zoom out, zoom in and fit, 12 px from the lower right corner. Fit is dimmed while
   the view already shows everything.
9. **"Show all"** (`ShowAllText`): a pill in the upper left corner while a cluster is focused.
10. **Totals panel** (`ShowTotals`, `TotalsPlacement`): the scope's name, the large MRR, the accounts, growing and at-risk
    subtotals, and the list of clusters with its MRR / Accounts switch.

### Accounts

Add `GalaxyAccount` objects inline in `Items`, or set `ItemsSource` to a list; when the list is an observable collection,
the galaxy follows its changes. The selection and the focused cluster survive a change when their account and cluster
still exist. When you change an account in place, call `Refresh()`.

- `Id` is the account's stable key; the selection matches by it. `Name` is shown on the card and announced.
- `Mrr` sets the dot's area: its radius is `0.30 + 0.028 × √MRR` px at the fitted zoom, at least 0.74 px.
- `Health` is the key of one of the `HealthStates`; accounts with an unknown key draw with the neutral state.
- `Groups` holds the account's value for each grouping, by the grouping's key (`["plan"] = "business"`).
- `Owner` and `Note` fill the card; `Tag` is yours.

```csharp
var account = new GalaxyAccount
{
    Id = "acct-0042",
    Name = "Juniper Labs",
    Mrr = 18_400,
    Health = "growing",
    Owner = "Rhea Ingram",
    Note = "Upgrade call booked"
};
account.Groups["plan"] = "business";
account.Groups["region"] = "harbor";
accounts.Add(account);
```

### Groupings

Each `GalaxyGrouping` adds a tab. Its `Key` names the `Groups` entry it reads; the key `health` groups by `Health` and
takes its clusters, labels and colors from `HealthStates`. List `GalaxyGroupDefinition`s to give the values labels and a
fixed order for ties; values the accounts carry that are not listed are added after them, and accounts without a value
form an "Other" cluster (`OtherText`). `Header` is the tab text and `ListTitle` the list's title ("By plan" is built from
`ListTitleFormat` when it is not set).

`SelectedGroupingKey` is two-way: null shows the overview. When it changes, every dot springs from where it is to its slot
in the new layout and the camera fits the new layout; the labels fade in 120 ms later. Clusters are packed largest
account first, the largest cluster in the middle and the others around it by MRR (left, right, lower left, upper right,
upper left …), with room for their labels. The overview's list is by health; choosing one of its rows switches to the
health grouping and flies into that cluster.

<Demo name="Clusters" />

### Health states

`HealthStates` holds four states by default, in legend order: Growing (`Success`), Steady (`TextMuted` at 72%), Cooling
(`Warning`) and At risk (`Danger`). Each state names the ArcDesign color it draws with in `Tone` (or a fixed `Color`),
how strongly its dots and bar segments are drawn (`DotOpacity`, `BarOpacity`), and its paint order (`Layer`: steady dots
first, at-risk dots on top). The state with `IsGrowth` gives the "growing" subtotal and the one with `IsRisk` the "at risk"
subtotal and every cluster's risk share. The colors are read from the theme again on every theme or accent change.

```csharp
galaxy.HealthStates.Clear();
galaxy.HealthStates.Add(new GalaxyHealthState { Key = "expanding", Label = "Expanding", Tone = GalaxyTone.Success, Layer = 1, IsGrowth = true });
galaxy.HealthStates.Add(new GalaxyHealthState { Key = "stable", Label = "Stable", Tone = GalaxyTone.Neutral, DotOpacity = 0.72, BarOpacity = 0.5 });
galaxy.HealthStates.Add(new GalaxyHealthState { Key = "churning", Label = "Churning", Tone = GalaxyTone.Danger, Layer = 2, IsRisk = true });
```

### Focusing a cluster and selecting an account

Double-click a cluster (or its label), choose its row in the list, press ← or →, or set `FocusedGroupKey` to fly into it:
the cluster, its 10 px gap and its label fill three quarters of the stage height, every other cluster fades to a seventh of
its opacity and its label to 30%, "Show all" appears, and the panel switches to the cluster's numbers. `FlyToGroup(key)`,
`FocusNextGroup()` and `FocusPreviousGroup()` do the same from code, and `GroupFocused` reports every change.
`ResetView()`, "Show all", Fit, 0 and Home return to every cluster.

Hover a dot to see its card beside the pointer; click it to select it (`SelectedAccount`, two-way, and `AccountSelected`).
The selected dot keeps a ring and its card stays open: beside the pointer while the pointer is on the stage, pinned 12 px
right of the dot when the pointer leaves. A click on empty space or Escape clears the selection. ↓ and ↑ (or
`SelectNextAccount()` and `SelectPreviousAccount()`) step through the focused cluster's accounts from the largest down;
`FlyToAccount(account)` selects an account and zooms to it.

<Demo name="FocusedCluster" />

```csharp
galaxy.GroupFocused += (_, e) =>
{
    // e.Group is null when the view returned to every cluster.
    status.Text = e.Group is { } group ? $"{group.Label}: {group.AccountCount} accounts" : "All clusters";
};
```

### Camera

Drag to pan: the stage follows the pointer 1:1 and stretches with a rubber band past the content's edge, then springs
back; there is no glide. The mouse wheel zooms by 1.246 per notch about the pointer; trackpad scrolling still scrolls the
page, and Ctrl or ⌘ with the wheel or a pinch zooms too. The zoom buttons and the + and − keys zoom by `ZoomStep` (1.6)
about the center. The view never zooms out past the fitted view and never in past `MaxZoom` (9) times it. After a wheel
zoom the view waits 1.1 s before it slides back inside the content. `ZoomBy(factor, anchor)` zooms from code and `Zoom`
reports the current zoom (1 = fitted).

### Totals and the list

The panel names its scope ("All accounts", or the focused cluster), shows the MRR in large rolling digits and three
subtotals: accounts, growing MRR and at-risk MRR. The list has one row per cluster (per health state in the overview):
the name, the value, a 4 px bar and a caption. With `GroupSort` set to `Mrr` the rows are ordered and their bars measured
by MRR, and the caption counts the accounts; with `Accounts` they are ordered and measured by the number of accounts and
the caption shows the MRR. Outside the health grouping the caption adds the share of the cluster's MRR at risk, and the
bar is split into one segment per health state. Hovering a row dims every other cluster without moving the camera;
choosing it flies into the cluster. `Summary` exposes the same numbers for your own text.

### Layout and container width

The layout follows the control's own width, not the window's:

| Control width | Totals | Stage height |
|---|---|---|
| 880 px and wider | Beside the stage, 288 px wide | 16:9 of the stage's width, at most 560, at least the panel's height |
| 620 to 880 px | Under the stage, in two columns | `max(360, width × 0.52)` |
| Under 620 px | Under the stage, in one column | 380 |

`TotalsPlacement` set to `End` keeps the panel beside the stage at any width, and `Bottom` keeps it under the stage.
The fitted zoom follows the stage size, so a wide stage draws a larger galaxy; labels keep their size.

<Demo name="SidePanel" />

<Demo name="Narrow" />

### Your own data

Any customer list works: workspaces, stores, practices. Bring your own groupings, health states and wording, and format
amounts with `CurrencySymbol` and `CultureName`, or with your own `MrrFormatter`.

<Demo name="YourData" />

```xml
<arc:CustomerGalaxy CurrencySymbol="€"
                    AllAccountsText="All workspaces"
                    AccountCountFormat="{}{0} workspaces"
                    OneAccountFormat="{}{0} workspace"
                    SizeLegendText="Size is revenue"
                    ShowHint="False" />
```

### Texts

Every text has an English default and a property to change it. Formats use `{0}` for the value they name.

| Property | Default | Used for |
|---|---|---|
| `SubtitleFormat` | "{0} accounts. Dot size shows MRR, color shows health." | The subtitle when `Subtitle` is null |
| `AllText` | "All" | The overview tab |
| `AllAccountsText` | "All accounts" | The panel's scope when no cluster is focused |
| `MrrCaptionText` | "Monthly recurring revenue" | The caption over the large number |
| `MrrText` | "MRR" | The sort button and the card's first row |
| `AccountsText` | "Accounts" | The subtotal and the sort button |
| `OwnerText` | "Owner" | The card's owner row |
| `HealthText` | "Health" | The card's health row |
| `ListTitleFormat` | "By {0}" | The list title; `{0}` is the grouping in lower case |
| `HealthGroupingText` | "Health" | The health grouping's name (overview list, announcements) |
| `AccountCountFormat` | "{0} accounts" | Account counts |
| `OneAccountFormat` | "{0} account" | One account |
| `AtRiskShareFormat` | "{0} at risk" | The risk share in a row's caption |
| `HintText` | "Drag to move · scroll to zoom · double-click a cluster to open it" | The gesture hint |
| `ShowAllText` | "Show all" | The pill that returns from a cluster |
| `ZoomInText`, `ZoomOutText`, `FitText` | "Zoom in", "Zoom out", "Show every account" | The zoom buttons' names |
| `SizeLegendText` | "Size shows MRR" | The legend's size entry |
| `GroupByText` | "Group accounts by" | The tabs' name |
| `SortByText` | "Order the list by" | The MRR / Accounts switch's name |
| `OtherText` | "Other" | The cluster of accounts without a value |
| `EmptyText` | "No accounts to show" | The stage without accounts |
| `FlyToClusterText`, `GroupAndFlyFormat` | "Fly to this cluster", "Group by {0} and fly to this cluster" | A row's action for screen readers |
| `KeyboardHelpText`, `StageSummaryFormat`, `OverviewLayoutText`, `GroupedLayoutFormat` | (sentences) | The stage's screen-reader name and help |
| `GroupAnnouncementFormat`, `AccountAnnouncementFormat`, `GroupingAnnouncementFormat`, `OverviewAnnouncementText` | (sentences) | Live announcements |

### Data shape and performance

- The control reads `Id`, `Name`, `Mrr`, `Health`, `Owner`, `Note` and the `Groups` of each account when the data
  changes, and works on its own copy in flat arrays; a frame of the turning galaxy allocates nothing.
- 2,400 dots draw in about 3 ms per frame in the headless Skia test host on a development laptop, and moving a grouping
  or the camera costs a fraction of a millisecond more. Expect several times that in a browser (WebAssembly); keep the
  data to a few thousand accounts there.
- Only dots inside the stage are drawn, so a zoomed-in view draws few.
- The stage asks for frames only while something moves. It stops when the pointer rests on it, when it leaves the screen,
  and at reduced motion, where the galaxy is one still picture.

## API reference

<!-- api: CustomerGalaxy -->

<!-- api: GalaxyAccount -->

<!-- api: GalaxyGrouping -->

<!-- api: GalaxyGroupDefinition -->

<!-- api: GalaxyHealthState -->

## Accessibility

- The stage is a focusable custom control described as an "interactive chart". Its name summarizes the view ("Customer
  galaxy of 2,400 accounts and $6.05M in monthly recurring revenue, grouped by plan …") and its help text lists the keys
  (`KeyboardHelpText`). The focus ring is 2 px outside the stage and shows only for keyboard focus.
- A hidden table stands in for the canvas: one row per cluster of the current grouping with its accounts, MRR and accounts
  per health state.
- A polite live region announces the focused cluster ("Corporate, 70 accounts, $2.80M MRR, $60K at risk.", with your
  numbers), an account chosen with the arrow keys, and a new grouping or reset ("4 clusters by plan. Corporate is the
  largest …").
- The grouping tabs are a tab list: only the chosen tab is a tab stop, and the arrow keys, Home and End move the choice.
  The MRR / Accounts buttons report whether they are pressed, and each list row is a button that reports whether its
  cluster is focused, with its name, MRR, accounts and risk share as its name and its action as help text.
- The zoom buttons are named; Fit is disabled and skipped by Tab while the view already shows everything.
- Rolling numbers are read once, with their final value.
- Colors are never the only signal: the card and the list name every health state, and the legend labels the colors.

### Keyboard

| Keys | Where | Action |
|---|---|---|
| `Tab` | Anywhere | Tabs, stage, "Show all", zoom buttons, MRR / Accounts, list rows |
| `ArrowLeft` / `ArrowRight` | Tabs | Choose the previous or next grouping (wrapping); `Home` / `End` the first or last |
| `ArrowRight` / `ArrowLeft` | Stage, grouped | Fly to the next or previous cluster by MRR (wrapping; the largest or smallest first) |
| `ArrowDown` / `ArrowUp` | Stage | Select the next or previous account by MRR in the focused cluster (in all accounts otherwise); the card pins beside it |
| `+`, `=` / `−` | Stage | Zoom in or out by 1.6 about the center |
| `0`, `Home` | Stage | Return to every cluster |
| `Escape` | Stage | Clear the selection; pressed again, return to every cluster |
| `Enter`, `Space` | Rows, buttons | Choose the row (fly to its cluster) or press the button |

## Motion

- The overview and every cluster turn clockwise at 1.6° per second (one turn in about 224 s), and every dot of the overview
  drifts a few pixels over a few minutes. While the pointer rests on the stage the turning eases to a stop (about 0.4 s)
  and the stage stops drawing; it picks up again when the pointer leaves.
- A grouping change springs every dot to its new slot: about 70% set off at once, the rest up to a third of a second later;
  each first wanders slowly, then spirals in, swelling slightly while it flies. Most dots arrive within a second. The
  camera fits the new layout on the same timing and the new labels rise into place 120 ms later.
- Flights into a cluster, "Show all" and Fit move the camera on a slow critical spring (about 1 s); wheel, button and key
  zooms on a quicker one (about 0.55 s), keeping the point under the pointer still. Drags follow the pointer exactly.
- Dimming fades the other clusters to a seventh in about a quarter of a second; a hovered dot grows to 1.84 times its size
  with a ring 3 px around it.
- The card fades in and grows from 96%; the numbers roll digit by digit, 26 ms apart from the left; the bars and their
  segments ease to their new widths; reordered rows slide to their places; the tab pill glides.
- Reduced motion draws one still galaxy: no turning, no drift, and every grouping change, flight, zoom, dimming and hover
  lands at once; the card and the labels only fade. Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Overview and clusters turn, dots drift | 1.6°/s, eases out under a resting pointer | Still | Still |
| Grouping change | Each dot springs to its slot with a staggered start | Lands at once | Immediate |
| Flight into a cluster, "Show all", Fit | Critical spring, about 1 s | Lands at once | Immediate |
| Wheel, button or key zoom | Critical spring, about 0.55 s, anchored | Lands at once | Immediate |
| Drag past the edge, then release | Rubber band, springs back | Rubber band, lands inside | Rubber band, lands inside |
| Dimming other clusters | Fades in about 0.26 s, 7 steps | At once | Immediate |
| Hovered dot | Grows to 1.84× in about 0.4 s | At once | Immediate |
| Cluster labels | Rise 4 px and fade in after 120 ms; dim to 30% over 240 ms | Fade | Immediate |
| Card | Fades in and grows from 96% | Fades | Immediate |
| Numbers | Digits roll, 26 ms apart | Change at once | Immediate |
| Bars and segments | Ease to their widths (580 ms) | Change at once | Immediate |
| Rows reordered by the sort switch | Slide to their places (about 0.55 s) | Jump | Immediate |
| Tab pill | Glides on a snappy spring | Jumps | Immediate |
| Gesture hint | Fades out over 480 ms | Fades quickly | Immediate |

## Related

- `BarChart`, `DonutChart` and `Treemap` for a few exact values or parts of a whole.
- `DataGrid` and `Table` for reading and comparing accounts row by row.
- `MetricCard` and `AnimatedCounter` for one headline number.
- `SegmentedControl`, which the grouping tabs resemble.

| Component | Choose it when |
|---|---|
| `Treemap` | You want exact areas for parts of a whole, without the packing gaps |
| `BarChart` | A handful of categories are compared by one value |
| `DataGrid` | People need to read, sort and edit the accounts themselves |
| `MetricCard` | A dashboard tile shows one number and its change |
| `ActivityHeatmap` | Activity over days and hours matters more than who the customers are |
