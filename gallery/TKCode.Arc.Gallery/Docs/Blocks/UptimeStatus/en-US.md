---
title: Uptime status
summary: A status page for a set of services: 90 days of daily bars with uptime figures and a hover card per day, a response-time chart with p50 and p95 curves against a target for the chosen service, and an incident timeline, all derived from the services' samples and the incidents, with keyboard paging by a week.
---

<Demo name="Preview" />

## When to use

`UptimeStatus` is a public or internal status page in one block. It answers three questions in the order people ask them:
is everything working right now (the header), how reliable has each service been (one bar per day for 90 days, with an
uptime figure), and what happened and when (the incident timeline). Choosing a service charts its response time over the last
day, so a slow Tuesday in the bars can be matched with a latency curve and an incident.

- **A status page for customers or the team.** The header turns from a tick to an exclamation mark and says what is wrong
  while an incident is open; every row says which service it is.
- **A service-level review.** The bars and the uptime figures are computed from the incidents you give it, so the page
  cannot disagree with the incident list.
- **A runbook companion.** The latency chart puts the p95 against the target, so "slow" is a number with a line next to it.

## When not to use

- **Real-time monitoring.** The block shows daily history and half-hourly samples; it does not poll anything. Feed it from your
  monitoring system and let it follow the data you change.
- **One metric over time.** Use a line chart or the `Metric explorer` block when there is no service, incident or target
  to relate it to.
- **Hundreds of services.** The page is made for roughly 3 to 10 rows; group the rest before showing them.
- **Per-minute detail.** A bar is a whole UTC day. Show an incident's exact timing in its timeline.

## Usage

The block derives from Avalonia's `TemplatedControl` and contains no data. Give it `Services` and `Incidents`:

```xml
<arc:UptimeStatus PageName="Larkspur status" UpdatedAt="2027-03-14T14:32:00Z">
    <arc:UptimeService Key="edge" Name="Edge" Description="Routing and TLS" TargetP95="350" />
    <arc:UptimeService Key="console" Name="Console" Description="console.larkspur.test" TargetP95="900" />
</arc:UptimeStatus>
```

```csharp
status.Services.AddRange(services);                        // each with 48 half-hourly LatencySamples
status.Incidents.Add(new UptimeIncident
{
    Title = "Elevated 5xx on Edge",
    Severity = ServiceStatus.Outage,
    Start = DateTimeOffset.Parse("2027-02-20T07:40:00Z"),
    Duration = TimeSpan.FromMinutes(19),
    ServiceKeys = { "edge" },
    Updates = { new IncidentUpdate("Investigating", start, "Some requests fail.", IncidentTone.Warning) }
});
```

`ServicesSource` and `IncidentsSource` take collections you own instead (an `ObservableCollection` is followed). The block
also follows changes to the services' samples and to every property of an incident, and shows a burst of changes together on
the next turn of the UI thread.

### What is derived

Everything on the page follows from the data, so there is nothing to keep in step by hand:

- **A day's uptime** is 1 minus the minutes of incidents that fell on that UTC day over 1,440; an incident across midnight is
  split between the two days. The hover card writes it with two decimals ("98.40%").
- **A day's status** is the most severe incident of the day: no incident is operational, a degraded one is a 65% bar, an outage
  a 35% bar. Degraded and outage minutes both count as downtime.
- **A service's uptime** is 1 minus all its minutes over `DayCount` times 1,440 ("99.95%" for 69 minutes in 90 days).
- **A service's state** (the badge on its row, the title) comes from its unresolved incidents. A resolved incident leaves its
  day's bar degraded, because the day had an incident, but the page is operational again.
- **The count** under "Incidents" is the incidents of the window, and the list is newest first.

`Today` is the last day of the window (default: the day of the newest latency sample) and `DayCount` the number of bars.

### The bars

Each bar band is as wide as the row. The bar width is `round((W - 2 (n - 1)) / n)`, never under 3, and bar *i* starts at
`round(i (W - w) / (n - 1))`: a 1,112px band gets 10px bars with 2 or 3px gaps, a 472px band 3px bars. Pointing at the band
snaps to the nearest bar (the card follows the bar, not the pointer), dims the other bars of that service to 38% and opens the
hover card above the band: the service and date with the day's uptime, the status, and each incident of the day with its
minutes, or "No incidents recorded". The card is 256 wide, sits 8 above the band, is kept 8 from the section's edges and may
cover the row above.

### The response-time chart

Choose a service by its name, by clicking its bars, or with Enter or Space on its bars; `SelectedService` is two-way.
The chart shows the last 48 half-hour samples: the band between p95 and p50, the p95 curve (2px, accent), the p50 curve (1.5px)
and a dashed line at `TargetP95`. Both curves are monotone cubic, so they never overshoot the data. The y axis starts at 0
and ends at the nice number above `ChartHeadroom` (1.2) times the larger of the target and the highest p95: a target of 400
with a highest p95 of 266 gives 0 to 500 in steps of 100; one of 1,000 with 612 gives 0 to 1,250 in steps of 250. The three
numbers above the chart (p95, p50, target) roll to the new service's values.

Pointing at the chart (or pressing → or ← on the focused chart) snaps a crosshair with a dot per curve to the nearest sample
and replaces the numbers and the time at the top right with that sample's, without rolling; leaving restores the newest.

<Demo name="Events" />

### Incidents

Each incident is a button with its severity mark, title, meta line ("Major outage · Feb 20, 07:40 UTC · 19 min · Edge"), a
status (a badge while it goes on, "Resolved" afterwards) and a chevron. Opening it shows the updates, newest first, with the
newest dot filled in its `IncidentUpdate.Tone` and older ones hollow, and the line about the person leading the response.
`IsExpanded` is two-way and several incidents can be open together.

