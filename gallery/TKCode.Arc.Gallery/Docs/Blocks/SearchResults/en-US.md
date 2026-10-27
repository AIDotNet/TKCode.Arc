---
title: Search results
summary: A workspace search page with a suggestions popup, filters whose counts follow the query, a date range, sorting, removable filter chips, highlighted results and a detail sheet.
---

<Demo name="Preview" />

## When to use

`SearchResults` is the search page of a workspace: a large search field with a popup of recent and suggested searches, a
column of filters whose counts follow the query, a header with the number of results and a sort switch, and one list of
results, people and documents together, with the matched words marked. It fills the main content area of an application and
answers one question for a query that can span several kinds of data: what matches, of which kind, and where it lives. Choosing
a result opens a detail without leaving the page.

- **A help center or knowledge base**, where articles, how-to guides and release notes sit side by side. A customer who
  types "refund" ticks the kind that fits the question, and the counts beside every value show where the answers are before
  they click.
- **A project workspace that indexes documents, files and people.** The Team filter narrows "onboarding" to one group, the
  date range hides what nobody has touched this year, and each result states where it lives ("Doc in Support / Onboarding") so
  the right item is easy to pick.
- **A support console that searches tickets and macros on a server.** Set `IsFilteringEnabled` to false, show the server's
  order and the server's counts, and keep the highlights, the chips and the detail the control already draws.
- **An operations tool that searches incidents, runbooks and dashboards.** An on-call engineer presses `/`, types a service
  name, presses ArrowDown and opens the runbook with Enter, without leaving the keyboard.
- **A finance back office that searches invoices, customers and exports.** A result carries a passage around the match, a
  last-edited date and, in the detail, the owner and the size, so the person can tell two similar invoices apart.

## When not to use

- **Jumping to a page or running an action from anywhere in the app.** Use `CommandPalette`, which opens over the
  whole window and lists commands next to pages.
- **A compact search in a header or toolbar.** Use `ExpandingSearch`, which stays an icon until it is opened and takes
  no room in the page.
- **Filtering a list that is already on the page.** Use `SearchField` above the list and filter the list in code. A
  list of a few dozen rows does not need filters, counts or a detail.
- **Choosing one value from a short known list**, such as a status or a country. Use `Select` or `Combobox`; a search
  page with filters is too much for a choice of five.
- **Browsing records by column with sorting and filters.** Use `Table` with `FilterToolbar`. Results in
  `SearchResults` are read and opened, not compared across columns.

## Usage

Give the control its items and, for the left column, the filters you want. Every word of the query must match the title, the
subtitle, the snippet, the caption, the location, the body or a keyword of an item. The control matches, filters, counts and
sorts the items itself, unless you turn that off and let a server do it.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SearchResults Height="640" Query="invoice" Label="Search finance">
        <arc:SearchResults.Filters>
            <arc:SearchValueFilter Key="kind" Title="Type" />
            <arc:SearchValueFilter Key="team" Title="Team" />
            <arc:SearchDateFilter Key="edited" Title="Edited" AppliesTo="Docs, Files" />
        </arc:SearchResults.Filters>
        <arc:SearchResultItem Title="Invoice template"
                              Kind="Docs"
                              KindLabel="Doc"
                              Location="Finance / Templates"
                              Snippet="Use this template for every customer invoice."
                              Icon="{x:Static arc:ArcIcons.FileText}" />
    </arc:SearchResults>
