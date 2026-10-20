---
title: Countdown
summary: A launch countdown with rolling digits that morphs into a live state at zero.
---

## When to use

- A launch or event page where the time left to a fixed moment is the main message.
- A compact deadline in a banner, a table row or a sale window that switches to "Live now" when it opens.

## When not to use

- A banner with a countdown and an action button: use `AnnouncementBar`.
- A share of finished work: use `Progress`.
- A timer that the person must read to the second in a live region: `Countdown` is not announced while it runs.

## Usage

Set `Target` to the moment to count down to, or `Duration` to count down a fixed length from when the control
is shown. The default is the large variant with days, hours, minutes and seconds.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Countdown Label="Launch in" />
</UserControl>
```

```csharp
launch.Target = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
```

### Compact deadline

`Variant` set to `Compact` shows one pill that is sized to the font. Use it in tight places.

```xml
<arc:Countdown Variant="Compact"
               Units="Hours, Minutes"
               Label="Offer ends in" />
```

### Units

`Units` lists the units to show, largest first. The largest unit listed absorbs the units that are not shown, so
the default is days through seconds.

```xml
<arc:Countdown Units="Minutes, Seconds"
               Duration="00:00:12" />
```

### Wall time in a time zone

Parse a target with `ResolveTarget` when the text has no offset. Target text with `Z` or an offset is exact.
Wall time is read in the time zone you pass, so every visitor counts down to the same moment.

```csharp
countdown.Target = Countdown.ResolveTarget("2030-01-01T09:00:00", timeZone);
```

### Pause, resume and restart

A duration countdown keeps its remaining time while it is paused. A target countdown keeps counting to the same
moment, so pausing only freezes the display.

```xml
<arc:Countdown Duration="00:00:12"
               IsPaused="{Binding IsTimerPaused, Mode=TwoWay}" />
```

```csharp
timer.Pause();
timer.Resume();
timer.Restart(); // starts a duration again from its full length
```

### Completion

`CompleteContent` replaces the digits at zero, beside a pulsing live dot. It defaults to "Live now". Handle
`Completed` to react once, at the moment the countdown reaches zero.

```csharp
timer.CompleteContent = "Registration is open";
timer.Completed += (_, _) => RefreshOffers();
```

## API reference

<!-- api: Countdown -->

## Accessibility

- `Label` names the countdown. The default is "Time remaining".
- The control has no live region. Spoken text gives the time left without seconds when more than a minute remains,
  and reads `CompleteContent` once the countdown is over.
- The visible digits are not announced as they change, so a screen reader does not repeat every second.
- The control is not focusable and has no keyboard interaction.

## Motion

- Each digit is a wheel that turns the short way round, with soft edges at the top and bottom of the window.
- The digits roll in one after another the first time they are shown. After that they change once per second.
- Units appear and disappear with the container springing to its new size.
- At zero the digits fade and scale into the completion label, and the live dot pulses.
- Reduced motion makes the wheels jump to the new value, sizes the container at once, switches content with a
  fade and stops the pulse.

## Related

- `AnnouncementBar` for a site banner with a countdown and an action button.
- `Progress` when the work has a known amount of progress rather than a deadline.
- `Stepper` when the countdown is one part of a multi-step flow.
