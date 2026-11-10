---
title: Research run
summary: A deep-research run from question to cited report: a stage stepper, the plan, sources filling as they are read, the notes taken from them, and a report whose numbered citations open their sources.
---

<Demo name="Preview" />

## When to use

`ResearchRun` shows a research agent at work on a question that needs many sources. A small stepper says where it is
(plan, read, write, report) and how long it has taken. The plan lists the steps the agent set itself and ticks them off.
The sources it found fill a reading line each while they are read, and a source that is left out is struck through
with the reason. Findings land in the notes with the numbers of the sources behind them. When the report is ready the
whole process folds into one line, and the report takes the stage with numbered citations that open their sources.

- **Deep research and literature review assistants** that take minutes and read dozens of pages.
- **Due diligence, market scans and support investigations** where the reader must check where each claim comes from.
- **Showing a stored research report** with its sources and notes.

## When not to use

- **A quick answer with a few sources.** Use `AI answer`, which cites sources inline in one reply.
- **Agents that act rather than read** (edit files, call tools). Use `Agent run`.
- **A plain list of search results.** Use `Search results`.

## Usage

The block derives from Avalonia's `TemplatedControl`. With no properties it plays a built-in sample run. In an app, set
the question and the plan, add `ResearchRunSource` items as the agent finds them and update their status and progress,
add notes as they are taken, and set the report as it is written:

```xml
<arc:ResearchRun Question="{Binding Question}"
                 Phase="{Binding Phase}"
                 Plan="{Binding Plan}"
                 CompletedPlanSteps="{Binding PlanDone}"
                 Sources="{Binding Sources}"
                 Notes="{Binding Notes}"
                 Report="{Binding Report}" />
```

```csharp
research.StopRequested += (_, _) => agent.Cancel();
research.RestartRequested += (_, _) => StartAgain();
research.SourceInvoked += (_, e) => { OpenInReader(e.ResearchSource); e.Handled = true; };
```

### Phases and the clock

`Phase` moves from `Planning` to `Reading`, `Writing` and `Done` (or `Stopped`). While the run plans, reads or writes the
clock counts on its own from `Elapsed` and the button reads Stop; afterwards it reads Research again. When the phase
becomes `Done` the process folds into a summary line ("Read 6 sources, took 9 notes in 2m 31s") with Show process.

### Sources, notes and citations

A `ResearchRunSource` is an `AiSource` (number, title, site, address, excerpt) with a `Status` (queued, reading, read,
skipped) and a `Progress` from 0 to 1. Notes are `ResearchRunNote` records: a finding and the numbers of its sources.
In the report, write `[3]` after a claim to cite source 3; the mark becomes a small numbered button with the source's
title as its tooltip. Pressing a citation or a source row raises `SourceInvoked`; when nobody handles it the source's
http or https address opens.

<Demo name="Finished" />

## API reference

<!-- api: ResearchRun -->

<!-- api: ResearchRunSource -->

<!-- api: ResearchRunNote -->

<!-- api: ResearchRunLabels -->

## Accessibility

- The run is a region named "Research"; the question is a heading and each section has a heading.
- **Tab** reaches Stop, the source rows, the citations, Show process and Copy. **Enter** or **Space** opens a source.
- A source row reads its number, title, site and status ("Source 2: Sizing guide, retrofit-guild.example, Read").
- A citation reads "Source 3: Radiators that work at low flow temperatures".
- A polite live region says when each phase starts and when the report is ready.
- Status is shown by words and glyphs, never by color alone.

## Motion

- The stepper's dots and connectors change color over 240ms; reading lines fill smoothly.
- The process folds and opens on a spring with a short fade.
- Reduced motion keeps short fades; Off applies every change at once.

## Responsive behavior

- The run is at most 860 wide and follows its container below that.
- At 640 or less the plan and the sources stack and the paddings tighten.
- Long titles and sites shorten with an ellipsis; the report wraps.

## Related

- `Agent run` for agents that act through tools.
- `AI answer` for a single cited reply.
- `Agent tasks` for many agents at once.
