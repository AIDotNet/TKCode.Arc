---
title: Prompt cache
summary: Requests that share a prompt prefix read it from the cache instead of processing it again, and the savings add up.
---

<Demo name="Preview" />

## When to use

`PromptCache` explains prompt caching with a short simulated story. A round of up to four requests shares one stable
prefix (tool schemas, instructions, documents, the conversation so far) and adds a new message at the end of each. The
first request processes the whole prompt and stores the prefix; the next ones read the prefix back and only process
their message. The card adds up what that does to input cost and to the time before the first output token. It is a
teaching illustration with a simulated clock and sample prices, not a monitor of a real cache.

- **API and SDK documentation**: the page that introduces cache breakpoints or cached input pricing. The rows show
  why a stable prefix matters before the reader writes any code.
- **Pricing and usage pages**: a section that explains the three prices behind cached traffic (input, cache write,
  cache read) and the time to live. Pass your own prices and the totals follow.
- **Onboarding for an AI product**: a first-run explanation of why the order of a prompt matters, with the change
  toggle showing that an edit early in the prompt costs more than one at the end.
- **Engineering posts and internal training**: a talk or a post about latency, where the first-token speedup is the
  number people remember.

## When not to use

- **Reporting a real cache hit rate**: use `LineChart` or `BarChart` with numbers from your telemetry. Every number in
  this card is computed from the sample data you give it.
- **Showing how much of a quota is used**: use `UsageMeter`. It shows a real share of a limit; this card shows a story.
- **Listing real requests and their costs**: use `DataGrid` with the rows from your logs.
- **Explaining other inference ideas**: `SpeculativeDecoding` explains draft and verify, `AgentLoop` explains an
  agent's cycle of context, action and checks.

## Usage

The card needs no data: it ships with a story of its own, a support assistant for a freight company whose four
questions share a 45.5K-token prefix. It starts playing the first time it is mostly on screen and loops.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PromptCache MaxWidth="744" />
</UserControl>
```

`PromptCache` derives from `Explainer`, the base of the Special explainers, which supplies the story clock, the play,
step and restart buttons, the live region and reduced motion. The card lays itself out from its own width, so give it
the width of the column it sits in; 744px shows the requests and the cache side by side.

### Anatomy

1. **Header**: the title (`Title`, "Prompt cache" by default), the model line (the mark and name from `Model`, then the
   time to live, "5-minute cache") and three round buttons: play or pause, send the next request, and restart.
2. **Legend**: every prefix segment with its color and token count, then the new message.
3. **Requests**: one row per request. The row header shows "Request N", the simulated time it was sent and its status;
   under it a bar of the prompt; at the right three numbers: tokens processed, input cost and time to first token.
   Above the rows, two keys tell the solid processed fill from the light cached fill.
4. **Cache panel**: a ring that shows the time to live left, the cache status (Empty, Writing, Reading, "Expires in
   4:22" or Expired), a bar of what is cached, the cached token count and the three prices.
5. **Caption**: one or two sentences that say what just happened.
6. **Footer**: the input cost of the round against the same prompts without a cache, the first-token speedup on hits,
   and the change toggle.

Bars use a square root scale, so a 200-token message stays visible next to a 33,800-token knowledge base; the legend
and the numbers give the real counts.

### The story

At full motion one round takes about ten seconds:

1. **Request 1 writes.** Nothing is cached, so the whole prompt is processed: each segment fills in turn at one steady
   speed, then the new message. The prefix lifts out of the row and flies into the cache panel, changing from the
   solid color to the light cached tint on the way. The ring fills and the countdown starts at the time to live.
2. **Requests 2 to 4 hit.** The simulated clock moves on by the request's gap and the ring drains a little. The prefix
   turns to its cached tint while a band of light passes over it (and over the panel's bar), only the message is
   processed, and the ring refills: every hit resets the time to live.
3. **Numbers roll from the row above.** Each row's three numbers start from the previous row's values, so you see the
   cost and the first token come down on a hit.
4. **The cache idles and expires.** With no more requests the clock speeds up, the ring drains, turns to the warning
   color in the last minute and empties. A looping card then starts the next round with its first request already
   written; with `IsLooping="False"` the card stops on the expired cache.

All numbers in the card are illustrative: they follow from the prices, the token counts and the first-token estimate
you pass, not from a real model.

### The arithmetic

With `P` the prefix tokens, `m` the message tokens, and the three prices per token:

| Outcome | Read from the cache | Written to the cache | Processed | Input cost |
|---|---|---|---|---|
| Write (nothing cached, or the cache expired) | 0 | `P` | `P + m` | `P × write + m × input` |
| Hit | `P` | 0 | `m` | `P × read + m × input` |
| Partial hit at segment `k` | the segments before `k` | the segments from `k` on | written + `m` | cached × read + written × write + `m` × input |

The uncached cost of a request is `(P + m) × input`. The cost statistic compares the round's totals: with the built-in
story the first write costs $0.1712 against $0.1371 without a cache (25% higher), and after the three hits the round
costs $0.2147 against $0.5491 (61% lower). The speedup averages the first token of cache hits only, with and without
the cache: 0.48s against 1.42s, 2.9x. Writes and partial hits do not count toward it.

### Your own prefix and requests

Fill `Segments` with `PromptCacheSegment` items in prompt order (an id, a label, a token count and an optional
`Color`) and `Requests` with up to four `PromptCacheRequest` items (the new message's token count, the simulated
`Gap` in seconds since the request before, and a `Message` that is read out and passed in the results). While a list is
empty the card uses its built-in story. To bind instead, set `SegmentsSource` and `RequestsSource`.

<Demo name="CodeReview" />

```xml
<arc:PromptCache Title="Review bot cache" Ttl="3600" ChangeSegmentId="style">
    <arc:PromptCache.Model>
        <arc:ExplainerModel Name="Quill 2 Mini" />
    </arc:PromptCache.Model>
    <arc:PromptCache.Pricing>
        <arc:PromptCachePricing InputPrice="1.20" CacheWritePrice="2.40" CacheReadPrice="0.12" />
    </arc:PromptCache.Pricing>
    <arc:PromptCache.Segments>
        <arc:PromptCacheSegment Id="repo" Label="Repository map" Tokens="7400" />
        <arc:PromptCacheSegment Id="style" Label="Style guide" Tokens="1800" />
        <arc:PromptCacheSegment Id="diff" Label="Open diff" Tokens="21500" />
    </arc:PromptCache.Segments>
    <arc:PromptCache.Requests>
        <arc:PromptCacheRequest Message="Flag any unchecked null returns in the parser" Tokens="140" />
        <arc:PromptCacheRequest Message="Suggest a clearer name for the retry helper" Tokens="90" Gap="64" />
        <arc:PromptCacheRequest Message="Does the new migration need a rollback step?" Tokens="210" Gap="118" />
    </arc:PromptCache.Requests>
