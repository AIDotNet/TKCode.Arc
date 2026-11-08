---
title: Text filter box
summary: One text filter condition: a checkbox to switch it on, contains, starts with, ends with, equals or a regular expression, and case sensitivity.
---

<Demo name="Basic" />

## When to use

`TextFilterBox` is one row of an advanced filter for a text field: a checkbox that switches the condition on, the field
name, an operator shown as a symbol and the filter text. Use it where people search a list by one field with more
control than a search box gives:

- **Names and titles** in a product or document list: "Name ∋ Pro".
- **Files and paths**: "File name $ .axaml", "Path ^ src/".
- **Log lines and codes** that follow a pattern: "Message .* ^ERR\d+".

The box only holds the condition. It never filters data: your view model reads `IsActive`, `Operator`, `Text` and
`IsCaseSensitive` and filters in whatever way fits.

## When not to use

- **One search over everything**: use `SearchField`.
- **Picking from known values**: use `Select`, `MultiSelect` or `ChipGroup`.
- **Numbers**: use `NumberFilterBox`.

## Usage

`TextFilterBox` derives from `FilterBox`, an Avalonia `TemplatedControl`. Its parts are Arc controls: a `Checkbox` with
the label and description, a `Select` for the operator and an `Input` for the text, which takes the remaining width.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TextFilterBox Label="Product name"
                       Operator="Contains"
                       Text="Keyboard"
                       Placeholder="Type a product name"
                       MaxLength="80"
                       IsCaseSensitive="False" />
</UserControl>
```

`IsActive`, `Operator`, `Text` and `IsCaseSensitive` are two-way by default. The operators are `Contains` (∋, the
default), `NotContains` (∌), `Equal` (=), `NotEqual` (≠), `StartsWith` (^), `EndsWith` ($) and `Regex` (.*). An empty
text constrains nothing. `Clear()` empties the text and keeps the operator.

### Regular expressions

With `Operator="Regex"` the text is a .NET regular expression. A pattern that does not parse shows the parser's message
under the field, sets `IsPatternValid` to `False`, `PatternError` to the message and the `:invalid` pseudo-class; until it
is fixed the condition constrains nothing. Patterns run in the regular expression interpreter (nothing is compiled or
generated at run time, so it is safe for Native AOT), and a match that runs past `MatchTimeout` (200 ms) counts as no
match.

<Demo name="Regex" />

### Extra content

`ExtraContent` goes after the field, such as a "Match case" checkbox or a button. It is disabled with the filter and
takes no room when it is null.

<Demo name="ExtraContent" />

### Filtering in the view model

`Condition` is the current condition as a `TextFilterCondition`: build one from your own properties whenever they
change (not once per item, so a pattern is parsed once) and test each item with `Matches`. Comparisons are ordinal, with
or without case. `Matches` on the box returns true while the filter is off.

```csharp
var name = new TextFilterCondition(NameOperator, NameText, NameCaseSensitive);
var visible = products.Where(p => !NameActive || name.Matches(p.Name));
```

<Demo name="NumberFilterBox.Combined" />

### Lining up stacked filters

Give each box the same `LabelSizeGroup` and set `Grid.IsSharedSizeScope="True"` on their container: number and text
filters share one label column, so the operators line up. `LabelWidth` sets a fixed width instead.

<Demo name="Alignment" />

## API reference

<!-- api: TextFilterBox -->

<!-- api: FilterBox -->

<!-- api: TextFilterCondition -->

<!-- api: TextOperator -->

<!-- api: FilterOperators -->

## Accessibility

- The checkbox is named by the label and described by `Description`; Space toggles it.
- The operator field is named after the label and `OperatorText` ("Product name operator"), and its help text is the
  operator's name, such as "Starts with"; the same name shows as a tooltip.
- The text field is named by the label; an invalid pattern is its error message, read with the field.

### Keyboard

| Key | When | Action |
|---|---|---|
| Tab | Anywhere | Move between the checkbox, the operator, the text and the extra content |
| Space | Checkbox | Switch the filter on or off |
| Enter, Space, Up, Down | Operator | Open the menu; then move and pick as in `Select` |
| Typing | Text | Edit the filter text |

## Motion

- The parts move as their own controls do: the check draws itself, the operator menu drops from the field and the error
  message slides in under the field. The row itself does not animate.
- With reduced motion or motion off, the parts follow their own reduced behavior.

## Responsive behavior

- The box stretches to the width it is given. The 84 px operator keeps its width and the text field takes the rest, down
  to 160 px. A long label or description wraps to leave them that room.
- When even a 120 px label would not fit (below about 390 px), the label moves onto its own row above the fields, and a
  shared `LabelSizeGroup` no longer applies to that box. A fixed `LabelWidth` stacks the same way when it does not fit.
- `ExtraContent` takes its own width at the end of the row.
- All fields are at least 36 px tall (`Size="Small"`), and the checkbox keeps its 44 px hit area at every size.

## Related

- `NumberFilterBox`: the same row for numbers, with = ≠ < ≤ > ≥ and a tolerance.
- `SearchField`: one search box for a whole list.
- `FilterToolbar`: chips for a handful of quick facets above a list.
