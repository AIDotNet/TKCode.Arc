---
title: Action swap
summary: Swap an action label while preserving its place and intent.
---

<Demo name="Preview" />

## When to use

`ActionSwap` is one control that cycles through a few related states: Follow and Following, Send invite and Sent, Add
to library and Added. A press moves it to the next state and wraps around at the end. The pill keeps its place on the
screen while its width, tone, icon and letters change together, so the person sees that it is the same action in a new
state rather than a different button.

- **Following a person or a topic**: Follow becomes Following and fills in quietly, and a second press goes back. The
  pending word keeps the pill the same width of thought while the request is sent.
- **Sending an invitation or a message**: Send invite shows Sending while the mail service answers, then Sent with a
  drawn check, and returns to Send invite by itself after two seconds so the next person can be invited.
- **Adding something to a collection** (a library, a cart, a watch list): the pill folds into a round spinner while the
  server works and opens again with the result.
- **A toggle that can fail**: a rejected action shows a short failure word in place, then returns to the state it left,
  so the person can retry at once.

## When not to use

- **One save or publish that ends in a success tick**: use `ActionButton`, which has a fixed idle, pending and success
  sequence and a reset timer.
- **An action whose label never changes**: use `Button`.
- **A light and dark switch**: use `ThemeSwitcher`.
- **A destructive action that asks first**: use `ConfirmMorph`, which asks the question in place before anything runs.
- **A dense table with one control per row**: every letter is an animated element, so keep the count of swapping labels
  on a screen small.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ActionSwap>
        <arc:ActionSwap.States>
            <arc:ActionSwapState Id="idle" Label="Send invite" Icon="{x:Static arc:ArcIcons.Send}" />
            <arc:ActionSwapState Id="sent" Label="Sent" Icon="{x:Static arc:ArcIcons.Check}" Tone="Success"
                                 PendingLabel="Sending" HoldFor="0:0:2" />
        </arc:ActionSwap.States>
    </arc:ActionSwap>
