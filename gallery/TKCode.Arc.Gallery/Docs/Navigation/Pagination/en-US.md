---
title: Pagination
summary: Move through a long collection with clear bounds.
---

<Demo name="Default" />

## When to use

`Pagination` moves a person through one collection that the application loads a page at a time: the invoice list in a
billing console, the orders table in a store back office, the search results of a CRM. The control holds the current
page number and the page count, draws the previous and next buttons and a window of five page buttons, and leaves the
loading to the application. It sits under the data it pages, where the person looks for the next set of rows.

- **Invoices and payments in a billing console**: a list of 248 invoices loaded 10 at a time from the server. The
  footer shows Page 12 of 25, the previous and next buttons stop at the ends, and the page number lives in the view
  model so a refresh keeps the person on the same page.
- **Orders and returns in a store back office**: the orders table pages through the server results, and the page
  number is also the value a support agent quotes ("page 4 of the returns queue").
- **Search results in an internal tool**: a result set that is too long to scroll, where the count is known and each
  page is a separate request.
- **Audit logs and event streams with a known count**: the page count comes from the server's total, so the bounds of
  the list are visible before the person clicks.

## When not to use

- **Browsing a slideshow of items**, such as product photos or onboarding cards: use `Carousel`, which steps through
  content that the person views one item at a time.
- **Reading a long feed as it scrolls**, such as an activity stream or a log that grows: use `Timeline` inside a
  scrolling container such as `ScrollArea`. Page buttons break the reading flow of a feed.
- **Moving to a location inside a hierarchy**, such as Billing, then Invoices, then the invoice: use `Breadcrumb`,
  which shows the path to the current page.
- **Switching between views of the same object**, such as Overview, Activity and Settings: use `Tabs`.
- **Choosing a range of dates or numbers**: use `DateRangePicker` or a `Slider`. A page number does not describe a
  range of values.

## Usage

Bind `CurrentPage` and `PageCount`. The control renders a window of five page buttons, which moves with the current
page.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Pagination PageCount="{Binding PageCount}"
                    CurrentPage="{Binding CurrentPage, Mode=TwoWay}" />
</UserControl>
```

`Pagination` derives from Avalonia's `TemplatedControl`, not from a list. It has no `ItemsSource`, and it never loads or
counts anything: the application computes the page count and reacts to page changes. `CurrentPage` is two-way by
default, so `Mode=TwoWay` is optional. Both values are plain integers, so the same control works with a count from
a server response, a local list or a constant.

### Anatomy

1. **Landmark** (`Label`): a navigation landmark with the name "Pagination" by default. It is not drawn.
2. **Previous button** (`PreviousLabel` is its accessible name): a 36 px square button with a 16 px left chevron. It is
   disabled on the first page and stays in place.
3. **Page buttons**: five 36 px square buttons with 4 px between them. They show the numbers in tabular figures at the
   extra-small type size, in the secondary text color, and take the control radius.
4. **Current-page mark**: a surface-colored pill with a 1 px border and the resting shadow. It sits under the current
   number and glides to the next number when the page changes.
5. **Boundary buttons and ellipses** (`ShowBoundaryPages`): the first and last page buttons and a 24 px ellipsis on
   each side of the window. They appear only when the window does not reach the ends.
6. **Next button** (`NextLabel` is its accessible name): the mirror of the previous button, disabled on the last page.
7. **Focus ring**: a 2 px ring in the focus color, drawn 4 px outside the button. It appears only for keyboard focus.

The control lays the items out in a row and keeps them left-aligned. Disabled buttons drop to 40% opacity and show the
"not allowed" cursor.

### Reacting to a page change

Handle `PageChanged` to load the page. `NewPage` is the page the user asked for, and `OldPage` is the page before the
change. The event is raised whenever `CurrentPage` changes, so a change made by code (a binding, a reset after a
filter) raises it too.

```csharp
pagination.PageChanged += async (_, e) =>
{
    status.Text = $"Page {e.NewPage} of {pagination.PageCount}";
    await LoadPageAsync(e.NewPage);
};
```

`NewPage` is the raw value of `CurrentPage`, not the value after clamping. If code sets `CurrentPage` to 99 on a list
of 25 pages, `NewPage` is 99 while the buttons show page 25; clamp it in the handler, or keep `CurrentPage` in range
in the view model (see below).

### Boundary pages

`ShowBoundaryPages` keeps the first and last page one click away, with an ellipsis between them and the window. It is
an addition beyond Arc and is off by default. The boundary buttons appear only when `PageCount` is greater than five,
and each one appears only when the window does not already reach its end. Use it for a long list where the person may
need the first or last page quickly, such as a log that is read from the newest entry.

<Demo name="BoundaryPages" />

### Few pages and no pages

With fewer than five pages, all of them are shown; the arrows disable at the ends. A `PageCount` of `0` renders an
empty navigation landmark: the page buttons and the arrows are hidden, so only the landmark remains. Use zero for an
empty result, and show an empty state in place of the control.

<Demo name="FewPages" />

<Demo name="NoPages" />

### Labels

`Label` names the navigation landmark. `PreviousLabel`, `NextLabel` and `PageLabelFormat` name the buttons for
assistive technology. Set them when the default text is not in the user's language. `PageLabelFormat` takes a `{0}`
placeholder for the page number and is formatted with the current culture.

```xml
<arc:Pagination Label="Results pages"
                PreviousLabel="Previous page"
                NextLabel="Next page" />
