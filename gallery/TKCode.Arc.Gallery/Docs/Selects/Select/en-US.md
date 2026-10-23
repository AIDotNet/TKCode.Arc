---
title: Select
summary: A compact choice field with a keyboard friendly menu.
---

<Demo name="Field" />

## When to use

`Select` is the default field for one value from a short, fixed list: a currency, a billing cycle, a sort order, a
plan or a region. It sits in a form as a labelled field, keeps the chosen value visible in the trigger, and opens its
list from there, so the person can scan the options without leaving the form.

- **Billing cycle and currency in invoice settings**: a finance admin picks USD and an annual cycle for a workspace.
  The short list is fixed by the billing system, and the chosen value is saved with the invoice settings form.
- **Sort order in a table or list toolbar**: an incident list shows "Sort by: Newest first" in the toolbar, next to
  filter controls. The field is compact, and the list stays scannable.
- **Plan or tier on a subscription change form**: a team admin chooses Free, Pro or Team. The plan name is the value,
  and a disabled option can show a plan that the account is not allowed to choose.
- **Region or data residency in a workspace setup**: an onboarding wizard asks where data is stored. The field has a
  label and a description, so the person knows why the choice matters.
- **Priority or severity in a ticket editor**: a support agent sets P1 to P4 on a case. The options are few and always
  the same, so the field is faster to use than a free-text box.

## When not to use

- **Long lists or lists people search**, such as time zones, customer accounts or countries: use `Combobox`. The
  menu scrolls, but a long list is faster to find by typing.
- **Several values at once**: use `MultiSelect`, which keeps each picked value as a chip in the field.
- **Two to four options that should always be visible**: use `SegmentedControl`. Every option shows at once, so the
  person does not open a menu to compare them.
- **Each option needs a description or a price to decide**: use `RadioCards`. A Select row shows only its label, so the
  explanation has to sit beside the option.
- **Actions instead of a value** such as Edit, Duplicate or Delete: use `DropdownMenu`. Choosing an item there runs an
  action; choosing an option here stores a value.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Select Label="Region"
                Description="Where your workspace data is stored."
                Width="280">
        <arc:SelectItem Content="North America" Value="na" />
        <arc:SelectItem Content="Europe" Value="eu" />
        <arc:SelectItem Content="South America (soon)" Value="sa" IsEnabled="False" />
        <arc:SelectItem Content="Africa" Value="af" />
    </arc:Select>
</UserControl>
```

`Select` derives from Avalonia's `SelectingItemsControl` in single-selection mode, so it keeps the items model:
`ItemsSource`, `Items`, `SelectedIndex`, `SelectedItem`, `SelectedValue`, `SelectionChanged` and `IsEnabled`. Avalonia's
built-in text search is turned off for the control, because `Select` has its own typeahead (see "Keyboard"). The
Arc layer adds the label, the description, the trigger with its value roll and the menu with its highlight and check
mark. `SelectItem` children and `SelectOption` records both work, and a `SelectItem` without a `Value` uses its
`Content` as the value.

### Anatomy

1. **Label** (`Label`): 14 px, medium weight, above the trigger. It is also the field's accessible name, and it uses
   the same type size as the `Input` label.
2. **Trigger**: a box at least 44 px high (`ControlSizeMD`) with 12 px of padding on each side, a 1 px border and the
   control radius (18 px). It uses the surface color. Hover and open change the border to the strong border color and
   the background to the muted surface color. Keyboard focus draws a 2 px focus ring 4 px outside the trigger.
3. **Value** (`SelectedText`): the label of the selected option, shown with a roll when the choice changes. While
   nothing is selected, `Placeholder` shows in the muted text color.
4. **Chevron**: a 16 px down arrow after the value, with a 12 px gap. It rotates 180 degrees while the menu is open.
5. **Description** (`Description`): 12 px muted copy under the trigger. It is also the field's accessible help text.
6. **Menu**: a raised surface with a 1 px border, a 20 px corner radius and 6 px of padding. It opens 4 px below the
   trigger and is exactly as wide as the trigger.
7. **Option rows**: 36 px high with a 14 px corner radius. The label starts 11 px in and 34 px stay free on the right
   for the check mark, which sits 10 px from the right edge. The highlighted row uses the muted surface color, and
   disabled rows are drawn at 45% opacity.
8. **Scroll buttons**: 28 px bands at the top and bottom of the list that appear when the list has more rows above or
   below. Keeping the pointer on one scrolls the list.
9. **Menu height**: the list is capped at `MaxDropDownHeight` (320 px by default) and scrolls inside that height.

A disabled field fades to 50% opacity, shows the "not allowed" cursor and closes its menu if it was open.

### Bound options and value

Set `ItemsSource` to any objects or to `SelectOption` records. `SelectedValue` is two-way, so the bound property
receives the chosen value.

```xml
<arc:Select Label="Sort by"
            Width="220"
            ItemsSource="{Binding SortOrders}"
            SelectedValue="{Binding SortKey, Mode=TwoWay}" />
