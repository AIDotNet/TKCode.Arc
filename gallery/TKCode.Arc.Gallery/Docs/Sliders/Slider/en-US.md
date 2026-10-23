---
title: Slider
summary: Pick a value or a range on a track that follows your finger.
---

<Demo name="SingleValue" />

## When to use

`Slider` lets a person choose a value or a range by moving a thumb along a track. It fits settings and filters where
the position on the track carries the meaning and an exact digit matters less. It sits in a form, a filter panel or a
control bar, and it shows the chosen value in a readout beside the label and in a bubble above the thumb while the
person moves it.

- **Price and amount filters on a product listing**: a range with two thumbs, `IsRange="True"`, where the lower and
  upper values become the query. The track shows the spread of the catalog at a glance, and the readout shows
  "$200 to $800" while the thumbs move.
- **Spend limits and budgets** in a billing or procurement console: a monthly cap for a team, set between $0 and
  $5,000. The person sees roughly where the cap sits, and the formatted readout gives the exact figure.
- **Playback and display settings** in a media or training app: playback speed with labeled ticks at 0.5x and 1x,
  or the volume of a call. Labeled marks name the presets, and clicking a label jumps there.
- **Alert thresholds** in an operations dashboard: a CPU or error-rate threshold between 0 and 100 percent, with marks
  at the levels the team uses in its runbooks. The track makes the scale obvious where a number alone would not.
- **Opacity, density or zoom controls** in a data tool, where the value is judged by what changes on screen while the
  thumb moves.

## When not to use

- **Exact numbers the person types**, such as a refund amount or a quantity: use `NumberField`, or `MoneyInput` for an
  amount with a currency. A track only approximates a value.
- **A value the person cannot change**, such as storage used or an upload's progress: use `Progress`.
- **A single setting that should feel physical**, with the track stretching past its ends and detents the thumb clicks
  into: use `ElasticSlider`. It has one value and no range.
- **A choice from a short fixed list**, such as a plan tier or a delivery speed: use `Select` for a dropdown, or
  `SegmentedControl` when the options fit in one row.
- **A date or a time span**: use `DatePicker`, `DateRangePicker` or `TimePicker`. A slider cannot show calendar units.
- **An on or off setting**: use `Switch`. A slider implies that intermediate values are meaningful.

## Usage

Basic usage is a single value. `Value` is the position on the track, and `Minimum` and `Maximum` set its range.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Slider Label="Volume"
                Value="64"
                Width="360" />
