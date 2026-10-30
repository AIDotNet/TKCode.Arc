---
title: Provider status
summary: A status card for the AI providers your product depends on: health as an icon and label, median latency and a 30 day uptime bar with a tooltip per day, incidents that open into their updates, and a visible countdown to the next check.
---

<Demo name="Preview" />

## When to use

`ProviderStatus` is a card that lists the AI providers your product routes to. Each row shows the provider's mark, its
health as an icon and a label, the median (p50) latency and a 30 day uptime bar of small ticks with a tooltip for each
day. A row with an incident is a button that opens the incident's updates, newest first. The header sums up health
across the providers and checks again on a visible countdown ring.

- **Live health of the model providers** your product depends on, on a settings page or an admin dashboard.
- **Explaining degraded AI features** to people or admins, with the provider's incident updates in place.
- **Routing and fallback settings**, where people need to see which providers are healthy right now.

## When not to use

- **A full public status page** with components, subscriptions and history: build a page, not a card.
- **One provider's state inside a chat header**: use `AgentStatus` or a small badge.
- **Detailed latency over time**: use a chart.

## Usage

Give it your providers and a `RefreshHandler` that fetches fresh status from your own aggregation endpoint. Do not poll
the providers' status pages from the client. The handler returns the new providers, or updates `Providers` itself and
returns `null`. It runs on the countdown and from the refresh button; a newer check cancels the older one through the
token.

```csharp
card.Providers = await statusApi.GetProvidersAsync();
card.RefreshHandler = async token =>
{
    var response = await http.GetAsync("/api/provider-status", token);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("Couldn’t reach the status feeds. Showing the last results.");
    }
    return await response.Content.ReadFromJsonAsync<List<ProviderStatusItem>>(token);
};
card.IncidentOpened += (_, e) => OpenBrowser(e.Incident.Url);
card.Footnote = "Latency is the median over the last 5 minutes.";
```

Without providers, a handler or a simulation the card shows its empty state: the header reads "No providers" and the
refresh controls hide.

### Simulated checks

For a demo or a prototype, leave `Providers` and `RefreshHandler` unset and give the card a `ProviderStatusSimulation`
with a story of `ProviderStatusSimulationStep`s. The card shows the first step at once and runs a simulated check every
`Interval` (8 seconds): each check takes `CheckDelay` (900ms) and moves to the next step, looping, so incidents open, post
updates and resolve; a step with `Fails` shows the error state and keeps the last results. Its Status page link says the
action is simulated, and without a `Footnote` the card notes that the data is a sample.

```csharp
card.Simulation = new ProviderStatusSimulation
{
    Steps =
    [
        new ProviderStatusSimulationStep { Providers = beforeTheIncident },
        new ProviderStatusSimulationStep { Providers = incidentOpened },
        new ProviderStatusSimulationStep { Fails = true },
        new ProviderStatusSimulationStep { Providers = resolved },
    ],
};
card.Footnote = "Sample data: the incidents and numbers here are invented.";
```

The preview plays such a story with real provider names and the status page components their pages list; every incident,
latency and uptime figure in it is invented, never a provider's real history, and its footnote says so.

### Your providers

<Demo name="OwnData" />

Each `ProviderStatusItem` has an `Id` (rows are reused while it stays), a `Name`, a `Detail` line such as the models you
call, its `Health`, the median latency in `LatencyMs` and its `Days`. The card formats the numbers (412 ms, 1.92 s,
99.61%), so keep them as plain numbers.

`Mark` is a 24 × 24 stroked glyph drawn at 16px in the text color; pass your provider's logo path, and `MarkBrush` to
color it. Without a mark the row shows the name's first letter in a tinted square.

### Uptime bar

`Days` is the daily history, oldest first. The bar shows the 30 days ending on the last one, which is labeled Today, and
pads missing days as No data. Each day has a `Date`, an `Uptime` from 0 to 100 (or null), an optional `Health` (derived
from the uptime when left out: below 99.9 degraded, below 99 an outage) and an optional `Note` for its tooltip. The
percentage beside the bar is the mean of the days with data.

One tooltip serves the whole card: pointing at a tick, tapping it or dragging sideways across the bar moves it from day
to day.

### Incidents

Set `Incident` on a provider to make its row a button that opens the incident's `Updates`. Each update has a `Stage`
(Investigating, Identified, Monitoring, Resolved), a time in `At` (shown as HH:MM UTC) or a preformatted `AtText`, and a
`Message`. Updates show newest first; a new one fades in at the top while the panel grows.

