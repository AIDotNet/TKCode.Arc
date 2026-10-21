---
title: Confirm morph
summary: A destructive button that morphs into an inline confirmation, a spinner, and a result with undo.
---

<Demo name="Press" />

## When to use

`ConfirmMorph` is a button for a destructive or important action that asks first, in place. Pressing it turns the same
pill into a one-line question, then into a spinner while the work runs, then into a result with an optional Undo. It
fits an action that one question explains, so the person answers without leaving the table, card or editor.

- **Deleting the selected items in a list**: a file browser or an order list where the toolbar asks "Delete 3 files?".
  The question names the count, so the person sees exactly what will go without a modal interrupting the selection.
- **Removing a member or revoking access** on a team page: the row action "Remove member" answers in one press, and the
  result ("Removed") confirms the change in the same spot without a toast the person might miss.
- **Discarding an unsaved draft** in an editor: a short countdown gives the person a few seconds to change their mind
  before the discard runs, which suits an action that cannot be recovered.
- **Reversible actions with a grace period**: archiving a project or closing a support ticket, where the result offers
  Undo for a few seconds after the server accepts the change.
- **Calls to a server that can fail**: the pending face shows that work is running, and a failed call turns into an error
  face with Retry, so the person does not have to find the control again.

## When not to use

- **A consequence that needs several lines of explanation or a typed name**, such as deleting an account or a production
  database: use `Dialog`, which can hold the detail, a list of what will be lost and a text field.
- **A safe asynchronous submit with no question**: use `ActionButton`, which shows its pending and success states without
  asking first.
- **An action that must never fire on a tap**, such as a factory reset in a device console: use `HoldToConfirm` from
  Gestures, a dedicated hold control for that job.
- **A list of commands with no single default action**, such as Duplicate, Move, Export and Delete for one row: use
  `DropdownMenu` from Menus.
- **A decision that must block the rest of the page**, such as unsaved changes before navigation: use `Dialog`. The
  morph does not block input elsewhere; an outside press answers the question no.

## Usage

