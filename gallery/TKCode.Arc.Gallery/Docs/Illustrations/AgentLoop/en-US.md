---
title: Agent loop
summary: An agent gathers context, calls tools and checks the result, going round the loop until the task is done.
---

<Demo name="SampleRun" />

## When to use

`AgentLoop` explains how a tool-using AI agent works: it gathers context, takes an action with a tool, checks the
result, and goes round again until the check passes. A bright head with a soft trail circles a ring past three
stations around the model. Context blocks join a stack beside the ring, tool calls drop out of the ring and resolve,
and checks either pass or send the loop round once more. The run ends with the ring drawn in and a summary. It is an
illustration of a run you write, not a log of a real agent, and nothing in it calls a service.

- **Product and documentation pages for an agent feature**: a page that tells customers how an assistant fixes a
  failing build or triages a support ticket. The card shows why the agent takes several passes instead of answering
  at once.
- **Explaining failure and retry**: a first check that fails with a retry note, or a tool call that times out and is
  narrowed down in the same step, shows that an agent recovers by looking again, not by guessing.
- **Onboarding for an internal agent platform**: a short visual for engineers who are about to write their first
  tools, next to the text that describes how the platform feeds tool results back into the model.
- **Landing pages and talks**: a self-contained, accessible animation that runs once when it scrolls into view and
  ends on a still summary that reads well as a screenshot.
- **A recap of a finished run**: under reduced motion, or with the story jumped to its end, the card is a summary of
  the context the agent built up, the last tool call and the last check.

## When not to use

- **Streaming a real agent's steps**: use `ChatThread` or a log view with the agent's actual messages and tool output.
  `AgentLoop` plays the run you describe; it does not accept events while it plays.
- **A single tool call in a transcript**: show the call as a row in your chat or log. The card is about the loop, and a
  run of one call with one passing check says little.
- **Searching over many candidate thoughts**: use `ReasoningTree`, which scores and prunes branches. The agent loop is
  one path that repeats.
- **Showing how full a context window is**: the token total here counts the blocks of the story; it is not a budget or a
  meter, and it never warns about a limit.
- **Status of a live job**: use `Alert`, `Badge` or a table with the real state. A finished illustration cannot report
  that a production run failed.

## Usage

