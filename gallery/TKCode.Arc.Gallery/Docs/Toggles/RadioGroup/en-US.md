---
title: Radio group
summary: Choose one option from a visible set.
---

## When to use

- Two to six mutually exclusive options that need a description each, such as plans.
- A choice in a form that is submitted with the form.
- Options listed vertically, where every option stays visible.

## When not to use

- Short inline views that switch the content: use `SegmentedControl`.
- A long list of options: use `Select`.
- More than one option at a time: use `Checkbox`.
- Options that need a price, a meta text or an icon on a card: use `RadioCards`.

## Usage

`Header` is the group's label. The options are the `Items` of the control; each `RadioOption` takes a value, a label
and an optional description.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RadioGroup Header="Plan" Width="420" SelectedValue="pro">
        <arc:RadioOption Value="hobby" Label="Hobby" Description="For side projects and experiments." />
        <arc:RadioOption Value="pro" Label="Pro" Description="For professionals who ship every week." />
        <arc:RadioOption Value="team" Label="Team" Description="Shared workspaces, roles and audit logs for growing teams." />
    </arc:RadioGroup>
</UserControl>
```

### Bound options

Bind `ItemsSource` to a list of options and `SelectedValue` to the selected value. Both are two-way.

```xml
<arc:RadioGroup Header="Plan"
                ItemsSource="{Binding Plans}"
                SelectedValue="{Binding PlanId}" />
```

```csharp
var plans = new RadioGroup
{
    Header = "Plan",
    ItemsSource = new[]
    {
        new RadioOption("hobby", "Hobby", "For side projects and experiments."),
        new RadioOption("pro", "Pro", "For professionals who ship every week.")
    },
    SelectedValue = "pro"
};
plans.SelectionChanged += (_, _) => status.Text = $"Selected: {plans.SelectedValue}";
```

### Disabled options

`IsDisabled` keeps an option visible but unavailable. Arrow keys skip disabled options.

```xml
<arc:RadioGroup Header="Density" Width="420" SelectedValue="comfortable">
    <arc:RadioOption Value="comfortable" Label="Comfortable" />
    <arc:RadioOption Value="compact" Label="Compact" />
    <arc:RadioOption Value="spacious" Label="Spacious" IsDisabled="True" />
</arc:RadioGroup>
```

### Selection events

`SelectionChanged` is raised when the selection changes, by the pointer or by the keyboard. `SelectedItem` gives the
`RadioOption` itself.

```csharp
group.SelectionChanged += (_, _) =>
{
    if (group.SelectedItem is RadioOption option)
    {
        Console.WriteLine(option.Value);
    }
};
```

## API reference

<!-- api: RadioGroup -->
<!-- api: RadioOption -->

## Accessibility

- `Header` names the group. Each option is a radio button, and its description is the help text.
- Arrow keys move the selection to the next or previous option and wrap around. Disabled options are skipped.
- Home and End select the first and last option. Space and Enter select the focused option.
- Tab enters the group at the selected option and leaves it from there, so the group is a single tab stop.
- Keyboard focus draws a 2px outline offset by 2px. Pointer focus does not.

## Motion

- One highlight glides to the selected row on a morph spring. The new dot springs in while the old dot shrinks away.
- Pressing a radio circle scales it down slightly. Pressing an unselected row previews its dot.
- When the group changes size, the highlight moves to the new row at once.
- Reduced motion places the highlight and the dot at once and removes the press scale. Off does the same.

## Related

- `RadioCards` for options that need a price, a meta text or an icon on a card.
- `SegmentedControl` for two to five short views that switch the content at once.
- `Checkbox` when the user may choose several options.
- `Select` for a long list of options.
