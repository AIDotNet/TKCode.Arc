---
title: Time dial
summary: A round dial for picking a time of day or a length of time. Drag the handle around the face; it snaps to steps and answers the arrow keys.
---

<Demo name="Preview" />

## When to use

`TimeDial` picks a time by turning: the handle runs round a track on a clock face and the readout in the middle shows the
value. It snaps to a step as it goes, so a rough drag still lands on 7:30 rather than 7:31. As a clock, one turn is twelve
hours and carrying the handle past 12 switches between AM and PM; as a timer, one turn is a length you choose, the track
fills from 12 o'clock, and the handle may go round more than once.

- **Alarms and reminders**: a wake-up time, a quiet-hours start.
- **Timers and sessions**: focus blocks, cooking timers, a meeting length.
- **Touch-first screens**: a large target that works with a thumb.

## When not to use

- **Exact times typed from memory**: a time field with typing is faster; use `TimePicker`.
- **Long ranges**: days or weeks do not fit on a dial.
- **Dense forms**: the dial needs about 240 px square to be comfortable.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TimeDial Label="Wake-up time" Value="7:30:00" />
</UserControl>
```

`TimeDial` is in the Special package; register it with `UseArcSpecial()` next to `UseArcControls()`. `Value` is a
`TimeSpan` and can be bound both ways. It changes while the handle moves; `ValueCommitted` is raised once the hand lets
go, a key moves the value, or AM and PM switch. `ValueText` gives the value as words, such as "7:30 AM".

### A length of time

Set `Mode` to `Duration`. One turn adds `TurnLength`, the value stays between `Minimum` and `Maximum`, and the track fills
from 12 o'clock, a full ring for every complete turn.

<Demo name="Duration" />

### Steps and the 24-hour clock

`Step` is what the value snaps to and what the arrow keys move by; `LargeStep` is what Page Up and Page Down move by.
`Is24Hour` shows 18:45 instead of 6:45 PM. The dial takes the size it is given, up to 240 px.

<Demo name="Clock24" />

### Best practices

**Do**

- Name the dial with `Label`, like "Alarm time": it is what assistive technology reads.
- Choose a `Step` that matches the task: 5 minutes for alarms, 15 for bookings, 1 for timers.
- Show the result in words nearby when it matters, using `ValueText`.

**Avoid**

- A tiny dial: below about 160 px the handle is hard to grab.
- Using the dial for values that are not times.

## API reference

<!-- api: TimeDial -->

## Accessibility

- The dial is a slider named by `Label`. Its value is in minutes, and its help text reads the value as words ("7:30 AM",
  "1 hr 25 min").
- It is a tab stop. Pointer focus draws no ring; keyboard focus shows the focus ring around the handle.

### Keyboard interactions

| Key | Action |
|---|---|
| Up or Right | Adds one step. |
| Down or Left | Takes away one step. |
| Page Up / Page Down | Adds or takes away `LargeStep`. |
| Home / End | Goes to the start or the end (12:00 AM and the last step before midnight on a clock). |
| A / P | Switches to AM or PM on a 12-hour clock. |

## Motion

- The handle follows the value on a spring: a snappy one while it is dragged, so each step clicks into place, and a
  smoother one for a jump, such as a press on the far side of the track. On a clock it always goes the short way round.
- The handle grows a little while it is held.
- Frames run only while a spring moves.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Drag and keys | Handle springs to each step | Jumps | Jumps |
| Press on the track | Handle springs there | Jumps | Jumps |
| Holding the handle | Grows by 12 percent | No change in size | No change in size |

## Responsive behavior

- The dial is square and takes up to 240 px (`DialSize`), shrinking to fit a smaller space.
- A press counts on the track from a little inside or outside it, so the handle is easy to catch with a finger.
- A drag that leaves the dial keeps working: the angle is read around the dial's center wherever the pointer is.

## Related

- `TimePicker` types or picks a time from lists.
- `Countdown` shows a time running out.

| Component | Choose it when |
|---|---|
| `TimeDial` | Turning to a time feels natural, often on touch |
| `TimePicker` | People type or pick an exact time |
| `Slider` | A plain number on a line |
