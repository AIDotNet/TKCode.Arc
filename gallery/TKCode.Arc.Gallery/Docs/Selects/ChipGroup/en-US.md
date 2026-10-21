---
title: Chip group
summary: Filter by a few facets with chips that morph as you pick them.
---

<Demo name="FilterChips" />

## When to use

`ChipGroup` shows a small set of options as toggle chips, all visible at once, so people can see what is available and
what is active without opening a menu. It fits filter bars and facet pickers at the top of a list, where the choices are
known, few and changed often. Every chip is a button with a pressed state, and the group keeps one tab stop, so a row
of chips costs one stop in the keyboard order.

- **Support inbox filters**: a ticket list in a help desk that filters by topic (Billing, Refunds, Login, API,
  Webhooks). Agents switch topics many times per shift, and every topic stays visible, so no one has to open a menu to
  learn what exists.
- **Order status in an e-commerce back office**: a single choice of Open, Packed, Shipped or Returned for the orders
  table. With `IsMultiple="False"` a second click clears the filter, which matches how people back out of a filter.
- **Facets on an analytics dashboard**: regions or channels above a chart, such as EMEA, North America, Paid search or
  Email. The chips sit on one line above the widgets they affect, and the selection is the state the view model
  reads when it reloads the data.
- **Onboarding steps that ask for a few interests**: a workspace setup page where a team picks the areas it will use
  (Finance, Sales, Support). Choices that are short and visible are easier to scan than a dropdown.
- **Long facet lists in a reporting page**: `MaxVisibleItems` keeps the first few chips and folds the rest behind a
  "+N more" chip, so a page with twenty product lines does not grow tall, and the lines already picked stay in view.

## When not to use

- **The selection must fit inside a compact field or a toolbar slot**: use `MultiSelect`, which shows the chosen values
  in its trigger and opens a list.
- **Each option is a yes or no answer in a form**: use `Checkbox`, which carries a label, a state and a validation
  message per option.
- **Choosing one view out of a few**, such as Table, Board or Calendar: use `SegmentedControl`, which reads as a mode
  switch rather than a filter.
- **A single value from a long or searchable list in a form**: use `Select` for a short list or `Combobox` when people
  type to find the value.
- **Values people invent**: use `TagInput`, which accepts new text as a tag. A chip group only selects options that
  already exist.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ChipGroup Label="Topics" MaxWidth="560">
        <arc:SelectItem Content="Design" />
        <arc:SelectItem Content="Engineering" />
        <arc:SelectItem Content="Research" />
        <arc:SelectItem Content="Marketing" />
        <arc:SelectItem Content="Sales" />
    </arc:ChipGroup>
</UserControl>
```

`ChipGroup` derives from Avalonia's `SelectingItemsControl`, the base of `ListBox`. The item source, `Items`,
`SelectedItem`, `SelectedIndex`, `Selection` and the `SelectionChanged` event behave as they do in a list box. On top of
that base, the control lays its chips out in a wrapping panel, morphs a chip when it is picked, and folds long sets
behind a "+N more" chip. `SelectItem` supplies the content and value of each option, and any object works as an option
when you set `ItemsSource`.

### Anatomy

1. **Group label** (`Label`): the accessible name of the group, such as "Topics". The control does not draw it, so
   place a visible heading above the group.
2. **Chip**: a pill with a radius of half its height. Its height is the Arc small control size, 36 px. The label uses the
   small type size (`TypeSM`) at medium weight (500), with 14 px of padding on each side (`ChipInlinePadding`). The fill
   is the surface color, the border is 1 px in the border color and the text is the secondary text color.
3. **Selected state**: the fill mixes 11% of the accent color into the surface, and the border mixes 42% of the accent
   into the border color. Both are computed in OKLab, so the tint follows the accent. On hover the mixes rise to 16%
   and 60%. The selected state always has the check as well, so color is never the only signal.
4. **Check** (selected chips): a 14 px check in the accent-strong color. Its slot is 18 px, which the label gives up
   when the chip is selected.
5. **More chip** (`+N more`): the muted surface and muted text colors, the same height as a chip. It reads "Show less"
   while the folded chips are shown.
6. **Spacing**: 8 px between chips and between lines (`ChipGap`).
7. **Focus ring**: a 2 px ring drawn 3 px outside the focused chip in the focus-ring color. The group draws no focus
   adorner of its own.

### Choosing the selection mode

`IsMultiple` is `True` by default, so each chip toggles on its own and the selected values keep the order of the
options, not the order of the clicks. Set `IsMultiple="False"` for a single choice. A click on another chip replaces the
selection, and a click on the selected chip clears it, so the group can always end with no choice. The group has no
setting that forbids clearing; if a form needs a value, validate it on submit.

<Demo name="SingleChoice" />

```xml
<arc:ChipGroup Label="Status" IsMultiple="False">
    <arc:SelectItem Content="Open" Value="open" />
    <arc:SelectItem Content="In progress" Value="progress" />
    <arc:SelectItem Content="Closed" Value="closed" />
