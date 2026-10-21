---
title: Scrub input
summary: A Figma-style number field: drag the label to scrub, or type values, units, and arithmetic.
---

<Demo name="Inspector" />

## When to use

`ScrubInput` is a compact number field for inspector and property panels. Its label doubles as a drag handle: the person
drags it sideways to tune a value, or clicks into the field to type an exact one. A panel of these fields reads like a
design tool's inspector, so it suits any screen where a row of numbers is adjusted many times and checked by eye.

- **Widget layout in a dashboard editor**: the X, Y, W and H of a chart tile. A designer drags W to see the grid respond,
  then types `320` to snap it to the grid unit.
- **Visual settings in a report or document editor**: opacity, rotation and scale. Dragging gives quick feedback, and
  typing gives the exact figure a brand guideline asks for.
- **Settings with a unit the person can change**: a stroke width that a designer enters in `px`, `pt` or `em`. The unit
  button cycles through `Units`, and a typed unit such as `50%` switches the unit.
- **Limits in a rules or threshold panel**: an alert threshold between 0 and 100 percent, or a timeout in seconds with a
  minimum of 1. `Minimum` and `Maximum` keep the value in range while the person drags.
- **Theme and token editors in internal tools**: spacing, radius and elevation values that change the preview at once,
  where `ValueChanged` drives the preview and `ValueCommitted` writes the saved theme.

## When not to use

- **A form number that people step with buttons**: use `NumberField`, which has a stepper and a limit note for a
  checkout quantity or a seat count.
- **A position on a range where the shape matters more than the exact figure**: use `Slider`, for example a volume or a
  brightness control.
- **A currency amount**: use `MoneyInput`, which handles the currency symbol, grouping and rounding of money.
- **Many numeric values in rows**: use `DataGrid` for a table of figures. `ScrubInput` is designed for a handful of fields
  in one panel, not for every cell of a list.
