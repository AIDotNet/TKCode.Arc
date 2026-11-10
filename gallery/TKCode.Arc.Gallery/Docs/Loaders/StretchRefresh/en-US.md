---
title: Stretch refresh
summary: A feed you pull down to refresh: the rows spread apart, a line stretches across the gap until it is time to let go, then runs while new items arrive.
---

<Demo name="Preview" />

## When to use

`StretchRefresh` is a feed card that refreshes when it is pulled past its top. A header with the title, a short status
and a round refresh button sits over a muted well; the rows lie on a sheet that covers the well. Pull the sheet down
and it follows with growing resistance while the rows drift apart; a short line in the gap grows from its centre and
reaches full length exactly at the point where letting go will refresh. Let go there and the line runs back and forth
while the work happens, then gives way to a check and a short result as the new rows drop in from the top.

- **Activity feeds, inboxes and timelines** where new entries arrive at the top and people check for them now and then.
- **Panels that should refresh by gesture on every input**: mouse drag, touch, pen, and the wheel or trackpad scrolling
  up at the top of the feed all pull the same way.
- **Feeds where the result matters**: the indicator says what the refresh brought ("3 new updates") and a screen reader
  hears it too.

## When not to use

- **Static content that never refreshes**: use a plain list.
- **Paging through older entries**: use `Pagination`, or load more at the bottom of the list.
- **A fixed history of events with dates and markers**: use `Timeline`.
- **Content that updates by itself** (a live log, a chat thread): push the new items in as they come; a pull gesture
  suggests the feed is stale until asked.
- **A whole screen on a phone**: use the mobile `PullToRefresh` wrapper, which fits any scrolling content.

## Usage

Give the control a title, the items and a refresh handler, and a height (or a parent that sizes it): the feed scrolls
inside the card. The handler is an `async` function that does the work, puts the new items into the collection, and
returns a short result. Return `null` for the done label ("Up to date"); throw for the error label ("Couldn’t refresh").

```xml
<arc:StretchRefresh x:Name="Feed" Title="Northbeam activity" Subtitle="Updated 12 min ago" Height="368" />
```

```csharp
Feed.ItemsSource = items;                       // an ObservableCollection, newest first
Feed.RefreshHandler = async ct =>
{
    var fresh = await api.FetchNewAsync(ct);
    foreach (var item in fresh.Reverse()) { items.Insert(0, item); }
    Feed.Subtitle = "Updated just now";
    return fresh.Count == 1 ? "1 new update" : $"{fresh.Count} new updates";
};
```

The preview replays a simulated backend, `StretchRefreshSampleFeed`: each refresh takes 1150 ms, marks what was shown as
read, adds the next batch of invented entries on top (3, then 1, then 2, then none) and changes the subtitle. Reset is
part of the demo, not of the control. In an app, replace the sample feed with your own work.

### Anatomy

1. **Header**: the title, the subtitle (a change rises in), and a 36 px round refresh button whose glyph spins while
   the work runs and turns into a check when it ends.
2. **Well**: the muted area behind the sheet. Its indicator holds the 72 × 3 line and a status line ("Pull to refresh",
   "Release to refresh", "Refreshing", then the result with a check).
3. **Sheet**: the rows on the surface colour. Its top corners round off as it is pulled and a soft shadow falls on the
   well above it.
4. **Rows**: built from `ItemTemplate`. Without one, `StretchRefreshActivity` items show as `StretchRefreshActivityRow`:
   an avatar or status glyph, a sentence with the actor and target in the emphasis weight, an optional quote and photo,
   a meta line, and an accent dot while the entry is unread.

### States

| State | What shows |
|---|---|
| `Idle` | The sheet covers the well; nothing of the indicator is visible |
| `Pulling` | A gap opens; the line grows from its centre in the muted colour; "Pull to refresh" |
| `Armed` | The pull is past `Threshold`: the line is full length, stretches a little further and turns to the foreground colour; "Release to refresh" |
| `Refreshing` | The sheet rests 60 px down; a short segment runs along the line; the header glyph spins; "Refreshing" |
| `Done` | The line shrinks away; a check draws itself beside the result; new rows drop in; then the sheet returns |
| `Failed` | As done, with a cross in the danger colour and the error label |

