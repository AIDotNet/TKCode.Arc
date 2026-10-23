---
title: Revenue globe
summary: A live payments map: arcs fly from every city to headquarters on a dotted globe, with a day timeline to replay or scrub and the revenue, regions and top cities beside it.
---

<Demo name="Preview" />

## When to use

`RevenueGlobe` turns a day of payments into something people can watch. Every payment flies as an arc from the city it
came from to your headquarters, the figures beside the globe count up as the arcs land, and a timeline under it shows the
whole day: how busy each quarter hour was, where the present is, and a playhead you can drag back to any moment. It fits
the top of an operations or revenue overview, a screen on the office wall, or the home page of a small business tool.

- **A revenue overview for a team that sells worldwide.** Regions show today's revenue against their forecast, so the
  question "are we on track?" is answered per region at a glance, and the top cities say where the day is happening.
- **A wall display for launches and campaigns.** The globe drifts on its own, the arcs keep flying, and Replay today
  plays the whole day in under a minute, which makes the morning's spike easy to show in a stand-up.
- **A live feed from your payment provider.** Set `ClockMode` to `External`, append payments as webhooks arrive and move
  `Now`; each new payment flies its arc as soon as it is due.
- **A post-mortem of an incident.** Drag the playhead to the minute the errors started and read the totals as they were
  then: every figure, list and bar follows the playhead, not only the clock.

## When not to use

- **Precise analysis.** The globe is a picture of activity. Totals by hour, by product or by customer belong in a chart or a
  table (`DataGrid`, a line chart) where the numbers can be compared directly.
- **Many thousands of places.** The block draws each city as a dot with its own arc. A few dozen cities read well; for
  hundreds, group them into the cities people know.
- **Payments that do not happen in a day.** The timeline always spans 24 hours. Weekly or monthly totals need another view.
- **A page full of other animation.** The drifting globe and the arcs draw the eye. Put it where it can be the one moving
  thing, or turn the drift off with `DriftSpeed="0"`.

## Usage

### Anatomy

From top to bottom (wider than 880 pixels, the panel moves to the right of the globe):

1. **Header**: `Title`, `Description` and `DateText` (you format the date).
2. **Globe**: land and sea as dots shaded by depth, city dots sized by today's revenue, the headquarters with its name, and
   the payment arcs. Hovering a city shows a card with its revenue, payments, share of the total and local time.
3. **Timeline controls**: Replay today, Pause / Play, the clock at the playhead (UTC by default) and the Live pill.
4. **Timeline**: payments per 15 minutes as bars, darker up to the playhead and lighter up to the present, with dashes for
   the rest of the day, and the playhead you can drag.
5. **Panel**: revenue today, payments and the last hour; the regions against their forecast; the five top cities.

### Data

Give the cities, the regions they count toward, the headquarters and the payments. Cities and regions are plain objects,
so they can be written in XAML; payments are usually loaded in code.

```xml
<arc:RevenueGlobe x:Name="Globe"
                  Title="Ebook orders"
                  Description="Cities buying from Dublin today."
                  DateText="Thursday, 22 October"
                  CurrencySymbol="€">
    <arc:RevenueGlobe.Regions>
        <arc:RevenueRegion Key="eu" Name="Europe" Forecast="61000" />
        <arc:RevenueRegion Key="am" Name="Americas" Forecast="38000" />
    </arc:RevenueGlobe.Regions>
    <arc:RevenueCity Key="dub" Name="Dublin" CountryCode="IE" RegionKey="eu" Latitude="53.35" Longitude="-6.26" />
    <arc:RevenueCity Key="ams" Name="Amsterdam" CountryCode="NL" RegionKey="eu" Latitude="52.37" Longitude="4.90" UtcOffsetHours="1" />
    <arc:RevenueCity Key="tor" Name="Toronto" CountryCode="CA" RegionKey="am" Latitude="43.65" Longitude="-79.38" UtcOffsetHours="-4" />
</arc:RevenueGlobe>
```