- **A text value with no numeric meaning**: use `Input`. A `ScrubInput` only accepts numbers, arithmetic and units.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ScrubInput Label="X" Value="120" Unit="px" />
</UserControl>
```

`ScrubInput` derives from `TemplatedControl`, not from a text box. The control paints its own shell (the background, the
1 px border, the track and the stretch) and hosts a borderless text editor for typing. That is why a drag, a stretch and
a shake all happen on the shell, while the number stays a normal editable text. The label is the drag handle, so the
visible label is also the control's only handle for the mouse.

### Anatomy

1. **Label** (`Label`): text drawn at weight 500, or a control such as a 14 px `ArcGlyph`. The label is at least 30 px wide,
   with 10 px of padding before it and 4 px after. Dragging it scrubs the value, and a double click returns the value to
   `ResetValue`.
2. **Shell**: a muted surface with a 1 px border and a 12 px corner radius at the medium size (9 px at the small size).
   Its height is 36 px at `Medium` and 30 px at `Small`.
3. **Track** (`IsTrackVisible`): a quiet wash, foreground at 6 percent, that fills the shell in proportion to the value
   between the two limits. It shows only when both limits are finite, and it brightens while the value is being scrubbed.
4. **Number** (the editor): the value as the field shows it, rounded to the effective precision. It has 4 px of padding on
   the left and selects all of its text on focus.
5. **Unit** (`Unit`, `Units`): the unit after the number, with a 2 px gap. With one unit it is plain text. With several
   units it becomes a button that cycles through them, with a 6 px corner radius and a foreground wash of 7 percent on hover.
6. **Modifier tag**: a 20 px pill about 6 px above the field that reads "×10" with Shift or "×0.1" with Alt while a drag is
   in progress. It shows only while a modifier is held.

### Scrubbing and typing

A press on the label and a horizontal move of 3 px or more start a scrub. Each `PixelsPerStep` pixels (2 by default) of
travel change the value by one `Step`. The value follows the pointer while the drag runs, and the release commits it.
A click in the number focuses the field and selects its text. Typing does not change the value until the entry is
committed with Enter, when the field loses focus, or with an arrow key.

Shift and Alt change the step during a drag, and the tag shows the multiplier. Escape cancels a drag and returns the value
to where the drag started. Touch works too: a horizontal drag scrubs, and a vertical touch scrolls the parent panel
instead, so a long inspector can still be scrolled with a finger.

### Limits and wrapping

`Minimum` and `Maximum` define the range. The defaults are negative and positive infinity, which means no limit. A
dragged value past a limit stretches the field by up to 12 px with resistance and springs back on release. A typed or
stepped value past a limit is clamped to the limit and committed; a step at the limit does not change the value and
gives a small bump.

`IsWrapping` makes the value wrap from the maximum to the minimum instead of stopping. It needs both limits, and the
maximum must be larger than the minimum. Without both finite limits the setting has no effect, so an unbounded field never
wraps.

```xml
<arc:ScrubInput Label="W" Value="320" Minimum="0" Unit="px" ResetValue="320" />
<arc:ScrubInput Label="O" Value="80" Minimum="0" Maximum="100" Unit="%" ResetValue="100" />
```

```csharp
var rotation = new ScrubInput
{
    Label = "R",
    Value = 15,
    Minimum = 0,
    Maximum = 360,
    IsWrapping = true,
    IsTrackVisible = false,
    Unit = "°",
    ResetValue = 0
};
```

`ResetValue` is where a double click on the label returns. `IsTrackVisible` defaults to showing the track when both limits
are finite; set it to `False` for an angle, where a track across 0 to 360 says little.

### Steps and precision

`Step` is the change per arrow key and per step of a drag, and it defaults to 1. `FineStep` (with Alt) defaults to a tenth
of `Step`, and `CoarseStep` (with Shift) defaults to ten times `Step`. Coarse steps land on the coarse grid, so 13 with
Shift goes to 20 at a step of 10, not to 23.

`Precision` sets the decimal places. When it is not set, the field keeps the larger number of decimals of `Step` and of
the fine step, so a `Step` of 0.1 keeps two decimals. The displayed text drops trailing zeros, so 1.5 shows as `1.5`.

```xml
<arc:ScrubInput Label="Gap" Value="1.5" Step="0.1" Minimum="0" Unit="em" />
```

### Input grammar

The editor accepts arithmetic, a unit after a number, and a comma as the decimal point. `ScrubExpression.Evaluate` runs the
same grammar, so a field and a settings import agree on what a value means.

| Entry | Result |
|---|---|
| `12*2`, `12 × 2`, `24/2`, `12+3-1` | Arithmetic; `×` and `÷` and `−` are accepted as operators |
| `(100-24)/2` | Parentheses group the operations |
| `1,5em` | `1.5` with the unit `em`; a comma is a decimal point |
| `50%` | `50` with the unit `%`, which switches the unit when `%` is in `Units` |
| `15deg`, `15°` | `15` with the degree unit, the same as `°` |
| `abc`, `12px` with no `px` in `Units`, `4/0` | Rejected: the old value stays and the field flags the entry as invalid |

A unit matches its entry in any letter case. An entry with a unit that is not in the unit list is rejected, and so is a
result that is not finite, such as division by zero.

### Multiple units

With several `Units` the unit becomes a button. A click cycles to the next unit, and the value keeps its number. A typed
unit in the entry switches the unit too: typing `50%` in a field whose unit is `px` sets the unit to `%` and the value to
50. A single unit, or a field without `Units`, shows the unit as plain text, and the unit button is not a tab stop.

<Demo name="CyclingUnits" />

### Sizes

`Size` sets the height. `Small` is 30 px with 12 px text and a 9 px corner radius. `Medium` is the default: 36 px with 14 px
text and a 12 px corner radius. `Large` has no size of its own and renders as `Medium`. Use `Small` for a dense inspector
column and `Medium` for a form or a panel that mixes these fields with other 36 px controls.

<Demo name="Sizes" />

### Binding and events

`Value` and `Unit` are two-way. `ValueChanged` fires for every change, including each step of a drag and each change made
from code. `ValueCommitted` fires once a change settles: when a scrub ends, when Enter or blur commits a typed entry, when
a key step lands, or when a reset runs. Both report the old value, the new value, the unit and the `ChangeSource`.

`ChangeSource` tells you where the change came from. `Programmatic` marks a value set from code or a binding, which raises
`ValueChanged` only and gives no feedback; `Scrub`, `Type`, `Key` and `Reset` mark the four ways a person changes the
value. A handler that saves the value should use `ValueCommitted`, so it does not write during every step of a drag.

<Demo name="Events" />

```xml
<arc:ScrubInput Label="Opacity" Value="{Binding Opacity, Mode=TwoWay}" Minimum="0" Maximum="100" Unit="%" />
```

```csharp
amount.ValueCommitted += (_, e) =>
{
    Console.WriteLine($"{e.OldValue} -> {e.NewValue} {e.Unit} ({e.ChangeSource})");
};
```

`ValueCommitted` also fires when a commit leaves the value unchanged, such as a typed entry clamped to a limit. Compare
`e.OldValue` with `e.NewValue` before you save if the save is expensive.

### Binding to a view model

A layout inspector binds each field to a property of the selected widget. The view model owns the values and the saving,
and the fields show them. The example uses CommunityToolkit.Mvvm.

```csharp
public partial class WidgetLayoutViewModel : ObservableObject
{
    [ObservableProperty]
    private double _x = 120;

