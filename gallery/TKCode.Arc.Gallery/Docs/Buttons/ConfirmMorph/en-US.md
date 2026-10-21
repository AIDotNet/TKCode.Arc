---
title: Confirm morph
summary: A destructive button that morphs into an inline confirmation, a spinner, and a result with undo.
---

<Demo name="Press" />

## When to use

- A destructive action on one object, such as deleting the selected files or removing a member.
- An action whose consequence is clear from one question, such as "Delete 3 files?".
- An action that can be undone for a short time after it runs, offered as Undo on the result.

## When not to use

- A consequence that needs several lines of explanation or a typed name: use `Dialog`.
- A safe asynchronous submit with no question: use `ActionButton`.
- An action that must never be triggered by accident: use `HoldToConfirm` from Gestures.
- A command list with no single default action: use `DropdownMenu` from Menus.

## Usage

Set `Label` to the resting text. The question is `Prompt`, or the label with a question mark when `Prompt` is not set.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ConfirmMorph Label="Delete project" Prompt="Delete this project?" />
</UserControl>
```

### Actions and undo

`ConfirmAction` runs when the question is answered. The pill shows pending while the task runs. A task that throws
shows the error face with Retry. Set `UndoAction` to offer Undo on the result.

<Demo name="ErrorAndRetry" />

```csharp
delete.ConfirmAction = () => documentService.DeleteAsync(selected);
delete.UndoAction = () => documentService.RestoreAsync(selected);
```

### Confirm modes

`ConfirmMode` sets how the question is answered. `Press` runs on one press. `Hold` requires Confirm to be held for
`HoldDuration`, which is one second by default. `Countdown` starts a `CountdownSeconds` countdown that Cancel or
Escape can still stop.

<Demo name="Hold" />

<Demo name="Countdown" />

```xml
<arc:ConfirmMorph Label="Remove member"
                  ConfirmLabel="Remove"
                  ConfirmMode="Hold"
                  HoldDuration="00:00:01" />

<arc:ConfirmMorph Label="Discard draft"
                  ConfirmLabel="Discard"
                  ConfirmMode="Countdown"
                  CountdownSeconds="3" />
```

### Labels and tone

The eight text properties (`ConfirmLabel`, `CancelLabel`, `PendingLabel`, `DoneLabel`, `ErrorLabel`, `RetryLabel`,
`UndoLabel` and `UndoingLabel`) set the words of each face. `Tone="Neutral"` colors the confirm answer with the accent
for an important action that can be reversed. The default `Danger` suits destructive actions.

<Demo name="NeutralTone" />

### Controlled state and timeouts

`State` is two-way. Setting it from code moves the control without running an action. `StateChanged` reports each
move, and `Cancelled` is raised when the question is dismissed. `ConfirmTimeout` returns an unanswered question to rest,
and `ResultTimeout` removes the result. Set `IsOutsidePressCancelEnabled` to `False` to keep the question open on an
outside press.

```csharp
confirm.StateChanged += (_, e) => log.Text = $"State: {e.NewState}";
confirm.Cancelled += (_, _) => log.Text = "Cancelled";
confirm.State = ConfirmMorphState.Idle;
```

### Disabled

The resting pill drops its shadow when `IsEnabled` is `False`.

<Demo name="Disabled" />

## API reference

<!-- api: ConfirmMorph -->

## Accessibility

- The question is a group named by its prompt. The resting button keeps the name of its label.
- The focus follows the morph: Confirm when the question opens, the root while work runs, Undo or Retry on the result,
  and the trigger button at rest.
- Presses in the first 280 milliseconds after the question opens are ignored, so a double click or a held Enter does not
  answer it by accident.
- In `Hold` mode the help text is "Press and hold". Letting go early announces that a longer hold is needed.
- The prompt, the pending and the result words, and "Undo is available" are announced politely.
- Enter and Space press the button on the current face. Escape cancels the question or countdown.

## Motion

- Only the width of the pill changes. It grows on a spring and shrinks more smoothly, and the text does not scale.
- Each new face's parts clear a small blur in reading order, and the old face fades out in place.
- The result check draws itself. The countdown digits roll, and the pending spinner turns.
- In `Hold` mode a filled copy of the danger color reveals from the left as Confirm is held. Letting go early springs it back.
- Reduced motion removes the width spring and the blur, crossfades the faces and slows the spinner. The hold fill and the
  countdown ring still show progress. Off switches the faces at once.

## Related

- `Dialog` for a consequence that needs more explanation or a typed name.
- `ActionButton` for an asynchronous submit with no question.
- `HoldToConfirm` for an action that must never fire on a tap.
- `DropdownMenu` for a list of commands without one default action.
