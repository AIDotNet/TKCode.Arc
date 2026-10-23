---
title: Reasoning tree
summary: A search over candidate thoughts: each is scored, weak branches are pruned, the best expand and the winning path draws in.
---

<Demo name="ArchiveMove" />

## When to use

`ReasoningTree` explains tree search over thoughts, the idea behind "tree of thoughts" prompting and beam search at test
time. A question proposes a few candidate thoughts, each one gets a score, the weakest are pruned, and the best few are
expanded one depth further. After three depths the best thought is the answer, and the path from the question to the
answer draws in. The beam width control lets the reader change how many thoughts survive each depth and watch the
search again, which is the point of the illustration: a wider search can find an answer that a greedy one misses.

- **Documentation and course material about reasoning strategies.** A page that explains why a model that explores
  several lines of thought can beat one that commits to its first idea. The reader steps through the search one beat at a
  time and reads each depth's count ("2 of 4 kept").
- **Product pages for planning or agent features.** A visual summary of how an assistant weighed options before it chose
  a plan, written in your product's own words (a migration plan, a build fix, a launch date).
- **Talks, onboarding and empty states.** A short story that plays on its own when it scrolls into view and ends on a
  clear answer with a check, without any data from your system.

The scores are illustrative. The card does not run a model and does not read real model output; it plays the search you
give it. Say so in the copy around it, for example "Scores are made up for the example."

## When not to use

- **Showing a real agent's steps as they happen.** Use `ChatThread` or `Timeline` to stream what an agent actually did. This card replays a fixed story and has no way to report progress.
- **Explaining the plan, act and observe loop of a tool-using agent.** That is a cycle, not a tree; the Agent loop
  illustration in this group tells that story.
- **Token-level mechanics.** Use the Speculative decoding or Mixture of experts illustrations for what happens inside one
  generation step. A reasoning tree works on whole thoughts.
- **Choosing a model.** A tree of thoughts does not explain routing between models; describe that with a table or a
  diagram of your own.
- **Large trees.** The card reads three depths and is designed for about three thoughts at depth 1 and two children under
  each thought. A broad tree crowds the columns; summarize it instead.

## Usage

With no data the card plays its built-in story: a team weighs three ways to move a photo archive to new storage over a
weekend. It starts the first time it is mostly in view, finds the answer in about 6.6 seconds at a beam width of 2, and
finishes drawing the path about a second later.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ReasoningTree MaxWidth="744" />
</UserControl>
```

`ReasoningTree` derives from `Explainer`, the base of the animated AI explainers, which derives from Avalonia's
`TemplatedControl`. The base supplies the story clock, the playback buttons, autoplay on first sight, reduced-motion
handling and the polite live region; the card adds the search, the drawn tree and the beam width control.

### Your own question and thoughts

Set `Question` to a short question for the root node (about three short lines in the wide layout) and `QuestionDetail`
to the full sentence that the tag and screen readers use. Put `ReasoningThought` elements inside the card: they are the
thoughts at depth 1, and the thoughts inside them are their children. Each thought has a `Label` (two to four words, under
about 20 characters), a full `Text` and a `Score` from 0 to 0.99.

```xml
<arc:ReasoningTree Question="How do we halve the nightly build?"
                   QuestionDetail="The nightly build takes 84 minutes. How do we get it under 40 without dropping a single test suite?">
    <arc:ReasoningThought Label="Cache dependencies" Score="0.78"
                          Text="Restore packages and toolchains from a shared cache instead of downloading them every night.">
        <arc:ReasoningThought Label="Pin lockfiles" Score="0.81"
                              Text="Pin every lockfile so the cache key only changes when a dependency really does." />
        <arc:ReasoningThought Label="Warm the cache" Score="0.52"
                              Text="Run a small job before midnight that fills the cache for the nightly run." />
    </arc:ReasoningThought>
    <arc:ReasoningThought Label="Split the tests" Score="0.71"
                          Text="Run the slow integration suites on four agents in parallel instead of one." />
