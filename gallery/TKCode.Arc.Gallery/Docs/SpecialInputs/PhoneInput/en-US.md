---
title: Phone input
summary: A phone field with a country picker, formatting as you type, and E.164 output.
---

<Demo name="Default" />

## When to use

`PhoneInput` collects a telephone number together with the country it belongs to. It sits in a form beside the other
fields of the flow, and it handles the parts a plain text field gets wrong: the country code, the national format, the
trunk prefix people type at home and the E.164 value that the server stores. The person picks a country once, types
the national number, and the component keeps the stored value in one canonical form.

- **Sign-up and account creation in a SaaS product**: a mobile number that receives a sign-in code. The number must be
  complete before the request leaves the page, so the field validates on blur and shows the expected digit count.
- **Checkout and delivery details in e-commerce**: a courier needs a reachable number for the parcel. Use `Country`
  with the shipping country preselected, and `PreferredCountries` for the markets the store ships to.
- **Contact forms in CRM and support tools**: a customer record holds one `+` number per contact. Bind `Value` to the
  record and store the E.164 string unchanged, so every later dialer and SMS integration reads the same value.
- **Account settings and billing profiles in fintech**: a number that the bank or payment provider sends alerts to.
  `Countries` can limit the picker to the markets where the product is licensed.
- **Bulk import screens in internal tools**: `PhoneNumbers.Parse` reads numbers written as "+44 (0)7911 123456" or
  "0044 7911..." before they are shown in the field, so a support agent can correct one row without retyping it.

## When not to use

- **Free text that is never dialed**: use `Input`. A number stored as a label, a reference or an order note does not
  need a country picker.
- **Verification codes**: use a one-time code input in your own UI. A code is not a phone number, and this library does
  not ship an OTP input.
- **Carrier, line type or reachability checks**: use a number metadata or messaging service. `PhoneInput` checks the
  length and the shape of a number against its built-in table of 49 countries, not whether the line is live.
- **A fixed list of values with no number semantics**: use `Select` or `Combobox` when the person chooses from known
  options, such as a support queue or a plan type.
- **A number that is only displayed**: use `Text` or a `Field` with `IsReadOnly` content for a value on a detail page.
  Call `PhoneNumbers.Format` to show the E.164 string in a list or table without an editor.
- **Amounts of money or counts**: use `MoneyInput` or `NumberField`. A phone number is an identifier, so it is text
  with a format, not a quantity.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PhoneInput Label="Phone number"
                    Country="US"
                    Description="We text a sign-in code to this number." />
