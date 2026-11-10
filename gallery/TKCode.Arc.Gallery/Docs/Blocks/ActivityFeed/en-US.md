---
title: Activity feed
summary: What happened in a workspace, grouped by day: who did what with an icon per kind, filter chips and a person picker, and new items that slide in live or wait behind a pill while paused.
---

<Demo name="Preview" />

## When to use

`ActivityFeed` shows what happened in a workspace: who commented, changed, deployed or joined, newest first and grouped
by day. Chips filter by kind and a picker by person. While the feed is live, new items slide in at the top as they
happen; paused, they wait behind a "new updates" pill so the list never moves under the reader.

- **A project or workspace home** with the latest activity beside the content.
- **An audit view** filtered to one person or one kind of change.
- **A deploy log** where builds and releases arrive live.

## When not to use

- **A personal inbox of things to act on**: use `NotificationCenter` or `InboxTriage`.
- **A chat**: use `ChatThread`.
- **Tens of thousands of events with search**: use a `Table` with filters and paging.

## Usage

```xml
<arc:ActivityFeed x:Name="Feed" Title="Workspace activity" KindFilter="Deploy" />
```

```csharp
Feed.Push(new ActivityFeedItem("e42", DateTimeOffset.Now, "Rosa Lind", "commented on Launch plan", ActivityKind.Comment)
{
    Target = "Spring release",
    Detail = "Can we move the screenshots first?",
});
```

`Items` holds everything, in any order. `Push` adds an item as it happens: while `IsLive` it goes straight in (when it
passes the filters); while paused it waits, `PendingCount` grows, and the pill or `ShowPending()` lets it in. Each item has
an actor (null for the system), an action, a target line, a kind (`Comment`, `Change`, `Deploy`, `Member`, `Other`) that
picks the icon and the chip, an optional portrait, a `Tone` for outcomes, and an optional `Detail` that the row expands to
show. A feed with no items when it first loads shows a sample workspace.

### Filtered and paused

<Demo name="Comments" />

`KindFilter` and `ActorFilter` are two-way; the chips and the person picker set them.

## API reference

<!-- api: ActivityFeed -->

<!-- api: ActivityFeedItem -->

## Accessibility

- The feed is a `Timeline` region named by the title; day headings are headings, rows with a detail expand with `Enter` or `Space`.
- The live chip says whether updates are live and what pressing it does. New items while paused are announced ("3 new updates"); outcomes such as a failed deploy are written in the action, not shown by color alone.
- The pill and the chips are reachable with `Tab`; `↑` / `↓` move between expandable rows inside the timeline.

## Motion

- New items slide in at the top while the rows below glide down; the line between markers draws as rows come into view.
- The live dot sends out a soft ring every 1.6 s. It runs on a frame loop that stops off screen, while hidden, and under reduced motion.
- The "new updates" pill drops in from above. Reduced motion uses fades only; with motion off changes are immediate.

## Responsive behavior

- The feed is at most 640 px wide and the timeline scrolls past 560 px (`FeedMaxHeight`).
- The chips and the person picker wrap onto two lines in a narrow column; the timeline tightens its layout below its own narrow width.

## Related

- `Timeline` for the day-grouped list on its own.
- `NotificationCenter` for personal updates with read state.
- `InboxTriage` for items that each need a decision.
