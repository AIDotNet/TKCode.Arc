---
title: Empty state
summary: A useful next step when there is nothing to show yet.
---

<Demo name="FirstUse" />

## When to use

- An empty list, a folder or a first-use screen where the next step is clear.
- A search or filter that returned no results, with actions to fix it.
- A state that changes in place, such as "No projects yet" turning into "Project created".

## When not to use

- The data is still loading: use `Skeleton`.
- An error on a page that already has content: use `Alert`.
- A card-shaped group of content or actions: use `Card`.

## Usage

`Title` and `Description` are required. Add `Action` for a call to action. The default icon is a folder.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:EmptyState Label="Projects"
                    Title="No projects yet"
                    Description="Projects keep your files, people and conversations in one place.">
        <arc:EmptyState.Action>
            <arc:Button Content="New project" Variant="Primary" />
        </arc:EmptyState.Action>
    </arc:EmptyState>
</UserControl>
```

### Icon

`Icon` takes a control, or a 24 by 24 Lucide geometry such as one of the `ArcIcons` glyphs. When the icon changes, the
old one fades out and the new one pops in.

```xml
<arc:EmptyState Title="No results"
                Description="Check the spelling or try a broader term."
                Icon="{Binding SearchIcon}" />
```

```csharp
empty.Icon = ArcIcons.Search;
```

### Action row

`Action` takes one button, or a panel with several buttons. The row is centered below the description.

<Demo name="ZeroResults" />

### Changing state

Keep the control mounted and change its properties. The title and description roll to the new copy, and the height
springs to fit it. Set `Action` to `null` to remove the buttons.

```csharp
empty.Icon = ArcIcons.Check;
empty.Title = "Project created";
empty.Description = "Invite your team or start adding files whenever you're ready.";
empty.Action = null;
```

## API reference

<!-- api: EmptyState -->

## Accessibility

- The control is a region landmark named by `Label`. Set `Label` for each empty state on a page.
- The title is a level 3 heading. The description follows it in the reading order.
- The icon is not exposed to assistive technology. The text carries the meaning.
- Buttons in `Action` keep their own focus and keyboard behavior.

## Motion

- A new title or description rises in from below with a soft blur, and the old text lifts away.
- The text block changes height on a smooth spring to fit the new copy.
- A new `Icon` pops in, and the icon block fades and scales in the first time it appears.
- Reduced motion fades the text and changes the height at once. The icon block does not animate in. Off applies every
  change at once.

## Related

- `Skeleton` while the content is still loading.
- `Alert` for an error or a notice on a page that already shows content.
- `Card` for a grid item with media, an owner and actions.