Set `Label` to the resting text. The question is `Prompt`, or the label with a question mark when `Prompt` is not set.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ConfirmMorph Label="Delete project" Prompt="Delete this project?" />
</UserControl>
```

`ConfirmMorph` derives from Avalonia's `TemplatedControl`, not from `Button`. Its template declares every face (the
resting pill, the question, the countdown, the spinner, the result and the error) and the control switches between them
as its `State` changes. The resting pill and the answer buttons are ordinary Avalonia buttons inside the template, so
they take focus and respond to Enter and Space like any other button. The control itself is focusable but is not a tab
stop: Tab moves between the buttons of the face that is showing.

The state machine has five values, `Idle`, `Confirming`, `Pending`, `Done` and `Error`. `State` reads and writes that
value and is two-way by default, so a view model can observe the flow, start it or force a face without running any
action.

### Anatomy

1. **Resting pill** (`Label`, `Icon`): a 36 px tall pill on the raised surface color with the label in the small text
   size and medium weight. `Icon` is an optional 16 px glyph before the label. The pill has 14 px of padding on the left
   and 16 px on the right. Hover, press and keyboard focus add a tint of the foreground color at 5%, 9% and 6%.
2. **Question** (`Prompt`): the question text, up to 256 px wide, with 12 px of padding on the left and 8 px on the right.
3. **Cancel** (`CancelLabel`): a secondary answer in the secondary text color. Its hover, press and focus fills are the
   foreground color at 7%, 11% and 8%.
4. **Confirm** (`ConfirmLabel`, `Tone`): the answer that runs the action. The danger tone uses the danger color at 13%
   at rest, 18% on hover and 22% when pressed, with the danger color for the text. The neutral tone uses the accent fill
   with the accent foreground.
5. **Countdown** (`CountdownSeconds`, `CountdownLabel`): a ring that drains over the countdown, the remaining seconds
   and its own Cancel button.
6. **Pending** (`PendingLabel`, `UndoingLabel`): a spinner and a label while `ConfirmAction` runs. `UndoingLabel` shows
   while `UndoAction` runs.
7. **Result** (`DoneLabel`, `UndoLabel`): a check mark that draws itself, the done label and an Undo button when
   `UndoAction` is set.
8. **Error** (`ErrorLabel`, `RetryLabel`): the error label in the danger color and a Retry button.

The answer buttons are 28 px tall, which is the pill height less 8 px, so every face fits inside the pill. Each face
keeps its natural width; only the pill's width animates between them.

### Actions and undo

`ConfirmAction` runs when the question is answered. A task that has not completed keeps the pending face on screen. A
task that throws moves the control to the error face, and Retry runs `ConfirmAction` again. When the task completes, the
result face shows `DoneLabel`.

`UndoAction` adds Undo to the result. Pressing Undo runs it with the pending face showing `UndoingLabel`, and the control
returns to rest when it finishes. Without `UndoAction` the result has no Undo button.

<Demo name="ErrorAndRetry" />

```csharp
delete.ConfirmAction = () => documentService.DeleteAsync(selected);
delete.UndoAction = () => documentService.RestoreAsync(selected);
```

The sample above fails every other attempt, so the error face and Retry appear during the demo. In production the
task should throw only for failures the person can retry, such as a network error. A validation error that the person
must fix is better shown in the page than behind a Retry button.

### Choosing a confirm mode

`ConfirmMode` sets how the question is answered. The default is `Press`.

| Mode | How the action is answered | Choose it when |
|---|---|---|
| `Press` (default) | One press on Confirm, or Enter from anywhere in the question | The question is clear and the result can be undone or the action is cheap to repeat |
| `Hold` | Confirm is held until its fill completes; `HoldDuration` sets the time (1 second by default) | A tap is too easy to trigger by accident, such as removing a member from a shared workspace |
| `Countdown` | Confirm starts a `CountdownSeconds` countdown (3 seconds by default) that Cancel or Escape can stop | The person may need a last chance after deciding, such as discarding a draft they just closed |

### Hold mode

In `Hold` mode, a pointer press on Confirm, or Enter or Space while Confirm has focus, fills the Confirm button from its
leading edge. The action runs when the fill completes. Letting go early springs the fill back to empty and announces
that a longer hold is needed. The help text of Confirm is "Press and hold".

<Demo name="Hold" />

```xml
<arc:ConfirmMorph Label="Remove member"
                  ConfirmLabel="Remove"
                  ConfirmMode="Hold"
                  HoldDuration="00:00:01" />
```

### Countdown mode

In `Countdown` mode, answering Confirm starts the countdown rather than the action. The ring drains and the seconds
count down. The action runs when the countdown reaches zero, or at once when Enter is pressed during the countdown.
Cancel, the Escape key or an outside press stops it, and the action does not run.

<Demo name="Countdown" />

```xml
<arc:ConfirmMorph Label="Discard draft"
                  ConfirmLabel="Discard"
                  ConfirmMode="Countdown"
                  CountdownSeconds="3" />