</UserControl>
```

`SearchResults` derives from Avalonia's `TemplatedControl`, so it takes the usual styling and theme resources. It is built
from Arc parts: its own search field (a styled `TextBox`), `Checkbox` rows, a two-thumb `Slider` for the date range,
a `SegmentedControl` for the sort order, `Avatar` for people, `EmptyState` for no results, `Skeleton` for loading and a
`ScrollArea` for the list. Each result is a `SearchResultItem`, an `AvaloniaObject` whose properties can be bound like any
other. The control sets the pseudo-classes `:narrow`, `:loading`, `:empty`, `:no-filters` and `:filtered` for styling.

### Anatomy

1. **Search field** (`Label`, `PlaceholderText`, `ShortcutText`): 50 px high with an 18 px radius, a magnifier, the text,
   a 24 px clear button that appears with the first character and a 24 px keycap with the key that focuses the field
   ("/" by default; it hides while the field has focus). The fill is muted with a subtle border; hover and focus use the
   strong border.
2. **Suggestions popup** (`Recent`, `Suggestions`, `RecentLabel`, `SuggestedLabel`): opens under the focused field, as wide as the
   field and 8 px below it. Two groups, "Recent" with a clock and "Suggested" with a magnifier, in 40 px rows with a 14 px
   radius. The row that equals the query shows a return key.
3. **Filters column** (`Filters`, `FiltersTitle`, `ClearAllText`): 244 px wide with a divider. A title with a "Clear all" link
   that fades in once a filter is active, then one group per filter: 34 px checkbox rows with the count right-aligned, and
   the date range with its two thumbs and a caption. Without filters the column is absent and the results take the whole width.
4. **Results header** (`ResultsFormat`, `OneResultFormat`, `ElapsedFormat`, `SortOrder`): the count in medium weight with a
   rolling number, the time it took in the muted color, and the sort switch on the right ("Best match", "Newest", "A to Z").
5. **Active filters bar** (`RemoveFilterText`, `ClearAllText`): a rounded bar under the header that opens and closes with a
   height animation. Every ticked value and narrowed range is a 34 px pill such as "Type · People" with a cross, and
   "Clear all" sits at the end.
6. **Result row** (`SearchResultItem`): a 36 px leading column (a round `Avatar` for people, a 20 px stroked icon for
   everything else), the title in medium weight, the subtitle in muted extra-small type, a snippet of up to two lines in
   the secondary text color and a muted caption. Rows have 20 px side padding, a 24 px radius and sit 4 px apart.
7. **Cursor**: one highlight block behind the active row, moved by the keyboard or the pointer. The suggestions popup has
   its own, of the same kind.
8. **Key hints** (`IsHintsVisible`, `HintMoveText`, `HintOpenText`, `HintSearchText`): small keycaps under the results,
   "↑ ↓ to move, ↵ to open, / to search", shown while results are listed.
9. **Detail** (`OpenedItem`, `DetailTemplate`): a dialog on wide screens, a bottom sheet on narrow ones, with the kind, a
   round close button, the title, the subtitle, the body, a table of facts and two buttons.
10. **Empty state and skeleton rows**: a centered `EmptyState` with the search icon, or three skeleton rows while loading.

The frame has a 1 px border and the surface radius. The field row has 24 px padding (16 px when narrow) and a 1 px rule under it.
Below 560 px the narrow layout applies.

### Items

Add `SearchResultItem` objects inline, or set `ItemsSource` to a list. When `ItemsSource` is null the inline `Items`
collection is used. When the list is an observable collection, the results follow its changes. A result is made of these
properties:

- `Id` is the stable key of the item, such as the record's own identifier. When it is empty the title is used instead.
- `Title` is the name of the result. It is marked where it matches and it decides the ranking.
- `Subtitle` is the line under the title, such as a person's role and city. Without one the row writes "`KindLabel` in
  `Location`" (the pattern is `KindLocationFormat`), or just the location.
- `Snippet` is a passage of the content. Long passages are cut to a window around the first match.
- `Caption` is a last muted line under the snippet, such as an email address.
- `Kind` is the type of the result ("Docs", "People", "Files"): the value the filter with the key `kind` reads. `KindLabel`
  is its singular name ("Doc"), used in the subtitle and the detail; it falls back to `Kind`.
- `Location` is a path or a breadcrumb such as "Engineering / Handbook". It is searched too.
- `Icon` takes a 24 by 24 Lucide geometry or any control. Without an icon the result uses the FileText glyph. A person
  shows a round avatar instead when `AvatarName` (initials) or `Avatar` (a photo) is set.
- `Keywords` are extra words that also find the item: synonyms, ticket numbers and tags. Keywords are searched but not
  shown.
- `Date` is when the item was last edited. It sorts "Newest" and feeds the date range.
- `Attributes` holds the values the other filters read, by filter key (`["team"] = "Design"`).
- `Body`, `Url` and `Details` fill the detail: the paragraphs of text, the address "Copy link" copies and the rows of the
  table of facts.
- `IsEnabled` set to false lists the item dimmed. It is skipped by the keyboard cursor and cannot be opened.
- `Tag` is any value you want back when the item is opened.

```csharp
search.ItemsSource = new ObservableCollection<SearchResultItem>
{
    new()
    {
        Id = "q3",
        Title = "Q3 planning notes",
        Kind = "Docs",
        KindLabel = "Doc",
        Location = "Product / Planning",
        Snippet = excerpt,
        Body = fullText,
        Date = new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero),
        Url = "https://example.com/docs/q3",
        Attributes = { ["team"] = "Product" },
        Details = { new SearchResultDetail { Label = "Author", Value = "Maya Lindqvist", AvatarName = "Maya Lindqvist" } }
    },
    new()
    {
        Id = "maya",
        Title = "Maya Lindqvist",
        Kind = "People",
        KindLabel = "Person",
        Subtitle = "Product lead, Stockholm",
        Caption = "maya@example.com",
        AvatarName = "Maya Lindqvist",
        Attributes = { ["team"] = "Product" }
    }
};
```

If you change an item in place, call `Refresh()` to run the search and the counts again. Adding or removing items from an
observable collection does not need it, and neither do changes to the `Filters` list, to the values of a filter or to a
filter's `Key` and date range.

### Matching, ranking and highlights

The query is split on white space into distinct words, and an item matches when every word occurs in its title, subtitle,
snippet, caption, location, body or one of its keywords. A word may be found in different fields. The comparison ignores
case, but it does not fold accents: "resume" does not find "résumé". An empty query lists every item. The rows of `Details`
are only shown and the `Attributes` only feed the filters; neither is searched.

Ranking uses only the first word of the query, and applies to "Best match". Results keep their source order inside each
rank.

| Rank | The first word is found | Example for "release" |
|---|---|---|
| 1 | At the start of the title | "Release checklist" |
| 2 | At the start of a word in the title | "Q3 release notes" |
| 3 | Inside a word of the title | "Prerelease audit" |
| 4 | Only in the subtitle, snippet, caption, location, body or keywords | "Sync design: the release queue" |

Every occurrence of every word is marked in the title, the subtitle, the caption and the snippet: accent-subtle fill,
primary text and medium weight. Overlapping marks merge.

A long snippet is cut around the first match: it starts at the beginning when the match fits in the first lines, else
about 36 characters before the match, at a word start. It is at most 220 characters and two lines, with an ellipsis at each
cut end. When only the `Body` contains the words, the row shows a passage of the body in place of the snippet, so a result
always shows why it matched.

### Filters

`Filters` holds the filters of the column, and `SearchFilter` is their base. A `SearchValueFilter` is a group of checkboxes
over one attribute. Its `Key` is `kind` to read `SearchResultItem.Kind`, otherwise the name of an entry in
`SearchResultItem.Attributes`. Leave `Values` empty and the filter lists the values it finds, in the order they first appear
(new values that later items bring are appended); list them yourself as `SearchFilterValue` objects to fix their order and
labels. Several ticked values of one filter combine with OR, and the filters combine with AND.

Every value shows how many results it would show with the current query. The count ignores that filter's own ticks, as in
faceted search, so ticking "Docs" does not make "Files" read 0. It follows the query and the other filters while you type.
A value with no results keeps its place, dimmed, and cannot be ticked (unless it is already ticked, so it can be removed).

A `SearchDateFilter` is a two-thumb range slider over `SearchResultItem.Date`. The range spans the oldest and the newest
date of the items it applies to, or `Minimum` and `Maximum` when you set them. `Start` and `End` are the chosen ends (null:
that end of the range, and moving a thumb back to its end sets null again). `AppliesTo` names the kinds it concerns as a
comma separated list; items of other kinds pass, so a range over documents does not hide people, and the caption under the
slider says so ("Applies to docs and files"). While a range is narrowed, an item of an applying kind without a date is hidden. A date filter always reads `Date`; its `Key`
only names it.

<Demo name="Filters" />

Ticked values and narrowed ranges appear as chips in the bar under the results header. A chip's cross removes its filter,
and "Clear all" (in the bar and in the column) removes them all. In code you can read and set a value's `IsSelected` and a date
filter's `Start` and `End`, call `ClearFilters()`, and read `HasActiveFilters` and `ActiveFilterCount`. The control reads at
most 64 filters.

### Sorting and the results header

The header shows how many results there are, how long the search took, and a sort switch. `SortOrder` is two-way: best
match, newest (`SearchResultItem.Date`, items without a date last) or A to Z (by title, using the current culture). The control
cannot measure the time, so you set `Elapsed` when your search finishes; leave it null to show only the count. Both numbers
roll to their new value. When nothing matches, the header shows `NoResultsText` instead.

### Suggestions and recent searches

Focusing the field opens a popup under it with the recent searches and your `Suggestions` (at most six, and none that are
already in Recent). The control adds a query to `Recent`, newest first and without duplicates, whenever one is submitted
(Enter, a suggestion or a "Try" chip) or a result is opened with it. `MaxRecent` caps the list. Seed `Recent` before the page
shows and save it from its `CollectionChanged` event; it is an `AvaloniaList<string>`, not a bindable property.

Arrow keys move through the rows, Enter runs the highlighted one or the text as typed, Escape closes the popup, and a click on
a row fills the field and submits it. Once you edit the text, the popup lists only the entries that contain it, and it closes
itself when nothing is left. `QuerySubmitted` tells you about every submitted query. Set `IsSuggestionsEnabled` to false for a
field that never opens the popup.

<Demo name="Suggestions" />

### Binding to a view model

The view model owns the query, the sort order, the items and the open command. The control owns the cursor, the filters'
ticks and the layout.

```csharp
public partial class WorkspaceSearchViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public WorkspaceSearchViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private SearchSortOrder _sort = SearchSortOrder.BestMatch;

    [ObservableProperty]
    private TimeSpan? _elapsed;

    public ObservableCollection<SearchResultItem> Results { get; } = [];

    [RelayCommand]
    private void Open(SearchResultItem? item) => _navigation.Navigate(item?.Tag);
}
```

```xml
<arc:SearchResults Label="Search the workspace"
                   ItemsSource="{Binding Results}"
                   Query="{Binding Query}"
                   SortOrder="{Binding Sort}"
                   Elapsed="{Binding Elapsed}"
                   IsDetailEnabled="False"
                   InvokeCommand="{Binding OpenCommand}" />