`State` and `IsRefreshing` are read-only and bindable; `RefreshStarted` and `RefreshCompleted` report each cycle with what
started it (`Pull`, `Wheel`, `Button` or `Code`) and its result or error.

### Your own rows

Any item type works with an `ItemTemplate`. Give `ItemKeySelector` a stable key so that a row keeps its view across a
refresh: rows that stay slide from where they were to their new place, and rows with new keys enter one after another.
The log beside this feed shows the events of each cycle.

<Demo name="CustomRows" />

### Errors and labels

A handler that throws ends the cycle in `Failed`: the indicator shows a cross and `Labels.Error`, the header glyph turns
into a cross, and `RefreshCompleted` carries the exception. Every word the control shows or announces is in
`StretchRefreshLabels`; this demo rewords them for a mail inbox and fails every other time.

<Demo name="Errors" />

### Narrow containers and the threshold

The card fills the width it is given; rows wrap and photos shrink with their 4:3 shape. `Threshold` is the pull, after
resistance, that arms a refresh; the line always reaches full length exactly there. In windows 475 px wide or less the
card drops its border and corners so it can sit edge to edge.

<Demo name="Narrow" />

### Reduced motion

<Demo name="ReducedMotion" />

Under reduced motion the sheet still follows the pull and the rows still spread, so the gesture keeps its feel; the line
does not over-stretch or run (while refreshing, its segment rests mid-line and breathes in opacity, and the header
glyph breathes with it), the sheet jumps to its resting places, text changes and new rows only fade, and the sheet
returns and the cycle ends together 1.2 s after the result.

### Content guidelines

- **Title**: a noun phrase for the feed ("Northbeam activity", "Inbox", "Deploy queue"). It also names the feed for screen
  readers.
