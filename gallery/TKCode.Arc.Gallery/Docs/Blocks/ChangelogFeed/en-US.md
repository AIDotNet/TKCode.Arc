---
title: Changelog feed
summary: Release notes you can filter, open in place and scroll through month by month.
---

<Demo name="Preview" />

## When to use

Use `ChangelogFeed` for a product changelog or release notes page grouped by month, with filters by kind of change and an email subscribe action in the header.

## When not to use

- **A short list of events without filters**: use `Timeline`.
- **Expandable content that is not dated**: use `Accordion`.
- **Thousands of notes**: entries are not virtualized, so load older months on demand.

## Usage

```csharp
feed.Months = [new ChangelogMonth { Key = "2026-03", Label = "March 2026", ShortLabel = "Mar" }];
feed.Entries =
[
    new ChangelogEntry
    {
        Id = "tags", Month = "2026-03", Date = "Mar 3", Version = "2.1.0", Kind = ChangelogKind.New,
        Title = "Tags for every note", Summary = "Group notes across folders.",
        Details = ["Type a hash sign to add a tag."],
        Media = new ChangelogCode { File = "tags.json", Code = "{ }" }
    }
];
feed.Subscribed += async (_, e) => await list.AddAsync(e.Email);
```

<Demo name="CustomData" />

Entries belong to a month by key and are listed under it, newest first. A month with no matching entries leaves the bar and the list. `DefaultOpen` chooses the entries open at first; without it, the newest one is. `ToggleEntry`, `ToggleFilter`, `ClearFilters` and `JumpToMonth` do the same from code.

### Subscribing

The pill turns into an email field and then a confirmation with Undo. A valid address raises `Subscribed`; Undo raises `SubscriptionUndone`. The block sends nothing itself, so post the address to your mailing list in the handler.

### Media

`Media` is a `ChangelogPhoto` (a picture cropped to 16:10 with a caption) or a `ChangelogCode` (a snippet with a copy button). The panel is built the first time its entry opens.

### Narrow containers

<Demo name="Narrow" />

## API reference

<!-- api: ChangelogFeed -->

<!-- api: ChangelogSubscribeEventArgs -->

## Accessibility

- Each row is a button whose name holds the date, kind, title and summary, and whose help text says whether it is open.
- Filter chips are toggle buttons, and a live region says how many updates show.
- Month buttons are named by `JumpFormat`. The list can take focus so the keys scroll it.
- `Escape` cancels the subscribe field and returns to the pill; `Enter` submits it.

## Motion

- A row opens on the smooth spring and closes on `{ visualDuration: 0.28, bounce: 0 }` while its chevron turns on the snappy spring. The panel's content rises 8 px out of a 4 px blur after 60 ms.
- A filter change fades rows out (scale .98, blur 2, 180 ms) and the others spring to their new places. Counts roll on the snappy spring.
- The month label rises 14 px with the scroll direction and the month pill glides on the morph spring. A month button scrolls on the smooth spring.
- The subscribe pill morphs its width; its content swaps with scale .96 and blur 2; a bad address shakes it for 480 ms; the check draws in 420 ms.
- Reduced motion uses short fades, with no shake and no travel. Off is instant.

## Responsive behavior

- The block follows its own width, at most 920 px.
- At 640 px and less the header stacks, rows move date and version above the text, and the list drops from 560 to 520 px.
- At 480 px and less the month count, the year and the shown count go, so the month nav fits on one line.
- Hover styles apply to a mouse or a pen only.

## Related

- `Timeline` for a short vertical list of events.
- `Accordion` for undated expandable content.
- `CopyButton` for the snippets' copy action.