```

```csharp
var sort = new Select
{
    Label = "Sort by",
    Width = 220,
    ItemsSource = new[]
    {
        new SelectOption("newest", "Newest first"),
        new SelectOption("oldest", "Oldest first"),
        new SelectOption("az", "Name, A to Z"),
    },
    SelectedValue = "newest"
};
sort.SelectionChanged += (_, _) => Console.WriteLine(sort.SelectedValue);
```

`SelectedValue` is the value the form stores. `SelectedText` is the label of the selected option, read-only, and is
the right string for a summary line or a log message. `SelectedIndex` and `SelectedItem` select by position or by the
item object instead.

### Binding to a view model

In an MVVM app the view model owns the choice and the list of options, and the field shows them. With
CommunityToolkit.Mvvm the view model keeps the saved value as a plain string, and the field writes the chosen value
into it.

```csharp
public partial class InvoiceSettingsViewModel : ObservableObject
{
    private readonly IBillingSettings _settings;

    public InvoiceSettingsViewModel(IBillingSettings settings) => _settings = settings;

    public ObservableCollection<SelectOption> Currencies { get; } = new()
    {
        new SelectOption("usd", "US dollar (USD)"),
        new SelectOption("eur", "Euro (EUR)"),
        new SelectOption("gbp", "Pound sterling (GBP)"),
    };

    [ObservableProperty]
    private string _currency = "usd";

    [ObservableProperty]
    private string _billingCycle = "monthly";

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _settings.SaveAsync(Currency, BillingCycle);
    }
}
```

```xml
<StackPanel Width="360" Spacing="16">
    <arc:Select Label="Currency"
                ItemsSource="{Binding Currencies}"
                SelectedValue="{Binding Currency, Mode=TwoWay}" />
    <arc:Select Label="Billing cycle"
                SelectedValue="{Binding BillingCycle, Mode=TwoWay}">
        <arc:SelectItem Content="Monthly" Value="monthly" />
        <arc:SelectItem Content="Annual, billed up front" Value="annual" />
    </arc:Select>
    <arc:Button Content="Save settings" Command="{Binding SaveCommand}" />
</StackPanel>
```

- `SelectedValue` is two-way, so set `Mode=TwoWay` explicitly when the view model must receive the choice. It holds
  the option's value, not its label.
- `IsOpen` is two-way and opens or closes the menu from the view model. `SelectedText` is read-only, so bind it one
  way for display.
- Use `SelectionChanged` only for side effects such as analytics or a preview. The view model should read the bound
  value, so a change made from code and a change made by a person reach it the same way.
- Compiled bindings need `x:DataType` on the view (the examples here leave it out for brevity).

### Custom labels and values

For data items that are not `SelectOption`, set `TextSelector` and `ValueSelector`. They are AOT safe, unlike
`DisplayMemberBinding`. The label is what the person sees and the value is what the form stores.

<Demo name="Selectors" />

```csharp
public sealed record Plan(string Code, string Name, int MonthlyPrice);

var plans = new Select
{
    Label = "Plan",
    Width = 260,
    ItemsSource = new[]
    {
        new Plan("free", "Free", 0),
        new Plan("pro", "Pro", 24),
        new Plan("team", "Team", 48),
    },
    TextSelector = item => item is Plan p ? $"{p.Name} (${p.MonthlyPrice}/mo)" : null,
    ValueSelector = item => item is Plan p ? p.Code : null,
};
```

Without a `ValueSelector`, the value is the item itself, so `SelectedValue` returns the `Plan` object. Set a selector
whenever the form stores a code or an identifier rather than the object.

### Placeholder and description

`Placeholder` is shown while nothing is selected. Its default is "Select an option". Write a placeholder that tells
the person what to choose, such as "Choose a plan", and keep it short enough for the trigger. `Description` is a muted
hint under the trigger and is also the accessible help text.

```xml
<arc:Select Label="Plan" Placeholder="Choose a plan" Description="You can change it later." />
```

Use the description for the consequence of the choice ("Changes apply from the next invoice"), not for the list of
options. A placeholder disappears once a value is chosen, so it must never hold information the field needs after
that point.

### Long lists and disabled state

The menu is capped at `MaxDropDownHeight` (320 px by default) and scrolls. Only the visible rows are drawn, so a list
of thousands of items stays responsive. A disabled `Select` ignores input and does not open.

<Demo name="LongLists" />

<Demo name="Disabled" />

```xml
<arc:Select Label="Build number" ItemsSource="{Binding Builds}" MaxDropDownHeight="240" />
<arc:Select Label="Plan" IsEnabled="False" SelectedIndex="1">
    <arc:SelectItem Content="Free" />
    <arc:SelectItem Content="Pro" />
