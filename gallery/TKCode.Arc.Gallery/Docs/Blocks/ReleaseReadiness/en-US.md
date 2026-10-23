---
title: Release readiness
summary: A release gate where a short list of checks unlocks one action, with a progress bar, the owners, a notify switch and the result in place.
---

<Demo name="Preview" />

## When to use

`ReleaseReadiness` is the last step before something goes out: a short list of things that must be true, and one button
that stays locked until they are. As checks are ticked the bar fills and the text counts down ("2 to go", "All clear");
when every required check is done the bar turns green and the button unlocks. Pressing it runs your code, shows a spinner
while it works, and then says what happened right under the footer.

- **A release or rollout.** Sign-off, tests, release notes and support briefed, then "Start rollout". The notify switch
  decides whether a channel hears about it, and the success line says so.
- **A handover between teams.** Runbook, dashboards, alerts and open incidents must be done; a couple of nice-to-have
  checks can be marked optional so they are shown without holding the handover back.
- **Any irreversible action with a checklist.** A migration, a price change, a campaign send: anything where a person
  should confirm a few facts first and see the result in the same place.

## When not to use

- **Long or nested checklists.** The block is designed for three to eight checks in one list. Use a task list, a
  `Timeline` or a project board for dozens of items or for items with owners and dates of their own.
- **Checks the system can verify itself.** If a build server already knows whether tests passed, show that status
  instead of asking a person to tick it; a tick here is a person's word.
- **Forms.** Checks are booleans with a title and a description. Use `Checkbox`, `Input` and a submit button when people
  enter values.
- **Several actions.** The gate unlocks exactly one action. Offer other actions elsewhere on the page.

## Usage

The block derives from Avalonia's `TemplatedControl`. Each check is a `ReleaseCheck`, an `AvaloniaObject` with a title, an
optional description and `IsDone` (two-way). Owners are `ProjectPerson`s drawn as generated avatars: initials on a soft
tint picked from the person's key, never a photo unless you give one.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ReleaseReadiness Title="Version 5.3"
                          Subtitle="Lumen mobile · goes live Tuesday"
                          ChannelName="#launches">
        <arc:ReleaseReadiness.Owners>
            <arc:ProjectPerson Name="Rina Kato" />
            <arc:ProjectPerson Name="Samir Aziz" />
        </arc:ReleaseReadiness.Owners>
        <arc:ReleaseCheck Title="Design signed off" Description="Final screens approved by product" IsDone="True" />
        <arc:ReleaseCheck Title="Smoke tests green" Description="Build 5.3.0-beta.2 passed on preview" />
        <arc:ReleaseCheck Title="Release notes written" Description="Customer summary drafted and reviewed" />
        <arc:ReleaseCheck Title="Support briefed" Description="Help desk knows what is changing" />
    </arc:ReleaseReadiness>
