---
title: Inbox triage
summary: An inbox for getting through it fast: swipe or press a key to archive, snooze, assign or label, undo any step from the bar at the bottom, and watch the count of what is left roll down to inbox zero.
---

<Demo name="Preview" />

## When to use

`InboxTriage` is an inbox built for getting to zero: support tickets, review requests, messages or alerts that each need
one quick decision. Every row can be archived, snoozed, assigned to a teammate or labelled, by swiping or with one key,
and every step can be undone from the bar at the bottom. A rolling counter and a progress bar show how much is left, and an
empty inbox says so.

- **A support queue** worked one ticket at a time.
- **Review or approval requests** that each need a yes, a later or a hand-off.
- **Notification triage** on a phone, where swiping is natural.

## When not to use

- **Reading long messages**: pair a list with a reading pane; triage rows show a subject and one line.
- **Hundreds of items at once**: rows are not virtualized; page the inbox.
- **Bulk actions on a selection**: use a `Table` with selection and a toolbar.

## Usage

```xml
<arc:InboxTriage x:Name="Inbox" Title="Support inbox">
    <arc:InboxTriageItem Id="t1" Sender="Ines Duarte" Subject="Refund for order 4471" />
</arc:InboxTriage>
```

```csharp
Inbox.Triaged += (_, e) => api.Apply(e.Item.Id, e.Action, e.Value);   // Value is the teammate or the label
Inbox.Undone += (_, e) => api.Revert(e.Item.Id, e.Action);
```

Triaged items leave `Items` and come back on undo, at the place they had, with their old labels and assignee.
`Teammates` and `AvailableLabels` fill the Assign and Label lists. When the inbox is empty as it first loads, it gets a
sample inbox, teammates and labels; set `UsesSampleData="False"` to start empty.

### Actions

| Action | Swipe | Key | Result |
|---|---|---|---|
| Archive | right | `E` | Leaves the inbox. |
| Snooze | left | `S` | Leaves the inbox with `SnoozedUntil` set (`SnoozeFor`, or tomorrow at 9:00). |
| Assign | left | `A` | Opens the teammate list; the item leaves with `Assignee` set. |
| Label | left | `L` | Opens the label list; the label is added or removed and the item stays. |
| Undo | | `Z` / `Ctrl+Z` | Takes back the last step. |

The same actions are on each row's More actions menu, and in code: `Archive`, `Snooze`, `Assign`, `ToggleLabel`, `Undo`,
`OpenAssign`, `OpenLabels` and `MoveTo`.

### Narrow and without hints

<Demo name="Narrow" />

`ShowsShortcuts="False"` hides the key hints. `UndoDuration` sets how long the undo bar stays (zero keeps it).

## API reference

<!-- api: InboxTriage -->

<!-- api: InboxTriageItem -->

<!-- api: InboxTriageEventArgs -->

## Accessibility

- The list is named by the title; each row is named by its subject and has a More actions menu with every action, so no gesture is required.
- The current item is marked with an accent bar, not by color alone in the text. Every step is announced with the count left ("Archived: Refund for order 4471. 5 items left.").

### Keyboard

| Keys | Action |
|---|---|
| `J` / `↓`, `K` / `↑` | Next or previous item. |
| `Home` / `End` | First or last item. |
| `E`, `S`, `A`, `L` | Archive, snooze, assign, label the current item. |
| `U` | Mark read or unread. |
| `Z` or `Ctrl+Z` | Undo. |
| `↑` / `↓`, `Enter`, `Escape` in a list | Choose a teammate or label, or close the list. |

## Motion

- A triaged row slides away in the direction of its action and the list closes the gap; an undone item returns in place.
- The counter rolls down digit by digit, the progress bar fills, and the undo bar rises from the bottom.
- Reduced motion fades rows instead of sliding them and keeps the counter static; with motion off changes are immediate.

## Responsive behavior

- The block is at most 560 px wide and the list scrolls past 520 px (`ListMaxHeight`).
- Rows trim the subject and preview to one line each; labels wrap under the sender on narrow widths.
- Swiping works with touch, a pen and a mouse drag, so the block works the same on a phone.

## Related

- `SwipeActions` for the swipeable list on its own.
- `NotificationCenter` for a read-only list of updates.
- `Toast` for a single undoable message.
