---
title: Progress
summary: Show how much of a known task is complete.
---

## When to use

- Uploads, imports and other long tasks where the total amount of work is known.
- A percentage that a person should be able to read at a glance, with `IsValueVisible` on.

## When not to use

- Loading with no measurable progress: use `Skeleton` to reserve the space.
- A quota measured against a limit: use `UsageMeter`.
- A dashboard metric: use `Gauge`.
- A position in a multi-step flow: use `Stepper`.

## Usage

Set `Value` and, when the total is not 100, `Maximum`. The bar is read-only and cannot take focus.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Progress Label="Uploading 4 files"
                  IsValueVisible="True"
                  Value="35" />
</UserControl>
```

### Bar only

Without a label or a value the bar stands alone. Its accessible name is "Progress".

```xml
<arc:Progress Value="62" />
```

### Custom maximum

`Percentage` is `round(Value / Maximum × 100)`, so 1,392 of 2,400 reads as 58%.

```xml
<arc:Progress Label="Importing contacts"
              IsValueVisible="True"
              Maximum="2400"
              Value="1392" />
```

### Changing label

Changing `Label` rises the new text in from a soft blur while the old text lifts away.

```xml
<arc:Progress Label="{Binding PhaseName}"
              Value="{Binding PhaseProgress}" />
```

### Completion

At 100% the fill turns to the success color and a check appears beside the count. Style the state with the
`:complete` pseudo-class.

```csharp
upload.Value = 100;
// upload.IsComplete is now true and the check is shown when IsValueVisible is on.
```

Progress does not announce completion by itself. Show a notice, for example with a toast, when the task ends.

## API reference

<!-- api: Progress -->

## Accessibility

- `Label` is the accessible name. Without a label the name is "Progress".
- The control exposes the value as a percentage, so a screen reader reads the current percentage.
- It is not a live region. Announce completion separately when the task ends.
- It is not focusable and has no keyboard interaction.

## Motion

- One smooth spring moves the fill and the counted percentage together, so the number always matches the bar.
- At 100% the fill changes to the success color after a short delay, and the check settles in with a small scale and blur.
- A changed label rises in from a soft blur while the old label fades out.
- Reduced motion jumps the fill and the count straight to the new value, and swaps the label with a short fade.

## Related

- `Skeleton` when the amount of work is unknown and the content is still loading.
- `UsageMeter` when a value fills an allowance with a limit.
- `Gauge` for a single metric on a dashboard.
- `Stepper` when the task is a sequence of named steps.
