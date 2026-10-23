---
title: Mixture of experts
summary: A router scores every expert for each token and sends it to its top choices, whose outputs blend into the result.
---

<Demo name="Routing" />

## When to use

`MixtureOfExperts` explains how a sparse mixture-of-experts layer handles text: each token is scored by a router,
handed to its best one or two experts, and rebuilt from their outputs. It answers the question readers ask when they
first see a model described as "480B parameters, 30B active": which parameters run, and why most of them sit still
for any one token. It is an explainer, not a monitor: its scores, loads and parameter counts are illustrative, so
say so in the text around it.

- **Model documentation**: a page that introduces a sparse model and its active-parameter count. The header readout
  and the summary sentence show where the smaller number comes from.
- **Courses and onboarding**: a lesson on routing, where the reader switches between top 1 and top 2, points at a token
  to see its gate scores, and points at an expert to see which tokens it took.
- **Capacity and load balancing**: a note on why experts have a capacity and what happens to the tokens that do not
  fit. The skipped choices and the load balance bar make the trade-off visible.
- **Product pages for an inference platform**: a short visual next to the text that explains why a mixture-of-experts
  model is cheaper to serve than a dense one of the same size.
- **Talks and release notes**: a looping card that tells the whole story in under forty seconds with no narration.

## When not to use

- **Showing real routing statistics from your model**: use `BarChart` or `DataGrid` with the measured expert loads. The
  card plans its own small story from the scores it is given; it is not an instrument.
- **Choosing which model answers a request**: that is a model router in your product, a different idea at a different
  level. A model picker or a plain `Select` is the right control.
- **Explaining attention or tokenization**: the card shows tokens as given and says nothing about how text is split or
  which tokens attend to which. Use a diagram made for that.
- **Many cards on one screen**: each card animates while it plays. Keep one per page, or pause the others with
  `IsPlaying`.
- **Long sequences**: the story spends 2.2 seconds on each token. Fifteen to twenty tokens tell the story; a paragraph
  turns it into a wait.

## Usage

