---
title: Multi-step form
summary: A short form asked one step at a time in a card, with a progress list, Back and Continue, checks before each step, and a success state at the end.
---

<Demo name="Preview" />

## When to use

`MultiStepForm` breaks a short form into a few focused steps and walks people through them in one card. The header lists
every step with a round marker and counts where they are ("Step 1 of 2"); one step of fields fills the middle; Back and
Continue sit at the bottom, and Continue becomes the finishing action on the last step. Submitting the last step replaces
the fields with a short success message and folds the buttons away.

- **Onboarding and sign-up.** Name a workspace, pick a lead, choose a plan: two to four small decisions that would feel
  heavy as one long form.
- **Intake and requests.** A support request, a booking or an application where each step asks one kind of question and
  the last step sends everything.
- **Settings that come in a fixed order.** Profile, address, delivery: steps a person may step back through to change an
  answer without losing the others.

## When not to use

- **Only the progress indicator.** Use `Stepper` when you need the list of steps without the card, the buttons and the
  fields.
- **Long flows or full pages.** Five or more steps, steps with their own navigation, or a flow that spans pages (a
  checkout) deserve a page layout of their own. The progress list scrolls when steps do not fit, but a long list is a sign
  the flow should be shorter.
- **A single short form.** Three fields do not need steps; put them in one form with one submit button.
- **Tasks done elsewhere.** A checklist of things a person completes in other places (connect a calendar, invite the team)
  is a checklist, not a form.

## Usage

The block derives from Avalonia's `TemplatedControl`. Each step is a `MultiStepFormStep`, an `AvaloniaObject` with a
`Title` (the heading above the fields and the label in the progress list), an optional `Description` line and the step's
fields as `Content`. Write the steps as children; the fields are ordinary controls.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MultiStepForm CompleteLabel="Create workspace"
                       SuccessTitle="You're in"
                       SuccessDescription="Your workspace is ready.">
        <arc:MultiStepFormStep Id="workspace"
                               Title="Workspace basics"
                               Description="Give it a short, memorable name.">
            <arc:Input Label="Workspace name" Text="Northwind studio" />
        </arc:MultiStepFormStep>
        <arc:MultiStepFormStep Id="lead" Title="Pick a lead">
            <arc:Combobox Label="Lead" SelectedIndex="0">
                <arc:SelectItem Content="Priya Raman" />
                <arc:SelectItem Content="Tomás Ortega" />
                <arc:SelectItem Content="June Whitfield" />
            </arc:Combobox>
        </arc:MultiStepFormStep>
    </arc:MultiStepForm>