```

- `Query` and `SortOrder` are two-way, so typing updates the view model and a link can open the page with a query
  already filled in.
- `ItemsSource` and `Elapsed` are one-way. Keep `Results` as an `ObservableCollection` and change it in place.
- `InvokeCommand` runs with the opened item as its parameter, and the control checks `CanExecute` first. This view model
  navigates, so the sample turns the built-in detail off; keep it on and bind the two-way `OpenedItem` when the view model
  should know which detail is open.
- `Filters` and `Recent` are collections you fill and read, not properties you bind.

### Opening results

Enter or a click on a result raises `ItemInvoked` with the item, runs `InvokeCommand` with the same item, and then opens the
built-in detail. `ActiveItem` is the result under the keyboard cursor, the first enabled result until you move it, so a status
line can describe what Enter will open. Results with `IsEnabled` set to false are dimmed, skipped by the cursor and cannot be
opened. When your app navigates instead of showing the detail, set `IsDetailEnabled` to false, or set `Handled` on the event.

```csharp
search.ItemInvoked += (_, e) =>
{
    Navigate(e.Item.Id);
    e.Handled = true; // skip the built-in detail
};
```

The detail is a dialog on wide screens and a bottom sheet with a grab handle on narrow ones. It shows the kind of the result
with a round close button, the title with the matches marked, the subtitle, the paragraphs of `Body` (separated by blank lines;
the snippet when there is no body), a table made of a first "Matches" row (how often the query is marked in the title and
the body) and the item's `Details`, and two buttons. "Open" (`OpenFormat`, with the kind label in lower case) and "Copy link"
raise `DetailActionInvoked`; "Copy link" also puts `Url` on the clipboard. Escape, the close button or a press on the dimmed page
closes it, and focus returns to where it was.

`OpenedItem` is two-way: set it, or call `ShowDetail(item)`, to open a detail from code, and `CloseDetail()` closes it.
`DetailTemplate` replaces the content between the header and the buttons; its data is the `SearchResultItem`.

<Demo name="Detail" />

```csharp
search.DetailActionInvoked += (_, e) =>
{
    if (e.Action == SearchResultAction.Open)
    {
        Navigate(e.Item.Id);
        search.CloseDetail();
    }
};
```

### Server-side search

When a server searches, set `IsFilteringEnabled` to false. The items are then shown in the order you give, the counts on the
filter values are yours to set, and the words of the query are still marked. `SearchChanged` tells you, with a `Reason` of
`Query`, `Filters` or `Sort`, that something changed and you should ask again. Set `IsLoading` while the request runs: skeleton
rows take the place of the results and keep the height of the previous results, so the page does not jump, the count line
reads "Searching…" and the cursor and the hints go away. Debounce the query before you call the server, and cancel the request
that a newer keystroke overtakes. Without a debounce, every keystroke starts a request and the results flicker.

<Demo name="ServerSearch" />

### No results

When nothing matches, an empty state shows the query in its title (`EmptyTitleFormat`, with `{0}` for the query) and the
description (`EmptyDescription`). The count line shows `NoResultsText`. With an empty query and no items the title also uses
`NoResultsText`.

If filters are active, the description becomes `EmptyFilteredDescription`, "The filters leave nothing. Remove one to see
more.", and a "Clear filters" button (`ClearFiltersText`) removes them. `Suggestions` adds queries to try, shown as buttons
after `TryText` ("Try" by default). Choosing one fills the search field and runs the search.

<Demo name="NoResults" />

### Narrow layouts

Below 560 pixels of width the field row tightens from 24 to 16 pixels of padding, the filters column folds into a "Filters"
button that carries the number of active filters, and the button opens the same filters in a bottom sheet (`OpenFiltersSheet()`
does the same from code). The sort switch moves under the count, and the detail becomes a bottom sheet that is at most 92 percent
of the window high. With no filters at all, the narrow layout keeps the sort switch on the right. The filters sheet closes by
itself when the control becomes wide again.

<Demo name="Narrow" />

### Long lists

Only the first `PageSize` results (15 by default) are built. More follow when the list is scrolled within 240 pixels of its
end, when "Show more" (`ShowMoreText`) is pressed, or when ArrowDown or PageDown is pressed on the last listed result. A new
query, filter or sort order starts from the top again.

Typing stays fast with thousands of items. A query that only grows searches the previous matches instead of every item; ticking
a filter does not search the text again; the counts are made in the same pass as the filtering; and rows that stay are the same
controls, so a keystroke re-lays out only what changed. Searching and filtering 5,000 items takes a millisecond or two on a
development laptop, and most of the cost of a keystroke is laying out the rows that changed.

### Texts

Every text has an English default and a property to change it. Formats use `{0}` for the count, the seconds, the query or the
kind, and `{1}` where a second value is named.

| Property | Default | Used for |
|---|---|---|
| `Label` | "Search" | The landmark and the search field name |
| `PlaceholderText` | "Search" | The hint in the empty field |
| `ShortcutText` | "/" | The key on the field's keycap and in the hints |
| `FiltersTitle` | "Filters" | The title of the filters column and the sheet |
| `ClearAllText` | "Clear all" | The link that removes every filter |
| `ClearFiltersText` | "Clear filters" | The empty state's button that removes the filters |
| `ResultsFormat` | "{0} results" | The count for several results |
| `OneResultFormat` | "{0} result" | The count for one result |
| `NoResultsText` | "No results" | The count when nothing matches, and the empty title without a query |
| `LoadingText` | "Searching…" | The count while `IsLoading` is true |
| `ElapsedFormat` | "in {0} seconds" | The time after the count; `{0}` is the seconds |
| `BestMatchText` | "Best match" | The first sort option |
| `NewestText` | "Newest" | The second sort option |
| `TitleSortText` | "A to Z" | The third sort option |
| `RecentLabel` | "Recent" | The group of recent searches in the popup |
| `SuggestedLabel` | "Suggested" | The group of suggestions in the popup |
| `TryText` | "Try" | The word before the suggestions of the empty state |
| `EmptyTitleFormat` | "No results for “{0}”" | The empty state title |
| `EmptyDescription` | "Check the spelling or try fewer words." | The empty state text |
| `EmptyFilteredDescription` | "The filters leave nothing. Remove one to see more." | The empty state text while filters are active |
| `ShowMoreText` | "Show more" | The button under the listed results when more are left |
| `KindLocationFormat` | "{0} in {1}" | A subtitle made of the kind label and the location |
| `HintMoveText` | "to move," | The hint after the arrow keys |
| `HintOpenText` | "to open," | The hint after the return key |
| `HintSearchText` | "to search" | The hint after the shortcut key |
| `MatchesLabel` | "Matches" | The first row of the detail's table |
| `OpenFormat` | "Open {0}" | The detail's primary button; `{0}` is the kind label in lower case |
| `CopyLinkText` | "Copy link" | The detail's secondary button |
| `CloseText` | "Close" | The accessible name of the close buttons |
| `ClearSearchText` | "Clear search" | The accessible name of the field's clear button |
| `FiltersButtonText` | "Filters" | The narrow layout's button |
| `AppliesToFormat` | "Applies to {0}" | The caption of a date filter without one; `{0}` lists its kinds |
| `AndText` | "and" | The word between the last two kinds of that caption |
| `RemoveFilterText` | "Remove filter" | The accessible name of a chip's cross |
| `DateFormat` | "MMM yyyy" | How the ends of a date range are written, in the current culture |

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SearchResults Label="Search the help center"
                       PlaceholderText="Search articles"
                       ResultsFormat="{}{0} articles"
                       OneResultFormat="{}{0} article"
                       NoResultsText="No articles"
                       ElapsedFormat="{}in {0}s"
                       FiltersTitle="Refine"
                       EmptyTitleFormat="Nothing found for “{0}”" />
</UserControl>
```

