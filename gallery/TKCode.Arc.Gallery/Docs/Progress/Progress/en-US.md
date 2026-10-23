---
title: Progress
summary: Show how much of a known task is complete.
---

<Demo name="CountedValue" />

## When to use

`Progress` shows how far a task with a known total has come: a file upload, a data import, a batch job or a
month-end close. It sits in the flow of the page, above or beside the content it describes, and it gives the person a
single answer to "how much is left". The optional count and the completion check let the bar carry the number as well
as the shape, so a person can read the state without interpreting the fill.

- **File uploads and exports**: an invoice export or a document upload in a billing or CRM screen, with `Label` naming
  the job ("Uploading 4 files") and `IsValueVisible` showing the percentage. The bar fills as each chunk completes.
- **Data imports with a known row count**: a contacts import of 2,400 rows in a CRM. Set `Maximum` to the row count and
  `Value` to the rows processed, so the bar and the count read in the units the user counts in.
- **Batch jobs and reconciliations**: a month-end ledger close or a nightly reconciliation in a finance console, where
  several jobs run side by side. Each job gets its own bar, and the completed ones show the check.
- **Setup and onboarding checklists with a measurable total**: a workspace setup that has imported 3 of 5 data sources.
  The bar shows the share of the setup that is done, and the list of steps says which ones remain.
- **Long operations in developer tools**: a build, a package sync or a log archive with a measurable number of steps.

## When not to use

- **A load with no measurable progress**: use `Skeleton` to reserve the space of the content that is loading.
- **A quota measured against an allowance**: use `UsageMeter` for storage, seats or API calls against a limit. A
  bar at 100% means the job finished, while a quota at 100% means the allowance is used up and needs a warning.
- **A single metric on a dashboard**: use `Gauge` for a headline number such as availability or on-time delivery.
- **A position in a multi-step flow**: use `Stepper` to show which step is current. A bar implies a total amount of
  work that the steps do not have.
- **A deadline rather than a share of work**: use `Countdown` for the time left until a moment. A bar that counts time
  down reads as work done, which is the opposite of what a deadline shows.

## Usage

Set `Value` and, when the total is not 100, `Maximum`. The bar is read-only and cannot take focus.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Progress Label="Uploading 4 files"
                  IsValueVisible="True"
                  Value="35" />
</UserControl>
```

`Progress` derives from Avalonia's `RangeBase`, so `Value` and `Maximum` are the range properties you already know from
sliders and scroll bars. `Minimum` is treated as 0, and a `Maximum` that is zero or negative counts as 100. The control
clamps `Value` to the range when it computes the percentage, so a value above the maximum reads as 100% and a value
below zero reads as 0%. The control never writes to `Value`: it only shows what it is given, which makes it safe to
bind from a view model that owns the job.

### Anatomy

1. **Label** (`Label`): the visible name of the job, in the small type size and the secondary text color. It sits on
   the left of the row above the track and is also the accessible name. Without a label the row is hidden.
2. **Readout** (`IsValueVisible`): the counted percentage, right-aligned, in the muted text color with tabular figures.
   It sits 12 px after the label. Its box is as wide as "100%", so the check and the digits do not shift as the number
   gains a digit. At 100% a success-colored check appears beside the count.
3. **Track**: a pill 7 px high, including its 1 px border, with the border color, the surface-muted background and
   fully rounded ends. The track clips its fill, so the round end keeps its shape at every value.
4. **Fill**: the accent color from the left edge of the track, sized to the percentage. At 100% it changes to the success
   color.

The meta row (label and readout) appears only when `Label` has text or `IsValueVisible` is on. The gap between the meta
row and the track is 8 px. A bar with neither shows only the track, 7 px high, which suits a table row or a card footer.

### Bar only

Without a label or a value the bar stands alone. Its accessible name is "Progress".

<Demo name="BarOnly" />

Use the bar alone when the surrounding layout already names the job, such as a row in a table whose first column is the
job name, or a card whose heading says "Export". Adding a second label in that case repeats the same words.

### Custom maximum

`Percentage` is `round(Value / Maximum × 100)`, so 1,392 of 2,400 reads as 58%.

<Demo name="CustomMaximum" />

Use `Maximum` when the total is a count that people think in: rows, files, bytes or steps. A count reads more
honestly than a percentage that the person must compute, and it stays correct when the total changes, because the
percentage is recomputed from both numbers. Keep `Value` and `Maximum` in the same unit, and do not mix bytes and
megabytes in one bar.

### Changing label

Changing `Label` rises the new text in from a soft blur while the old text lifts away.

<Demo name="ChangingLabel" />

Use a changing label for a job that moves through named phases, such as "Preparing export", "Compressing archive" and
"Uploading archive". Keep the same bar across the phases and reset `Value` for each phase only when the phase has its own
total. A bar that restarts from zero on every phase tells the person that the whole job has started over.

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

`IsComplete` is read-only. It becomes true when the percentage reaches 100, and the `:complete` pseudo-class follows it.
A job whose count stops at 99 of 100 is not complete, so set `Value` to the full total when the work is done. Progress does not announce completion by itself. Show a notice, for example with a toast, when the task ends.

### Binding to a view model

In an MVVM app the view model owns the job and the bar shows its state. With CommunityToolkit.Mvvm, an
`ObservableObject` holds the counts and a computed percentage; the bar binds to them one way.

```csharp
public partial class ImportViewModel : ObservableObject
{
    [ObservableProperty]
    private int _rowsImported;