```

### Binding to a view model

An invoice list keeps the current page and the page count in its view model. The count is derived from the total that
the server returns, and a change of the current page loads the matching rows. With CommunityToolkit.Mvvm,
the generated partial method runs when the person changes the page, so the load does not depend on the view.

```csharp
public sealed record InvoiceRow(string Id, string Customer, string Amount, string Status);

public sealed record InvoicePage(int Total, IReadOnlyList<InvoiceRow> Rows);

public interface IInvoiceService
{
    Task<InvoicePage> GetPageAsync(int page, int pageSize);
}

public partial class InvoiceListViewModel : ObservableObject
{
    private const int PageSize = 10;
    private readonly IInvoiceService _invoices;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageCount;

    public ObservableCollection<InvoiceRow> Rows { get; } = new();

    public InvoiceListViewModel(IInvoiceService invoices)
    {
        _invoices = invoices;
    }

    partial void OnCurrentPageChanged(int value)
    {
        _ = LoadPageAsync(value);
    }

    private async Task LoadPageAsync(int page)
    {
        var result = await _invoices.GetPageAsync(page, PageSize);
        PageCount = (int)Math.Ceiling(result.Total / (double)PageSize);
        if (PageCount > 0 && page > PageCount)
        {
            // The list shrank: move to the last page, which loads it.
            CurrentPage = PageCount;
            return;
        }
        Rows.Clear();
        foreach (var row in result.Rows)
        {
            Rows.Add(row);
        }
    }
}
```

```xml
<arc:Pagination PageCount="{Binding PageCount}"
                CurrentPage="{Binding CurrentPage}"
                Label="Invoice pages" />
