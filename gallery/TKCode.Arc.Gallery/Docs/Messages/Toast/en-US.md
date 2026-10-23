---
title: Toast
summary: Brief confirmation for a completed background action.
---

<Demo name="LocalToast" />

## When to use

`Toast` is a single, page-owned confirmation: a short message that rises from its edge, stays for a few seconds and
goes away. It is the right tool when the page itself knows that something finished, for example after a form saved,
and the person is still looking at the same screen. The page places the toast, sets its text and decides when it is
open, so the message always matches the view it appears on.

- **Save confirmations in settings and profile pages**: "Changes saved" under a billing address form, after the
  request succeeds. The person stays on the page, so a short, self-closing message is enough.
- **Copy and export feedback**: "API key copied" beside a key in a developer console, or "Invoice link copied" on an
  invoice detail page. The toast confirms the action without taking focus from the field the person is working in.
- **Background sync results on one screen**: "Synced to all your devices" after a draft saves in a CRM note editor.
  The page sets `IsOpen` when the save completes and nothing else needs to know about it.
- **Bulk actions on a list page**: a "3 contacts archived" message under a table that the person can swipe away when
  it covers the rows they need, with no queue of earlier messages to clear.

## When not to use

- **Several messages that can be open at once, or results from anywhere in the app**: use `ToastStack`, which queues,
  types, updates and stacks notifications and can be called from any control through `Toast.Show`.
- **A message that stays until someone acts on it**, such as a payment failure or a quota warning: use `Alert` inside
  the page, where it remains until it is resolved.
- **A message that must be answered before the person continues**: use `Dialog`, which takes focus and blocks the page
  until the person chooses an answer.
- **A rotating banner across the top of a site**, for example a maintenance notice with a countdown: use
  `AnnouncementBar`.

## Usage

A `Toast` shows its `Title` and an optional `Description`. It is visible while `IsOpen` is `True`, and it closes
after `Duration` (4.5 seconds by default).

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Toast Title="Changes saved"
               Description="Synced to all your devices."
               VerticalAlignment="Bottom" />
</UserControl>
```

`Toast` derives from Avalonia's `TemplatedControl`, so its look lives in a `ControlTheme` and can be restyled with
the usual Avalonia mechanisms. It adds the message and motion behavior: the entrance and exit, the timer, the swipe
gesture and the `Closed` event. It does not queue. Each `Toast` in the page is independent, so place it where its
message belongs and use `ToastStack` when several messages can be open together.

### Anatomy

1. **Surface** (`PART_Root` and `PART_Surface`): the card. Its width is the available width up to `MaxToastWidth`
   (416 px, 26rem), and a narrower container shrinks it. It has a 1 px border in the border color, the panel corner
   radius, the floating shadow and a surface color that is 6% mixed toward the page background. Padding is 16 px on
   the left and 14 px on the other three sides.
2. **Icon disc** (`PART_Icon`): a 30 px circle with a 12 px gap after it. Its fill is the success color at 10% over
   the surface, with a 1 px ring in the success color at 24% over the border. The disc is the same for every toast.
3. **Check mark** (`PART_Check`): a 16 px check in the success color, stroked at 2.25. It draws itself after the
   entrance begins. The check is a decoration and is not exposed to assistive technology.
4. **Title** (`PART_Title`): `Title`, 14 px, medium weight, on one line. A long title ends with an ellipsis. Its line
   box is 19.6 px and its top padding centers the first line on the 30 px disc.
5. **Description** (`PART_Description`): `Description`, 14 px in the secondary text color, wrapped. It sits 2 px below
   the title and adds no gap when it is empty.
6. **Close button** (`PART_Close`): a 32 px round ghost button with a 2 px X glyph. It is muted at rest and takes the
   foreground color on hover. Its accessible name is "Dismiss notification".

### Showing and hiding

Set `IsOpen` to `True` to show the toast again after it closed. Closing, swiping and timing out set `IsOpen` to
`False`, so the property always reflects what is on screen. Changing `Title` or `Description` while the toast is open
rises the new text in and the height follows; the timer keeps running. Setting `IsOpen` to `True` on a toast that is
already open has no effect.

```csharp
toast.Title = "Draft published";
toast.Description = "Readers will see it in a few seconds.";
toast.IsOpen = true;
```

Keep one toast per message location. When the same message needs to appear again, update its text and open it,
rather than creating a new `Toast` each time.

### Duration

`Duration` sets the time before the toast closes itself. The default is 4.5 seconds. A value of zero or less keeps the
toast open until the person closes it or the page sets `IsOpen` to `False`. The timer starts when the toast opens and
restarts when `Duration` changes while the toast is open. It does not pause on hover, so the copy has to be read in
the time given.

```xml
<arc:Toast Title="Upload complete" Duration="0:0:8" />
<arc:Toast Title="Still syncing" Duration="0" />
```

Choose the duration from the length of the copy. A one-line confirmation suits the default. A message with a
description needs more time to read, so give it a longer `Duration`, such as 8 seconds. Use zero only when the page
closes the toast itself, and make sure the page always sets `IsOpen` to `False` when the work ends.

### Closed event

`Closed` is raised when the toast starts closing for one of three reasons. `ToastCloseReason` tells them apart:
`Dismissed` for the close button or `Close()`, `Swiped` for a swipe past the distance or speed threshold, and
`TimedOut` when `Duration` elapsed.

```csharp
toast.Closed += (_, e) =>
{
    if (e.Reason == ToastCloseReason.TimedOut)
    {
        log.Text = "The message closed on its own.";
    }
};
```

`Closed` is raised only for closes that the toast starts. Setting `IsOpen` to `False` from code closes the toast
without raising it, because the page already knows the state it set. Use `Close()` when the page wants the same
result as the close button, including the event.

### App-wide toasts

`Toast.Show` sends a toast from any control in the window to the window's `ToastStack`, which is the same service
`ToastStack` offers. Use it when the message comes from code that does not own a `Toast` control, such as a shared
export command, a clipboard helper or a background job. `ToastKind` selects the icon and color: `Success`, `Info`,
`Warning`, `Error` or `Loading`. `Toast.Show` returns an id, and `Toast.Update` and `Toast.Dismiss` accept that id.

<Demo name="FromAnywhere" />

```csharp
var id = Toast.Show(this, "Export started", ToastKind.Loading, "Acme Analytics is building the CSV.");