### Data shape and performance

The control reads the title, subtitle, snippet, caption, kind, location, body, keywords, date, attributes and id of each item
and the rows of its details; put anything else in `Tag`.

- Local matching checks every item on a new query, and only the previous matches while the query grows, so a local index of a
  few thousand items stays responsive. Past that, search on a server and set `IsFilteringEnabled` to false.
- Format dates and sizes in `Details` with the reader's culture before you set them; the control shows the strings as given.
  The date range and the sort use the current culture, and the first A to Z sort of a process is warmed up when the first
  search page is created, so it does not stall the window.
- Leave `PageSize` small. Every listed row is built and laid out, and the next page loads as the person scrolls.

### Example: Finance search for invoices

A finance back office opens search with the query "invoice" already filled in, after a link from the invoice list. The Team
filter has Finance ticked and the Edited range starts on August 1, so two chips sit under the header and an older invoice PDF is
left out. The counts beside Sales and Support show what the other teams would add. Newest first puts the person, who has no
date, last.

![Search results for invoices with filters, chips, a person and documents with the matching words marked](images/SearchResults.png)

```xml
<arc:SearchResults Width="840"
                   Height="860"
                   Label="Search finance"
                   PlaceholderText="Search invoices, customers and exports"
                   Query="invoice"
                   SortOrder="Newest"
                   Elapsed="0:0:0.08">
    <arc:SearchResults.Filters>
        <arc:SearchValueFilter Key="kind" Title="Type">
            <arc:SearchFilterValue Value="Docs" />
            <arc:SearchFilterValue Value="Files" />
            <arc:SearchFilterValue Value="People" />
        </arc:SearchValueFilter>
        <arc:SearchValueFilter Key="team" Title="Team">
            <arc:SearchFilterValue Value="Finance" IsSelected="True" />
            <arc:SearchFilterValue Value="Sales" />
            <arc:SearchFilterValue Value="Support" />
        </arc:SearchValueFilter>
        <arc:SearchDateFilter Key="edited" Title="Edited" AppliesTo="Docs, Files" Start="2026-08-01" />
    </arc:SearchResults.Filters>
    <arc:SearchResultItem Title="Invoice template"
                          Kind="Docs"
                          KindLabel="Doc"
                          Location="Finance / Templates"
                          Date="2026-09-14"
                          Snippet="Use this template for every customer invoice. Fill in the purchase order number before you send it."
                          Caption="Edited by Nia Okonkwo"
                          Icon="{x:Static arc:ArcIcons.FileText}">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Finance</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="Late invoices"
                          Kind="Docs"
                          KindLabel="Doc"
                          Location="Finance / Reports"
                          Date="2026-09-02"
                          Snippet="Invoices more than 30 days overdue, grouped by customer, with the contact who owns each account."
                          Caption="Edited by Soren Lindgren"
                          Icon="{x:Static arc:ArcIcons.FileText}">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Finance</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="Invoices Q2.xlsx"
                          Kind="Files"
                          KindLabel="File"
                          Location="Finance / Exports"
                          Date="2026-08-19"
                          Snippet="One row per invoice with the customer, the amount and the day it was paid."
                          Caption="Edited by Tobias Wren · 48 KB"
                          Icon="{x:Static arc:ArcIcons.File}">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Finance</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="Tobias Wren"
                          Kind="People"
                          KindLabel="Person"
                          Subtitle="Finance partner, Dublin"
                          Snippet="Approves every invoice above 10,000 and keeps the payment calendar."
                          Caption="tobias@tidewater.example"
                          AvatarName="Tobias Wren">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Finance</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="Invoice questions macro"
                          Kind="Docs"
                          KindLabel="Doc"
                          Location="Support / Macros"
                          Date="2026-08-22"
                          Snippet="A reply for customers who ask why an invoice shows two charges.">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Support</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="Invoice discount rules"
                          Kind="Files"
                          KindLabel="File"
                          Location="Sales / Pricing"
                          Date="2026-08-27"
                          Snippet="Which discounts may appear on an invoice, and who has to approve them.">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Sales</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="INV-1042.pdf"
                          Kind="Files"
                          KindLabel="File"
                          Location="Customers / Brightline"
                          Date="2026-03-12"
                          Snippet="The signed invoice for the annual plan.">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Finance</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
</arc:SearchResults>
```

