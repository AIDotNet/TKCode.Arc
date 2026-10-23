---
title: Elastic slider
summary: A slider that stretches like rubber past its ends, with a swaying value tag and detents the thumb clicks into.
---

<Demo name="RubberBand" />

## When to use

`ElasticSlider` sets one value on a track that gives under the finger. It fits a single setting that people adjust by
feel and confirm by looking at the track: a control in a media player bar, a tool inspector, or a settings row where
the slider is the only control on the line. The track stretches past either end and springs back on release, so the
person feels that a limit was reached without a message appearing.

- **Playback and output levels** in a meeting or media app: the volume or microphone gain in a player bar. Exact
  decibels matter less than the feel of quiet and loud, and a detent at a useful level (`Off`, `50`, `Max`) gives a
  target to aim for.
- **Adjustments in a creative tool inspector**: brightness, contrast or blur of the selected layer. People compare a
  result by dragging back and forth, so the value tag that follows the thumb and the stretch at the limits suit the
  task.
- **Device and display controls** in an operations console or a kiosk setup: screen brightness on a device settings
  page, or a fan speed on a rack panel, where the setting changes often and a preset such as 50% is a common target.
- **Strength of a visual effect** in a theme or branding editor, such as the opacity of a banner overlay or the
  intensity of a shadow, where the person is judging how it looks rather than reading a number.

## When not to use

- **A quiet form or settings page where the value is set once**: use `Slider`. It has the same track and value bubble
  but stays still at the ends and suits forms that people read top to bottom.
- **A range with a lower and an upper end**, such as a price or date filter: use `Slider` with `IsRange`.
  `ElasticSlider` has a single `Value` and no range mode.
- **An exact number the person must type**, such as a tax rate, a discount or a quantity: use `NumberField`. A track
  only approximates a value, and the stretch does not help when the person needs a precise figure.
- **A value the person cannot change**, such as upload progress, storage used or a job's completion: use `Progress`.
- **Many sliders in one list**, such as a row per channel in a mixer or per product in an inventory table: use
  `Slider`. The stretch, sway and detent pop repeat on every row and pull attention away from the data.

## Usage

Basic usage is a single value. `Value` is the position on the track, and `Minimum` and `Maximum` set its range.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ElasticSlider Label="Volume"
                       Value="40"
                       Width="360"
                       ValueFormatter="{Binding VolumeFormatter}" />
</UserControl>
```

`ElasticSlider` derives from Avalonia's `RangeBase`. `Value`, `Minimum`, `Maximum`, `SmallChange`, `LargeChange` and the
`ValueChanged` event therefore behave as they do on any range control. The control adds the label, the readout, the
value tag, the rubber-band track, the detents and the two icon slots. `SmallChange` is the step the value snaps to, and
it defaults to 1 on this control.

### Anatomy

1. **Label** (`Label`): medium weight, on the row above the track with a 19.6 px line height. It truncates with an
   ellipsis when the width is short. It is also the thumb's accessible name.
2. **Readout**: the formatted `Value`, right-aligned on the label row in the secondary text color, with 16 px of space
   before it. It fades out while the value tag rides the thumb, so the number is never shown twice.
3. **Start icon** (`StartIcon`): before the track with 12 px of space, in the muted text color. It is hidden when the
   property is null.
4. **Track and fill**: an 8 px track in the control track color, with the part before the thumb in the control fill
   color. The track row is the medium control height.
5. **Thumb**: a 24 px disc with the control thumb color and its raised shadow. Its hit area reaches 12 px beyond each
   side of the disc, so a near miss still grabs it.
6. **Value tag**: a raised bubble 10 px above the thumb (10 px higher again after a touch or pen press), at least
   28 px tall, with 12 px of horizontal padding. It uses the foreground color as its background and the background
   color as its text.
7. **Detent ticks**: 4 px dots, one per mark. Ticks on the track use the foreground at 32% opacity; ticks on the filled
   part use the control glyph color at 60% so they stay visible on the fill.
8. **End icon** (`EndIcon`): after the track with 12 px of space, in the muted text color.
9. **Mark labels row**: 20 px high, under the track, in the extra small type size in the muted color. The row appears
   only when at least one mark has a `Label`.

### Icons

`StartIcon` and `EndIcon` sit before and after the track. When the track is pulled past an end, the icon on that end
moves outward with it, so the stretch reads as the whole control giving under the finger. Use glyphs or small icon
buttons there, not text that needs room to grow. A pair such as a minus and a plus for volume, or a moon and a sun for
brightness, tells people which way the value goes.

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

<Demo name="Detents" />

Detents work only while the thumb is dragged. Within 7 px of a mark on screen, the pointer holds the value on the mark,
and the thumb glides onto it; moving farther away releases the value to the pointer again. A mark outside `Minimum` and
`Maximum` is ignored. When two marks are less than about 14 px apart on screen, the nearer one always wins, so the other
cannot be reached by dragging. Keys do not snap to marks: they move by `SmallChange`, and a key press that lands exactly
on a mark still plays the tick's pop.

Use marks for the values people actually choose: `0` and `100` for the limits, one middle preset, or a level that has a
meaning such as unity gain. Label only the ones that need a name (`Off`, `Max`); a row of bare numbers under the track
adds noise.

```xml
<arc:ElasticSlider Label="Brightness" Value="50" Width="360">
    <arc:ElasticSlider.Marks>
        <arc:SliderMark Value="0" Label="Off" />
        <arc:SliderMark Value="50" Label="50" />
        <arc:SliderMark Value="100" Label="Max" />
    </arc:ElasticSlider.Marks>
