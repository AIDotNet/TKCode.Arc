---
title: Toast stack
summary: Stack short results at the edge until you reach for them.
---

<Demo name="Stack" />

## When to use

`ToastStack` holds the short results of work that people started and do not need to wait for: an export that
finished, an invite that was sent, a file that failed to upload. It sits at the edge of the window, over the page, and
keeps the newest result in front while older ones tuck behind it. Any control in the window can add to it, so a
background job, a save handler or a command palette can report what it did without knowing about the page layout.

- **Background job results in an admin console**: "Export ready" after a billing export finishes, or "Import failed"
  with the row count, while the operator keeps working in the table.
- **Confirmations with an undo window**: "Project archived" with an Undo button in a project list, where the action
  can be reversed for a few seconds and the toast turns into "Project restored" when it is.
- **Loading that becomes its result**: "Uploading report.pdf" in a loading state that updates in place to "Report
  uploaded" or to an error with the reason, instead of two separate messages.
- **Typed notifications from several sources**: warnings about storage or quota, success messages from a checkout
  flow and errors from a sync, all in one place with one reading order and one dismissal gesture.

## When not to use

- **One confirmation that the page controls with its own state**: use `Toast`, which has no queue and is set with
  `IsOpen`. It is simpler when only one message exists.
- **A message that stays on the page until it is resolved**, such as a plan that is past its limit or a failed
  payment: use `Alert`, because a stack hides older messages behind the front one and a timer closes the rest.
- **A site-wide banner that rotates messages**, such as a maintenance notice or a promotion: use `AnnouncementBar`.
- **A message the person must answer before continuing**, such as a delete confirmation or a permission request: use
  `Dialog`, which takes focus and blocks the page until a choice is made.

## Usage

Place a `ToastStack` over the content, for example as the last child of the window's root panel. Call
`Toast.Show` from any control in the window to add a toast. If the window has no `ToastStack`, `ToastHost` creates one
on the overlay layer.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <Grid>
        <!-- Page content goes here. -->
        <arc:ToastStack Label="Notifications" />
    </Grid>
</UserControl>
```

`ToastStack` derives from Avalonia's `TemplatedControl` and implements `IToastService`, the interface of the app-wide
API: `Show`, `Update`, `Dismiss` and `Count`. Code that holds the stack can call those members directly. Code that
only knows the window uses `Toast.Show`, `Toast.Update` and `Toast.Dismiss`, or `ToastHost.GetService`, which returns the
same interface for the window's stack. A window has one active stack: a placed `ToastStack` wins over one that
`ToastHost` created.

### Anatomy

1. **Region** (the stack itself): a region landmark named by `Label`, "Notifications" by default. When
   `IsHotkeyEnabled` is on, the name ends with "(Alt+T)" so a screen reader user learns the shortcut.
2. **List** (`PART_List`): the area that holds the cards. Only the list rectangle takes pointer input, so the
   page under the rest of the stack stays clickable. The list is 360 px wide (22.5rem) or the window width minus 32 px,
   whichever is smaller.
3. **Front card**: the newest toast, drawn with the floating shadow (and the same shadow whenever the stack is expanded).
4. **Tucked cards**: older toasts behind the front card. Each step back shows 14 px of the card in front, shrinks by
   5%, uses the muted surface color and hides its content. `VisibleToasts` (3 by default) sets how many show; the rest
   wait behind.
5. **Icon slot** (`PART_Icon`): an 18 by 20 px slot with an 18 px Lucide glyph at 1.75 stroke, followed by a 12 px gap.
   The color follows the kind: success, accent, warning or danger. A loading toast shows a 15 px spinner instead.
6. **Copy** (`PART_Title` and `PART_Description`): the title in 14 px medium weight and the description in the secondary
   text color, both wrapped at a 20 px line height. The description sits 2 px below the title.
7. **Action button** (`PART_Action`): a 28 px pill with 12 px side padding, Secondary variant at small size. It sits to
   the right of the copy, or under the copy in a card narrower than 336 px.
8. **Close button** (`PART_Close`): a 28 px round ghost button with a 16 px X glyph. Its accessible name is "Dismiss
   notification".

Card content is padded 16 px on the left and 12 px on the other sides. The stack sits 24 px from the window edge
(16 px when `IsContained` is on) and is centered at the bottom edge in windows narrower than 480 px.

### Showing a toast

`Show` takes a `ToastOptions` and returns the toast's id. `Kind` selects the icon, color and spoken prefix, and
`Title` is required. `Toast.Show` takes the same values as arguments and sends them to the stack of the window that
owns the `owner` visual.

```csharp
stack.Show(new ToastOptions
{
    Kind = ToastKind.Warning,
    Title = "Storage almost full",
    Description = "92% of 10 GB used."
});

