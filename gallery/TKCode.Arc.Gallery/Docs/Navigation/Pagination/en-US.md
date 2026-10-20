---
title: Pagination
summary: Move through a long collection with clear bounds.
---

## When to use

- Paging through server results or a long table, where the page number is kept in state or in the address.
- Showing where the user is in a fixed number of pages, with the previous and next buttons disabled at the ends.
- Sharing a page count with a data grid or list that reloads when the page changes.

## When not to use

- Browsing a slideshow of items: use `Carousel`.
- Reading a long feed as it scrolls: use `Timeline` with a height limit, not page buttons.
- Moving to a location inside a hierarchy: use `Breadcrumb`.

## Usage

Bind `CurrentPage` and `PageCount`. The control renders a window of five page buttons, which moves with
the current page.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Pagination PageCount="{Binding PageCount}"
                    CurrentPage="{Binding CurrentPage, Mode=TwoWay}" />
</UserControl>
```

### Reacting to a page change

Handle `PageChanged` to load the page. `NewPage` is the page the user asked for.

```csharp
pagination.PageChanged += async (_, e) =>
{
    status.Text = $"Page {e.NewPage} of {pagination.PageCount}";
    await LoadPageAsync(e.NewPage);
};
```

### Boundary pages

`ShowBoundaryPages` keeps the first and last page one click away, with an ellipsis between them and the
window.

```xml
<arc:Pagination PageCount="48"
                CurrentPage="20"
                ShowBoundaryPages="True" />
```

### Few pages and no pages

With fewer than five pages, all of them are shown; the arrows disable at the ends. A `PageCount` of `0`
renders an empty navigation landmark.

```xml
<arc:Pagination PageCount="3" CurrentPage="2" />
<arc:Pagination PageCount="0" />
```

### Labels

`Label` names the navigation landmark. `PreviousLabel`, `NextLabel` and `PageLabelFormat` name the buttons
for assistive technology. Set them when the default text is not in the user's language.

```xml
<arc:Pagination Label="Results pages"
                PreviousLabel="Previous page"
                NextLabel="Next page" />
```

## API reference

<!-- api: Pagination -->

## Accessibility

- The control is a navigation landmark named by `Label`.
- Each page button is named by `PageLabelFormat`, for example "Page 3". The current page has the current-page
  state.
- The previous and next buttons are disabled at the ends and keep their names.
- Tab moves between the previous button, the page buttons and the next button. Enter or Space goes to the
  focused page. Left and Right arrows move focus between the buttons.

## Motion

- A single current-page mark glides to the selected button on a morph spring.
- When the window moves, the numbers slide like a belt by the number of slots that moved, and the new numbers
  fade in.
- Reduced motion moves the mark at once and replaces the numbers in place. Off applies the change at once.

## Related

- `Tabs` for switching between views rather than pages of one collection.
- `Breadcrumb` for the location of a page in a hierarchy.
- `Carousel` for stepping through slides.
