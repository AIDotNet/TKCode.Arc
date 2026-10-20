---
title: Money input
summary: A currency field with live grouping, stable width, rolling digits, and minor-unit output.
---

## When to use

- Hand-typed amounts such as payments, tips, transfers and budgets.
- Currency that the user can change, such as on invoices or expense reports.
- Amounts that need the minor-unit value (cents, yen) for storage or payment APIs.

## When not to use

- Quantities and other non-currency numbers: use `NumberField`.
- Picking an amount from a range: use `Slider`.
- Dense property panels that drag values: use `ScrubInput`.
- Free text that only looks like a number, such as an account code: use `Input`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MoneyInput Label="Amount"
                    Currency="USD"
                    Minimum="5"
                    Maximum="1000"
                    Description="Arrow keys step by one unit." />
</UserControl>
```

### Currency and quick-add chips

`Currencies` sets the codes in the currency menu. With one code the code is shown as static text. `QuickAddAmounts`
sets the chips in major units. An empty list hides them.

```xml
<arc:MoneyInput Label="Price" Currency="JPY" Value="12000" />
<arc:MoneyInput Label="Budget" Currency="EUR" Value="2400" />
```

```csharp
var price = new MoneyInput
{
    Label = "Price",
    Currency = "JPY",
    Value = 12000,
    QuickAddAmounts = [1000, 5000, 10000]
};

var budget = new MoneyInput
{
    Label = "Budget",
    Currency = "EUR",
    Currencies = ["EUR"],
    Value = 2400
};

var tip = new MoneyInput { Label = "Tip", Value = 12.5m, QuickAddAmounts = [] };
```

Japanese yen has no minor unit, so the field shows no cents.

### Culture

`Culture` sets the grouping and the symbol placement. The default is `en-US`.

```csharp
var betrag = new MoneyInput
{
    Label = "Betrag",
    Culture = CultureInfo.GetCultureInfo("de-DE"),
    Currency = "EUR",
    Value = 1234.56m,
    Maximum = 10000
};
```

### Error and description

A non-empty `ErrorMessage` marks the field invalid and replaces the description. `Description` gives help text.

```xml
<arc:MoneyInput Label="Invoice total"
                Description="Charged when the order ships."
                ErrorMessage="Enter the amount on the invoice." />
```

### Binding and events

`Value` is two-way and uses major units, so `12.50` is `12.5`. `ValueChanged` reports the old and new value, the
currency, the minor units, the formatted string and the `ChangeSource`.

```xml
<arc:MoneyInput Label="Amount" Value="{Binding Amount, Mode=TwoWay}" Currency="{Binding Currency, Mode=TwoWay}" />
```

```csharp
amount.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.Formatted} ({e.Currency}), minor units {e.MinorUnits}, from {e.ChangeSource}");
};

amount.OpenCurrencyMenu();
```

## API reference

<!-- api: MoneyInput -->

## Accessibility

- `Label` names the field. With `IsLabelVisible="False"` the label is hidden but still names it.
- The field is a spin button. Its value is the major value and its value text is the full currency string.
- ArrowUp and ArrowDown step by one unit, Shift or PageUp and PageDown by ten steps, and Enter completes the decimals.
- The currency button opens a list with typeahead. Arrow keys move, Enter or Space selects and Escape returns to the
  button.
- The range, the hint and the error are announced and shown as help text.

## Motion

- Digits roll in the direction of the change, and added columns grow in width. Typed digits land at once.
- A refused change pushes the amount sideways and flashes the range hint. Changing the currency rolls the symbol.
- Reduced motion removes the rolling, the push and the width spring: digits fade and sizes jump. Off motion does the same.

## Related

- `NumberField` for quantities and other numbers that have no currency.
- `ScrubInput` for dragged numeric settings, and `Slider` for picking from a range.
- `Input` for free text that has no amount structure.