The card plays a built-in sample run when it is given no iterations: three loops of an invented triage, with an
invented model and tools. It starts the first time it is mostly in view and stops on its done state.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentLoop />
</UserControl>
```

`AgentLoop` derives from `Explainer`, the base of the animated AI explainers in the Special package, which derives
from Avalonia's `TemplatedControl`. The base supplies the story clock, `IsPlaying`, `Speed`, `AutoPlay`, `IsLooping`,
the play, step and restart buttons, the polite live region and the reduced-motion opening state. `AgentLoop` adds the
iterations, the ring and its stations, the lanes and the context stack. Register the package with `UseArcSpecial()`
next to `UseArcControls()`.

The numbers in the card (tokens, durations, results) are illustrative. Say so in the text around it when the page is
about a real product.

### Anatomy

1. **Header**: the `Title` (default "Agent loop") on the left, and the status on the right: an icon with Ready,
   Running, Paused, Done or Stopped. Under them, the caption keeps room for two lines: `Task` while the run is ready
   or running, and `Summary` once it ends.
2. **Ring**: a 1.5px track with three small chevrons that point clockwise. The radius is at most 94px; on narrow
   columns it shrinks so the lower labels fit beside their nodes.
3. **Stations**: Gather context at the top, Take action at the lower right and Verify at the lower left, each a node
   with a label and an icon. An idle station is muted, the active one is filled in the accent with a thin ring and a
   calm shimmer on its name, and a visited one keeps an accent outline.
4. **Head and trail**: a bright dot that eases from station to station, dragging a gradient trail that grows and
   shrinks with each trip and is gone when the head lands.
5. **Center**: the model's mark and name (`Model`), and a line under them: Ready, the loop counter, or the ending
   ("Done in 3 loops", or "Stopped after 2 loops").
6. **Pins and lanes**: a short line drops from each lower station to its lane. Tool calls land in the lane under Take
   action and checks in the lane under Verify, each as a chip. A dashed placeholder fills a lane until its first chip.
7. **Context stack**: the heading with the running token total, and one slot per loop. Each block shows a mark, a
   label, its tokens and a detail line. A dashed placeholder fills the stack until the first block joins.
8. **Footer**: play or pause, Next step and Restart on the left, and the 0.5×, 1× and 2× speed control on the right.
   `AreControlsVisible` hides the whole row.

### The story and its timing

The run is a timeline built once from the iterations, so pausing, stepping and replaying are exact. At 1× speed each
loop takes:

| Part | Time |
|---|---|
| At Gather context (the block joins 0.1 s in) | 0.85 s |
| Trip to Take action | 0.7 s |
| At Take action, one call | 1.35 s |
| Each further call in the same loop | + 1.15 s |
| Trip to Verify | 0.5 s |
| At Verify (the check drops 0.1 s in) | 0.95 s |
| Trip back to Gather context | 0.7 s |

A loop is therefore 3.7 s plus its time at Take action, and the run starts 0.22 s after Ready. The sample run has one,
two and two calls, so it takes 0.22 + 5.05 + 6.2 + 6.2 = 17.67 seconds. A call drops 0.1 s after the head arrives,
shows "Calling" and its tool for 0.75 s, then turns into its result; the next call takes its place in the same lane.
The run ends when the head is back at Gather context after the last loop. If the last check passed, the ring draws in
and the status reads Done; if it failed, the ring stays neutral and the status reads Stopped.

The token total is the sum of the blocks gathered so far. Counts under 1,000 show as whole numbers and larger ones
with one decimal and a K, so 1,400 + 5,800 + 8,600 reads 15.8K at the end of the sample run.

### Your own run

Give the card its loops as content. Each `AgentLoopIteration` has a `Gather` block, one to three calls as its content,
and a `Verify` check. Marks are 24 by 24 stroked glyphs such as the `ArcIcons` members.

<Demo name="BuildFix" />

```xml
<arc:AgentLoop Task="Fix the failing nightly build and tell #builds."
               Summary="The build cache key had changed. Clearing it fixed the build.">
    <arc:AgentLoopIteration>
        <arc:AgentLoopIteration.Gather>
            <arc:AgentLoopContext Label="Build log" Detail="Stage 3 of 7 failed" Tokens="2900"
                                  Mark="{x:Static arc:ArcIcons.Terminal}" />
        </arc:AgentLoopIteration.Gather>
        <arc:AgentLoopCall Tool="CI" Action="Read pipeline run" Result="Cache key changed"
                           Mark="{x:Static arc:ArcIcons.GitBranch}" />
        <arc:AgentLoopIteration.Verify>
            <arc:AgentLoopCheck Passed="False" Note="Fix not applied yet" />
        </arc:AgentLoopIteration.Verify>
    </arc:AgentLoopIteration>
    <!-- More loops; the last one should pass its check. -->