```

`CountdownLabel` sets the text before the seconds. When it is not set, the control uses the pending label followed by
"in".

### Labels and tone

The eight text properties (`ConfirmLabel`, `CancelLabel`, `PendingLabel`, `DoneLabel`, `ErrorLabel`, `RetryLabel`,
`UndoLabel` and `UndoingLabel`) set the words of each face. Their defaults are "Delete", "Cancel", "Deleting", "Deleted",
"Couldn’t finish", "Retry", "Undo" and "Restoring", so a control with no text properties set still reads as a delete
action. Set every one of them that the action changes, because the defaults are English and describe deletion.

`Tone="Neutral"` colors the confirm answer with the accent for an important action that can be reversed. The default
`Danger` suits destructive actions.

<Demo name="NeutralTone" />

### Controlled state and timeouts

`State` is two-way. Setting it from code moves the control to that face without running an action, so a view model can
restore the result of an earlier session or show the pending face while its own request is in flight. `StateChanged`
reports each move with `OldState` and `NewState`, and `Cancelled` is raised when the question or the countdown is
cancelled.

`ConfirmTimeout` returns an unanswered question to rest (6 seconds by default), and `ResultTimeout` removes a result
(5 seconds by default). A pointer resting on the control holds both timers, and so does a minimized window, so the
person is never hurried while they are looking at the control. Set either timeout to zero to turn it off.

```csharp
confirm.StateChanged += (_, e) => log.Text = $"State: {e.NewState}";
confirm.Cancelled += (_, _) => log.Text = "Cancelled";
confirm.State = ConfirmMorphState.Idle;
```

Setting `State` to `Pending` or `Done` from code shows that face without running `ConfirmAction` or `UndoAction`. Calling
`Ask()` opens the question as a press on the resting button would, and `Cancel()` closes an open question or countdown.
`Ask()` does nothing unless the control is in `Idle` and enabled.

### Focus and dismissal

The control moves focus with the morph so that the keyboard always has a sensible place. Confirm receives focus when the
question opens, the control itself receives focus while a countdown or the pending work is running, Undo or Retry
receives focus on the result or the error, and the resting pill receives focus again when the control returns to rest.

An open question is dismissed by:

- Cancel, or the Escape key;
- a press outside the control, which answers the question no (set `IsOutsidePressCancelEnabled` to `False` to keep
  the question open on an outside press);
- `ConfirmTimeout`, when the person does not answer.

`ResultTimeout` also returns a result to rest when the person does not act on it.

### Disabled

The resting pill drops its shadow when `IsEnabled` is `False`, and `Ask()` ignores the request. Say why nearby, in the
row's description.

<Demo name="Disabled" />

### Example: project danger zone

A settings page that holds two actions with different weight. "Archive project" is reversible, so it uses the neutral
tone, and its question is open in the markup below so the state is visible. "Delete project" is permanent, so it keeps
the danger tone and rests as a pill until the person asks for it. Each row explains the consequence in one sentence
next to the control, so the question itself can stay short.

![A danger zone with an open archive question and a resting delete button](images/DangerZone.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Danger zone"
               FontSize="{arc:ArcDesignTokenResource TypeLG}"
               FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Border Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
            Padding="20">
        <StackPanel Spacing="20">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Grid.Column="0" Spacing="4" VerticalAlignment="Center">
                    <TextBlock Text="Archive Acme Analytics"
                               FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Archived projects can be restored for 30 days."
                               TextWrapping="Wrap"
                               FontSize="{arc:ArcDesignTokenResource TypeSM}"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
                <arc:ConfirmMorph Grid.Column="1"
                                  Label="Archive project"
                                  Prompt="Archive this project?"
                                  ConfirmLabel="Archive"
                                  PendingLabel="Archiving"
                                  DoneLabel="Archived"
                                  Tone="Neutral"
                                  State="Confirming"
                                  VerticalAlignment="Center" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource BorderSubtle}" />
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Grid.Column="0" Spacing="4" VerticalAlignment="Center">
                    <TextBlock Text="Delete Acme Analytics"
                               FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="All 42 dashboards and their data are removed for good."
                               TextWrapping="Wrap"
                               FontSize="{arc:ArcDesignTokenResource TypeSM}"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
                <arc:ConfirmMorph Grid.Column="1"
                                  Label="Delete project"
                                  Icon="{x:Static arc:ArcIcons.Trash2}"
                                  Prompt="Delete this project?"
                                  VerticalAlignment="Center" />
            </Grid>
        </StackPanel>
    </Border>
</StackPanel>
```

### Binding to a view model

With CommunityToolkit.Mvvm, the view model owns the state of the action and the work it runs. `ConfirmAction` and
`UndoAction` are `Func<Task>` properties, so the view model exposes them as properties of that type and binds them. The
`State` value is bound two-way, so the view model can also move the control.