```csharp
Globe.Headquarters = Globe.Cities[0];
Globe.DayStart = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
Globe.Payments = orders.Select(o => new RevenuePayment(o.PaidAt, o.CityKey, o.Total)).ToList();
```

- A payment names its city by `RevenueCity.Key`. Payments of the headquarters count in every total but draw no arc.
  Payments whose city is unknown count in the totals only.
- Payments should come in time order; anything else is sorted once. Totals at any moment are prefix sums, so a whole day of
  a few hundred thousand payments replays without walking them again.
- `RevenueRegion.Forecast` is the revenue expected from the region for the whole day; its row shows today's revenue as a
  share of it. A region flies the camera to `CenterLatitude` / `CenterLongitude`, or to the revenue-weighted middle of its
  cities when those are not set.
- The globe's land comes from Natural Earth 1:110m (public domain), sampled once on a lattice of 12,800 points (dots
  1.7° apart). `Land` takes a coarser or finer sampling (`RevenueGlobeLand.Sample(7000, 1200)`) or your own dots
  (`RevenueGlobeLand.FromPoints(...)`).

### The clock

`Now` is the present: payments after it wait, and the light bars of the timeline end there. With the default
`ClockMode="Internal"` the globe moves `Now` on in real time from the value you set, so a day prepared in advance plays out
live, which is what the preview above does from 14:30. When the block is scrolled out of view it stops drawing, and catches
up with the real time when it comes back.

With `ClockMode="External"` your app moves `Now`. Append payments with `AddPayment` (or add them to a collection that raises
change events) and then move `Now` to the time your server reported:

<Demo name="LiveFeed" />

### Replay, pause and the playhead

Replay today goes back to 00:00 and plays the day at `ReplaySpeed` (1500×: an hour in 2.4 seconds), easing in and braking
so it lands exactly on the present, where it goes live again. Pause holds the playhead (the arcs in flight still land);
Play replays on from there. Go live, or End on the timeline, fast-forwards to the present in at most two seconds.
Dragging the timeline jumps to the pointer and follows it; letting go replays on with the speed of the drag.

All of this is also available to your own controls: `ReplayCommand`, `TogglePauseCommand` and `GoLiveCommand`, the
methods `Replay()`, `Pause()`, `Play()`, `GoLive()` and `SeekTo(time)`, the two-way `Time` and `IsPaused`, and
`TimeChanged`, which fires every frame while the playhead moves:

<Demo name="Playback" />

### Cities and regions

Click a city on the globe or in the top-cities list and the camera flies there along the great circle, zooms in to
`CityZoom` and holds the view; the card stays on the city. Click a region and the camera flies to the region at
`RegionZoom`, the other cities fade and the region's three biggest cities get their names. A city and a region are never
selected together. Click the selected row again, click empty space on the globe or press Escape to let go: the zoom springs
back, the tilt returns home and the drift resumes.

`SelectedCity` and `SelectedRegion` are two-way, `FlyTo(city)` and `FlyTo(region)` select from code, `ResetView()` clears
the selection and turns the globe back home, and `SelectionChanged` reports every change:

<Demo name="Selection" />

### Narrow layouts

The block follows its own width, not the window's. From 880 pixels the panel sits beside the globe; between 600 and 880 it
sits under it in two columns; below 600 it is one column with a smaller revenue figure, and below 400 the clock takes a row
of its own. The globe's height follows the width (at most 580 pixels), and its radius is always 41% of the smaller side.

<Demo name="Narrow" />

### Reduced motion

When motion is turned down the globe holds still: it shows the latest arcs drawn whole, there is no drift, no Replay or
Pause button, and the pill reads "Now". Dragging, selecting and the timeline keys still work; every change lands at once.

<Demo name="ReducedMotion" />

### Texts

Every word the block shows or announces is in `Labels`, a `RevenueGlobeLabels` with English defaults. Replace it as a whole
to translate the block or change its wording; the formats that take numbers or names are functions.

