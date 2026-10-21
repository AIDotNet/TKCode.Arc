---
title: Multi-select
summary: Select several values while keeping the field readable.
---

<Demo name="Chips" />

## When to use

`MultiSelect` lets a person pick several values from one fixed list without the field growing. It sits in forms and
filter bars where the answer is a set: the labels on a ticket, the owners of a project, the channels that receive an
alert. The picked values show as chips inside the field, so the person sees the choice without opening the menu.

- **Labels and tags on an issue or a ticket**: a support agent sets Bug, Billing and Priority on a case in the
  ticket editor. A fixed list keeps the labels consistent across the team, and the chips show the set at a glance.
- **Owner and team filters in a support queue or an order list**: a manager filters the queue by two or three
  agents or regions. The chips inside the filter bar show the active filters, and the `+N` chip shows the rest.
- **Notification channels in an alert rule**: an SRE chooses Email, Slack and PagerDuty for one rule. Each channel is
  a fixed option, and the rule form saves the picked values in the order they were chosen.
- **Environments or regions in a deploy or report scope**: a release manager picks staging and production for a
  pipeline. Each option can be disabled when the person cannot use it, and the field stays one line.
- **Large fixed lists with a search**: a sales lead picks several cities from a long list. `IsSearchable` adds a search
  field to the menu, so the list stays usable without a separate dialog.

## When not to use

- **Every option must stay visible while people toggle them**: use `ChipGroup`. Its chips are always on screen, so
  people compare the options and their states together.
- **Values that the person types and creates**: use `TagInput`. `MultiSelect` only picks from the list you give it.
- **Exactly one value**: use `Select` for a short list, or `Combobox` for a long one that people search.
- **Independent yes or no settings**: use one `Checkbox` or `Switch` per setting, so each setting has its own label and
  description.
- **Commands, not values**: use `DropdownMenu`. Choosing an item there runs an action; choosing an option here stores a
  value in the form.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MultiSelect Label="Labels"
                     Description="Shown on the issue card."
                     Width="320">
        <arc:SelectItem Content="Bug" Value="bug" />
        <arc:SelectItem Content="Feature" Value="feature" />
        <arc:SelectItem Content="Documentation" Value="docs" />
        <arc:SelectItem Content="Infrastructure" Value="infra" IsEnabled="False" />
    </arc:MultiSelect>
</UserControl>
```

`MultiSelect` derives from Avalonia's `SelectingItemsControl`, so it keeps the items model you already know:
`ItemsSource`, `SelectedItems`, `Selection`, `SelectionChanged`, keyboard focus and `IsEnabled`. Its selection mode is
`Multiple | Toggle` by default, so each pick adds or removes one value. The Arc layer adds the label, the description,
the chip row, the clear button and the menu. `SelectItem` children and `SelectOption` records both work; a
`SelectItem` without a `Value` uses its `Content` as the value.

### Anatomy

1. **Label** (`Label`): 14 px, medium weight, above the trigger. It is also the field's accessible name.
2. **Trigger**: a bordered box at least 44 px high (`ControlSizeMD`) with 12 px before the chips, 11 px after the
   chevron and 5 px above and below. It uses the control radius, the surface color and the subtle border color.
   Hover and open change only the border and background colors.
3. **Chip row** (`Placeholder` is replaced by it): chips show the picked labels in the order they were picked. Each
   chip is at most 144 px wide, has 7 px horizontal and 3 px vertical padding, uses the 12 px extra-small type size
   and sits 5 px after the previous chip. Chips use the muted surface color with a subtle border.
4. **Overflow chip**: a `+N` chip after the visible chips. It takes its count from the picks beyond
   `MaxVisibleChips` and uses the secondary text color.
5. **Placeholder** (`Placeholder`, default "Select options"): the muted text shown while nothing is selected. It
   fades out as soon as a value is picked.
6. **Chevron**: a 16 px down arrow. It rotates 180 degrees while the menu is open. Its left margin grows from 8 px to
   34 px while a value is picked, so the clear button beside it never overlaps the arrow.
7. **Clear button**: a 22 px round button with a 14 px cross, placed 36 px from the right edge of the trigger. It
   appears while at least one value is picked and the field is enabled, and it is named "Clear selections".
8. **Description** (`Description`): 12 px muted helper copy under the trigger. It is also the field's accessible help
   text.
9. **Menu**: a raised surface with a 1 px border, the panel radius and 5 px of padding. It opens 8 px below the
   trigger and is as wide as the trigger. Each option row is 38 px high with a 21 px corner radius and a check mark
   for each picked option. Disabled options are drawn at 42% opacity.

The field has no height limit for its menu by default. `MaxDropDownHeight` caps it when a long list would run off the
screen (see the data shape section).

### Chips and overflow

Picked values appear as chips in the field. `MaxVisibleChips` sets how many show before the rest fold into a `+N`
chip. The default is 2. Set it to 1 for a narrow filter bar, and to 0 to show only the count. When the row runs out
of room, the visible chips shrink with the remaining width, but the `+N` chip keeps its size, so the count never
disappears.

<Demo name="OneVisibleChip" />

```xml
<arc:MultiSelect Label="Owners"
                 MaxVisibleChips="1"
                 Width="280"
                 ItemsSource="{Binding Owners}" />