</arc:ChipGroup>
```

Choose single mode when the chips are mutually exclusive states of one thing, such as a ticket status or a reporting
period. Keep multiple mode for facets that combine, such as topics or regions, where "Billing and Login" is a valid
filter.

### Folding long lists

`MaxVisibleItems` shows that many chips, and the rest fold behind a "+N more" chip. The default is no limit. Two rules
keep folding predictable:

- A selected chip stays in view even when it is past the limit, so an active filter is never hidden. A chip that is
  picked while the list is expanded also stays in view after the list folds again.
- `IsExpanded` is two-way. Clicking the more chip toggles it, and the chip reads "Show less" while the list is open.

<Demo name="Folded" />

```xml
<arc:ChipGroup Label="Topics"
               MaxVisibleItems="5"
               MaxWidth="560"
               IsExpanded="{Binding ShowAllTopics, Mode=TwoWay}"
               ItemsSource="{Binding Topics}" />
```

Pick a limit that keeps the first line of chips useful at the width the group has. Five to seven chips suit a filter bar
on a desktop page, and fewer suit a narrow side panel. Put the most used options first, because the folded ones are
the ones people have to expand to reach.

### Binding to a view model

With CommunityToolkit.Mvvm, the view model owns the options and the selection, and the group displays them. Bind the
options to an `ObservableCollection<T>`, bind the chosen items to a second collection, and react to changes in the view
model rather than in the view.

```csharp
public partial class TicketFilterViewModel : ObservableObject
{
    public ObservableCollection<string> Topics { get; } = new()
    {
        "Billing", "Refunds", "Login", "API", "Webhooks", "Invoices", "SSO"
    };

    public ObservableCollection<string> ChosenTopics { get; } = new();

    [ObservableProperty]
    private bool _showAllTopics;