</arc:AgentLoop>
```

`Task` is the caption while the run plays. `Summary` is the caption at the end; leave it unset to show a count of
loops and tool calls. Every loop reserves a slot in the context stack from the start, so the card does not grow while
it plays. Two to four loops read best.

Set `Status="Error"` on a call to show a failed call: the spinner turns into a cross, the result reads as the reason
and screen readers hear "failed". Follow it with a second call in the same loop to show the agent trying again.

### A run that stops

When the last loop's check fails, the run ends stopped: the status shows Stopped, the ring stays neutral and the center
says how many loops ran. Without a `Summary`, the caption says the run stopped after so many loops and calls.

<Demo name="StoppedRun" />

### Playback, steps and speed

`IsPlaying` and `Speed` are two way: the footer writes them and an app can bind them. `Speed` scales the story clock
(0 holds it still, at most 4); the footer offers 0.5×, 1× and 2×. Transitions such as a chip landing or the ring
drawing in keep their own pace at any speed. `Play()`, `Pause()`, `StepForward()` and `Restart()` do the same from
code.

Next step plays to the next beat and stops there: a new context block, each tool result, each check, and the end. It
plays at 2.5 times the speed (at least 2.5×), so a step is quick but still shows the trip. Restart goes back to Ready
and plays. `StationChanged` fires once each time the head reaches a station, with the station and the zero-based loop;
`Finished` fires when the run reaches its end.

<Demo name="EventLog" />

```csharp
loop.StationChanged += (_, e) => log.Add($"Loop {e.Loop + 1}: {e.Station}");
loop.Finished += (_, _) => status.Text = loop.Phase == AgentLoopPhase.Done ? "Done" : "Stopped";
```

`Phase` is the state the status shows: Ready, Running, Paused, Done or Stopped. `IsLooping` plays the run again four
seconds after it ends.

### Narrow containers

The layout follows the card's own width, not the window's. From a 640px container the context stack sits in a column
beside the ring, with two lanes under it. Narrower, the stack moves under the ring, with two blocks per row from 460px
and one below. Below a 360px ring column the station labels drop their icons and shrink, and the two lanes merge into
one that shows the latest tool call or check. On the narrowest cards the two lower stations move further down the ring
so their labels fit, and the speed control wraps under the buttons.

<Demo name="Narrow" />

### Reduced motion

Under reduced motion the card opens on its finished run: the ring drawn in, every block in the stack, the last chips
and the summary. Play again and Next step still work. The head jumps from station to station without a trail, and
every change applies in place without a drop or a fade. Because the run is played to its end at once when the card
opens, `StationChanged` fires for every station together at that moment, followed by `Finished`.

<Demo name="ReducedMotion" />

### Driving it from data

Bind `IterationsSource` when the loops come from a view model. A new collection, or a change to an observable one,
rebuilds the story. Before the card has been seen it rebuilds quietly, so autoplay still starts on first sight; after
that it starts over.

```csharp
public sealed class AgentDemoViewModel
{
    public ObservableCollection<AgentLoopIteration> Loops { get; } =
    [
        new(new AgentLoopContext("Support ticket", "Refund not received", 1200, ArcIcons.Inbox),
            [new AgentLoopCall("Billing", "Find the payment", "Refund issued on May 2", mark: ArcIcons.CreditCard)],
            new AgentLoopCheck(true, "Customer told"))
    ];
}
```

```xml
<arc:AgentLoop IterationsSource="{Binding Loops}"
               Task="Answer a refund question."
               Speed="{Binding Speed}" />
