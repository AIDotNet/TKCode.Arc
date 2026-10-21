---
title: Notification center
summary: A home for updates with read state, grouped information, and animated disclosure.
---

<Demo name="Preview" />

## When to use

- Updates need a place to wait until people get to them: mentions, finished jobs, approvals, account warnings.
- People need to mark updates read or unread, open the details of one, or clear the old ones.
- The app header has room for one icon button that also shows how many updates are unread.

## When not to use

- A short confirmation that needs no follow-up, such as "Saved": use `Toast`.
- A problem on the current page that people must see now: use `Alert`.
- A full history with search and filters: build a page with `Timeline` or a table.

## Usage

Add `NotificationItem` children, or set `ItemsSource`. Each item has a `Title`, an optional `Description`, a `Time`
label and an `IsRead` state. An update from a person sets `ActorName` and `ActorSource` and shows their avatar. A system
update shows the icon of its `Tone`: Info, Success or Warning.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:NotificationCenter>
        <arc:NotificationItem Id="deploy" Title="Deploy finished" Description="Version 2.4.1 is live." Time="18m" Tone="Success" />
        <arc:NotificationItem Id="mention" Title="Mia Chen mentioned you" Time="2m" ActorName="Mia Chen" />
        <arc:NotificationItem Id="digest" Title="Your weekly digest is ready" Time="Yesterday" IsRead="True" />
    </arc:NotificationCenter>
</UserControl>
```

The bell shows the unread count, up to "9+". Click it to open the panel. Click a row to show its details with
"Mark read" and "Dismiss". Only one row is open at a time.

### Read state and your data

The control changes `IsRead` on the item and raises `ReadChanged`. "Mark all read" raises it once for each item it
marks. "Dismiss" removes the item from `Items`, or from `ItemsSource` when that is a list you can change, and raises
`Dismissed`. "Clear read" raises it once for each read item. Save the changes to your store in these handlers. When
your store changes, the bell and the panel follow the items and the collection.

<Demo name="ReadState" />

```csharp
center.ItemsSource = store;   // an ObservableCollection<NotificationItem>
center.ReadChanged += (_, e) => api.SetRead(e.Item.Id, e.IsRead);
center.Dismissed += (_, e) => api.Dismiss(e.Item.Id);
```

A read-only source keeps its items, and the control hides the dismissed ones.

### Unread view and empty state

The panel lists every update, or only the unread ones. `View` is two-way. When the list is empty the panel says so:
"All clear", or "Nothing unread". When there are read updates, a link goes back to the All view.

<Demo name="UnreadView" />

### In an app header

The panel opens 12 pixels below the bell and stays 12 pixels inside the window, so the bell can sit at the edge of
a header. The unread badge reaches 5 pixels past the bell's top right corner, so leave that much room around the bell
in a container that clips. In a window 380 pixels wide or less the panel uses tighter spacing, and "Mark all read"
shows its icon only.

<Demo name="AppHeader" />

### Wording

`Label` names the panel and the bell. `Labels` holds every other word, with English defaults. Assign a new
`NotificationCenterLabels` to change them. In `ReadCount`, `{0}` is the rolling number. Words with a count, such as
the summary under the heading, are functions you can set in code.

<Demo name="CustomWording" />

```csharp
center.Labels = new NotificationCenterLabels
{
    Summary = unread => unread > 0 ? $"{unread} new" : "Nothing new",
    Trigger = (label, unread) => $"{label}, {unread} new"
};
```

### Opening from code

`IsOpen` is two-way. `Open()` and `Close()` do the same. `MarkAllRead()` and `ClearRead()` run the bulk actions and
raise the same events as the buttons.

```csharp
center.Open();
center.MarkAllRead();
```

## API reference

<!-- api: NotificationCenter -->

<!-- api: NotificationItem -->

## Accessibility

- The bell is a button named by `Label` and the unread count, such as "Notifications, 3 unread". It reports whether the
  panel is expanded.
- Opening the panel moves focus to its close button. Tab moves through close, All and Unread, "Mark all read", the
  rows, the open row's buttons and "Clear read", and wraps around inside the panel.
- Escape and the close button close the panel and return focus to the bell.
- All and Unread are toggle buttons. Each row is a button named by its title and state, such as "Deploy finished,
  unread. Show details", and reports whether it is expanded.
- After you mark a row read in the Unread view, or dismiss a row, focus moves to the next row, then the previous one,
  then the Unread button. After "Mark all read" focus returns to the current view button.
- The summary under the heading is a polite live region, so the new count is read out.

## Motion

- The panel grows out of the bell: it starts 6 pixels toward the bell at scale 0.97 and fades in over 240 ms. It
  leaves in 120 ms at half the distance and scale 0.98.
- The bell tips while the panel is open. The badge pops in and out, and its number rolls up or down.
- Rows settle in 4 pixels from below, 35 ms apart, when the panel opens or a view adds them. Leaving rows fade and
  close their height on a smooth spring. After a bulk action they leave one after another, at most 200 ms apart from
  the first.
- The counts roll, the summary rises into its new wording, and the All and Unread highlight glides between the
  buttons. Details, the footer and the empty state open and close their height on the same spring.
- Reduced motion keeps only short fades of 120 ms, with no movement, scaling or blur. The panel shows and hides at
  once. Off applies every change at once.

## Related

- `Toast` for a short message that goes away by itself.
- `Popover` for other content anchored to a button.
- `Badge` for a count or a status on another control.
