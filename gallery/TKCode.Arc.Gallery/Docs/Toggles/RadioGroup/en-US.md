---
title: Radio group
summary: Choose one option from a visible set.
---

<Demo name="Plans" />

## When to use

A radio group shows a small set of mutually exclusive options with every option visible at once. It belongs in forms
and settings pages where the person must compare the choices before committing, and where each choice needs a
description to be understood. The group is a single labeled control: its header names it, its rows hold the options,
and its selection is one value the form can read or save.

- **Plan selection in a billing or upgrade page**: a workspace owner picks Hobby, Pro or Team. Each row carries a
  description of what the plan includes, which is the information the decision depends on.
- **Delivery or fulfillment options at checkout**: standard shipping, express or pickup, each with its delivery window
  as the description. The chosen option is submitted with the order.
- **Export format or report scope in an analytics console**: CSV, Parquet or JSON, or "This month" against "Year to
  date". Each row explains what the file contains, so the person does not guess.
- **Access or role choice in an invitation form**: Viewer, Editor or Admin, with a description of the permissions
  that come with each role. Use `IsDisabled` on a role the person's own plan does not include.
- **Environment or region selection in a developer tool**: a deployment target chosen once for a form, where every
  option must stay visible for review.

## When not to use

- **Short inline views that switch the content on screen**, such as Day, Week and Month on a chart: use
  `SegmentedControl`, which reads as a row of tabs and changes the view at once.
- **Long lists of options**, such as countries, currencies or teams with dozens of members: use `Select`, which keeps
  the list out of the way until it is needed.
- **More than one option at a time**, such as choosing several notification channels or product areas: use
  `Checkbox` for each option, since a radio group allows exactly one selection.
- **Options that need a price, a meta line or an icon on a card**: use `RadioCards`, which lays each option out as a
  selectable card.
- **A single on or off setting**, such as "Email me invoices": use `Switch`, which takes effect at once and needs no
  group.

## Usage

`Header` is the group's label. The options are the `Items` of the control; each `RadioOption` takes a value, a label
and an optional description.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RadioGroup Header="Plan" Width="420" SelectedValue="pro">
        <arc:RadioOption Value="hobby" Label="Hobby" Description="For side projects and experiments." />
        <arc:RadioOption Value="pro" Label="Pro" Description="For professionals who ship every week." />
        <arc:RadioOption Value="team" Label="Team" Description="Shared workspaces, roles and audit logs for growing teams." />
    </arc:RadioGroup>
</UserControl>
```

`RadioGroup` derives from Avalonia's `HeaderedSelectingItemsControl`. Selection, `SelectedIndex`, `SelectedItem`,
`SelectionChanged`, the header and `HeaderTemplate` are therefore the same as in any Avalonia selecting control, and
the group adds the Arc row design, the single moving highlight, the keyboard model and the automation names on top.
The group creates one row for each option; you never add rows yourself.

### Anatomy

1. **Header** (`Header`): the group's legend, in medium weight, with 8 px below it before the first row. A string
   header also names the group for assistive technology. `HeaderTemplate` replaces the text with richer content.
2. **Rows**: the options, stacked with an 8 px gap. Each row is a 1 px bordered box with the control radius and 12 px
   of inner padding on every side. The row stretches to the width of the group.
3. **Ring**: a 17 px circle with a 1 px border in the strong border color. A selected ring fills with the accent
   color (`ControlOn`).
4. **Dot**: a 7 px dot in the glyph color (`ControlGlyph`) inside the ring. It appears only on the selected row.
   While the pointer is down on an unselected row, a faint dot at 22% opacity previews the choice.
5. **Label** (`RadioOption.Label`): medium weight, in the small type size. It sits 12 px to the right of the ring and
   falls back to the option's `Value` when `Label` is empty. Unselected labels use the secondary text color; the
   selected label uses the foreground color.
6. **Description** (`RadioOption.Description`): the extra line under the label, in the extra-small type size and the
   muted text color, 2 px below the label. It wraps on narrow widths and collapses when the description is empty.
7. **Highlight**: one outline that covers the selected row, with a 1 px accent stroke and a fill of the same color at
   5%. It is a single element for the whole group, so it moves between rows instead of appearing on each one.
8. **Focus ring**: a 2 px ring drawn 2 px outside the focused row. It appears only for keyboard focus.

The row height follows its content: a label alone gives a compact row, and a description adds one wrapped line per
text width. Disabled rows and a disabled group fade to 50% opacity.

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

`SelectedValue` holds the selected option's `Value`, not the option object. Use `SelectedItem` when you need the whole
`RadioOption`, for example to read its description after a save. `SelectedValue` is applied when the options arrive,
so it can be set before `ItemsSource` in code or XAML.

### Binding to a view model

With CommunityToolkit.Mvvm, the view model owns the selected plan and the list of options, and the group only shows
them. `SelectedValue` and `SelectedItem` are two-way, so a change made by the pointer or the keyboard reaches the view
model at once.

```csharp
public interface IBillingService
{
    Task ChangePlanAsync(string planId);
}