### Content guidelines

Write the labels and the results so that a person can choose one without opening it. Kinds are plural nouns with an
initial capital ("Docs", "People", "Files"), because they are filter values; give each a singular `KindLabel` ("Doc",
"Person", "File") for the subtitle and the "Open doc" button. Titles name the thing, not a sentence about it. Locations are
breadcrumbs joined with " / ". A person's subtitle is a role and a city, and the caption is the one way to reach them.
Rows in `Details` are short facts: a name, a size, a relative time.

| Instead of | Write |
|---|---|
| Results: 4 | 4 results (`ResultsFormat` with the count) |
| No matches found for your query | No results for “quarterly forecast” (`EmptyTitleFormat`) |
| Reset | Clear filters (`ClearFiltersText`) |
| Open | Open doc (`OpenFormat` with the kind label) |

### Best practices

**Do**

- Give every result a `Kind` and a `KindLabel` that a person recognizes; the Type filter and every subtitle read them.
- Put the words people search for in the title first. The title is searched and ranked; the location, the subtitle and the
  keywords are searched; the snippet is searched and marked. Put the long text in `Body`, where it is searched and shown
  in the detail.
- Offer two to four filters, in the order of how often they narrow a search, and list their values yourself when the order
  matters ("Support", "Design", "Engineering" is a better order than the first-seen one).
