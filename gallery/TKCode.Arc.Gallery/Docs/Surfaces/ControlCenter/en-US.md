---
title: Control center
summary: A quick settings panel whose tiles grow into their details, with a focus-session dial and volume and text-size meters that stretch at their ends.
---

<Demo name="Preview" />

## When to use

`ControlCenter` is a compact panel of everyday switches for a workspace: a wide Focus tile, Notifications and
Presenting tiles, and two segmented meters for the alert volume and the text size. Each tile toggles with one press and
says what it means right now ("Until 12:13 PM", "2 of 3 channels", "Off"). Tiles with a chevron grow into a detail
that fills the panel, where the finer settings live, and shrink back into place when you are done. It is 360 by 396
pixels and fits a popover, a side panel or the corner of a settings page.

- **A quick settings popover** behind a status-bar or avatar button: the handful of switches people reach for many
  times a day, each one press away.
- **Focus sessions**: pick a mode, set a length on the dial (one turn is two hours, in 5-minute steps) and start. The
  tile then shows the mode and when the session ends.
- **Notification control in two depths**: the tile pauses everything at once; the detail has a switch per channel and
  shows who is always let through.
- **Levels that should feel physical**: the meters follow a grab one to one, jump to a tap, swell while held and stretch
  slightly at their ends instead of stopping dead.

## When not to use

- **A full settings page**: many groups, search and descriptions need a page of `Switch`, `Slider` and form rows, not a
  fixed-size panel.
- **A single on/off option**: use `Switch` where the option is explained.
- **Precise numeric input**: a value that has to be typed or read to the decimal needs a `Slider` with a field, or a
  number input.
- **More than two drill-down areas or deeper navigation**: the panel has two details and one level; for stacked panels
  use a sheet or page navigation.
- **Scheduling**: sessions here start now and run for up to two hours. A calendar or time picker suits anything later or
  longer.

## Usage

Give the panel its focus modes, channels and (optionally) people. The first mode is what a tap on the Focus tile starts,
at that mode's default length.

```xml
<arc:ControlCenter Volume="{Binding AlertVolume}"
                   TextSize="{Binding TextSize}"
                   AreNotificationsOn="{Binding NotificationsOn}"
                   IsPresenting="{Binding Presenting}"
                   FocusSession="{Binding Session}" />
```

```csharp
center.FocusModes.Add(new ControlCenterFocusMode
{
    Id = "drafting", Label = "Drafting", Icon = ControlCenterSamples.HeadphonesIcon,
    Description = "Long stretches for writing and sketching", DefaultMinutes = 50
});
center.Channels.Add(new ControlCenterChannel { Id = "comments", Label = "Comments", IsOn = true });
center.People.Add(new ControlCenterPerson { Id = "mara", Name = "Mara Lind" });
```

`ControlCenter` derives from Avalonia's `TemplatedControl`. It lives in the Special package: register it with
`UseArcSpecial()` next to `UseArc()`. `ControlCenterSamples.Apply(center)` fills a panel with the invented content of the
preview.

Every value the user can change is a two-way property, and `StateChanged` reports the whole state after every change
with the reason. A dragged meter reports once per whole step, not once per pixel. The text size only scales the "Aa"
sample in the panel: apply it to your app yourself.

<Demo name="Events" />

### Anatomy

1. **Panel**: 360 wide (or the width of its container, if narrower) and always 396 high; raised surface, 1px border,
   radius 30, floating shadow, 10px padding.
2. **Focus tile**: the full width, 84 high. Icon circle, title and state line; the chevron at the right opens the Focus
   detail.
3. **Notifications and Presenting tiles**: half width, 104 high, icon on top and text at the bottom. Notifications has a
   chevron in its corner.
4. **Meters**: a label row (icon, label, value, and the "Aa" sample for the text size) over a 30px track of 28 segments.
5. **Detail**: inset 8 from the panel's border with radius 22, over a scrim of the raised surface at 55%. Its header has
   the icon, the title and Close.

An "on" tile mixes 9% of the accent into its surface and fills its icon circle with the accent. Tiles have no hover
change; only the chevrons and Close react to the pointer.

### Every state

<Demo name="States" />

