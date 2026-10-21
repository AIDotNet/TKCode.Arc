---
title: Stepper
summary: Show where a person is in a multi-step flow and what is done.
---

<Demo name="Checkout" />

## When to use

- A strict multi-step flow such as checkout or account setup, where the person moves forward one step at a time.
- A vertical timeline of steps with a description for each, and an error state on a failed step.

## When not to use

- A single percentage of finished work: use `Progress`.
- Steps that are not ordered or that a person can jump between freely: use `Tabs`.
- A step's content: `Stepper` only shows the position. Render the step content yourself and drive `CurrentIndex`.

## Usage

Give `Stepper` its steps and the index of the step in progress. An index equal to the step count marks every
step complete.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Stepper CurrentIndex="1">
        <arc:Stepper.Steps>
            <arc:StepperStep Label="Account" Description="Create your login" />
            <arc:StepperStep Label="Shipping" Description="Where should we send it" />
            <arc:StepperStep Label="Payment" Description="Card or invoice" />
            <arc:StepperStep Label="Review" Description="Check and confirm" />
        </arc:Stepper.Steps>
    </arc:Stepper>
</UserControl>
```

### Bound steps

`ItemsSource` takes precedence over `Steps`. Bind a collection of `StepperStep` objects and keep `CurrentIndex`
in the view model.

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="{Binding CurrentStep, Mode=TwoWay}" />
```

### Navigable steps

Set `IsNavigable` to let people return to completed steps by click, or with the arrow keys and Enter. Handle
`StepSelected` to react. If the event is not handled, the chosen step becomes the current step.

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="{Binding CurrentStep, Mode=TwoWay}"
             IsNavigable="True" />
```

```csharp
stepper.StepSelected += (_, e) =>
{
    // e.Index is the completed step that was chosen; e.Step is its StepperStep.
    SaveDraft();
};
```

### Vertical with error

`Orientation` set to `Vertical` gives a timeline. Set `Error` on a step to turn its marker into an alert; the
message replaces its description.

<Demo name="VerticalWithError" />

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="2"
             Orientation="Vertical"
             Width="320" />
```

### Compact and current details

`IsCompact` shows markers only, with the current step named underneath. A horizontal stepper narrower than 480 px
switches to this mode by itself. `Details` set to `Current` shows the description of the current step only.

<Demo name="Compact" />

<Demo name="CurrentDetails" />

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="2"
             IsCompact="True" />
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="1"
             Details="Current" />
```

## API reference

<!-- api: Stepper -->

<!-- api: StepperStep -->

## Accessibility

- `Label` names the stepper. Each step is named with its status: Completed, Not started or Error.
- The current step is marked as the current step.
- Changes of step are announced politely, for example "Step 2 of 4: Shipping". When every step is complete,
  `CompleteLabel` is announced.
- Completed steps are focusable and can be chosen with Enter or Space when `IsNavigable` is on. Arrow keys move
  between reachable steps, Home and End go to the first step or the current step.
- Steps that are not reached yet are not focusable and are skipped by Tab.

## Motion

- When progress jumps over several steps, the connectors fill one after another, and the new current marker gets a halo that grows in.
- A number turns into a drawn check when a step completes, or into an alert mark when it fails.
- A changed description rises in from a soft blur, and the space it occupies springs to its new height.
- Reduced motion applies every change at once.

## Related

- `Progress` when the work has a percentage rather than steps.
- `Tabs` when the person can switch between sections in any order.
- `Skeleton` when the content of a step is still loading.
