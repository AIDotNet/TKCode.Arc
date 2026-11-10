---
title: Presence stack
summary: The people viewing a page right now as overlapping avatars in their own colors; newcomers grow in at the front, leavers shrink away, and the rest fold into a +N chip.
---

<Demo name="LiveViewers" />

## When to use

`PresenceStack` shows who else has this document, board or page open right now. Each viewer is an avatar in a ring of
their own color (the color of their cursor, say), overlapping the next, with the most recent arrival in front. It
follows a live list: a newcomer grows in at the front and sends out one ring while the others slide over; when the row
is full, the last visible person slides into the "+N" chip; someone who leaves shrinks away and the gap closes. People
who are away but still connected fade. A rolling count says how many are viewing, and the chip opens the full list.

- **Collaborative editors**: the avatars in the header of a shared document or design file.
- **Dashboards and tickets**: "who else is looking at this incident right now".
- **Live sessions**: the audience of a presentation or a meeting page.

## When not to use

- **A fixed team or a list of owners**: use `AvatarGroup`, which shows members, not presence.
- **Hundreds of viewers**: show a count instead; the stack is for the handful people want to recognize.
- **Where exactly each person is**: cursors and selection highlights in the document say that; the stack only says who
  is here.

## Usage

Add `PresenceStackViewer` items oldest first, so add newcomers at the end and remove whoever leaves; the stack shows
the most recent first (`IsNewestFirst="False"` keeps the list order).

```xml
<arc:PresenceStack MaxVisibleCount="3">
    <arc:PresenceStackViewer Name="Iris Mendel" Detail="Reading" />
    <arc:PresenceStackViewer Name="Jon Kale" IsIdle="True" />
    <arc:PresenceStackViewer Name="Kim Arden" Detail="Editing" />
    <arc:PresenceStackViewer Name="Lou Prentiss" />
</arc:PresenceStack>
```

For a live list, bind `ItemsSource` to an observable collection. Changes made together (several people at once) move
together.

```csharp
viewers.Add(new PresenceStackViewer("Ada Brook", detail: "Editing the intro"));
viewers.Remove(leaving);
```

A viewer's `Name` gives the initials, the tooltip and the accessible name; `Source` takes a picture; `Detail` adds a
note ("Editing slide 3"); `IsIdle` fades the avatar and turns the ring gray. Without a `Brush` the ring takes a chart
series color picked from the name, so a person keeps the same color across sessions.

### Size and count

`Size` is `Small` (28 px avatars, the default) or `Medium` (36 px). `MaxVisibleCount` (default 4) is how many avatars
show before the chip. `IsCountVisible` and `CountLabel` control the "4 viewing" text after the row; `ViewerCount`
reports the number.

<Demo name="Sizes" />

### The full list

The chip opens a list of everyone viewing, with their detail or "Idle". `IsListOpen` (two way), `OpenList()` and
`CloseList()` drive it from code; Escape or a click outside closes it.

## API reference

<!-- api: PresenceStack -->

<!-- api: PresenceStackViewer -->

## Accessibility

- The stack is a list named "Viewing now, 4 viewing" (`Label` and `CountLabel`). The row is one tab stop: the arrow
  keys move between people and the chip, Home and End go to the ends. Each person's name, idle state and detail are
  their accessible name and tooltip.
- The chip is a button named "2 more viewers"; Enter or Space opens the list, Escape closes it and returns focus.
- Joins and leaves are announced politely ("Ada Brook joined", "Ben Ortiz left"); set `AnnouncesChanges="False"` when
  the page already announces them.
- Idle is said in words, not only by the fade.

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Someone joins | Grows in at the front, one ring goes out, the others slide over | Fades in, the others move at once | At once |
| Someone leaves | Shrinks out where they were, the gap closes | Fades out, the gap closes at once | At once |
| Row full | The last person slides into the chip and fades | Fades | At once |
| Count and chip | Digits roll | Change at once | Change at once |
| Row width | Springs | At once | At once |

## Responsive behavior

- The stack is as wide as its avatars and count; it does not stretch. Lower `MaxVisibleCount` in narrow headers.
- Tooltips need a pointer; on touch, the chip's list shows every name and detail.
- Small avatars are 28 px, below the 44 px touch size; each is still focusable and the list is one tap away.

## Related

| Component | Choose it when |
|---|---|
| `AvatarGroup` | You show the members of a team, not who is here now |
| `Avatar` | You show one person |
| `LiveVisitors` | You count anonymous visitors on a map |