// Later, when the export finishes:
Toast.Update(this, id, new ToastUpdate
{
    Kind = ToastKind.Success,
    Title = "Export ready",
    Description = "Download it from the Exports page."
});
```

The `owner` argument is any `Visual` attached to a window. If the window has no `ToastStack`, the first call creates
one on the window's overlay layer. An owner that is not attached yet raises an `InvalidOperationException`, so call
these methods after the view is loaded. The app-wide API is covered on the `ToastStack` page.

### Binding to a view model

`IsOpen` is two-way by default, so a binding needs no `Mode`. The title and description are one-way: the view model
owns the text and the toast displays it. The view model below is written in CommunityToolkit.Mvvm style.

```csharp
public partial class ProfileViewModel : ObservableObject
{
    private readonly IProfileService _profiles;

    public ProfileViewModel(IProfileService profiles) => _profiles = profiles;

    [ObservableProperty]
    private bool _isSavedToastOpen;

    [ObservableProperty]
    private string _savedToastTitle = "Changes saved";

    [ObservableProperty]
    private string? _savedToastDescription;

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _profiles.SaveAsync(Draft);
        SavedToastTitle = "Changes saved";
        SavedToastDescription = "Synced to all your devices.";
        IsSavedToastOpen = true;
    }
}
```

```xml
<Grid RowDefinitions="*,Auto">
    <!-- Form content goes here. -->
    <arc:Toast Grid.Row="1"
               Title="{Binding SavedToastTitle}"
               Description="{Binding SavedToastDescription}"
               IsOpen="{Binding IsSavedToastOpen}"
               HorizontalAlignment="Right"
               VerticalAlignment="Bottom"
               Margin="0,0,0,8" />
</Grid>
```

`IsOpen` is the only two-way property, so the view model sees every close, whatever its reason. Subscribe to `Closed`
in code behind when the view must react to the reason. For each save, set the title and description first and then
`IsSavedToastOpen` to `True`: a closed toast opens with the new text, and an open one updates its text in place.

### Example: profile save in a CRM

A contact record in a CRM has a save button in its footer. When the save succeeds, a confirmation rises at the
bottom-left of the form, names what happened and says where it went. The toast does not block the next edit and
closes on its own after the default 4.5 seconds, so the person can keep working. A failed save uses `Alert` instead,
because a failure must stay visible until the person fixes the problem.

```xml
<Grid Width="640" Height="220">
    <Border Background="{arc:ArcDesignTokenResource SurfaceRaised}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}" />
    <arc:Toast Title="Changes saved"
               Description="Northwind Logistics is updated in all your views."
               HorizontalAlignment="Left"
               VerticalAlignment="Bottom"
               Margin="24" />
