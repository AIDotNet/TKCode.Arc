---
title: Number filter box
summary: One number filter condition: a checkbox to switch it on, an operator shown as a symbol and a value, with an optional tolerance.
---

<Demo name="Basic" />

## When to use

`NumberFilterBox` is one row of an advanced filter: a checkbox that switches the condition on, the field name, an operator
shown as a symbol (= ≠ < ≤ > ≥) and the value. Use it where people build conditions on numeric fields of a list:

- **Prices, ratings and measures** in a product list: "Price ≤ 1000".
- **Counts** in a stock or log view: "Stock < 50", "Errors > 0".
- **Several conditions at once**, stacked with `TextFilterBox` in a filter panel, each switched on or off on its own.

The box only holds the condition. It never filters data: your view model reads `IsActive`, `Operator` and `Value` and
filters in whatever way fits (LINQ, a database query, a remote API, live or on a button).

## When not to use

- **A quick facet toggle** ("In stock", "On sale"): use `ChipGroup` or `FilterToolbar`.
- **A range with two ends**: use `Slider` with a range, or two boxes ("≥ 10" and "≤ 50").
- **A plain number in a form**: use `NumberField`.

## Usage

`NumberFilterBox` derives from `FilterBox`, an Avalonia `TemplatedControl`. Its parts are Arc controls: a `Checkbox`
with the label and description, a `Select` for the operator and a `NumberField` for the value. Bind the state two-way:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:NumberFilterBox Label="Price"
                         Description="Products at or above this price."
                         Operator="GreaterThanOrEqual"
                         Value="100"
                         NumberMode="Decimal"
                         DecimalPlaces="2"
                         Minimum="0"
                         Maximum="99999"
                         Tolerance="0.01" />
</UserControl>
```

`IsActive`, `Operator` and `Value` are two-way by default, so `{Binding PriceActive}` and the like write back without
`Mode=TwoWay`.

### Integer and decimal values

`NumberMode` is `Integer` (whole numbers) or `Decimal` (the default) with `DecimalPlaces` fraction digits (2 by default).
`SmallChange` is one step of the arrow keys and steppers, and the grid typed values snap to, counted from `Minimum`; by
default it is 1 for integers and one unit of the last decimal place for decimals. `LargeChange` is the step of Page Up,
Page Down and Shift with an arrow. `Minimum` is 0 by default: set it for negative values.

`Tolerance` is part of the condition, not of the field: two values at most `Tolerance` apart count as equal. It does not
change what can be typed.

<Demo name="Modes" />

### Switching a filter off

Unchecking the box (or setting `IsActive` to `False`) disables the operator, the value and `ExtraContent`, and keeps
them: checking it again brings the same condition back. The checkbox itself stays enabled, and the label toggles it too.

<Demo name="Active" />

### Choosing the operators

`Operators` limits the menu to a list, in your order. The symbols and their spoken names come from `FilterOperators`, a
plain lookup with no reflection, so it is safe for AOT and trimming. `FilterChanged` is raised after any change of the
condition.

<Demo name="Operators" />

### Filtering in the view model

`Condition` is the current condition as a `NumberFilterCondition` record: operator, value and tolerance. It has no UI,
so a view model can build one from its own properties and test each item with `Matches`. `Matches` on the box returns
true while the filter is off.

```csharp
var price = new NumberFilterCondition(PriceOperator, PriceValue, Tolerance: 0.01m);
var visible = products.Where(p => !PriceActive || price.Matches(p.Price));
```

The tolerance widens equality: `Equal` accepts values within it, `NotEqual` those outside it, `<` and `>` must clear it
and `≤` and `≥` may fall short by it. Values are `decimal`; cast a `double` with `(decimal)value`.

<Demo name="Combined" />

### Lining up stacked filters

Give each box the same `LabelSizeGroup` and set `Grid.IsSharedSizeScope="True"` on their container: the label column
takes the width of the longest label, so the operators line up. `LabelWidth` sets a fixed width instead. `Size` sets the
height of the operator and value fields (Small 36, Medium 44, Large 50).

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
            Grid.IsSharedSizeScope="True" Spacing="8">
    <arc:NumberFilterBox Label="Price" LabelSizeGroup="FilterLabel" />
    <arc:TextFilterBox Label="Product name" LabelSizeGroup="FilterLabel" />
</StackPanel>
```

## API reference

<!-- api: NumberFilterBox -->

<!-- api: NumberFilterCondition -->

<!-- api: NumberOperator -->

<!-- api: NumberFilterMode -->

## Accessibility

- The checkbox is named by the label and described by `Description`; Space toggles it.
- The operator field is named "Price operator" (the label and `OperatorText`) and its help text is the operator's name,
  such as "Greater than or equal to"; the same name shows as a tooltip.
- The value field is named by the label.

### Keyboard

| Key | When | Action |
|---|---|---|
| Tab | Anywhere | Move between the checkbox, the operator and the value (inactive parts are skipped) |
| Space | Checkbox | Switch the filter on or off |
| Enter, Space, Up, Down | Operator | Open the menu; then move and pick as in `Select` |
| A symbol key | Operator | Jump to the operator with that symbol |
| Up, Down | Value | One `SmallChange` |
| Page Up, Page Down | Value | One `LargeChange` |

## Motion

- The parts move as their own controls do: the check draws itself, the operator menu drops from the field and the value
  rolls its digits. The row itself does not animate.
- With reduced motion or motion off, the parts follow their own reduced behavior.

## Responsive behavior

- The operator is 84 px and the value 168 px. The label column is as wide as the label (or `LabelWidth`, or its size group),
  and a long label or description wraps to leave the fields their room.
- When even a 120 px label would not fit (below about 400 px), the label moves onto its own row above the fields, and a
  shared `LabelSizeGroup` no longer applies to that box. A fixed `LabelWidth` stacks the same way when it does not fit.
- All fields are at least 36 px tall (`Size="Small"`), and the checkbox keeps its 44 px hit area at every size.

## Related

- `TextFilterBox`: the same row for text, with contains, starts with, ends with, equals and regular expressions.
- `FilterToolbar`: chips for a handful of quick facets above a list.
- `NumberField`: a number in a form, without an operator.
