---
title: Card
summary: A contained group of related content and actions.
---

<Demo name="QuickLook" />

## When to use

- Browsable grid items such as projects, listings or posts that carry media, an owner and a status.
- Related content and footer actions that belong together as one unit.
- A card that should grow into a larger quick look (set `Details`) without leaving the page.

## When not to use

- A single number that needs context: use `MetricCard`.
- A card that expands in place without a dialog: use `ExpandableCard`.
- Content that has no card to grow from and needs a modal: use `Dialog`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Card Title="Harbour redesign"
              Description="New booking flow and room pages."
              Width="280" />
</UserControl>
```

### Media, owner and footer

`Media` is shown at the top and drifts in slightly on hover. `Avatar` and `Meta` form the byline, and `Action` is a
trailing footer control that stays separately clickable.

```xml
<arc:Card Title="Harbour redesign"
          Description="New booking flow and room pages."
          Meta="Maya Chen"
          Status="Updated 2 hours ago"
          Width="280">
    <arc:Card.Media>
        <Border Height="132" Background="#5B8DEF" />
    </arc:Card.Media>
    <arc:Card.Avatar>
        <arc:Avatar DisplayName="Maya Chen" Size="Small" />
    </arc:Card.Avatar>
    <arc:Card.Action>
        <arc:Button Content="Share" Variant="Ghost" Size="Small" />
    </arc:Card.Action>
</arc:Card>
```

### Status

`Status` is a short line such as "Updated 2 hours ago". When the text changes, the new words rise in one after another.

<Demo name="StatusUpdates" />

```xml
<arc:Card Title="Weekly digest"
          Description="Highlights from every team, every Friday."
          Status="{Binding LastUpdate}"
          Width="300" />
```

### Quick look

Set `Details` and the title becomes a button; the whole card then grows into a larger view on the same page.

<Demo name="Controlled" />

```xml
<arc:Card Title="Quarterly plan"
          Description="Goals, owners and dates for the next three months."
          IsDetailsOpen="{Binding IsPlanOpen, Mode=TwoWay}"
          Width="280">
    <arc:Card.Details>
        <TextBlock Text="Opened from code: IsDetailsOpen is two-way." TextWrapping="Wrap" />
    </arc:Card.Details>
</arc:Card>
```

Use `OpenDetails()` and `CloseDetails()` to open and close the quick look from code, and `DetailsOpenChanged` to learn why it closed.

```csharp
card.DetailsOpenChanged += (_, e) => log.Text = e.IsOpen ? "Open" : $"Closed ({e.Reason})";
card.OpenDetails();
card.CloseDetails();
```

### Without a quick look

Without `Details` the title is a plain heading and the card only lifts on hover.

<Demo name="WithoutQuickLook" />

## API reference

<!-- api: Card -->

## Accessibility

- The title is a level 3 heading. With `Details` set, the title is a button; Enter or Space opens the quick look.
- Escape closes the quick look and returns focus to the title. While it is open, Tab stays inside it.
- Footer actions keep their own focus. Give an icon-only action a name with `AutomationProperties.Name`, and set
  alternative text on images in `Media`.
- Changes to `Status` are read out politely. `CloseButtonName` sets the name of the quick look's close button.

## Motion

- On hover the card lifts 2px and its border and shadow deepen. The media slowly zooms to 1.04 (mouse only).
- With `Details`, the surface, media, title and byline fly into the quick look on one spring while the page dims behind
  a blur. Closing flies them back into the card.
- A new `Status` lifts the old line away while the new words rise in.
- Reduced motion removes the lift, zoom and morph; the quick look only fades. Off applies changes at once.

## Related

- `MetricCard` for one number with its context and change.
- `ExpandableCard` for content that expands in place, without a dialog.
- `Dialog` for a modal with no card to grow from.
- `EmptyState` for a grid or list that has no cards yet.