</UserControl>
```

`Slider` derives from Avalonia's `RangeBase`, so `Value`, `Minimum`, `Maximum`, `LargeChange` and `ValueChanged` work as
they do on any range control. Two things differ from a native slider. `SmallChange` is the step the value snaps to, and
it defaults to 1 on this control. Setting `IsRange` switches the control to two thumbs, with `LowerValue` and
`UpperValue` in place of `Value`. The control adds the label, the readout, the value bubble, the marks and the content
slots.

### Anatomy

1. **Label** (`Label`): medium weight, on the row above the track with a 19.6 px line height. It truncates with an
   ellipsis when the width is short. It names the thumb for assistive technology.
2. **Readout**: the formatted value, right-aligned on the label row in the secondary text color, with 16 px of space
   before it. In range mode it shows the low and high values with an en dash between them in the muted color, and the
   two numbers are 4 px apart. `IsValueVisible` hides it.
3. **Start content** (`StartContent`): before the track with 12 px of space. It is hidden when the property is null.
4. **Track and fill**: a 6 px track in the control track color. The fill runs from the start to the thumb, or between
   the two thumbs in range mode, in the control fill color.
5. **Thumbs**: 22 px discs with the control thumb color and a raised shadow. Each thumb's hit area reaches 11 px beyond
   each side of the disc.
6. **Ticks**: 4 px dots, one per mark. Ticks outside the fill use the foreground at 30% opacity; ticks on the fill use
   the control glyph color at 60%.
7. **Value bubble**: a raised bubble 8 px above the active thumb (18 px on a touch or pen press), at least 28 px tall,
   with 12 px of horizontal padding and a 1 px border. Its colors are inverted: the foreground is the background and
   the background is the text. It appears while the thumb is dragged or has keyboard focus.
8. **End content** (`EndContent`): after the track with 12 px of space, in the same way as the start content.
9. **Mark labels row**: 20 px high, under the track in the extra small type size. A label is muted, and the label of a
   mark that the value has reached uses the secondary color. The row appears only when at least one mark has a `Label`.

### Start and end content

`StartContent` and `EndContent` sit before and after the track, such as icons or a mute button. A mute button suits a
volume control: the person can silence the output without moving the thumb. Keep the content to a single glyph or a
small icon button, because the track takes the remaining width.

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

Set `IsRange` to show two thumbs. Read and write the ends with `LowerValue` and `UpperValue`, and keep the thumbs apart
with `MinStepsBetweenThumbs`, which counts in steps. In range mode `Value` is not used. `ValueFormatter` formats the
readout, the bubble and the spoken value of each thumb.

Choose a range when the person is narrowing a set, such as a price, a date span in days or a score band. Use a single
thumb for a threshold, because a second thumb asks the person to choose two related numbers at once.

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

`MinStepsBetweenThumbs` keeps the two thumbs apart by a number of steps, so with `SmallChange="10"` and a value of `5`
the thumbs can never be closer than 50 units. A thumb pressed into the other one stops at the gap. Leave it at `0`, the
default, when the two ends may meet.

### Steps and marks

`SmallChange` is the step the value snaps to. `Marks` adds ticks on the track. A mark with a label prints the label under
the track, and clicking the label moves the nearest thumb there. A bare `SliderMark` (or a number, which converts
implicitly) is a tick with no label.

Use marks for presets the person returns to, such as 0.5x, 1x and 2x for playback, or the values a runbook names for an
alert. Label only the presets that need a name, and leave the others as ticks; a row of labels under a narrow track
collides quickly.

<Demo name="StepsAndMarks" />

Marks do not change the value by themselves. A drag, a key press or a clicked label sets the value, and the marks only
show where the presets are.

### Ranges and limits

The value always stays inside `Minimum` and `Maximum`, and a range keeps both ends inside them. The control clamps values
as it receives them, so a XAML page may set `Minimum="360"` before `Maximum="900"` without an error; the final values
are the ones to rely on.

Use `LargeChange` for the jump that `PageUp`, `PageDown` and `Shift` with an arrow make. When it is not set, the jump is
a tenth of the range, which is right for most scales. Set it explicitly for a scale where a tenth is too coarse, such as
a 0 to 5 rating where the jump should be 1.

### Events

`ValueCommitted` runs once per release, per key press that changes the value, and per clicked mark label. Put slow
work, such as a network request, on this event. Its `SliderValueCommittedEventArgs` carries `Value`, `LowerValue`,
`UpperValue` and `IsRange`. For a single value the lower and upper values repeat `Value`; in range mode `Value` is the
lower end. `RangeChanged` runs on every step of a range while it is dragged or changed by keys, and its
`SliderRangeChangedEventArgs` carries `LowerValue` and `UpperValue`.

```csharp
price.ValueCommitted += (_, e) => SavePriceFilter(e.LowerValue, e.UpperValue);
price.RangeChanged += (_, e) => committed.Text = $"${e.LowerValue:N0} – ${e.UpperValue:N0}";
```

### Binding to a view model

A filter panel is the typical case: the view model owns the two prices, the thumbs mirror them, and a command runs the
query when the person has finished. Use CommunityToolkit.Mvvm for the view model.

```csharp
public interface ICatalogQuery
{
    Task FilterByPriceAsync(double minPrice, double maxPrice);
}

public partial class PriceFilterViewModel : ObservableObject
{
    private readonly ICatalogQuery _catalog;

    [ObservableProperty]
    private double _minPrice = 200;

    [ObservableProperty]
    private double _maxPrice = 800;

    [ObservableProperty]
    private string _summary = "Showing products from $200 to $800.";

    public PriceFilterViewModel(ICatalogQuery catalog) => _catalog = catalog;