    [ObservableProperty]
    private int _rowsTotal = 2400;

    [ObservableProperty]
    private string _phaseName = "Importing contacts";
}
```

```xml
<arc:Progress Label="{Binding PhaseName}"
              IsValueVisible="True"
              Maximum="{Binding RowsTotal}"
              Value="{Binding RowsImported, Mode=OneWay}" />
```

Bind `Value` and `Maximum` one way. The import service sets `RowsImported` as each batch commits, and the binding
carries the change to the bar, so the page does not poll the job. Bind `Label` to the phase
name so a change in phase rises in with the new text. Use a command, such as a `Cancel` button wired to a
`[RelayCommand]`, next to the bar rather than inside it, because the bar takes no input.

### Reporting progress

Update `Value` when a unit of work completes, such as a batch of rows or a file, not on every byte or frame. Each change
starts the fill spring, so a steady stream of tiny updates keeps the bar moving but adds no information. Batches of a few
hundred rows, or one file at a time, are a good unit. When the job has long pauses, such as a server-side step with no feedback, change the `Label` to say what the job is waiting for instead of
letting the bar sit still without an explanation.

### Example: Data import in a CRM

An import screen shows three jobs that run side by side: the export of the invoice history, the contacts import and a
ledger reconciliation. Each job has its own bar and its own label, so a person can see which one is done without
reading a log. The completed job shows the check, the running one shows its count out of the total, and the job that has
just started sits near the start of its track. The copy names the job, so the bar reads without the page heading.

![A data migration panel with one finished job, one import at 58% and one job just started](images/MigrationJobs.png)

```xml
<Border Width="640"
        Padding="28"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="22">
        <StackPanel Spacing="4">
            <TextBlock Text="Data migration"
                       FontSize="18"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Three jobs for the Northwind workspace"
                       FontSize="13"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:Progress Label="Invoice history export"
                      IsValueVisible="True"
                      Value="100" />
        <arc:Progress Label="Contacts import"
                      IsValueVisible="True"
                      Maximum="2400"
                      Value="1392" />
        <arc:Progress Label="Ledger reconciliation"
                      IsValueVisible="True"
                      Value="4" />
    </StackPanel>