Toast.Show(button, "Invite sent", ToastKind.Success, "Maya will get an email shortly.");
```

Each toast is announced with its kind and copy, and the stack keeps the newest toast in front. `Count` reports how
many toasts are queued, both the ones on screen and the ones waiting behind them, and `ToastsChanged` is raised
whenever a toast is shown, updated or dismissed. Use that event to update a counter or a badge in the header.

### Kinds and lifetimes

`ToastKind` sets the icon and color, and the default lifetime. The default lifetime of the stack is 5 seconds.

| Kind | Icon | Default lifetime | Spoken prefix |
|---|---|---|---|
| `Success` | Circle check in the success color | `Duration` (5 seconds) | "Success:" |
| `Info` | Info glyph in the accent color | `Duration` (5 seconds) | "Info:" |
| `Warning` | Triangle alert in the warning color | 1.6 times `Duration` (8 seconds) | "Warning:" |
| `Error` | Circle X in the danger color | 1.6 times `Duration` (8 seconds) | "Error:" (announced assertively) |
| `Loading` | A turning ring | None: it waits for an update | "In progress:" |

Warnings and errors stay longer because a person may need to read and act on them. A loading toast has no timer, so
it stays until the work updates it or the stack dismisses it. Set `ToastOptions.Duration` or `ToastUpdate.Duration` to
override the default for one toast. `Timeout.InfiniteTimeSpan` keeps a toast until it is closed, and zero or less also
keeps it open.

### Loading and update

Pass an id in `ToastOptions` and call `Update` with the same id to change the toast in place. `Show` with an id that
is already on the stack updates that toast instead of adding one. Use `Duration` set to `Timeout.InfiniteTimeSpan` to
keep a toast until it is updated or dismissed. The toast icon, text and height morph to the new state, and the timer
restarts with the new lifetime.

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

`ToastUpdate` changes only the members it sets. A null `Kind`, `Title`, `Description` or `Action` keeps the current
value. A null `Duration` restarts the toast with the default lifetime for its kind. Use `RemoveDescription` or
`RemoveAction` to take the description or the action away.
An update to an id that is no longer on the stack does nothing, so a late result for a dismissed toast is safe to
send.

### Actions

`ToastAction` adds a button to a toast. The button takes the label and a callback that receives the toast id. After
the callback runs, the toast closes unless the callback updated it, so a plain action such as "View" needs no extra
code. An Undo can update the toast into its result and remove the button with `RemoveAction`.

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

The callback decides what the toast does next. If it neither updates nor dismisses the toast, the stack dismisses it
after the callback returns. The callback runs on the UI thread, so start long work from it rather than doing it inside.
An action that updates its toast and removes its button leaves a toast with only a close button; keyboard focus moves
to that button so the person can keep going with the keyboard.

### Position and limits

`Position` moves the stack to the bottom left, center or right edge. The default is `BottomRight`. A window narrower
than 480 px always centers the stack, whatever the position. `VisibleToasts` sets how many toasts show at once, and
`Limit` drops the oldest toasts beyond a count. `Limit` never goes below one.

<Demo name="Position" />

```xml
<arc:ToastStack Position="BottomCenter" VisibleToasts="3" Limit="12" />
```

`IsContained` pins the stack 16 px inside its own bounds instead of 24 px from the window edge. Turn it on for a
preview, a panel or any stack that lives inside a smaller area of the window rather than over the whole window.

### Driving it from code

A `ToastStack` is a service rather than a control with a value to bind, so a view model receives the interface, not
the control. Get the window's service from the view once, and pass it on:

```csharp
public partial class ExportViewModel : ObservableObject
{
    private readonly IToastService _toasts;

    public ExportViewModel(IToastService toasts) => _toasts = toasts;

    [RelayCommand]
    private async Task ExportAsync()
    {
        var id = _toasts.Show(new ToastOptions
        {
            Kind = ToastKind.Loading,
            Title = "Exporting invoices",
            Duration = Timeout.InfiniteTimeSpan
        });

        var result = await _exports.CreateCsvAsync(Range);
        _toasts.Update(id, result.IsSuccess
            ? new ToastUpdate { Kind = ToastKind.Success, Title = "Export ready", Description = result.FileName }
            : new ToastUpdate { Kind = ToastKind.Error, Title = "Export failed", Description = result.Error });
    }
}
```

```csharp
// In the view's code behind, once the view is attached to its window:
var toasts = ToastHost.GetService(this);
DataContext = new ExportViewModel(toasts);
```

The view model and the export service are the application's own. The stack does not need a `ToastStack` in the view:
`ToastHost` creates one on the window's overlay layer on the first call. Place a `ToastStack` yourself when the
position, limits or `IsContained` must differ from the defaults.

### Example: invoice export in a billing console

A billing console lets a finance operator export a month of invoices as a CSV. The export takes a few seconds and
the operator should keep working, so the request starts with a loading toast that has no timer. When the file is
ready, the toast becomes "Export ready" with a Download button; when it fails, the toast becomes an error with the
reason and a Retry button. Errors stay longer than the default, and only the stack's list rectangle takes pointer
input, so the invoice table stays usable around it.

The stack is placed over the page content with a limit of eight toasts, so a long session of exports cannot grow the
stack without bound:

```xml
<Grid>
    <!-- Invoice table and filters go here. -->
    <arc:ToastStack Label="Billing notifications"
                    Position="BottomRight"
                    VisibleToasts="3"
                    Limit="8" />
