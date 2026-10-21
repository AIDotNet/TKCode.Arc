---
title: Slider
summary: Pick a value or a range on a track that follows your finger.
---

<Demo name="SingleValue" />

## When to use

- Approximate values where position matters more than the exact number, such as volume, opacity or a price range.
- A range with two thumbs, labeled ticks and a formatted readout.
- Values that change while the user watches the track, such as playback position or brightness.

## When not to use

- Exact numbers that the user types: use `NumberField`.
- A value that the user cannot change: use `Progress`.
- A single setting that the user drags for feel, with the track stretching past its ends: use `ElasticSlider`.

## Usage

Basic usage is a single value. `Value` is the position on the track, and `Minimum` and `Maximum` set its range.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Slider Label="Volume"
                Value="64"
                Width="360" />
</UserControl>
```

### Start and end content

`StartContent` and `EndContent` sit before and after the track, such as icons or a mute button.

```xml
<arc:Slider Label="Volume"
            Value="64"
            Width="360">
    <arc:Slider.StartContent>
        <TextBlock Text="-" />
    </arc:Slider.StartContent>
    <arc:Slider.EndContent>
        <TextBlock Text="+" />
    </arc:Slider.EndContent>
</arc:Slider>
```

### Range

Set `IsRange` to show two thumbs. Read and write the ends with `LowerValue` and `UpperValue`, and keep the thumbs
apart with `MinStepsBetweenThumbs`. `ValueFormatter` formats the readout, the bubble and the spoken value.

<Demo name="Range" />

```xml
<arc:Slider Label="Price"
            IsRange="True"
            Minimum="0"
            Maximum="1000"
            SmallChange="10"
            LowerValue="{Binding MinPrice, Mode=TwoWay}"
            UpperValue="{Binding MaxPrice, Mode=TwoWay}"
            MinStepsBetweenThumbs="5"
            LowerThumbName="Minimum price"
            UpperThumbName="Maximum price"
            ValueFormatter="{Binding PriceFormatter}" />
```

### Steps and marks

`SmallChange` is the step the value snaps to. `Marks` adds ticks on the track. A mark with a label prints the label
under the track, and clicking the label moves the nearest thumb there.

<Demo name="StepsAndMarks" />

### Events

`ValueCommitted` runs once per release, per key press that changes the value, and per clicked mark label. Put slow
work, such as a network request, on this event. `RangeChanged` runs on every step of a range while it is dragged or
changed by keys.

```csharp
price.ValueCommitted += (_, e) => SavePriceFilter(e.LowerValue, e.UpperValue);
price.RangeChanged += (_, e) => committed.Text = $"${e.LowerValue:N0} – ${e.UpperValue:N0}";
```

### Disabled

A disabled slider leaves the tab order.

<Demo name="Disabled" />

## API reference

<!-- api: Slider -->

<!-- api: SliderMark -->

## Accessibility

- `Label` names the slider. In a range, each thumb is named from `LowerThumbName` and `UpperThumbName`, or from
  `Label` with "minimum" and "maximum".
- Each thumb reports its value as text, so the spoken value follows the `ValueFormatter`.
- Arrow keys step by `SmallChange`, Shift with an arrow or PageUp and PageDown take a large step, and Home and End go to
  the lowest or highest allowed value.
- The readout, the bubble and the mark labels are hidden from assistive technology. Clicking a mark is a pointer
  shortcut; the same value is reachable by keyboard.
- The focus ring appears after a key press, not after a pointer press.

## Motion

- The thumb follows the pointer one to one. On release it settles on the nearest step and keeps the momentum of the
  drag, so a fast flick can move it one step further.
- Pressing the track springs the nearest thumb to the pointer. A key at a limit makes the thumb strain toward the press
  and spring back.
- The value bubble pops up above the active thumb, rolls its digits while the value changes and stays for 700 ms after
  a release. The dragged thumb grows slightly.
- Reduced motion removes momentum, stretch and digit rolling. The thumb goes straight to the value and the bubble only
  fades. Off motion jumps straight to the end state.

## Related

- `ElasticSlider` for a single setting that stretches past its ends and clicks into detents.
- `NumberField` when the user needs to type an exact number.
- `Progress` for a value that is shown but not changed by the user.
