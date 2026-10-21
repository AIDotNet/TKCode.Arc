---
title: Countdown
summary: A launch countdown with rolling digits that morphs into a live state at zero.
---

<Demo name="Launch" />

## When to use

`Countdown` shows the time left until a fixed moment, or through a fixed length of time, as rolling digits. Use it where
the remaining time is the main message of a page or a row. When the moment arrives, the control changes to its live
state by itself, so the screen does not need a timer of its own to switch the wording.

- **Launch and event pages**: a product launch, a conference keynote or a webinar that starts at a fixed time. The
  large variant gives the page a focal point above the fold, and the digits make the start time easy to read at a
  glance.
- **Sale and promotion windows**: a flash sale in a checkout banner or on a pricing page, with `Target` set to the
  closing moment. The control switches to "Live now" when the offer opens, without a server round trip for the change.
- **Deadlines in tables and cards**: a payment due date in a billing list, an invoice that expires or a trial that ends
  in a subscription row. The `Compact` variant is a single pill that fits a table cell at a fixed height.
- **Holds and access windows**: a seat reservation in a ticketing flow, or an early-access window in a developer
  console. `Duration` counts down a fixed length from when the control is shown, and `IsPaused` freezes it while the
  visitor waits on another step.
- **Scheduled maintenance on a status page**: the time left in a maintenance window, with `CompleteContent` set to
  "Maintenance in progress" so the state that follows the countdown is stated in the same place.

## When not to use

- **A site banner with a countdown and an action button**: use `AnnouncementBar`, which carries the message, the action
  and the dismiss control in one strip.
- **Progress through known work**: use `Progress` for a share of finished work. A countdown shows time left, not
  how much of a job is done.
- **Consumption against a fixed limit**: use `UsageMeter` for storage, seats or API calls measured against a quota.
- **Alerts that must reach a screen reader as the deadline nears**: `Countdown` is not a live region and does not
  announce its changes. Raise an `Alert` or a `Toast` at the moment that needs attention.
- **A number that moves with data**: use `AnimatedCounter` for a total that changes as data arrives, such as orders
  received today. A countdown runs on the clock and only moves toward zero.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Countdown Label="Launch in" />
</UserControl>
```

Set `Target` to the moment to count down to, or `Duration` to count down a fixed length from when the control is shown.
The default is the large variant with days, hours, minutes and seconds.

```csharp
launch.Target = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
```

`Countdown` derives from Avalonia's `TemplatedControl`, so the usual surface properties (`Background`, `BorderBrush`,
`BorderThickness`, `CornerRadius` and `Padding`) are forwarded to its shell, and `Foreground`, `FontFamily` and
`FontSize` are set by the theme. The theme sets the digit and unit typography from the Arc tokens, so a page can change
the colors and spacing without touching the template. The countdown does not take focus, and it has no visible label:
`Label` is the accessible name only.

### Anatomy

1. **Shell**: a border that springs to the size of the content it holds, so the width changes when a
   unit group appears or leaves. The large variant has no background or border of its own. The compact variant draws a
   1 px border in the border color on the surface color, with a pill radius and 12 px of horizontal padding.
2. **Digit wheels**: each digit is a clipped wheel that feathers at its top and bottom edges. In the large
   variant the digits use the display font at medium weight with display tracking, and their size follows the width of
   the control: 12.5% of the width, between 28 px and 72 px. In the compact variant the digits use the body font with no
   tracking and the size of the text.
3. **Unit names**: under each group in the large variant, in the muted text color at the extra-small type size. In the
   compact variant, a quiet unit letter follows each group at the small type size.
4. **Separators**: a colon between the groups in the large variant. The compact variant shows the groups in one line.
5. **Completion**: a 10 px success-colored pulse dot and the `CompleteContent` text. In the large
   variant the text uses the display font at medium weight and the size of 11% of the width, between 28 px and 52 px.
   In the compact variant the text uses the body font in the success color.

### Choosing a variant

Use `Large` (the default) when the countdown is the main content of its area: a hero block, an event header or a
dashboard card with room for four groups. The large digits need about 320 px of width to show four groups, so give it a
full column or a card rather than a sidebar cell.

Use `Compact` when the countdown is one fact among others: a banner, a table cell, a card footer or a sentence in a
notice. The pill is at least 32 px high and sizes to its text, so it keeps a table row at its usual height.

<Demo name="Compact" />

```xml
<arc:Countdown Variant="Compact"
               Units="Hours, Minutes"
               Label="Offer ends in" />
```

### Choosing units

`Units` lists the units to show, largest first. The largest unit listed absorbs everything above it, so `Hours` alone
shows 51 hours for a window of two days and three hours. The default is days through seconds, which suits a launch that
is more than a day away. A window measured in hours reads better with `Hours, Minutes`, and a short hold reads best with
`Minutes, Seconds`.

| Value | Shows |
|---|---|
| `Days` | Whole days; the largest unit absorbs the hours and below |
| `Hours` | Whole hours; can exceed 24 when days are not shown |
| `Minutes` | Whole minutes |
| `Seconds` | Whole seconds |
| `All` | Days, hours, minutes and seconds (the default) |

The flags combine, for example `Hours | Minutes`. `None` is treated as `All`. Changing `Units` or `Variant` at run time
updates the groups in place, so a page can switch between a short and a long display without recreating the control.

```xml
<arc:Countdown Units="Minutes, Seconds"
               Duration="00:00:12" />