</UserControl>
```

```csharp
var swap = new ActionSwap { AsyncAction = async (next, current) => await invites.SendAsync() };
swap.States.Add(new ActionSwapState { Id = "idle", Label = "Send invite", Icon = ArcIcons.Send });
swap.States.Add(new ActionSwapState
{
    Id = "sent", Label = "Sent", Icon = ArcIcons.Check, Tone = ActionSwapTone.Success,
    PendingLabel = "Sending", HoldFor = TimeSpan.FromSeconds(2)
});
```

<Demo name="SendInvite" />

`ActionSwap` derives from Avalonia's `Button`. It keeps `Click`, `Command`, `IsEnabled`, activation with Enter and
Space, the focus model and the automation peer. Describe the faces in `States`, in order; `Value` is the `Id` of the
face being shown (the first one when it is null). The control needs at least two states: with fewer it is inert and
dimmed.

### States

Each `ActionSwapState` is one face:

- `Id` and `Label` are required. The label is also the accessible name while the state is shown.
- `Icon` is a 16 px Lucide glyph before the label. Use `ArcIcons` or `ArcIcons.Parse(pathData)`.
- `Tone` colors the pill: `Neutral` (the default, foreground fill), `Accent`, `Success`, `Warning` and `Quiet`. Filled
  tones carry the primary action; the tinted `Success` and `Warning` tones confirm a result with most of the color on
  the icon, and `Quiet` is a bordered surface for the "already done" side of a toggle.
- `PendingLabel` is shown while `AsyncAction` runs on the way into this state, such as Sending before Sent. Without it
  the pill folds into a round spinner.
- `HoldFor` holds the state for a while and then moves on by itself, for brief confirmations such as Sent. The timer
  restarts only when the state or the duration changes, so a data refresh never resets it.

<Demo name="Tones" />

### Actions

`AsyncAction` runs on press with the next and the current state, before the control moves on. Return a task to show the
working phase, or null to move on at once.

- While the task runs the control is **working**: it ignores presses, reports itself busy and keeps keyboard focus. A
  fast answer still shows the working shape for at least 450 ms, so it reads as a step and not as a flash.
- When the task completes the new state **commits**: `Value` changes, the label is announced and `ValueChanged` is
  raised.
- When the task faults, or the action throws, the control shows `FailureLabel` (default "Try again") for 1.8 seconds
  and returns to the state it left. Nothing commits.

```csharp
follow.AsyncAction = async (next, current) =>
{
    await api.SetFollowingAsync(userId, next.Id == "following");
};
follow.ValueChanged += (_, e) => status.Text = e.State.Label;
```

<Demo name="FollowToggle" />

### Working without a label

A state with no `PendingLabel` folds the whole pill into a circle around a small spinner while its action runs. The
padding eases down on the same spring as the width, and the letters give way as the shape closes. When the result
arrives the pill opens again and the new letters rise in as the shape widens.

<Demo name="Spinner" />

### Failure

<Demo name="Failure" />

Show the reason beside the control. The failure word is short on purpose ("Try again"); the detail belongs in a message
the person can read after the pill has returned to its state.

### Sizes

`Size` is `Small` (36 px high), `Medium` (44 px, the default) or `Large` (50 px), with 16, 20 and 24 px of inline
padding. The pill hugs its label, so give related states labels of similar length to keep neighbours from shifting.

<Demo name="Sizes" />

### Disabled

Set `IsEnabled` to `False` to dim the control to 60 percent and remove it from the tab order.

<Demo name="Disabled" />

### Content guidelines

- **Labels**: a verb and its object in sentence case, short: "Send invite", "Follow". The label is both what is shown
  and the accessible name.
- **Pending labels**: the present participle with no ellipsis: "Sending". The spinner already signals progress.
- **Result labels**: the past tense in one or two words: "Sent", "Added".
- **Count**: two or three states. More than that is a menu or a segmented control.

## API reference

<!-- api: ActionSwap -->

<!-- api: ActionSwapState -->

<!-- api: ActionSwapValueChangedEventArgs -->

| `ActionSwapTone` value | Look |
|---|---|
| `Neutral` | Foreground fill with background text (default). |
| `Accent` | Accent fill with accent foreground text. |
| `Success` | A success-tinted surface, a success-colored icon. |
| `Warning` | A warning-tinted surface, a warning-colored icon. |
| `Quiet` | A bordered surface with the default text color. |

| `ActionSwapPhase` value | Meaning |
|---|---|
| `Idle` | Showing the current state. A press runs the action. |
| `Working` | `AsyncAction` is running. Presses are ignored. |
| `Failed` | The action failed. `FailureLabel` shows for 1.8 seconds. |

## Accessibility

- The control is a native button: the accessible name is the visible label of the face being shown, so the animated
  letters are not read one by one.
- While working it reports itself as unavailable but keeps keyboard focus; a hidden polite status region announces new
  labels, pending labels and failures.
- With fewer than two states the control is inert.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` / `Space` | Runs the action and moves to the next state. Ignored while working. |
| `Tab` / `Shift+Tab` | Moves to the next or previous control. A working pill keeps its focus stop. |

## Motion

- Only the changed letters of a label rise in out of a soft 4 px blur while the kept ones glide to their new places;
  leaving letters lift away a little faster. The width follows on a spring.
- Icons trade places through a blur: the old one shrinks away while the new one grows out of it. A success icon settles
  with a little more give while its stroke wipes in from the leading edge.
- Tone, padding and width move on one spring and every change can be interrupted mid-flight.
- A press scales the pill to 97 percent (98.5 percent when it is wider than 220 px) and springs back. Hover styles apply
  to mouse and pen only; on touch the press scale is the only feedback.

| Change | Full | Reduced | Off |
|---|---|---|---|
| New label or icon | Letters rise from a blur, icons grow, width springs | A 120 ms crossfade, no width or fold animation | Immediate |
| Working without a label | The pill folds into a circle around a spinner | Immediate fold, a still ring | Immediate |
| Press | Scales to 97 percent on a snappy spring | No scale | No scale |

## Related

| Component | Choose it when |
|---|---|
| `ActionButton` | One save or publish ends in a success tick and a reset timer |
| `Button` | The label never changes state |
| `ThemeSwitcher` | The action switches between light and dark |
| `ConfirmMorph` | A destructive action should ask a question in place |