</arc:PromptCache>
```

Segments without a color take the chart series colors in the order 3, 2, 4, 1, so they follow the theme and the accent.
Changing the data rebuilds the story and starts it over, so set it once rather than on every refresh.

### Prices and the first token

`Pricing` takes a `PromptCachePricing` with dollars per million tokens. The defaults are sample prices that follow a
common pattern: $3.00 input, cache writes at 1.25 times that ($3.75) and reads at a tenth ($0.30). Set
`CacheWritePrice` equal to `InputPrice` when your provider charges nothing extra for writes. `Ttl` is the time to live
in simulated seconds (300 by default); it drives the ring, the countdown and the header note.

`FirstTokenEstimator` returns the seconds to the first output token from the processed and cached token counts. The
default is an illustrative model, `0.40 + processed / 45,000 + cached / 600,000`: a fixed overhead, prefill of new
tokens, and a much cheaper load of cached ones. Pass your own measurements when you have them.

```csharp
cache.FirstTokenEstimator = (processed, cached) => 0.25 + processed / 60_000.0 + cached / 900_000.0;
```

### The change toggle

The toggle under the statistics edits one segment, `ChangeSegmentId` ("rules", the instructions, by default). While it
is armed (`IsChangeArmed`), the next request matches the cache only up to that segment: the segments before it are
read, a warning line marks the break, and the segment and everything after it are processed and written again. The
toggle releases once that request is sent, and the next one hits the rewritten cache. An edit of the first segment
breaks the match before anything, so it is a plain write. Set `ChangeSegmentId` to null (or to an id no segment has)
to hide the toggle.

### Playback, stepping and events

`IsPlaying`, `Speed` (0 to 4; 0 holds still), `Play()`, `Pause()` and `Restart()` come from `Explainer`. The step button
("Send the next request", `StepForward()`) pauses the story and sends exactly one request: a pause before it is
skipped, a request in progress is finished, and after a finished round it starts a new one whose first request hits
the cache if it is still warm. Restart empties the cache and keeps the play state, so a paused card stays paused at
its start.

`RequestCompleted` reports each finished request as a `PromptCacheResult`: its index, simulated time, kind, break,
cached, written and processed tokens, cost with and without the cache, first token with and without the cache, whether
it was edited or cold, and its message. `Expired` fires each time the cache runs out. `Results` holds the current
round's results and `CacheState` what the cache is doing.

<Demo name="EventLog" />

```csharp
cache.RequestCompleted += (_, e) =>
    log.Add($"{e.Result.Kind}: {e.Result.Cost:C4} instead of {e.Result.UncachedCost:C4}");