```

### Target and duration

Use `Target` for a deadline that is the same for every visitor: a launch, a sale close or an expiry date stored on
the server. Use `Duration` for a length of time that starts when the control is shown: a checkout hold, a trial
timer or a review window. When both are set, `Target` takes precedence.

A target countdown keeps counting to the same instant after the visitor leaves and returns. A duration countdown keeps
its remaining time while it is off screen, so a visitor who switches away and back sees the time that was left.

### Time zones and the clock

Parse a target with `ResolveTarget` when the text has no offset. Target text with `Z` or an offset is exact.
Wall time is read in the time zone you pass, so every visitor counts down to the same moment.

```csharp
countdown.Target = Countdown.ResolveTarget("2030-01-01T09:00:00", timeZone);
```

`ResolveTarget` does not read the device's time zone. Wall time is resolved for the date it names, so daylight saving
applies to that date. A wall-clock time that does not exist on that date, because the clocks skipped it, resolves to the
later instant.

Pass a `TimeProvider` through the `TimeProvider` property when the device clock cannot be trusted. A server-synced
provider keeps the display and the completion moment on the same clock for every visitor. The default is
`TimeProvider.System`.

### Pause, resume and restart

A duration countdown keeps its remaining time while it is paused. A target countdown keeps counting to the same
moment, so pausing only freezes the display. `Completed` still fires on time when a target is paused.

<Demo name="DurationWithPause" />

```xml
<arc:Countdown Duration="00:00:12"
               IsPaused="{Binding IsTimerPaused, Mode=TwoWay}" />
```

```csharp
timer.Pause();
timer.Resume();
timer.Restart(); // starts a duration again from its full length
```

`IsPaused` is two-way, so a view model can read the state and a button can bind to it. `Restart` clears the completed
state and starts a duration from its full `Duration` again. For a target, `Restart` re-reads the target.

### Completion

`CompleteContent` replaces the digits at zero, beside a pulsing live dot. It defaults to "Live now". Handle
`Completed` to react once, at the moment the countdown reaches zero.

```csharp
timer.CompleteContent = "Registration is open";
timer.Completed += (_, _) => offers.Refresh();
```

`Completed` is raised only while the countdown is shown and reaches zero. A target that has already passed when the
control is shown displays the completed state at once and does not raise `Completed`, so a page that loads after a sale
closed does not run the offer code twice. `Remaining` is zero once the countdown is complete, and `IsCompleted` tells
the view model which state is on screen.

### Example: Product launch on an event page

A launch page needs one fact above the fold: when the product is available. The countdown is the hero of the block,
with the large variant, a short line of context and one action. The copy names the offer and its end, so the visitor
knows what the number means. The example uses a duration so that the scene is stable on every capture. A launch that every visitor should see
end at the same moment uses `Target` instead, with the moment written in UTC or with an offset.

![A launch hero with a large countdown, an early-access note and a primary action](images/LaunchHero.png)

```xml
<Border Width="680"
        Padding="32"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="24">
        <StackPanel Spacing="6">
            <TextBlock Text="Northwind Commerce 2026 Summer Release"
                       FontSize="20"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Early-access pricing ends when the countdown reaches zero."
                       FontSize="14"
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:Countdown Duration="3.07:42:18" />
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:Button Content="Reserve early access" Variant="Primary" />
            <arc:Button Content="View pricing" Variant="Ghost" />
        </StackPanel>
    </StackPanel>