| Tile | Off | On |
|---|---|---|
| Focus | "Focus", "Off", headphones icon | The mode's name, "Until {time}", the mode's icon |
| Notifications | "Paused", bell with a slash | "{on} of {all} channels", bell (also with 0 channels on) |
| Presenting | "Off" | "Popups held back" (`Labels.PresentingOn`) |

When a state changes, the icon and the state line trade places in a short blur: the old one fades, blurs and moves up
8px while the new one rises from 8px below. The Focus title changes at once.

### The Focus detail

- **Mode switch**: one segment per mode, with a raised pill that glides to the selected one. Picking a mode sets the dial
  to the mode's default length and rolls in its description.
- **Dial**: one full turn is 120 minutes. Grab it anywhere and turn: it moves by the angle you turn, not to where you
  pressed, so a tap changes nothing. It keeps counting across 12 o'clock, resists past 5 and 120 minutes (the arc and the
  number hold at the limit) and snaps to the nearest 5 minutes when released. Under the number it shows when a session
  started now would end.
- **Buttons**: without a session, Start session; with one, End session and Update session.

The detail edits a draft. Start and Update apply it (the session then ends at now plus its minutes); Close, Escape or a
press on the scrim discard it, and opening again shows the running session's mode and length. Sessions do not end on
their own: when the time is up, end the session from your code with `EndFocus()` if you need to.

### The Notifications detail

The master switch at the top is the same setting as the tile. While it is off, the channel switches dim but keep working,
and the tile still remembers each channel. Channel rows come from `Channels`; a long list scrolls inside the detail. With
`People`, the foot of the detail shows their avatars (pictures, or initials on a generated gradient) and a sentence
naming them.

### Your own labels and content

Everything the panel says comes from `ControlCenterLabels`: tile titles, states, button text, accessible names, the time
format and the sentences with numbers (delegates). This demo is written entirely in XAML, with one focus mode, five
channels and no people.

<Demo name="Custom" />

### Narrow containers

The panel takes the width of a narrower container. At a container of 312 or less, or a window of 360 or less, it
tightens its spacing (6px padding and gaps, smaller tile padding) and uses a 150px dial. In a window 455 wide or less it
also drops its border, radius, shadow and fill so it sits flush in the page, while the open detail keeps its own surface.

<Demo name="Narrow" />

### Reduced motion

<Demo name="ReducedMotion" />

### Content guidelines

- Keep mode names to one or two words ("Drafting", "Reviewing"); they share the width of the mode switch.
- Write mode descriptions as one short sentence about what still gets through. Long text is cut at the panel's width.
- Name channels after what they deliver ("Comments", "Shared folders"), and use the second line for when.
- The Presenting state line should say what presenting does, in two or three words.
- Pass people with their full names; the sentence uses first names and the avatars use initials.

### Best practices

- Persist the state from `StateChanged` (or the bound properties) and restore it by setting the same properties.
- Give the dial's modes sensible defaults: the most used length should be one tap away.
- Use `TimeProvider` to give the panel your app's clock (and to test session end times).
- Format times for your users with `Labels.FormatTime`; the default is the short US time ("12:13 PM").
- Open a detail from code with `OpenDetail` (for example from a deep link) rather than simulating clicks.

### Common questions

**Why does the session not end by itself?** The panel shows when a session ends but does not act on it, so a host can
extend, notify or end it as it sees fit. Call `EndFocus()` or set `FocusSession` to null.

**Can I add a third detail or more tiles?** No: the layout is fixed. Use several panels, or build your own from `Switch`
and the gallery's other controls.

**Why is a tap on the dial ignored?** The dial moves by the angle you turn, so a stray tap never changes the length. The
meters, by contrast, jump to a tap when you release.

## API reference

<!-- api: ControlCenter -->

<!-- api: ControlCenterFocusMode -->

<!-- api: ControlCenterChannel -->

<!-- api: ControlCenterPerson -->

<!-- api: ControlCenterFocusSession -->

<!-- api: ControlCenterState -->

<!-- api: ControlCenterLabels -->

<!-- api: ControlCenterStateChangedEventArgs -->

<!-- api: ControlCenterDetailEventArgs -->

<!-- api: ControlCenterSamples -->

The enums:

| Type | Values |
|---|---|
| `ControlCenterDetail` | `None`, `Focus`, `Notifications` |
| `ControlCenterChangeReason` | `FocusStarted`, `FocusUpdated`, `FocusEnded`, `NotificationsToggled`, `ChannelToggled`, `PresentingToggled`, `VolumeChanged`, `TextSizeChanged` |

