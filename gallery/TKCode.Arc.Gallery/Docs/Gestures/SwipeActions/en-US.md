---
title: Swipe actions
summary: Reveal row actions with a swipe, or from the same actions in a menu.
---

<Demo name="Inbox" />

## When to use

`SwipeActions` is a list whose rows reveal actions when they are swiped sideways. It is built for touch-first triage,
where a person works through a long list of items one at a time and each item needs one or two quick decisions. The
actions sit under the row, so the decision is made where the item is read, and a full swipe removes the row and closes
the gap without a separate confirmation.

- **Mail triage on a tablet**: a support or sales inbox where a swipe left archives a thread and a swipe right marks it
  unread. Both actions are one tap from the row, and the list closes the gap after an archive.
- **Field service job list on a phone**: a technician swipes right to mark a job done and left to reschedule it. The
  job card stays readable while the actions wait under it, so the decision is made without opening the job.
- **Approvals on a phone**: a manager approves a purchase request with an accent swipe and rejects it with a danger
  swipe. Full swipe is turned off for the rejection row, so a long drag cannot reject a request by accident.
- **Notification center in a companion app**: a sign-in alert is dismissed with a danger swipe, and a long-running
  export is muted with an action that keeps the row in place.

## When not to use

- **A desktop table, or a list used mainly with a mouse**: use `ContextMenu` on the row or `DropdownMenu` on a row
  button. Swipe gestures are the wrong primary path for a pointer, and the menu is already on every row.
- **Reordering rows by dragging them up and down**: use `ReorderableList`. Swipe gestures lock to the horizontal axis
  and never change the order of the rows.
- **Long, paged or sorted datasets with many columns**: use `SortableDataTable`. Swipe rows are not virtualized, so a
  list of thousands of rows should be paginated before it is shown here.
- **One destructive action outside a list**, such as deleting a project from its settings page: use `HoldToConfirm`,
  which needs a deliberate hold instead of a gesture a row can trigger by accident.
- **More than two quick actions on a side**: the revealed width grows with each action. Keep the two most common
  actions on the swipe and move the rest into the row's More actions menu, or use `ContextMenu` for a full list.

## Usage

`SwipeActions` is the list. Each item is a `SwipeActionsRow`, or a data item shown through `ItemTemplate`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SwipeActions Label="Inbox" ItemsSource="{Binding Messages}">
        <arc:SwipeActions.LeadingActions>
            <arc:SwipeAction Label="Unread" Tone="Accent" KeepsRow="True" />
        </arc:SwipeActions.LeadingActions>
        <arc:SwipeActions.TrailingActions>
            <arc:SwipeAction Label="Archive" />
            <arc:SwipeAction Label="Delete" Tone="Danger" />
        </arc:SwipeActions.TrailingActions>
        <arc:SwipeActions.ItemTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Subject}" />
            </DataTemplate>
        </arc:SwipeActions.ItemTemplate>
    </arc:SwipeActions>