public partial class UpgradeViewModel : ObservableObject
{
    private readonly IBillingService _billing;

    public UpgradeViewModel(IBillingService billing)
    {
        _billing = billing;
        Plans.Add(new RadioOption("hobby", "Hobby", "For side projects and experiments."));
        Plans.Add(new RadioOption("pro", "Pro", "For professionals who ship every week."));
        Plans.Add(new RadioOption("team", "Team", "Shared workspaces, roles and audit logs for growing teams."));
        SelectedPlanId = "pro";
    }

    public ObservableCollection<RadioOption> Plans { get; } = new();

    [ObservableProperty]
    private string? _selectedPlanId;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        await _billing.ChangePlanAsync(SelectedPlanId!);
    }

    private bool CanSave() => !string.IsNullOrEmpty(SelectedPlanId);

    partial void OnSelectedPlanIdChanged(string? value) => SaveCommand.NotifyCanExecuteChanged();
}
```

```xml
<StackPanel Width="420" Spacing="16">
    <arc:RadioGroup Header="Plan"
                    ItemsSource="{Binding Plans}"
                    SelectedValue="{Binding SelectedPlanId}" />
    <arc:Button Content="Change plan" Command="{Binding SaveCommand}" />
</StackPanel>
```

- `SelectedValue` is the property to bind for the saved choice. Bind `SelectedItem` as well only when the view needs the
  option object.
- `ItemsSource` takes an `ObservableCollection<RadioOption>`, so adding or removing an option updates the group.
- Use `SelectionChanged` in code for side effects that are not data, such as a preview label. Keep the save in the
  command, so it runs once when the person presses the button and not on every arrow key.
- Set `Header` in XAML with a plain string. A string header is also the group's accessible name.

### Disabled options

`IsDisabled` keeps an option visible but unavailable. Arrow keys skip disabled options, and the row cannot be selected
by the pointer or by Space and Enter.

<Demo name="InlineOptions" />

Show a disabled option when the person needs to know it exists and why it is not available, such as a plan the account
has not reached yet. Say the reason in the option's description, for example "Available on the Team plan". Remove the
option instead when it is never relevant to this person.

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

### Option count and layout

The default items panel is a plain vertical `StackPanel`, so every option is created when the group is shown. That is
the right trade-off for two to six options with descriptions. For a longer list, use `Select`, which opens a list on
demand and does not place every row on the page. For a horizontal row of short views, use `SegmentedControl`.

Give the group a width at which the longest description wraps onto two or three lines. A very narrow group wraps each
description onto many short lines, and the options become harder to compare.

### Example: plan selection in a workspace upgrade

A billing page for a workspace owner, from a SaaS admin console. The header is a sentence that states the
consequence of the choice ("Changes take effect at the next invoice"). The three plans carry the one fact that
separates them, so the person can decide from the group alone. The selected row is Team, and the primary action names
what it does.

![A billing page with a plan group that has the Team plan selected and a save action](images/UpgradePlan.png)

```xml
<StackPanel Width="620" Spacing="20">
    <StackPanel Spacing="6">
        <TextBlock Text="Billing plan"
                   FontSize="18"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Acme Analytics renews on 1 December 2026. Changes take effect at the next invoice."
                   FontSize="13"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <arc:RadioGroup Header="Plan" SelectedValue="team">
        <arc:RadioOption Value="hobby"
                         Label="Hobby"
                         Description="One workspace, up to 3 seats, 30 days of history." />
        <arc:RadioOption Value="pro"
                         Label="Pro"
                         Description="Unlimited seats, 1 year of history and scheduled exports." />
        <arc:RadioOption Value="team"
                         Label="Team"
                         Description="Roles, audit logs and single sign-on for up to 500 seats." />
        <arc:RadioOption Value="enterprise"
                         Label="Enterprise"
                         Description="Contact sales. Available on annual contracts."
                         IsDisabled="True" />
    </arc:RadioGroup>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Save plan" />
    </StackPanel>
