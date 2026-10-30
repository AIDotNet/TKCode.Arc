---
title: Agent timeline
summary: A replay of a multi-agent run on one time axis: each agent's thinking, tool calls and waiting, the handoffs between them, and the time, tokens and cost.
---

<Demo name="Preview" />

## When to use

`AgentTimeline` shows one orchestrated run of several AI agents as a compact Gantt chart. The orchestrator sits on the
top row and each subagent gets a row below it, with a color dot, its name and the model it runs on. Spans on a shared
time axis say what each agent did and when: thinking is a tint of the agent's color, a tool call is solid, and waiting
is hatched. Thin elbow connectors show work handed down to a subagent and results coming back up. The run replays
with a playhead while the header counts the elapsed time, the tokens and the cost, and it ends on a summary with the
parallel speedup.

- **Run history and trace viewers**: show where the time of a finished multi-agent run went, which agents worked in
  parallel and which ones only waited.
- **Debugging panels**: a failed tool call and its retry stand out with a cross and a retry arrow, and pinning a span
  traces its handoff path through the run.
- **Product demos and changelogs**: replay a real run with its own numbers for time, tokens and cost, at 1x or faster.
- **Following a run as it happens**: live mode grows open spans to the current time while your backend appends events.

## When not to use

- **One agent's steps and answer**: when people need the agent's reasoning, sources and output rather than the timing,
  show the transcript with `ToolCall` rows or an `AgentPlan`.
- **One agent's state at a glance**: use `AgentStatus` for a compact live pill.
- **Distributed request tracing**: spans of services and network calls belong in a tracing view, not here; the
  timeline's vocabulary (thinking, tool calls, waiting, handoffs) is about agents.
- **Long traces of thousands of steps**: the timeline handles a few hundred spans comfortably. For longer traces pass a
  window of the run or merge tiny spans first.
- **An explanation of how agents work in general**: use the `AgentLoop` illustration, which tells that story with
  invented steps.

## Usage

Given no data, the timeline is empty: the title "Agent run" over an empty axis, with nothing to play. The package ships
no sample run. The preview above gives it a run the gallery writes and sets `AutoPlay="True"`, so it replays the first
time it is about a fifth in view. Register the package with `UseArcSpecial()` next to `UseArcControls()`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentTimeline />
</UserControl>
```

`AgentTimeline` derives from Avalonia's `TemplatedControl`. It fills its container up to 744px; it reads best between
360 and 760px wide.

### Anatomy

1. **Header**: the `Title` (else the run's title, else "Agent run") and the status line under it. While the run
   replays the line reads the state (Running with a shimmer, Paused or Ready), the elapsed time, the tokens and the
   cost; at the end it turns into a check and the summary: agents, length, parallel speedup and cost.
2. **Agent labels**: one 44px row per agent, the orchestrator first: a color dot, the name, and under it the model's
   mark and name.
3. **Plot**: the spans of every agent on one time axis, with grid lines at the ticks. A span that is still running has
   a square leading edge and a short shine; a finished span has round corners.
4. **Connectors**: an elbow from the orchestrator down to a subagent's next span (a dispatch) or from a subagent's last
   span up to the orchestrator (a result), with a dot where it meets the upper row.
5. **Playhead and axis**: a thin line at the current time, and under the rows the axis with its tick labels and the
   scrubber knob.
6. **Footer**: play or pause (replay once finished), restart and the speed control on the left; the legend (Thinking,
   Tool call, Waiting, Handoff, Failed) on the right, or under them when there is no room.
7. **Span card**: hover or focus a span to see who did it on which model, what it did, its tool or kind, its length and
   tokens, and notes for a failure, a retry or its handoffs.

### The preview's run

The preview replays an invented incident investigation, "Signup emails bouncing since Mar 3", written by the gallery. A
coordinator plans three checks and hands them to a status watcher, a config auditor and a log reader, which work in
parallel. The log reader's first scan fails and a narrower query succeeds on the second try. The coordinator combines
the reports, has a scribe write the incident note and files it. Five agents, 30 spans and eight handoffs take 26.8
seconds and 49,920 tokens, a 1.5x parallel speedup. The agents run on real models (Qwen3.8-Max, Grok 4.3, Qwen3.7-Plus,
Qwen3.8-Flash and Mistral Small 4), and each agent's `Price` is a blend of its model's list prices (three input
tokens to one output token) as the vendors published them on 2026-10-29, so the cost is the invented token counts at
real list prices. The task, the timings and the tokens are invented.

### Your own run

Give the timeline an `AgentTimelineRun`: its agents in row order (the orchestrator first), the spans with times in
seconds from the start of the run, and the handoffs. A run of your own opens finished, ready to replay; set
`AutoPlay="True"` to replay it when it comes into view instead.

<Demo name="OwnRun" />

```xml
<arc:AgentTimeline>
    <arc:AgentTimeline.Run>
        <arc:AgentTimelineRun Title="Monthly refund summary">
            <arc:AgentTimelineRun.Agents>
                <arc:AgentTimelineAgent Id="planner" Name="Planner" Price="8" />
                <arc:AgentTimelineAgent Id="clerk" Name="Data clerk" Price="0.2" />
            </arc:AgentTimelineRun.Agents>
            <arc:AgentTimelineRun.Spans>
                <arc:AgentTimelineSpan AgentId="planner" Kind="Thinking" Start="0" End="1.2"
                                       Label="Outlined the summary" Tokens="1100" />
                <arc:AgentTimelineSpan AgentId="planner" Kind="Waiting" Start="1.2" End="5.8"
                                       Label="Waiting on the data clerk" />
                <arc:AgentTimelineSpan AgentId="clerk" Kind="Tool" Start="1.5" End="3.9"
                                       Label="Exported last month's refunds" Tool="Ledger"
                                       ToolMark="{x:Static arc:ArcIcons.Database}" Tokens="2200" />
            </arc:AgentTimelineRun.Spans>
            <arc:AgentTimelineRun.Handoffs>
                <arc:AgentTimelineHandoff From="planner" To="clerk" At="1.2" Label="Which refund reasons grew?" />
            </arc:AgentTimelineRun.Handoffs>
        </arc:AgentTimelineRun>
    </arc:AgentTimeline.Run>