</UserControl>
```

Leading and trailing actions are shared by every row. The outermost action of a side is the one that a full swipe
runs.

`SwipeActions` derives from Avalonia's `ItemsControl`, so `ItemsSource`, `ItemTemplate` and the item containers work as
they do on a list. Each container is a `SwipeActionsRow`, which derives from `ContentControl`, so a row can also be used
on its own. Each `SwipeAction` is a plain model object, not a control: it holds the label, the icon, the tone and the
command, and the row draws it.

Leading actions sit under the left edge and are revealed by a swipe to the right. Trailing actions sit under the right
edge and are revealed by a swipe to the left. Each side can hold any number of actions, but the revealed width grows
with each one, so two per side is the practical limit.

### Anatomy

1. **List frame** (`Label` names it): one surface around all rows, with a hairline separator between rows. The
   separator starts 16 px in from the left edge (`SeparatorInset`).
2. **Row body** (`MinRowHeight` 64 px): the content you supply, with 16 px of padding on the left, 12 px above and
   below, and 8 px on the right before the More actions button. The row grows taller when its content is taller.
3. **More actions button**: a 32 px square with a 12 px radius and an 18 px ellipsis. It is the
   keyboard and screen reader path to the same actions, so it is always present on every row.
4. **Revealed actions**: each action is 76 px wide at rest, with its icon in a 20 px glyph over its label. The icon and
   label are 3 px apart. A neutral action fills with the foreground color mixed 20% into the surface; the accent and
   danger actions use their theme colors.
5. **Full swipe stretch**: when a swipe passes the commit threshold, the outermost action stretches across the whole row.
6. **More actions menu**: a menu at least 176 px wide, placed 6 px below the More actions button. It lists the row's
   actions in the same order as the swipe, and marks a destructive action so it reads as one.

### Tones

`Tone` sets the fill of a revealed action. Choose the tone by the result, not by the position on the row.

| Tone | Fill | Use it for |
|---|---|---|
| `Neutral` (default) | A quiet neutral fill | Archive, snooze, move, and other reversible housekeeping |
| `Accent` | The accent color | Done, approve, mark unread: a positive or primary result |
| `Danger` | The danger color | Delete, reject, dismiss: a result that removes or refuses something |

Pair a `Danger` action with a label that names the result, such as "Delete" or "Reject", so the color is not the only
signal. Keep at most one `Danger` action on each side, and place it outermost, so a full swipe can only reach it
deliberately.

### Actions that run

`Command` runs when the action is chosen, by a full swipe, a tap on the revealed action or the More actions menu.
`Invoked` on the `SwipeAction` and `ActionInvoked` on the list or row tell you which row and item it was. The
`Invoked` handler of the action runs first, then its `Command`. `CommandParameter` defaults to the row's data item when
it is not set.

`KeepsRow="True"` runs the action and returns the row to its place instead of removing it. Use it for toggles such as
unread, mute and flag, where the row should stay on the list.

```csharp
inbox.ActionInvoked += (_, e) =>
{
    if (!e.Action.KeepsRow)
    {
        messages.Remove((Message)e.Item!);
    }
};
```

Remove the data item in the handler. An action that does not keep the row runs only after the row has slid away and
collapsed, so the list closes the gap without a jump. The handler can remove the item directly. If the item is still in
the list 1.4 seconds after the action ran, the row slides back into place, so an action that never removes its item
does not leave an empty band behind.

The inbox demo keeps the removed message and offers an undo, which is the pattern to follow for an archive or delete.
A removed row is inserted back at its old index when the person taps Undo.

### Rows with their own actions

A `SwipeActionsRow` can declare its own actions. A row that declares them ignores the list's leading and trailing
actions. `IsFullSwipeEnabled` on a row overrides the list's setting: `False` keeps the row from committing on a full
swipe, so the person must tap the revealed action; `True` allows it, even when the list turns it off; `null` (the
default) follows the list.

<Demo name="OwnActions" />

### Resting open

`Open(SwipeActionSide.Trailing)` shows a row's actions without a gesture, springing the row to its resting width. A row
with one revealed action rests at 76 px; a row with two rests at 152 px. The list holds one open row at a time: opening
a row puts the previous one away. `Close()` puts an open row away, and `SwipeActions.CloseRows()` closes the open row of
the list. `OpenRow` returns the row that is open, or `null`.

```csharp
// Show the export row's actions when the notifications screen opens.
export.Open(SwipeActionSide.Trailing);
```

Use a resting open row to show that a swipe is possible, such as the first time a person opens a notification list.
Do not leave a row open permanently, because it hides the row's content and makes the list look broken.

<Demo name="RestingOpen" />

### Full swipe

A full swipe commits the outermost action of a side. The threshold is the larger of two values: the open width of the
side plus 48 px, or 56% of the row's width. Once the row passes the threshold, it stays armed until the drag comes back
20 px, so a small wobble does not cancel the commit. A flick faster than 900 px/s also commits when it carries the row past the
threshold in its direction.

Set `IsFullSwipeEnabled="False"` on the list, or on a row, for an action where a stray long drag would be costly, such
as a rejection or a payment. The revealed action still runs on a tap.

### Binding to a view model

In an MVVM app the view model owns the collection and the rules that remove items. The list binds to the collection,
and the actions are wired in code, because `SwipeAction` is a plain model object and not a control in the visual tree.

```csharp
public partial class InboxViewModel : ObservableObject
{
    private readonly IMailService _mail;