`ExpandedId` names the open provider and is two-way; `ExpandedChanged` reports the person opening and closing rows. The
Status page link under the updates raises `IncidentOpened`. When nothing handles it, the link opens the incident's
`Url`; with neither, the link is hidden.

### Checks and errors

`RefreshInterval` sets the time between automatic checks (30 seconds by default). The countdown ring drains toward the
next check and pauses while the card is off screen or hidden. While a check runs, a short spinner and a shimmering
"Checking" take its place. When the handler throws, the header shows the exception's message, the last results stay on
screen and the countdown turns into a retry that waits at most 10 seconds. `RefreshInterval="{x:Null}"` turns
automatic checks off and keeps the refresh button. Without a handler the refresh controls are hidden. `CheckNow()`
checks at once.

### States

<Demo name="States" />

`IsLoading` shows skeleton rows with the same row height for the first load. `ErrorText` shows an error of your own in
the header, such as a failed first load.

### Narrow cards

<Demo name="Narrow" />

From 560px of card width each row is one line with columns for status, p50 latency, the uptime bar and the 30 day
percentage. Narrower cards take two lines per row: the name and the health above, with the latency under the name, and
a full-width bar with its percentage below. The rule follows the card, not the window, and holds up at 360px; long
names and details end with an ellipsis.

### Your own words

`Labels` holds every word the card shows or says; numbers, dates and sentences with values are functions:

```csharp
card.Labels = new ProviderStatusLabels
{
    Operational = "Opérationnel",
    Degraded = "Dégradé",
    Outage = "Panne",
    NextCheck = seconds => $"Prochaine vérification dans {seconds} s",
};
```

### Best practices

- Wire `RefreshHandler` to your own endpoint that aggregates the providers' feeds.
- Label sample or synthetic numbers with `Footnote`.
- Keep `Detail` short, such as the models you call; it ends with an ellipsis when space runs out.
- Keep an incident on its provider for a while after it is resolved, so people can read what happened.

## API reference

<!-- api: ProviderStatus -->

<!-- api: ProviderStatusItem -->

<!-- api: ProviderStatusDay -->

<!-- api: ProviderStatusIncident -->

<!-- api: ProviderStatusUpdate -->

<!-- api: ProviderStatusLabels -->

<!-- api: ProviderStatusSimulation -->

<!-- api: ProviderStatusSimulationStep -->

## Accessibility

- The card is a group named by its title. Rows are a list; each row is named by the provider and its health.
- An incident row has a full-row button that reports expanded or collapsed and is named with the provider, its health
  and the incident's title.
- Health is never color alone: every status shows an icon and a label, and each day carries its health in its tooltip
  and in the bar's name.
- Each uptime bar is one tab stop named by a summary of its 30 days. The arrow keys move through the days and a polite
  live region reads each one, so 180 ticks never flood the tab order.
- The same live region announces checks, failed checks, `Notice` and simulated actions ("Simulated, no requests were
  sent").
- Keyboard focus shows a ring just inside the row button and around the bar; pointer focus draws none.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves to the refresh button, each incident row and each uptime bar |
| Enter, Space | On an incident row, opens or closes its updates |
| Arrow Left, Arrow Right | On an uptime bar, moves the day tooltip one day back or forward |
| Home, End | On an uptime bar, jumps to the oldest day or today |
| Escape | On an uptime bar, hides the day tooltip |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Health, latency, uptime and the summary change | One crossfade in a reserved cell: out in 100ms, in over 160ms with a 2px rise | Crossfade, no rise | At once |
| Incident opens or closes | Height springs to the measured content (0.24s, no bounce), content fades | At once | At once |
| A new update arrives | Fades in at the top while the panel springs to its new height | At once | At once |
| A tick's health changes | Color changes over 240ms | Color changes over 240ms | At once |
| Countdown | The ring drains linearly; the seconds change once a second | Same | Same |
| Checking | A short spinner and a shimmering label | Still label, no spinner | Still label, no spinner |
| Skeleton rows | Pulse | Still | Still |
| Chevron | Turns on a spring | At once | At once |

The countdown pauses while the card is off screen or its window is hidden. Rows have fixed columns, so nothing reflows
when a value changes.

## Related

- `AgentStatus` for the live status of one agent run.
- `ToolCall` for a tool call in an agent transcript.
- `Tooltip` for short supporting text.
