---
title: Toggle group
summary: A row of toggle buttons for one choice or for several independent options.
---

<Demo name="TextStyle" />

## When to use

A toggle group keeps a few closely related on and off controls together in one flat row. Pressed options sink into a
tinted fill, so they read as tools on the surface and not as a switch between pages.

- **Text formatting in an editor toolbar**: bold, italic and underline each toggle on their own, and `Mode="Multiple"`
  joins neighbours that are both on into one shape.
- **A display option such as alignment or layout**: one option at a time, with the fill gliding to the new choice.
- **Quick filters above a list**: several can apply at once, and each stays one short word or one icon.
- **Dense tool panels**: `Size="Small"` gives 28 px options that still line up with small controls.

## When not to use

- **Switching between views of the content**: use `SegmentedControl`, whose raised pill on a sunken track says "this
  view".
- **A single setting that is on or off**: use `Switch`, which applies at once and needs no group.
- **A choice that needs an explanation for each option**: use `RadioGroup` or `RadioCards`, which have room for
  descriptions.
- **Many options or filters from a data set**: use `ChipGroup`, which wraps onto several lines.

## Usage

Write the options inside the group, or bind `ItemsSource` to a list of `ToggleGroupOption`. In the default single mode
`SelectedValue` holds the pressed option's `Value`; pressing it again keeps it, like a radio group.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ToggleGroup Label="Alignment" SelectedValue="{Binding Alignment}">
        <arc:ToggleGroupOption Value="start" Label="Start" />
        <arc:ToggleGroupOption Value="center" Label="Center" />
        <arc:ToggleGroupOption Value="end" Label="End" />
    </arc:ToggleGroup>
</UserControl>
```

`ValueChanged` fires when a person toggles an option, not when your code sets a value. Its arguments carry the toggled
`Value`, whether it `IsPressed` now and all `Values`.

### Icons and text

`Icon` takes a stroked 24 by 24 glyph such as `ArcIcons.List` and draws it at the right size before the content. The
content of the option is its visible text. An option with only an icon must have a `Label`: it becomes the accessible
name and the tooltip, and `Title` replaces the tooltip when it should say something else. An option with no icon and no
content shows its `Label` as text, with no tooltip.

<Demo name="Layout" />

### Several options at once

`Mode="Multiple"` lets each option toggle on its own. `SelectedValues` lists the pressed values in the order they were
pressed; in XAML it is a comma-separated list. A value with no option yet is kept, and it applies when the option
arrives.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ToggleGroup Label="Text style" Mode="Multiple" SelectedValues="bold,italic">
        <arc:ToggleGroupOption Value="bold" Label="Bold" />
        <arc:ToggleGroupOption Value="italic" Label="Italic" />
        <arc:ToggleGroupOption Value="underline" Label="Underline" />
    </arc:ToggleGroup>
</UserControl>
```

### Sizes

`Size` sets the option height: `Small` is 28 px with 14 px icons, and `Medium`, the default, is 36 px with 16 px icons.
`Large` looks like `Medium`.

<Demo name="Sizes" />

### Disabled options

`IsDisabled` on an option removes it from the keyboard and from the pointer while it keeps its place in the row.
`IsEnabled="False"` on the group disables every option and fades the group.

<Demo name="Disabled" />

## API reference

<!-- api: ToggleGroup -->
<!-- api: ToggleGroupOption -->

## Accessibility

| Key | Action |
|---|---|
| Right, Down | Moves focus to the next option, and wraps |
| Left, Up | Moves focus to the previous option, and wraps |
| Home, End | Moves focus to the first or last enabled option |
| Space, Enter | Toggles the focused option |

The arrow keys only move focus, so a person can reach an option without changing anything. The group is one tab stop:
the last focused option, else the first pressed one, else the first enabled one. `Label` names the group. In single mode
the options report as radio buttons; in multiple mode they report as buttons with a pressed state.

## Motion

In single mode one fill glides between options on the morph spring. In multiple mode a fill fades in over 160 ms while it
settles from 86% scale over 360 ms, and a join crosses the 2 px gap and squares its corners over 240 ms. The content of a
pressed option shrinks to 92% in 90 ms and returns in 320 ms; the option's box never scales, so neighbours stay put. With
reduced motion the fill jumps or fades in 120 ms, nothing scales and colors change in 120 ms.

## Responsive behavior

The options keep their size and never wrap or shrink, so keep the row short enough for the narrowest layout, or move
longer sets to `ChipGroup`. Hover colors appear only for pointers that can hover, not for touch. Use short labels or
icons on a phone, where the medium size gives a 36 px target.

## Related

| Component | Choose it when |
|---|---|
| `SegmentedControl` | The choice switches the view beside it |
| `Switch` | One setting is on or off |
| `RadioGroup` | Each option needs a label and a description |
| `ChipGroup` | Many filters wrap across several lines |