</arc:ReasoningTree>
```

The demo below uses that question with a full tree whose scores are written so that each beam width finds a different
answer: 0.84 with a beam of 1, 0.89 with 2 and 0.95 with 3. It starts at a beam width of 1; try 2 and 3 in its footer.

<Demo name="BuildTime" />

To bind thoughts from a view model, set `ThoughtsSource` to a collection of `ReasoningThought`; it is used instead of the
`Thoughts` written in markup. Changing a thought, adding one or removing one starts the search over. Thoughts deeper than
depth 3 are ignored, and a thought should appear under one parent only.

### Anatomy

1. **Header**: the model's mark and name (`Model`, an `ExplainerModel` with a `Name` and an optional `Mark`), the
   `Title` ("Tree of thoughts") in muted text, and the status on the right. While the search plays the status is a
   present-tense label with a calm shimmer; when the reader pauses or steps it shows a pause icon and the past tense; at
   the end it shows a green check and "Answer found". Set `Model` to null to show only the title.
2. **Column headers** (wide layout): "Question" and "Depth 1" to "Depth 3". The column of the current beat turns to the
   foreground color. After a depth is pruned its header shows "2 of 4 kept"; the last depth shows "Best of 4".
3. **Question node**: the root, on a muted fill, 84px high in the wide layout.
4. **Thought nodes**: a card with the label, a score bar and the digits. The last depth keeps room for the answer's check.
5. **Edges**: a 1.25px curve from each parent to each child in the strong border color. A pruned thought's edge retracts
   into its parent.
6. **Winning path**: a 2px line in the accent color from the question through every kept thought on the path to the
   answer, drawn over the edges with a lighter sheen at its head.
7. **Tag**: a dark bubble beside the hovered or focused thought with its full text and a second line with the score and
   fate ("Score 0.78 · Kept"). For the question, the second line reads "Question".
8. **Footer**: play or pause, next step and restart on the left, and the beam width control on the right. Under 520px the
   label reads "Beam"; under 306px the beam control moves under the buttons. `AreControlsVisible` hides the footer.

Kept thoughts and the path use `AccentBrush` (chart series 1 by default). The answer always uses the success color with a
check, so a custom accent never hides which thought won.

### The search, step by step

The search is plain beam search over at most three depths:

1. **Propose.** Depth 1 proposes every thought given to the card; later depths propose all children of the previous
   depth's survivors, in document order.
2. **Score.** Each candidate's bar fills to its score while the digits count up to it ("0.78").
3. **Keep.** The best `BeamWidth` candidates survive; the rest are pruned. Equal scores keep the thought that comes first
   in the document.
4. Survivors expand at the next depth. The last depth does not keep a beam: its best thought is the answer.

The last depth is depth 3, or an earlier depth whose survivors have no children (the search cannot go deeper). The count
beside each header is the number kept and the number of candidates; the number pruned is the difference.

With the built-in story the three widths play out like this:

| Beam width | Depth 1 | Depth 2 | Depth 3 | Answer |
|---|---|---|---|---|
| 1 | 1 of 3 kept | 1 of 2 kept | Best of 2 | Done by Sunday, 0.88 |
| 2 | 2 of 3 kept | 2 of 4 kept | Best of 4 | Small region first, 0.93 |
| 3 | 3 of 3 kept | 3 of 6 kept | Best of 6 | Small region first, 0.93 |

The greedy search (beam 1) follows the best first idea, copying in batches, and settles for a weaker plan; a beam of 2
keeps "Mirror, then switch" alive long enough to find the better answer.

#### Scores you leave out

A thought without a `Score` gets a seeded one: `0.18 + 0.78 × r`, rounded to two decimals, where `r` is the first value of
a mulberry32 generator seeded with `Seed` combined (XOR) with the FNV-1a hash of the thought's path key. The path key joins
the `Key` (or the label when there is no key) of the thought and its ancestors with "/". The same tree and the same
`Seed` therefore always search the same way, and giving a thought a `Key` keeps its seeded score when you rename it.
Seeded scores stay between 0.05 and 0.99. The next demo has no scores at all; "Next seed" changes `Seed` and the search
starts over with new numbers.

<Demo name="AnswerLog" />

### Timing

At `Speed` 1 the beats fall at fixed times after a restart: the question is read for 0.56 s, then each depth proposes
(0.62 s), scores (0.88 s) and keeps (0.76 s); the last depth goes from scoring straight to the answer at 6.58 s. The path
then draws for 0.76 s after a 0.16 s pause, and the answer settles with one short press. A beat's length does not depend
on how many thoughts it shows. `Speed` divides the beat lengths (0.5 doubles them, 0 holds the story still); the fades,
bars and path keep their own durations.

### Playback

`IsPlaying` is two-way: the play button and `Play()` / `Pause()` set it, and an app can bind it. Pausing freezes the
story clock and resuming continues the current beat from where it stopped. Changes that a beat already started (a fade,
a bar filling) still finish.

`StepForward()` (the next-step button) pauses and moves to the next beat at once: from the start, nine steps reach the
answer (three per depth, minus the last keep). At the end the step button turns off and keyboard focus moves to Restart.
`Restart()` starts the search over from the question and plays it. After the answer, the play button reads "Play again".

`BeamWidth` takes 1 to 3 (other values are clamped) and is two-way: the footer control writes it and a binding can read
or set it. A new width starts the search over and plays it. `IsLooping` plays the search again four seconds after each
answer, and `AutoPlay` set to false keeps the card at "Ready" until the reader presses play.

`Answered` is raised once each time the search reaches its answer, with the answer, the path from depth 1 and the beam
width. `Answer` holds the current answer (null until it is found).

```csharp
tree.Answered += (_, e) =>
    status.Text = $"Beam {e.BeamWidth}: {e.Answer.Label} ({e.Score:0.00}) via {string.Join(" → ", e.Path.Select(t => t.Label))}";
