---
title: TL;DR toggle
summary: An article card that streams an AI summary or key points in place of the article, with a length slider and key points that jump back to their paragraphs.
---

<Demo name="Preview" />

## When to use

`TldrToggle` is an article card with a Full, Summary and Key points switch and a length slider. Switching streams an AI
summary in place of the article while the card's height springs to fit, and each key point links back to the paragraph
it came from, which is scrolled into view and highlighted.

- **A quick summary or key points** above or in place of a long article, doc, ticket or report.
- **Letting readers check AI claims** against the source, through key points that link back to paragraphs.
- **Reading surfaces** where people choose how much detail they want with a length control.

## When not to use

- **Open-ended questions about content**: use an answer or chat component instead.
- **Short content** of a couple of paragraphs, where a summary saves no time.
- **Summaries of many documents at once**: use a list or a digest layout instead of one card per item.

## Usage

Give the card an `Article` whose paragraphs have stable ids, and a `Summarizer` that streams `TldrStreamEvent`s. Send
`TldrStreamEvent.OfText` for prose; for key points send `TldrStreamEvent.OfPoint(paragraphId)` before each point's
text, so its jump link lands on the right paragraph. Without a summarizer the card simulates summaries from your
article's lead sentences (`TldrStreams.Simulate`) and sends nothing. With no `Article` and no `Simulation` it shows its
quiet empty state: no header, "This article has no text to summarize yet." and the switch and slider off.

```csharp
card.Article = new TldrArticle
{
    Title = post.Title,
    SourceName = "Engineering blog",
    AuthorName = post.Author,
    Date = "Oct 21, 2026",
    Paragraphs = post.Paragraphs.Select(p => new TldrParagraph(p.Id, p.Text)).ToList(),
};
card.Model = new AgentModel("GPT-6 Luna", ArcIcons.Sparkles);
card.Summarizer = request => myService.StreamTldr(request); // IAsyncEnumerable<TldrStreamEvent>
card.Jumping += (_, e) => analytics.Track("tldr_jump", e.ParagraphId);
```

The request carries `Mode` (`Summary` or `KeyPoints`), `Length` (1 to 5), `TargetWords` (35 to 130), `PointCount`
(3 to 7), the article and a `CancellationToken` that is cancelled on Stop, Escape, a mode or length change, a new
article, or when the card leaves the window. Pass the length to your model as `TargetWords` or `PointCount` rather than
a vague word like "short".

### Simulation

To show the card without a summarizer (a demo, a design review, an onboarding tour), give it a `TldrToggleSimulation`:
the article, the model to credit, a summary written for each length and key points ranked by weight. The card streams
the written text after a short thinking pause, says it is simulated and nothing was sent, and opens the summary by itself
once it has been half in view for a second (`AutoPlay="False"` keeps it on the article). A length without a written
summary gets the lead sentences. Your own `Article` wins over the simulation, and setting `Simulation` to null stops it
and shows the empty state.

```csharp
card.Simulation = new TldrToggleSimulation
{
    Article = post,
    Model = new AgentModel("GPT-6 Luna", ArcIcons.Sparkles),
    Summaries = [shortest, shorter, medium, longer, longest],
    KeyPoints = [new TldrKeyPoint("results", "Median cold start fell from 4.2 seconds to 0.9.")],
};
```

The preview above plays a simulation whose post, summaries and key points are the gallery's own demo data.

### Anatomy

1. **Header**: the source's mark (or its first letter in a tinted square), name, date and reading time; the title; the
   author with an avatar.
2. **Controls**: the mode switch (`SegmentedControl`) and the length slider (`Slider`), which reads "~60 words" or
   "4 points" and is disabled in Full mode.
3. **Body**: the article, the streaming summary or the key point checklist. Its height springs to its content.
4. **Footer**: the state as an icon and a word, the model with its 14px mark, and Stop, Regenerate and Copy.

### Key points

<Demo name="KeyPoints" />

Key points arrive as a checklist: each point ticks from a dashed circle to a check when it is written. Once the list
is done, each point's ¶ link switches back to the article, waits for the height to settle, scrolls the paragraph into
view, moves focus there and highlights it. `Jumping` is raised first with the paragraph's id. Call `JumpTo(id)` to do
the same from code.

### Results, Stop and Regenerate