- Set `IsLoading` for any request that takes longer than a blink, so the page keeps its height and shows that work is
  underway.
- Measure the time yourself and set `Elapsed` only for a real search; a made-up number reads as a promise.

**Avoid**

- A filter for every attribute. A long column scrolls, and the person has to read the options before the results; filters
  pay off when they cut a long list quickly.
- A whole document in `Snippet`. The control cuts long text around the match; a long passage hides why the result matched.
  `Body` is for the full text.
- Changing an item's `Kind`, `Title` or `Attributes` in place without calling `Refresh()`. The counts and the ranking are
  computed when the search runs.
- Using `IsEnabled` to hide a result. A disabled result is still listed, dimmed and counted; remove it from the items
  when it should not appear at all.
- Navigating on `ItemInvoked` and leaving `IsDetailEnabled` on. One Enter would then do both; turn the detail off or set
  `Handled`.

### Common questions

**Can I change the order of the filter values?** Yes. List them as `SearchFilterValue` objects inside the filter. Only a
filter without values lists what it finds, in the order the values first occur.

**How do I clear the search from code?** Set `Query` to an empty string and call `ClearFilters()`. An empty query lists every
item again. Escape in the field clears the query from the keyboard, after it closes the suggestions.

**Can I hide the filters?** Leave `Filters` empty (or give no value filter any value): the column goes away and the results
take the whole width.