```

Choose the count by the width of the field, not by the length of the list. Each chip takes room for its label, up to
144 px. For long labels, such as "Infrastructure review", use one visible chip so the field stays on one line, and let
the menu show the full set.

### Bound options and values

Set `ItemsSource` to `SelectOption` records. `SelectedValues` lists the values in the order they were picked. It is
read-only, so read it in a handler or from code.

```csharp
var labels = new MultiSelect
{
    Label = "Labels",
    Width = 320,
    ItemsSource = new[]
    {
        new SelectOption("bug", "Bug"),
        new SelectOption("feature", "Feature"),
        new SelectOption("docs", "Documentation"),
        new SelectOption("infra", "Infrastructure", isDisabled: true),
    }
};
labels.SelectionChanged += (_, _) => Console.WriteLine(string.Join(", ", labels.SelectedValues));
```

`SelectedValues` is the list to store. The chips and `SelectedValues` both follow the pick order, not the order of
the options: if a person picks Documentation before Bug, the chips and the saved list start with Documentation.
Sort the values in your code when the saved order matters.

### Binding to a view model

In an MVVM app the view model owns the options and the picked items, and the field shows them. Bind `ItemsSource` to
the options and `SelectedItems` to an `ObservableCollection` in the view model. `MultiSelect` derives from Avalonia's
`SelectingItemsControl`, which keeps a bound `SelectedItems` list and the selection in step both ways: each pick or
chip removal adds or removes the option in the collection, and adding or removing an option in the collection from
code updates the chips.

```csharp
public partial class AlertRuleViewModel : ObservableObject
{
    private readonly IAlertRuleService _rules;

    public AlertRuleViewModel(IAlertRuleService rules) => _rules = rules;

    public ObservableCollection<SelectOption> Channels { get; } = new()
    {
        new SelectOption("email", "Email"),
        new SelectOption("slack", "Slack"),
        new SelectOption("pagerduty", "PagerDuty"),
        new SelectOption("sms", "SMS", isDisabled: true),
    };

    public ObservableCollection<SelectOption> SelectedChannels { get; } = new();

    [RelayCommand]
    private async Task SaveAsync()
    {
        var ids = SelectedChannels.Select(c => (string)c.Value!).ToArray();
        await _rules.SaveAsync("checkout-latency", ids);
    }
}
```

```xml
<StackPanel Width="320" Spacing="16">
    <arc:MultiSelect Label="Notify"
                     ItemsSource="{Binding Channels}"
                     SelectedItems="{Binding SelectedChannels}"
                     Placeholder="Pick channels"
                     Description="Channels that receive the first alert." />
    <arc:Button Content="Save rule" Command="{Binding SaveCommand}" IsDefault="True" />
</StackPanel>
```

- Bind `SelectedItems` with the default one-way mode: the control fills the collection you give it, so the view model
  keeps one collection for the lifetime of the form. Replacing the collection object resets the selection to its
  content.
- The collection follows the order of the picks as Avalonia's selection reports them; `SelectedValues` gives the
  values in the order the person picked them. Store `SelectedValues` when that order matters.
- `IsOpen` is two-way, so a binding can open and close the menu and the menu writes its state back. `SelectedValues`
  is read-only; read it, never assign it.
- `SelectionChanged` is raised after every pick and every removal, including keyboard picks; use it for side effects
  such as a live preview, not to copy the selection.
- Compiled bindings need `x:DataType` on the view (the examples here leave it out for brevity).

### Searchable menu

`IsSearchable` adds a search field at the top of the menu. Typing filters the options; press `Down` to highlight the
first match and `Enter` to pick it. `SearchPlaceholder` sets the hint in the search field, and `EmptyText` sets the
line shown when nothing matches.

<Demo name="Searchable" />

```xml
<arc:MultiSelect Label="Cities"
                 Placeholder="Pick cities"
                 SearchPlaceholder="Type a city"
                 IsSearchable="True"
                 Width="320"
                 ItemsSource="{Binding Cities}" />
