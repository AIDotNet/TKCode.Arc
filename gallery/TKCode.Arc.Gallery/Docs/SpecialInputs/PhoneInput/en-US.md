---
title: Phone input
summary: A phone field with a country picker, formatting as you type, and E.164 output.
---

## When to use

- Sign-up, checkout and contact forms that collect a phone number.
- Numbers that receive a text message or a call, where the country matters.
- Forms that store the number in E.164 form (`+14155550132`).

## When not to use

- Free text that is never dialed: use `Input`.
- Verification codes: use a one-time code input in your own UI. It is not part of this library.
- Carrier or number-type validation: use a full number metadata service. `PhoneInput` checks length and shape only.
- Picking from a long fixed list of values only: use `Select`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PhoneInput Label="Phone number"
                    Country="US"
                    Description="We text a sign-in code to this number." />
</UserControl>
```

### Preset value

`Value` takes an E.164 string. The country is picked from the number, and the field shows the formatted national
number.

```xml
<arc:PhoneInput Label="Mobile" Value="+447400123456" />
```

### Restricted countries

`Countries` limits the picker. `PreferredCountries` lists the countries that appear in the Suggested group above the
full list. An empty list shows one list sorted by name.

```csharp
var phone = new PhoneInput
{
    Label = "Phone number",
    Countries = new[] { "GB", "IE", "FR", "DE", "ES", "IT", "NL", "PT" },
    PreferredCountries = new[] { "GB", "IE" },
    Country = "GB"
};
```

### Validation and errors

Validation runs after the field loses focus. `IsValidationEnabled="False"` turns it off. An `ErrorMessage` replaces
the built-in message.

```xml
<arc:PhoneInput Label="Phone number"
                Value="+1201555"
                ErrorMessage="Enter the full number to get a code." />
<arc:PhoneInput Label="Phone number" IsValidationEnabled="False" />
```

### Binding and events

`Value` and `Country` are two-way. `ValueChanged` reports the E.164 value, the formatted number, the country and the
status on every edit.

```xml
<arc:PhoneInput Label="Phone number"
                Value="{Binding Phone, Mode=TwoWay}"
                Country="{Binding Country, Mode=TwoWay}" />
```

```csharp
phone.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.NewValue} ({e.Status}, valid: {e.IsValid})");
};

phone.OpenPicker();
```

## API reference

<!-- api: PhoneInput -->

## Accessibility

- `Label` names the field. With `IsLabelVisible="False"` the label is hidden but still names it.
- The country button is named "Country, <name> +<code>". The search box is a combo box, and the list has Suggested and
  All countries groups.
- ArrowDown and ArrowUp open the picker on the country button. Inside the search, arrows, PageUp and PageDown move,
  Home and End jump to the first and last item, Enter selects and Escape returns focus to the button.
- A country change caused by a paste or a picker choice is announced politely.
- A number error appears after blur as help text. A valid number is announced as "Valid <country> number".

## Motion

- The field's surface grows from the country button into the country list, with its corners and width moving together.
- The flag and dial code roll to the new country. The highlight glides between rows.
- A valid check appears with a small pop.
- Reduced motion jumps the surface to its size and swaps content with short fades. Off motion switches at once.

## Related

- `Input` for text that is not a phone number.
- `Select` for a fixed list of values, and `Field` or `FieldFrame` to build a similar labeled field.
- `MoneyInput` and `NumberField` for numeric values with their own formatting rules.