    public InboxViewModel(IMailService mail)
    {
        _mail = mail;
    }

    public ObservableCollection<MailThread> Threads { get; } = [];

    [RelayCommand]
    private async Task ArchiveAsync(MailThread thread)
    {
        await _mail.ArchiveAsync(thread.Id);
        Threads.Remove(thread);
    }

    [RelayCommand]
    private void ToggleUnread(MailThread thread)
    {
        thread.IsUnread = !thread.IsUnread;
    }
}
```

```xml
<arc:SwipeActions Label="Inbox"
                  ItemsSource="{Binding Threads}"
                  RowLabelBinding="{Binding Subject}">
    <arc:SwipeActions.ItemTemplate>
        <DataTemplate>
            <StackPanel Spacing="2">
                <TextBlock Text="{Binding Sender}" FontWeight="SemiBold" />
                <TextBlock Text="{Binding Subject}" />
                <TextBlock Text="{Binding Preview}" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </DataTemplate>
    </arc:SwipeActions.ItemTemplate>
</arc:SwipeActions>
```

```csharp
// In the view's code-behind, where the view model is available:
var archive = new SwipeAction { Label = "Archive", Icon = ArcIcons.Archive, Command = viewModel.ArchiveCommand };
var unread = new SwipeAction { Label = "Unread", Icon = ArcIcons.Mail, Tone = SwipeActionTone.Accent, KeepsRow = true, Command = viewModel.ToggleUnreadCommand };
inbox.TrailingActions.Add(archive);
inbox.LeadingActions.Add(unread);
```

- The list reads `ItemsSource`, and the view model changes the collection to add or remove rows. Use an `ObservableCollection<T>` so the list updates when items are removed.
- `RowLabelBinding` names each generated row for its More actions button, for example "More actions for Invoice #2041".
- The command receives the row's data item as its parameter, so one command serves every row.
- Compiled bindings need `x:DataType` on the view and on the data template (the examples here leave it out for
  brevity).

### Example: mail triage on a tablet

A support inbox on a tablet. The subject is the row's first line in bold, the sender and time give context, and the
preview tells the person whether the thread needs them. The leading action marks the thread unread with the accent,
and the trailing actions archive it or delete it. The rows in this scene declare no actions of their own, so every row
uses the list's actions. The screenshot shows the resting list; the revealed actions appear only when a row is swiped
or opened.

![An inbox list with three rows, each with a sender, a subject and a preview](images/InboxTriage.png)

```xml
<Border Width="560"
        CornerRadius="12"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}"
        ClipToBounds="True">
    <arc:SwipeActions Label="Support inbox">
        <arc:SwipeActions.LeadingActions>
            <arc:SwipeAction Label="Unread" Icon="{x:Static arc:ArcIcons.Mail}" Tone="Accent" KeepsRow="True" />
        </arc:SwipeActions.LeadingActions>
        <arc:SwipeActions.TrailingActions>
            <arc:SwipeAction Label="Archive" Icon="{x:Static arc:ArcIcons.Archive}" />
            <arc:SwipeAction Label="Delete" Icon="{x:Static arc:ArcIcons.Trash2}" Tone="Danger" />
        </arc:SwipeActions.TrailingActions>
        <arc:SwipeActionsRow Label="Invoice #2041 is overdue">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Northwind Finance" FontSize="14" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Invoice #2041 is overdue" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Payment was due 14 October 2026. Reply with a date to keep the account active."
                           FontSize="13" TextWrapping="Wrap"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </arc:SwipeActionsRow>
        <arc:SwipeActionsRow Label="Acme Analytics renewal">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Acme Analytics" FontSize="14"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Renewal quote for 2027" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="The seat count moved from 40 to 52. Review before Friday."
                           FontSize="13" TextWrapping="Wrap"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </arc:SwipeActionsRow>
        <arc:SwipeActionsRow Label="Team lunch on Thursday">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Maya Chen" FontSize="14"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Team lunch on Thursday" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Table for six at 12:30. Tell me if you cannot make it."
                           FontSize="13" TextWrapping="Wrap"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </arc:SwipeActionsRow>
    </arc:SwipeActions>