**Why does a value show 0 and refuse to be ticked?** With the current query and the other filters it would show nothing, so
it is dimmed and disabled, and it keeps its place so the column does not jump while the person types. A value that is already
ticked stays available, so it can be removed.

**How do I keep recent searches between sessions?** Handle `Recent.CollectionChanged` and store the list; add the stored
entries to `Recent` before the page shows.

## API reference

<!-- api: SearchResults -->

<!-- api: SearchResultItem -->

<!-- api: SearchResultDetail -->

<!-- api: SearchFilter -->

<!-- api: SearchValueFilter -->

<!-- api: SearchFilterValue -->

<!-- api: SearchDateFilter -->

## Accessibility

- The control is a search landmark named by `Label`, which also names the search field. The clear button, the filters
  button and the close buttons have their own names.
- The results are a list named with the result count, and the count line is a polite live region, so the number of results is
  announced as it changes. The result under the cursor is the list's selected item, and each result is named with its title,
  kind and subtitle.
- Each filter is a group named by its title. Each value is a checkbox named with its label and count; a value without results is
  disabled but keeps its place. The date range's thumbs are named after the filter ("Edited start", "Edited end") and move one
  day with the arrow keys and 30 days with PageUp and PageDown. A chip is named with its filter and value, and its cross
  says "Remove filter" followed by that name.
- The sort switch is a segmented list: the arrow keys select the previous or next option, and only the selected one is a tab stop.
- In the search field, ArrowDown walks the suggestions while the popup is open, otherwise it moves into the results. Enter runs
  the highlighted suggestion or the typed text, and with the popup closed it opens the result under the cursor. Escape closes
  the popup, then clears the query. "/" or Ctrl+F (Cmd+F on macOS) moves focus to the field while focus is inside the control,
  and typing in the results continues the query in the field.
- In the results, ArrowUp, ArrowDown, Home, End, PageUp and PageDown move the cursor, ArrowUp on the first result returns to
  the field, Enter or Space opens a result and Escape returns to the field.
- The detail and the filters sheet are modal: focus moves in, stays inside, Escape, the close button or a press on the dimmed
  page closes them, and focus returns to where it was. Closing a modal does not reopen the suggestions popup by itself.