</arc:ElasticSlider>
```

### Steps and format

`SmallChange` is the step the value snaps to. Arrow keys and a drag without a detent both land on it. `LargeChange`
is the step for `PageUp`, `PageDown` and `Shift` with an arrow; when it is not set, it is a tenth of the range.

`ValueFormatter` formats the value tag, the readout and the spoken value. It is a `Func<double, string>`, so set it in
code or bind it from the view model. When it is not set, the value is shown with as many decimals as the step and the
minimum need, so a step of 0.1 shows `1.0` and a whole-number step shows `40`. `IsValueVisible` hides the readout
beside the label. The tag still appears while the thumb is dragged or changed by keys, so hiding the readout does not
hide the value.

<Demo name="FineSteps" />

```xml
<arc:ElasticSlider Label="Zoom"
                   Minimum="0.5"
                   Maximum="3"
                   SmallChange="0.1"
                   Value="1"
                   Width="360"
                   ValueFormatter="{Binding ZoomFormatter}" />
```

Choose the step from the track width. A step should move the thumb by several pixels: on a track about 300 px wide, a
step of 0.1 across a range of 2.5 moves it 12 px, which is visible. A much finer step makes each key press look like
nothing happened, and a much coarser one skips the values in between.

### Events

`ValueCommitted` runs once per release, per key press that changes the value, and per clicked mark label. Its
`SliderValueCommittedEventArgs.Value` is the committed value; `LowerValue` and `UpperValue` repeat it and `IsRange` is
`False`, because the control has one value. A key press at a limit does not change the value, so it does not commit.
Put slow work, such as a network request, on `ValueCommitted`, not on `ValueChanged`, which the inherited event raises
on every step of a drag.

```csharp
volume.ValueCommitted += (_, e) => SaveVolume(e.Value);
volume.ValueChanged += (_, e) => PreviewVolume(e.NewValue);
```

### Binding to a view model

In an MVVM app the view model owns the value and the slider mirrors it. Use CommunityToolkit.Mvvm for the view model.
`Value` is two-way: a drag, a key press or a detent writes the new value back to the view model, and a change made in
code moves the thumb with a spring. `ValueCommitted` is an event, not a command, so subscribe to it in the view's code
behind and call the view model from there.

```csharp
public interface IAudioSettings
{
    Task SetVolumeAsync(double level);
}

public partial class PlaybackViewModel : ObservableObject
{
    private readonly IAudioSettings _settings;

    [ObservableProperty]
    private double _volume = 40;

    public PlaybackViewModel(IAudioSettings settings) => _settings = settings;

    public Task SaveVolumeAsync(double value) => _settings.SetVolumeAsync(value);
}
```

```xml
<arc:ElasticSlider x:Name="Volume"
                   Label="Volume"
                   Minimum="0"
                   Maximum="100"
                   Value="{Binding Volume, Mode=TwoWay}"
                   Width="360" />
