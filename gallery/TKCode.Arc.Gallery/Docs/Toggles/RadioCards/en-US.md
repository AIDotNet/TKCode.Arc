---
title: Radio cards
summary: Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.
---

<Demo name="Grid" />

## When to use

- Options that need a description, a price or an estimate before the user can decide, such as plans, shipping speeds or regions.
- Options that need a reason shown when they cannot be chosen, through `DisabledReason`.
- A required choice among a short list of options.

## When not to use

- Short text-only options: use `RadioGroup`.
- Two to four views that switch the content at once: use `SegmentedControl`.
- Long lists: use `Select`.

## Usage

The cards are the `Items` of the control. Each `RadioCardOption` takes a value, a label, a description and a meta
text, which is shown at the end of the card.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RadioCards SelectedValue="pro">
        <arc:RadioCardOption Value="starter" Label="Starter" Description="For individuals getting started." Meta="$0" />
        <arc:RadioCardOption Value="pro" Label="Pro" Description="Unlimited projects and priority support." Meta="$12" />
        <arc:RadioCardOption Value="team" Label="Team" Description="Shared workspaces and roles for small teams." Meta="$32" />
    </arc:RadioCards>
</UserControl>
```

### Bound options

Bind `ItemsSource` to a list of options and `SelectedValue` to the selected value. Both are two-way.

```xml
<arc:RadioCards ItemsSource="{Binding Plans}"
                SelectedValue="{Binding PlanId}"
                AutomationProperties.Name="Plan" />
```

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

### List layout

`Layout="List"` stacks the cards as full-width rows with the meta at the end. Long descriptions wrap. The default
`Grid` layout fills columns that are at least `MinColumnWidth` wide (180 by default).

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

## API reference

<!-- api: RadioCards -->
<!-- api: RadioCardOption -->

## Accessibility

- Each card is a radio button to assistive technology. Set `AutomationProperties.Name` on the group when no visible header names it.
- The card label is the card's name. The description is its help text. A disabled card reports its `DisabledReason` instead.
- Tab focuses the selected card, or the first available card. Arrow keys move the selection and wrap around, and Home
  and End select the first and last card. Enter and Space select the focused card. Right-to-left layouts mirror the arrows.
- Keyboard focus draws a 2px outline offset by 2px. Pointer focus does not.
- The selection shows as a ring and a dot, so it does not depend on color alone.

## Motion

- The selection ring glides from card to card on a spring. It fades in the first time it is placed. When the layout
  changes size, it moves to the new position at once.
- The dot grows in from nothing when a card is chosen. The card background and border change color quickly.
- Reduced motion puts the ring and the dot in their final state at once and removes the color transitions. Off does the same.

## Related

- `RadioGroup` for short options that are plain text, in a vertical list.
- `SegmentedControl` for two to four views that switch the content at once.
- `Checkbox` when the user may choose several options.
- `BillingToggle` for a monthly or yearly billing period.