```

### Wide and narrow

The layout follows the card's own width, not the window's. From 620px of stage (a card of about 670px) the tree reads left
to right in four columns. The question takes 19% of the width and each depth 22.5%, with 4.5% gaps. Thoughts sit on a
62px pitch, stacked around their parent and pushed apart when they would overlap. The stage reserves the tallest column
of any beam width, so the card keeps its height when the beam changes.

Narrower, the tree becomes an outline: one row per thought, indented 16px per depth and joined by elbow lines. The stage
reserves a row for every thought the current search will show, so the card does not grow while it plays. When new
thoughts are inserted under a row, the rows below glide down on a critically damped spring before the new rows fade in.
A new beam width changes the height of a narrow card over 480ms.

<Demo name="NarrowColumn" />

### Reduced motion

Under reduced motion (or Off) the card opens on the finished search: the path is drawn, the answer has its check, and the
status reads "Answer found". Play, step and restart still work on the same beats, but every change applies at once, the
rows do not glide and the status has no shimmer. A new beam width shows that width's finished search immediately.

<Demo name="ReducedMotion" />

### Content guidelines

Write the question as the reader would ask it and keep it short; put the full version, with its constraints, in
`QuestionDetail`. Write each label as the gist of a plan, not a sentence, and keep it under about 20 characters so it stays
on one line. Put the whole thought in `Text`; it appears in the tag and in the accessible name.

| Instead of | Write |
|---|---|
| Option A | Copy in batches |
| We could maybe copy the archive in several batches | Copy in batches |
| Score: 78% | Score 0.78 (the card formats it) |
| A label with the full sentence | A short label and the sentence in `Text` |
| Question? | How should we move the photo archive? |

Write the scores so the story teaches something: if every beam width finds the same answer, the beam control has nothing
to show. A common shape is a strong first idea that leads to a mediocre plan and a weaker first idea that leads to the
best one.

### Best practices

**Do**

- Say near the card that the scores are illustrative.
- Give depth 1 about three thoughts and each later thought about two children, so the columns stay readable at every beam
  width.
- Use `Key` on thoughts whose labels may change, so their seeded scores stay put.
- Keep one playing explainer per screen and let the others wait for the reader.

**Avoid**

- Presenting the numbers as a model's real output or as a benchmark.
- Changing `Thoughts`, `Question` or `Seed` on a timer: every change starts the search over.
- Long labels: they are cut with an ellipsis, and the reader has to hover to read them.
- Trees deeper than three levels: the extra levels are ignored.

### Common questions

**Why does a beam of 3 find the same answer as a beam of 2 in the built-in story?** Because the best plan is already
inside the beam of 2. The demo with your own data shows a tree where each width finds a different answer.

**What happens with a tie?** The thought that comes first in the document wins, both for the survivors of a depth and for
the answer.

**What if a thought has no children?** It can survive its depth, but it adds no candidates to the next one. If no survivor
of a depth has children, that depth becomes the last one and its best thought is the answer.

**What if there are no thoughts at all?** An empty `Thoughts` collection without `ThoughtsSource` plays the built-in story.
An empty `ThoughtsSource` shows the question alone, with the status "No thoughts to search".

**Can I translate the card?** Yes: set `Labels` to a `ReasoningTreeLabels` with your words (headers, status, tag lines,
accessible names and announcements; sentences with numbers are functions), and `PlaybackLabels` for the buttons.

## API reference

<!-- api: ReasoningTree -->

<!-- api: ReasoningThought -->

<!-- api: ReasoningTreeAnsweredEventArgs -->

<!-- api: ReasoningTreeLabels -->

## Accessibility

- The stage is a tree named after the full question ("Search tree for …"). Each shown thought is a tree item whose name is
  its full text with its score and fate (not scored yet, scored, kept, pruned, on the winning path, answer). Its help text
  gives its depth and its place among its shown siblings ("Depth 1, 1 of 3"). The question's name is "Question: " and
  the full question.
- State never relies on color alone: a pruned thought fades and loses its edge, the answer has a check, and the header
  status pairs an icon with words.
- Hover and focus show the full thought in the tag; the same words are in the accessible name, so the tag itself is hidden
  from assistive technology.
- A polite live region speaks each beat when the reader steps or pauses ("Depth 2: kept 2 of 4, pruned 2."), the new
  beam width when it changes, and the answer when the search ends. It stays quiet while the search plays on its own.
- Pointer focus draws no ring; keyboard focus draws a 2px ring outside the node that follows its corners.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Moves to the tree (one stop, on the current thought), then play or pause, next step, restart and the beam width control |
| `ArrowUp` / `ArrowDown` | The previous or next shown thought in the same column (wide), or the previous or next row (narrow) |
| `ArrowRight` | The child on the winning path, else the best scored shown child, else the first shown child |
| `ArrowLeft` | The parent |
| `Home` / `End` | The question; the answer once it is found (before that, the first thought of the deepest shown depth) |
| `ArrowLeft` / `ArrowRight` on the beam width | Changes the beam width, which starts the search over |

When a restart hides the focused thought, focus moves to the question.

## Motion

| Change | Full | Reduced and Off |
|---|---|---|
| Thoughts proposed | Each edge draws out of its parent (240 ms) and the card fades in (180 ms), 20 ms apart per sibling | Shown at once |
| Thoughts scored | Bars fill and digits count on one 640 ms curve | Full bars and final digits |
| Depth pruned | Pruned cards fade to 40% and their edges retract (240 ms); kept bars turn to the accent | Applied at once |
| Answer | The path draws in 760 ms with a sheen at its head; each node on it lights as the line arrives; the answer settles with one press and its check | Path and check shown at once |
| New rows (narrow) | Rows below glide down on a critically damped spring before the new rows fade in | Rows jump to their places |
| Header status | Labels swap with a quiet 160 ms rise; the working label shimmers | Swap without shimmer |
| Tag | Fades in and rises 3 px out of the node's side (160 ms) | Shown at once |

The beats follow `Speed`; fades and fills keep their durations and finish even while the story is paused.

## Related

- `DataFlow` and `VectorSearch` in this group for systems rather than reasoning.
- The Agent loop, Mixture of experts, Speculative decoding and Prompt cache explainers share this card's frame, playback
  controls and header.

| Component | Choose it when |
|---|---|
| `Timeline` | You show what an agent really did, in order |
| `ChatThread` | You show a conversation with an assistant |
| `DataFlow` | You explain how data moves through a pipeline |
| `VectorSearch` | You explain how a question finds its nearest documents |
