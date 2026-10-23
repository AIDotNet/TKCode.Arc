---
title: Number field
summary: Enter a bounded number with clear increment controls.
---

<Demo name="Default" />

## When to use

`NumberField` is a bounded numeric input with a stepper on each side. It fits a value that a person sets exactly and
often changes by one step: the number of seats on a plan, the items in a cart, the days of a retention window, a unit
price. The field enforces the range while the person types and when they press a stepper, so the form never stores a
value outside the limits it shows.

- **Seat and license counts in a SaaS admin console**: an admin raises the number of seats from 3 to 4 with the plus
  stepper, and the label "Up to 10 per team" stays visible while the limit is enforced.
- **Quantities in an order or cart**: a buyer changes the number of units for a line item between 1 and 99, and a limit
  note explains why the plus stepper stops at the maximum.
- **Retention and threshold settings in developer or ops tools**: an engineer sets a retry count, a timeout in seconds or
  a percentage threshold, with a unit suffix that follows the value.
- **Prices and unit costs with a fixed step**: a catalog editor steps a price by $0.50 or a cost by $0.01, and the step
  sets the number of decimals the field shows.
- **Dense settings panels where a value is adjusted in place**: a design tool's width or opacity field, where dragging
  the label changes the value when `IsScrubEnabled` is on.

## When not to use

- **Approximate values where position matters more than the exact number**: use `Slider`, which shows the whole range
  and lets the person judge a position.
- **Digit strings such as phone numbers, postal codes or verification codes**: use `Input`. A stepper makes no sense for
  an identifier, and the field would reformat it.
- **Currency amounts with a currency code**: use `MoneyInput`, which groups the amount, shows the currency and reports
  minor units.
- **Canvas-style property panels that scrub many values**: use `ScrubInput` when the whole panel is built around dragging.
- **A number chosen from a fixed list of options**: use `Select` or `Combobox`. A stepper implies the values are
  continuous.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:NumberField Label="Seats"
                     Minimum="1"
                     Maximum="10"
                     Value="3"
                     Description="Up to 10 per team." />