</StackPanel>
```

### Content guidelines

- **Header**: a short noun phrase that names the choice: "Plan", "Delivery method", "Export format". Do not write a
  question ("Which plan?"); the options are the answer.
- **Option label**: one or two words that name the option as the person thinks of it: "Team", "Standard shipping".
  Use sentence case, and keep labels parallel, so every row is a plan or every row is a method.
- **Description**: one sentence with the fact that separates the option from the others: seats, history, delivery
  window, what is included. Write numbers as digits, and keep the descriptions about the same length.
- **Disabled reason**: name the condition in the description: "Available on the Team plan".

| Instead of | Write |
|---|---|
| Which plan would you like? | Plan |
| Best value!!! | Unlimited seats and 1 year of history. |
| Option 2 | Pro |
| Contact our sales team for pricing information regarding enterprise options | Contact sales. Available on annual contracts. |

### Best practices

**Do**

- Put the option the person is most likely to choose first, or the one the account already has selected, so the
  decision starts from the current state.
- Give every option a description when the labels alone do not explain the difference. A row without a description is
  faster to read, so add one only when it carries information.
- Set a `SelectedValue` that matches the saved state. An empty selection shows no mark, which reads as an unanswered
  question on a form that expects an answer.
- Use `IsDisabled` for an option that exists but cannot apply, and say why in its description.

**Avoid**

- Radio groups for a yes or no choice. A `Switch` or a `Checkbox` says what the person is setting, and two radio rows
  ask the same thing twice.
- More than six options in a group. The page grows by a row for each option, and the choice becomes a scan rather
  than a comparison; use `Select` instead.
- Replacing the options on every refresh while the person is reading them. Update the collection only when the data
  changes, so the group does not redraw under the pointer.
- Relying on color alone to show the selection. The filled ring and the dot are the second cue that a selected row
  carries, so the choice does not depend on telling two colors apart.

### Common questions

**Does the group select anything by default?** No. `SelectedIndex` is -1 until a value is set, and no row shows the
mark. Set `SelectedValue` to the default you want.

**Can I set `SelectedValue` before the options arrive?** Yes. The value is applied when the options arrive, so a
group whose options load asynchronously keeps the value that was set first.

**Why does the group not name itself?** A string `Header` names the group. A non-string header, such as a panel with
a heading and a hint, does not, so set `AutomationProperties.Name` on the group in that case.

**How do I show the description of the selected option somewhere else?** Read `SelectedItem` as a `RadioOption` in
`SelectionChanged`, and show its `Description` in a text block. The group's own rows keep their descriptions.

**Can the rows take arbitrary objects?** Use `RadioOption` for every row. The row reads `Label`, `Description` and
`IsDisabled` from that type, so other objects do not give the row its text. Map your own model to `RadioOption` in
the view model.

## API reference

<!-- api: RadioGroup -->
<!-- api: RadioOption -->

## Accessibility

- `Header` names the group when it is a string. Each option is a radio button, and its description is the help text.
- The group is one tab stop. Tab enters it at the selected option, and the next Tab leaves it. Keyboard focus never
  stops on an unselected option.
- Arrow keys move the selection to the next or previous option and wrap around. Disabled options are skipped.
- Home and End select the first and last option. Space and Enter select the focused option.
- The highlight and the dot are hidden from assistive technology. Each option reports whether it is selected.
- Keyboard focus draws a 2px ring around the row. Pointer focus does not draw one.
- Color is not the only cue: the selected row has a filled ring, a dot and a foreground label.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Enters the group at the selected option; the next `Tab` leaves the group |
| `Down` / `Right` | Selects and focuses the next option, wrapping from the last to the first |
| `Up` / `Left` | Selects and focuses the previous option, wrapping from the first to the last |
| `Home` / `End` | Selects and focuses the first or last option |
| `Space` / `Enter` | Selects the focused option |

Arrow keys skip disabled options, in both directions.

## Motion

- One highlight glides to the selected row on a morph spring. The new dot springs in while the old one shrinks away,
  so a change reads as one physical move.
- Pressing a ring scales it down slightly. Pressing an unselected row previews its dot.
- When the group changes size, the highlight moves to its new row at once.
- Reduced motion places the highlight and the dot at once and removes the press scale. Off does the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Selection moves | The highlight glides on a morph spring; the new dot springs in from 40% and fades in | The highlight and the dot land at once | Immediate |
| Old dot leaves | Shrinks and fades out at once | Immediate | Immediate |
| Ring press | Scales to 88% while held; springs back on release | No scale | No scale |
| Unselected row pressed | The preview dot shows at 22% opacity | The preview dot shows | The preview dot shows |
| Row and label colors | Background and ring colors crossfade; the label color crossfades | Immediate | Immediate |
| Group resizes | The highlight moves to the new row at once | Immediate | Immediate |

## Related

- `RadioCards` for options that need a price, a meta text or an icon on a card.
- `SegmentedControl` for two to five short views that switch the content at once.
- `Checkbox` when the person may choose several options.
- `Select` for a long list of options.

| Component | Choose it when |
|---|---|
| `RadioCards` | Each option needs a price, a meta line or an icon, and the cards compare well side by side |
| `SegmentedControl` | Two to five short views of the same content switch at once |
| `Checkbox` | The person may select several options, or one option is a yes or no setting |
| `Select` | The list is long, or the options are many items such as countries or teams |
| `Switch` | A single setting takes effect at once with no group |