```

```csharp
// In the view's code behind, after InitializeComponent().
Volume.ValueCommitted += async (_, e) => await ViewModel.SaveVolumeAsync(e.Value);
```

- `Value` is the two-way property to bind.
- Bind `Mode=TwoWay` explicitly so the intent is visible in the XAML. The view model's `Volume` then changes on every
  step of a drag, which is right for a preview and wrong for an expensive save; save from `ValueCommitted` instead.
- Bind `Value` to a `double` property. The slider works in `double`, so a whole-number property should also be a
  `double`, or the view model should convert at its boundary.
- `ValueFormatter` is set in code or with a binding to a `Func<double, string>` on the view model, as in the Usage
  example.

### Using motion with restraint

The motion on this control carries information. The stretch says that the limit was reached, the sway shows how fast
the thumb moved, and the detent pop confirms that the value landed on a mark. Use it on one or two elastic sliders in a
view, where a person spends time adjusting a value by feel. Put several of them in a list and the motion becomes a
pattern that competes with the content; use `Slider` there.

Reduced motion removes the stretch, the sway and the detent pop. The thumb goes straight to the value and the tag only
fades in, so the control still reads correctly without the physical feedback. Do not depend on the motion to show the
value: the readout and the spoken value always carry it.

### Disabled

A disabled slider leaves the tab order and fades to 50% opacity. Its track shows the "no" cursor and ignores the
pointer and the keys. Use it only while the value cannot apply, and say why nearby, for example "Available on the Pro
plan". A value the person can never change belongs in `Progress` or in plain text.

<Demo name="Disabled" />

### Example: photo editor adjustments

An image editor's inspector with three adjustments: exposure and contrast centered on 0, and the opacity of the selected
layer. The integers need no formatter, so the readout shows plain values. The detent on 0 is the one that matters: a
person who drags exposure off neutral and back wants to land on it exactly, and the thumb holds there.

![An adjustments panel with exposure and contrast detents at zero and a layer opacity slider](images/PhotoAdjustments.png)

```xml
<StackPanel Width="560" Spacing="22">
    <TextBlock Text="Adjustments"
               FontSize="14"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:ElasticSlider Label="Exposure"
                       Minimum="-100"
                       Maximum="100"
                       Value="25"
                       SmallChange="1">
        <arc:ElasticSlider.StartIcon>
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Moon}"
                          Size="16"
                          Foreground="{arc:ArcDesignTokenResource TextMuted}"
                          VerticalAlignment="Center" />
        </arc:ElasticSlider.StartIcon>
        <arc:ElasticSlider.EndIcon>
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Sun}"
                          Size="16"
                          Foreground="{arc:ArcDesignTokenResource TextMuted}"
                          VerticalAlignment="Center" />
        </arc:ElasticSlider.EndIcon>
        <arc:ElasticSlider.Marks>
            <arc:SliderMark Value="-100" Label="-100" />
            <arc:SliderMark Value="0" Label="0" />
            <arc:SliderMark Value="100" Label="100" />
        </arc:ElasticSlider.Marks>
    </arc:ElasticSlider>
    <arc:ElasticSlider Label="Contrast"
                       Minimum="-100"
                       Maximum="100"
                       Value="-10"
                       SmallChange="1">
        <arc:ElasticSlider.Marks>
            <arc:SliderMark Value="0" />
        </arc:ElasticSlider.Marks>
    </arc:ElasticSlider>
    <arc:ElasticSlider Label="Layer opacity"
                       Minimum="0"
                       Maximum="100"
                       Value="80"
                       SmallChange="1"
                       IsValueVisible="True" />
