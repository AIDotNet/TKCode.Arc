---
title: Live visitors
summary: A realtime visitors card where the live count rolls digit by digit, new arrivals ping in on a dotted world map, the top pages reorder with bars, and signups and upgrades tick in.
---

<Demo name="Preview" />

## When to use

`LiveVisitors` answers "who is here right now" in one tile: a pulsing live status, the number of people on the site
rolling digit by digit, a dotted world map where each new arrival pings in and fades, the busiest pages with bars, and a
ticker of signups and upgrades with the city, the country and how long ago.

- **Realtime visitors** on a marketing site or in an app, next to the signups and upgrades as they happen.
- **Launch days and campaigns** where a team watches traffic and conversions live.
- **Admin and growth dashboards** that need one glanceable tile for right now.

## When not to use

- **Trends over days or weeks**: use a chart with axes.
- **Precise geography or per-country breakdowns**: use a table or a proper map; the dotted map is decoration.
- **A single number with no context**: use a stat card.

## Usage

With no data and no simulation (below) the card holds its placeholders and its status says No data. As soon as you set
`Count`, it shows only the data you pass. Feed it from your realtime source (a websocket, server-sent events or a two second poll of your
analytics API) by setting new snapshots; the card works out what moved from the ids:

```csharp
card.Count = snapshot.Online;
card.Pages = snapshot.Pages.Select(p => new LiveVisitorsPage(p.Path, p.Visitors)).ToArray();
card.Visitors = snapshot.Arrivals          // the last 5 to 8 seconds of arrivals
    .Select(a => new LiveVisitorsPing(a.Id, a.Latitude, a.Longitude)).ToArray();
card.Events = snapshot.Conversions         // newest first
    .Select(c => new LiveVisitorsEvent(c.Id, c.Label, c.City, c.CountryCode, c.At)).ToArray();
card.Connection = socket.IsOpen ? LiveVisitorsConnection.Live : LiveVisitorsConnection.Reconnecting;
```

### Push events as they arrive

<Demo name="YourData" />

When your stream sends single events rather than snapshots, push them. `PushVisitor` adds one arrival that pings in now
and fades out after six seconds; `PushEvent` adds a conversion at the top of the ticker and, with a latitude and
longitude, pings it on the map with a stronger ring and labels its city for two seconds. Pushed items join whatever is
in `Visitors` and `Events`.

```csharp
socket.OnVisit += v => card.PushVisitor(v.Latitude, v.Longitude);
socket.OnConversion += c => card.PushEvent(new LiveVisitorsEvent(c.Id, "Signed up", c.City, c.Country, c.At)
{
    Latitude = c.Latitude,
    Longitude = c.Longitude,
});
```

### A simulated stream

For a prototype or a demo, leave `Count` unset and give the card a `LiveVisitorsSimulation` of a site you describe:
about `TypicalCount` people spread over `SitePages`, an arrival from one of `Cities` every 600ms, page counts that drift,
and one of `Conversions` every 2.4s, each picked by its weight. The card advances it from its own clock while it is on
screen, live and not paused, and says once that the stream is simulated. Your own `Count` sets it aside, and setting
`Simulation` to null empties the card. The preview's site, cities, pages and conversions are invented demo data.

```csharp
card.Simulation = new LiveVisitorsSimulation
{
    TypicalCount = 128,
    Cities = [new LiveVisitorsSimulationCity("Berlin", "DE", 52.52, 13.40, 4), new LiveVisitorsSimulationCity("Tokyo", "JP", 35.68, 139.69, 3)],
    SitePages = [new LiveVisitorsSimulationPage("/", 34), new LiveVisitorsSimulationPage("/pricing", 22)],
    Conversions = [new LiveVisitorsSimulationConversion("Signed up", 62), new LiveVisitorsSimulationConversion("Upgraded to Pro", 28)],
};
```

### Anatomy

1. **Status**: a dot and a word: Live (the dot pulses), Paused, Connecting or Reconnecting (a warning dot).
2. **Pause**: a toggle that holds the last snapshot. `IsPauseVisible="False"` hides it.
3. **Count**: the number in tabular digits that roll on their own strips, then the words from `Labels.People`.
4. **Map**: a dotted world map from 80°N to 56°S. Arrivals are accent dots with a ring; conversions have a larger dot
   and a stronger ring.
5. **Top pages**: the busiest `MaxPages` pages (4 by default) with bars scaled to the busiest page.
6. **Signups and upgrades**: the newest `MaxEvents` conversions (4 by default) with a country code chip, what happened,
   the city and the time.

### Connection states

`Connection` reflects your socket instead of hiding the card. `Connecting` shows placeholder bars in the same space as
the number and the lists. `Reconnecting` keeps the last data and switches the status to a warning dot with the word
Reconnecting.