```

The default match is a case-insensitive text search on each option's label, its `Meta` text and its `Keywords`. Add
`Keywords` for synonyms that people search for: an option labelled "Infrastructure" with the keyword "devops" appears
when someone types "devops". Set `FilterPredicate` when the match must follow your own rule, for example a prefix
match on a customer code. The predicate receives the option and the trimmed search text.

Searching is an addition to Arc, whose menu has no search field, so keep it for lists long enough that scrolling is
slower than typing. Opening the menu moves focus into the search field; closing the menu returns focus to the trigger
when the search field had it.

### Selecting in code

`Selection` is the selection model. `SelectAll()` and `UnselectAll()` are offered for code only, since Arc has no
select-all control.

```csharp
labels.Selection.Select(0);
labels.Selection.Select(1);
labels.UnselectAll();
```

Use `Selection.Select` and `Selection.Deselect` with the item index to change one pick at a time. `SelectAll()` picks
every enabled option, so it skips disabled options, and `UnselectAll()` clears the selection and the chips.

### Placeholder and disabled state

`Placeholder` is shown while nothing is selected. A disabled field ignores input, closes its menu if it was open, hides
the clear button and fades to 50% opacity with the "not allowed" cursor.

<Demo name="Disabled" />

```xml
<arc:MultiSelect Label="Reviewers" Placeholder="Select options" IsEnabled="False" />
```

Disable the field only while the value cannot change, and say why nearby, for example in the description: "Locked
after the release is approved." A disabled field still shows its chips, so people can read the current set.

### Data shape and performance

Options are `SelectItem` children or `SelectOption` records, and the control draws each visible option itself
(`SelectItem` is a data carrier, not a container), so the menu does not create a control per item. Keep the item
model small: a value, a label and the flags you need (`IsDisabled`, `Keywords`). Put the heavy data, such as
avatars or account details, in the view model and look it up when you save.

- The menu has no height limit by default and the screen bounds it. For a list of more than about a dozen options,
  set `MaxDropDownHeight` (for example 320) so the menu scrolls and stays on screen.
- Keep the labels short. A label longer than the chip width (144 px) is truncated with an ellipsis, so the full
  text is only in the menu.
- When the list changes while the menu is open, the menu refilters at once and keeps the picked values. Replace the
  whole collection when you load a new list, rather than editing it item by item.

### Example: alert rule form

An alert rule form in an observability console. The labels are short nouns, the channels are a fixed list, and the
helper text explains what the choice does ("Channels that receive the first alert"). The Notify field has one chip, so
the person sees a selected value and the clear button beside it. The Environments field is empty and shows the placeholder,
so it is clear that a choice is still needed. The chip for each picked channel shows its label, and the chevron tells
people the field opens.

![An alert rule form with one channel chip and an empty environment field](images/AlertRuleForm.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:Input Label="Rule name"
               Text="Checkout latency above 800 ms"
               Description="Shown in the incident title." />
    <arc:MultiSelect Label="Notify"
                     Description="Channels that receive the first alert."
                     Placeholder="Pick channels"
                     SelectedIndex="0">
        <arc:SelectItem Content="Email" Value="email" />
        <arc:SelectItem Content="Slack" Value="slack" />
        <arc:SelectItem Content="PagerDuty" Value="pagerduty" />
        <arc:SelectItem Content="SMS" Value="sms" IsEnabled="False" />
    </arc:MultiSelect>
    <arc:MultiSelect Label="Environments"
                     Description="Environments the rule watches."
                     Placeholder="Pick environments">
        <arc:SelectItem Content="Production" Value="prod" />
        <arc:SelectItem Content="Staging" Value="staging" />
        <arc:SelectItem Content="Sandbox" Value="sandbox" />
    </arc:MultiSelect>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Save rule" />
    </StackPanel>
</StackPanel>
```

The scene uses `SelectedIndex` for the picked channel because XAML cannot list several picks without names. In an
application, load the saved picks into `SelectedItems` as shown in the view model example.

### Content guidelines

- **Label**: a short plural noun in sentence case: "Labels", "Owners", "Channels". Use the singular noun for a
  single-value field.
- **Placeholder**: the instruction for an empty field, as a verb phrase: "Pick channels", "Select options". It
  disappears when the first value is picked, so it must not hold information the chips replace.
- **Option labels**: the names people already use in the product, in sentence case, with no counts or punctuation.
  "Billing", not "Billing (3)". The count belongs to the `+N` chip.
- **Description**: one sentence about what the choice does or where it appears: "Shown on the issue card."
- **Search placeholder**: an object to search for, not a command: "Type a city", not "Search for a city here".

| Instead of | Write |
|---|---|
| Select options... | Pick channels |
| Labels (choose as many as you want) | Labels |
| Selected: Bug, Feature, Docs, +1 more | The `+N` chip already shows the rest |
| Search | Type a city |
| Click here to add labels | Pick labels |

### Best practices

**Do**

- Give every field a visible label, because the chips alone do not say what the values mean.
- Store the values (`SelectedValues`) and show the labels, so renaming an option does not change saved data.
- Set `MaxVisibleChips` from the field width so the chips fit on one line.
- Validate the required set on submit and point to the field, because the field has no error row of its own.