</Border>
```

The layout gives the countdown the full width of the card, so the large digits scale to the largest size the theme
allows without crowding the copy. The primary action is one verb and an object. The secondary action is a ghost button,
so the page has one clear next step.

### Content guidelines

The countdown has no visible label, so the words next to it carry the meaning. Write the deadline in full beside the
digits, and write `CompleteContent` as a short state that reads correctly after the time has run out. Use sentence case,
numbers as digits, and no urgency words in the copy. The control keeps the digits and the unit names; the surrounding
text says what they count to.

| Instead of | Write |
|---|---|
| `Hurry, the time is nearly up` | `Early-access pricing ends 2026-10-29 at 09:00 UTC` |
| `Time remaining` (as the only text) | `Registration closes in`, with the time zone in the line below |
| `Done` | `Live now` or `Registration is open` |
| `Countdown expired` | `Offer closed`, when the offer no longer applies |

Keep `CompleteContent` to two or three words so it fits the compact pill. Put the time zone in the text, never only in
the countdown, because the digits do not say which zone the moment belongs to.

### Best practices

**Do**

- Set `Target` with an offset or a time zone for a deadline that every visitor shares, because the same moment then
  reads the same on every device.
- Name the deadline in the text next to the countdown, because the digits alone do not say what happens at zero.
- Use `Compact` in table rows and banners, because the large digits make a dense list hard to scan.
- Pass a server-synced `TimeProvider` when the device clock cannot be trusted, such as a checkout that closes a hold.

**Avoid**

- Using a countdown for a deadline the person must act on in a few seconds, because the control is not announced as it
  changes. Raise an `Alert` or a `Toast` when the outcome matters.
- Several large countdowns on one screen, because each one moves every second and draws the eye away from the page.
- Reading the time zone from the device for a target, because a visitor in another zone then counts to a different
  moment. Use `ResolveTarget` with the zone the event belongs to.
- Restarting a target countdown to mean a new deadline. `Restart` re-reads the same target; set a new `Target` instead.

### Common questions

**What does the countdown show when the target has already passed?** It shows the completed state at once, with
`CompleteContent`, and it does not raise `Completed`. Check `IsCompleted` in your view model if the page should show a
different layout for a closed offer.

**How do I count down a fixed length, such as a 15-minute hold?** Set `Duration` to `00:15:00` and `Units` to
`Minutes, Seconds`. The countdown starts when it is shown. Bind `IsPaused` to a view model property so a checkout step
can hold the timer while the visitor reads a form.

**Why does the countdown not update while the window is hidden?** The clock stops ticking when the control is off screen
or not in the visual tree. A duration keeps its remaining time while it is away, and a target keeps counting to the same
instant, so the display is correct when it returns.

**How do I react when the countdown ends?** Handle the routed `Completed` event, or subscribe in code with
`timer.Completed += (_, _) => offers.Refresh();`. The event fires once, while the control is shown and reaches zero.

## API reference

<!-- api: Countdown -->

### Companion types

`CountdownUnits` is a flags enum. The members are listed here because `Units` takes a combination of them.

| Member | Meaning |
|---|---|
| `Days` | Days; absorbs hours and below when it is the largest unit shown |
| `Hours` | Hours; absorbs minutes and seconds when it is the largest unit shown |
| `Minutes` | Minutes |
| `Seconds` | Seconds |
| `All` | Days, hours, minutes and seconds |
| `None` | No units selected; treated as `All` |

`CountdownVariant` has two members: `Large` (display digits with unit names, the default) and `Compact` (one pill for
banners and table cells).

## Accessibility

- `Label` names the countdown. The default is "Time remaining".
- The control has no live region. Spoken text gives the time left without seconds when more than a minute remains,
  and reads `CompleteContent` once the countdown is over. A non-text `CompleteContent` is spoken as "Complete".
- The visible digits are not announced as they change, so a screen reader does not repeat every second.
- The control type is Text. The visible digits are in the raw view, and the spoken text is the value that assistive
  technology reads.
- Use a text line next to the countdown that names the deadline in full, for example "Closes 2026-10-29 at 09:00 UTC".
  A countdown alone does not say what it counts to.

### Keyboard

`Countdown` is not focusable and has no keyboard interaction. Put the controls that act on it, such as a Pause button
in the same group, in the tab order and bind them to `IsPaused`.

## Motion

- Each digit is a wheel that turns the short way round, with soft edges at the top and bottom of the window.
- The digits roll in one after another the first time they are shown. After that they change once per second.
- Units appear and disappear with the container springing to its new size.
- At zero the digits fade and scale into the completion label, and the live dot pulses.
- Reduced motion makes the wheels jump to the new value, sizes the container at once, switches content with a
  fade and stops the pulse.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A digit changes each second | The wheel turns the short way; blur grows with the distance it travels | The digit jumps to the new value | Immediate |
| The first values appear | Digits roll in from zero, staggered 35 ms apart, up to 0.3 s | The digits show at once | Immediate |
| A unit group appears or leaves | Width springs with a morph, height with a smooth spring | The container takes its new size at once | Immediate |
| Reaching zero | Digits fade and scale to 0.92 into the completion label | The content switches with a fade | Immediate |
| The live dot | Pulses on a two-second cycle | Stops | Stops |

### Using motion with restraint

The digits move once a second, which is enough to show that the time is running. Keep the rest of the page still, and
do not stack a second moving element beside a countdown, such as a shimmering banner or a looping animation. Use one
countdown per screen region. Several countdowns in a long table each move every second, so keep a table to compact
pills and a few rows.

## Related

- `AnnouncementBar` for a site banner with a countdown and an action button.
- `Progress` when the work has a known amount of progress rather than a deadline.
- `UsageMeter` for consumption against a fixed quota, such as storage or seats.
- `Stepper` when the countdown is one part of a multi-step flow.
- `Alert` or `Toast` for a message that must be announced when a deadline passes.

| Component | Choose it when |
|---|---|
| `AnnouncementBar` | A site-wide strip needs a countdown, a message and a button |
| `Progress` | The share of finished work is known |
| `UsageMeter` | A quota or limit is measured against current use |
| `Stepper` | The countdown belongs to one step of a flow |
| `Alert` / `Toast` | A deadline outcome must be announced to the visitor |
