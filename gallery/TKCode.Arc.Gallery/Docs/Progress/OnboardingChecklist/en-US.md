---
title: Onboarding checklist
summary: Guide a new workspace through setup, one step at a time.
---

<Demo name="Workspace" />

## When to use

An onboarding checklist walks someone through a short setup they will do once. A ring and a count follow every check,
a finished step strikes through and folds into a completed row, and the next step opens by itself.

- **A new account or workspace**: three to eight steps such as creating a project and inviting a team, in any order.
- **Steps that do the work themselves**: give a step an `ActionLabel` and an `Action`. The button shows progress while
  the task runs and checks the step off when it completes.
- **Steps only the person can confirm**: leave the action out, and the round check marks the step by hand.
- **A checklist that stays around for a while**: `IsHidden` folds it down to its header, so it can wait on a dashboard
  without taking the page.

## When not to use

- **A flow with a strict order**: use `Stepper`, which shows one position in a fixed sequence.
- **Progress of one running task**: use `Progress`, which measures a known amount of work.
- **A list of tasks that keeps growing**: use a `Table` or a list; this control is for a short setup that ends.
- **A single question**: use `Checkbox`.

## Usage

List the steps inside the control and give each one a stable `Id`. Set `Title`, and optionally `DoneTitle` for the
moment every step is done (it reads "You're all set" by default).

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OnboardingChecklist Title="Before your first release" DoneTitle="Ready to ship">
        <arc:OnboardingChecklistStep Id="guide" Title="Read the release guide" Description="It covers versioning." />
        <arc:OnboardingChecklistStep Id="owner" Title="Add a second owner" />
    </arc:OnboardingChecklist>
</UserControl>
```

### Steps checked off by hand

Without an `ActionLabel`, a step has no button: its round check is the only way to finish it. Checking a step again
before it folds away, or from the completed row, marks it as not done and brings it back.

<Demo name="ByHand" />

### Actions and errors

`Action` is a `Func<Task>`. While it runs, the button shows a spinner and ignores presses. When the task completes the
step is checked off; when it throws, the step stays open and shows the exception's message above the button, so the
person can try again. `Command` also runs a step, for view models.

<Demo name="FailingAction" />

### Saving progress

Set `Completed` to the keys of the steps finished earlier; those steps start in the completed row. `CompletedChanged`
reports the keys in step order whenever a person or an action changes them, so you can save them. `CompleteStep` and
`ReopenStep` do the same from code, for example when the work finished somewhere else.

<Demo name="PartlyDone" />

### Hiding the checklist

The Hide button folds the body and leaves the header, with its ring and count. Bind `IsHidden` to keep the choice
between visits; `HiddenChanged` reports it when a person changes it.

<Demo name="Hidden" />

### Other languages

The words the control writes itself, visible and spoken, are in `Strings`. Set a new `OnboardingChecklistStrings` to
translate them.

## API reference

<!-- api: OnboardingChecklist -->

<!-- api: OnboardingChecklistStep -->

`OnboardingChecklistStrings` has one text property for each fixed word: `Show`, `Hide`, `ShowChecklist`, `HideChecklist`,
`CountFormat`, `Completed`, `Progress`, `ProgressFormat`, `OpenSteps`, `CompletedSteps`, `StepDoneFormat`,
`StepUndoneFormat`, `AllDoneFormat` and `ActionFailed`.

## Accessibility

| Key | Action |
|---|---|
| Tab | Moves between checks, titles, actions, the Hide button and the completed row |
| Space, Enter | Presses the focused check, title, action or button |
| Up, Down | Moves between step titles, wrapping at the ends |
| Home, End | Goes to the first or last step title |

When a focused step finishes, focus moves to the next step's action, or its title when it has none, or to the completed
row when nothing is left. Reopening a step from the completed row moves focus to that step's check. Controls in a part
that is folding away or hidden take no focus.

The control is a named group whose name follows the title, and it announces each change politely: "Create a project
done. 1 of 4 complete." The ring reports itself as a progress bar named "Setup progress" with a status such as "1 of 4
steps done". Each check is a check box named after its step, and titles, the Hide button and the completed row report
whether they are expanded. An action that is running or finished stays focusable and reports its state.

## Motion

Checking a step grows the fill from 0.6 on the snappy spring while the tick draws in 240 ms, and a one pixel strike
crosses the title in 480 ms, line by line when it wraps. After 720 ms the step folds into the completed row. Rows open on
the smooth spring and close on a slightly quicker one; the ring fills on the morph spring and draws a tick in its center
when the last step is done. Digits turn in the direction the count moved, and the title and the Hide label rise out of a
soft blur without changing the size of their slot.

With reduced motion, sizes and the ring change at once and only opacity fades. With motion off, everything changes at
once. The 720 ms pause stays in every case, because it lets a person see what they finished.

## Responsive behavior

The control fills the width it is given, and long titles and descriptions wrap. At 380 px and below, the Hide button shows
only its chevron and keeps its spoken name, so the header stays on one line. Each check has a 40 px target. Hover effects
do not apply to touch.

## Related

| Component | Choose it when |
|---|---|
| `Stepper` | The steps have a strict order and one current position |
| `Progress` | One task runs and its progress is known |
| `Accordion` | The sections are content to read, not work to finish |
| `Checkbox` | There is one independent choice |
