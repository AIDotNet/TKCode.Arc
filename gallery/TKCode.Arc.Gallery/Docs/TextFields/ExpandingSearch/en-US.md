---
title: Expanding search
summary: An icon that morphs into a search field with results beneath it.
---

## When to use

- A header or toolbar where a full search field would take too much room.
- Jumping to a document, project or page from a list of results.
- Showing recent searches before the user types anything.

## When not to use

- Filtering a list that is already on the page: use `SearchField`.
- Choosing a value for a form: use `Combobox`.
- A global command search across the whole app: use a command palette, which this library does not provide.

## Usage

The control starts as a round icon button. Activating it opens the field and shows the results or the suggestions.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandingSearch Label="Search projects and docs"
                         MaxResults="6"
                         ExpandedWidth="360" />
</UserControl>
```

### Items and suggestions

Fill `ItemsSource` with `ExpandingSearchItem` objects. Each item has an `Id`, a `Title`, and optional `Meta`,
`Group` and `Keywords`. Results are matched against the title and the keywords, and listed by group.
`Suggestions` are shown before anything is typed.

```csharp
var items = new List<ExpandingSearchItem>
{
    new() { Id = "q3", Title = "Quarterly report", Meta = "Doc · Edited 2 hours ago", Group = "Docs" },
    new() { Id = "atlas", Title = "Atlas redesign", Meta = "Project · 12 open tasks", Group = "Projects" },
    new() { Id = "billing", Title = "Billing migration", Group = "Projects", Keywords = ["invoices", "payments"] }
};

header.ItemsSource = items;
header.Suggestions = new[] { items[0], items[1] };
header.SuggestionsLabel = "Recent";
```

### Selection

`ItemSelected` is raised when a result or suggestion is chosen, after the field folds back. Bind `SelectCommand`
to run a command with the chosen item instead.

```csharp
header.ItemSelected += (_, e) => Open(e.Item.Id);
```

```xml
<arc:ExpandingSearch Label="Search"
                     ItemsSource="{Binding Items}"
                     SelectCommand="{Binding OpenCommand}" />
```

### Anchor and width

`Anchor` sets the edge the button sits on. The field grows away from that edge up to `ExpandedWidth`.

```xml
<arc:ExpandingSearch Label="Find a dashboard"
                     Anchor="Start"
                     ExpandedWidth="300" />
```

### Open and close from code

`Expand` opens the field and moves focus into it. `Collapse` folds it back into the icon and clears the query.

```csharp
search.Expand();
search.Collapse();
```

`IsExpanded` is two-way, so you can also bind it to the view model.

## API reference

<!-- api: ExpandingSearch -->

<!-- api: ExpandingSearchItem -->

## Accessibility

- `Label` names the button, the field and the results.
- The results are a list grouped by heading. The number of results and the empty state are announced politely.
- Enter or Space on the button opens the field. ArrowDown and ArrowUp move through the results and wrap around.
- Enter chooses the highlighted result and Escape folds the field back into the icon.
- The collapsed field is hidden from focus and from hit testing, so it does not block the content beneath it.

## Motion

- Opening morphs the width and turns the round icon into a rounded field on the same spring. Closing is smoother and shorter.
- The results panel follows the field's width, opens its height smoothly and fades in.
- The highlight slides between results and keeps the speed of the previous move.
- Reduced motion switches the states with a fade and no width or position movement. Off switches them at once.

## Related

- `SearchField` for a field that filters content already on the page.
- `Combobox` for choosing a value that is stored in a form.
- `Input` for a general single-line field.
