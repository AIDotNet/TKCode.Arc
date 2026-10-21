---
title: Hold to confirm
summary: Confirm a destructive action by holding, not tapping.
---

<Demo name="Default" />

## When to use

- A destructive or hard to undo action whose consequence is already visible on the screen, such as deleting a project.
- A compact settings row where a confirmation dialog would be too heavy.
- A place where a stray tap on a touch device must not run the action.

## When not to use

- A small action that is safe or easy to undo: use `Button` or `ActionButton`.
- An action that needs the user to read the consequences first: use `Dialog`.
- Deleting an item in a touch list: use `SwipeActions`, which reveals the action on a swipe.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:HoldToConfirm Label="Hold to delete project"
                       ConfirmedLabel="Deleted"
                       Command="{Binding DeleteProject}" />
</UserControl>
```

The `Command` runs once, after the hold completes. A normal click or an automation invoke does not confirm.

### Tone and hold duration

`Tone` sets the fill color: `Danger` for irreversible actions, `Accent` (the default) and `Neutral` for the rest.
`HoldDuration` sets how long the button must be held; the default is 1.2 seconds.

<Demo name="Tones" />

### Icon

`Icon` is a 24 by 24 Lucide geometry shown before the label. When the hold completes, the icon becomes a drawn check.

```csharp
var archive = new HoldToConfirm
{
    Label = "Hold to archive",
    Tone = HoldToConfirmTone.Neutral,
    Icon = ArcIcons.Archive,
    ConfirmedLabel = "Archived"
};
```

### Hint while holding

`HoldingChanged` is raised when a hold starts and when it ends. `IsHolding` tells you the current state. Use them
for a hint next to the button.

```csharp
project.HoldingChanged += (_, e) =>
{
    hint.Text = e.IsHolding
        ? "Keep holding…"
        : project.IsConfirmed ? "Deleted." : "Let go early and the fill rewinds.";
};
```

Letting go early rewinds the fill on a spring and nothing is confirmed.

### Done state

`IsConfirmed` is two-way. It becomes `true` when the hold completes. Set it back to `false` to reset the button;
the fill rewinds as the label morphs back.

```xml
<arc:HoldToConfirm Label="Hold to delete project"
                   IsConfirmed="{Binding IsDeleted}" />
```

```csharp
reset.Click += (_, _) => project.IsConfirmed = false;
```

### Disabled

A disabled button cannot be held.

<Demo name="Disabled" />

## API reference

<!-- api: HoldToConfirm -->

## Accessibility

- `Label` names the button. After the hold completes, the name follows `ConfirmedLabel`.
- The help text says how to confirm: press and hold for the duration, or hold Space or Enter with a keyboard.
- The completed state is announced once through a polite status region, and the button reports that it is no longer
  available but keeps focus.
- Holding with Space or Enter uses the keyboard; key auto-repeat does not restart the hold. Moving focus away cancels
  the hold, and so does moving the pointer more than 24 px from the button.

## Motion

- While held, the fill moves across the button at a steady rate for the time that is left. Letting go early rewinds
  it on a smooth spring that keeps no overshoot.
- The label and icon change with a short blur-and-slide when the hold completes, and the icon draws a check.
- Reduced motion removes the press scale and the rewind spring. The label and icon fade, and the fill still follows
  the hold because it shows progress.
- Off motion keeps only the fill progress. Everything else changes at once.

## Related

- `Button` and `ActionButton` for actions that do not need a hold.
- `Dialog` when the user must read the consequences before confirming.
- `SwipeActions` for deleting a row in a touch list.
