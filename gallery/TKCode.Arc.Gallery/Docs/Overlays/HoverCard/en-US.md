---
title: Hover card
summary: Preview a person or link on hover or focus without leaving the page.
---

<Demo name="Mention" />

## When to use

- A mention, avatar or author name that opens a short profile.
- A link preview, where a click must still navigate to the link.
- Dense lists, where moving from one trigger to the next should open the next card at once.

## When not to use

- A plain text label: use `Tooltip`.
- Content with buttons or inputs: use `Popover`. Focus never enters a hover card, so its content stays read-only.
- The user's own account menu: use `UserMenu`.

## Usage

A `HoverCard` opens after a short hover or keyboard focus on its `Trigger`. Its content is a preview, usually a
`HoverCardProfile`.

### Profile card

```xml
<arc:HoverCard Placement="Bottom">
    <arc:HoverCard.Trigger>
        <arc:Button Variant="Ghost" Content="@maya" />
    </arc:HoverCard.Trigger>
    <arc:HoverCardProfile DisplayName="Maya Chen"
                          Role="Product designer, Payments"
                          Bio="Designs checkout and billing flows."
                          Meta="Lisbon · 14:20 local time">
        <arc:HoverCardProfile.Stats>
            <arc:HoverCardStat Label="Projects" Value="12" />
            <arc:HoverCardStat Label="Reviews" Value="48" />
        </arc:HoverCardProfile.Stats>
    </arc:HoverCardProfile>
</arc:HoverCard>
```

### Link preview

`arc:HoverCard.Attach` adds a card to any control, such as a plain text link.

<Demo name="LinkPreview" />

### Timing and width

`OpenDelay` and `CloseDelay` default to 500ms and 140ms. `CardWidth` sets the width of the card, and the card is
always limited by the space available in the window.

```csharp
card.OpenDelay = TimeSpan.FromMilliseconds(300);
card.CardWidth = 320;
```

### Showing from code

`Show` and `Hide` change `IsOpen` directly. `IsOpen` is two-way, and `Opened` and `Closed` report the state changes.

```csharp
card.Show();
card.Closed += (_, e) => log.Text = $"Closed by {e.Reason}";
```

### Dense list

Once a card is open, moving to the next trigger opens it almost at once with a fade only.

<Demo name="DenseList" />

## API reference

<!-- api: HoverCard -->

<!-- api: HoverCardProfile -->

<!-- api: HoverCardStat -->

## Accessibility

- While the card is open, its summary is written to the trigger's help text.
- Focus never enters the card. Keep its content read-only and put actions on the trigger or in a `Popover`.
- Tab focus opens the card at once, Enter skips the open delay, and Escape closes it. It stays closed while the pointer
  rests on the trigger.
- On touch, a tap toggles the card. Only one card is open at a time.

## Motion

- The card grows from its trigger edge: it scales from 0.96 to 1 and moves 4px toward the trigger on a smooth spring,
  with a separate fade.
- Profile rows settle in from 4px below one after another. The avatar fades in from a 4px blur once it has loaded.
- When another card closed less than 300ms ago, the next card opens after 80ms with a fade only.
- Reduced motion removes the scale and the movement and keeps a short fade. Off shows or removes the card at once.

## Related

- `Tooltip` for a short text label on hover or focus.
- `Popover` for a card with buttons or inputs, opened on click.
- `Avatar` for the picture shown in a profile.