```

An empty `IterationsSource` shows the ring at rest with the controls off. `Labels` takes an `AgentLoopLabels` object
with every word the card shows or announces, including the station names; set only what you need.

### Content guidelines

Write the run so that every piece fits on one line: a context label of one to three words, a detail under about 30
characters, a call action of two to four words and a result under about 30 characters. Name the tools by what they
are in your product, and use generic glyphs, not brand marks.

| Instead of | Write |
|---|---|
| Context | Build log |
| Retrieved the complete build log for analysis | Stage 3 of 7 failed |
| Tool call 1 | Read pipeline run |
| The tool returned a successful response | Cache key changed |
| Verification failed | Fix not applied yet |
| The agent has completed the task successfully | The cache key had changed. Clearing it fixed the build. |

Keep the station names to about two words; a longer name is cut beside its station. The task is one sentence; the
summary can take two lines.

### Best practices

**Do**

- Let the last loop pass its check, and let an earlier one fail with a retry note. The failed check is what explains
  why agents loop.
- Show one failed tool call followed by a narrower call in the same loop, because recovering inside a step is the
  second lesson of the diagram.
- Keep the run to two to four loops. More loops make the card taller and the story longer than readers will watch.
- Say in the copy that the run is illustrative, and match its tools to the ones your product really has.

**Avoid**

- Presenting the numbers as measurements. The tokens and timings tell a story and are not read from a model.
- Brand marks of real services in the chips. Use generic glyphs and name the tool in text.
- Replacing `IterationsSource` on a timer. Each change starts the run over.
- Several playing cards on one screen. Each one draws its ring on every frame while it plays.

### Common questions

**How long does the run take?** 0.22 s plus 3.7 s per loop plus 1.35 s at Take action, and 1.15 s more for each extra
call. Divide by the speed.

**Why does a step stop where it does?** Steps stop after each beat a reader can name: a block that joined, each result,
each check and the end. A call that starts is not a stop, because its result follows.

**Can the card show more than three calls in a loop?** No. Calls past the third are ignored, because the lane shows one
chip at a time and longer steps hide the story. Split the work across loops instead.

**What does a loop without calls do?** The head still stops at Take action for one call's time, and nothing lands in
the lane.

**How do I hide the model?** Set `Model` to null. The center then shows only the loop counter.

## API reference

<!-- api: AgentLoop -->

<!-- api: AgentLoopIteration -->

<!-- api: AgentLoopContext -->

<!-- api: AgentLoopCall -->

<!-- api: AgentLoopCheck -->

<!-- api: AgentLoopLabels -->

<!-- api: AgentLoopStationChangedEventArgs -->

The enums:

| Type | Values |
|---|---|
| `AgentLoopStation` | `Gather`, `Act`, `Verify` |
| `AgentLoopCallStatus` | `Success` (default), `Error` |
| `AgentLoopPhase` | `Ready`, `Running`, `Paused`, `Done`, `Stopped` |

`AgentLoopDefaults` holds the sample run (`CreateIterations()`, `CreateModel()`, `Task`, `Summary`), so a page can
start from it and change one loop.

## Accessibility

- The card is a group named by its `Title`. The ring drawing (track, chevrons, pins, head and trail) is hidden from
  assistive technology; the station names, the center, the chips and the context blocks are real text.
- The context stack is a list named "Context blocks", read in the order the blocks were gathered. A block's token
  count is followed by the word "tokens".
- A result is read with the tool's name first ("Tracker returned: …"), and a failed call says "failed". A check shows
  an icon and the words "Check passed" or "Retry", so it never relies on color.
- The status pairs each state with an icon and a word: Ready, Running, Paused, Done and Stopped.
- A polite live region speaks each beat when you step or pause (a block that joined, a call in progress, a result, a
  check) and the summary at the end. It stays quiet while the run plays on its own.
- When stepping reaches the end, Next step turns off and keyboard focus moves to Restart instead of falling to the page.
- Keyboard focus shows a 2px ring around the buttons and the speed control; pointer focus shows none.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through play or pause, Next step, Restart and the speed control |
| `Enter` / `Space` on play | Play or pause; once the run ended, play it again |
| `Enter` / `Space` on Next step | Play to the next beat and stop there |
| `Enter` / `Space` on Restart | Start over from Ready and play |
| `Left` / `Right` (or `Up` / `Down`) on the speed control | Move between 0.5×, 1× and 2×, wrapping around |
| `Home` / `End` on the speed control | Choose 0.5× or 2× |

## Motion

- The head eases between stations (0.7 s, 0.5 s and 0.7 s at 1×) and drags a gradient trail along the ring that is
  longest halfway through a trip and gone when it lands, so it never sits still with a tail.
- The active station fills, a thin ring settles around its node, and its name carries a calm shimmer.
- A tool call drops out of the ring: the pin under Take action draws down in 180ms and the chip lands at its end with a
  6px drop and a slight scale, on a spring without bounce. Its spinner crossfades to a check or a cross. A second call
  takes the place of the first after it fades out in 80ms.
- A context block fades in with a short drop while the token total counts up over 500ms.
- The loop counter rolls its number; the status, the caption and the center line crossfade.
- At the end the ring draws in clockwise over 760ms and the head fades out. A stopped run leaves the ring neutral.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Opening | Plays when first seen | Opens on the finished run | Opens on the finished run |
| Head between stations | Eases along the ring with a trail | Jumps at the end of each trip | Jumps at the end of each trip |
| Chips and blocks | Drop in on a spring, fade out when replaced | Appear in place | Appear in place |
| Token total | Counts up over 500ms | Jumps to the total | Jumps to the total |
| Station colors and chip borders | Change over 240ms (480ms for a new block) | Change at once | Change at once |
| Done ring | Draws in over 760ms | Shown drawn | Shown drawn |

## Related

- `ReasoningTree` for a search over candidate thoughts rather than a loop of tool calls.
- `PromptCache` for how a shared prompt prefix is reused across requests.
- `SpeculativeDecoding` and `MixtureOfExperts` for how a model produces its tokens.

| Component | Choose it when |
|---|---|
| `ChatThread` | You show a real conversation with an agent, message by message |
| `Timeline` | You list what a real run did, in order, with times |
| `Alert` | You report the state of a real job in text |
| `ReasoningTree` | The story is choosing between candidate thoughts |
| `DataFlow` | The story is data moving through a pipeline |
