---
title: Docs sidebar
summary: A documentation sidebar with a version switcher, search that narrows the pages, sections and pages that fold open, and the current page marked and scrolled into view.
---

<Demo name="Preview" />

## When to use

`DocsSidebar` is the left column of a documentation site or an in-app help center. The header carries the product
name and a small switcher for the documentation version. Under it, a search box narrows the list as the reader types.
Pages are grouped into titled sections that fold, and a page can open into sub-pages. The page being read is marked,
and whenever it changes, from a click or from your router, its section and parent open and the list scrolls so it
stays in view. It is built on `Sidebar`, so it shares its marker, keyboard model and filter.

- **Product documentation** with guides, an API reference and a changelog, where readers jump between many pages.
- **Versioned docs**, where readers on an older release need the matching pages.
- **Help centers inside an app**, where the current article is chosen by a link and the sidebar must follow.

## When not to use

- **The main navigation of an app**: use `Sidebar`, which has icons and collapses to a rail.
- **A mailbox**: use `InboxSidebar`, which adds Compose, counts and labels.
- **The headings of one long page**: an outline of anchors next to the article reads better than a page list.

## Usage

```csharp
docs.Sections.Add(new DocsSidebarSection("Guides",
    new DocsSidebarPage("guides/caching", "Caching", "New"),
    new DocsSidebarPage("guides/deploying", "Deploying")));
docs.CurrentKey = "guides/caching";
docs.PageInvoked += (_, e) => router.Open(e.Page.Key);
docs.VersionChanged += (_, e) => LoadPages(e.NewVersion);
```

Pages and sections can also be written in XAML. Without `Sections` the sidebar shows an invented example site.
`GetAdjacentPage(-1)` and `GetAdjacentPage(1)` give the previous and next page in reading order for the links at the
bottom of an article.

<Demo name="OwnPages" />

## API reference

<!-- api: DocsSidebar -->

## Accessibility

- The sidebar is a navigation landmark named "Documentation". The arrow keys move between rows, Enter or Space opens
  a page, and a parent page opens and closes its sub-pages.
- "/" or Ctrl+K (Cmd+K) puts the caret in the search box from anywhere in the window. Turn this off with
  `IsSearchShortcutEnabled`.
- The version switcher is a button named "Version v3.2" that opens a menu with radio items.

## Motion

- The selection marker glides between rows, and sections fold on a height spring (from `Sidebar`).
- Revealing the current page scrolls the list once the rows are in place and again after an opening section settles.
- Reduced motion keeps the folds and the marker but without springs; Off changes everything at once.

## Responsive behavior

- The sidebar keeps the width of `Sidebar` (240 px) and fills the height it is given; the list scrolls on its own.
- Long page titles end with an ellipsis. On a narrow window, place the sidebar in a `Drawer`.

## Related

- `Sidebar`, which this block is built on.
- `InboxSidebar` for a mailbox and `SearchResults` for a full search page.
- `Breadcrumb` to show where the current page sits.
