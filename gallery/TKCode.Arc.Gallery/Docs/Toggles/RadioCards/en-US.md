---
title: Radio cards
summary: Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.
---

<Demo name="Grid" />

## When to use

`RadioCards` is a single choice where each option needs more than a label before the person can decide: a price, a
delivery time, a description or a reason it is unavailable. It sits in a form or a step of a flow, such as plan
selection at sign-up or shipping at checkout, and the card the person picks is the one the flow continues with.

- **Plan selection at sign-up or in a billing step**: a SaaS product with Starter, Pro and Team plans. Each card shows
  the plan's price in its meta slot and one line of what it includes.
- **Shipping and delivery choices at checkout**: Standard, Express and Overnight, each with a delivery window and a cost.
  The List layout keeps the window and the price on one row, so the cost is easy to compare.
- **Region or data residency choices in an admin console**: a workspace region picked for compliance, where each card
  states the region and a short note on latency or retention.
- **Options with a reason they cannot be picked**: an Enterprise plan that needs a sales contact, or a storage tier that
  the account's current plan does not include. `DisabledReason` explains the restriction where the person is looking.
- **A required choice in a short list**: an export format or a report period in a dashboard dialog, where the person
  must pick one of three to four options before Run becomes meaningful.

## When not to use

- **Short text-only options**, such as "Yes, No, Maybe" or a status: use `RadioGroup`, which is compact and has no
  price or description slot.
- **Two to four views that switch the content at once**, such as Overview, Activity and Settings above a panel: use
  `SegmentedControl`. The choice changes the view, not a value the form saves.
- **Long lists of options**, such as a country list or a hundred projects: use `Select` or `Combobox`. Cards take a lot
  of space per option and do not filter.
- **A billing period switch with a savings note**, such as Monthly and Yearly: use `BillingToggle`, which is one row of
  short labels with badges.
- **Several options that can all be on**, such as the add-ons on a plan: use `Checkbox`. A radio card allows one choice.

## Usage

The cards are the `Items` of the control. Each `RadioCardOption` takes a value, a label, a description and a meta
text, which is shown at the end of the card.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RadioCards SelectedValue="pro">
        <arc:RadioCardOption Value="starter" Label="Starter" Description="For individuals getting started." Meta="$0" />
        <arc:RadioCardOption Value="pro" Label="Pro" Description="Unlimited projects and priority support." Meta="$12" />
        <arc:RadioCardOption Value="team" Label="Team" Description="Shared workspaces and roles for small teams." Meta="$32" />
    </arc:RadioCards>