```csharp
public interface IProjectService
{
    Task ArchiveAsync(string projectId);

    Task RestoreAsync(string projectId);

    Task DeleteAsync(string projectId);
}

public partial class ProjectDangerViewModel : ObservableObject
{
    private readonly IProjectService _projects;

    public ProjectDangerViewModel(IProjectService projects)
    {
        _projects = projects;
    }

    [ObservableProperty]
    private ConfirmMorphState _archiveState = ConfirmMorphState.Idle;

    [ObservableProperty]
    private string _projectName = "Acme Analytics";

    public Func<Task> ArchiveAction => () => _projects.ArchiveAsync(ProjectId);

    public Func<Task> UndoArchiveAction => () => _projects.RestoreAsync(ProjectId);

    public Func<Task> DeleteAction => () => _projects.DeleteAsync(ProjectId);

    public string ProjectId { get; } = "prj_2026_0412";
}
```

```xml
<StackPanel Spacing="12">
    <arc:ConfirmMorph Label="Archive project"
                      Prompt="Archive this project?"
                      ConfirmLabel="Archive"
                      Tone="Neutral"
                      State="{Binding ArchiveState, Mode=TwoWay}"
                      ConfirmAction="{Binding ArchiveAction}"
                      UndoAction="{Binding UndoArchiveAction}" />
    <arc:ConfirmMorph Label="Delete project"
                      Icon="{x:Static arc:ArcIcons.Trash2}"
                      ConfirmAction="{Binding DeleteAction}" />
</StackPanel>
```

- `State` is two-way and is the only property that needs `Mode=TwoWay`, which is already its default. Read it to keep
  other controls in step, such as disabling the row's other actions while an archive runs.
- `ConfirmAction` and `UndoAction` are one-way: the view model supplies the work, and the control calls it.
- The event `StateChanged` is useful for logging, but the view model should not need it when `State` is bound.
- Compiled bindings need `x:DataType` on the view; the example leaves it out for brevity.

### Content guidelines

- **Label**: a verb and its object in sentence case, such as "Delete project" or "Archive project". The label is what the
  person sees before they decide, so it must say what the action does.
- **Prompt**: the question with the count or the name in it, ending with a question mark: "Delete 3 files?" A prompt that
  names the object tells the person what they are answering.
- **Confirm label**: the verb alone, "Delete", "Archive", "Revoke". It answers the question with the same word the label
  uses, so "Yes" never appears.
- **Done label**: the past tense of the verb, "Deleted", "Archived". Keep it short so the result fits the pill.
- **Error label**: say that the action did not finish, not what went wrong in technical terms. The Retry button handles
  the next step.

| Instead of | Write |
|---|---|
| Yes | Delete |
| Are you sure you want to delete this item? | Delete 3 files? |
| Oops, something went wrong! | Couldn’t finish |
| Done! | Deleted |
| Undo last action | Undo |

### Best practices

**Do**

- Name the object in the label or the prompt, so the question is answered in context: "Delete 3 files?" needs no reading
  of the page around it.
- Pick `Tone="Neutral"` only for reversible actions. Keep the danger tone for anything the person cannot take back, so
  the color keeps its meaning.
- Offer `UndoAction` when the server can restore the object for a while. The result stays long enough to press Undo
  before `ResultTimeout` returns the control to rest.
- Set every text property the action needs. The defaults describe deletion, and a generic "Deleting" label on an
  archive action reads as a mistake.
- Use `Countdown` or `Hold` for the actions that are costly to repeat, and `Press` for the rest. One mode per screen
  keeps the answers consistent.

**Avoid**

- Using the control for consequences that need several lines of explanation or a typed name. The question face is one
  line; put the detail in a `Dialog` instead.
- Long prompts. The question face is sized for one short line, and the prompt has a 256 px maximum width; keep it to a
  question about the object.
- Setting `ConfirmTimeout` to a very short time for a question the person must read. Six seconds is enough for a
  glance; much less makes the question vanish before it is read.
- Turning off `IsOutsidePressCancelEnabled` by default. An outside press that does nothing feels like a stuck control;
  keep the default unless the question must stay open while the person works elsewhere.
- Running a long action in `ConfirmAction` without a pending label. The spinner and the label are the only signal that
  work is in progress.

### Common questions

**Why does the Enter key that opened the question not answer it at once?** A press in the first 280 milliseconds after
the question opens is ignored, and so is the auto-repeat of Enter. This stops a double click or a held key from
answering a question the person has not read.

