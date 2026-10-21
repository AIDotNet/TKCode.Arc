---
title: Toast
summary: Brief confirmation for a completed background action.
---

<Demo name="LocalToast" />

## When to use

- Confirming a background action that finished, such as "Changes saved", while the user stays on the page.
- A single local message that the page controls with `IsOpen`.
- A short message that closes by itself or by a swipe, with no queue.

## When not to use

- Several notifications with types, actions or updates: use `ToastStack`.
- A message that stays on the page until the user acts on it: use `Alert`.
- A message that must be answered before the user continues: use `Dialog`.
- A rotating banner across the top of a site: use `AnnouncementBar`.

## Usage

A `Toast` shows its `Title` and an optional `Description`. It is visible while `IsOpen` is `True`, and it closes
after `Duration` (4.5 seconds by default).

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Toast Title="Changes saved"
               Description="Synced to all your devices."
               VerticalAlignment="Bottom" />
</UserControl>
```

### Showing and hiding

Set `IsOpen` to `True` to show the toast again after it closed. The title and description can change while it is
open; the new text rises in and the height follows.

```csharp
toast.Title = "Draft published";
toast.Description = "Readers will see it in a few seconds.";
toast.IsOpen = true;
```

### Duration

`Duration` sets the time before the toast closes. A value of zero or less keeps the toast open until the user
closes it. The timer does not pause on hover, so keep the copy short.

```xml
<arc:Toast Title="Upload complete" Duration="0:0:8" />
<arc:Toast Title="Still syncing" Duration="0" />
```

### Closed event

`Closed` is raised when the toast starts closing. `Reason` tells whether the user pressed the close button, swiped
the toast away, or the time ran out.

```csharp
toast.Closed += (_, e) =>
{
    if (e.Reason == ToastCloseReason.TimedOut)
    {
        log.Text = "The message closed on its own.";
    }
};
```

### App-wide toasts

`Toast.Show` sends a toast from any control in the window to the window's `ToastStack`. Use it when the message
comes from code that does not own a `Toast` control. `ToastKind` selects the icon and color. `Toast.Show` returns
an id that `Toast.Update` and `Toast.Dismiss` accept.

<Demo name="FromAnywhere" />

## API reference

<!-- api: Toast -->

## Accessibility

- `Title` is the name of the toast and `Description` is its help text. The toast is announced politely and does not
  take focus.
- The close button is named `Dismiss notification`. Tab reaches it, and Enter or Space closes the toast.
- The check mark icon is not exposed to assistive technology.
- Keep the text short. The timer does not pause, so a user who needs more time should get a message that stays,
  such as an `Alert`.
- A swipe is a pointer and touch gesture. The close button gives the same result from the keyboard.

## Motion

- The toast rises from below and fades in. Its check mark draws itself a moment later.
- Closing sinks the toast slightly and fades it out. A closed toast takes no more space.
- Changed text rises in and the height follows with a spring.
- Reduced motion only fades the toast in and out and turns off swipe dragging.
- Off motion shows and hides the toast immediately.

## Related

- `ToastStack` for queued, typed and updatable notifications from anywhere in the app.
- `Alert` for a message that stays on the page until it is resolved.
- `AnnouncementBar` for a rotating banner with a countdown.