</arc:AgentTimeline>
```

```csharp
timeline.SpanSelected += (_, e) => details.Show(e.Span);   // null when the pin is removed
```

- **Agents**: `Id` names the agent in spans and handoffs. `Model` is an `AgentModel` (a name and a 24×24 stroked mark
  such as an `ArcIcons` glyph). `Price` is a blended rate in US dollars per million tokens. `Color` overrides the row
  color; by default the orchestrator is neutral and the following agents take the chart series colors 1 to 4 in turn.
- **Spans**: `Kind` is `Thinking`, `Tool` or `Waiting`. A tool call names its `Tool` and can carry a `ToolMark`
  (a wrench when none). `Status="Failed"` with a `Detail` marks a failed call; `IsRetry` marks the call that retried
  it, with its note in `Detail`. `Tokens` count up evenly across the span, and `Cost` replaces the cost worked out from
  the agent's price.
- **Handoffs**: the shape is inferred. Work handed down a row is a dispatch: it drops at `At` and turns into the
  target's next span. Work handed up is a result: it leaves the end of the source's last span and rises to the target
  at `At`. Give a dispatch a time just before the target's first span and a result a time just after the source's last.
- `Agents`, `Spans` and `Handoffs` on the timeline override the run's lists, and every list is observed when it raises
  collection changes. Spans are `AvaloniaObject`s, so a new `End` or `Status` reaches the plot at once.

### Numbers and formats

- **Time**: one decimal, rounded half up ("26.8s"); from a minute on "1m 05.3s".
- **Tokens**: each span's tokens accrue evenly from its start to its end, summed and rounded, with thousands separators.
- **Cost**: each span's tokens times its agent's price per million (or the span's own `Cost`), with two decimals.
- **Speedup**: the time agents spent thinking and calling tools, summed over every agent, divided by the run's length.
  Waiting is idle time and does not count. Under 1.05 the summary says "no parallel speedup".
- The counters update about twenty times a second and each one keeps the width of its final value, so the header never
  shifts while it counts.

The axis is as long as the run. Its tick step is the smallest of 0.1, 0.2, 0.5, 1, 2, 5, 10, 15 and 30 seconds, then
1, 2, 5, 10, 15 and 30 minutes, that gives at most six ticks from zero, at least 40px apart: 26.8 seconds shows 0 to
25 in steps of five at any width, 6.2 seconds steps by two and a minute steps by 15.

### Playback and speed

`IsPlaying` and `Speed` are two way: the footer writes them and an app can bind them. Play continues from where the
replay stands; once it has finished, it starts over. Restart always goes back to zero and plays at the current speed.
`Speeds` lists the options of the speed control (1x and 2x by default); with one option or none the control hides.
`Play()`, `Pause()`, `Restart()` and `Seek(seconds)` do the same from code. `PlaybackChanged` reports each action a
person takes (play, pause, restart, a seek, a speed), and `Completed` fires each time a replay reaches the end.

<Demo name="Speeds" />

At 1x the replay runs in real time. A single frame advances at most 0.1 seconds times the speed, so a stalled window
loses a moment instead of jumping ahead, and the clock stops while the timeline is off screen.

Pressing or dragging anywhere on the axis moves the replay there and pauses it; the knob then takes keyboard focus.
`Phase` is Ready at zero, Running while it plays, Paused part way and Finished at the end.

### Pinning a span

Click a span, or press Enter on it, to pin it. The card then stays, and everything off the span's handoff path dims:
spans to 28% and connectors to 16%. For a span of the orchestrator, the path is the span itself, the handoffs that start
or end during it and the spans at their other ends. For a subagent's span, it is that agent's whole row, every handoff
to or from the agent and the spans at their other ends. Click the span again, press Enter again or press Escape to
unpin. `PinnedSpan` is two way, and `SpanSelected` reports each change.

### Live runs

Set `IsLive` while a run is in progress. The time runs from the run's `StartedAt` (or from when live mode starts),
spans without an `End` grow to now, and the playback controls hide. Append spans and handoffs as events arrive and set
each span's `End` when it closes. The axis grows in calm steps: when the time passes 90% of it, it springs to a round
length past one and a half times the time (5, 8, 12, 20, 30, 50 seconds and so on). Set `IsLive` to false when the run
ends: the timeline settles on the summary, announces it and raises `Completed`.

<Demo name="Live" />

```csharp
timeline.Run = liveRun;            // an AgentTimelineRun your event handler appends to
timeline.IsLive = true;
// on each event
liveRun.Spans.Add(new AgentTimelineSpan("logs", AgentTimelineSpanKind.Tool, 3.1, null, "Bounce scan"));
span.End = 5.2;
// when the run ends
timeline.IsLive = false;
```

`AgentTimelineLiveDriver` plays a finished run into a timeline as if it were happening now, which is how the demo above
works.

### Narrow containers

The layout follows the timeline's own width, not the window's. Below 520px the label column narrows from 176 to 132px
and shows only the dot and the name (the span card still names the model), and the side padding drops from 24 to 16px.
When the plot would be narrower than 300px it keeps 300px and scrolls sideways with the labels pinned; a divider appears
beside the labels once it is scrolled, and the replay keeps the playhead in view unless the reader scrolled in the last
2.5 seconds. The status line wraps after a separator, and the legend moves under the controls and wraps on its own.

<Demo name="Narrow" />

### Content guidelines

Write each span label as what the agent did, in the past tense and in a few words; the card shows it in full but the
accessible name reads it with the agent, kind and times around it. Name tools by what they are in your product, with
generic glyphs as marks. Keep handoff labels to one short sentence: they appear as "From Coordinator: …" and
"To Coordinator: …" in the cards.

| Instead of | Write |
|---|---|
| Tool call | Searched blocklist reports for our domain |
| LLM generation step 3 | Matched the bounces to SPF failures |
| Error | Full scan of mail events ran past the time limit |
| Handoff | Mail config changes since Mar 1 |

### Best practices

**Do**

- Put the orchestrator first, and order subagents so connectors cross idle rows: the agent that returns first right
  under the orchestrator, dispatches to the deepest row first.
- Mark blocked time as `Waiting`, so parallel work and idle time read at a glance and the speedup counts only real work.
- Mark a failed call with `Status="Failed"` and a `Detail`, and the call that retried it with `IsRetry`.
- Give agents a `Price` so the cost is real, or leave it out everywhere rather than on some agents only.

**Avoid**

- Brand marks of real model vendors or services. Use generic glyphs and name the model in text.
- Replacing the run object on every event in a live run; append to its lists instead.
- Several replaying timelines on one screen. Each one draws on every frame while it plays.

### Common questions

**Why does my run open finished?** A run of your own is a record, so it opens complete; set `AutoPlay="True"` to replay
it when it comes into view. Under reduced motion every timeline opens finished.

**Why is a connector only a short stub?** A dispatch looks for the target's first span starting at or after its time.
In a live run that span has not arrived yet; the elbow completes when it does.

**Why does the card show a longer time than the span?** The card always shows a span's final length, tokens and
handoffs, even while the replay is still drawing it.

**Can I translate it?** Set `Labels` to an `AgentTimelineLabels` with your words; sentences with numbers are functions,
so the word order can change.

## API reference

<!-- api: AgentTimeline -->

<!-- api: AgentTimelineRun -->

<!-- api: AgentTimelineAgent -->

<!-- api: AgentTimelineSpan -->

<!-- api: AgentTimelineHandoff -->

<!-- api: AgentTimelineLabels -->

<!-- api: AgentTimelineSpanSelectedEventArgs -->

<!-- api: AgentTimelinePlaybackChangedEventArgs -->

<!-- api: AgentTimelineLiveDriver -->

<!-- api: AgentModel -->

The enums and values:

| Type | Values |
|---|---|
| `AgentTimelineSpanKind` | `Thinking`, `Tool`, `Waiting` |
| `AgentTimelineSpanStatus` | `Ok` (default), `Failed` |
| `AgentTimelinePhase` | `Ready`, `Running`, `Paused`, `Finished`, `Live` |
| `AgentTimelinePlaybackAction` | `Play`, `Pause`, `Restart`, `Seek`, `Speed` |
| `AgentTimelineSummary` | `AgentCount`, `Duration`, `Speedup`, `Tokens`, `Cost` (the `Summary` property) |

## Accessibility

- The timeline is named by its title. Each span is a button to assistive technology, named with the agent, the kind,
  the tool, "failed" or "retry", what it did, its time range and its tokens ("Log reader, Tool call, Mail logs, failed:
  Bounce scan took too long, 3.1s to 5.2s, 430 tokens"); a pinned span reads as pressed. The spans are one tab stop and
  the arrow keys move between them.
- The plot is drawn, so the spans have no control each: a focus stand-in sits on the span the keyboard is on and
  carries its name, and each arrow key moves focus to a fresh stand-in so screen readers announce the new span.
- The scrubber is a slider named "Run time" with the time as its value and help text such as "12.4s of 26.8s".
- Status is never color alone: a failed call shows a cross and "Failed" in its card and the legend, a retry shows an
  arrow and its note, and the finished state shows a check with the summary.
- A polite live region speaks when the reader acts: "Playing at 1x" on play, restart or a speed change while playing,
  "Paused at 11.1s" on pause or a seek, and the run's summary at the end. The first replay on sight is silent, and the
  counting status line is not a live region (its accessible name holds the counts and how many agents are busy).
- The span card repeats what the span's name already says, so it is hidden from assistive technology.
- Keyboard focus shows a 2px ring on the spans, the knob and the controls; pointer focus shows none.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the spans (one stop), the scrubber, play or pause, restart and the speed control |
| `Left` / `Right` on a span | The previous or next span in the same row |
| `Up` / `Down` on a span | The span in the row above or below whose middle is closest in time (rows with nothing yet are skipped) |
| `Home` / `End` on a span | The first or last span of its row |
| `Enter` / `Space` on a span | Pin it and highlight its handoff path; again to unpin |
| `Escape` on a span | Unpin |
| `Left` / `Down`, `Right` / `Up` on the scrubber | Half a second back or forward (and pause) |
| `Shift` + arrows, `Page Down` / `Page Up` on the scrubber | Five seconds back or forward |
| `Home` / `End` on the scrubber | The start (Ready) or the end of the run (Finished) |
| `Left` / `Right` on the speed control | The previous or next speed, wrapping around |

## Motion

- The playhead, the knob and every active span move with the replay clock on each frame; the spans grow without any
  layout pass. The counters update about twenty times a second.
- An active thinking or tool span has a square leading edge and a 28px shine that sweeps across it in 1.05 seconds
  every 1.5 seconds, also while paused. When the span ends, its right corners round off, the edge fades and a failed
  call turns to its failed look over 160ms; the cross or the retry arrow appears over 160ms.
- A connector draws in over 360ms from the side the handoff leaves: a dispatch from the top, a result from the bottom.
  Its dot pops in over 160ms, at once for a dispatch and 260ms later for a result, once the line has reached it.
- Pinning dims and undims over 160ms; hovering a thinking or tool span deepens its color over 160ms.
- The span card fades in over 160ms while rising 3px, glides between spans on a critically damped spring (about 20
  rad/s), springs to its new height and crossfades its content (100ms out, 160ms in); it leaves after an 80ms grace with
  a 120ms fade.
- When the run ends the playhead fades out over 240ms and the status line crossfades to the summary (160ms in, 100ms
  out); the play button's glyph crossfades too. The knob grows to 1.3x over 160ms while focused or dragged.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Opening | Replays the run when it comes into view if `AutoPlay` is on | Opens finished | Opens finished |
| Playback | Plays on request | Plays on request | Plays on request |
| Shine on active spans | Sweeps every 1.5 s | None | None |
| Span completion, dimming, hover | 160ms | At once | At once |
| Connector draw-in and dot pop | 360ms and 160ms | Appear at once | Appear at once |
| Card position and height | Springs | Jump | Jump |
| Card fade and content crossfade | 160ms / 120ms, 100 / 160ms | Kept (short crossfade) | At once |
| Status line and playhead | Crossfade, 240ms fade | At once | At once |

## Related

- `AgentStatus` for one agent's live state in a compact pill.
- `ToolCall` for the input and output of a single tool call.
- `AgentPlan` for the plan an agent follows, step by step.
- `ContextWindow` for how full a model's context is.
- `AgentLoop` for an illustration of how a single agent loops through context, tools and checks.

| Component | Choose it when |
|---|---|
| `AgentStatus` | You show what one running agent is doing now |
| `ToolCall` | People need one call's input and output |
| `AgentPlan` | The story is the plan, not the timing |
| `ContextWindow` | The question is how much of the context is used |
| `AgentLoop` | You explain the agent loop with an invented story |
