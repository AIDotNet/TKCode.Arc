---
title: Speculative decoding
summary: A small draft model guesses a few tokens ahead and the large model checks them in one pass, keeping the ones it agrees with.
---

<Demo name="SimulatedRun" />

## When to use

`SpeculativeDecoding` explains why a language model can answer faster when a small draft model works ahead of it. A
draft lane proposes a few tokens at a time as dashed "ghost" chips, a target lane checks all of them in a single
pass, and the guesses the target agrees with travel into the answer. The card plays the run once, round by round,
and keeps a running score: how much faster the answer was written than by the target alone, the share of drafts
that were accepted, and the tokens written per target pass. It is an explainer, not a decoder: the numbers are
illustrative.

- **Model and product pages about latency**: a page that says "responses are up to 2.5 times faster" can show where
  the time goes. The reader sees that every pass of the large model can write several tokens instead of one.
- **Inference dashboards and release notes**: a recorded run from your own server, replayed with `Rounds`, shows the
  acceptance and the tokens per pass of a real request next to the metrics that summarize thousands of them.
- **Teaching the trade-off of the draft length**: the slider lets the reader try drafts of 2 to 8 tokens. Longer
  drafts write more per pass when the guesses are right, but accept a smaller share, because everything after the
  first miss is thrown away. With the built-in sample the speedup peaks at 4.
- **Comparing with plain decoding**: the compare switch races a second line in which the target writes one token per
  pass on the same clock, so the gap between the two approaches is visible, not just stated.
- **Courses and blog posts**: the run is deterministic. The same tokens, seed and draft length always give the same
  rounds, so a screenshot or a paragraph that refers to "round 3" stays true.

## When not to use

- **Streaming a real answer to a user**: use `MarkdownStream` or a chat component. This card explains the mechanism
  behind the speed; it does not show text as it arrives.
- **Token probabilities and alternatives**: a card that shows the candidates for one position and their scores is a
  different story. The draft model here only guesses right or wrong; it carries no probabilities.
- **Routing requests between models**: speculative decoding runs both models on every request. To explain which
  model answers which request, use a diagram of the router.
- **What happens inside one forward pass**: attention and expert routing are below the level of this card. Use
  `MixtureOfExperts` for routing inside the model.
- **Live performance monitoring**: the statistics are computed from the run on the card, not from your traffic. Use
  charts with your own measurements for that.

## Usage

The card needs no data. With nothing set, it plays the built-in sample: a 22-token maintenance notice drafted four
tokens at a time, which takes 6 target passes and ends 2.6 times faster than the target alone.

```xml
<arc:SpeculativeDecoding />
```

`SpeculativeDecoding` derives from `Explainer`, the base of the animated AI explainers, which derives from Avalonia's
`TemplatedControl`. The base class supplies the story clock, the play and restart buttons and their names, autoplay
and the live region; this control describes the lanes, the answer and the arithmetic.

### Your own answer

`Tokens` takes the answer the target model writes, one `DecodeToken` per token. The text keeps its leading space, as
tokenizers do (`" refund"`), and a token without one, such as punctuation, joins the word before it. `Ease` says how
likely the draft guesses the token (0 to 1; tokens without it use 0.9), and `Miss` is what the draft proposes when it
gets it wrong. Give numbers, names and rare words a low ease and a believable miss; leave short function words and
punctuation easy. The lane names can carry invented model names when the page needs them.

<Demo name="OwnAnswer" />

`Seed` decides which guesses succeed. Each guess draws one number from a seeded generator and succeeds when the
number is below the token's ease; drawing stops at the first miss. Change the seed until the run tells the story you
want, then keep it: the same seed, tokens and draft length always replay the same rounds. `TokensSource` takes a
sequence of tokens instead, for binding.

### A recorded run

`Rounds` (or `RoundsSource`) replays a run recorded from your own server instead of simulating one. Each
`DecodeRound` has the tokens the draft proposed, how many of them the target accepted from the start, and the
token the target wrote itself: the correction of the first rejected guess, or a bonus token when every guess was
accepted. The answer is the accepted drafts and the target tokens in order. The draft length slider is hidden,
because the rounds already fix it.

<Demo name="RecordedRun" />

### Anatomy