    [ObservableProperty]
    private string? _status = "In progress";

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        await _tickets.QueryAsync(ChosenTopics.ToArray(), Status);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:ChipGroup Label="Topics"
                   MaxVisibleItems="5"
                   ItemsSource="{Binding Topics}"
                   SelectedItems="{Binding ChosenTopics}"
                   IsExpanded="{Binding ShowAllTopics, Mode=TwoWay}" />
    <arc:ChipGroup Label="Status" IsMultiple="False">
        <arc:SelectItem Content="Open" Value="open" />
        <arc:SelectItem Content="In progress" Value="progress" />
        <arc:SelectItem Content="Closed" Value="closed" />
    </arc:ChipGroup>
    <arc:Button Content="Apply filters" Command="{Binding ApplyFiltersCommand}" />
</StackPanel>
```

- `SelectedItems` is two-way. A chip click adds or removes the item in `ChosenTopics`, and an item the view model adds
  to the collection is selected. Use a collection of the same values as `ItemsSource` so that the lists match by value.
- `IsExpanded` is two-way, so the view model can open the folded chips, for example after a search matches one of them.
- `SelectedItem` is the single choice in single mode. Bind it with `Mode=TwoWay` when the view model needs to set it.
- `SelectedValues` is read-only and lists the `Value` of each chosen option in option order. Read it in a handler or
  when the command runs, not as a binding source.
- To react to every change, handle `SelectionChanged` in code or query the view model when the command runs.

For items that are objects rather than strings, set `TextSelector` to the label and `ValueSelector` to the value in
code. Both are typed as delegates, so they work without reflection and under trimming.

```csharp
topicsGroup.TextSelector = item => ((Topic)item!).Name;
topicsGroup.ValueSelector = item => ((Topic)item!).Id;
```

### Data shape and performance

Each chip is a control with its own text layout and motion, so the group is built for a short facet list, not for a
catalog. Keep the option count to what a person can scan in one glance, and use `MaxVisibleItems` to keep the first
line short when a list is longer. For a catalog of many values, use `MultiSelect` with search or `Combobox`.

The chip row reflows when the options or the width change, and each chip springs to its new position. Chips move while
they reflow, so avoid changing the option list while a person is picking from it.

### Example: support inbox filter bar

A filter bar above a support ticket list. The topics are a multiple choice with a fold after five chips, the status is
a single choice that starts on "In progress", and the footer states the result count so the filter's effect is visible
before the person applies it. The chips use short nouns that match the ticket fields, and the folded set keeps the
currently selected "Invoices" chip in view even though it sits past the limit.

![A support inbox filter bar with topic chips, a folded overflow and a single status choice](images/SupportFilters.png)

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
            xmlns:generic="using:System.Collections.Generic"
            Width="680"
            Spacing="20">
    <TextBlock Text="Support tickets"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <StackPanel Spacing="8">
        <TextBlock Text="Topic"
                   FontSize="13"
                   FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:ChipGroup Label="Topics" MaxVisibleItems="5">
            <arc:ChipGroup.ItemsSource>
                <generic:List x:TypeArguments="x:String">
                    <x:String>Billing</x:String>
                    <x:String>Refunds</x:String>
                    <x:String>Login</x:String>
                    <x:String>API</x:String>
                    <x:String>Webhooks</x:String>
                    <x:String>Invoices</x:String>
                    <x:String>SSO</x:String>
                </generic:List>
            </arc:ChipGroup.ItemsSource>
            <arc:ChipGroup.SelectedItems>
                <generic:List x:TypeArguments="x:String">
                    <x:String>Billing</x:String>
                    <x:String>Invoices</x:String>
                </generic:List>
            </arc:ChipGroup.SelectedItems>
        </arc:ChipGroup>
    </StackPanel>
    <StackPanel Spacing="8">
        <TextBlock Text="Status"
                   FontSize="13"
                   FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:ChipGroup Label="Status" IsMultiple="False" SelectedIndex="1">
            <arc:SelectItem Content="Open" Value="open" />
            <arc:SelectItem Content="In progress" Value="progress" />
            <arc:SelectItem Content="Closed" Value="closed" />
        </arc:ChipGroup>
    </StackPanel>
    <Grid ColumnDefinitions="*,Auto">
        <TextBlock Text="Showing 42 of 318 tickets"
                   VerticalAlignment="Center"
                   FontSize="13"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8">
            <arc:Button Content="Clear filters" Variant="Secondary" />
            <arc:Button Content="Apply filters" />
        </StackPanel>
    </Grid>
</StackPanel>
```

### Content guidelines

- **Option labels**: a short noun or a noun phrase in sentence case, such as "Billing", "Packed" or "In progress". Use the
  words of the field the filter applies to, so the chip and the column header match.
- **Group label**: the name of the facet in a visible heading above the group, such as "Topic" or "Status". The
  `Label` value repeats it for assistive technology, so keep the two the same.
- **Counts and results**: state the effect in the footer, for example "Showing 42 of 318 tickets", and write numbers as
  digits.
- **Buttons**: use a verb and an object, such as "Apply filters" and "Clear filters", not "OK".

| Instead of | Write |
|---|---|
| Filter options | Topic |
| Select your topic(s) | Billing, Refunds |
| Click to filter | Apply filters |
| Misc | Other |
| All of the things | All topics |

### Best practices

**Do**

- Use a visible heading above each group and keep `Label` equal to it, so a screen reader and a sighted user name the
  same facet.
- Keep `IsMultiple` true for facets that combine and use single mode only for mutually exclusive states.
- Set `MaxVisibleItems` on long facet lists, and order the options so the most used ones come first.
- Apply the filter with a command or on a clear action, and show the result count, so the chips do not feel like they
  change data by themselves.

**Avoid**

- Chips for values people type or create. The group selects existing options only; use `TagInput` for new tags.
- Relying on a second click as the only way to clear a filter. A single choice clears that way, but a visible "Clear
  filters" button is easier to find and also clears multiple choices.
- Long option lists without a fold. Each chip is a control with its own motion, so a long catalog makes the bar heavy and
  hard to scan.
- Using chips for yes or no questions in a form. `Checkbox` carries the label, state and validation that a form needs.
- Reordering the options at run time. Chips move under the pointer, so people lose their place in a list that changes
  while they read it.

### Common questions

**Why does the group not show a heading?** The control draws only the chips and the "+N more" chip. `Label` is the
accessible name, so show the heading as a separate `TextBlock` above the group, as in the example.

**Can I stop people from clearing a single choice?** No. The group clears the selection on a second click by design.
If the form needs a value, validate it on submit and show the message next to the group.

**Are selected chips hidden when the list is folded?** No. A chip that is selected when the list folds stays in view,
and so does a chip picked while the list is open. Only the unselected overflow folds.

**How do I select chips in code?** Use `Selection.Select(index)` and `Selection.Deselect(index)`, or `Toggle(index)`,
which works like a click. `Toggle` ignores indexes out of range and disabled options. To select several values at
once, add them to the `SelectedItems` list.

**How do I preselect chips in XAML?** Use `SelectedIndex` for one chip in single mode, as the folded-list example above does. For
several chips, bind or set `SelectedItems` to a list of the same values as `ItemsSource`, as the screenshot does.

## API reference

<!-- api: ChipGroup -->

<!-- api: SelectItem -->

## Accessibility

- `Label` is the accessible name of the group. The group is exposed as a named group, and the name comes from `Label`
  when no other name is set.
- Each chip is a button with a toggle state. Screen readers announce a chip as on or off, so the selection is not
  conveyed by color alone.
- The "+N more" chip is an expandable control. It reports whether the folded chips are expanded and can be expanded and
  collapsed through the automation expand and collapse actions.
- The group keeps one tab stop, so Tab moves into the group once and moves out of it once. The stop is the last chip
  that had focus, else the first selected chip, else the first chip.
- The focus ring is drawn on the focused chip in the focus-ring color, so it stays visible on a selected chip.

### Keyboard

The group takes one tab stop, and the arrow keys move between chips inside it.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move into the group on its tab stop chip, and out of it |
| `Right` / `Down` | Move to the next chip; from the last one, wrap to the first |
| `Left` / `Up` | Move to the previous chip; from the first one, wrap to the last |
| `Home` / `End` | Move to the first or last chip, which is the "+N more" chip when the list folds |
| `Space` or `Enter` | Toggle the focused chip; on the "+N more" chip, expand or collapse the folded chips |

## Motion

- A picked chip grows a check from its left edge, and its label slides open to the new width while the chip's edge
  follows on a spring.
- Neighbouring chips glide to their new positions, including across line breaks, so the row reflows without a jump.
- Folded chips appear with a short stagger when the list expands, and the group height changes smoothly.
- A pressed chip scales down slightly and springs back when released.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A chip is picked or cleared | The check grows, the label slides and the edge springs to the new width | A short fade | Immediate |
| Neighbouring chips move | They glide to their new positions, across lines too | A short fade | Immediate |
| Folded chips appear | Staggered entrance, with the group height easing | A short fade | Immediate |
| Folded chips leave | They fade in place, faster than they came | A short fade | Immediate |

## Related

- `MultiSelect` for the same choice inside a compact field, and `Checkbox` for form choices that each stand on their
  own.
- `SegmentedControl` for picking one view, and `Select` or `Combobox` for one value from a list in a form.

| Component | Choose it when |
|---|---|
| `MultiSelect` | The selected values must fit in a compact field or toolbar slot |
| `Checkbox` | Each option is a yes or no answer in a form, with its own validation |
| `SegmentedControl` | People choose one view or mode out of two to four |
| `Select` | One value from a short list in a form |
| `Combobox` | One value from a longer list that people type to find |
| `TagInput` | People create their own tags instead of picking existing options |