## Accessibility

- The panel is a group named by `Labels.PanelName`. Each tile is a toggle button whose name reads its title and state
  ("Notifications, 2 of 3 channels"); the chevrons are named "Focus session settings" and "Notification channels".
- An open detail is named by its title. The tiles and meters behind it leave the tab order, hit testing and the
  automation tree, and focus stays inside the detail until it closes.
- The mode switch is a radio group named "Mode". The dial is a slider named "Session length" from 5 to 120 whose help
  text reads the minutes and the end time ("50 minutes, until 12:13 PM"). The meters are sliders named by their labels,
  with the value as "64%".
- Switches are named by their row labels. Avatars are decorative: the names are in the sentence next to them.
- Keyboard focus shows a 2px ring 2px outside the tile, button, segment, dial or track; pointer focus draws none.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Focus tile, its chevron, Notifications tile, its chevron, Presenting tile, volume, text size. Inside a detail: Close, the selected mode, the dial, the buttons, then back to Close |
| `Enter` / `Space` on a tile | Toggle it (the Focus tile starts the first mode or ends the session) |
| `Enter` / `Space` on a chevron | Open its detail; focus moves to Close |
| `←` `→` `↑` `↓` / `Home` / `End` in the mode switch | Select the previous, next, first or last mode |
| `←` `↓` / `→` `↑` on the dial | 5 minutes shorter or longer |
| `Page Down` / `Page Up` on the dial | 15 minutes shorter or longer |
| `Home` / `End` on the dial | 5 or 120 minutes |
| `←` `↓` / `→` `↑` on a meter | One step (1% volume, 5% text size) |
| `Page Down` / `Page Up` on a meter | Four steps |
| `Home` / `End` on a meter | Minimum or maximum |
| `Escape` | Close the detail; focus returns to its chevron |

Each tile is one tab stop, and the mode switch is a single tab stop with arrow keys, as radio groups should be.

## Motion

- Opening a detail grows the tile's box into the detail's on a spring without bounce (about 0.75 s to settle). The radius
  stays 22 the whole way and the content is laid out at its final size, so the growing box reveals it rather than
  stretching it; the icon and title glide from the tile into the header. The scrim fades in over 230ms and the content
  over 240ms after 60ms.
- Closing runs the same spring back. The content fades out in 110ms; the tile fades back in over the first half of the way
  and the detail fades out between halfway and 95%.
- Pressing a tile shrinks it to 0.975 on the same spring. Tile colors change over 240ms.
- The mode pill glides on the same spring; the dial springs quickly to a new mode, a key or a snap, and more slowly back
  from beyond its ends. A meter's level springs to a tap or a key with its number counting along; the track swells to
  1.18 times its height while held and springs back from a stretch.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Detail open and close | Morph, crossfade, scrim and content fades | Appears in place; scrim and content fade | Instant |
| Icon and state swaps | Blur, travel (and scale for icons) | Fade and blur, no travel | Instant |
| Dial and meter values | Springs, numbers count | Jump | Jump |
| Meter swell | 1.18 while held | None | None |
| Tile press | Scale 0.975 | None | None |

## Responsive behavior

- The panel is as wide as its slot up to 360 px and always 396 px high; tiles reflow inside that width.
- When the viewport (the nearest viewport host, otherwise the window) is 360 px or narrower, or the panel itself is 312 px
  or narrower, spacing tightens and the focus dial shrinks to 150 px.
- When the viewport is 455 px or narrower, the panel drops its border, corners, shadow and fill so it sits flush on a
  phone screen; an open detail keeps its own surface so it stays readable.
- On touch, tiles, the dial and the meters work with taps and drags; every value can also be changed with the keyboard
  (arrow keys on the meters and the dial), so nothing depends on hover.

## Related

- `Switch` for a single on/off option with its label.
- `Slider` for a precise value with ticks or a field.
- `SegmentedControl` for picking one of a few options in a page.
- `Popover` to show the panel from a button.

| Component | Choose it when |
|---|---|
| `ControlCenter` | A few everyday settings should be one press away, with depth on demand |
| `Switch` | One option, explained in place |
| `Slider` | A precise numeric value |
| `SegmentedControl` | One choice among a few, in a page or toolbar |