</Border>
```

Each bar uses the same width and the same readout, so the column reads as one list. The contacts import uses `Maximum`
because the team counts rows, and the other two use the default 100 because their totals are naturally percentages. A
finished job keeps its row in the list with the check, so the person sees that it is complete rather than wondering
whether it disappeared.

### Content guidelines

The label names the job and the count, so both should read correctly on their own. Name the object of the work, use
sentence case and keep the label short enough to fit on one line beside the readout. Use the present participle while
the job runs and a plain noun when it is done, so the label tells the person what state the job is in.

| Instead of | Write |
|---|---|
| `Progress` | `Uploading 4 files` |
| `Done` | `Invoice history export` (the check and 100% say it is done) |
| `Importing... please wait` | `Importing contacts` |
| `Step 2 of 5 (40%)` | `Ledger reconciliation`, with `Value` set to 40 |
| `Error: upload failed` | `Upload failed. Retry` in a message next to the bar, and leave the bar where it stopped |

Keep the numbers in the readout in digits, and let the bar show the percentage. Do not write the percentage into the
label too, because the person then reads the same number twice.

### Best practices

**Do**

- Show `IsValueVisible` on any job that a person waits for, because the number tells them whether the job is moving.
- Use `Maximum` when the total is a count the person thinks in, because the percentage then stays correct as the total
  changes.
- Leave the bar where it stopped when a job fails, and say why in a message next to it, because a reset bar hides the
  work that was done.
- Keep one bar per job in a list, so each job has its own state, label and check.

**Avoid**

- Showing a bar for work with no measurable total, because a bar that moves at random teaches people to ignore it.
  Use `Skeleton` for content that is loading.
- Resetting `Value` to zero between phases of one job, because the person reads it as the whole job starting over.
- Updating `Value` on every byte or frame, because each change restarts the fill spring and adds no information.
- Treating the check as the only sign of completion in a list. Add a status word or a message when the job's result
  matters, such as "Export ready".

### Common questions

**What happens when `Value` is above `Maximum`?** The percentage is clamped to 100, and `IsComplete` becomes true. The
control does not change `Value` itself, so reset the job in your view model if it should not count as finished.

**Can the bar show an unknown total?** No. The control has no indeterminate state, so a bar with no total is a wrong
answer. Use `Skeleton` while the content loads, or show the job's current phase in a label without a bar.

**How do I hide the label and keep the readout?** Keep `IsValueVisible` on and leave `Label` empty. The meta row then
shows only the readout, right-aligned above the track, and the accessible name becomes "Progress". Give the bar a
heading in the surrounding layout when the name matters.

**How do I know when a job completes?** Subscribe to the view model's job state, or check `IsComplete` after you set
`Value`. The control has no event for completion, so the page announces it: show a toast or a message when the job ends.

## API reference

<!-- api: Progress -->

## Accessibility

- `Label` is the accessible name. Without a label the name is "Progress".
- The control exposes the value as a percentage, so a screen reader reads the current percentage, for example 30%.
- It is a read-only progress bar for assistive technology, and it does not ask for focus.
- It is not a live region. Announce completion separately when the task ends.
- It is not focusable and has no keyboard interaction. Keep the controls that start, pause or cancel the job in the tab
  order, next to the bar, so the person can reach them with the keyboard.
- The percentage is the value that is read aloud, so give the job a name that says what is counted, such as
  "Contacts import", and the number has a meaning on its own.

## Motion

- One smooth spring moves the fill and the counted percentage together, so the number always matches the bar.
- At 100% the fill changes to the success color after a short delay, and the check settles in with a small scale and blur.
- A changed label rises in from a soft blur while the old label fades out.
- Reduced motion jumps the fill and the count straight to the new value, and swaps the label with a short fade.

| Change | Full | Reduced | Off |
|---|---|---|---|
| `Value` changes | The fill and the count move together on one smooth spring | The fill and the count jump to the new value | Immediate |
| Reaching 100% | The fill fades to the success color after 200 ms; the check scales from 0.6 after 0.24 s | The check fades in | Immediate |
| `Label` changes | The new label rises in from 0.3 em with a soft blur; the old one fades | A short fade | Immediate |

### Using motion with restraint

The fill and the count are the only moving parts that matter in a progress bar, and they should move only when the job
moves. Do not loop a pulse or a shimmer on a running bar, because the motion then suggests activity that the job does not
have. A bar that is paused or waiting should stop changing, and the label should say why. Reduced motion needs no extra
work: the control jumps the values and fades the check.

## Related

- `Skeleton` when the amount of work is unknown and the content is still loading.
- `UsageMeter` when a value fills an allowance with a limit.
- `Gauge` for a single metric on a dashboard.
- `Stepper` when the task is a sequence of named steps.
- `Countdown` when the time left matters more than the share of work done.

| Component | Choose it when |
|---|---|
| `Skeleton` | The content is loading and no progress can be measured |
| `UsageMeter` | A value is measured against a limit that people manage |
| `Gauge` | One headline metric is shown on a dashboard |
| `Stepper` | The task is a sequence of named steps with a current step |
| `Countdown` | The remaining time to a fixed moment is the message |