</UserControl>
```

`PhoneInput` derives from `FieldTextBox`, which derives from Avalonia's `TextBox`, so the number is an ordinary text
editor: caret, selection, IME, undo and the clipboard work as they do in a text box. The component adds the country
button, the formatting, the E.164 `Value`, the country list and the message rows. `Label`, `Description` and
`ErrorMessage` are the same properties `Input` uses.

### Anatomy

1. **Label** (`Label`): the field's name, shown above the frame. It is also the accessible name of the number. Hide it
   with `IsLabelVisible="False"` only when a visible heading already names the field.
2. **Field frame**: the Arc frame of the field. In the default theme it is the medium control height (44 px), with
   the control radius and the surface color. Hover darkens the border, focus turns it to the foreground color, and an
   invalid field turns it to the danger color. Nothing changes size.
3. **Country button**: a 36 px high button inset 3 px in the frame. It shows the flag and the
   calling code (such as "+1") with 9 px before the flag, and reserves 33 px on the right for the chevron. The chevron
   sits 12 px from the right edge of the anchor, and a divider inset 10 px top and bottom separates the trigger from the
   number.
4. **Number** (the text editor, `PhoneInput` itself): the digits as they are typed, formatted for the selected country.
   The number has 12 px of padding on the left and 36 px on the right, which keeps room for the valid check.
5. **Typing guide**: the rest of the expected number, drawn in the muted text color at 72 percent
   opacity after the typed digits. It is a hint only; it is not part of the value.
6. **Valid check**: an 18 px slot 12 px from the right edge, shown when the number has a valid
   length for the country.
7. **Description row** (`Description`): helper copy under the frame. It shows only while there is no message.
8. **Error row** (`ErrorMessage` or the built-in validation message): copy in the danger color under the frame. A
   value in `ErrorMessage` replaces the built-in message.

### Preset value

`Value` takes an E.164 string such as `+447400123456`. The country is read from the calling code, and the field shows
the national number in that country's format. A value set in code does not announce itself.

<Demo name="PresetValue" />

### Restricted countries

`Countries` limits the picker to the listed ISO 3166-1 alpha-2 codes. Use it when the product or the store operates in
a fixed set of markets: a Nordic bank that onboards Norway, Sweden and Denmark, or a shipping form that accepts the
countries on its delivery map. `PreferredCountries` lists the countries that appear in the Suggested group above the
full list. A preferred country that is not in `Countries` is dropped, so a restricted list never offers a country the
product does not serve. An empty `PreferredCountries` shows one list sorted by name.

<Demo name="RestrictedCountries" />

<Demo name="NoPreferredCountries" />

Assign a new list to change either property. An existing list that is changed in place does not refresh the picker,
so replace the list instead.

### Validation and errors

Validation runs after the number loses focus, and only when the person entered digits. A field that was never typed in
stays quiet, which lets an optional number stay empty. An incomplete or too long number then shows the built-in message
in the error row, and the frame turns to the danger color. A complete number shows the valid check.

| Situation | When it is checked | Message |
|---|---|---|
| Digits entered, count shorter than the country's valid lengths | When the number loses focus | Built-in: "Numbers in United States have 10 digits" |
| Digits entered, count longer than the longest valid length | When the number loses focus | The same built-in message, with the country's lengths |
| Complete number of a valid length | Never shown as an error; the valid check appears | None |
| `ErrorMessage` has text | Immediately | The `ErrorMessage` text replaces the built-in message |
| `IsValidationEnabled` is `False` | Never | None; only `ErrorMessage` shows |

`IsValidationEnabled="False"` turns the built-in check off, so only `ErrorMessage` shows. Use it when the server
validates the number after submit, or on a profile page where a partial number is allowed. `ValidationTextFormat` changes the built-in copy; its `{0}` is the country name and its `{1}` the valid
lengths, such as "10" or "10 or 11" for countries with two lengths.

<Demo name="ValidationOff" />

<Demo name="DescriptionAndError" />

`ValidationMessage` reports the built-in message currently shown, or `null`. Read it when the form needs the message as
text, for example in a summary at the top of a long form. `Status` gives the same answer as a value: `Empty`,
`Incomplete`, `Valid` or `TooLong`.

### Storing and checking numbers

Store the `Value` string. It is E.164: a plus sign, the calling code and the national digits without the trunk prefix,
such as `+14155550132`. The `PhoneNumbers` helpers work on the same data as the picker, so a form, an importer and a
list can share one set of rules without a second table:

| Helper | Use it for |
|---|---|
| `PhoneNumbers.Format(e164)` | Showing a stored number in a table or a summary, such as "+44 7400 123456"; returns the input when it cannot be read |
| `PhoneNumbers.Parse(input)` | Reading a pasted or imported number into a country and national digits; returns `null` when the text does not start with "+" or "00" or the code is unknown |
| `PhoneNumbers.StatusOf(country, digits)` | Checking a national number on the server with the same lengths the field uses |
| `PhoneNumbers.ToE164(country, digits)` | Building the stored value from a country and the national digits |
| `PhoneNumbers.Find(iso)` | Getting the table entry for an ISO code, or `null` for an unknown code |

To check a stored record in code, pass `Value` to `PhoneNumbers.Parse`. It returns the `Country` and the `National`
digits for `StatusOf`:

```csharp
var parsed = PhoneNumbers.Parse(contact.Phone);
if (parsed is { } number)
{
    var status = PhoneNumbers.StatusOf(number.Country, number.National);
    // status is Valid, Incomplete or TooLong; store contact.Phone unchanged when it is Valid.
}
```

### Binding and events

`Value` and `Country` are two-way. `ValueChanged` is raised on every edit (typing, pasting, picking a country). It reports
the old and new E.164 values, the country, the formatted number, the status and `IsValid`. Use the event for live
feedback and the bindings for the stored value.

<Demo name="ValueChanged" />

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

`ValueChanged` also fires when `Value` is assigned in code. A handler that writes the value back should compare
`e.NewValue` with the stored value first, so it does not loop. `OpenPicker()` and `ClosePicker()` open and close the
country list from code; `IsPickerOpen` reports the state.

### Binding to a view model

In an MVVM application the view model owns the number, the country and the rules for saving it. The field shows the
values and writes the edits back. The example below uses CommunityToolkit.Mvvm and the `PhoneNumbers` helpers to check
the number before the save call.

```csharp
public partial class ContactViewModel : ObservableObject
{
    private readonly IContactService _contacts;