    [RelayCommand]
    private async Task ApplyAsync()
    {
        Summary = $"Showing products from ${MinPrice:N0} to ${MaxPrice:N0}.";
        await _catalog.FilterByPriceAsync(MinPrice, MaxPrice);
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:Slider Label="Price"
                IsRange="True"
                Minimum="0"
                Maximum="1000"
                SmallChange="10"
                LowerValue="{Binding MinPrice, Mode=TwoWay}"
                UpperValue="{Binding MaxPrice, Mode=TwoWay}"
                MinStepsBetweenThumbs="5"
                Width="360" />
    <TextBlock Text="{Binding Summary}" />
</StackPanel>
```

- `LowerValue` and `UpperValue` are the two-way properties to bind in range mode; `Value` is the two-way property in
  single mode. Bind the one that matches `IsRange`.
- Bind `Mode=TwoWay` explicitly. The view model's `MinPrice` changes on every step of a drag, which is right for a
  readout or a summary line and wrong for a query. Run the query from `ApplyAsync`, or from `ValueCommitted` in the
  view's code behind, so a drag sends one request.
- The command in the view is a `Button` bound to `ApplyCommand`; it stays enabled while the thumbs move, so the person
  can apply the new range at any point.

### Using motion with restraint

Motion on the slider tells the person what the thumb did: the bubble confirms the value, the digits roll to show the
direction of change, and the momentum after a flick shows how far the thumb can carry. These cues help while a person
searches a range by feel. They distract on a page where the value only needs to be set once; use a plain field there.

Reduced motion removes the momentum, the stretch and the rolling digits. The thumb goes straight to the value and the
bubble only fades, which keeps the control readable without the motion that shows the direction of change.

### Disabled

A disabled slider leaves the tab order and fades to 50% opacity. Its track shows the "no" cursor and ignores the pointer
and the keys. Use it while the value cannot apply, and say why nearby, for example "Set by your plan administrator".
A value the person can never change belongs in `Progress` or in plain text.

<Demo name="Disabled" />

### Example: product filter panel

A filter panel on a product listing, with a price range and a minimum rating. The price thumbs have names of their own,
so a screen reader says "Minimum price" and "Maximum price", and the labels say the unit. The rating slider uses a
labeled mark at the floor ("Any") and at the top, so the lowest and highest choices are named without a row of numbers.
The summary line under the controls reports the current choice in words, so the person can read the result without
moving a thumb.

![A product filter panel with a price range slider and a minimum rating slider](images/ProductFilters.png)

```xml
<StackPanel Width="560" Spacing="22">
    <TextBlock Text="Filter products"
               FontSize="14"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Slider Label="Price (USD)"
                IsRange="True"
                Minimum="0"
                Maximum="1000"
                SmallChange="10"
                LowerValue="200"
                UpperValue="800"
                MinStepsBetweenThumbs="5"
                LowerThumbName="Minimum price"
                UpperThumbName="Maximum price" />
    <arc:Slider Label="Minimum rating"
                Minimum="0"
                Maximum="5"
                SmallChange="1"
                Value="4">
        <arc:Slider.Marks>
            <arc:SliderMark Value="0" Label="Any" />
            <arc:SliderMark Value="1" />
            <arc:SliderMark Value="2" />
            <arc:SliderMark Value="3" />
            <arc:SliderMark Value="4" />
            <arc:SliderMark Value="5" Label="5" />
        </arc:Slider.Marks>
    </arc:Slider>
    <TextBlock Text="Showing 186 products from 200 to 800 USD, rated 4 stars or more."
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Clear" Variant="Secondary" />
        <arc:Button Content="Apply filters" />
    </StackPanel>
</StackPanel>
```

In the app, the summary comes from the view model and is updated in the same command that runs the query, so the text
and the results never disagree. The XAML example writes the text literally so the scene is complete.

### Content guidelines

- **Label**: a short noun, with the unit in brackets when the readout does not carry it: "Volume", "Price (USD)". The
  label names the thumb, so it must say what the value is.
- **Thumb names**: in a range, name each thumb explicitly with `LowerThumbName` and `UpperThumbName`, such as "Minimum
  price" and "Maximum price". The default "minimum" and "maximum" suffixes read poorly next to a unit.
- **Mark labels**: a short preset name or number: "0.5x", "1x", "Any". Use a word when a number is ambiguous, such as
  "Any" for a rating floor of 0.
- **Readout**: a formatted number, not a sentence. Write the unit in the formatter so the readout, the bubble and the
  spoken value agree.
- **Summary**: say the current choice in full words under the controls, such as "Showing products from $200 to $800."

| Instead of | Write |
|---|---|
| Price range | Price (USD) |
| Minimum | Minimum price |
| Rating 0 | Any |
| $200.00 to $800.00 | $200 to $800 |
| Set the volume level | Volume |

### Best practices

**Do**

- Name the unit in the label or in the formatter, and keep the readout and the spoken value the same. Without a unit the
  number is a guess for a person who cannot see the track.
- Use a range with `IsRange` for a span that the person narrows, and write the query from the committed values so the
  results do not update on every step of a drag.
- Set `MinStepsBetweenThumbs` when two thumbs describe one thing, so the minimum can never pass the maximum in the
  application's logic.
- Label only the presets that people choose, and leave the rest as unlabeled ticks. The ticks still show where the scale
  divides, and the labels stay short enough not to collide on a narrow track.

**Avoid**

- Using a slider for a value the person must enter exactly, such as a tax rate or a refund. People cannot aim for a
  digit on a track, and a corrected value then depends on luck.
- Running an expensive request on `ValueChanged` or `RangeChanged`. A drag raises those on every step, so the request
  runs dozens of times for one adjustment.
- Setting a `SmallChange` finer than the track can show. A step of 0.001 across 1000 units moves the thumb by a fraction of
  a pixel, so keys appear to do nothing.
- Relying on the bubble to show the value. The bubble appears only while the thumb is dragged or has keyboard focus, so
  a settled value needs the readout or a summary line.

### Common questions

**Why does the thumb ignore my `Value` when `IsRange` is on?** In range mode the control reads `LowerValue` and
`UpperValue` only, and `Value` is not used. Set the two ends and read them back from the same properties.

**How do I stop the two thumbs from meeting?** Set `MinStepsBetweenThumbs` to the number of steps the thumbs must keep
apart. Keys and drags both stop at the gap, so the lower value never passes the upper one minus the gap.

**Why does a flick sometimes move the value one step past where I released?** The release carries the momentum of the
drag, and a step can take one more move when it is at least 12 px wide on screen. A pause before the release removes the
momentum, so a slow release lands on the nearest step.

**What does a click on a mark label do in range mode?** It moves the thumb nearest to the label's value and commits the
change. The other thumb stays where it is.

**Can a tick without a label be clicked?** No. Only a labeled mark responds to a click, through its label. An unlabeled
tick is reached by dragging or with the keys, so give a preset a label when the person should be able to click it.

## API reference

<!-- api: Slider -->

<!-- api: SliderMark -->

## Accessibility

- `Label` names the slider. In a range, each thumb is named from `LowerThumbName` and `UpperThumbName`, or from `Label`
  with "minimum" and "maximum".
- Each thumb reports its value as text, so the spoken value follows the `ValueFormatter`. Each thumb also reports the
  minimum and maximum allowed for it, so the other thumb limits its range.
- The readout, the bubble and the mark labels are hidden from assistive technology. A clicked label is a pointer
  shortcut; the same values are reachable with the keyboard.
- The control itself is a group, and each thumb is a separate stop in the tab order.
- The focus ring appears after a key press, not after a pointer press.

### Keyboard

Each thumb receives focus with `Tab`. In range mode `Tab` moves from the lower thumb to the upper one, and the keys below
apply to the thumb that has focus.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to the next or previous thumb, then out of the control; a disabled slider is skipped |
| `Right` / `Up` | Increase the value by `SmallChange` |
| `Left` / `Down` | Decrease the value by `SmallChange` |
| `Shift` + arrow, `PageUp` / `PageDown` | Increase or decrease by `LargeChange` |
| `Home` / `End` | Go to the lowest or highest value the thumb may take; in range mode the other thumb and `MinStepsBetweenThumbs` set the limit |
| A key at a limit | The value stays put; the thumb strains toward the key and springs back |

## Motion

- The thumb follows the pointer one to one. On release it settles on the nearest step and keeps the momentum of the
  drag, so a fast flick can move it one step further. The momentum is used only when that step is at least 12 px wide.
- Pressing the track springs the nearest thumb to the pointer. A key at a limit makes the thumb strain toward the press
  and spring back.
- The value bubble pops up above the active thumb, rolls its digits while the value changes and stays for 700 ms after
  a release. The dragged thumb grows slightly.
- Past either limit the track gives by up to 9 px and returns on release, so the person feels the end of the range
  without a message.
- Reduced motion removes momentum, stretch and digit rolling. The thumb goes straight to the value and the bubble only
  fades. Off motion jumps straight to the end state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Drag past a limit | The track gives up to 9 px and springs back | No stretch; the thumb stops at the limit | Immediate |
| Release | The thumb settles on a step with the drag's momentum, at most one step further | The thumb settles on the nearest step with no momentum | Immediate |
| Press on the track | The nearest thumb springs under the pointer | The thumb goes straight to the pointer | Immediate |
| Dragged thumb | Grows to 1.16 times on a snappy spring | Grows at once | Immediate |
| Value bubble | Pops up from 0.6 times with a soft blur; fades out at 0.8 times | Fades in and out | Immediate |
| Readout digits | Roll digit by digit as the value changes | Switch at once | Immediate |

## Related

- `ElasticSlider` for a single setting that stretches past its ends and clicks into detents.
- `NumberField` when the person needs to type an exact number.
- `Progress` for a value that is shown but not changed by the person.

| Component | Choose it when |
|---|---|
| `ElasticSlider` | One physical setting, such as volume or brightness, with detents and a stretch at the limits |
| `NumberField` | The person must type or step an exact number with bounds and a unit |
| `MoneyInput` | The value is an amount of money that needs a currency and parsing |
| `Select` / `SegmentedControl` | The choice is one of a short fixed list of options |
| `DateRangePicker` | The range is a span of dates rather than numbers |
| `Progress` | The value is shown to the person and the application sets it |