    [ObservableProperty]
    private double _width = 320;

    [ObservableProperty]
    private double _opacity = 80;

    [RelayCommand]
    private void ResetLayout()
    {
        X = 0;
        Width = 320;
        Opacity = 100;
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:ScrubInput Label="X"
                    Value="{Binding X, Mode=TwoWay}"
                    Unit="px" />
    <arc:ScrubInput Label="W"
                    Value="{Binding Width, Mode=TwoWay}"
                    Minimum="0"
                    Unit="px"
                    ResetValue="320" />
    <arc:ScrubInput Label="Opacity"
                    Value="{Binding Opacity, Mode=TwoWay}"
                    Minimum="0"
                    Maximum="100"
                    Unit="%"
                    ResetValue="100" />
</StackPanel>
```

`Value` and `Unit` are the two-way properties. `ValueChanged` and `ValueCommitted` are events, not commands, so connect
them in code-behind or a behavior, and write the saved document from `ValueCommitted`. `Minimum`, `Maximum`, `Step` and
`ResetValue` are one-way settings for the field.

### Glyph labels

A 14 px glyph can stand in for the text label, which keeps a dense panel calm. A glyph has no text for assistive technology,
so give the field an accessible name with `AutomationProperties.Name`. A double click on the glyph still resets the value.

<Demo name="GlyphLabels" />

### States

A disabled field dims to 50 percent and ignores input, so it cannot be scrubbed or typed into. An entry that cannot be read,
such as `abc`, is rejected: the edge turns red for 900 ms with a short shake, and the old value stays. A rejected entry is
the only way the field shows an error; it has no message row, so put a visible hint next to the panel when the valid
range matters.

<Demo name="States" />

### Example: dashboard tile inspector

The layout panel of a dashboard editor, shown for the selected tile "Revenue by region". The designer drags the W and H
labels to feel the grid, and types an exact size when a brand spec asks for one. Rotation has no track because a track
over 0 to 360 degrees says little. A drag past 0 or 100 percent stretches the opacity field and springs back. The tile values are
the source of truth; the panel writes them back on `ValueCommitted`.

![Layout inspector for a dashboard tile with X, Y, W, H, rotation and opacity fields](images/TileInspector.png)

```xml
<StackPanel Width="600" Spacing="16">
    <TextBlock Text="Revenue by region"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,Auto,Auto" ColumnSpacing="12" RowSpacing="12">
        <arc:ScrubInput Grid.Row="0" Grid.Column="0" Label="X" Value="120" Unit="px" />
        <arc:ScrubInput Grid.Row="0" Grid.Column="1" Label="Y" Value="48" Unit="px" />
        <arc:ScrubInput Grid.Row="1" Grid.Column="0" Label="W" Value="320" Minimum="0"
                        Unit="px" ResetValue="320" />
        <arc:ScrubInput Grid.Row="1" Grid.Column="1" Label="H" Value="180" Minimum="0"
                        Unit="px" ResetValue="180" />
        <arc:ScrubInput Grid.Row="2" Grid.Column="0" Label="Rotation" Value="15" Minimum="0"
                        Maximum="360" IsWrapping="True" IsTrackVisible="False" Unit="°"
                        ResetValue="0" />
        <arc:ScrubInput Grid.Row="2" Grid.Column="1" Label="Opacity" Value="80" Minimum="0"
                        Maximum="100" Unit="%" ResetValue="100" />
    </Grid>
    <TextBlock Text="Drag a label to scrub. Double click a label to reset it."
               FontSize="12"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

### Content guidelines

- **Label**: a short axis or property name in a dense panel ("X", "W", "Rotation", "Opacity"), and a full word in a form
  ("Opacity" rather than "Op"). The unit goes in `Unit`, never in the label.
- **Unit**: the unit the value is measured in: `px`, `%`, `em`, `°`. Use the symbol, not the word, in dense rows.
- **Limits**: set `Minimum` and `Maximum` to the physical or business limit, so the drag cannot pass a value that the
  product rejects. Leave them unset only when the value has no natural bound.
- **Help text**: a `ScrubInput` has no message row. State the valid range in the nearby label, a heading or a tooltip
  of the panel, for example "Opacity 0 to 100 percent".

| Instead of | Write |
|---|---|
| Label "Width (px)" with Unit "px" | Label "W" with Unit "px" |
| Label "Opacity %" | Label "Opacity" with Unit "%" |
| Label "Rot" | Label "Rotation" with Unit "°" |
| Unit "pixels" | Unit "px" |

### Best practices

**Do**

- Set `ResetValue` on every field that has a default. A double click that returns the design to its default is faster
  than a menu, and it matches what the person expects from a design tool.
- Save from `ValueCommitted`, not from `ValueChanged`. A drag raises `ValueChanged` on every step, and saving on each
  step writes the document dozens of times per second.
- Set `Step` to the grain the product uses, such as 1 px for a layout or 0.1 em for typography. The fine and coarse steps
  follow from it.
- Keep the field width at the column width of the panel and use `Size="Small"` for a dense inspector column.

**Avoid**

- Using it for currency. A scrubbed amount has no rounding rule, and `MoneyInput` formats the currency, the grouping and
  the decimals for the locale.
- Relying on a drag alone for a value a person must reach exactly. Typing is the exact path; keep the field editable.
- Putting many `ScrubInput` fields in a long table. Each field is a template with its own pointer tracking; use `DataGrid`
  for rows of numbers.
- Setting `IsWrapping` without finite limits. Wrapping needs both `Minimum` and `Maximum`, so without them the setting does
  nothing.
- Disabling a field to show an error. A disabled field dims and ignores input; a rejected entry is the error signal.

### Common questions

**Why did my typed value jump back to the old one?** The entry was rejected. An unknown unit, a malformed expression or a
division by zero keeps the old value, sets `IsInvalid` for 900 ms and shakes the field. A value outside the limits is not
rejected; it is clamped to the limit and committed.

**Why does a typed value like `1.5` show as `1.5` when the step is 0.1?** `Precision` is not set, so the field keeps the
larger number of decimals of `Step` and of the fine step (a tenth of `Step`). Trailing zeros are dropped. Set `Precision`
to fix the number of decimals for every value.

**How do I change the unit from code?** Set `Unit` to a string. For a unit the person can cycle or type, put that string in
`Units` too, so the unit button and the typed entry accept it. The `Unit` change is two-way, so a binding receives it.

**Does it work with touch?** Yes. A horizontal touch drag scrubs the value, and a vertical touch scrolls the parent panel,
so a long inspector remains scrollable with a finger.

**Can I use it without a label?** Yes, through a glyph label, which can be any control. `Label` is an `object`, so an
`ArcGlyph` works. Give the field an `AutomationProperties.Name`, because a glyph has no text for assistive technology.

## API reference

<!-- api: ScrubInput -->

Companion types. `ScrubInputChangeSource` is the `ChangeSource` of the event arguments:

| Member | Meaning |
|---|---|
| `Programmatic` | `Value` was set from code or a binding; raises `ValueChanged` only |
| `Scrub` | A drag on the label; also a drag cancelled with Escape |
| `Type` | A typed entry committed with Enter, blur, an arrow key or assistive technology |
| `Key` | An ArrowUp or ArrowDown step |
| `Reset` | A double click on the label, returning to `ResetValue` |

`ScrubInputValueChangedEventArgs` is the argument of `ValueChanged` and `ValueCommitted`:

| Member | Meaning |
|---|---|
| `OldValue` | The value before the change; after a scrub `ValueCommitted` reports the value the drag started from |
| `NewValue` | The value after the change |
| `ChangeSource` | The `ScrubInputChangeSource` that produced the change |
| `Unit` | The unit in effect after the change, `""` when the field has none |

`ScrubExpression.Evaluate(text, units)` is public and returns a `ScrubExpressionResult` (`Value`, `Unit`) or `null` for a
rejected entry. Use it to read a typed value in an import or a command line with the same rules as the field.

## Accessibility

- The number is a spin button. Its accessible name is the label, its value text reads the value with its unit (for example
  "50 percent" or "90 degrees"), and it exposes the minimum, maximum and current value as a range.
- The label is a real label: a click on it focuses the field and selects its number. A label that is a glyph needs
  `AutomationProperties.Name`.
- An invalid entry sets the invalid state for a short time and keeps the old value. The message is announced.
- The unit button is named "Unit: <unit>. Change unit", and it is a tab stop only when there are several units.
- Setting a value through the range provider clamps it to the limits, as typing does.

### Keyboard

The field itself is not a focus target: the number editor takes focus, and the label is only a pointer handle.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Moves focus to the number, and to the unit button when there are several units |
| Click on the number | Focuses it and selects its text |
| `ArrowUp` / `ArrowDown` | Steps the value by `Step`, after committing an open entry |
| `Shift` + `ArrowUp` / `ArrowDown` | Steps by `CoarseStep`, landing on the coarse grid |
| `Alt` + `ArrowUp` / `ArrowDown` | Steps by `FineStep` |
| `Enter` | Commits the typed entry and selects all of the text |
| `Escape` (with an open entry) | Reverts the entry to the last value and selects all |
| `Escape` (during a drag) | Cancels the drag and returns the value to where the drag started |
| Focus leaves the number | Commits the open entry |
| `Shift` or `Alt` held during a drag | Switches the step at once and shows the multiplier tag |
| Double click on the label | Returns the value to `ResetValue` |

## Motion

- Past a limit the field stretches with resistance, and it springs back on release. The digits lean with the stretch.
- An invalid entry or a step at a limit shakes the field once.
- A unit change rises in with a small blur. The track fill follows the pointer while dragging and glides to the new value
  when the value is typed or stepped.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Drag past a limit | The field stretches up to 12 px and springs back on release | No stretch | Immediate |
| Rejected entry or step at a limit | The field shakes once; the edge is red for 900 ms | No shake; the red edge still shows for 900 ms | Immediate; the red edge shows for 900 ms |
| Unit changes | The unit rises in with a small blur | The unit fades in place | Immediate |
| Track fill | Follows the pointer while dragging; glides to the new value when typed or stepped | Jumps to the new value | Immediate |

## Related

- `NumberField` for a form number with a stepper and a limit note.
- `Slider` when the position on a range matters more than the exact value.
- `MoneyInput` for currency values.
- `Input` for free text, and `DataGrid` for tables of numbers.

| Component | Choose it when |
|---|---|
| `NumberField` | The number is a form value that people step with buttons, such as a quantity |
| `Slider` | People choose a place on a range, and the exact figure matters less than the position |
| `MoneyInput` | The value is an amount of money with a currency and rounding rules |
| `DataGrid` | The numbers are rows and columns in a table rather than a few fields in a panel |
| `Input` | The value is text with no numeric meaning |