    public ContactViewModel(IContactService contacts)
    {
        _contacts = contacts;
    }

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _country = "US";

    [ObservableProperty]
    private string? _phoneError;

    [RelayCommand]
    private async Task SaveAsync()
    {
        PhoneError = null;
        var parsed = PhoneNumbers.Parse(Phone);
        if (parsed is not { } number ||
            PhoneNumbers.StatusOf(number.Country, number.National) != PhoneNumberStatus.Valid)
        {
            PhoneError = "Enter a complete phone number, such as +1 415 555 0132.";
            return;
        }

        await _contacts.SaveAsync(Phone);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:PhoneInput Label="Mobile number"
                    Value="{Binding Phone, Mode=TwoWay}"
                    Country="{Binding Country, Mode=TwoWay}"
                    ErrorMessage="{Binding PhoneError}" />
    <arc:Button Content="Save contact" Command="{Binding SaveCommand}" IsDefault="True" />
</StackPanel>
```

`Value` and `Country` are the two-way properties. `SaveCommand` is the command to bind to a submit button, and
`ErrorMessage` receives the server-side rule. Clear `PhoneError` when the number changes, so a stale message does not
stay on screen. `ValidationMessage`, `Status`, `SelectedCountry` and `IsPickerOpen` are read-only properties; bind them
one way when the form needs them.

### Disabled and hidden label

`IsEnabled="False"` fades the field and keeps the country list from opening. `IsLabelVisible="False"` hides the label,
which still names the number for assistive technology. Hide it only when a column header or a heading names the field.

<Demo name="States" />

### Keyboard

The country button sits before the number in the tab order. On the button, ArrowDown and ArrowUp open the list, and a
printable character opens a search seeded with that character. Inside the number, typing `+` opens the list with the
calling-code search, and Backspace or Delete on a separator deletes the digit beside it. The complete map is in the
Accessibility section.

<Demo name="Keyboard" />

### Example: checkout contact details

A contact step in a checkout flow. The mobile number is stored as E.164 and has a preselected country, because the
courier calls this number for delivery. The alternate number shows a message that replaces the built-in validation
with an instruction the person can act on. The email field and the buttons complete the step, and the primary action
names what happens next.

![Checkout contact details with a mobile number, an alternate number with an error and an email field](images/CheckoutContact.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:Input Label="Full name"
               Text="Maya Chen" />
    <arc:PhoneInput Label="Mobile number"
                    Value="+14155550132"
                    Description="The courier calls this number on delivery day." />
    <arc:PhoneInput Label="Alternate number"
                    Country="GB"
                    Value="+447911"
                    ErrorMessage="Enter the full number, such as 07911 123456." />
    <arc:Input Label="Delivery email"
               Text="maya.chen@northwind.com"
               Description="Order updates go to this address." />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Back" Variant="Secondary" />
        <arc:Button Content="Continue to payment" />
    </StackPanel>
</StackPanel>
```

### Content guidelines

- **Label**: name the number by its purpose, in sentence case: "Mobile number", "Delivery phone", "Alternate number".
  Use "Phone number" only when the form has no other number. The label stays visible while the person types.
- **Description**: one sentence that says what the number is used for: "We text a sign-in code to this number." Keep it
  to the consequence, so the person understands why the field is asked for.
- **Error**: say what is missing and show an example in the national format: "Enter the full number, such as
  07911 123456." Do not repeat the validation text as a rule ("Invalid number").
- **Country names**: the picker uses the English names from the built-in table. Localize the picker's text with
  `SearchPlaceholder`, `SearchLabel`, `ListLabel`, `SuggestedLabel` and `AllCountriesLabel`.
- **Placeholder and examples**: the placeholder and the typing guide come from the country's example number, so set
  `Country` to the market the form serves and let the field show its national shape.

| Instead of | Write |
|---|---|
| Phone | Mobile number |
| Invalid phone number. | Enter the full number, such as 07911 123456. |
| Phone number is required. | Enter a mobile number so the courier can reach you. |
| Wrong number | Numbers in United Kingdom have 10 digits. (the built-in message; keep it) |

### Best practices

**Do**

- Store the `Value` string unchanged. E.164 is the only form the rest of the system needs, and it is what the
  integrations for SMS, calls and CRM sync expect.
- Set `Country` from the user's shipping, billing or account country before the person types. The picker then
  starts where the person expects, and the number needs no prefix.
- Limit `Countries` to the markets the product serves. The picker then never offers a country that the order or the
  account cannot use.
- Validate the number again on the server with `PhoneNumbers.StatusOf`. The client check is a convenience; the server
  must not trust the field alone.
- Keep the built-in validation on for sign-up and checkout, where a short number fails silently later otherwise.

**Avoid**

- Storing the national format or the typed text. A number saved as "07911 123456" cannot be dialed without the country
  it belongs to, and each consumer would have to guess that country.
- Using `Input` for phone numbers because the picker looks like extra work. A plain text field cannot tell "+44" from
  a trunk zero, and it cannot hold the country the number belongs to.
- Relying on the flag emoji for meaning. On Windows the flag shows as two letters, so the country name and the calling
  code are the reliable cue.
- Showing the validation message before the person leaves the field. The component waits for blur on purpose, so a
  number that is still being typed does not flash an error.
- Mutating `Countries` or `PreferredCountries` in place. Assign a new list, because the picker rebuilds from the
  property assignment.

### Common questions

**The number I pasted picked a different country. Is that a bug?** No. A pasted or typed international number ("+44 ...",
"+33 ...") sets the country from its calling code, and the picker announces "Country set to ...". `+1` picks Canada
when the area code belongs to Canada.

**How do I know whether the number is complete?** Read `Status` for the current answer, or handle `ValueChanged` and read
`IsValid`. `Status` is `Incomplete` for fewer digits than a valid length, `TooLong` for more digits than the longest
valid length, and `Valid` for a valid length. `Empty` means there are no national digits at all.

**Why does the field show no error for a number I just typed partially?** Validation runs only after the number loses
focus, and only when digits were entered. Leave the field to see the message; a number that was set in code (through
`Value`) is not checked until the person edits it and leaves it.

**Can I show the built-in message in another language?** Yes. Set `ValidationTextFormat` with `{0}` for the country
name and `{1}` for the valid lengths, and set `NoMatchesTextFormat`, `SearchLabel`, `SearchPlaceholder` and the other
text properties to your strings. The country names themselves come from the built-in table.

## API reference

<!-- api: PhoneInput -->

Companion types. `PhoneNumberStatus` is the value of `Status` and the `Status` of the event arguments:

| Member | Meaning |
|---|---|
| `Empty` | No national digits (a typed trunk prefix alone is still empty) |
| `Incomplete` | Fewer digits than a valid length, or a length between two valid ones |
| `Valid` | A valid length for the country |
| `TooLong` | More digits than the longest valid length |

`PhoneInputValueChangedEventArgs` is the argument of `ValueChanged`:

| Member | Meaning |
|---|---|
| `OldValue` | The previous E.164 value, `""` when there were no digits |
| `NewValue` | The new E.164 value, such as `+14155550132`, `""` when there are no digits |
| `Country` | The selected `PhoneCountry` |
| `Formatted` | The number as shown in the field, without the calling code; a typed trunk prefix is kept |
| `Status` | The `PhoneNumberStatus` of the number |
| `IsValid` | `True` when `Status` is `Valid` |

`PhoneNumbers` is the static helper class behind the field. It is public so that forms and imports can use the same
rules:

| Member | Meaning |
|---|---|
| `Countries` | The 49 countries of the built-in table, in table order |
| `Find(iso)` | The `PhoneCountry` for an ISO code (case-insensitive), or `null` |
| `Flag(iso)` | The flag as regional indicator letters; Taiwan, Hong Kong and Macao return the flag of China |
| `FormatNational(country, digits)` | Formats national digits as typed, with separators that print only while digits follow |
| `StatusOf(country, digits)` | The `PhoneNumberStatus` of national digits (a trunk prefix is allowed) |
| `ToE164(country, digits)` | The E.164 form, or `""` without digits |
| `Parse(input, pool)` | Reads an international number into a `PhoneNumberParseResult` (`Country`, `National`), or `null` |
| `Format(e164)` | Formats an E.164 number for display, such as "+44 7400 123456" |

## Accessibility

- `Label` names the field. With `IsLabelVisible="False"` the label is hidden but still names it.
- `IsRequired="True"` marks the number as required for assistive technology and for forms.
- The country button is named "Country, <name> +<code>", for example "Country, United States +1". The search box is a
  combo box that reports the active country, and the list has Suggested and All countries groups.
- ArrowDown and ArrowUp open the picker on the country button. Inside the search, arrows, PageUp and PageDown move,
  Home and End jump to the first and last item, Enter selects and Escape returns focus to the button.
- A country change caused by a paste or a picker choice is announced politely, as "Country set to <name>" or
  "<name>, +<code>". The number of results in a search is announced as "<n> countries" or "No matches".
- A number error appears after blur as help text. A valid number is announced as "Valid <country> number".
- The description and the error are the field's help text, read after the name and the value.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | From outside, moves to the country button; from the button, moves to the number; from the number, leaves the field |
| `Shift+Tab` | Moves backward: from the number to the country button, and from the button to the previous control |
| `ArrowDown` / `ArrowUp` on the country button | Opens the country list |
| A printable character on the country button | Opens the list with that character as the search |
| `Backspace` / `Delete` on the country button | No effect on the number |
| `+` typed in the number | Opens the list with a calling-code search, such as "+44" finding United Kingdom |
| `Backspace` / `Delete` on a separator in the number | Deletes the digit beside the separator |
| `ArrowDown` / `ArrowUp` in the search | Moves the active country by one row |
| `PageDown` / `PageUp` in the search | Moves by 8 rows |
| `Home` / `End` in the search | Moves to the first or last row, while the search is empty |
| `Enter` in the search | Picks the active country, keeps the digits within the new country's lengths and returns to the number |
| `Escape` in the search | Closes the list and returns focus to the country button |
| `Tab` / `Shift+Tab` in the search | Closes the list and moves to the number, or back to the country button |
| Pointer press outside the list | Closes the list without changing the country |

## Motion

- The field's surface grows from the country button into the country list: its width, height and corner radius move
  together on a spring. The list is at most 340 px wide, and the corner radius moves from 17 px to 22 px.
- The flag and the calling code roll to the new country in the direction of the list. The highlight glides between
  rows.
- A message row opens its height smoothly, and its text rises in with a soft blur. A valid check pops in.
- Reduced motion jumps the surface to its final size and swaps the content with short fades. Off motion switches at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Country list opens | The surface grows from the trigger with a spring (0.4 s, slight bounce) | Surface jumps to its final size; content fades | Immediate |
| Country list closes | The surface shrinks back on a shorter spring (0.32 s, no bounce) | Surface jumps to the closed size; content fades | Immediate |
| Country changes | The flag and calling code roll in the list direction (0.2 s fade and slide) | Short fade | Immediate swap |
| Highlight moves between rows | The highlight glides (0.28 s, slight bounce) | Short fade | Immediate |
| Message row appears or changes | Height opens smoothly; the new text rises from below with a soft blur | Full height with a short fade | Immediate |
| Valid check appears | Pops in on a snappy spring | Short fade | Immediate |
| Hover, focus, invalid | The border color crossfades | The border color crossfades | Immediate |

## Related

- `Input` for text that is not a phone number, and `Textarea` for several lines.
- `Select` for a fixed list of values, and `Field` or `FieldFrame` to build a similar labeled field around your own editor.
- `MoneyInput` and `NumberField` for numeric values with their own formatting rules.
- `PasswordField` and `SearchField` for secrets and search text.

| Component | Choose it when |
|---|---|
| `Input` | The value is free text, an identifier or a URL and has no country or calling code |
| `Select` / `Combobox` | The person chooses one value from a known list, and the list is not a set of countries |
| `NumberField` / `MoneyInput` | The value is a quantity or an amount that you format and round |
| `SearchField` | The text filters or searches content instead of being saved as a contact detail |
| `Field` + `FieldFrame` | You build your own editor and want the same label, frame and message rows |