### Pausing

The pause button sets `IsPaused` (two-way) and raises `PausedChanged`; use it to pause or close your own subscription.
While paused the card holds its last snapshot whatever you keep passing, and its clock stops. Resuming shows the latest
data. Setting `IsPaused` from code does the same without raising the event.

### Narrow containers

<Demo name="Narrow" />

The card is at most 640px wide and fills narrower containers. Below 520px the two lists stack and the padding drops to
16px; the map scales with the card. Paths and cities end in an ellipsis, while counts and times keep fixed right-aligned
columns.

### Your own words

`Labels` holds every word the card shows or says, including the plural of the count, the short and spoken times and
the country names. Replace the object to localize or reword:

```csharp
card.Labels = new LiveVisitorsLabels
{
    People = n => n == 1 ? "person watching the stream" : "people watching the stream",
    EventsHeading = "New subscribers",
};
```

### Flags

The card shows a country as its ISO alpha-2 code in a small chip and spells it out for screen readers. Pass your own
image in `LiveVisitorsEvent.Flag` to show it instead; it stays decorative.

### Content guidelines

- Keep event labels short and in the past tense: "Signed up", "Upgraded to Pro".
- Never put personal data such as an email address in the ticker; a city and a country are enough.
- Send arrivals as a rolling window of five to eight seconds; the card animates arrivals and departures from the ids.

### Best practices

- Keep ids stable across snapshots so rows update in place and only real changes move.
- Use `Connection` for socket state instead of hiding or emptying the card.
- Pause your own subscription in `PausedChanged` so a paused card costs nothing.

### Common questions

**Can I use the simulated stream elsewhere?** Yes. Give a `LiveVisitorsSimulation` its data, call `Advance` with the time
that passed and read `Count`, `Pages`, `Visitors` and `Events`; `Restart` starts it over. Nothing is fetched or tracked.

**Why does the number not change width while it counts?** The digits are tabular and roll on their own strips; only a new
leading digit (99 to 100) grows in.

## API reference

<!-- api: LiveVisitors -->

<!-- api: LiveVisitorsPage -->

<!-- api: LiveVisitorsPing -->

<!-- api: LiveVisitorsEvent -->

<!-- api: LiveVisitorsLabels -->

<!-- api: LiveVisitorsSimulation -->

<!-- api: LiveVisitorsSimulationCity -->

<!-- api: LiveVisitorsSimulationPage -->

<!-- api: LiveVisitorsSimulationConversion -->

## Accessibility

- The card is a labelled group named by `Labels.Title` ("Live visitors").
- The count is read once as plain text ("128 people on the site now"); the rolling digits are hidden from screen readers,
  and count changes are not announced, so screen readers are not flooded.
- The pause button is a toggle that reports whether it is pressed; its name and tooltip name the action ("Pause live
  updates", "Resume live updates").
- The status is never color alone: the dot always sits next to Live, Paused, Connecting or Reconnecting.
- Each event row reads as one sentence with the country spelled out and the time in words ("Signed up, Berlin, Germany,
  12 seconds ago"); the code chip is decorative. Each page row reads as "/pricing, 24 people".
- The map is decorative and hidden; the same information is in the lists.
- A polite live region reports pause and resume, and says when the stream is simulated.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Reaches the pause button. The map and the lists are read-only. |
| Enter, Space | Pauses or resumes live updates. |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| The count changes | Each digit rolls on its own strip (280ms, ease-out); a new leading digit grows in | Changes at once | Changes at once |
| An arrival | The dot fades in and one ring expands and fades over 900ms | The dot fades in | Appears at once |
| A conversion with a position | A larger dot with a stronger ring, and the city labelled for 2s | The dot and the label fade in | At once; the label still leaves after 2s |
| An arrival expires | Fades out over 400ms | Fades out over 400ms | Leaves at once |
| Rows reorder or a new one arrives | Rows slide by a row height (a bounce-free 0.28s spring), the new row fades in over 160ms, the last one slides out below the clip and fades | Rows fade, nothing slides | At once |
| Page bars | Ease to their new width over 280ms | Ease over 280ms | At once |
| Live dot | A ring leaves it every 1.6s while live and on screen | Still | Still |
| Status word | Crossfades (out 100ms, in 160ms) in a reserved width | Short crossfade | At once |

The card never changes height: the lists hold their rows' space and the map keeps its aspect ratio. The pulse, the
clock and the simulation stop while the card is off screen or paused.

## Related

- `AgentStatus` for the live status of a running agent.
- `ContextWindow` for another live, self-playing data card.
- `AgentTimeline` for a replay of a run with counters that roll as it plays.
