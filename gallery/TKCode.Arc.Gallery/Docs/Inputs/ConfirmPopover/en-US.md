---
title: Confirm popover
summary: A small question next to the button that asked it, for actions such as delete; focus starts on Cancel, Escape backs out, and the work can run before it closes.
---

<Demo name="Preview" />

## When to use

`ConfirmPopover` asks one short question right where the person acted: "Delete these notes?" opens next to the bin
they pressed, with Cancel and the action. It keeps the page in view, needs a deliberate second press, and starts with
focus on Cancel, so a stray Enter does no harm. Escape or a press outside backs out, and focus returns to the button
that opened it. When the action takes a moment, the confirm button can run the work and show a spinner until it ends.

- **Deleting one item in a list or a table row**: a comment, a file, a saved view, an API key.
- **Leaving with unsaved work**: discarding a draft from its own Discard button.
- **A one-way switch**: revoking access, ending a session, publishing to readers.

## When not to use

- **The action can be undone**: do it at once and offer an `UndoToast`; a question slows down every person to protect
  the few who slip.
- **The decision needs more than a sentence**: a list of what will be lost, typing a name to confirm, or choices
  beyond yes and no belong in a `Dialog`.
- **Many items at once**: bulk deletion is easy to misjudge; use a `Dialog` that states the count.

## Usage

Put the button that asks in `Trigger`, write the question in `Title` and handle `Confirmed`.

```xml
<arc:ConfirmPopover Title="Delete this comment?" ConfirmText="Delete">
    <arc:ConfirmPopover.Trigger>
        <arc:Button Variant="Ghost" Content="Delete" />
    </arc:ConfirmPopover.Trigger>
</arc:ConfirmPopover>
```

`ConfirmPopover` lives in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`.

The question opens in a `Popover`, so it takes the same placement (`Placement`, `Alignment`), flips when it does not
fit, and moves the same way. `Confirmed` is raised when the action is confirmed and `Cancelled` when the question
closes in any other way, with the `Reason` (the cancel button, Escape or a press outside). `Command` runs after
`Confirmed`.

### Attached to a button

`arc:ConfirmPopover.Attach` adds a question to a button that is already in place. A click on the button opens it.

<Demo name="Attached" />

For an action that does not destroy anything, set `IsDestructive` to false: the confirm button and the mark use the
accent instead of the danger color. `InitialFocus="Confirm"` starts on the action when backing out is the rare case.
`Icon` replaces the warning triangle, or hides the mark when it is null.

### Running the work

`ConfirmHandler` runs when the action is confirmed. The confirm button shows a spinner and Cancel is disabled until
it finishes; true closes the question as confirmed, false or an exception keeps it open so the person can try again.

<Demo name="Async" />

```csharp
confirm.ConfirmHandler = async () =>
{
    var response = await api.RevokeKeyAsync(keyId);
    return response.IsSuccessStatusCode;
};
```

### From code

`ConfirmAsync` opens the question and answers when it closes, so a command can ask first and act after.

```csharp
if (await confirm.ConfirmAsync(rowButton))
{
    await DeleteAsync(row);
}
```

### Anatomy

1. **Trigger** (`Trigger`): the button that asks; it stays in place and answers presses with color only.
2. **Mark** (`Icon`): a 32px circle with a 16px glyph, in the danger or accent color.
3. **Question** (`Title`) and **detail** (`Message`): medium weight, then secondary text; both wrap within 272px.
4. **Buttons**: Cancel (`CancelText`) and the action (`ConfirmText`), small, right aligned, 8px apart.

### Content guidelines

Ask about the specific thing and name the action on its button.

| Instead of | Write |
|---|---|
| Are you sure? | Delete these notes? |
| OK | Delete |
| This action cannot be undone. Are you really sure you want to continue? | They are removed for everyone. |

## API reference

<!-- api: ConfirmPopover -->

<!-- api: ConfirmPopoverCancelledEventArgs -->

| Type | Values |
|---|---|
| `ConfirmPopoverFocus` | `Cancel` (default), `Confirm` |

## Accessibility

- The question opens with focus on Cancel (or the action with `InitialFocus="Confirm"`); after a key press the focus
  ring shows, after a click it does not.
- Tab and Shift+Tab cycle between the two buttons inside the question; Left and Right move between them too.
- Escape closes the question and returns focus to the trigger with its focus ring; a press outside closes it and
  returns focus without the ring.
- The popover surface is named after the question, and the action's help text repeats it, so "Delete" is read with
  "Delete these notes?".
- The trigger reports whether the question is open (expanded or collapsed).

### Keyboard

| Key | Action |
|---|---|
| `Enter` / `Space` on the trigger | Opens the question |
| `Tab` / `Shift` + `Tab` | Moves between Cancel and the action |
| `Left` / `Right` | Moves between Cancel and the action |
| `Enter` / `Space` | Presses the focused button |
| `Escape` | Cancels and returns to the trigger |

## Motion

- The question opens and closes with the `Popover` motion: it starts 5px toward its trigger at 97% and settles on a
  spring, and leaves in about 160ms.
- The confirm button's label crossfades into a spinner while `ConfirmHandler` runs.
- Under reduced motion the question only fades; with motion off it appears and leaves at once.

## Responsive behavior

- The question is 272px wide inside the popover surface and wraps longer text.
- Near an edge it flips to the other side of the trigger and stays inside the window.

## Related

- `Dialog` for decisions that need more room or block the page.
- `UndoToast` for actions that are better done at once and taken back.
- `Popover` for other small interactive panels.

| Component | Choose it when |
|---|---|
| `ConfirmPopover` | One destructive action needs a second, deliberate press |
| `UndoToast` | The action can be reverted for a few seconds |
| `Dialog` | The decision needs details, input or more than two choices |