- **Subtitle**: when the feed was last updated, in a few words. Update it in the handler; the change rises in.
- **Result**: say what changed, briefly: "3 new updates", "1 new message". When nothing changed, say so plainly ("You’re
  all caught up") or return nothing for the done label.
- **Error label**: say what failed, not what to do about it; the button and the pull are the retry.
- **Refresh button name**: name what it refreshes when the page has several feeds ("Refresh activity").

### Best practices

- Put the new items into the collection **before** the handler returns: the result, the check and the entering rows
  belong together.
- Keep the work at least half a second, so the running line can be seen; a refresh that returns at once looks like a
  glitch. If the data is cached, wait the difference.
- Give items stable keys (`StretchRefreshActivity.Id` or `ItemKeySelector`); without them a changed item counts as new.
- Mark old entries read when new ones arrive: `StretchRefreshActivity.IsUnread` fades the dot on screen.
- Keep feeds short (a screen or two): rows are not virtualized, so every row can spread with the pull.

### Common questions

**Does the speed of the pull matter?** No. Only the distance does: a fast flick that ends under the threshold springs
back, as it does in the reference.

**Can the wheel pull when the feed is scrolled?** No. The wheel and the trackpad pull only when the feed is already at its
top; the event that brings it to the top only scrolls, and the next one pulls. The pull ends 150 ms after the last wheel
event.

**What happens to a drag that leaves the card?** It keeps pulling: the control follows the pointer across the window once
the press began on the feed, and captures it only when the pull starts, so a press that does not pull still clicks the
row under it.

**Can I start a refresh from code?** Call `RefreshAsync()`. It returns `false` at once while a cycle is running, otherwise
`true` when the work has ended.

## API reference

<!-- api: StretchRefresh -->

<!-- api: StretchRefreshLabels -->

<!-- api: StretchRefreshStartedEventArgs -->

<!-- api: StretchRefreshCompletedEventArgs -->

<!-- api: StretchRefreshActivity -->

<!-- api: StretchRefreshActivityRow -->

<!-- api: StretchRefreshSampleFeed -->

The enums:

| Type | Values |
|---|---|
| `StretchRefreshState` | `Idle`, `Pulling`, `Armed`, `Refreshing`, `Done`, `Failed` |
| `StretchRefreshTrigger` | `Pull`, `Wheel`, `Button`, `Code` |
| `StretchRefreshActivityTone` | `Person`, `Success`, `Danger`, `Neutral` |

`StretchRefreshSamples` holds the invented sample content: `CreateInitial()`, `CreateBatches()` and `FormatResult(count)`.

## Accessibility

- The refresh button is the keyboard and switch path to the same refresh as the pull. Its name is
  `Labels.RefreshButton`. While a cycle runs it stays focusable but does nothing (the equivalent of `aria-disabled`).
- The feed is a focusable region named by the title; arrow keys, Page Up and Page Down scroll it. Its keyboard focus
  ring is drawn inside the sheet, so the well cannot clip it.
- While the work runs the feed reports a busy status; "Refreshing" and then the result are announced politely.
- Unread rows read "New." first; each activity row reads its sentence, quote and meta line as one name.
- The indicator is decoration: the status region carries the same words.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move to the refresh button, then to the feed |
| `Enter` / `Space` on the button | Refresh without pulling |
| Arrow keys, `Page Up`, `Page Down`, `Home`, `End` in the feed | Scroll the feed |

## Motion

- The sheet follows the pull one to one through the resistance curve `280·d / (d + 400)`; each row moves down by its
  index × 5% of the pull, so the gaps between rows open up.
- The line's fill grows from its centre with the pull. Past the threshold the whole line stretches to 1.12 on a quick
  spring and its fill turns to the foreground colour over 160 ms.
- On release the sheet springs to 60 px (critically damped, about 0.4 s) and the rows close up; the fill fades as a
  segment 40% as long runs to the far end and back, 700 ms each way on an in-out curve; the header glyph turns once
  every 0.85 s.
- When the work ends the segment stops and fades, the line shrinks to 0.4 and fades (180 ms), a check fades and grows in
  and its stroke draws over 320 ms, rows that stay slide to their new places on a smooth spring, and new rows drop
  16 px into place out of a soft blur, 100 ms after the result and 60 ms apart. The sheet springs back 1.21 s later and
  the indicator resets at 1.86 s.
- Status words and the subtitle rise in out of a soft blur over 240 ms while the old words lift away in 120 ms.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Pull and row spread | Follows the pull | Follows the pull | Follows the pull |
| Armed over-stretch | Spring to 1.12 | None | None |
| Sheet to rest and back | Springs | Jumps | Jumps |
| Running segment and spinning glyph | Animated | Rest in place and breathe in opacity (1.8 s) | Still |
| Text changes | Rise and blur | Fade | Replaced |
| New rows | Drop, grow and unblur | Fade | Appear |
| Rows that stay | Slide | Jump | Jump |

## Responsive behavior

- The card is as wide as its parent allows; give it a height or a parent that sizes it. The rows wrap and photo
  attachments shrink with their aspect.
- In windows 475 px wide or less the card drops its border, corners and background (the header and the sheet keep their
  surface), so it can run edge to edge.
- Touch pulls only start when the feed was at its top when the finger went down; otherwise the finger scrolls the feed.
- Hover only changes the refresh button, and only for mouse and pen.

## Related

- `PullToRefresh` (Mobile) wraps any scrolling content with a pull to refresh.
- `Timeline` for a fixed history of events.
- `Pagination` for paging through older entries.
- `Skeleton` for placeholders while a first load runs.

| Component | Choose it when |
|---|---|
| `StretchRefresh` | A feed card should refresh by pull, wheel or button and say what it brought |
| `PullToRefresh` | A phone screen's own scrolling content should refresh by pull |
| `Timeline` | The events are a fixed history |
| `Pagination` | Older entries come in pages |