</Grid>
```

```csharp
var id = _toasts.Show(new ToastOptions
{
    Kind = ToastKind.Loading,
    Title = "Exporting invoices for March",
    Duration = Timeout.InfiniteTimeSpan
});

var result = await _exports.CreateCsvAsync(March2026);

_toasts.Update(id, result.IsSuccess
    ? new ToastUpdate
    {
        Kind = ToastKind.Success,
        Title = "Export ready",
        Description = "412 invoices, 1.8 MB.",
        Action = new ToastAction("Download", () => _files.Open(result.FileName))
    }
    : new ToastUpdate
    {
        Kind = ToastKind.Error,
        Title = "Export failed",
        Description = result.Error,
        Action = new ToastAction("Retry", () => _ = ExportAgainAsync())
    });
```

### Content guidelines

- **Title**: a short result in past tense or a state for loading: "Export ready", "Invite sent", "Uploading report.pdf".
  The kind already says whether it succeeded, so do not repeat it in the title.
- **Description**: one sentence with the number, name or next step: "412 invoices, 1.8 MB.", "Check the address and try
  again." Write numbers as digits.
- **Action label**: a verb, one or two words: "Undo", "Retry", "Download", "View". Never "OK" or "Close", because the
  close button already exists.
- **Errors**: say what failed and what the person can do: the reason and the way forward. Do not blame the person.
- **Sentence case, no exclamation marks**: a notification reports a fact.

| Instead of | Write |
|---|---|
| Success!!! File uploaded | Report uploaded |
| Error | Export failed |
| Something went wrong. Please try again later. | Couldn't send the invite. Check the address and try again. |
| Click OK to continue | Project restored |

### Best practices

**Do**

- Use one toast per job and update it, so a loading state becomes its result instead of leaving two messages.
- Give every loading toast a path to an end: an update on success, an update on failure, or a timeout that the code
  sets deliberately. A loading toast with no update stays on screen.
- Keep `Limit` at a number the operator can scan, such as eight, so old results do not pile up behind new ones.
- Use `Warning` and `Error` for things the person must act on, so their longer lifetime matches the work they need
  to do.

**Avoid**

- Using the stack for messages that must stay until they are resolved, such as a plan limit. Stacked toasts close on
  their own; use `Alert` for those.
- Long descriptions. A toast is read at a glance, and the card is at most 360 px wide.
- Actions that do the main task silently, such as "Delete all" with no undo. Give destructive work a `Dialog` before
  it runs, and an Undo afterwards.
- Showing a toast for every keystroke or every poll tick. Each `Show` and `Update` is announced to assistive
  technology, so frequent messages become noise. Batch them into one result.

### Common questions

**Why did a toast disappear before I read it?** The timer runs while the stack is collapsed and stops only while the
stack is expanded, dragged or the window is minimized. Set a longer `Duration` for the toast, or hover over the stack
to pause the timers while you read.

**How do I keep a toast until the person closes it?** Set `Duration` to `Timeout.InfiniteTimeSpan` in `ToastOptions`
or `ToastUpdate`. Loading toasts already work this way until they are updated.

**What happens when a window has no `ToastStack`?** The first `Toast.Show` or `ToastHost.GetService` call creates one
on the window's overlay layer. If the window has no overlay layer, `ToastHost` throws an `InvalidOperationException`,
so place a `ToastStack` in the window in that case. A call from a control that is not attached to a window throws too.

**How do I stop the stack from taking the Alt+T shortcut?** Set `IsHotkeyEnabled` to `False`. Alt+T can clash with a
menu access key on Windows, so an application with its own menu may turn it off. You can still move focus into the
stack yourself with `FocusInto()`, for example from a "Notifications" menu item.

**Can I count or read the queued toasts?** Read `Count` for the number of queued toasts, shown or waiting, and
`IsExpanded` to tell whether the list is fanned out. Subscribe to `ToastsChanged` to keep a badge in step with the
stack.

## API reference

<!-- api: ToastStack -->

<!-- api: ToastOptions -->

Companion types, from the source:

`ToastUpdate` changes a shown toast. Null members stay as they are.

| Member | Meaning |
|---|---|
| `Kind` | New kind; the icon morphs |
| `Title` | New message |
| `Description`, `RemoveDescription` | New second line, or remove it |
| `Action`, `RemoveAction` | A new action, or fold the action button away |
| `Duration` | New lifetime; null restarts with the default for the kind |

`ToastKind` is the type of a toast: `Success`, `Info`, `Warning`, `Error` and `Loading`. `ToastStackPosition` is the
edge of the stack: `BottomRight` (default), `BottomCenter` and `BottomLeft`.

`ToastHost` finds the stack of a window and is the entry point for the app-wide API:

| Member | Meaning |
|---|---|
| `GetService(owner)` | The window's `IToastService`; creates a stack on the overlay layer when there is none |
| `GetStack(owner)` | The window's `ToastStack`, with the same creation rule |
| `Find(topLevel)` | The registered stack of a window, or null; a placed stack wins over an auto-created one |

## Accessibility

- The notification area is a region landmark named by `Label`. With `IsHotkeyEnabled` on, the name ends with
  "(Alt+T)". Alt+T moves focus into the stack and expands it.
- Tab moves between the action and close buttons of the visible toasts. Escape closes the focused toast.
- New and updated toasts are announced as "Kind: Title. Description". Errors are announced assertively; other kinds
  are announced politely.
- Each card is named "Kind: Title", so a screen reader can tell the types apart, and its description is its help text.
  The close button is named "Dismiss notification".
- Timers pause while the stack is expanded, while a toast is dragged and while the window is minimized. They keep
  running when the stack is covered by another window.
- Toasts outside `VisibleToasts` and toasts that are leaving are inert: they cannot be reached by pointer or by Tab.
- Keyboard focus is handed on when the focused toast closes. It goes to the next toast's first button; with none left,
  it returns to the element that had focus before the stack was entered.

### Keyboard

| Keys | Action |
|---|---|
| `Alt+T` | Move focus to the front toast (its action when it has one, otherwise its close button) and expand the stack. Set `IsHotkeyEnabled` to `False` to turn it off |
| `Tab` / `Shift+Tab` | Move between the action and close buttons of the visible toasts |
| `Enter` or `Space` | Press the focused button: run the action, or close the toast |
| `Escape` | Close the focused toast, with no action run |

Keyboard focus expands the stack, and leaving the stack folds it back. A mouse click on a button does not keep the stack
expanded.

## Motion

- A new toast rises from below its own height and fades in. Older toasts tuck back, shrink and lose their content
  tone. Hovering or focusing the stack fans the toasts out into a list, and leaving it folds them back.
- Swiping a toast to the right follows the pointer. A long or fast swipe dismisses it, and the card fades as it moves
  away.
- Updates morph the icon, the copy and the card height in place, and the action button opens or closes its width. A
  loading ring turns while the work runs.
- Reduced motion jumps the positions and fades toasts in and out quickly. Off motion shows every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A new toast arrives | Rises from below with a standard enter fade | Appears in place with a fast fade | Immediate |
| Older toasts tuck back | Step back 14 px and shrink 5% per step on the morph spring; content fades | Positions jump; a fast fade | Immediate |
| The stack expands or folds | Fans out into a list on the morph spring; other changes use the smooth spring | Positions jump | Immediate |
| A toast is dismissed | Sinks 10 px and shrinks to 96% while it fades out | Fast fade | Immediate |
| A toast is swiped away | Follows the pointer; fades to 25% over 280 px; a fast release fades out in 0.22 seconds | The swipe still dismisses; the fade is quick and nothing is thrown | Immediate |
| A toast is updated | Icon, copy and card height morph; the action slot opens or closes; the loading ring turns | The loading ring stops; the new copy and height apply without a spring | Immediate |

## Related

- `Toast` for one local confirmation that the page controls with `IsOpen`.
- `Alert` for a persistent message inside the page.
- `AnnouncementBar` for a rotating banner with a countdown.
- `Dialog` for a decision that must be made before the page continues.

| Component | Choose it when |
|---|---|
| `Toast` | The page owns one confirmation and sets its text and open state |
| `Alert` | The message stays on the page until the person or the data resolves it |
| `AnnouncementBar` | A site-wide notice rotates through several messages |
| `Dialog` | The person must answer before the page continues |
| `ToastStack` + `Toast.Show` | Results from background work can arrive from any control and may overlap |