**Avoid**

- Using `MultiSelect` for values people type. Free text belongs in `TagInput`, where each entry is created and checked.
- Showing the same choice in two fields. A person who sets it in one place expects it to appear in the other.
- Disabling options to hide them. A disabled option is still listed, so people learn it exists and why it is locked.
- Changing the option list under an open menu without a reason. The menu refilters as soon as the list changes, so
  the person sees a different set while still picking.
- Relying on the pick order for meaning. The chips and `SelectedValues` follow the order of the picks, which varies
  from person to person; sort the values when the order matters.

### Common questions

**Why does `SelectedValues` not match the order of my list?** The control records each value when it is picked and
keeps that order, for the chips and for `SelectedValues`. Sort the values in your code when the order must match the
list.

**How do I preselect values, and clear them later?** Bind `SelectedItems` to a list of the option objects to set the
initial picks. To change the picks from code afterwards, use `Selection.Select` and `Selection.Deselect` with the item
index, or `UnselectAll()` to clear them.

**Why is there no clear button?** It appears only while at least one value is picked and the field is enabled. A
disabled field hides it, and an empty field has nothing to clear.

**Does Enter submit the form or press the default button?** No. On the trigger, Enter opens the menu, so it never
reaches the window's default button. Inside an open menu, Enter toggles the highlighted option and keeps the menu open.
Use a button for submitting the form.

**Can a person select all options with the keyboard or the mouse?** Arc has no select-all control. `SelectAll()` is
available in code, and it picks only the enabled options.

## API reference

<!-- api: MultiSelect -->

<!-- api: SelectOption -->

## Accessibility

- `Label` names the field. The name also lists the picked labels, so a screen reader hears the current values.
- The field announces that it opens a multi-select list. Expanded state is exposed while the menu is open.
- Chips are hidden from assistive technology and replaced by a spoken summary. The clear button is named
  "Clear selections".
- The focus ring is drawn on the trigger, not on the menu, so focus stays on the field while the menu is open.
- Each move of the highlight and each toggle is announced in the live region with the option's position in the list
  and whether it is selected.
- `Description` is the field's help text. A disabled field is read as disabled and cannot be focused for input.

### Keyboard

The keys below work while the trigger has focus. Keys with a note also work while the search field has focus.

| Keys | Action |
|---|---|
| `Enter` or `Space` | Opens the menu when it is closed |
| `Enter` (menu open, option highlighted) | Toggles the highlighted option and keeps the menu open |
| `Enter` (menu open, no option highlighted) | Closes the menu |
| `Space` (menu open) | Closes the menu; use `Enter` to toggle an option |
| `Down` / `Up` | Opens the menu when it is closed, then moves the highlight. Wraps at both ends and skips disabled options |
| `Escape` | Closes the menu without changing the picks |
| `Tab` (search field open) | Closes the menu and moves focus to the next control |
| Letters and digits (search field open) | Filter the options; `Space` types a space into the search text |

Clicking an option toggles it and keeps the menu open, so a person can pick several values in one visit. Clicking
outside the menu closes it, and clicking the clear button removes every pick, closes the menu and returns focus to the
trigger.

## Motion

- Chip slots open to their new width with a smooth spring. Each chip grows in from a blur.
- The `+N` count rolls up or down as the number changes.
- Each check draws itself in the menu, and the menu drops in from above.
- Reduced motion replaces the size and scale changes with short cross-fades. Off switches the changes directly.
- The clear button pops in with a snappy spring when the first value is picked, and presses in to 96% when pressed.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A value is picked | The chip slot opens and the chip grows in from a blur | Short cross-fade | Immediate |
| A value is removed | The chip shrinks out and its slot closes | Short cross-fade | Immediate |
| The `+N` count changes | The digits roll up or down | Short cross-fade | Immediate |
| Menu opens or closes | Drops in from 6 px above with a slight scale | Short cross-fade | Immediate |
| An option's check changes | The check draws in or out | Short cross-fade | Immediate |
| Hover, press, open | The border and background colors crossfade | Immediate | Immediate |

## Related

- `ChipGroup` shows every option at once; `MultiSelect` keeps the list in a menu.
- `TagInput` for values that people create; `MultiSelect` for values from a fixed list.
- `Select` for one value from a short list.
- `Combobox` for one value from a long list that people search.

| Component | Choose it when |
|---|---|
| `ChipGroup` | All options should stay visible and a few facets are toggled |
| `TagInput` | People create free values, such as tags or recipients |
| `Select` | One value from a short list in a form |
| `Combobox` | One value from a long list that people search |
| `Checkbox` / `Switch` | Each setting is an independent yes or no |
| `DropdownMenu` | The menu runs actions instead of storing values |