</UserControl>
```

`NumberField` derives from `FieldTextBox`, so it is an Avalonia `TextBox` with the Arc label, description and error rows
around it. The text box is the draft editor: when the field is not being edited, its text is transparent and the value is
painted as rolling digits. Typing opens a draft; the draft applies live while it is valid, in range and on the step
grid, and commits when the person presses Enter or leaves the field. `Value` is a `decimal`, so the arithmetic on
prices and fractions is exact.

### Anatomy

1. **Label** (PART_Label): the field's label, on a head row above the shell with 8 px between the row and the shell.
2. **Limit note** (PART_LimitNote): a 12 px note beside the label, such as "Max 10 seats", in a warning tint mixed from
   the foreground and the warning color. It appears when a press meets a limit.
3. **Decrease stepper** (PART_DecreaseButton): a square of 28, 34 or 40 px by size, with a 16 px minus glyph and a
   14 px corner radius.
4. **Value** (PART_ValueGroup, PART_Digits): the value with its prefix and suffix, between the steppers with a 2 px gap
   on each side.
5. **Increase stepper** (PART_IncreaseButton): the same square as the decrease stepper, with a 16 px plus glyph.
6. **Shell**: the field frame, with a 3 px inset on every side (4 px at the large size). Its width is 164, 196 or 228 px
   for the small, medium and large sizes, unless `Width` is set; the medium shell is 44 px tall.
7. **Description and error rows**: the field's help text and `ErrorMessage` under the shell.

A stepper dims to 32% at a limit, to 60% while it refuses a press, and to 35% when the field is disabled. A stepper at a
limit stays pressable, so the press still shows the limit.

### Prefix, suffix and step

`Step` sets the increment, and its precision sets the default number of fraction digits: a step of `0.5` shows one
decimal, and a step of `0.01` shows two. `Prefix` and `Suffix` are text before and after the value. `SuffixFormatter`
is a function of the value, so the unit can follow it, such as singular and plural.

<Demo name="Price" />

```csharp
var seats = new NumberField
{
    Label = "Seats",
    Minimum = 1,
    Maximum = 10,
    Value = 3,
    SuffixFormatter = n => n == 1 ? " seat" : " seats"
};
```

`PrefixFormatter` works the same way for a prefix. Both are functions, so they are not available in XAML; set them in code
behind, as in the example above. A `Suffix` string is the static form.

A minimum below zero accepts the minus sign while the person types, so a range such as -20 to 40 needs no special
handling.

<Demo name="NegativeRange" />

### Formatting and culture

`Culture` sets the grouping and decimal separators, and it defaults to `en-US` rather than the current culture, so the
same value reads the same way on every machine. `UseGrouping` turns the group separators on or off. `MinimumFractionDigits`
and `MaximumFractionDigits` override the digits that the step gives. Use them when a value has a fixed display, such as
two decimals for a rate that is stepped by whole numbers.

### Sizes

`Size` sets the width and the stepper size: `Small` is 164 px wide with 28 px steppers, `Medium` (the default) is 196 px
with 34 px steppers, and `Large` is 228 px with 40 px steppers. An explicit `Width` stretches the shell to that width
instead of the size's width.

<Demo name="Sizes" />

Use one size for each form. `Small` fits a dense table or a toolbar, `Medium` is the default for settings and forms, and
`Large` suits a single prominent number on a page such as a quantity on a product page.

### Limit hint and errors

`IsLimitHintEnabled` (on by default) shows a note beside the label when a press meets a limit or a typed value passes one.
The default text is "Max 10 seats" or "Min 1 seat". `LimitHintFormatter` writes the note from the edge that was reached
and its limit.

<Demo name="LimitHint" />

An `ErrorMessage` marks the field invalid. Use it for a rule the range does not express, such as a quantity that must be
at least one when the order has items.

<Demo name="States" />

### Validation

Keep range rules in the field and business rules in the view model or the form. The table shows when each check runs.

| Rule | When it is checked | Where it shows |
|---|---|---|
| Above `Maximum` | While typing, the draft turns warning-tinted; a press at the limit strains and names the limit | Limit note and the live announcement ("10 seats, maximum") |
| Below `Minimum` | While typing, after a short wait (700 ms), so a person can type the first digit of a larger number | Warning tint on the shell, then the limit note |
| On the step grid | When the draft commits (Enter or leaving the field) | The value is snapped to the step grid and clamped to the limits |
| Business rules, such as at least one item | On submit, from the view model | `ErrorMessage`; clear it when the value changes |

Setting `Value` from code never shows the strain or the note. It updates the display quietly, so a loaded form does not
flash a warning.

### Binding and events

`Value` is two-way. `ValueChanged` reports the old value, the new value and the `ChangeSource`, which says whether the
value came from code, a stepper, a key, the wheel, a scrub or typing.

<Demo name="BindingAndEvents" />

```xml
<arc:NumberField Label="Items" Minimum="0" Maximum="50" Value="{Binding ItemCount, Mode=TwoWay}" />
```

```csharp
field.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.OldValue} -> {e.NewValue} ({e.ChangeSource})");
};
```

`StepUp` and `StepDown` move the value by one step from code. They commit an open draft first and make no announcement and
no strain, so a "Add one" button in the app does not flash the limit.

### Binding to a view model

Keep the value and its limits in the view model, and let the field enforce the limits it shows. Changes from the field
arrive as `Value` updates, so the view model needs no extra handler.

```csharp
public partial class SeatPlanViewModel : ObservableObject
{
    public const int MaxSeats = 10;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeatCost))]
    private decimal _seats = 3;

    [ObservableProperty]
    private decimal _pricePerSeat = 12.5m;

    public decimal SeatCost => Seats * PricePerSeat;

    [RelayCommand]
    private async Task SavePlanAsync()
    {
        await _plans.SaveAsync("plan_team_2026", (int)Seats);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:NumberField Label="Seats"
                     Value="{Binding Seats, Mode=TwoWay}"
                     Minimum="1"
                     Maximum="10"
                     Suffix=" seats"
                     Description="Up to 10 per team." />
    <arc:Button Content="Save plan" Command="{Binding SavePlanCommand}" />
</StackPanel>
```

- Keep `Minimum` and `Maximum` in XAML so the limits are visible in the markup, and bind only `Value`.
- Convert to an integer only at the boundary, because the field stores a `decimal` and a seat count is whole.

### Scrub label

With `IsScrubEnabled` on, drag the label sideways to change the value: one step for every 6 px of movement. Past a limit,
the value follows the pointer with resistance, and it settles at the limit when the pointer is released. Enable scrubbing
only where the label is a clear handle, such as a design panel. Elsewhere a stray drag on a label changes a value.

<Demo name="ScrubLabel" />

### Keyboard

Focus the field to use the keyboard. The keys are listed below the field.

<Demo name="Keyboard" />

### Example: seat plan form in an admin console

An administrator sets the seats for a team plan in an admin console. The seats field shows the limit in its description
and in the limit note once the plus stepper reaches the maximum. The price per seat uses the same step rules with a
prefix and two decimals, so the price stays readable. The support hours field holds 0, which the business rule
rejects, so it carries an `ErrorMessage` that states the rule. The primary action names what happens next.

![A seat plan form with a seat count at its maximum, a price per seat and a quantity error](images/SeatPlanForm.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <StackPanel Width="620" Spacing="20">
        <TextBlock Text="Team plan: Acme Analytics"
                   FontSize="14"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:NumberField Label="Seats"
                         Value="10"
                         Minimum="1"
                         Maximum="10"
                         Suffix=" seats"
                         Width="360"
                         HorizontalAlignment="Left"
                         Description="Up to 10 per team. Changes apply from the next invoice." />
        <arc:NumberField Label="Price per seat"
                         Value="12.5"
                         Prefix="$"
                         Step="0.5"
                         MinimumFractionDigits="2"
                         Minimum="0"
                         Maximum="999"
                         Width="360"
                         HorizontalAlignment="Left"
                         Description="Billed monthly in USD." />
        <arc:NumberField Label="Support hours"
                         Value="0"
                         Minimum="0"
                         Maximum="40"
                         Suffix=" hours"
                         Width="360"
                         HorizontalAlignment="Left"
                         ErrorMessage="Add at least 1 support hour, or turn support off." />
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Discard" Variant="Secondary" />
            <arc:Button Content="Save plan" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

The three fields share one width, so the values line up in a column. The limit note stays hidden until a press reaches the
limit, which keeps the screen calm until it has something to say.

### Content guidelines

- **Label**: a noun for the quantity or setting: "Seats", "Retry attempts", "Timeout". The label stays visible; the
  stepper names include it, so keep it short.
- **Suffix**: name the unit in the singular and plural with `SuffixFormatter` when the unit changes with the number:
  " seat" and " seats". Use a leading space in the suffix so the digits and the unit do not touch.
- **Description**: state the limit or the consequence: "Up to 10 per team", "Changes apply from the next invoice".
  Write numbers as digits.
- **Limit hint**: name the limit the person reached in the terms of the field: "Max 10 seats", "Min 1 seat". Keep it to
  one short phrase.
- **Errors**: say what to enter, not what is wrong: "Add at least 1 support hour, or turn support off."

| Instead of | Write |
|---|---|
| Invalid value | Enter a number from 1 to 10. |
| Quantity | Items in this order |
| Max reached! | Max 10 seats |
| Seats: | Seats |

### Best practices

**Do**

- Set `Minimum` and `Maximum` from the business rule, so the stepper stops where the rule stops and the limit note explains
  why.
- Set `Step` to the smallest change a person should make: `0.5` for a half-hour, `0.01` for cents. The step also sets the
  decimals, so the display never shows a value the step cannot produce.
- Use `StepUp` and `StepDown` for an "add one" button, because they do not flash the limit when the button is used at it.
- Keep the value in the view model as a `decimal`, and round only at the boundary with an integer or a payment API.
- Leave `IsScrubEnabled` off in forms, so a drag on a label never changes a value by accident.

**Avoid**

- Using the field for an identifier or a code, where the stepper and the formatting change the characters.
- Storing a number the field would refuse, such as a seat count above `Maximum`. The form then saves a value the screen
  never showed as valid.
- Showing an `ErrorMessage` while the person is still typing a valid number. Set it on submit, or after the field loses
  focus.
- Using a very small `Step` on a large range. A step of 0.01 across 0 to 999 needs many presses, so a text entry with
  `MoneyInput` or `Input` is faster.
- Hiding the limit note with `IsLimitHintEnabled="False"` when the limit is not obvious from the context. Without the note,
  a stopped stepper looks broken.

### Common questions

**Why does the plus stepper look dimmed?** The value is at the maximum. The stepper dims to 32% but stays pressable, so a
press still shows the limit note and the strain.

**How do I show a unit that changes with the number?** Set `SuffixFormatter` in code: `n => n == 1 ? " seat" : " seats"`.
It is a function, so it is not a XAML attribute.

**Why does a typed number snap to the step?** The draft is committed on Enter or when the field loses focus, and the commit
snaps the value to the step grid that starts at `Minimum`, then clamps it to the limits.

**How do I know the value changed because of a step and not typing?** Read `ChangeSource` in `ValueChanged`. `Key` is an
arrow or page key, `Button` is a stepper, `Wheel` is the mouse wheel while focused, `Scrub` is the label drag, `Type` is
typing and `Programmatic` is code.

**Can I use the mouse wheel?** Yes, while the field has keyboard focus, each wheel notch steps the value. This is an
extension of the Arc design, so an application that does not want it can avoid focusing the field.

## API reference

<!-- api: NumberField -->

The change source and the limit edge are public enums that the events and the limit note use.

| Type | Members |
|---|---|
| `NumberFieldValueChangedEventArgs` | `OldValue`, `NewValue` (decimal), `ChangeSource` (`NumberFieldChangeSource`) |
| `NumberFieldChangeSource` | `Programmatic`, `Button`, `Key`, `Wheel`, `Scrub`, `Type` |
| `NumberFieldLimitEdge` | `Minimum`, `Maximum` |

## Accessibility

- `Label` names the field. With `IsLabelVisible="False"` the label is hidden but still names it.
- The field is a spin button with a range: its minimum, maximum, small change (1) and large change (10) are exposed, and
  its value text is the value with its prefix and suffix, such as "3 seats".
- The steppers are named "Decrease Seats" and "Increase Seats" from `DecreaseText`, `IncreaseText` and the label. They stay
  focusable at a limit, so a screen reader reaches the limit note.
- Changes are announced politely. A clamp announces the limit it reached ("10 seats, maximum"), and a typed value that
  is out of range is added to the help text.
- The focus ring appears for keyboard focus only. A press with the pointer does not draw it.

### Keyboard

| Keys | Action |
|---|---|
| `Up` / `Down` | Steps by `Step`; holding the key repeats the step, starting after 400 ms and speeding up |
| `Shift` + `Up` / `Down`, `PageUp` / `PageDown` | Steps by `LargeStep` (ten steps by default) |
| `Home` / `End` | Moves to `Minimum` / `Maximum` when no draft is open (`End` needs a finite maximum) |
| Digits, `-` (when `Minimum` is below zero), the decimal separator | Type a draft; a valid draft applies live |
| `Enter` | Commits an open draft (snapped and clamped); with no draft, selects the whole number |
| `Escape` | Reverts an open draft to the value before editing |
| `Enter` on a focused stepper | Takes one step |
| `Tab` | Moves from the editor to the Decrease and Increase steppers, then on |

The mouse wheel steps the value while the field has keyboard focus. Keys pressed on a stepper belong to the stepper, not to
the draft.

## Motion

- The digits roll like an odometer in the direction of the change, and wrap from 9 to 0. A step that is interrupted by
  another one changes its target in the middle of the roll.
- A press at a limit stretches the value back, shakes the refused stepper and shows the limit note for about 1.5 seconds.
- Reduced motion sets the value at once and flashes the limit in the warning color. Off motion does the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Step with a stepper or an arrow key | Digits roll in the change direction | Value changes at once | Value changes at once |
| Press at a limit | The value stretches back, the stepper shakes, the limit note rises | The digits flash in the warning color | The digits flash in the warning color |
| Typed draft past a limit | The shell takes the warning tint | The shell takes the warning tint | The shell takes the warning tint |

## Related

- `Slider` when the position on a range matters more than the exact number.
- `Input` for digit strings that have no stepper.
- `MoneyInput` for currency amounts, and `ScrubInput` for dense property panels.

| Component | Choose it when |
|---|---|
| `Slider` | The person judges a position within a range and the exact figure matters less |
| `MoneyInput` | The number is an amount with a currency code and minor units |
| `ScrubInput` | Many numeric values in a dense panel are adjusted by dragging |
| `Input` | The value is an identifier or code, not a quantity |
| `Select` / `Combobox` | The value is one of a fixed list of options |
