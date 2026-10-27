---
title: Line replay
summary: Watch a day of data unfold: the line draws itself as a clock runs, events you mark pop up on the chart when it reaches them, and you can pause, drag to any moment or change the speed.
---

<Demo name="Preview" />

## When to use

`LineReplay` is for data whose order is the story. Play it and one series is drawn across its day; each event you marked
appears on the line when the drawing gets there, so readers see the trigger and the reaction one after the other: the
announcement, then the climb; the outage, then the dip.

- **Looking back at a launch or a campaign.** See which announcement, email or mention moved the numbers.
- **After an incident.** Walk the team through what the metric did between the alert, the fix and the all-clear.
- **Data stories.** A day, a session or a match where the order of events is the point.

## When not to use

- **Comparisons.** When several series or periods should be read side by side, `LineChart` (or `Small multiples`) shows them
  without waiting for a replay.
- **Editable annotations.** Moments come from your data; readers cannot add their own.
- **Two related measures.** Plot one against the other instead of replaying a single line.

## Usage

The block derives from Avalonia's `TemplatedControl`. `Points` are `LineReplayPoint`s on one clock in minutes from the
start (midnight in the demos), each with a value and optionally a `Baseline`; they do not need to be evenly spaced.
`Moments` are `LineReplayMoment`s on the same clock with a title, an optional detail and a `Kind` that picks the icon.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LineReplay Title="Release day"
                    SeriesLabel="Release day"
                    BaselineLabel="Typical Wednesday"
                    ValueLabel="Active">
        <arc:LineReplayMoment Id="notes" Time="390" Title="Release notes published" Kind="Launch" />
        <arc:LineReplayMoment Id="queue" Time="705" Title="Sign-up queue backed up" Kind="Incident" />
    </arc:LineReplay>
</UserControl>
```

```csharp
replay.Points = samples.Select(s => new LineReplayPoint(s.Minute, s.Active, s.TypicalActive)).ToArray();
```

### Anatomy

- **Header**: `Title`, `Description`, and a legend for the line (`SeriesLabel`) and the dashed comparison (`BaselineLabel`).
- **Readout**: the time, the value (`ValueLabel`), the ratio to the comparison ("Versus typical Wednesday 2.6×") and the peak
  so far with its time. Each value keeps its width while it plays, so the columns never shift.
- **Chart**: a lane for the callout above the plot, the value grid, the comparison drawn in full, the area and the line up
  to the playhead, a marker and a stem for each moment passed, the value on the tip and a clock on the time axis.
- **Transport**: the play button (Play, Pause, or Replay at the end), the scrubber with a tick per moment, and the speed
  (0.5×, 1×, 2×, 4×).
- **Key moments**: one row per moment with its time and detail; once its window has played it also shows its effect, the
  rise over the next hour or, for an incident, the dip over 20 minutes. From 1000 wide the rows sit in a column beside the
  chart; below, under it in two columns, and in one column below 560.

### Playing

`Duration` is how long a full replay takes at 1× (16 seconds by default) and `Speed` sets the pace. `DefaultPosition`
chooses where the playhead rests before the first play: `End` (the default) shows the whole line with every moment, and
Replay rewinds and plays; `Start` shows an empty chart that the first play draws. Leave `Baseline` out of every point and
the comparison, its legend entry and the ratio disappear.

<Demo name="FromStart" />

### From code

`Play()`, `Pause()`, `Replay()` and `TogglePlay()` do what the button does. `Seek(minutes)` glides the playhead to a time,
and `SeekToMoment(moment)` to a moment; if it was playing it continues from there. `Position` and `IsPlaying` report the
state. `MomentReached` fires when playback passes a moment, not when someone scrubs or jumps, so it is safe for analytics.
`AutoPlay` plays from the start the first time the block is 40% in view (not with reduced motion).

<Demo name="Events" />

### Formats and words

`TimeFormatter` prints times ("9:40 am" by default), `TickFormatter` the axis labels ("6 am") and `ValueFormatter` the
values ("2,316"). The button texts and names (`PlayText`, `PauseText`, `ReplayText`, `PlayLabel`, `PauseLabel`,
`ReplayLabel`), the readout labels (`TimeText`, `VersusFormat`, `PeakText`, `PeakFormat`), the effects (`RiseFormat`,
`DipFormat`), `MomentsTitle`, the screen reader names and the announcements are properties with English defaults.

## API reference

<!-- api: LineReplay -->

<!-- api: LineReplayMoment -->

<!-- api: LineReplayPoint -->

<!-- api: LineReplayMomentKind -->

<!-- api: LineReplayPosition -->

## Accessibility

- **Tab** moves through the play button, the scrubber, the speed control and each key moment.
- On the scrubber, **Space**, **Enter** or **K** plays or pauses (at the end, replays from the start); **Left** and **Right**
  step one point, or an hour with **Shift**; **Page up** and **Page down** step an hour; **Home** and **End** jump to the
  start or the end. In the speed control the arrow keys choose the speed.
- **Enter** or **Space** on a key moment moves the playhead to it.
- The scrubber reports itself as a slider in minutes. Its spoken value ("9:40 am, 2,316 active") changes only while
  playback is stopped, to keep screen readers quiet during a replay.
- Everything the line shows is also in the readout as text, so the drawing is left out of the accessibility tree.
- A polite announcement follows each moment passed, each pause or jump, and the end of the replay ("Replay finished. Peak
  4,894 at 10:40 am.").
- The play button's name says what pressing it will do: Play replay, Pause replay, Replay from the start. The most recent
  moment passed is flagged as the current one; its effect stays silent until that stretch has been played.
- An incident is told apart by its warning icon and its own title as well as by its color.

## Motion

- **Playback** runs at every motion level: the viewer asked for it and can stop it. One position moves forward each frame,
  and the line, the playhead, the labels, the scrubber and the readout are all drawn from it.
- **Rewinding and jumping** move the playhead on the smooth spring (no overshoot); the drawn line shrinks or grows with it,
  and a second jump during the first continues from the current spot.
- **Reaching a moment**: its marker grows from 40% to full size as it fades in, a dotted stem appears under it, and its card
  lifts 8px into place above the plot; the card it replaces fades out in 120 ms.
- **Hour labels** near the moving clock fade (160 ms) rather than sit under it. The moment rows change color in 160 ms and
  show their effect in 240 ms.
- With reduced motion the replay still plays, but rewinds, jumps, markers and cards simply land, and `AutoPlay` does
  nothing.

## Related

- `LineChart` for several series on one plot.
- `Small multiples`, the other Line charts block, for many series side by side.
- `SegmentedControl` and `Button`, which the transport is made of.
