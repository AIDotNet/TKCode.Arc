---
title: Search results
summary: Search across kinds of content with facet tabs and live counts, grouped results with highlighted matches, and keyboard navigation.
---

<Demo name="Preview" />

## When to use

- A search page for a workspace, a help center or an admin tool, where results come in several kinds.
- People need to narrow results to one kind, such as docs, issues, files or people, and see how many each kind holds.
- Results need context: where an item lives, a passage around the match and a short note such as the last update.

## When not to use

- Jumping to a page or running a command from anywhere in the app: use the command palette block.
- A compact search in a header or toolbar: use `ExpandingSearch`.
- Filtering a list that is already on the page: use `SearchField` next to the list.

## Usage

Give the control its items and a query. Every word of the query must match the title, the snippet, the location or a
keyword of an item. The facet row lists All and every kind, each with its number of results, and the list groups the
results by kind.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchResults Height="480" Query="invoice" Label="Search finance">
        <arc:SearchResultItem Title="Invoice template"
                              Kind="Docs"
                              Location="Finance / Templates"
                              Meta="Updated 1w ago"
                              Snippet="Use this template for every customer invoice."
                              Icon="{x:Static arc:ArcIcons.FileText}" />
        <arc:SearchResultItem Title="Invoice totals do not match the report"
                              Kind="Issues"
                              Location="FIN-88 · Reporting"
                              Meta="Open"
                              Icon="{x:Static arc:ArcIcons.CircleDot}" />
    </arc:SearchResults>
</UserControl>
```

### Items

Add `SearchResultItem` objects inline, or set `ItemsSource` to a list. When the list is an observable collection, the
results follow its changes. `Kind` decides the facet and the group; items without a kind are listed under `OtherLabel`.
`Icon` takes a Lucide geometry or any control, such as an `Avatar` for people. `Keywords` are extra words that also find
the item, and `Tag` is any value you want back.

```csharp
search.ItemsSource = new ObservableCollection<SearchResultItem>
{
    new() { Id = "q3", Title = "Q3 planning notes", Kind = "Docs", Location = "Product / Planning", Snippet = notes },
    new() { Id = "fn-482", Title = "Sync stalls after sleep", Kind = "Issues", Keywords = ["FN-482"] },
    new() { Id = "maya", Title = "Maya Lindqvist", Kind = "People", Icon = new Avatar { DisplayName = "Maya Lindqvist", Size = AvatarSize.Small } }
};
```

If you change an item in place, call `Refresh()` to run the search again.

### Matching, ranking and highlights

Results whose title starts with the first word come first, then titles where a word starts with it, then titles that
contain it, then everything else. Results with the same rank keep the order of the items. Every match in the title and
the snippet is marked. A long snippet is cut to a window that starts a little before the first match, with an
ellipsis at each cut end.

### Facets and groups

`SelectedKind` is the current facet, or null for All. It is two-way, so you can bind it to a route or a view model.
Under All, each kind shows its first `GroupPreviewCount` results and a "See all" link that opens its facet. A single
facet shows `PageSize` results and a "Show more" button. A facet without results for the query stays in place with
dimmed text.

<Demo name="FacetsAndEvents" />

### Opening results

Enter or a click opens a result: `ItemInvoked` is raised and `InvokeCommand` runs with the item. `ActiveItem` is the
result under the keyboard cursor. Results with `IsEnabled` set to false are dimmed, skipped by the cursor and cannot
be opened.

```csharp
search.ItemInvoked += (_, e) => Navigate(e.Item.Id);
```

```xml
<arc:SearchResults ItemsSource="{Binding Results}"
                   Query="{Binding Query}"
                   SelectedKind="{Binding Kind}"
                   InvokeCommand="{Binding OpenCommand}" />
```

### Server-side search

When a server searches, set `IsFilteringEnabled` to false. The items are then shown in the order you give and counted
per kind, and the words of the query are still marked. Set `IsLoading` while the request runs: skeleton rows take the
place of the results and keep their height.

<Demo name="ServerSearch" />

### No results

When nothing matches, an empty state shows the query. If the current facet is empty but another one is not, it names
the kind with the most results and offers "Search all". `Suggestions` adds queries to try; choosing one fills the
search field.

<Demo name="NoResults" />

### Narrow layouts

Below 560 pixels the frame tightens and each result moves its note into the location line. The facet row scrolls
inside its track, with faded edges where more facets are hidden.

<Demo name="Narrow" />

### Texts

Every text has an English default and a property to change it. Formats use `{0}` for the count or the query.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchResults Label="Search the help center"
                       PlaceholderText="Search articles"
                       AllLabel="Everything"
                       ResultsFormat="{}{0} articles"
                       OneResultText="1 article"
                       NoResultsText="No articles"
                       EmptyTitleFormat="Nothing found for “{0}”"
                       SuggestionsLabel="Try searching for" />
</UserControl>
```

## API reference

<!-- api: SearchResults -->

<!-- api: SearchResultItem -->

## Accessibility

- The control is a search landmark named by `Label`, which also names the search field.
- The facets are a tab list. Only the selected facet is a tab stop. ArrowLeft and ArrowRight move between facets and
  select them, and Home and End jump to the first and the last. Each facet is named with its kind and its count.
- The results are a list, and the result under the cursor is its selected item. Each result is named with its title,
  kind and location. Group headings are level 3 headings.
- In the search field, ArrowDown moves into the results and Enter opens the result under the cursor, or the first
  result. In the results, ArrowUp, ArrowDown, Home, End, PageUp and PageDown move the cursor, ArrowUp on the first result
  returns to the field, Enter or Space opens a result and Escape returns to the field.
- Escape in the field clears the query. Ctrl+F (Cmd+F on macOS) or `/` moves focus to the field while focus is inside the
  control, and typing in the results continues the query in the field.
- The stats line is a polite live region, so the number of results is announced as it changes.

## Motion

- The cursor glides between results: on a spring when it follows the pointer, and with a 70ms tween when it follows the
  arrow keys. It fades in when it first appears and fades out when it is cleared.
- New results fade in and rise 4 pixels, 35ms apart. Results that were already shown stay still.
- Facet counts roll to their new values, and the selected facet's block slides between facets on the morph spring.
- The stats text crossfades, and the results, the skeleton rows and the empty state fade into each other.
- Reduced motion keeps the fades and drops every movement: the cursor and the facet block jump, counts change at once and
  results only fade in. Off applies every change at once.

## Related

- The command palette block for actions and navigation from anywhere in the app.
- `ExpandingSearch` for a compact search in a header.
- `SearchField` for filtering a list on the page.
- `EmptyState` and `Skeleton`, which this block uses for no results and loading.