**Does a failed action leave the control in a broken state?** No. A `ConfirmAction` task that throws moves the control
to `Error`, which shows `ErrorLabel` and a Retry button. Retry runs `ConfirmAction` again, and Escape returns the control
to rest without running it.

**How do I open the question from a keyboard shortcut or from code?** Call `Ask()`. It opens the question in the same way
a press on the resting pill does, and it is ignored unless the control is in `Idle` and enabled.

**How do I know when the action has finished?** Watch `State` for `Done`, or handle `StateChanged`. The control moves to
`Done` after `ConfirmAction` completes and to `Error` when it throws, so a view model can refresh its list in either
case.

## API reference

<!-- api: ConfirmMorph -->

## Accessibility

- The question is a group named by its prompt. The resting button keeps the name of its label.
- The focus follows the morph: Confirm when the question opens, the control while a countdown or the pending work is
  running, Undo or Retry on the result, and the resting pill at rest.
- Presses in the first 280 milliseconds after the question opens are ignored, so a double click or a held Enter does not
  answer it by accident.
- In `Hold` mode the help text is "Press and hold". Letting go early announces that a longer hold is needed.
- The prompt, the pending and the result words, and "Undo is available" are announced politely through a status region.
- Enter and Space press the button on the current face. Escape cancels the question or countdown, and returns a result
  or an error to rest.
- Keyboard focus shows as a tint of the button's background (6% to 20%, by tone and state), not as a ring.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` / `Space` on the resting pill | Opens the question |
| `Tab` / `Shift+Tab` | Moves between the buttons of the face that is showing, such as Cancel and Confirm |
| `Enter` (`Press` mode) | Answers yes from anywhere in the question, except while Cancel has focus |
| `Enter` / `Space` on Confirm (`Hold` mode) | Fills Confirm while held; the action runs when the fill completes |
| `Enter` / `Space` on Confirm (`Countdown` mode) | Starts the countdown |
| `Enter` during a countdown | Runs the action now |
| `Enter` / `Space` on Cancel, Undo or Retry | Presses that button |
| `Escape` | Cancels the question or the countdown; returns a result or an error to rest |

## Motion

- Only the width of the pill changes. It grows on a spring and shrinks more smoothly, and the text does not scale.
- Each new face's parts clear a small blur in reading order, and the old face fades out in place.
- The result check draws itself. The countdown ring drains and its digits roll, and the pending spinner turns.
- In `Hold` mode a filled copy of the danger color reveals from the left as Confirm is held. Letting go early springs it back.
- Reduced motion removes the width spring and the blur, crossfades the faces and slows the spinner. The hold fill and the
  countdown ring still show progress. Off switches the faces at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Question opens or closes | The pill width grows on a spring and shrinks smoothly | No width spring; the faces crossfade | Immediate |
| Face changes | Parts clear a small blur in reading order, 35 ms apart; the old face fades out over 120 ms | Crossfade | Immediate |
| Result | The check mark draws itself | The check appears without drawing | Immediate |
| Countdown | The ring drains and the seconds roll | The ring still drains | Immediate |
| Pending | The spinner turns | The spinner turns more slowly | Immediate |
| Hold fill | The fill reveals from the leading edge and springs back on early release | The fill still shows progress | Immediate |

## Related

- `Dialog` for a consequence that needs more explanation or a typed name.
- `ActionButton` for an asynchronous submit with no question.
- `HoldToConfirm` for an action that must never fire on a tap.
- `DropdownMenu` for a list of commands without one default action.

| Component | Choose it when |
|---|---|
| `Dialog` | The consequence needs several lines, a list of affected items or a typed name |
| `ActionButton` | The action is safe and asynchronous, and no question should come first |
| `HoldToConfirm` (Gestures) | A single hold is the whole interaction and the action must never fire on a tap |
| `DropdownMenu` (Menus) | The row or toolbar offers several commands and none is the default |
| `CopyButton` | The action copies a value to the clipboard and shows the result in place |
| `ButtonGroup` | Several buttons form one toolbar and none of them needs its own question |