1. **Header**: the title (`Title`, also the card's accessible name), a status line that names the current step, and
   the play / pause and restart buttons. The status line wraps on narrow cards and reserves two lines there, so the
   lanes never move.
2. **Draft lane** (`DraftLabel`, `DraftDetail`): a small dot for the small model. Each round its chips appear one
   by one as dashed ghost chips in the draft color, with a soft shimmer while they wait.
3. **Target lane** (`TargetLabel`, `TargetDetail`): a larger dot for the large model. Under each chip sits a cell
   that the target's sweep fills from left to right. A kept guess gets a check, the first rejected guess is struck
   through with a cross and the target's own token, and the guesses after it fade. When every guess is kept, an
   extra cell with a plus brings the target's bonus token.
4. **Answer line** ("Output", "Speculative" while comparing): the whole sentence is laid out from the start, so the
   line never reflows. Tokens land in their places, each underlined in the color of the model that wrote it: the
   draft color for accepted guesses, the target color for the target's own tokens. The pass count sits under the
   name and gets a check at the end.
5. **Target-only line** (`IsComparing`): the same sentence written one token per target pass on the same clock,
   underlined in the target color.
6. **Statistics**: the speedup, the share of drafts accepted and the tokens per pass. They show a dash until the first
   round settles, then roll to the running totals after each round.
7. **Controls**: the draft length slider (2 to 8) and the compare switch.
8. **Summary**: at the end the lanes show two lines with the totals, beside the two lanes.

### The story and its arithmetic

Every round has three steps. With the defaults (a pass of 640 ms, a draft cost of 0.1):

| Step | Duration | What happens |
|---|---|---|
| Draft | k × 64 ms | The draft proposes k chips, one every 64 ms. k is the draft length, but at most the number of tokens left minus one, so the target always writes the last token. |
| Verify | 0.72 pass (461 ms) | The sweep crosses all k cells in 0.4 pass (256 ms, however many cells there are). Each cell gets its verdict as the sweep passes its middle. |
| Settle | 0.28 pass (179 ms) | The kept guesses and the target's token fly into the answer, 20 ms apart, the columns fade and the statistics roll. |

A round therefore writes the a accepted guesses plus one token of the target's, and costs one pass plus the draft
time. The statistics follow from the totals of the rounds so far:

- **Speedup** = tokens / (passes + draft cost × drafted). The draft time counts, so drafting more is not free.
- **Drafts accepted** = accepted / drafted, as a whole percentage.
- **Tokens per pass** = tokens / passes. The target alone writes exactly one.

The built-in sample shows the shape the reader should take away. The speedup rises from short drafts to a draft of 4
and falls after it, while the share of accepted drafts keeps falling:

| Draft length | Passes | Drafted | Accepted | Accepted share | Speedup |
|---|---|---|---|---|---|
| 2 | 10 | 19 | 12 | 63% | 1.8× |
| 3 | 8 | 21 | 14 | 67% | 2.2× |
| 4 | 6 | 24 | 16 | 67% | 2.6× |
| 5 | 6 | 29 | 16 | 55% | 2.5× |
| 6 | 6 | 33 | 16 | 48% | 2.4× |
| 8 | 6 | 39 | 16 | 41% | 2.2× |

`PlannedStats` gives the totals of the whole run as soon as it is planned, `Stats` the totals of the rounds that have
settled so far, and `Completed` reports the totals each time the speculative line finishes. `DraftCost` sets the cost
of one draft token as a share of a pass, both on screen and in the speedup, and `PassDuration` how long one pass
takes on screen.

### Comparing with the target alone

`IsComparing` opens the target-only line. It writes token m at (m − 1) passes + 0.72 pass on the same clock as the
speculative line, so after the six passes of the sample the target alone has written 8 of 22 tokens. When the
speculative line is done the story keeps running until the target alone is done too, and the status line says so.
Turning the comparison on after the run has finished carries the clock on from there; it does not restart the run.
The statistics and `Completed` always describe the speculative line.

### Playback

The run plays once, the first time the card is mostly in view (`AutoPlay`), and stops at the end; `IsLooping` plays
it again a few seconds later. The play button pauses and resumes the run where it stopped, and after the end plays it
from the start. Restart always plays from the start. `IsPlaying` is two way, and `Play()`, `Pause()` and `Restart()`
do the same from code. `Speed` scales the clock; there is no speed control on the card, and no step button.

Changing the draft length (with the slider or `DraftLength`) plans the run again and plays it from the start. So do
new tokens, rounds, a new seed, draft cost or pass duration. An input that is equal to the old one, such as a new list
with the same tokens, does not restart the run.

```xml
<arc:SpeculativeDecoding DraftLength="{Binding DraftLength}"
                         IsComparing="{Binding ShowsBaseline}" />
```

```csharp
decoder.Completed += (_, e) => status.Text = $"{e.Stats.Speedup:0.0}× faster in {e.Stats.Passes} passes";
decoder.DraftLength = 6;
decoder.Restart();
```

<Demo name="BoundControls" />

### Loading and errors

`Status` describes the data behind a recorded run. While it is `Loading` the lanes dim, the status line shows a
spinner and a shimmering label, the controls are disabled and the run waits; autoplay starts once the status turns
`Ready`. `Error` shows `ErrorText` (or the default message) with a danger icon. A retry button appears next to the
playback buttons only when someone handles `RetryRequested` or `RetryCommand` is set, because a button that does
nothing is worse than none.

<Demo name="StatusStates" />

### Narrow cards

Below 560 px of its own width the card uses its narrow layout: tighter padding, the lane names as a legend above the
columns, each line's name above its text with the pass count on the right, the slider on its own row and the switch
under it. Draft columns that do not fit wrap onto a second row, and the lanes grow on a smooth spring.

<Demo name="NarrowCard" />

### Reduced motion

With reduced motion the card never starts on its own and does not jump to its end either: it waits at its first
state until the reader presses play. The run then has exactly the same beats, but the chips appear without sliding,
the sweep steps a whole cell at a time, the strike is drawn at once, there is no shimmer, and tokens appear in their
places in the answer instead of flying there.

<Demo name="ReducedMotion" />

### Content guidelines

Write an answer of 15 to 30 tokens that reads well on its own, and mix easy and hard tokens the way a real model
would meet them: function words and punctuation are easy, numbers, names and the key nouns are hard. Make each miss
a plausible wrong guess of the same kind (a different number, a nearby word), not a random word. Keep the lane names
generic, or use clearly invented names; do not name real products unless the run really came from them.

| Instead of | Write |
|---|---|
| A sentence of 60 tokens | One or two short sentences of 15 to 30 tokens |
| Every token with ease 0.5 | Easy tokens near 0.95, hard ones near 0.1 to 0.3 |
| `Miss=" banana"` for a number | `Miss=" 30"` for `" 25"` |
| A real model name on a simulated run | "Draft model" and "Target model", or invented names |
| "3× faster" in the page text next to a simulated run | "about 2.6 times faster in this illustrative run" |

### Best practices

**Do**

- Say that the numbers are illustrative when the run is simulated, and say where a recorded run came from.
- Keep the default draft length of 4 for the sample, so the first run shows the best speedup and the slider shows the
  trade-off in both directions.
- Use `Rounds` for numbers you want to quote: a recorded run shows exactly what your server did.
- Choose a seed whose run includes a kept round, a corrected round and a round whose first guess fails, so all three
  verdicts appear.

**Avoid**

- Several playing cards on one screen. Each one draws every frame while it plays.
- Tokens without leading spaces for words. The answer would run the words together, and punctuation rules depend on
  the spaces.
- Changing the tokens or the seed on a timer. Every change plans the run again and starts it over.
- Reading `Stats` as a measurement of your system. It describes the run on the card.

### Common questions

**Why is the speedup lower than the tokens per pass?** The speedup counts the draft model's time: each drafted token
costs `DraftCost` of a pass. With 6 passes and 24 drafted tokens the run costs 6 + 2.4 = 8.4 passes for 22 tokens,
which is 2.6 times faster, while the tokens per pass are 22 / 6 = 3.7.

**Why does the last round never draft the final token?** The target always writes at least one token per round, so
the plan leaves the last token of the answer to the target. A round with one token left has no drafts at all.

**Why do the chips after a rejected guess show the right words?** Once a guess is rejected, everything after it is
discarded without being checked, so those chips show the real tokens and fade.

**How do I get the same run as in a screenshot?** Keep the tokens, the seed and the draft length. The plan depends on
nothing else.

**Can I hide the playback buttons?** Set `AreControlsVisible` to false. The run still plays on its own and can be
driven with `IsPlaying`, `Play()` and `Restart()`.

## API reference

<!-- api: SpeculativeDecoding -->

The base class's `Model` is not shown by this card; the two lanes name the models. There is no step button, so
`IsStepping` stays false unless `StepForward()` is called from code, which plays to the end of the run.

<!-- api: SpeculativeDecodingLabels -->

Tokens use the `DecodeToken` class.

| Property | Type | Meaning |
|---|---|---|
| `Text` | `string` | The token with its leading space, such as `" refund"`; punctuation has none |
| `Ease` | `double?` | How likely the draft guesses the token, 0 to 1; null uses 0.9 |
| `Miss` | `string?` | What the draft proposes when it misses; null shows an ellipsis |

Recorded rounds use the `DecodeRound` class.

| Property | Type | Meaning |
|---|---|---|
| `Drafts` | `AvaloniaList<string>` | The tokens the draft proposed, in order |
| `Accepted` | `int` | How many drafts the target accepted from the start (clamped to the drafts) |
| `TargetToken` | `string` | The token the target wrote: the correction, or the bonus after a fully kept round |

Totals use the `DecodeStats` record, also the `Stats` property of `SpeculativeDecodingCompletedEventArgs`.

| Property | Type | Meaning |
|---|---|---|
| `Passes` | `int` | Target passes, one per round |
| `Drafted` | `int` | Tokens the draft proposed |
| `Accepted` | `int` | Drafted tokens the target accepted |
| `Tokens` | `int` | Tokens written into the answer |
| `Acceptance` | `double` | Accepted / drafted, 0 to 1 |
| `TokensPerPass` | `double` | Tokens / passes |
| `Speedup` | `double` | Tokens / (passes + draft cost × drafted) |

`SpeculativeDecodingStatus` is `Ready`, `Loading` or `Error`. `SpeculativeDecodingSamples` holds the built-in sample
(`Tokens`, `Seed`) and a recorded code completion (`CodeCompletion`).

## Accessibility

- The card is a named group: its accessible name is `Title`.
- The lanes are hidden from assistive technology. Once a round has its verdicts, a hidden text summarizes it: what the
  draft proposed, how many guesses the target kept and which token it wrote instead (or added).
- Each answer line carries the text written so far as its accessible name ("Output: Tomorrow at 7, the east"), so a
  screen reader reads words rather than tokens.
- Each statistic has a spoken value ("Speedup: 2.6 times", "Drafts accepted: 67 percent", "not yet" before the first
  round); the rolling digits are decorative.
- Verdicts never rely on color alone: kept guesses get a check, the rejected guess is struck through with a cross
  beside the target's token, and discarded guesses fade.
- A polite live region announces play, pause, restart, the comparison turning on or off, a new draft length with its
  expected acceptance and speedup, and the result at the end. It never announces every round.
- Pointer focus draws no ring; keyboard focus shows the shared focus ring.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through play or pause, restart, the draft length slider and the compare switch |
| `Enter`, `Space` | On play or pause and restart, activate them; on the compare switch, toggle the race |
| `Left` / `Down`, `Right` / `Up` | On the slider, shorten or lengthen the draft by one token; the run replays |
| `Home`, `End` | On the slider, jump to a draft of 2 or 8 tokens |

## Motion

- Draft chips appear one by one, fading in from 4 px to the left, and a soft shimmer crosses their text while they
  wait for the target.
- The target's sweep fills the cells from left to right; each verdict fades in as the sweep reaches the cell's middle,
  and the strike draws from the left.
- Kept guesses and the target's token fly from their chips into their places in the answer on a critically damped
  spring, 20 ms apart, while the columns fade; their underlines take the model's color shortly after they land.
- The lanes follow wrapping columns on the smooth spring, and the target-only line opens and closes on the same
  spring. The statistics roll digit by digit.
- The status line and the line name swap with a short crossfade.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Autoplay | When the card is mostly in view | Never; waits for play | Never; waits for play |
| Chips and columns | Slide in from 4 px, shimmer while waiting | Appear in place, no shimmer | Appear in place |
| Sweep | Fills each cell continuously | Steps a whole cell at its verdict | Steps a whole cell |
| Tokens into the answer | Fly on a spring, then fade in their underline | Fade in in place | Fade in in place |
| Lanes and the target-only line | Grow and shrink on a spring | Jump to their size | Jump to their size |
| Statistics | Roll digit by digit | Change at once | Change at once |

## Related

- `MixtureOfExperts` for what happens inside one forward pass: a router sends each token to its top experts.
- `PromptCache` for another way to save time: a shared prompt prefix is read from the cache.
- `AgentLoop` and `ReasoningTree` for the other AI explainers with the same playback controls.

| Component | Choose it when |
|---|---|
| `MarkdownStream` | You show an answer streaming to the reader |
| `MixtureOfExperts` | The story is how a model routes each token inside one pass |
| `PromptCache` | The story is how repeated prompts become cheaper |
| `LineChart` | You plot real latency or throughput measurements |
