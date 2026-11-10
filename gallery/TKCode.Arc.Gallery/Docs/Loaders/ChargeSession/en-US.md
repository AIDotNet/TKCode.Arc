---
title: Charge session
summary: A live charging card: energy flows from the charger along the cable into the battery, which fills toward a draggable limit while power, energy, time and cost update.
---

<Demo name="Preview" />

## When to use

`ChargeSession` shows one charging session as it happens. A brushed-metal charger is connected by its cable to a battery
pack lying on its side. Small packets of energy leave the charger, run along the cable and through the pack, and land on
the fill front, which brightens a little each time. The pack fills toward a charge limit that you drag along the pack or
the curve below it. The charge curve flattens out near full, and the live point rides on it. Power, energy added, time
to the limit and cost update every frame.

- **EV and fleet apps** that show a live session with a charge limit the driver can change.
- **Operator consoles** where staff watch power, energy and cost for one bay or one vehicle.
- **Product and landing pages** that show how a charging network reports time to full and cost.
- **Any rechargeable device**: a laptop dock, a scooter bay or a battery bank, as long as a session has a start, a limit
  and a price.

## When not to use

- **A static quota or a single percentage**: use `Progress` or a usage meter.
- **One live reading without a session**: use a gauge or a stat.
- **Many sessions at once**: a table or a chart compares them better. Each card runs its own animation loop.
- **A real-time feed of measured power**: the card runs its own simulation from the numbers you give it (see
  [Your own session](#your-own-session)).

## Usage

With nothing set, the card shows an invented rapid charger ("Bay 6 rapid point", an 82 kWh pack and 180 kW peak) that
starts charging at 28% toward an 80% limit as soon as it appears. The preview above uses exactly these defaults.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ChargeSession />
</UserControl>
```

`ChargeSession` derives from Avalonia's `TemplatedControl` and lives in the Special package: register it with
`UseArcSpecial()` next to `UseArcControls()`. It is at most 880 px wide and stretches to the width it gets.

Describe the battery, the charger and the price with plain properties:

```xml
<arc:ChargeSession Title="Bay 12, depot charger"
                   Location="North yard, 350 kW"
                   Capacity="98"
                   MaxPower="350"
                   StartCharge="22"
                   Limit="85"
                   Rate="0.38"
                   Currency="EUR"
                   Locale="de-DE" />
```

`Limit` is two-way: dragging and the arrow keys change it, and setting it moves the marker. `StateChanged` reports every
stop, resume, completion and new session with the charge, energy, cost and limit at that moment, and `LimitChanged`
reports the limit once a drag or a key press has settled (350 ms after the last change).

```csharp
card.LimitChanged += (_, e) => api.SetChargeLimit("bay-12", e.Limit);
card.StateChanged += (_, e) =>
{
    if (e.State == ChargeSessionState.Stopped)
    {
        api.StopSession("bay-12", e.Detail.EnergyAdded, e.Detail.Cost);
    }
};
```

### Events and other currencies

This van charges in euros with German number formats at four times the speed. The log under it shows what a backend
would receive.

<Demo name="FleetEvents" />

`Currency` takes an ISO 4217 code and `Locale` a locale name. The cost uses the locale's own symbol when the currency is
the locale's currency (`de-DE` with `EUR` reads "9,57 €") and a short international symbol otherwise (`en-US` with
`EUR` reads "€9.57"). Energy and power use the same number format. Yen and won have no decimals.

### Anatomy

1. **Header**: the title and the location on the left; on the right, the charge in large tabular figures over the status
   (a lightning icon in the accent with "Charging", a square with "Stopped", a full battery with "Limit reached").
2. **Scene**: the charger with its screen and an LED that dims with the power, the cable with a glowing core, the plug
   nub and the pack. Inside the pack: the fill up to the charge, ten cells, the front line, a tint beyond the limit and
   the dashed limit line. The packets travel on top.
3. **Limit marker**: a pill with the limit on a thin stem that reaches down to the pack. It has a 64 × 44 touch target.
4. **Charge curve**: power against charge, with lines at full and half power. The part already charged this session is
   drawn in the accent over a fading area, the live point rides on the curve, and a dashed line marks the limit. The
   heading shows the reading, for example "180 kW at 43%".
5. **Stats**: power, energy added, time to the limit and cost in a 2 × 2 grid.
6. **Footer**: the hint and the action button (Stop charging, Resume charging or Start new session).

### States

| State | Header | Time stat | Button | Flow |
|---|---|---|---|---|
| `Charging` | Accent lightning, "Charging" | Minutes at full power ("23 min", "1 h 05 min", "Under 1 min") | Outline, Stop charging | Packets flow, the LED and the cable core glow |
| `Stopped` | Square, "Stopped" | "Paused" | Solid, Resume charging | Power drains in about a second, the packets already on their way land, the point drops to the baseline |
| `Complete` | Full battery, "Limit reached" | "Done" | Solid, Start new session | As stopped; the charge holds at exactly the limit |

The button keeps the width of its longest label, so the footer never moves when the state changes.

<Demo name="AccentAndSpeed" />

### Moving the limit

Press anywhere on the pack or the curve and the limit jumps to the pointer. After that it follows the pointer one to one,
with no threshold, no inertia and no rubber band, and it stops at `MinLimit` and 100%. While it is held, the pill grows
slightly and inverts its colors, and the limit line darkens. On touch, a horizontal drag moves the limit and a vertical
swipe still scrolls the page.

- Lowering the limit to or under the charge while charging completes the session at once.
- Raising it above the charge after the session completed resumes charging.
- While stopped, the limit moves but the session stays stopped.

### Your own session

The card runs a simulation of the session; it does not read a charger. One second on screen is forty seconds of
charging at `Speed` 1, so a session from 28% to 80% takes about 25 seconds. The power is `MaxPower` times the charge
curve times an availability level. Availability ramps up when charging starts or resumes and drains when it stops, so
the numbers, the LED, the cable core, the packet pace and the live point fall away together.

- `Speed` multiplies simulated time; 0 holds the session still while the packets keep flowing.
- `IsPaused` freezes the session and the flow on the current frame; the limit and the button still work.
- `TaperAt` moves the start of the taper; `PowerCurve` replaces the curve with your own function from charge (0 to 1) to
  share of the peak power (0 to 1).
- `Stop()`, `Resume()` and `StartNewSession()` do what the button does.
- `Charge`, `Power`, `EnergyAdded`, `Cost`, `MinutesToLimit` and `State` are read-only properties you can bind to.

### Narrow containers

<Demo name="Narrow" />

The card follows its own width, not the window's. From 640 px the curve sits beside the stats and the charge is 72 px
tall. Below 640 px the stats move under the curve, the padding becomes 24 and the charge 52 px. At 420 px and under the
padding becomes 16, the charge 36 px, the stats shrink one step and the button spans the card. The scene keeps its
proportions at every width, while the pill, the labels and the touch target keep their real size.

### Reduced motion

<Demo name="ReducedMotion" />

Under reduced motion the session holds still at its start: the packets rest at even spacing along the path to show the
direction of the flow and, while charging, breathe together in opacity (every 1.8 s) so the card still reads as busy;
the figures show full power, and Stop, Resume and the limit change everything at once. With motion off the packets
rest without breathing.

### Content guidelines

- **Title**: the charger or device people know, short enough for one line: "Bay 6 rapid point", "Fleet van 14".
- **Location**: where it is and its peak power, in the same order on every card: "Lantern Quay depot, 180 kW". Leave it
  empty to hide the line.
- **Hint**: one sentence that says what dragging does. Keep it under about 60 characters so it fits beside the button on
  a wide card.
- **Labels**: every word is in `ChargeSessionLabels`. Sentences with numbers are functions, so a translation can change
  the word order; the numbers arrive already formatted for `Locale`.

### Best practices

- Give the real usable capacity and peak power. The time to the limit is computed from them and from the curve, so a
  wrong capacity makes every other figure wrong too.
- Keep `MinLimit` at the lowest limit your backend accepts, and send the limit to it from `LimitChanged`, not from every
  property change while dragging.
- Use `AccentBrush` only to match a brand color for energy. It recolors the fill, the packets, the LED, the cable core,
  the charged part of the curve and the live point; the rest keeps the theme.
- Show one card per session the user is watching. For a dozen sessions, list them in a table and open a card for the
  one selected.

### Common questions

**Why does the charge creep on for a moment after Stop?** Power drains over about a second instead of dropping to zero,
and the session integrates whatever still flows. The creep is about a tenth of a percent and never passes the limit.

**Why is the time stat not the time on screen?** It is the charging time at full power: 23 minutes of charging take
about 35 seconds on screen at `Speed` 1.

**Can I feed it measured values?** Not directly: the card runs its own session. Keep it in step with your backend by
calling `Stop()`, `Resume()` and `StartNewSession()` and setting `Limit` when your data changes.

## API reference

<!-- api: ChargeSession -->

<!-- api: ChargeSessionState -->

<!-- api: ChargeSessionDetail -->

<!-- api: ChargeSessionStateChangedEventArgs -->

<!-- api: ChargeSessionLimitChangedEventArgs -->

<!-- api: ChargeSessionLabels -->

<!-- api: ChargeSessionSample -->

<!-- api: ChargeSessionSamples -->

## Accessibility

- The card is a group named by `AccessibleLabel` (or the title), and the title is a level 3 heading.
- The limit marker is a slider named "Charge limit", from `MinLimit` to 100, with a value text such as "Charge limit 80%,
  about 23 minutes to go". The value text follows the minutes left while charging.
- Stopping, resuming, reaching the limit, starting a new session and a settled limit change are announced politely with
  the charge, energy and cost at that moment.
- The action button has one accessible name at a time: the label that shows.
- The scene and the curve plot are not exposed to assistive technology. Every value they show is also in the header, the
  stats or the slider.
- The state never relies on color: the header pairs an icon with a word, and the time stat reads "Paused" or "Done".

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move to the limit marker, then to the action button |
| `←` `↓` / `→` `↑` on the marker | Lower or raise the limit by 1% |
| `Shift` + arrow, `Page Down` / `Page Up` | Lower or raise the limit by 10% |
| `Home` / `End` | Set the limit to `MinLimit` or to 100% |
| `Enter` / `Space` on the button | Stop, resume or start a new session |

Keys move the limit at once, with no animation. The card itself is not focusable, and pressing the pack or the curve
leaves focus where it was. The keyboard focus ring is a square 2 px frame around the marker's touch target; the pill
also turns to the muted surface with a dark border.

## Motion

- One frame loop advances the session and the packets, and draws the scene and the curve. It stops when the card is
  off screen, when `IsPaused` is set, and once the flow has drained after a stop or at the limit.
- Packets move by distance along the cable, so they keep an even pace on the curve. They ease out of the charger, cruise
  at a steady pace, slow as they near the fill front and fade in and out at the ends. One leaves about every 0.22 s at
  full power; while power drains they slow to 40% and stop being sent.
- Availability rises with a time constant of 0.38 s and drains with 0.24 s. Each landing brightens the front line, and
  the glow fades within about half a second.
- While the limit is held, the pill grows to 1.08 on the CSS spring (0.58 s) and its colors invert in 160 ms. The
  button shrinks to 0.97 while pressed, and its labels crossfade in 160 ms.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Session and packets | Run | Hold still; packets rest evenly along the path and breathe in opacity while charging | Hold still; packets rest evenly, no breathing |
| Stop, resume, new session | Power drains or ramps | At once | At once |
| Pill held, button pressed | Spring scale, color fade | Color change at once | Color change at once |
| Figures | Update every frame | Current values | Current values |

## Responsive behavior

- Breakpoints use the card's own width: side by side from 640 px, stacked below it, compact at 420 px and under.
- The scene is drawn at 0.24 of the card's content width at any size. Thin lines (outlines, cell dividers, the front and
  the limit line) keep their pixel width; the cable and the packets scale with the scene.
- The curve stretches to its column. Its height is 0.40 of its width beside the stats and 0.50 when stacked.
- The hint takes the room beside the button while it gets at least 220 px; otherwise the button moves to its own row.

## Related

- `Progress` for a plain percentage.
- `MorphLoader`, `SkeletonMorph` and the other Loaders for waiting states without a session.

| Component | Choose it when |
|---|---|
| `ChargeSession` | One live charging session with a limit, a curve and a cost |
| `Progress` | A single percentage, nothing to drag |
| A table or chart | Several sessions compared at once |