</UserControl>
```

`RadioCards` derives from Avalonia's `SelectingItemsControl`. It has the usual items model (`ItemsSource`, `Items`,
`SelectedIndex`, `SelectedItem`) and the selection events of that base class, and `SelectedValue` is the property an
application binds, because it carries the option's value rather than the option object. The cards add the selection
ring, the indicator dot, the description and meta slots, the disabled reason and the two layouts on top of the selecting
control, so a form gets a complete radio group with no custom template.

### Anatomy

1. **Card**: a 20 px corner radius on the surface color with a 1 px border. Hover and keyboard focus strengthen the border;
   a disabled card uses the muted surface and muted text.
2. **Selection ring**: a 1.5 px ring in the control-on color drawn over the card's border. It belongs to the group, not to
   the card, so it glides from one card to the next.
3. **Indicator**: an 18 px circle with a 1.5 px border, placed at the top end of a grid
   card and at the start of a list row. When the card is chosen, it fills with the control-on color and shows a 6 px dot
   in the glyph color.
4. **Icon** (`Icon`): an 18 px glyph beside the label, drawn with the secondary text color and the foreground color when
   the card is selected. A `Geometry` is wrapped at 18 px automatically; any other content is placed as given.
5. **Label** (`Label`): medium weight, truncated with an ellipsis when it does not fit.
6. **Description** (`Description`): extra-small text in the secondary color, meant for one or two short lines, with a 2 px
   gap under the label. A disabled card shows `DisabledReason` here instead.
7. **Meta** (`Meta`): the price or estimate. In a grid card it sits at the bottom in the large text size with 14 px above
   it. In a list row it sits at the end in the small medium-weight size with 12 px before it.
8. **Focus ring**: a 2 px ring around the card for keyboard focus. It is hidden for pointer focus.

Cards in one row share the height of the tallest card, so prices line up across the row. The grid places cards in
columns of at least `MinColumnWidth` (180 by default), with a 10 px gap. The list places one card per full-width row
with an 8 px gap.

### Binding to a view model

In an MVVM app, the view model owns the options and the selected value. Bind `ItemsSource` to an
`ObservableCollection<RadioCardOption>` and `SelectedValue` to the id of the selected option. Both are two-way for the
selection and one-way for the items. Use the value, not the option object, so the saved choice is a plain string or id.

```csharp
public partial class PlanViewModel : ObservableObject
{
    public ObservableCollection<RadioCardOption> Plans { get; } = new()
    {
        new RadioCardOption("starter", "Starter", "For individuals getting started.", "$0"),
        new RadioCardOption("pro", "Pro", "Unlimited projects and priority support.", "$12"),
        new RadioCardOption("team", "Team", "Shared workspaces and roles for small teams.", "$32"),
        new RadioCardOption("enterprise", "Enterprise", "SSO, audit logs and a dedicated contact.", "Custom")
        {
            IsDisabled = true,
            DisabledReason = "Talk to sales to enable Enterprise."
        }
    };

    [ObservableProperty]
    private string? _planId = "pro";