```

`CurrentPage` is two-way, so the control writes the page the person picks back to the view model, and the view model
reacts through its own property. `PageChanged` is not needed in this design. Use it when the view code, not the view
model, must act on the change, for example to scroll a list to its top.

### Keeping the page in range

The control clamps the page it shows, but it never changes the bound value on its own. Reducing `PageCount` below
`CurrentPage` (after a filter removes rows, for example) leaves `CurrentPage` where it was: the buttons show the last
page, and the value in the view model is out of range. Set `CurrentPage` to `PageCount` in the view model when the
count changes, as `LoadPageAsync` does in the example above.

`EffectivePage` is the page the buttons show, and it is 0 when there are no pages. `WindowStart` is the first number
in the five-page window. `GoToPage(page)` moves to a page, clamped to the valid range, and returns `false` when nothing
changed. `NextPage()` and `PreviousPage()` move one page and return `false` at the ends. Use these methods in code
behind or in a keyboard shortcut, so that the same rules apply as for the buttons.

```csharp
// A shortcut that moves to the last page without knowing the count in advance.
pagination.GoToPage(int.MaxValue);
```

### Example: invoice list footer

A table of invoices with its pagination footer. The footer states the page and the total in words, which is the first
thing a person checks after a page change, and the control sits at the right so the number and the arrows read as one
group. The current page is 12 of 25, so the window is 10 to 14 and both arrows are enabled. The label names the
landmark "Invoice pages" for a screen reader user, and the rows show the amounts with two decimals and the status as
text, not only a color.

![An invoice table with a footer showing page 12 of 25 and a pagination control](images/InvoiceFooter.png)

```xml
<StackPanel Width="680" Spacing="0">
    <Grid ColumnDefinitions="2*,2*,*,*" Margin="0,0,0,8">
        <TextBlock Grid.Column="0" Text="Invoice" FontSize="12" FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <TextBlock Grid.Column="1" Text="Customer" FontSize="12" FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <TextBlock Grid.Column="2" Text="Amount" FontSize="12" FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <TextBlock Grid.Column="3" Text="Status" FontSize="12" FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </Grid>
    <Grid ColumnDefinitions="2*,2*,*,*" Height="40">
        <TextBlock Grid.Column="0" Text="inv_2026_0412" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="Northwind Trading" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="2" Text="$4,800.00" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="3" Text="Paid" FontSize="14" VerticalAlignment="Center" />
    </Grid>
    <Grid ColumnDefinitions="2*,2*,*,*" Height="40">
        <TextBlock Grid.Column="0" Text="inv_2026_0411" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="Contoso Ltd" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="2" Text="$1,250.50" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="3" Text="Open" FontSize="14" VerticalAlignment="Center" />
    </Grid>
    <Grid ColumnDefinitions="2*,2*,*,*" Height="40">
        <TextBlock Grid.Column="0" Text="inv_2026_0410" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="Fabrikam" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="2" Text="$920.00" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="3" Text="Overdue" FontSize="14" VerticalAlignment="Center" />
    </Grid>
    <Grid ColumnDefinitions="*,Auto" Margin="0,20,0,0">
        <TextBlock Grid.Column="0" Text="Page 12 of 25, 248 invoices" FontSize="12"
                   VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <arc:Pagination Grid.Column="1" Label="Invoice pages" PageCount="25" CurrentPage="12" />
    </Grid>