</arc:Select>
```

Lower `MaxDropDownHeight` when the field sits near the bottom of a window or in a narrow dialog, so the menu stays
inside the window. Avoid a height so small that only one row fits.

### Data shape and performance

Options can be strings, records, `SelectOption` values or inline `SelectItem` children. The control draws each visible
option itself and does not create a control per item, so the cost depends on the rows on screen, not on the size of
the list. Keep the item model small: a value, a label and the flags you need, such as a disabled state. Load large or
remote data into the view model and set `ItemsSource` once per load, rather than changing the collection on every
keystroke.

- Set `TextSelector` and `ValueSelector` once, at creation. Changing them rebuilds the option list.
- The value is what the form stores; keep it stable across releases, so a saved plan code still resolves after the
  label is renamed.
- Use `SelectedValue` for the stored value and `SelectedText` for the summary. Do not parse the label to get the
  value back.

### Focus and dismissal

Focus stays on the trigger while the menu is open, and the highlighted option is announced rather than focused.
Escape closes the menu without changing the value. Tab closes it and moves focus on as usual. Clicking outside the
menu closes it (light dismiss), and pressing the trigger again closes it, so the trigger never reopens the menu it
just closed. A mouse opens the menu on press, so press, drag and release picks an option in one gesture; touch opens
it on release. Picking an option closes the menu and returns focus to the trigger.

When the field becomes disabled or leaves the window, an open menu closes itself. Set `IsOpen` to `False` from code
to close the menu after a save, or call `Close()`; call `Open()` to open the menu with the selected option
highlighted.

### Example: invoice settings

A billing settings form in a SaaS admin console. Both fields are short fixed lists with a label, and the selected
values are the ones the account actually uses: USD and a monthly cycle. The currency field has a value, so the
trigger shows the label and the chevron; the cycle field shows Monthly, and the annual option carries its billing rule in its label.
The helper text explains when the change applies, so the person knows the consequence before saving.

![An invoice settings form with a currency and a billing cycle selected, and an empty payment terms field](images/InvoiceSettingsForm.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:Select Label="Currency"
                Description="Invoices and payouts use this currency."
                SelectedIndex="0">
        <arc:SelectItem Content="US dollar (USD)" Value="usd" />
        <arc:SelectItem Content="Euro (EUR)" Value="eur" />
        <arc:SelectItem Content="Pound sterling (GBP)" Value="gbp" />
        <arc:SelectItem Content="Japanese yen (JPY)" Value="jpy" />
    </arc:Select>
    <arc:Select Label="Billing cycle"
                Description="Changes apply from the next invoice."
                SelectedIndex="0">
        <arc:SelectItem Content="Monthly" Value="monthly" />
        <arc:SelectItem Content="Annual, billed up front" Value="annual" />
        <arc:SelectItem Content="Quarterly" Value="quarterly" IsEnabled="False" />
    </arc:Select>
    <arc:Select Label="Payment terms"
                Placeholder="Choose payment terms"
                Description="Net 30 is the default for new customers.">
        <arc:SelectItem Content="Net 15" Value="net15" />
        <arc:SelectItem Content="Net 30" Value="net30" />
        <arc:SelectItem Content="Net 60" Value="net60" />
        <arc:SelectItem Content="Net 90 (needs approval)" Value="net90" IsEnabled="False" />
    </arc:Select>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Save settings" />
    </StackPanel>
</StackPanel>
```

The quarterly cycle and the 90-day terms are disabled, so the person can see that the options exist and that they
need a different plan or approval. The payment terms field has no value yet and shows its placeholder, which asks for
the choice before the form is saved.

### Content guidelines

- **Label**: a short noun in sentence case: "Currency", "Billing cycle", "Region". Do not repeat the question in the
  label ("Which currency?").
- **Placeholder**: an instruction in the form "Choose a plan" or "Select an option". It disappears after the first
  choice, so it must read correctly only while the field is empty.
- **Option labels**: the names people already see in the product, in sentence case. Include the unit or the code only
  when the list needs it: "US dollar (USD)", "Net 30".
- **Description**: one sentence about the consequence of the choice: "Changes apply from the next invoice."
- **Disabled option**: name the reason in the label or the description, for example "Net 90 (needs approval)".

| Instead of | Write |
|---|---|
| Select | Choose a plan |
| Which currency do you want? | Currency |
| SELECT A VALUE | Select an option |
| usd - US Dollar - default | US dollar (USD) |
| Option 3 | Quarterly |