</StackPanel>
```

The labels are nouns, and the marks print only the values people need to recognize: the two limits and the neutral
point. The contrast slider has one unlabeled tick at 0, so its detent still holds without adding a label row. The
layer opacity slider has no marks, because no preset opacity is meaningful for every layer.

### Content guidelines

- **Label**: one or two nouns in sentence case, such as "Volume", "Brightness" or "Layer opacity". The label names the
  thumb for assistive technology, so it must say what the value is.
- **Mark label**: a short word or number that names the preset: `Off`, `Max`, `50`. A mark without a label still
  holds the thumb, so leave unlabeled any preset that is obvious from the track.
- **Units**: put the unit in `ValueFormatter` output, not in the label, so the readout, the tag and the spoken value
  agree. Keep the unit short; a percent sign is clearer than the word "percent" on a track this small.
- **Hints**: a sentence beside the control, not inside it, when the value has a consequence: "Applies to the selected
  layer only."

| Instead of | Write |
|---|---|
| Set the volume level | Volume |
| Maximum volume | Max |
| 40 percent | 40% |
| Brightness (0 to 100) | Brightness |

### Best practices

**Do**

- Keep one elastic slider per decision and place it where the person is already looking at the result, such as beside
  the preview. The stretch and tag work best when the effect is visible while the thumb moves.
- Add detents at the values people return to, such as neutral, off or a standard preset, so they can land on them
  without precision.
- Save from `ValueCommitted`, not from `ValueChanged`. A drag raises `ValueChanged` on every step; committing on
  release sends one request for the chosen value.
- Pick a `SmallChange` that matches the precision the setting needs, and let `LargeChange` cover a useful jump (a tenth
  of the range by default is a good start).

**Avoid**

- Using the control for values that need an exact entry, such as a tax rate or a quantity. The track approximates the
  value, and people then have to correct it by stepping one key at a time.
- Stacking several elastic sliders in one list or table. The repeated stretch and sway draw attention away from the data
  they belong to.
- Hiding the readout and relying on the tag alone for a value people must record. The tag appears only while the thumb
  moves or is changed by keys, so a settled value is then invisible.
- Marks closer than about 14 px. The nearer mark wins and the other becomes unreachable with the pointer.
- Putting text that needs room in `StartIcon` or `EndIcon`. Those slots move with the stretch, so long content clips
  or crowds the track.

### Common questions

**Why does the thumb not land on a mark when I press a key?** Keys step by `SmallChange` and never snap to marks. A mark
is reachable with the keyboard only when a step lands on its value. For marks at 0, 25, 50, 75 and 100, set
`SmallChange="25"`, or use a `SmallChange` that divides the spacing between the marks evenly.

**How do I hide the readout and keep the value out of the way?** Set `IsValueVisible="False"` to hide the readout beside the
label. The tag still appears while the thumb is dragged or changed by keys, so the number stays available during an
adjustment and is hidden once the person stops.

**Can one elastic slider select a range?** No. `ElasticSlider` has one `Value` and no `IsRange`. Use `Slider` with
`IsRange="True"` and read `LowerValue` and `UpperValue`.

**Why does a key press at the end not raise `ValueCommitted`?** At a limit the value cannot change, so the control only
bounces the track toward the key. No value changes, so there is nothing to commit; a listener that saves on commit
correctly does nothing.

**Why is there no stretch or sway on a machine with reduced motion?** The stretch, the sway and the detent pop run only at
the full motion level. At the reduced level the thumb goes straight to the value and the tag only fades in. Test the
feel of the control at the full level, and check that the reduced level still reads correctly.

## API reference

<!-- api: ElasticSlider -->

<!-- api: SliderMark -->

## Accessibility

- `Label` names the thumb. The thumb's spoken value is the formatted `Value`, so a `ValueFormatter` that returns
  `40%` is read as `40%`. Without a label the thumb has no name, so keep the label visible.
- The control itself is a group, and the thumb is the only stop in the tab order. The track row, the value tag, the
  readout and the mark labels are not separate stops.
- The mark labels are hidden from assistive technology. Clicking a label is a pointer shortcut; the keyboard reaches the
  same values by stepping with `SmallChange` or `LargeChange`.
- The value tag and the detent pop are visual only. The spoken value carries the same number, so the tag adds nothing a
  screen reader user needs.
- The focus ring appears after a key press, not after a pointer press, so a mouse user is not left with a ring on the
  thumb.

### Keyboard

The thumb receives focus with `Tab`. The control itself does not take focus.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to the thumb and out of it; a disabled slider is skipped |
| `Right` / `Up` | Increase the value by `SmallChange` |
| `Left` / `Down` | Decrease the value by `SmallChange` |
| `Shift` + arrow, `PageUp` / `PageDown` | Increase or decrease by `LargeChange` |
| `Home` / `End` | Go to `Minimum` or `Maximum` |
| An arrow, `PageUp`, `PageDown`, `Home` or `End` at a limit | The value stays put; the track stretches toward the key and springs back |

## Motion

- Past either end, the track stretches like rubber, up to 26 px, and springs back with the release speed. The thumb
  swells while it is held.
- The value tag hangs above the thumb and follows it on a soft spring. Its angle follows the thumb's speed, so a fast
  drag tilts it more, up to 11 degrees.
- When the thumb clicks into a detent, the tick on the track pops once, up to twice its size, and settles in 0.34 s.
- Values that arrive from outside a drag, such as a key press, a bound value or a clicked mark label, move the thumb with
  a spring from where it currently is on screen.
- Reduced motion removes the stretch, the sway and the detent pop. The thumb goes straight to the value and the tag only
  fades in. Off motion jumps straight to the end state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Drag past an end | The track stretches up to 26 px and springs back with the release speed (0.5 s, bounce 0.42) | No stretch; the thumb stops at the limit | Immediate |
| Thumb while dragged | Swells to 1.28 times on a snappy spring | Swells at once | Immediate |
| Value tag | Follows the thumb on a soft spring and sways up to 11 degrees | Stays at the thumb and fades in over 0.15 s | Immediate |
| Readout | Fades out while the tag rides the thumb | Switches at once | Immediate |
| Detent pop | The tick goes from 1 to 2.1 to 1 times its size over 0.34 s | No pop | No pop |

## Related

- `Slider` for a quiet form control, a range with two thumbs, or a list of sliders where the motion would repeat.
- `NumberField` when the person needs to type an exact number.
- `Progress` for a value that is shown but not changed by the person.

| Component | Choose it when |
|---|---|
| `Slider` | The setting sits in a form, the value is a range, or many sliders repeat in one view |
| `NumberField` | The person must type or step an exact number, with a unit and bounds |
| `ScrubInput` | The value is a number changed by dragging on the label itself, without a track |
| `Progress` | The value is shown to the person and the application sets it |
| `Switch` | The setting has two states and no intermediate values |