    [RelayCommand]
    private async Task ContinueAsync()
    {
        await _subscriptions.StartAsync(PlanId);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:RadioCards ItemsSource="{Binding Plans}"
                    SelectedValue="{Binding PlanId}"
                    AutomationProperties.Name="Plan" />
    <arc:Button Content="Continue" Command="{Binding ContinueCommand}" IsDefault="True" />
</StackPanel>
```

- `SelectedValue` is two-way: a choice made by the person writes the id to `PlanId`, and a value set in code selects the
  matching card. A value that matches no card leaves nothing selected.
- Set `IsDisabled` on the option in the view model, not in the XAML, when availability depends on the account. The card
  then shows the reason without a separate binding.
- Compiled bindings need `x:DataType` on the view (the examples here leave it out for brevity).

### Options in code

The same options can be built in code-behind or in a service, then assigned to `ItemsSource`, or the control can take
them inline in XAML. Assigning `SelectedValue` after the options are set keeps the choice.

```csharp
var plans = new RadioCards
{
    ItemsSource = new[]
    {
        new RadioCardOption("pro", "Pro", "Unlimited projects and priority support.", "$12") { Icon = ArcIcons.Zap },
        new RadioCardOption("team", "Team", "Shared workspaces and roles for small teams.", "$32") { Icon = ArcIcons.Users }
    },
    SelectedValue = "pro"
};
plans.SelectionChanged += (_, _) => Console.WriteLine(plans.SelectedValue);
```

`Icon` takes a `Geometry`. It is drawn as an 18px icon at the start of the card.

### Choosing a layout

`Layout="List"` stacks the cards as full-width rows with the meta at the end. Long descriptions wrap. The default
`Grid` layout fills columns that are at least `MinColumnWidth` wide (180 by default).

Use the grid for plans and regions, where the person compares the cards side by side and the description is the reason
to choose. Use the list for shipping, payment or delivery options, where each choice is a sentence with a time and a
price that the person scans down. The grid drops to one column when the container is narrower than about 370 px (two
columns of 180 plus the gap), so a narrow dialog still shows every card at full width.

<Demo name="List" />

### Unavailable options

Set `IsDisabled` on an option to keep it visible but unavailable. Set `DisabledReason` to say why. The reason
replaces the description while the option is disabled. Arrow keys skip disabled options.

```xml
<arc:RadioCards SelectedValue="pro">
    <arc:RadioCardOption Value="pro" Label="Pro" Description="Unlimited projects and priority support." Meta="$12" />
    <arc:RadioCardOption Value="enterprise"
                         Label="Enterprise"
                         Description="SSO, audit logs and a dedicated contact."
                         Meta="Custom"
                         IsDisabled="True"
                         DisabledReason="Talk to sales to enable Enterprise." />
</arc:RadioCards>
```

### Required and disabled

`IsRequired` marks the group as required in a form. `IsEnabled="False"` disables every card.

```xml
<arc:RadioCards IsRequired="True" IsEnabled="False" />
```

### Example: plan selection at sign-up

A sign-up step for a project management product, Northwind Cloud. The four plans compare side by side in the grid. The
selected Pro card carries the ring and the filled indicator, the Enterprise card explains why it cannot be chosen online,
and the primary button continues with the chosen plan. Every card has the same height because the row takes the tallest
card, so the prices line up across the row.

![A plan selection step with four radio cards, one selected and one disabled with a reason](images/PlanCards.png)

```xml
<StackPanel Width="760" Spacing="20">
    <StackPanel Spacing="4">
        <TextBlock Text="Choose a plan for Northwind Cloud"
                   FontSize="18"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="You can change plans at any time from billing settings."
                   FontSize="13"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <arc:RadioCards SelectedValue="pro" AutomationProperties.Name="Plan">
        <arc:RadioCardOption Value="starter"
                             Label="Starter"
                             Description="For individuals getting started."
                             Meta="$0"
                             Icon="{x:Static arc:ArcIcons.User}" />
        <arc:RadioCardOption Value="pro"
                             Label="Pro"
                             Description="Unlimited projects and priority support."
                             Meta="$12"
                             Icon="{x:Static arc:ArcIcons.Zap}" />
        <arc:RadioCardOption Value="team"
                             Label="Team"
                             Description="Shared workspaces and roles for small teams."
                             Meta="$32"
                             Icon="{x:Static arc:ArcIcons.Users}" />
        <arc:RadioCardOption Value="enterprise"
                             Label="Enterprise"
                             Description="SSO, audit logs and a dedicated contact."
                             Meta="Custom"
                             Icon="{x:Static arc:ArcIcons.Shield}"
                             IsDisabled="True"
                             DisabledReason="Talk to sales to enable Enterprise." />
    </arc:RadioCards>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Back" Variant="Secondary" />
        <arc:Button Content="Continue with Pro" />
    </StackPanel>
</StackPanel>
```

### Content guidelines

- **Label**: the plan, speed or region name as a single word or short phrase: "Pro", "Express", "EU (Frankfurt)". Use
  sentence case for multi-word labels.
- **Description**: one line of what the choice gives the person, in plain words: "Unlimited projects and priority
  support", "2 to 3 business days". Keep it under about 60 characters so it fits two lines in a grid card.
- **Meta**: the price or time, with the currency sign and the unit in the description when it is not obvious:
  "$12", "Free", "Custom". Write numbers as digits.
- **Disabled reason**: the next step the person can take, not only the restriction: "Talk to sales to enable
  Enterprise." is better than "Not available".

| Instead of | Write |
|---|---|
| Pro plan (recommended) | Pro |
| Ships fast | 2 to 3 business days |
| 12 USD per month for the pro tier | $12 |
| Not available | Talk to sales to enable Enterprise. |

### Best practices

**Do**

- Put the price, time or other comparison in `Meta`, and keep the description for what the choice includes. A person
  compares the meta column first.
- Name the group with `AutomationProperties.Name` when no visible heading names it, so the choice is announced as
  "Plan" and not as an unnamed list of cards.
- Pick a default with `SelectedValue` when one option is the common choice, and say so in the description.
- Use `DisabledReason` for every disabled card, so a person knows why an option cannot be chosen and what to do.

**Avoid**

- Long descriptions in a grid of three or four cards. Two lines is the design; a paragraph makes the row tall and the
  comparison slow.
- Using the grid for options with no description or price. A `RadioGroup` is quicker to read for a short list.
- Removing a disabled option from the collection when the person needs to know it exists. Keep it visible with its reason.
- Setting `IsRequired` without a visible prompt. The attribute is reported to assistive technology, and the form still
  needs a message that says a plan must be chosen.

### Common questions

**What is selected when I do not set `SelectedValue`?** Nothing. The group has no selection until the person picks a card
or your code sets `SelectedValue`, and Tab then focuses the first available card.

**How does the keyboard move between cards?** The group is one tab stop. Arrow keys select the next or previous available
card, and Enter or Space selects the focused card. The arrow keys skip disabled cards, and Home and End select the first and
last available card. The keyboard map is in Accessibility.

**Why does the ring glide instead of jumping?** The ring is one shape that glides between the selected card's position
on a spring, so the eye follows the change. When the layout changes size, the ring moves to the new position at once.

**How do I show a card that is disabled but still selected?** Set `IsDisabled` and `SelectedValue` on the same option.
The card then shows its reason in place of the description, and the ring stays on it. Usually it is better to change the
selection in the view model before disabling an option.

**Can a card hold a control, such as a custom icon or a badge?** `Icon` accepts a `Geometry` or any content, and `Meta`
accepts any object, so a badge can be the meta content. Keep the content short, because the card's height is shared with
its row.

## API reference

<!-- api: RadioCards -->
<!-- api: RadioCardOption -->

## Accessibility

- Each card is a radio button to assistive technology. Set `AutomationProperties.Name` on the group when no visible header names it.
- The card label is the card's name. The description is its help text. A disabled card reports its `DisabledReason` instead.
- The group is one tab stop. `IsRequired` is reported to assistive technology as required for the form.
- Right-to-left layouts mirror the arrow keys: Right moves to the previous card, and Left to the next.
- Keyboard focus draws a 2px outline offset by 2px. Pointer focus does not.
- The selection shows as a ring and a dot, so it does not depend on color alone.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus into the group on the selected card, or on the first available card when none is selected, and out of it. |
| `Right` / `Down` | Select and focus the next available card. The move wraps from the last card to the first. In right-to-left layouts, `Right` moves back. |
| `Left` / `Up` | Select and focus the previous available card. In right-to-left layouts, `Left` moves forward. |
| `Home` / `End` | Select and focus the first or the last available card. |
| `Enter` / `Space` | Select the focused card. |

## Motion

- The selection ring glides from card to card on a spring. It fades in the first time it is placed. When the layout
  changes size, it moves to the new position at once.
- The dot grows in from nothing when a card is chosen. The card background and border change color quickly.
- Reduced motion puts the ring and the dot in their final state at once and removes the color transitions. Off does the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The selection moves to another card | The ring glides to the card on a spring | The ring is at the card at once | The ring is at the card at once |
| The first selection is placed | The ring fades in quickly | The ring appears at once | The ring appears at once |
| A card is chosen | The indicator dot pops in on a snappy spring | The dot appears at once | The dot appears at once |
| The layout changes size | The ring moves to the new position at once | The ring moves at once | The ring moves at once |
| Hover, press or focus on a card | The card background and border change color quickly | Immediate | Immediate |

## Related

- `RadioGroup` for short options that are plain text, in a vertical list.
- `SegmentedControl` for two to four views that switch the content at once.
- `Checkbox` when the user may choose several options.
- `BillingToggle` for a monthly or yearly billing period.

| Component | Choose it when |
|---|---|
| `RadioGroup` | The options are short text, with no price or description |
| `SegmentedControl` | The choice switches a view and is not saved as a value |
| `Select` / `Combobox` | The list is long or needs filtering |
| `BillingToggle` | The choice is a billing period with a savings note |
| `Checkbox` | More than one option can be on at once |
