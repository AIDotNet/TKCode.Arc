---
title: Search field
summary: A recognizable search entry point with clear affordances.
---

## When to use

- Filtering a list, table or toolbar that is already on the page.
- A search entry point with a clear button that the user can reach with the keyboard.
- Queries whose results are shown in the same view as the field.

## When not to use

- A compact header search that opens a results panel: use `ExpandingSearch`.
- A search whose value is stored in a form: use `Combobox`.
- Several filters and sort options side by side: put the field in a toolbar with the other controls.

## Usage

The field has a search icon at the start. A clear button appears at the end while the field has text.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchField Label="Search people"
                     Placeholder="Filter by name"
                     Width="360"
                     HorizontalAlignment="Left" />
</UserControl>
```

### Filter as the user types

`Text` changes on every key press. Filter the list in the `TextChanged` handler. If filtering is expensive,
debounce it in your code.

```csharp
search.TextChanged += (_, _) =>
{
    var query = search.Text ?? string.Empty;
    list.Children.Clear();
    foreach (var name in names.Where(n => n.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
    {
        list.Children.Add(new TextBlock { Text = name });
    }
};
```

### Bind the query

`Text` is a two-way property, so the view model can read or reset the query.

```xml
<arc:SearchField Label="Search"
                 Placeholder="Filter by name"
                 Text="{Binding Query}" />
```

### Submit

Enter raises `SearchSubmitted` with the text in the field. Escape clears the field.

```csharp
search.SearchSubmitted += (_, e) => submitted.Text = $"Submitted “{e.Query}”";
```

Call `Clear` to clear the field from code. The focus stays in the field unless you pass `false`.

```csharp
search.Clear();
```

### Sizes

`Size` sets the frame height: `Small` is 36, `Medium` (the default) is 44 and `Large` is 50.

```xml
<arc:SearchField Label="Small" Size="Small" Placeholder="Search" />
<arc:SearchField Label="Large" Size="Large" Text="Roadmap" />
```

## API reference

<!-- api: SearchField -->

## Accessibility

- `Label` names the field. The search icon is hidden from assistive technology.
- The clear button is named "Clear search" by default. Set `ClearText` to translate it.
- Enter submits the query and Escape clears the field from the keyboard.
- The clear button is reachable with the keyboard and shows the focus ring when focused.

## Motion

- The clear button scales in from a slightly smaller size with a light blur.
- Its slot is always reserved, so the field keeps its width whether or not the button is visible.
- Pressing the clear button scales it down slightly.
- Reduced motion only fades the clear button, with no scale and no blur. Off shows and hides it at once.

## Related

- `ExpandingSearch` for a compact search in a header that shows results beneath it.
- `Combobox` for a value chosen from a list.
- `Input` for a general single-line field.
