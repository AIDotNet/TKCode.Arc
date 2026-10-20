---
title: Popover
summary: A small anchored surface for contextual information.
---

## When to use

- Interactive content that should not block the page, such as share settings or a small filter form.
- Content opened by a click on a button or another control.
- Content anchored to any control from code with `ShowAt`.

## When not to use

- A short text label on hover or focus: use `Tooltip`.
- A read-only preview of a person or a link: use `HoverCard`.
- A decision that must be answered before the user continues: use `Dialog`.
- A list of commands: use `DropdownMenu`.

## Usage

A `Popover` is anchored to a trigger and is non-modal by default, so the rest of the page stays usable.

### Trigger and content

Set `Trigger` to the control that opens the panel. The content of the popover is the panel.

```xml
<arc:Popover>
    <arc:Popover.Trigger>
        <arc:Button Variant="Secondary" Content="Share" />
    </arc:Popover.Trigger>
    <StackPanel Spacing="8">
        <TextBlock Text="Share link" FontWeight="Medium" />
        <TextBlock Text="Anyone with the link can view." />
    </StackPanel>
</arc:Popover>
```

### Attached to a control

`arc:Popover.Attach` adds a popover to any control. A click on that control toggles the panel.

```xml
<arc:Button Content="Filters">
    <arc:Popover.Attach>
        <arc:Popover Placement="Top">
            <TextBlock Text="Opens on the top side." />
        </arc:Popover>
    </arc:Popover.Attach>
</arc:Button>
```

### Placement and alignment

`Placement` is the preferred side: `Top`, `Bottom`, `Left` or `Right`. The panel flips when it does not fit.
`Alignment` places it along the trigger edge: `Start`, `Center` or `End`. `Offset` is the gap to the trigger in
pixels; the default is 6.

```xml
<arc:Popover Placement="Bottom" Alignment="Center" Offset="8">
    <arc:Popover.Trigger>
        <arc:Button Variant="Ghost" Content="Filter" />
    </arc:Popover.Trigger>
    <TextBlock Text="Status: any" />
</arc:Popover>
```

### Opening from code

`ShowAt` anchors the popover to any control. `IsOpen` is two-way and `Opened` and `Closed` report the state changes.

```csharp
var popover = new Popover
{
    Content = "Opened from code with ShowAt(anchor).",
    Alignment = PopoverAlignment.Center
};
popover.ShowAt(anchor);
```

### Modal popover

`IsModal` set to true swallows presses outside the panel, so they only close it. Focus is trapped inside.

```xml
<arc:Popover IsOpen="{Binding ShowPicker}" IsModal="True">
    <TextBlock Text="Pick one option first." />
</arc:Popover>
```

## API reference

<!-- api: Popover -->

## Accessibility

- Enter and Space on the trigger open the popover. Escape closes it and returns focus to the trigger.
- Focus moves into the panel when it opens. A press outside closes the popover and returns focus to the trigger.
- Give the popover an `AutomationProperties.Name` when its content has no heading. That name labels the panel.
- Non-modal popovers do not trap focus. `IsModal` set to true traps focus inside the panel.
- The trigger exposes whether the popover is expanded.

## Motion

- The panel starts 5px toward its trigger at a scale of 0.97, fades in quickly and settles on a spring from the side it
  landed on.
- Closing takes about 160ms at half the travel. An open and a close reverse each other in the middle of the motion.
- The trigger changes color when pressed but does not scale, because the panel is measured against it.
- Reduced motion removes the movement and scaling and keeps only a short fade. Off shows or removes the panel at once.

## Related

- `Tooltip` for a one-line label on hover or focus.
- `HoverCard` for a read-only preview that opens on hover.
- `Dialog` for content that must be dealt with before the page can be used.
- `DropdownMenu` for a list of commands.
