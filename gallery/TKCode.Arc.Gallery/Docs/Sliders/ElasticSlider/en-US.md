---
title: Elastic slider
summary: A slider that stretches like rubber past its ends, with a swaying value tag and detents the thumb clicks into.
---

## When to use

- A single setting that the user adjusts by feel, such as volume, brightness or opacity.
- Media and creative tools where the track should feel physical.
- Detents at useful values, such as 0, 50 and 100, that the thumb clicks into.

## When not to use

- A quiet form or settings page: use `Slider`.
- A range with two thumbs: use `Slider`, which has `IsRange`.
- An exact number that the user types: use `NumberField`.

## Usage

Basic usage is a single value. `Value` is the position on the track, and `Minimum` and `Maximum` set its range.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ElasticSlider Label="Volume"
                       Value="40"
                       Width="360"
                       ValueFormatter="{Binding VolumeFormatter}" />
</UserControl>
```

### Icons

`StartIcon` and `EndIcon` sit before and after the track. When the track is pulled past an end, the icon on that end
moves outward with it.

```xml
<arc:ElasticSlider Label="Brightness"
                   Value="50"
                   Width="360">
    <arc:ElasticSlider.StartIcon>
        <TextBlock Text="-" />
    </arc:ElasticSlider.StartIcon>
    <arc:ElasticSlider.EndIcon>
        <TextBlock Text="+" />
    </arc:ElasticSlider.EndIcon>
</arc:ElasticSlider>
```

### Detents

Add `SliderMark` values to `Marks`. A dragged thumb clicks into each mark. A mark with a label also prints the label
under the track, and clicking the label moves the thumb there.

```csharp
var brightness = new ElasticSlider
{
    Label = "Brightness",
    Value = 50,
    Width = 360
};
brightness.Marks.Add(new SliderMark(0, "Off"));
brightness.Marks.Add(new SliderMark(50, "50"));
brightness.Marks.Add(new SliderMark(100, "Max"));
```

### Steps and format

`SmallChange` is the step the value snaps to. `ValueFormatter` formats the value tag, the readout and the spoken
value. `IsValueVisible` hides the readout beside the label. The tag still appears while the thumb is dragged or changed
by keys.

```xml
<arc:ElasticSlider Label="Zoom"
                   Minimum="0.5"
                   Maximum="3"
                   SmallChange="0.1"
                   Value="1"
                   Width="360"
                   ValueFormatter="{Binding ZoomFormatter}" />
```

### Events

`ValueCommitted` runs once per release, per key press that changes the value, and per clicked mark label. Put slow
work, such as a network request, on this event.

```csharp
volume.ValueCommitted += (_, e) => SaveVolume(e.Value);
```

## API reference

<!-- api: ElasticSlider -->

<!-- api: SliderMark -->

## Accessibility

- `Label` names the thumb. The spoken value follows the `ValueFormatter`.
- Arrow keys step by `SmallChange`, Shift with an arrow or PageUp and PageDown take a large step, and Home and End go to
  the lowest or highest value.
- At a limit, a key press makes the track bounce, and the value does not change.
- The tag and the detent pops are visual only. Clicking a mark label is a pointer shortcut; the same value is reachable
  by keyboard.
- The focus ring appears after a key press, not after a pointer press. A disabled slider leaves the tab order.

## Motion

- Past either end, the track stretches like rubber and springs back with the release speed. Held, the thumb swells
  slightly.
- The value tag hangs off the thumb and sways with its speed, so a fast drag tilts it more.
- When the thumb clicks into a detent, the tick on the track pops once.
- Reduced motion removes the stretch, the sway and the detent pop. The thumb goes straight to the value and the tag only
  fades. Off motion jumps straight to the end state.

## Related

- `Slider` for a quiet form control, or for a range with two thumbs.
- `NumberField` when the user needs to type an exact number.
- `Progress` for a value that is shown but not changed by the user.
