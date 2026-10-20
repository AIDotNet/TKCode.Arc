---
title: Expandable card
summary: Give a dense card more room when requested.
---

## When to use

- A summary card, such as a plan, an order or a log entry, whose details are useful but not always needed.
- A card that should grow wider as well as taller when it opens, so the details have room.
- A status card whose summary line changes over time, such as a delivery status.

## When not to use

- Several mutually exclusive sections: use `Accordion`.
- A card with no details to reveal: use `Card`.
- Browsing several cards in the same place: use `CardStack`.

## Usage

The `Header` is the card title. The `Description` is the summary line under it. The `Content` holds the details.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandableCard Header="Pro plan"
                        Description="$24 per month · billed yearly"
                        CollapsedWidth="420">
        <StackPanel Spacing="8">
            <TextBlock Text="Unlimited projects and collaborators." />
            <TextBlock Text="Version history for a full year." />
            <TextBlock Text="Priority support with a four-hour response time." />
        </StackPanel>
    </arc:ExpandableCard>
</UserControl>
```

Without a width, the card fills its container.

### Growing sideways

`ExpandedWidth` caps the width while the card is open. The card grows from `CollapsedWidth` to `ExpandedWidth`
on the same spring as its height. When `ExpandedWidth` is not set, the card uses `CollapsedWidth` when open.

```xml
<arc:ExpandableCard Header="Order #4821"
                    Description="Shipped · arrives Thursday"
                    CollapsedWidth="360"
                    ExpandedWidth="520">
    <TextBlock Text="2 × Linen shirt, 1 × Canvas tote" />
</arc:ExpandableCard>
```

### Open by default

`IsExpanded` is two-way. Set it to `True` to start open, or bind it to a view model property.

```xml
<arc:ExpandableCard Header="Deploy failed"
                    Description="api-gateway · 2 minutes ago"
                    IsExpanded="True">
    <TextBlock Text="Health check on /ready timed out after 30s." TextWrapping="Wrap" />
</arc:ExpandableCard>
```

### Updating the summary

Change `Description` in code. Only the words that changed roll in; the rest of the line stays still.

```csharp
order.Description = "Out for delivery · arrives today";
```

### Events

`ExpandedChanged` is raised after the card opens or closes.

```csharp
card.ExpandedChanged += (_, _) =>
{
    logger.Info($"Plan details open: {card.IsExpanded}");
};
```

## API reference

<!-- api: ExpandableCard -->

## Accessibility

- The header is a button with the expand and collapse pattern. Its state, expanded or collapsed, is announced.
- The card's name is the `Header`. The `Description` is exposed as the help text of the header button.
- Enter and Space toggle the card from the header.
- Escape closes the card when focus is inside it, and returns focus to the header.
- The details are inert while the card is closed, so their controls cannot take focus.

## Motion

- Opening is one motion: the width and height grow together on a smooth spring without overshoot, and the chevron
  turns with it.
- The details fade in once the card has made room. On close, the details fade first and the card shrinks after.
- Changed words in the summary rise in from below. Old words lift away and fade.
- A second click mid-motion reverses from the current size.
- Reduced motion applies the end state at once, and the summary text is replaced without animation.

## Related

- `Accordion` for a list of mutually exclusive sections.
- `Card` for a card without expandable details.
- `ExpandableText` for a long paragraph that expands in place.