</Grid>
```

### Content guidelines

- **Title**: one short past-tense statement of what finished: "Changes saved", "Invoice sent". It stays on one line,
  so keep it under about 30 characters in English.
- **Description**: one sentence that says where the result is or what happens next: "Synced to all your devices.",
  "Readers will see it in a few seconds." Leave it out when the title says everything.
- **No action words**: a toast has no button but its close button, so do not ask the person to do something in the
  copy. If a next step matters, put it in the page or use `Alert`.
- **Sentence case, no exclamation marks**: a confirmation states a fact; it does not celebrate.

| Instead of | Write |
|---|---|
| Success! Your changes have been saved successfully!!! | Changes saved |
| Saved. Click here to view. | Changes saved. Find it under Activity. |
| Error saving the form | Couldn't save. Try again. |
| The item was successfully deleted from your account | Contact deleted |

### Best practices

**Do**

- Use `Toast` for one confirmation the page controls, and keep the text in the view model so the state of the page
  and the message agree.
- Keep the title short enough to fit one line. A long title ends with an ellipsis, which hides the result.
- Place the toast where the person is looking, usually the bottom of the form or the page, so the confirmation sits
  near the action that caused it.
- Bind `IsOpen` to the view model, so the open state lives with the data the page already holds rather than only in
  the view.

**Avoid**

- Messages that must be read, such as a failed payment or a permission problem. The timer does not pause, so these
  belong in `Alert` or `Dialog`, which stay until resolved.
- Using `Toast` for several notifications in a row. Each `Toast` is independent and does not queue, so every message
  needs its own place on the page and none of them knows about the others; use `ToastStack` instead.
- A zero duration without a page-side close. A toast that never times out and is never closed by the page stays on
  screen for the rest of the session.
- Motion as the only signal. The text carries the result; the rise and the check mark only add emphasis.

### Common questions

**Why did my toast close before the person could read it?** The default duration is 4.5 seconds and the timer does
not pause while the pointer is over the toast. Set a longer `Duration` for long copy, or use `Alert` when the message
must stay until it is resolved.

**How do I know why the toast closed?** Subscribe to `Closed` and read `e.Reason`. `Dismissed` means the close button
or `Close()`, `Swiped` means a swipe, and `TimedOut` means the duration elapsed. Setting `IsOpen` to `False` from code
does not raise the event.

**How do I show the same message again?** Set `IsOpen` to `True` after the toast has closed. Set the new `Title` or
`Description` first, so the toast opens with the new text. An open toast keeps its current timer when its text
changes, and setting `IsOpen` to `True` on an open toast does nothing.

**Can a view model show a toast without a reference to the view?** Yes. Pass a `Visual` of the window, such as the
view itself, to `Toast.Show` from the code that receives it, or use `ToastHost.GetService` to get the window's
`IToastService` once and hand that to the view model.

**Does the toast take focus?** No. A toast is announced politely and does not move focus. Keyboard focus stays where
the person left it, which is usually the button that started the work.

## API reference

<!-- api: Toast -->

Companion type. `ToastCloseReason` is the reason in `ToastClosedEventArgs.Reason`:

| Member | Meaning |
|---|---|
| `Dismissed` | The close button or `Close()` closed the toast |
| `Swiped` | A swipe passed the distance (80 px) or speed (480 px/s) threshold |
| `TimedOut` | `Duration` elapsed |

## Accessibility

- `Title` is the accessible name of the toast and `Description` is its help text, so a screen reader reads both when
  the toast appears.
- The toast is a polite live region: it is announced after the current speech and does not interrupt it.
- The close button is named "Dismiss notification". Tab reaches it and Enter or Space closes the toast.
- The check mark is decoration and is not exposed to assistive technology.
- The toast does not take focus, so it never moves the person away from the field or button they were using.
- The timer does not pause on hover or focus. Keep the copy short, and use `Alert` for a message the person may need
  more time to read.
- A swipe is a pointer and touch gesture. The close button gives the same result from the keyboard.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to or from the close button; the toast has no other focusable control |
| `Enter` or `Space` | Press the close button: the toast closes with the reason `Dismissed` |

A pointer drag is the only way to swipe. A press that starts on the close button does not start a drag.

## Motion

- The toast rises 16 px from below at 96% scale on a spring, and its check mark draws itself 0.12 seconds later.
- Closing sinks the toast 8 px, shrinks it to 97% and fades it out. A closed toast takes no space.
- Changed text rises in and the height springs to fit the new copy.
- A swipe follows the pointer and, when released past the threshold, carries on with the release velocity while it
  fades. A short swipe springs back.
- Reduced motion fades the toast in and out and turns swiping off. Off motion shows and hides it at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The toast opens | Rises 16 px at 96% scale on the morph spring and fades in; the check draws after 0.12 seconds | Fades in at its final position, with the check already drawn | Shown at once |
| The toast closes (button, timer or `Close()`) | Sinks 8 px, shrinks to 97% and fades out over the exit duration | Fades out with the instant preset | Removed at once |
| Swipe away | Follows the pointer, then is thrown with the release velocity and fades out | Not available: swiping is off | Not available: swiping is off |
| Title or description changes | The old text lifts away, the new text rises in and the height springs to fit | Instant fade | Immediate |

## Related

- `ToastStack` for queued, typed and updatable notifications that can be raised from anywhere in the app.
- `Alert` for a message that stays on the page until it is resolved.
- `Dialog` for a message that must be answered before the person continues.
- `AnnouncementBar` for a rotating banner with a countdown.

| Component | Choose it when |
|---|---|
| `ToastStack` | Several results can be open at once, or the message comes from code outside the page |
| `Alert` | The message stays on the page until the person or the data resolves it |
| `Dialog` | The person must choose an answer before the page continues |
| `AnnouncementBar` | A site-wide notice rotates through messages, or needs a countdown |
| `Toast` + `IsOpen` | The page owns one short confirmation and its text, and nothing else needs to queue |