Finished results are kept per mode and length, so switching back is instant and makes no request. `Regenerate` (the
footer's button) asks again. Stop or Escape ends a stream and keeps what arrived. `Completed` reports each finished
result with its text, points and word count, for analytics or a server-side cache; stopped and failed runs do not
raise it. `ModeChanged` and `LengthChanged` report changes the reader makes; the slider reports a length when a drag
is released or a key moves it, so each change makes one request.

### Your own summarizer

<Demo name="YourSummarizer" />

Stream when your provider streams. When it returns whole results, wrap them with `TldrStreams.FromResult(text, token)`
or `TldrStreams.FromResult(points, token)` and the card still streams them in chunks of 3 to 8 words.

### Failures

<Demo name="Failure" />

An exception from the summarizer shows its message as an alert with Try again. `TldrStreams.Simulate(request, fail: true)`
and `TldrToggleSimulation.Summarize(request, fail: true)` throw after the thinking pause. When you wrap a simulation
yourself, set `IsSimulated="True"` so announcements and error text say "Simulated, nothing was sent".

### Narrow containers

<Demo name="Narrow" />

The card fills its container up to 680px. Below 520px the length slider moves under the mode switch at full width;
below 440px the padding steps from 24px to 16px. Footer labels stay on one line and the model's name truncates first.

### Autoplay

With no summarizer, the card opens the summary by itself once it has been half in view for a second, unless the reader
has already pressed or typed inside it. Set `AutoPlay` to turn it on or off explicitly.

### Your own words

`Labels` holds every word the card shows or says. Replace the object to localize or reword:

```csharp
card.Labels = new TldrToggleLabels
{
    Full = "Article",
    KeyPoints = "Highlights",
    Words = words => $"{words} words",
};
```

### Best practices

- Give paragraphs stable ids and have the model return the id each key point came from.
- Pass the length as `TargetWords` or `PointCount`.
- Without a summarizer the card runs a simulation and says so in its announcements; never ship that to production.

## API reference

<!-- api: TldrToggle -->

<!-- api: TldrArticle -->

<!-- api: TldrParagraph -->

<!-- api: TldrKeyPoint -->

<!-- api: TldrRequest -->

<!-- api: TldrStreamEvent -->

<!-- api: TldrStreams -->

<!-- api: TldrToggleSimulation -->

<!-- api: TldrToggleLabels -->

<!-- api: TldrModeChangedEventArgs -->

<!-- api: TldrLengthChangedEventArgs -->

<!-- api: TldrCompletedEventArgs -->

<!-- api: TldrJumpEventArgs -->

<!-- api: AgentModel -->

## Accessibility

- The card is named after the article's title, and the title is a level 2 heading.
- The mode switch is a labelled group ("Reading mode"); the length slider's spoken value is the target ("~60 words",
  "4 points"), and it is disabled in Full mode.
- A polite live region announces each run once ("Summarizing with GPT-6 Luna", "Summary ready, 58 words", "4 key points
  ready", "Stopped"), never the streamed chunks. With the simulation it adds "Simulated, nothing was sent".
- Failures render as an alert with the message and a Try again button.
- Jump links are named after their point ("Jump to paragraph 3: Partial bootstrapping loads..."), and the jump moves
  focus to the highlighted paragraph so keyboard and screen reader users land where the eye does.
- Status is never color alone: done, stopped and failed each pair an icon with a word, and key points tick from a dashed
  circle to a check.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the mode switch, the length slider, the jump links and the footer actions |
| Arrow keys | On the mode switch, move between Full, Summary and Key points; on the slider, change the length by one step (one request per step) |
| Home, End | The first or last mode, or the shortest or longest length |
| Enter, Space | On a jump link, switch to the article, scroll to its paragraph, focus it and highlight it |
| Escape | Stops a running stream and keeps what arrived |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Body height (a view swaps or text streams in) | One spring, 0.26s, no bounce | At once | At once |
| Container resize | Cuts straight to the new height | At once | At once |
| Views swap | Old out over 100ms, new in over 160ms | 120ms crossfade | At once |
| Footer state and actions | The same crossfade | 120ms crossfade | At once |
| Streamed chunk (3 to 8 words, every 30 to 40ms) | Fades in over 140ms | Appears | Appears |
| Kept result | Appears whole, no fade | Appears | Appears |
| Thinking (about 0.7s) | Calm shimmer on the body's line; then the footer's Writing shimmers | Still | Still |
| Key point ticks | Dashed circle fades and shrinks, check grows in on the same spring | At once | At once |
| Jump | Waits for the height, scrolls smoothly, highlight in over 160ms and out over 600ms | Jumps, highlight fades | Jumps, highlight shows |

Only one shimmer runs at a time.

## Related

- `ReasoningBlock` for the thinking of an AI reply.
- `SegmentedControl` and `Slider`, which the card uses for the mode and the length.
- `CopyButton` for copying a value with immediate confirmation.
- `ToolCall` for the tool calls of an agent transcript.