cache.Expired += (_, _) => log.Add("The cache expired.");
```

### Narrow containers

The layout follows the card's content width, not the window. From 660px the cache panel sits beside the requests;
below that it becomes a strip above them, so the flight still travels from a row into the cache. Below 480px each
request takes three lines (title and status, the bar, then the three numbers under shared headings), the statistics
stack and the toggle spans the card. Below 260px the playback buttons move under the title.

<Demo name="Narrow" />

### Reduced motion

With reduced motion the card opens on a finished round and waits. Playing and stepping still work, but every change
applies at once: no sweeps, no band, no flight, no rolling numbers and no accelerated drain. After a round the cache
expires, and a looping card starts a new round a moment later.

<Demo name="ReducedMotion" />

### Content guidelines

Name segments after what they hold in your prompt and keep the labels short; they appear in the legend, the toggle
and the captions ("Edit the style guide"). Keep the order realistic: what changes least comes first. Write messages
as the questions your users ask; they are read out to screen readers.

| Instead of | Write |
|---|---|
| Segment 1 | Tool schemas |
| System | Instructions |
| Big context | Knowledge base |
| Question 3 | Compare the two freight quotes in the sheet |
| $0.1 off | (let the card compute the costs) |

### Best practices

**Do**

- Use the token counts and prices of the model you describe, so the totals tell the reader something true about it.
- Keep gaps shorter than the time to live unless you want to show a cold write in the middle of a round.
- Use the change toggle to make the point about prompt order: an edit early in the prompt rewrites almost everything.
- Say near the card that the numbers are illustrative.

**Avoid**

- Feeding the card live traffic. It simulates one round at a time; real measurements belong in a chart.
- More than four requests: the card shows the first four of a round.
- Changing the data on a timer; every change starts the story over.

### Common questions

**Why does the first request cost more than no cache?** Writing to the cache costs more than plain input (1.25 times
with the sample prices), so the first write is more expensive. The hits pay it back; the cost statistic says "higher"
until they do.

**Why does the speedup ignore the partial hit?** It compares cache hits with the same prompts without a cache. A
partial hit reprocesses most of the prompt, so counting it would hide what a hit does.

**Can the card show my real cache?** No. Use `RequestCompleted` to see what the simulation computed, and a chart for
real numbers.

## API reference

<!-- api: PromptCache -->

<!-- api: PromptCacheSegment -->

<!-- api: PromptCacheRequest -->

<!-- api: PromptCachePricing -->

<!-- api: PromptCacheResult -->

<!-- api: PromptCacheLabels -->

## Accessibility

- The card is named after its title and marked as a simulation ("Prompt cache, a simulation with sample prices").
- The legend, the requests and the cache panel are named groups. Each request row reads as one sentence with its time,
  message, outcome, tokens read from the cache, tokens processed, cost and first token ("Request 1: not sent yet."
  before it runs); the bars and rolling numbers are hidden from assistive technology.
- Stepping, the toggle, pausing and restarting are announced in a polite live region; the story playing on its own
  stays silent.
- Outcomes never rely on color alone: every status pairs an icon with words, and cached fills differ from processed
  ones in tint and outline.
- The toggle is a toggle button and reports whether it is armed; the icon buttons have names and tooltips.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through play or pause, send the next request, restart and the change toggle |
| `Enter` or `Space` | Activate the focused button: play or pause, send a request, restart, or arm and release the edit |

Keyboard focus shows the 2px focus ring; pointer focus shows none.

## Motion

Every story animation runs on the explainer's clock, so pausing freezes all of it (a sweep in progress included) and
`Speed` scales all of it.

| Change | Full | Reduced and Off |
|---|---|---|
| Processing | Each segment fills in turn at one steady speed (a full bar in 1.1s), then the message in 120ms | Filled at once |
| Cache hit | Cached tint fades in over 160ms; a band passes over the prefix and the panel bar in 520ms | Tint at once, no band |
| Write | The written part flies into the cache panel in 460ms on a strong ease-out, solid fading to tint | Panel fills at once |
| Partial hit | A warning line rises in the gap before the changed segment (200ms); the panel drops what changed | Line shown at once |
| Numbers | Roll from the row above in 420ms (ease-out) | Change at once |
| Ring | Follows its target with a short lag; turns to the warning color in the last minute | At its target |
| Simulated gap | Passes in 0.55s on an S curve | At once |
| Idle | The time to live drains in about 4.2s, then the cache expires | Expires after a pause |
| Status and caption | Crossfade (160ms in, 100ms out) | Short crossfade |

## Related

| Component | Choose it when |
|---|---|
| `SpeculativeDecoding` | You explain how a draft model speeds up generation |
| `AgentLoop` | You explain an agent's cycle of gathering context, acting and checking |
| `DataFlow` | You explain how events move through a data platform |
| `LineChart` / `BarChart` | You plot a real cache hit rate or real costs over time |
| `UsageMeter` | You show how much of a real quota is used |