### A status that changes

Add an incident whose `IsResolved` is false and the block changes everything the incident touches: the header icon turns into an
exclamation mark and the title swaps to `DegradedTitle` (or `OutageTitle`), the affected service's row gets a badge, today's bar
fades to degraded and shrinks, its uptime rolls, the count rolls and a new incident grows into the top of the list. Set
`IsResolved`, `Duration` and add a "Resolved" update and the page returns to operational, the badge goes, today's bar stays
degraded and the newest timeline entry grows in. Removing the incident takes it all back. `StatusSummary` is the line next to the
page name while something is wrong ("Events delayed since 13:58 UTC"); without it the line reads "Updated …" from `UpdatedAt`.

The Preview demo drives exactly this from a button; the simulation lives in the demo's code, not in the block.

### Header action

`ActionContent` is whatever you want at the right of the header (a button, a link). Under a block width of 560 it moves under
the text.

### Other examples

<Demo name="AllGreen" />

<Demo name="Outage" />

### Narrow containers

The block reads its own width, not the window's. Under 860 the two lower sections stack; under 640 the legend moves under the
hint; under 560 the paddings tighten to 16 and 14, the header action drops under the text and a row's description and the word
"uptime" are hidden. The bars keep a minimum of 3px, so on a phone the gaps shrink.

<Demo name="Compact" />

### Words and formats

Every word is in `Labels` (an `UptimeStatusLabels`), the three titles are properties, and `LatencyFormatter`,
`PercentFormatter` and `DateFormatter` change how numbers and days are written in the hover card, the chart's tick labels and
the announcements.

<Demo name="Formats" />

## API reference

<!-- api: UptimeStatus -->

<!-- api: UptimeService -->

<!-- api: LatencySample -->

<!-- api: UptimeIncident -->

<!-- api: IncidentUpdate -->

<!-- api: UptimePerson -->

<!-- api: UptimeDay -->

<!-- api: UptimeDayIncident -->

<!-- api: UptimeStatusLabels -->

<!-- api: UptimeServiceEventArgs -->

<!-- api: UptimeDayEventArgs -->

<!-- api: UptimePointEventArgs -->

<!-- api: UptimeIncidentEventArgs -->

<!-- api: ServiceStatus -->

<!-- api: IncidentTone -->

## Accessibility

- Each service has a button named with its uptime and state ("Edge, 99.95% uptime, Operational") that is pressed while the
  service is charted, and a focusable group for its bars, named "Edge, daily status for the last 90 days" and described with the
  uptime and the incidents of the window.
- Inspecting a day with the keyboard announces it politely: "Sat, Feb 20: Major outage, 98.68% uptime. Elevated 5xx on Edge."
  Inspecting a sample announces "Mar 14, 14:00 UTC: p50 80 ms, p95 210 ms." The hover card itself is hidden from assistive
  technology; the announcements carry its content.
- The chart is a focusable group with a text summary (the newest p50 and p95, the highest p95 and when, the target) and a hidden
  table with a row per half hour.
- Incident buttons say whether they are expanded; each timeline is a list named "Updates, newest first" that reads every update.
- A separate status region announces an incident that appears and one that is resolved, and `Announce` lets you say your own
  sentence.
- Colour is never the only signal: bars differ in height as well as colour, badges and the header icon carry text and shape.
- The focus ring is 2px, 2px outside, on the bars, the chart, the service buttons and the incident buttons, for keyboard focus only.

### Keyboard

- **Tab** moves through the header action, then each service's name button and its bars, then the chart, then each incident.
- On the **bars**, focusing from the keyboard inspects the newest day. **← →** move by a day, **PageUp / PageDown** by seven days,
  **Home / End** to the ends, stopping there. **Escape** clears (the next **←** starts from the day before the newest) and
  **Enter / Space** chart the service.
- On the **chart**, focusing does not inspect. **→** shows the newest sample and **←** the one before it; then **← →** move by a half
  hour and **Home / End** jump to the ends. Page keys do nothing. **Escape** clears.
- On an **incident**, **Enter / Space** open or close it.

## Motion

- **Bars**: pointing dims the other bars to 38% in 120ms (Standard) while a full-strength copy of the inspected one stays on top.
  A day that changes status fades its colour in 240ms and its height springs (k 170, c 27).
- **Hover card**: fades in over 160ms from 97% scale; moving between bars it follows on a stiff spring (k 925, c 58, about
  240ms), also from row to row; leaving, it fades out in 120ms to 98%.
- **Choosing a service**: the highlight glides to the row (k 150, c 24, about half a second) while the curves, the band, the
  target line and the y domain spring together (k 171, c 26); tick labels cross-fade, the subtitle rises out and in, and the
  three numbers roll. Kept ticks slide with the domain.
- **Header**: the icon's line ends spring between a tick and an exclamation mark (k 160, c 26) while its colours fade in 240ms;
  title and summary swap with a short rise (about 200ms), and the colour of badges fades in 240ms.
- **Incidents**: a new incident grows from zero height on a spring while it fades in over 200ms; a removed one fades out in 130ms
  while it collapses. A timeline entry grows in on a spring (k 180, c 27). Opening an incident springs its height (k 171) and
  fades its content in 240ms; the chevron turns half way round on a quick spring.
- Reduced motion keeps the short fades (hover card, dimming, tick labels that leave) and lands springs, rolling numbers and the
  morph at once. Off applies everything at once.

## Related

- `Metrics dashboard`, `KPI drilldown` and `Metric explorer`, the other Analytics blocks.
- `Badge` and `Avatar`, the small parts the status badges and the response lead are in the spirit of.
- `Release readiness`, a Projects block with a similar state-changing header.
