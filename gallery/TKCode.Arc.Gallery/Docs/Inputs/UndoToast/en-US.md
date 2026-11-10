---
title: Undo toast
summary: A toast for an action that can be taken back, with a countdown ring and an Undo button; the countdown holds while the pointer or focus is on it.
---

<Demo name="Preview" />

## When to use

`UndoToast` lets an action happen at once and keeps the way back open for a few seconds. It shows what just happened
("Conversation archived"), a ring that counts down the seconds left, and an Undo button. The countdown holds while the
pointer rests on the toast or focus is inside it, and continues from where it stopped, so nobody loses the chance while
reaching for the button. The platform's undo shortcut works too while it is open. When the countdown ends, or the toast
is dismissed, the action stands.

- **Archiving, moving or marking messages**: the item leaves the list at once and comes back on Undo.
- **Removing an item from a board, a playlist or a cart**: quick to do, quick to take back.
- **Bulk changes that are cheap to revert**: "12 messages marked as read".

## When not to use

- **The action cannot be reverted**: deleting for good or sending to someone else; ask first with a `ConfirmPopover`
  or a `Dialog`.
- **A plain confirmation with nothing to take back**: use `Toast`.
- **Several notifications at once**: one undo toast shows the most recent action; use `ToastStack` for a queue of
  notifications.

## Usage

Place one `UndoToast` where toasts appear in your layout (usually the bottom of the window) and call `ShowAsync` after
the action. It answers true when the person undid it.

```xml
<arc:UndoToast x:Name="Toast" HorizontalAlignment="Center" VerticalAlignment="Bottom" />
```

```csharp
inbox.Remove(thread);
if (await Toast.ShowAsync("Conversation archived"))
{
    inbox.Insert(index, thread);
}
else
{
    await api.ArchiveAsync(thread.Id);
}
```

`UndoToast` lives in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`.

A new action shown while the toast is open replaces the earlier one, which then stands (its task answers false and
`Closed` reports `Replaced`); the toast stays in place and only its text and countdown start over. Without
`ShowAsync`, call `Show` and handle `Undone` or `Closed`, or bind `UndoCommand`.

### Countdown

`Duration` (5 seconds by default) is the time to undo, not counting the time under the pointer or with focus inside.
The ring empties clockwise and shows the whole seconds left. `IsCountdownVisible="False"` hides the ring for a quieter
toast; the countdown still runs.

<Demo name="Countdown" />

### Closing reasons

| `UndoToastCloseReason` | When | The action |
|---|---|---|
| `Undone` | Undo button, the undo shortcut or `Undo()` | Is reverted |
| `TimedOut` | The countdown ended | Stands |
| `Dismissed` | The close button, Escape, `Dismiss()` or `IsOpen = false` | Stands |
| `Replaced` | `Show` with a new action while open | Stands |

### Undo shortcut

While the toast is open, the platform's undo shortcut (Ctrl+Z, Cmd+Z on macOS) undoes the action, unless a text field
has focus and needs the shortcut for itself. Set `IsShortcutEnabled` to false when your page has an undo of its own.

### Anatomy

1. **Countdown** (`IsCountdownVisible`): a 28px ring with the seconds left.
2. **Message** (`Message`): what happened, up to two lines.
3. **Undo** (`UndoText`): a small ghost button in the accent color.
4. **Dismiss**: a round button that closes the toast and lets the action stand.

### Content guidelines

Say what happened in the past tense, with the object first.

| Instead of | Write |
|---|---|
| Success! Your conversation has been archived | Conversation archived |
| Click here to undo | Undo |
| Items were moved | 3 files moved to Archive |

## API reference

<!-- api: UndoToast -->

<!-- api: UndoToastClosedEventArgs -->

<!-- api: UndoToastLabels -->

| Type | Values |
|---|---|
| `UndoToastCloseReason` | `Undone`, `TimedOut`, `Dismissed`, `Replaced` |

## Accessibility

- The toast is a polite live region named by its message, so it is read when it appears; its help text names the undo
  shortcut ("Press Ctrl+Z to undo").
- The Undo button is named with the message ("Undo: Conversation archived"); the dismiss button is "Dismiss".
- The countdown holds while focus is inside the toast, so keyboard users have all the time they need.
- The ring is decorative; the time left is not announced every second.

### Keyboard

| Key | Action |
|---|---|
| `Tab` | Reaches Undo, then Dismiss |
| `Enter` / `Space` | Presses the focused button |
| `Escape` (focus in the toast) | Dismisses the toast; the action stands |
| `Ctrl` + `Z` / `Cmd` + `Z` | Undoes the action while the toast is open |

## Motion

- The toast rises 16px from 96% on the morph spring and fades in over 240ms; it sinks 8px to 97% and fades out over
  180ms.
- The ring empties every frame while the countdown runs; it dims to half opacity while the countdown holds.
- A new action keeps the toast in place: only the text and the ring start over.
- Under reduced motion the toast only fades (120ms); with motion off it appears and leaves at once. The countdown runs
  at every motion level.

## Responsive behavior

- The toast is at least 280px and at most 416px wide, or the width it gets when that is less.
- The message wraps to two lines and then ends with an ellipsis.
- A closed toast takes no space.

## Related

- `Toast` and `ToastStack` for notifications without an undo.
- `ConfirmPopover` for actions that cannot be reverted.

| Component | Choose it when |
|---|---|
| `UndoToast` | The action happens at once and can be reverted for a few seconds |
| `ConfirmPopover` | The action cannot be reverted and needs a second press |
| `Toast` | Only to confirm that something happened |