### Best practices

**Do**

- Keep the list short enough to scan, about a dozen options. Move longer lists to `Combobox`, where people type to find
  the value.
- Give the field a label and, when the choice has a consequence, a description that states it.
- Store the value, not the label, so a rename does not change saved data.
- Preselect the most common option when there is one, so the form is complete without a choice in the common case.

**Avoid**

- Using `Select` for several values; the field holds one value and the menu closes after each pick.
- Hiding the menu options behind a placeholder such as "Select" that tells the person nothing about the choice.
- Disabling options to hide them. A disabled option is still shown, so people learn that it exists and why it is locked.
- Changing the list while the menu is open. The menu refills at once, so the rows change under a person who is
  reading them.
- Using a `Select` as a read-only display. A disabled field is dimmed; show plain text when the value must not look
  interactive.

### Common questions

**The field shows the placeholder after I set `SelectedValue`. Why?** No option has that value, so nothing is
selected and the placeholder shows. Check that the value matches the option's `Value`, or the value that
`ValueSelector` returns.

**Why does the highlight stop at the last option instead of wrapping?** `Select` stops at the first and last enabled
option, so a person can see where the list ends. `MultiSelect` wraps instead.

**How do I open or close the menu from code?** Call `Open()` or `Close()`, or set `IsOpen`. `Open()` highlights the
selected option, or the first enabled option when nothing is selected.

**Can I show a value that cannot be changed without making it look like a field?** `Select` has no read-only state.
Disable it when the choice is locked and say why, or show the value as plain text in a label when the form should not
suggest that it can change.

**Does typing change the value?** Yes. With the menu closed, typing a letter picks the next option whose text starts
with it, without opening the menu. With the menu open, typing highlights the match, and Enter picks it.

## API reference

<!-- api: Select -->

<!-- api: SelectItem -->

## Accessibility

- `Label` names the field and `Description` is its help text.
- The selected value is exposed as plain text for assistive technology. Animated copies are hidden.
- The field exposes whether its menu is expanded.
- As the highlight moves, the highlighted option is announced in a polite live region with its position in the list
  and the list length.
- The focus ring stays on the trigger while the menu is open. Hover and open only change the border and background.

### Keyboard

The keys below apply while the trigger has focus.

| Keys | Action |
|---|---|
| `Enter`, `Space`, `Down`, `Up` (menu closed) | Opens the menu with the selected option highlighted; the first enabled option when nothing is selected |
| `Down` / `Up` (menu open) | Moves the highlight to the next or previous enabled option; stops at the first and last option |
| `Home` / `End` (menu open) | Moves to the first or last enabled option |
| `PageDown` / `PageUp` (menu open) | Moves the highlight eight options at a time |
| `Enter` or `Space` (menu open) | Picks the highlighted option, closes the menu and returns focus to the trigger |
| `Escape` (menu open) | Closes the menu without a change |
| `Tab` (menu open) | Closes the menu and moves focus on |
| Letters (menu closed) | Picks the next option whose text starts with the typed letters |
| Letters (menu open) | Highlights the next option that matches the typed letters |

A pause of about one second starts a new typeahead search. Disabled options are skipped by the arrow, Home, End,
Page and typeahead keys.

## Motion

- The shown value rolls in the direction of the list: later options rise from below, earlier options fall from above.
- The menu drops from the trigger with a short scale and fade. Closing is instant.
- The highlight glides from one option to the next.
- Reduced motion cross-fades the value instantly and fades the menu in and out. Off switches the value and the menu
  without any transition.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The selected value changes | Rolls in from the direction of the list | Instant cross-fade | Immediate |
| The menu opens | Drops from the trigger with a slight scale and a 6 px offset | Fades in | Immediate |
| The menu closes | Immediate | Fades out | Immediate |
| The chevron turns | Rotates with a spring | Immediate | Immediate |
| Hover, open, focus | The border and background colors crossfade | Immediate | Immediate |

## Related

- `Combobox` when the list is long or the person should type to find the value.
- `MultiSelect` when several values are chosen at once.
- `MorphSelect` when the trigger should grow into the list and the options need icons or groups.
- `SegmentedControl` when two to four options should always be visible.

| Component | Choose it when |
|---|---|
| `Combobox` | The list is long, or people type to find the value |
| `MultiSelect` | Several values are picked and shown as chips |
| `MorphSelect` | The trigger should grow into the list, or the options need icons, meta text or groups |
| `SegmentedControl` | Two to four options should always be visible |
| `RadioCards` | Each option needs its own description before the person chooses |
| `DropdownMenu` | The menu runs actions instead of storing a value |