</UserControl>
```

### Anatomy

- **Header**: `Title`, `Subtitle` and the owners' avatars on the right (hidden when there are none).
- **Progress**: what is left on the left, the count ("1 of 4") on the right in tabular figures, and a 4px bar under them.
- **Checks**: one row per check. The whole label is part of the checkbox, so pressing the title or the description ticks
  it, and pointing anywhere on the row tints it.
- **Footer**: the notify switch at the start and the button at the end. When the two do not fit on one line the buttons
  move to a second line, still at the end.
- **Status line**: opens under the footer with the success or the error text.

### Checks and the gate

`TotalCount` counts the required checks and `DoneCount` the required ones that are done; `IsReady` is true when there is at
least one check and every required one is done. A check with `IsRequired="False"` is shown and can be ticked, but is left
out of the counts, the bar and the gate. Use `ChecksSource` for a collection you own (an `ObservableCollection` is
followed), or fill `Checks`. Ticking a box writes `IsDone` back, so a two-way binding on it keeps your model in step.

`CheckChanged` reports every tick with the old and new value and the counts after it; `ReadyChanged` fires when the gate
opens or closes again.

### Starting the release

Set `ReleaseAction` to the work the button starts. It receives a `ReleaseRequest` (whether to notify, every check, the
optional checks that were skipped, and the attempt number) and a cancellation token. While the task runs `State` is
`Releasing`: the button shows a spinner and keeps the keyboard focus, and the checks and the switch are locked. When it
finishes, the button reads `ReleasedText` with a check mark, the status line says the release is on its way (and that the
channel was told, when the switch was on) and a reset button appears.

```csharp
gate.ReleaseAction = async (request, cancellation) =>
{
    await deployments.StartAsync("lumen-mobile", "5.3.0", notify: request.Notify, cancellation);
};
gate.Released += (_, e) => log.Info($"Rollout started on attempt {e.Request.Attempt}");
```

`ReleaseCommand` runs first with the request as its parameter; when it cannot execute, the release does not start.
`ReleaseStarting` fires before either and can cancel. `ReleaseAsync()` does what the button does, `CancelRelease()` cancels
the token and puts the gate back at once (a late result of the task is ignored), and `Reset()` puts every check back:
to `IsInitiallyDone` when it is set, otherwise to the value the check had when the block first showed it. The notify switch
keeps its value across a reset.

### Failure and retry

An exception from the action (or the command) sets `State` to `Failed`. The error shows in the status line in the danger
color, the checks and the switch unlock, and the button is ready for another try with the focus still on it. The line
shows the exception's message, `ErrorFormatter`'s text when you set one, or `FailureText` when the message is empty.
Changing a check after a failure clears the error. A cancellation (`CancelRelease`, or the block leaving the window while
the task runs) is not a failure: the gate simply goes back to ready.

<Demo name="Failing" />

### Framed or flat

`IsFramed` (the default) draws a card at most 560 wide with a 1px border, a 16 radius, the surface color and lines above
the checks and the footer. Set it to false to lay the block flat on the page at the container's width, for a full page
or a side panel; the rows keep their light dividers.

<Demo name="Flat" />

### Narrow containers

The block reads its own width, not the window's. At 388 or less every side padding tightens from 20 to 16. The header stays
on one line and shortens the subtitle with an ellipsis; the footer moves the buttons under the switch when they do not fit
beside it.

### Other words, optional checks

Every text is a property with an English default: `ReleaseText`, `ReleasedText`, `ResetText`; the progress texts
`RemainingFormat` ("{0} to go"), `RemainingOneText`, `ReadyText`, `ReleasingText` and `CountFormat` ("{0} of {1}"); the
notify switch's `NotifyText` or `NotifyFormat` ("Notify {0}") with `ChannelName`; the success lines `SuccessFormat` and
`SuccessNotifiedFormat` (with `ReleaseName`, or the title, and the channel); and the screen reader names `ChecksLabel`,
`ProgressLabel`, `OwnersLabel` or `OwnersLabelPrefix` with `AndWord`. `IsNotifyVisible` hides the switch and
`IsResetVisible` the reset button.

<Demo name="Handoff" />

## API reference

<!-- api: ReleaseReadiness -->

<!-- api: ReleaseCheck -->

<!-- api: ReleaseRequest -->

<!-- api: ReleaseReadinessState -->

## Accessibility

- **Tab** moves through the checks, the notify switch and the button. The button is not a tab stop until the gate is
  ready; after a release only the reset button is.
- **Space** ticks or clears the focused check. **Enter** does not, so a check is never ticked by accident.
- **Space** or **Enter** flips the notify switch and presses the buttons.
- After a release the focus moves to the reset button (or to the status line when the reset button is hidden). After a
  reset it moves to the first check that is not done. After a failure it stays on the button for a retry. The focus only
  moves when it was inside the block, so a release started from code never takes it.
- The block is named by its title, and the title is a level 2 heading.
- The owners are one group named "Owners: Rina Kato and Samir Aziz" (`OwnersLabel` or `OwnersLabelPrefix`); the avatars are
  not read one by one.
- The bar is a read-only progress bar named by `ProgressLabel` with the required checks done as its value and all of them
  as its maximum. The list of checks is named by `ChecksLabel`.
- Each checkbox is named by its check's title and described by its description; required checks say so.
- What is left ("2 to go", "All clear", "Rolling out") is announced politely as it changes; the count beside it is not.
  The success line is announced politely and the error assertively, and an error repeated on a retry is announced again.
- While the action runs the button keeps its name and reports the releasing text as its status.

## Motion

- **Ticking a check**: the bar's fill slides on a critically damped spring (no overshoot, about 580 ms) and turns green in
  240 ms when every required check is done. The checkbox draws its mark on its own spring; the title fades to the
  secondary color in 160 ms. The texts change in place without animation.
- **Hover**: the row tints in 160 ms.
- **Releasing**: the button shows its spinner, then cross-fades its label with a short blur while its width springs to
  the new text. The status line opens on a smooth spring with a separate 160 ms fade; a reset closes it the same way.
- Reduced motion places the bar, the colors and the status line at once and keeps only the button's short cross-fade.
  Off applies every change at once.

## Related

- `Checkbox`, `Switch` and `Button`, the controls the block is made of.
- `Progress` for a standalone progress bar with a label.
- `ActionButton` for a single action with pending and success states but no checks.
- `Project board` and `Kanban board`, the other Projects blocks.