```csharp
Globe.Labels = new RevenueGlobeLabels
{
    Replay = "Tag wiederholen",
    Live = "Live",
    GoLive = "Zur Gegenwart",
    OfForecast = percent => $"{percent:0} % der Prognose",
};
Globe.Culture = CultureInfo.GetCultureInfo("de-DE");
```

### Performance

- A frame draws 2,500 to 3,300 dots (land, sea and cities), the arcs in flight and their rings: about 3 ms in software
  rendering on a desktop, a little more while replaying (around 30 arcs), and a few kilobytes of memory per arc.
  Browsers are slower; on a phone, a coarser `Land` (`RevenueGlobeLand.Sample(7000, 1200)`) halves the dots.
- The globe asks for frames only while something moves: the drift, an arc, a glide or a flight. Under reduced motion an idle
  globe draws nothing.
- While replaying, at most 14 arcs start per second; the totals still count every payment.

## API reference

<!-- api: RevenueGlobe -->

<!-- api: RevenueCity -->

<!-- api: RevenueRegion -->

## Accessibility

- The globe is a focusable region named "Revenue globe" with how to use it ("Arrow keys turn the globe, Escape clears the
  selection."). Its description is a summary of the moment: revenue, payments, the time, each region and the leading city.
  It is refreshed when the minute changes, not every frame.
- Screen readers also find a hidden table of every city with its region, revenue and payments, updated with the playhead.
- The city dots themselves are not focusable. Keyboard users choose cities from the top-cities list and regions from the
  regions list; both are toggle buttons whose pressed state is the selection. A region row describes its share of today's
  revenue and its forecast.
- The figures count toward their value on screen, but screen readers read the final value at once.
- The timeline is a slider over the day so far (in seconds since midnight), named "Time of day, UTC".
- A polite live region announces "Paused", "Replaying today", "Back to live", "Camera on …" and "Selection cleared".
- Replay, Pause / Play and the Live pill are buttons; the pause button's name says what it will do.

### Keyboard

| Keys | Where | Action |
|---|---|---|
| `ArrowLeft` / `ArrowRight` | Globe | Give the globe a push west or east; it glides out |
| `ArrowUp` / `ArrowDown` | Globe | Tilt it briefly; it springs back to its home tilt |
| `Escape` | Globe | Clear the selected city or region |
| `ArrowLeft` / `ArrowRight` | Timeline | Move the playhead 5 minutes (`Shift`: an hour); in the past it replays on from there |
| `PageUp` / `PageDown` | Timeline | Move the playhead an hour |
| `Home` | Timeline | Go to 00:00 |
| `End` | Timeline | Go live |
| `Enter` or `Space` | Buttons and rows | Press the button; select or clear the region or city |
| Drag | Globe | Spin the globe; a fast release glides on, the tilt springs back home |
| Drag | Timeline | Move the playhead under the pointer; release to replay on |

## Motion

- Idle, the globe drifts back and forth at 4° a second over 86° of longitude around the headquarters, turning smoothly at
  the ends; the tilt always returns to 22° on a slow spring.
- A drag moves the surface under the pointer. Releasing glides on (the speed falls by 1.3% a frame) until the drift takes
  over; pulling the tilt past 65° stretches like a rubber band and springs back.
- Flights to a city or region follow the great circle on a critically damped spring and zoom out a little on the way when
  the flight is long.
- An arc's head travels in about a second (longer for far cities), the arc stays whole a third of a second, then its tail
  follows it into the headquarters while it fades. A small ring opens at the city when a payment starts and a larger one at
  the headquarters when it lands; the figures count up at that moment.
- The figures ease to every new value in about a quarter of a second. The hover card fades and grows in, follows its city
  on a spring and fades out.
- Under reduced motion none of this moves; see [Reduced motion](#reduced-motion).

## Related

- **Metric card** and **Animated counter**: one figure that counts toward its value, without the map.
- **Data grid** and the **line charts**: the precise numbers behind the picture, by hour, city or product.
- **Activity terrain** and **Customer galaxy**: the other showpieces that turn a dataset into a scene to explore.