</Border>
```

The three rows show the three things a triage list needs: a subject that asks for an action, a thread that is only
informational, and a personal message. The list uses one set of actions, so the same swipe means the same thing on
every row. The danger action is outermost, so only a deliberate full swipe reaches it, and the More actions menu
offers the same actions for a keyboard user.

### Content guidelines

- **Action label**: one short word, such as "Archive", "Delete", "Done" or "Unread". The label sits under a 20 px icon
  in a 76 px column, so a longer phrase is cut off. Keep the verb in the menu, where the full width is available.
- **Row label**: the subject or the item's name, for example "Invoice #2041 is overdue". It names the row for the More
  actions button, so write it as a sentence a screen reader user would recognize.
- **List label**: the collection's name, "Inbox", "Tasks", "Notifications". It is the list's accessible name.
- **Result wording**: name the result, not the gesture. "Delete" describes what happens; "Swipe to delete" describes
  how to reach it, and the swipe is not the only way.

| Instead of | Write |
|---|---|
| Swipe to archive | Archive |
| Move this thread to the archive folder | Archive |
| Remove | Delete |
| Mark as unread | Unread |
| Trash it forever | Delete |

### Best practices

**Do**

- Put the action a full swipe should run in the outermost position of a side, since that is the one it commits. Keep
  a destructive action out of that position unless an Undo is offered.
- Use `KeepsRow="True"` for toggles that change the row's state, such as unread or flag, and remove the item only for
  actions that finish with it. A toggle that removes the row is confusing when the person wants to undo it.
- Give each row a `Label` through `RowLabelBinding`, so the More actions button names the row it belongs to.
- Provide an Undo path after a removing action, such as a button or a toast, because a full swipe is quick and easy
  to trigger by accident.
- Keep the same actions on every row of one list, unless a row has a real reason to differ, such as a locked item.

**Avoid**

- Gestures as the only way to act. The More actions button is always present, and it serves keyboard and screen reader
  users who cannot swipe.
- Danger actions on both sides of a row, which makes a stray swipe in either direction destructive.
- Long labels on revealed actions. The label is cut to the 76 px column, so the meaning must fit in one word.
- Turning off full swipe on every row. Keep it on for reversible actions and turn it off only for a rejection or a
  payment, where the extra tap is the safeguard.
- Thousands of rows in one list. Rows are not virtualized, so the list builds every container; page the data instead.

### Common questions

**Why does my handler run after the row has slid away?** An action that removes its item runs after the row slides out
and collapses, so the list closes the gap without a visible jump. If you need the action to run immediately, set
`KeepsRow="True"` and remove the item later, for example after the server confirms the change.

**What happens if the item is not removed?** If the item is still in the list 1.4 seconds after the action ran, the row
slides back into place. Remove the item in the handler, or set `KeepsRow` when the row should stay.

**How do I stop a full swipe from running a destructive action?** Set `IsFullSwipeEnabled` to `False` on the row. The
person can still reveal the action and tap it, but a long drag only opens the row.

**Why do my row's actions ignore the list's actions?** A row that declares its own leading or trailing actions uses only
those. The list's actions are the default for rows that declare none.

**Can I use a single row without a list?** Yes. `SwipeActionsRow` works on its own, with its own actions and its own
`Open`, `Close` and `Run` methods. It has no list, so the single-open rule and the list's full-swipe setting do not
apply.

## API reference

<!-- api: SwipeActions -->

<!-- api: SwipeAction -->

<!-- api: SwipeActionsRow -->

### Tone and side values

| Type | Value | Meaning |
|---|---|---|
| `SwipeActionTone` | `Neutral` | A quiet neutral fill (the default) |
| `SwipeActionTone` | `Accent` | The accent fill |
| `SwipeActionTone` | `Danger` | The danger fill, for a destructive or refusing result |
| `SwipeActionSide` | `None` | The row is closed |
| `SwipeActionSide` | `Leading` | Under the left edge, revealed by a swipe to the right |
| `SwipeActionSide` | `Trailing` | Under the right edge, revealed by a swipe to the left |

## Accessibility

- The list has a name from `Label`, and each row is a list item. Each row has a More actions button named "More actions for" the row's `Label`.
- Mouse and keyboard users never need the gesture: Tab to the More actions button, press Enter or Space to open the menu, and move with the arrow keys.
- Escape closes the menu or puts away an open row. Pressing elsewhere also puts an open row away.
- The revealed actions are hidden from assistive technology and are not in the Tab order.
- When a row is removed from its menu, focus moves to the neighbouring row's More actions button. When the last row
  leaves, focus lands on the list itself.
- The menu lists the same actions as the swipe, and a destructive entry is marked as destructive, so a screen reader
  user hears the risk before the action runs.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Moves between the rows' focusable content and More actions buttons; revealed actions are skipped |
| `Enter` or `Space` on a More actions button | Opens the row's menu with its first item highlighted |
| `Up` or `Down` on a More actions button | Opens the menu at its last or first item, when it is closed |
| `Up` / `Down` in the menu | Moves between the actions |
| `Enter` in the menu | Runs the highlighted action |
| `Escape` | Closes the menu, or puts an open row away |

## Motion

- The row follows the pointer. Past its last stop it resists, so the row stretches a little and then returns.
- On release the row settles on the nearest stop with a spring that keeps the release speed. A quick flick
  counts as a swipe to the stop in its direction.
- A full swipe stretches the outermost action across the row, and the row slides away. The list then closes the gap.
- Reduced motion jumps the row to its stop and fades rows in and out without a height animation. Off motion moves
  everything at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Row follows the pointer | Follows the pointer, with rubber-band resistance past the last stop | No follow; the row jumps to its stop | No follow; the row jumps to its stop |
| Release | Springs to the stop with the release speed: smooth to close, snappy to open | Jumps to the stop | Immediate |
| Full swipe | The outermost action stretches across the row, and the row slides away | Fades out | Immediate |
| Row removed | The row's height collapses on a smooth spring after 120 ms, and the list closes the gap | Fades out, no height animation | Immediate |
| Row added | The row grows in on a smooth height spring with a fade | Fades in, no height animation | Immediate |
| More actions menu | Opens and closes with the menu surface's transition | The menu surface's reduced transition | The menu surface's off transition |

The swipe itself is the only motion the person controls, so keep it direct and responsive on every motion level. Use
the spring on release to show where the row will stop, and skip decorative movement on the list around it.

## Related

- `ContextMenu` and `DropdownMenu` for row actions on desktop.
- `ReorderableList` for dragging rows into a new order.
- `HoldToConfirm` for a single destructive action outside a list.
- `SortableDataTable` for large, sortable datasets.

| Component | Choose it when |
|---|---|
| `ContextMenu` / `DropdownMenu` | The primary input is a mouse, or the row has many actions |
| `ReorderableList` | The person changes the order of the rows by dragging them |
| `SortableDataTable` | The data has columns, sorting or a large number of rows |
| `HoldToConfirm` | One destructive action needs a deliberate hold, outside a list |
| `Dialog` | The consequences must be read before the action runs |
| `Toast` | An action ran and the result should be reported with an undo |
