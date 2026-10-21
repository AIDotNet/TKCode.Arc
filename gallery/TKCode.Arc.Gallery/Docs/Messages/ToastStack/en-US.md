---
title: Toast stack
summary: Stack short results at the edge until you reach for them.
---

<Demo name="Stack" />

## When to use

- Results of asynchronous work from anywhere in the app, such as an upload that failed or an invite that was sent.
- Notifications that need a type (success, info, warning, error) or an action such as Undo or View.
- A loading message that becomes its result when the work finishes.

## When not to use

- One local confirmation controlled by the page: use `Toast`.
- A message that stays on the page until the user resolves it: use `Alert`.
- A banner across the top of a site that rotates messages: use `AnnouncementBar`.
- A message that must be answered before the user continues: use `Dialog`.

## Usage

Place a `ToastStack` over the content, for example as the last child of the window's root panel. Call
`Toast.Show` from any control in the window to add a toast. If the window has no `ToastStack`, `ToastHost` creates one
on the overlay layer.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <Grid>
        <!-- Page content goes here. -->
        <arc:ToastStack Label="Notifications" />
    </Grid>
</UserControl>
```

### Showing a toast

`Show` takes a `ToastOptions` and returns the toast id. `Kind` selects the icon and color. `Toast.Show` takes the
same values as arguments and sends them to the stack of the window that owns `button`.

```csharp
stack.Show(new ToastOptions
{
    Kind = ToastKind.Warning,
    Title = "Storage almost full",
    Description = "92% of 10 GB used."
});

Toast.Show(button, "Invite sent", ToastKind.Success, "Maya will get an email shortly.");
```

### Loading and update

Pass an id in `ToastOptions` and call `Update` with the same id to change the toast in place. Use `Duration` set to
`Timeout.InfiniteTimeSpan` to keep a toast until it is updated or dismissed. The toast icon, text and height morph
to the new state.

```csharp
var id = stack.Show(new ToastOptions
{
    Kind = ToastKind.Loading,
    Title = "Uploading report.pdf",
    Duration = Timeout.InfiniteTimeSpan
});

await UploadAsync();

stack.Update(id, new ToastUpdate
{
    Kind = ToastKind.Success,
    Title = "Report uploaded",
    Description = "Shared with 4 people."
});
```

### Actions

`ToastAction` adds a button to a toast. After the action runs, the toast closes unless the callback updates it. An
Undo can update the toast into its result and remove the button with `RemoveAction`.

```csharp
stack.Show(new ToastOptions
{
    Title = "Project archived",
    Action = new ToastAction("Undo", id => stack.Update(id, new ToastUpdate
    {
        Kind = ToastKind.Success,
        Title = "Project restored",
        RemoveAction = true
    }))
});
```

### Position and limits

`Position` moves the stack to the bottom left, center or right edge. `VisibleToasts` sets how many toasts show at
once, and `Limit` drops the oldest toasts beyond a count.

<Demo name="Position" />

```xml
<arc:ToastStack Position="BottomCenter" VisibleToasts="3" Limit="12" />
```

## API reference

<!-- api: ToastStack -->

<!-- api: ToastOptions -->

## Accessibility

- The notification area is a region landmark named by `Label`. The keyboard shortcut Alt+T moves focus into the
  stack and expands it.
- Tab moves between the action and close buttons of the toasts. Escape closes the focused toast.
- New and updated toasts are announced as `Type: Title. Description`. Errors are announced assertively, the other
  types politely.
- Each toast has a name such as `Error: Couldn't send invite`, so a screen reader can tell the types apart.
- Timers pause while the stack is hovered or focused, while a toast is dragged, and while the window is minimized.

## Motion

- A new toast rises from below and fades in. Older toasts tuck behind the front toast and shrink slightly.
- Hovering or focusing the stack fans the toasts out into a list, and leaving it folds them back.
- Swiping a toast to the right follows the pointer. A fast or long swipe dismisses it with a fade.
- Updates morph the icon, text and height in place. A loading ring turns while the work runs.
- Reduced motion jumps the positions and only fades toasts in and out. Off motion shows every change immediately.

## Related

- `Toast` for one local confirmation that the page controls with `IsOpen`.
- `Alert` for a persistent message inside the page.
- `AnnouncementBar` for a rotating banner with a countdown.
