---
title: Action button
summary: A compact button for frequent toolbar actions.
---

<Demo name="Default" />

## When to use

- A single asynchronous submit, such as Save, Publish or Send invite, where the result belongs on the button.
- Actions where the label changes to a pending and a success word, and returns to idle by itself.
- A call to action at the end of a form or toolbar that needs a clear result without a toast.

## When not to use

- An ordinary form submit with a spinner and no result label: use `Button` with `IsLoading`.
- A destructive action that needs a deliberate press: use `HoldToConfirm` from Gestures.
- Several alternatives for the same action: use `SplitButton`.
- A dense table with many rows: keep the result in the row, not on a button per row.

## Usage

`ActionButton` derives from `Button`. Its work runs through `AsyncAction`, which is set in code.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ActionButton Label="Save" PendingLabel="Saving" SuccessLabel="Saved" />
</UserControl>
```

```csharp
var save = new ActionButton
{
    Label = "Save",
    PendingLabel = "Saving",
    SuccessLabel = "Saved",
    AsyncAction = () => documentService.SaveAsync()
};
```

### Labels and reset

`Label` is the idle text and the accessible name. `PendingLabel` shows while `AsyncAction` runs, and `SuccessLabel`
shows after it completes. `ResetAfter` sets how long the success state stays; the default is 2.4 seconds.
A zero value keeps the success state until `Reset()` is called.

<Demo name="Labels" />

<Demo name="KeepResult" />

```xml
<arc:ActionButton Label="Publish post"
                  PendingLabel="Publishing"
                  SuccessLabel="Published"
                  ResetAfter="00:00:03" />
```

### Failure

When `AsyncAction` throws, the button returns to idle and raises `ActionFailed`. The event argument carries the
`Exception`.

<Demo name="Failure" />

### Commands and clicks

`Command` and `Click` run before `AsyncAction`. Without an async action, they count as an immediate success.

### Disabled

Set `IsEnabled` to `False` to dim the button. While `AsyncAction` is pending, the button keeps keyboard focus and
ignores repeated activation.

<Demo name="Disabled" />

## API reference

<!-- api: ActionButton -->

## Accessibility

- The accessible name is always `Label`, so the letters of the animated label are not read one by one.
- While pending, the button reports itself as unavailable but keeps keyboard focus.
- A hidden polite status region announces the pending and the success labels.
- Enter and Space run the action. Repeated activation is ignored while pending.

## Motion

- Changed letters rise out of a soft blur and kept letters glide to their new positions. The width follows on a spring.
- The arrow leaves in the direction of the action and returns from the left. A spinner turns while the action runs,
  and a check draws itself on success.
- Reduced motion removes the press scale, the width spring and the drawn check. Label changes become an instant
  crossfade. Off replaces the label at once.

## Related

- `Button` for a plain action, with `IsLoading` for a spinner and no result label.
- `CopyButton` for copying a value, with the same kind of inline confirmation.
- `ConfirmMorph` for a destructive action that asks for confirmation in place.
- `HoldToConfirm` for a destructive action that needs a deliberate press.