The card needs no data: it ships with an invented sentence of fifteen tokens and eight experts. It starts the first
time it is mostly on screen and loops: after the last token a summary rests for 3.6 seconds and the routing starts
over.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MixtureOfExperts MaxWidth="760" />
</UserControl>
```

`MixtureOfExperts` derives from `Explainer`, the base of the animated AI explainers in the Special package, which
derives from Avalonia's `TemplatedControl`. The base supplies the story clock (`IsPlaying`, `Speed`, `IsLooping`,
`Play()`, `Pause()`, `Restart()`), the start-on-view behaviour, the play button and the polite live region; this
control describes the layer, plans the routes and draws them.

### Anatomy

1. **Header**: a switch between top 1 and top 2 (`TopK`), the active-parameters readout ("Active parameters 30B of
   480B", from `Parameters`) and the round play button.
2. **Input**: the token being routed, in a chip that slides in from the left. It dims once its packet has left for the
   router.
3. **Router**: a rounded box with one score bar and percentage per expert. Bars are proportional to the score itself,
   not to the largest score, so a 50% score fills half the track. The top choices take the first and second choice
   colors; the others stay muted.
4. **Gate lines**: one line from the router to each expert. The chosen lines light up in their choice's color; a line to
   a full expert turns into a dashed warning line.
5. **Expert tiles**: an icon, the expert's name and a counter of the tokens it has taken, with a thin capacity bar along
   the bottom. A chosen tile lights up in its choice's color; a full expert that skips the token turns dashed with a
   warning sign.
6. **Merge curves and point**: the outputs of the chosen experts curve into one point, then travel to the output.
7. **Output**: the token's output chip, with a dot for each expert that processed it. A token that passed through has a
   dashed chip and no dots.
8. **Sequence**: every token as a chip under a divider. Upcoming tokens are muted, the current one has a ring and the
   routed ones have a solid fill.
9. **Footer**: the route line (the token, an arrow, each choice with its weight) and a note that says it in words. On
   the right, the load balance readout and a bar with one segment per expert.

Under 568 pixels the stage turns to run from top to bottom: the router becomes a band of vertical bars over a row of
icon tiles. Under 640 pixels the load balance moves under the footer text, and under 480 pixels the padding tightens,
the readout takes its own line and the route line drops the token chip.

### The story

Each token takes 2.2 seconds at speed 1:

| Time | What happens |
|---|---|
| 0 s | The token becomes current and its chip slides in. The router still shows the previous scores, dimmed. The footer says the router is rating the experts. |
| 0.16 – 0.48 s | A neutral packet with a short trail travels to the router. |
| 0.48 s | The router shows the token's scores; the top choices take their colors and the footer shows the route. |
| 0.76 – 1.14 s | A packet in each choice's color travels to its expert (`Routed` is raised as they leave). A packet heading for a full expert stops just short of it. |
| 1.14 s | The chosen experts light up, their counters roll and the capacity bars and the load balance move. |
| 1.38 – 1.66 s | The outputs travel along the merge curves; curves of different lengths arrive together. |
| 1.66 – 1.84 s | The merged packet travels to the output. |
| 1.84 s | The output chip changes to the token and the token is marked as routed. |

After the last token the summary says how many tokens were routed and how few parameters each one used.

### The arithmetic

The card plans the whole sequence at once, so any token can be inspected before it plays. Every number comes from
the scores of the tokens:

- **Gate scores** are normalized to sum to 1. Percentages are rounded one by one, so a pair can add up to 101.
- **The choice** is the `TopK` experts with the highest scores. Equal scores go to the expert that comes first.
- **Weights** are the chosen scores divided by their sum: scores of 44% and 22% mix 67 to 33.
- **Capacity** is ⌈factor × tokens × k ÷ experts⌉ tokens per expert. Fifteen tokens over eight experts give 4 at top 2
  and 2 at top 1.
- **Loading** goes in order. A chosen expert that is already full skips the token; it keeps its weight, and the other
  expert is not raised to 100%. A token whose every choice is full passes through unchanged.
- **Load balance** is (E − max ÷ mean) ÷ (E − 1) over the loads: 100% when every expert has the same load, 0% when
  one expert took everything.
- **Active parameters** are shared + k × per expert: with the defaults, 6 + 12 = 18B at top 1 and 6 + 24 = 30B at top 2.

With the default data, top 2 makes 28 assignments for 15 tokens, because two second choices find their expert full,
and ends at 98% balance. Top 1 lets the final period pass through.

### Top k and capacity

`TopK` takes 1 or 2 and is two way: the header switch writes it and an app can bind it. Changing it re-plans every
route and carries on from the same point of the story, so the counters, the load balance and the footer change in
place and the readout rolls to the new count. `CapacityFactor` scales the capacity; `double.PositiveInfinity` turns
it off, and the capacity bars then fill relative to the busiest expert.

```xml
<arc:MixtureOfExperts TopK="1" CapacityFactor="1.5" />
```

### Your own experts and tokens

`Experts` takes 2 to 12 `MixtureOfExpertsExpert` items with a `Name`, an optional `Handles` sentence (shown while the
expert is inspected) and an optional 24×24 `Icon` geometry (the first letter of the name stands in without one).
`Tokens` takes `MixtureOfExpertsTokenItem` items with their `Text` and, optionally, one raw score per expert in
`Scores`. A token without scores gets stable example scores derived from its text: the same text always routes the
same way. `Parameters` takes the total, shared and per-expert counts in billions.

<Demo name="CodeModel" />

```xml
<arc:MixtureOfExperts TopK="1" Parameters="96, 4, 7.5" CapacityFactor="1.25">
    <arc:MixtureOfExperts.Experts>
        <arc:MixtureOfExpertsExpert Name="Keywords" Handles="Reserved words such as if, for and return."
                                    Icon="{x:Static arc:ArcIcons.Code}" />
        <arc:MixtureOfExpertsExpert Name="Names" Handles="Variables, functions and types."
                                    Icon="{x:Static arc:ArcIcons.Hash}" />
        <!-- … -->
    </arc:MixtureOfExperts.Experts>
    <arc:MixtureOfExpertsTokenItem Text="for" Scores="62, 12, 3, 4, 6" />
    <arc:MixtureOfExpertsTokenItem Text=" total" />
