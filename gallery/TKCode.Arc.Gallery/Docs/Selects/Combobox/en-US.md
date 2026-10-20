---
title: Combobox
summary: Search and select from a list without leaving the field.
---

## When to use

- Long lists that people search by name, such as time zones or countries.
- Options with aliases. `Keywords` let a user find an item by a term that is not in its label.
- Single-value choices where typing the answer is faster than scrolling.

## When not to use

- Short, fixed lists that need no typing: use `Select`.
- Choosing several values: use `MultiSelect`.
- Search that navigates content rather than setting a value: use `ExpandingSearch`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Combobox Label="Framework"
                  Width="280"
                  Description="Type to search.">
        <arc:SelectItem Content="Avalonia" Keywords="xaml dotnet" />
        <arc:SelectItem Content="AtomUI" Keywords="ant design" />
        <arc:SelectItem Content="MAUI" Keywords="xamarin" IsEnabled="False" />
        <arc:SelectItem Content="WinUI" />
    </arc:Combobox>
</UserControl>
```

### Bound options and value

Set `ItemsSource` to `SelectOption` records. `Keywords` are matched by the search but not shown. `SelectedValue`
is two-way and is `null` while nothing is chosen.

```xml
<arc:Combobox Label="Time zone"
              Width="320"
              ItemsSource="{Binding Zones}"
              SelectedValue="{Binding ZoneId, Mode=TwoWay}" />
```

```csharp
var zone = new Combobox
{
    Label = "Time zone",
    Width = 320,
    ItemsSource = cities.Select(c => new SelectOption(c.City, c.City)
    {
        Keywords = $"{c.Zone} {c.Offset}"
    }).ToArray()
};
zone.SelectionChanged += (_, _) => Console.WriteLine(zone.SelectedValue);
```

### Custom filter

By default the label, meta and keywords are matched as case-insensitive substrings. Set `FilterPredicate` to change
the match. It receives the item and the trimmed query.

```csharp
var prefix = new Combobox
{
    Label = "Starts with",
    ItemsSource = cities,
    FilterPredicate = (item, query) => item is string s
        && s.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)
};
```

### Placeholder and empty text

`Placeholder` is shown while the field is empty. While the user searches, the chosen label takes its place.
`EmptyText` is the status row shown when nothing matches.

```xml
<arc:Combobox Label="Country"
              Placeholder="Search countries"
              EmptyText="No country matches." />
```

### Clearing and focus

`Clear()` removes the selection and the query. `FocusInput()` moves focus to the field, which opens the list.

```csharp
zone.Clear();
zone.FocusInput();
```

## API reference

<!-- api: Combobox -->

<!-- api: SelectOption -->

## Accessibility

- `Label` names the field and its list. `Description` is the help text.
- The field is a combo box. Focus stays in the text input while the user moves the highlight with the arrows.
- Enter picks the highlighted option. Escape closes the list and drops the query, so the previous value stays.
- The empty state is announced politely. The clear button is named "Clear selection".
- The focus ring is drawn on the field. Caret, selection, IME and clipboard are the native `TextBox` behavior.

## Motion

- The list opens from above with a spring. Its height follows the number of matching results.
- The chosen label rises into the field from below and comes out of a soft blur.
- The clear button pops in while something is selected and scales down when pressed.
- Reduced motion keeps only the fade and changes the list height at once. Off switches everything directly.

## Related

- `Select` for short fixed lists without typing.
- `MultiSelect` when several values are chosen.
- `ExpandingSearch` when the search navigates content rather than setting a value.
- `Input` for free text that is not chosen from a list.
