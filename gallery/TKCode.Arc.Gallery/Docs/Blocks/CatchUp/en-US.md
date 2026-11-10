---
title: Catch up
summary: A while-you-were-away summary: a written recap, highlights, mentions and decisions with who, where and when, a filter by group and read state per item or all at once.
---

<Demo name="Preview" />

## When to use

`CatchUp` greets someone who comes back after a weekend or a holiday. A short written recap says what changed in two or
three sentences. Below it the items are grouped into highlights (things worth knowing), mentions (someone asked for them)
and decisions (something was settled), each with who did it, where and when, and an unread dot. A filter shows one group
at a time with its unread count. Opening an item hands its link to the app and marks it read; a small button beside each
item marks it read or unread; "Mark all as read" clears everything and the recap turns into "You are all caught up."

- **Returning to a workspace.** The first screen after time away, written by your summarizer.
- **Daily digests.** The same card for the last 24 hours.
- **Channel or project recaps.** Scope the items to one place.

## When not to use

- **Live notifications.** Use the `Notification center` block for a running feed.
- **Full activity logs.** Use `Timeline` when every event matters, not just the important ones.
- **A single update.** Use `Toast` or `Alert`.

## Usage

The block derives from Avalonia's `TemplatedControl`. Without any data it shows an invented weekend (`CatchUpSamples`).

```xml
<arc:CatchUp Since="Since Friday, 6:10 PM" Summary="{Binding Recap}" Items="{Binding Changes}" />
```

```csharp
catchUp.Items =
[
    new() { Id = "m1", Kind = CatchUpKind.Mention, Title = "Priya asked you to review the pricing page",
            Author = "Priya Raman", Source = "Pricing refresh", Time = "Sat 3:02 PM", Link = "doc:pricing#c12" },
];
catchUp.ItemInvoked += (_, e) => Navigate(e.Item.Link);
catchUp.ItemsRead += (_, e) => store.SaveRead(e.Items.Select(i => i.Id), e.IsRead);
```

### Anatomy

- **Header**: the title, `Since` with the unread count, and "Mark all as read".
- **Recap**: `Summary` beside a sparkle; once everything is read, "You are all caught up." with a double check.
- **Filter**: All, Highlights, Mentions and Decisions, each with its unread count (`Filter`, two-way).
- **Items**: grouped by `CatchUpItem.Kind` in that order; each shows the author's generated avatar (or the group's glyph),
  the title, the detail, where and when, and the unread dot; the button beside it toggles read.

Read state is kept by `CatchUpItem.Id`; `IsRead` on the item sets the start. `IsRead(item)`, `SetRead(item, value)`,
`MarkAllRead()` and `Open(item)` do from code what the buttons do; `UnreadCount` follows. `MarkReadOnOpen="False"` keeps
items unread when opened.

### Opening an item

<Demo name="Opened" />

### Words

`Labels` (`CatchUpLabels`) holds the title, the group names, the filter format, the caught-up and empty lines and the
screen reader names of the read buttons.

## API reference

<!-- api: CatchUp -->

<!-- api: CatchUpItem -->

<!-- api: CatchUpKind -->

<!-- api: CatchUpFilter -->

<!-- api: CatchUpLabels -->

## Accessibility

- The card is a named region; the title is a level 2 heading and each group name a level 3 heading.
- Each item is a button named by its title, with "unread" while unread, and described by its detail, author and where and
  when. **Up** and **Down** move between items, **Home** and **End** jump to the ends, **Enter** or **Space** opens one.
- The read button beside each item is named "Mark "…" as read" or "as unread".
- The filter is a segmented control with arrow keys; "You are all caught up." is announced after "Mark all as read".

## Motion

- The recap lifts into the caught-up line (old out in 100 ms, new in 160 ms, rising 4 px) while its height follows on a
  smooth spring.
- An item's unread dot fades out and its text fades to the read strength over 240 ms; rows tint on hover in 160 ms.
- Reduced motion crossfades the recap without the rise; Off applies every change at once.

## Responsive behavior

- The card is at most 600 wide. At 440 or less (`:compact`) the padding tightens from 20 to 16 and "Mark all as read" moves
  under the title.
- Titles wrap; details show at most two lines and end with an ellipsis; where and when shorten with an ellipsis.
- The read button stays visible, so nothing depends on hover.

## Related

- `Notification center` for a live feed of updates.
- `Timeline` and `Comment thread` for full histories.
- `AI insight card` for explaining one metric's change.