- Disabled results stay in the list and are dimmed, but the cursor skips them, so a keyboard user never lands on an item
  that cannot be opened.

### Keyboard

| Keys | Where | Action |
|---|---|---|
| `/`, `Ctrl+F` (`Cmd+F`) | Anywhere in the control | Move focus to the search field and select its text |
| Focus | Search field | Open the suggestions popup |
| `ArrowDown` / `ArrowUp` | Search field, popup open | Move the highlight through the suggestions; ArrowUp on the first one removes it |
| `Enter` | Search field, popup open | Run the highlighted suggestion, or the text as typed |
| `Escape` | Search field | Close the popup; with the popup closed, clear the query |
| `ArrowDown` | Search field, popup closed | Move into the results; the cursor is on the first one |
| `Enter` | Search field, popup closed | Open the result under the cursor |
| `ArrowUp` / `ArrowDown` | Results | Move the cursor by one result |
| `Home` / `End`, `PageUp` / `PageDown` | Results | Move the cursor to the first or last listed result, or by a page |
| `ArrowDown`, `PageDown` on the last listed result | Results | List the next page, then move |
| `ArrowUp` on the first result | Results | Return to the search field and clear the cursor |
| `Enter` or `Space` | Results | Open the result under the cursor |
| `Escape` | Results | Return to the search field |
| Any character | Results | Return to the field and add it to the query |
| `ArrowLeft` / `ArrowRight` | Date range thumb | Move one day |
| `PageUp` / `PageDown` | Date range thumb | Move 30 days |
| `Space` | Filter value | Tick or untick the value |
| `ArrowLeft` / `ArrowRight` | Sort switch | Select the previous or next option, wrapping around |
| `Escape` | Detail or filters sheet | Close it; focus returns to where it was |
| Pointer hover | Results | Move the cursor to the row under the pointer (touch does not) |
| Click | Results | Open the result |

## Motion

- The cursor glides between results: on a spring when it follows the pointer, and with a 70 ms tween when it follows the
  arrow keys. It fades in when it first appears and fades out when it is cleared. The suggestions popup has the same cursor.
- Results that were not shown before fade in and rise 4 pixels, 35 ms apart, for at most four rows. Results that stay do not
  move, so typing that only narrows the list animates nothing.
- The count and the time roll to their new values. The selected sort option's pill glides between the options on the morph
  spring.
- The bar of active filters opens and closes with a height spring, and its chips appear with the filters. The column's
  "Clear all" link fades in and out, and the field's clear button pops in and out.
- The suggestions popup enters with a small scale and a short rise and leaves quicker, like the other floating surfaces.
- The detail rises into place on wide screens and slides up from the bottom on narrow ones; the filters sheet slides up too.
- Reduced motion keeps the fades and drops every movement: the cursor and the sort pill jump, counts change at once and
  results only fade in. Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Cursor follows the pointer | Snappy spring | Jumps to the row | Immediate |
| Cursor follows an arrow key | 70 ms tween | Jumps to the row | Immediate |
| Cursor appears or is cleared | Fades in over 80 ms, out over 100 ms | Fades | Immediate |
| New results arrive | Fade in and rise 4 px, staggered 35 ms, up to four rows | Fade in | Immediate |
| Count or time changes | Digits roll to the new value | Changes at once | Immediate |
| Sort option changes | The pill slides on the morph spring | Jumps | Immediate |
| Active filters bar opens or closes | Height spring | Fades | Immediate |
| Suggestions popup opens or closes | Scale and rise with a fade | Fade | Immediate |
| Detail opens | Rises into place (slides up when narrow) | Fade | Immediate |
| Filters sheet opens | Slides up | Fade | Immediate |
| Count line, empty state, loading rows | Crossfade | Fade | Immediate |

## Related

- `CommandPalette` for actions and navigation from anywhere in the app.
- `ExpandingSearch` for a compact search in a header.
- `SearchField` for filtering a list on the page.
- `Checkbox`, `Slider` and `SegmentedControl`, which this block's filters and sort switch are built from.
- `EmptyState` and `Skeleton`, which this block uses for no results and loading.

| Component | Choose it when |
|---|---|
| `CommandPalette` | People run commands or jump to a page from anywhere in the app |
| `ExpandingSearch` | Search sits in a header or a toolbar and opens on demand |
| `SearchField` | The text filters a list that is already on the page |
| `FilterToolbar` | Filters sit above a table or a list, as chips, without a search page |
| `Tabs` | The kinds are views of one page rather than filters of a query |
| `Table` | The results are records to compare by column |