</UserControl>
```

`Completed` is raised when the last step was submitted and the success block is shown (`CompleteCommand` runs right
after it with the same arguments). The arguments carry the steps, whose content still holds what was entered.

```csharp
form.Completed += (_, e) => workspaces.Create(nameBox.Text, (string?)leadPicker.SelectedValue);
```

### Anatomy

- **Progress header**: one entry per step, a 20 px marker and the step's title. Upcoming steps are muted, the current
  one uses the accent, done ones show a green check. The count on the right reads `CountFormat` ("Step {0} of {1}").
- **Step**: the step's title as a heading, the optional description, then the fields. The card is 540 wide (or as wide
  as its container when that is narrower) and the step area is at least 224 high, so short steps do not make the card jump.
- **Buttons**: Back (disabled on the first step) and the submit button, which reads `NextLabel` ("Continue") and
  `CompleteLabel` ("Finish") on the last step.
- **Success block**: a check, `SuccessTitle` and `SuccessDescription`, centered where the fields were.

### Values are kept

Every step keeps its fields alive while another step is shown: a step that is not current is hidden but stays in the
form, so text typed into it, a selection, bindings and the data context all survive Back and Continue. You can read the
values when `Completed` fires, bind them to your view model, or both. `Restart()` shows the first step again; it does not
clear fields, so reset your model (or the controls) if a fresh start should be empty.

### Checking a step before moving on

A step is submitted by Continue, by the finishing button, or by Enter in a single-line text field of the step. Before the
form moves on it checks, in order:

1. **Data validation errors.** A field in the step that shows a validation error (from a binding, `INotifyDataErrorInfo`,
   or `DataValidationErrors.SetError`) stops the form and takes the focus.
2. **`StepValidating`.** Set `Cancel` on the arguments to stay; good for quick checks that need no waiting.
3. **`MultiStepFormStep.ValidateAsync`.** Your own check, awaited. Return false to stay (usually after setting
   `ErrorMessage` on a field). While it runs `IsBusy` is true, the submit button shows a spinner and keeps the focus, and
   Back is disabled. Back, any other step change, `Restart()` or leaving the window cancels its token. A check that throws counts as a no.

<Demo name="Validation" />

```csharp
// isEmailTaken is your own check, for example a call to your API.
form.Steps[0].ValidateAsync = async cancellation =>
{
    var taken = await isEmailTaken(emailBox.Text, cancellation);
    emailBox.ErrorMessage = taken ? "That email already has an account." : null;
    return !taken;
};
```

### Sending the last step

`CompleteAsync` runs when the last step is submitted, after its checks. The submit button shows a spinner until the task
finishes; then the success block appears and `Completed` is raised. If the task throws, the form stays on the last step,
`CompleteFailed` reports the exception, and the button is ready for another try. Without `CompleteAsync` the form
completes at once.

<Demo name="AsyncSubmit" />

```csharp
// sendRequest is your own call that stores or sends the answers.
form.CompleteAsync = cancellation => sendRequest(request, cancellation);
form.CompleteFailed += (_, e) => status.Text = e.Exception.Message;
```

### Driving the step yourself

`CurrentIndex` (zero-based, two-way) is the step on screen. Set it to start somewhere else or to jump; the form moves with
the motion of the direction of travel and raises `StepChanged` with the old and new index. A value outside the steps is
clamped once the steps are known, so `CurrentIndex="1"` in XAML works before the steps are written. `NextAsync()` does
what Continue does (checks included) and returns whether the form moved; `Back()` does what Back does. `IsComplete` is
two-way too: set it to show the success block without raising `Completed`, or back to false to show the steps again.

<Demo name="Controlled" />

### Narrow containers

Up to 540 wide the card follows its container; in a wider container it stays 540 wide and centers. The block reads its
own width, not the window's: at 388 or less the count moves above the progress list, the paddings tighten from 20 to 16
and the step area grows to at least 240. When the steps do not fit on one line, at any width, the list scrolls sideways
(without a scroll bar) and keeps the current step in view.

<Demo name="Narrow" />

### Other words

Every text is a property with an English default: `NextLabel`, `CompleteLabel`, `BackLabel`, `SuccessTitle`,
`SuccessDescription` and `CountFormat` on screen; `FormName`, `ProgressNameFormat`, `AnnouncementFormat`,
`CurrentStepText` and `CompletedStepText` for screen readers. In the formats `{0}` is the one-based step and `{1}` the
number of steps; only the `{0}` part of the count rolls when the step changes.

<Demo name="Labels" />

## API reference

<!-- api: MultiStepForm -->

<!-- api: MultiStepFormStep -->

<!-- api: MultiStepFormStepChangedEventArgs -->

<!-- api: MultiStepFormStepValidatingEventArgs -->

<!-- api: MultiStepFormCompletedEventArgs -->

<!-- api: MultiStepFormCompleteFailedEventArgs -->

## Accessibility

- After every step change the focus moves to the new step's heading, and after the last step to the success title. Both
  take the focus from code only (they are not tab stops and draw no ring), so a screen reader starts reading at the right
  place and the next Tab goes to the first field. The focus only moves when it was inside the form, so a step change made
  from elsewhere (a `CurrentIndex` binding) never takes it.
- Back turns disabled on the first step only after the focus has moved, so the focus is never dropped.
- The card is a form landmark named by `FormName`. The progress header is a navigation landmark and the list a list, both
  named by `ProgressNameFormat` ("Progress, step 1 of 2"); each entry is named by its step's title and reports
  `CurrentStepText` or `CompletedStepText` as its status. The visible count is hidden from screen readers.
- Each step is a group named by its title. A step change is announced politely with `AnnouncementFormat` ("Step 2 of 2");
  finishing announces the success title and line.
- The submit button is named by its current label; while a check or the final submission runs it keeps the focus and
  ignores presses.
- Keyboard focus rings are 2 px, 2 px outside the buttons; a pointer press draws none.

### Keyboard

| Keys | Action |
| --- | --- |
| Enter | In a single-line text field of the step: submits the step (continues, or finishes on the last step). A field that uses Enter itself (an open combobox list with a highlighted option, a multi-line text area) keeps it. |
| Enter / Space | On Back or the submit button: presses it. |
| Tab / Shift+Tab | Moves through the step's fields, then Back (skipped while disabled) and the submit button. |

## Motion

- **Changing step**: the new step slides 20 px in from the direction of travel (from the right going forward, from the
  left going back) on a critically damped spring of about 0.4 s while it fades in over 240 ms; the old step slides 20 px
  the other way and fades out in 120 ms. Both start together. Going back in the middle of a change turns the old step
  around from where it is, keeping its speed.
- **Height**: the step area springs to the new step's height on the same spring, growing for a taller step and shrinking
  for a shorter one (never below its minimum). Content is clipped top and bottom only while the height moves.
- **Progress**: a step that is done swaps its number for a check that grows from 0.6 on a short critically damped spring
  and then draws itself in 320 ms; going back pops the number in again out of a slight blur. Marker and label colors move
  over 240 ms.
- **Count and label**: the step number rolls 0.3em upward going forward and downward going back, rising out of a soft
  blur over 240 ms while the old number leaves in 120 ms. The submit label swaps the same way (always upward) while the
  button's width springs to the new label.
- **Finishing**: the last step slides out, the buttons fade out in 120 ms while their row folds away on the spring, and
  the success block rises 8 px as it fades in; its check draws from 80 ms, the title rises out of a blur from 100 ms and
  the line from 140 ms.
- **Buttons**: hover colors move over 160 ms; a press scales the button to 0.98 in 100 ms and a release springs it back.
- Reduced motion and Off change steps, heights, labels, markers and the success block at once and do not scale presses;
  the focus and the announcements are the same.

## Related

- `Stepper` for a progress indicator on its own.
- `Input`, `Combobox`, `Textarea` and `RadioCards`, the fields used in the examples.
- The other Auth blocks: Login, OTP input and Onboarding illustration.