</arc:MixtureOfExperts>
```

Spaces in `Text` are kept, as a tokenizer keeps them. Fewer than two experts fall back to the defaults, and experts
past the twelfth are left out. Changing either list starts the story over.

### Inspecting a token or an expert

Pointing at a token, or focusing it with the keyboard, holds the story where it is and shows that token's whole route
at once: its scores, both lines, the lit tiles and the loads as they stand after it, even for a token that has not
played yet. Pointing at an expert keeps the stage as it is and highlights the tokens that expert has taken so far,
tinted by whether it was their first or second choice; the footer shows its load and its `Handles` sentence. Moving
away carries on from the same moment.

A click, a tap, Enter or Space pins the token or expert: the story pauses on it and the play button offers to resume.
Escape, a second click or `Unpin()` releases it and the story plays on. `PinToken(index)` and `PinExpert(index)` do
the same from code; `InspectedTokenIndex` and `InspectedExpertIndex` report what is shown.

### Playback, events and binding

`Routed` is raised as each token's packets leave the router, with the token's `Index`, `Token`, normalized `Scores`,
the chosen `Experts` best first, their `Weights` and the `Dropped` experts that were full. `Routes` holds the whole plan
for the current settings, and `ActiveParameters`, `Capacity`, `CurrentTokenIndex` and `LoadBalance` report the state
on show. The example below logs the routes, keeps the story from looping, turns the capacity off and uses two other
series colors for the choices.

<Demo name="RouteLog" />

```csharp
card.Routed += (_, e) =>
{
    var first = e.Experts[0];
    log.Add($"{e.Token} → expert {first} ({e.Weights[0]:P0})");
};
```

`IsPlaying` is two way, so a view model can pause the card while a dialog is open. `Speed` scales the story clock
from 0 (hold still) to 4; the card has no speed control of its own. `FirstChoiceBrush` and `SecondChoiceBrush`
replace the choice colors, which default to chart series 1 and 2 and follow the accent.

### Narrow containers

The layout follows the card's own width, not the window's, so the same card works in a side panel.

<Demo name="Narrow" />

### Reduced motion

Under reduced motion the card opens on its summary, with the final loads and balance. Pressing Route again steps
through the tokens at 1.8 seconds each without any travel: the scores and the route appear at once, the experts light
up and count 0.6 seconds later, the output changes at 1.1 seconds, and the story stops on the summary instead of
looping.

<Demo name="ReducedMotion" />

### Content guidelines

Name the experts by what they tend to handle, in one short word (the narrow layout shows only their icons), and write
`Handles` as a plain sentence a reader can check against the tokens it took. Real experts are not this tidy; say so in
the text around the card. Pick a sentence that mixes kinds of tokens (a name, a verb, code, a number, punctuation), so
the routing visibly changes from token to token.

| Instead of | Write |
|---|---|
| Expert 3 | Digits |
| Handles numeric tokens with high activation | Whole numbers and digits inside dates. |
| A paragraph of forty tokens | One sentence of twelve to twenty tokens |
| 671B parameters (a real model's figure) | Your own illustrative figures, and say they are illustrative |
| Experts with no theme | Experts whose names match the tokens they take |

### Best practices

**Do**

- Give scores for the tokens that carry the point of the story (the one that finds its expert full, the one that passes
  through) and let the others derive theirs.
- Keep the default capacity factor when the story is about capacity: with 15 tokens and 8 experts it produces one or
  two skips, which is enough to notice.
- Explain the summary sentence in the surrounding text; it is the part readers quote.
- Let readers inspect: the hold-on-hover is how they check a route at their own pace.

**Avoid**

- Presenting the loads, the balance or the parameter counts as measurements of a real model.
- Using vendor model names or logos in the labels or the parameters.
- More than twelve experts or very long token texts; the tiles and chips do not grow to fit.
- Rebuilding the token list on a timer: every change starts the story over.

### Common questions

**Why does a skipped expert keep its weight?** That is how a capacity limit behaves in a real layer: the token's
output is mixed from the experts that processed it with the weights the router gave them. The card shows it as is so
the loss is visible, instead of quietly raising the other expert to 100%.

**What decides the scores of a token without any?** The card hashes the token's text and draws a favourite, a
runner-up and small scores for the rest from that seed. The same text always gets the same scores, so screenshots and
tests stay stable.

**Can the story start paused?** Set `AutoPlay="False"`; the card waits at its first token until `Play()` or the play
button. Set `IsLooping="False"` to stop on the summary.

**Does hovering pause the card?** It holds the story without changing `IsPlaying`, so a bound view model does not see
a pause. Pinning does pause it, and the play button then offers to resume.

## API reference

<!-- api: MixtureOfExperts -->

<!-- api: MixtureOfExpertsTokenItem -->

<!-- api: MixtureOfExpertsExpert -->

<!-- api: MixtureOfExpertsLabels -->

<!-- api: MixtureOfExpertsRoute -->

<!-- api: MixtureOfExpertsRoutedEventArgs -->

`Parameters` takes a `MixtureOfExpertsParameters` record: `Total`, `Shared` and `PerExpert` in billions, with
`Active(k)` for the parameters one token uses. In XAML it parses from "total, shared, perExpert".
`MixtureOfExpertsSamples.CreateExperts()` and `CreateTokens()` return fresh copies of the default story.

## Accessibility

- The card is a group named by `Title` ("Mixture of experts routing" by default).
- Expert tiles and sequence tokens are real buttons. Each expert group and the sequence take one Tab stop, and the
  arrow keys move within them. A pinned tile or token reports a pressed toggle state.
- A token's name carries its whole route ("Ada: People 71%, Syntax 29%", with "full, skipped" for an expert at
  capacity). A tile's name carries its load, whether it is full and its part in the route on show ("Syntax expert, 4
  tokens, at capacity, second choice for x").
- The readout and the load balance are read as sentences ("30B of 480B parameters active per token"); their rolling
  digits are hidden from screen readers.
- The live region speaks only for the reader's own actions: pause, resume, start over, pin and unpin, and a change of
  top k. The routing that plays on its own is not announced.
- Color is never the only signal: the first choice comes first in the route line, and a skipped expert has a warning
  sign, a dashed border and the word "full".

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move between the top-k switch, the play button, the expert tiles and the token sequence |
| `←` / `→` | In the top-k switch: choose top 1 or top 2 |
| `↓` / `→`, `↑` / `←` | In the tiles or the sequence: the next or previous one (no wrapping); focusing one inspects it |
| `Home` / `End` | The first or last tile or token |
| `Enter` / `Space` | Pin or release the focused tile or token; on the play button, pause, resume or route again |
| `Escape` | Release the pin (the focused item stays inspected until focus moves) |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Packets | A dot with a soft trail moves along each path, easing in and out | Not shown | Not shown |
| Token pace | 2.2 s per token, then a 3.6 s summary and a loop | 1.8 s per token, stops on the summary | Same as reduced |
| Score bars, line colors, router border | 240 ms | Change at once | Change at once |
| Tiles lighting up / going back | 160 ms / 480 ms | Change at once | Change at once |
| Counters, readout, load balance | Digits roll on a smooth spring | Change at once | Change at once |
| Capacity bars and load segments | Slide over 480 ms | Change at once | Change at once |
| Chips | The input slides in from 14 px, the output from 10 px | Change at once | Change at once |
| Footer lines | The old line fades out, the new one rises 2 px | Short crossfade | Change at once |

Packets run on the story clock, so they pause, hold and scale with `Speed`; the fades are short transitions that
finish on their own. The clock stops while the card is off screen.

## Related

| Component | Choose it when |
|---|---|
| `BarChart` | You plot the measured load of each expert |
| `Progress` | You show how full one real resource is |
| `DataFlow` | The story is events moving through a data pipeline |
| `VectorSearch` | The story is a question answered by its nearest documents |