</StackPanel>
```

### Content guidelines

- **Summary text**: state the range in words next to the control: "Page 12 of 25, 248 invoices" or "Showing 111 to 120
  of 248". The control shows numbers only, so the words carry the meaning for a person who scans the footer.
- **Labels**: name the list, not the control: "Invoice pages", "Results pages". The landmark name is read aloud, so
  "Pagination" alone tells a screen reader user nothing about the content.
- **Button names**: keep the default "Previous page" and "Next page" unless the application language differs. Format
  page labels with a `{0}` placeholder, such as "Go to page {0}", only when the default "Page {0}" reads wrong.
- **Empty results**: do not show page controls for zero results. Show the empty state with a sentence that says what
  was searched for.

| Instead of | Write |
|---|---|
| Label "Pagination" | Invoice pages |
| Footer "12 / 25" | Page 12 of 25, 248 invoices |
| Previous (icon only, no name) | Previous page |
| Pagination under an empty table | No invoices match these filters. |

### Best practices

**Do**

- Keep `CurrentPage` in range in the view model, and set it to `PageCount` when the count shrinks, so the bound value
  always matches what the buttons show.
- Load the page in the view model, from the page number, so a refresh and a deep link can reach the same page.
- Show the range and the total in words next to the control, so the person knows how many rows exist before they page.
- Set `Label` for each pagination on a page, so that two lists on one screen read as two landmarks.

**Avoid**

- Showing a page count that is not from the server total. A count computed from the rows already loaded hides the rest
  of the list and makes the last page look like the end.
- Clearing the rows before the next page arrives. Keep the current rows on screen while the request runs, and show the
  change in the footer text, so the person sees progress rather than an empty table.
- Using `ShowBoundaryPages` on a short list. It adds ellipses and buttons that a three-page list does not need.
- Setting `PageCount` to a value computed while the total is still loading. A zero or stale count makes the arrows
  disabled for the wrong reason.

### Common questions

**Does the control let people type a page number or change the page size?** No. Arc's pagination is controlled: it shows
the window and the arrows only. Add a page-size selector with `Select` and a jump-to-page field with `NumberField`
beside it, and call `GoToPage` from their handlers.

**Why does `PageChanged` fire when I set `CurrentPage` in code?** The event is raised from the property change, so any
change raises it, from the person or from code. Compare `e.NewPage` with the page you last loaded before you start a
request, so the same page is not loaded twice.

**What happens when `PageCount` is 0?** The page buttons and the arrows are hidden, and `EffectivePage` is 0. The
navigation landmark stays in the tree, so keep it on the page only when there is something to page through, or hide
the control in the view.

**Can I show more than five pages at once?** No. The window is always five page buttons (`WindowSize` is 5). For the
first and last page, use `ShowBoundaryPages`; for anything else, use a different component or a list with its own
scrolling.

## API reference

<!-- api: Pagination -->

## Accessibility

- The control is a navigation landmark named by `Label`, and it is announced as a group of controls.
- Each page button is named by `PageLabelFormat`, for example "Page 3". The current page has the current-page state.
- The previous and next buttons are disabled at the ends and keep their names.
- Tab moves between the previous button, the page buttons and the next button. Enter or Space goes to the focused page.
  Left and Right arrows move focus between the buttons.
- The ellipses and the hidden buttons are not focusable and are hidden from assistive technology.
- Keyboard focus follows the number: when the window moves while a page button has focus, focus moves with that number
  to its new slot.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move between the previous button, the visible page buttons and the next button; disabled arrows are skipped |
| `Enter` / `Space` | Go to the focused page, or to the previous or next page on an arrow |
| `Left` / `Right` | Move focus to the previous or next enabled button; the page does not change until Enter or Space |

## Motion

- A single current-page mark glides to the selected button on a morph spring.
- When the window moves, the numbers slide like a belt by the number of slots that moved, and the new numbers
  fade in.
- Reduced motion moves the mark at once and replaces the numbers in place. Off applies the change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The current page changes | The mark glides to the new number on the morph spring | The mark moves at once | Immediate |
| The window moves by some slots | Numbers travel by the slots that moved (40 px each) on the smooth spring; new numbers fade in | Numbers replaced in place | Immediate |
| A number leaves the window | Slides on and fades out in 120 ms | Removed at once | Immediate |
| Press on an arrow | The arrow scales to 96% while held | No press scale | No press scale |
| Hover, focus | The background and text colors crossfade | Immediate | Immediate |

## Related

- `Tabs` for switching between views rather than pages of one collection.
- `Breadcrumb` for the location of a page in a hierarchy.
- `Carousel` for stepping through slides.

| Component | Choose it when |
|---|---|
| `Tabs` | The views are peers of one object, and each has its own panel |
| `Breadcrumb` | The page sits in a hierarchy and the person needs the path back up |
| `Carousel` | The items are viewed one at a time, such as product photos |
| `Timeline` | A feed or log is read in order and the list grows as it scrolls |
| `ScrollArea` | A long list is read by scrolling, not by changing pages |
| `Select` / `NumberField` | The footer also needs a page-size selector or a jump-to-page field |
| `DateRangePicker` / `Slider` | The person chooses a range of dates or values, not a page |
