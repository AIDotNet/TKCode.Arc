---
title: Blog grid
summary: A blog index with a featured post, a category filter, post cards, pagination and an in-place reader.
---

<Demo name="Preview" />

## When to use

Use `BlogGrid` as the index of a blog, journal, news page or resource hub with a few categories and a featured story.

## When not to use

- **Release notes with versions**: use the `ChangelogFeed` block.
- **Large archives that need sorting and search**: use a data table.

## Usage

```csharp
grid.Posts = posts;
grid.Categories = ["Product", "Design", "Engineering", "Company"];
grid.HrefSelector = post => $"https://example.com/blog/{post.Id}";
grid.PostOpened += (_, e) => analytics.Track("post", e.Post.Id);
```

Posts are listed newest first. Only posts whose `Category` matches a filter can be filtered, and `Categories` sets the filters; "All" goes in front.

### Links or the reader

With `HrefSelector`, a card raises `PostOpened` and opens the link, unless a handler marks the event handled. Without it, a card opens the in-place reader; `ReadingPost` tells which post is open, and `OpenPost` and `CloseReader` do it from code.

<Demo name="Links" />

### Binding the filter and the page

`Category` and `Page` bind both ways, so an app can keep them in its route. Picking a filter resets the page to 1.

```xml
<arc:BlogGrid Category="{Binding Category}" Page="{Binding Page}" PageSize="9" ShowFeatured="False" />
```

### Narrow containers

<Demo name="Narrow" />

## API reference

<!-- api: BlogGrid -->

<!-- api: BlogPostEventArgs -->

## Accessibility

- Each card is one focusable link whose name holds the title, category, date and author, so it is announced once.
- Filters and page numbers are toggle buttons; the page buttons are named by `PageLabelFormat`.
- The reader moves focus to its back button, and closing returns focus to the card. Covers carry `ImageAlt`; portraits are decorative.
- `Tab` moves through filters, cards and pagination; `Enter` opens the focused post.

## Motion

- The filter and page highlights glide on the morph spring.
- A filter change crossfades the grid with a 10 px rise; paging slides it 24 px in the direction of travel: the old page leaves in 180 ms first, then the new one arrives on the smooth spring with a 240 ms fade. The height follows on the smooth spring.
- Opening a post flies the cover into the reader on the morph spring, and closing flies it back while the rest of the index fades in. The reader text rises 12 px after a 120 ms delay.
- Cover images zoom 3% in 480 ms on mouse hover.
- Reduced motion swaps travel and flights for plain fades. Off is instant.

## Responsive behavior

- The block follows its own width: three columns above 900 px, two up to 540 px, then one.
- At 680 px and less the featured post stacks its cover over its text and the spacing tightens. At 540 px and less Previous and Next collapse to arrows.
- Filters scroll sideways when they do not fit.

## Related

- The `ChangelogFeed` block for dated release notes.
- `Pagination` for paging on its own.
