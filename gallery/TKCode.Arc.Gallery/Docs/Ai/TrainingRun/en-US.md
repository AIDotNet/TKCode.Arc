---
title: Training run
summary: A fine-tuning job card where a live train and validation loss curve draws across a fixed step axis that doubles as the epoch progress bar, with the learning rate, elapsed time, ETA, recent events, pause and cancel, and results with Deploy.
---

<Demo name="Preview" />

## When to use

`TrainingRun` is one fine-tuning or training job on a card. The loss curve draws live across a step axis that spans the
whole run and doubles as the epoch progress bar, so nothing rescales while it trains. Beside it sit the step, the learning
rate, the elapsed time and the ETA, the newest three events, and Pause and Cancel. When the run completes, the results
replace the live stats and Deploy takes the model to an endpoint.

- **A fine-tuning or training job page** in an AI platform, where people watch the loss, pause, cancel and deploy.
- **An eval or distillation run** that reports a loss curve, checkpoints and a final score.
- **An onboarding step** that trains a custom model on the customer's data and ends in a deploy.

## When not to use

- **A chart with many series and no job lifecycle**: use `LineChart`.
- **A short agent task made of steps** rather than a loss curve: use `AgentStatus`.
- **A list of jobs**: render one compact row per job and open this card for the selected one.

## Usage

Set `Status`, `TotalSteps` and `Epochs` from the job config so the axis is right from the first sample. Then keep `Step`,
`LearningRate`, `Elapsed` and `Eta` current, and append to `Loss` and `Events` as they stream. There is no need to throttle:
the curve eases toward each new sample, so bursts still draw at an even pace.

```csharp
run.TotalSteps = job.TotalSteps;
run.Epochs = job.Epochs;
run.Status = TrainingRunStatus.Running;

// On every report from the job:
run.Loss.Add(new TrainingRunLossPoint(report.Step, report.TrainLoss, report.ValidationLoss));
run.Step = report.Step;
run.LearningRate = report.LearningRate;
run.Elapsed = report.Elapsed;
run.Eta = report.Eta;
```

`Apply` takes one `TrainingRunProgress` with whatever changed, and `FollowAsync` applies a whole stream of them on the UI
thread:

```csharp
await run.FollowAsync(jobs.WatchAsync(job.Id, token), token);
```

With no `Status` and no `Simulation` the card shows a queued job with no data yet: the name "Fine-tuning job", empty
stats and an empty chart. With no `Status` it plays its `Simulation` instead, which is handy while you wire up a real
job API: a `TrainingRunSimulation` names the job (`JobName`, `BaseModel`, `Details`, `Dataset`), the `Hardware` it
waits for and trains on, and the `Endpoint` a simulated deploy returns. The card queues it, starts it up and trains it
over 2,400 steps in three epochs with a procedural loss curve, checkpoints and evals. `Scenario="Oom"` makes it fail
once at step 940 so the retry path can be seen. The simulation freezes while the card is off screen, your own `Status`
wins over it, and setting `Simulation` to null stops it.

```csharp
run.Simulation = new TrainingRunSimulation
{
    JobName = "support-v4",
    BaseModel = new AgentModel("Base model"),
    Details = ["LoRA r16", "8× H200"],
    Dataset = new TrainingRunDataset("your-org/support-tickets", 48210),
    Hardware = "8× H200",
    Endpoint = "https://inference.example.com/v1/support-v4",
};
```

The demos on this page take their job from `TrainingRunDemoData`. The base model keeps a neutral label; the job, the
dataset and every loss are invented.

### Anatomy

1. **Head**: the base model's 24px mark (a monogram when `BaseModel` has none), the job name, the model with its
   `Details`, and the status as an icon and a word.
2. **Legend**: the train and validation loss of the latest sample, or of the inspected one, and the current epoch.
3. **Chart**: every other epoch as a faint band, the raw train loss (faint), the smoothed train loss and the validation
   loss with a dot per sample. Under the plot the step axis is the epoch progress bar, one segment per epoch.
4. **Stats**: step, learning rate, elapsed time and ETA on rolling digits. On completion the results take their place.
5. **Events**: the newest three, with an icon per kind and the step.
6. **Footer**: the dataset, the failure reason or the deployed endpoint, and the actions.

### Driven by your app

<Demo name="HostDriven" />

The buttons raise `PauseRequested`, `ResumeRequested`, `CancelRequested`, `RetryRequested` and `RestartRequested`. Act on
your job, then set `Status`. Cancel takes two presses: the first turns it into Confirm cancel for 3 seconds.

`Actions` decides which buttons may show (all by default). A read-only monitor sets `Actions="None"`. Each button also needs
the right status: Pause while running, Resume while paused, Cancel while queued, running or paused, Retry from checkpoint
after a failure, and Run again after the run ends.

### Results and deploy

<Demo name="Finished" />

On completion the results replace the live stats. By default they are the final train and validation loss, the steps and
the duration. `Metrics` sets up to four of your own. Deploy shows when `DeployHandler` is set:

```csharp
run.DeployHandler = async token =>
{
    var deployment = await api.DeployAsync(job.Id, token);
    return deployment.Endpoint;
};
```

The button loads while the task runs and keeps focus. Then the footer shows the endpoint with a copy button. If the task
throws, its message shows with Retry deploy. The token is cancelled when the card unloads or the run leaves Completed.

### Narrow

<Demo name="Narrow" />

The card fills its container up to 640px. Under 440px the padding drops to 16px, the stats go to two columns, the legend
shortens Validation to Val and the footer stacks the dataset over the actions.

### Your own words

`Labels` holds every word the card shows or says. Replace the object to localize or reword:

```csharp
run.Labels = new TrainingRunLabels
{
    Training = "Entrenando",
    Pause = "Pausar",
    StepAt = step => $"paso {step:N0}",
};
```

### Content guidelines

- Name the job after the model it produces, such as `support-v4`.
- Keep `Details` to two or three short facts: the method and the hardware.
- Write event labels as short past-tense facts: "Checkpoint saved", "Loss spike, gradients clipped".
- Give `ErrorText` the cause and the next step, not a stack trace.

### Best practices

- Pass `TotalSteps` and `Epochs` before the first sample so the axis never moves.
- For runs longer than a few thousand samples, downsample `Loss` to about 500 points.
- Remove the samples after a checkpoint when you roll back to it; the curve jumps back instead of easing.

### Common questions

**Why does the y axis not grow when the loss spikes?** It is fixed from the first samples so the curve never rescales
mid-run. A higher value is drawn at the top edge.

**What does the legend show?** The latest smoothed train loss and the latest validation loss, or the values of the sample
you inspect with the pointer or the arrow keys.

## API reference

<!-- api: TrainingRun -->

<!-- api: TrainingRunStatus -->

<!-- api: TrainingRunActions -->

<!-- api: TrainingRunScenario -->

<!-- api: TrainingRunLossPoint -->

<!-- api: TrainingRunEvent -->

<!-- api: TrainingRunEventKind -->

<!-- api: TrainingRunMetric -->

<!-- api: TrainingRunDataset -->

<!-- api: TrainingRunProgress -->

<!-- api: TrainingRunLabels -->

<!-- api: TrainingRunSimulation -->

## Accessibility

- A polite live region announces status changes (Queued, Training, Paused, Completed, Failed, Cancelled, Deployed) and
  `Notice`, never every step.
- The status is an icon and a word, never color alone. A failure reason is text in the footer and in the event log.
- The chart is a labelled group whose name carries the step and both losses. The arrow keys inspect samples and the
  legend shows them.
- Cancel is a two-step action, so a stray press never stops a long run. Deploy keeps focus while it loads.
- The stats are a list of terms with their values. Steps and numbers use tabular figures.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves to the loss chart, then to the footer actions |
| Left, Right | On the chart, steps through samples; the legend shows that step's losses |
| Shift + Left, Shift + Right | Jumps ten samples |
| Home, End | Jumps to the first or the latest sample |
| Escape | Clears the inspected sample |
| Enter, Space | Presses the focused action; Cancel needs a second press on Confirm cancel |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A new sample | The curve and the progress bar ease toward it (exponential, 120ms) | At once | At once |
| A rollback or reset | At once | At once | At once |
| A new event | The list slides down on a bounce-free 0.24s spring, the new row fades in | At once | At once |
| Status and footer text | Old fades out over 100ms, new fades in over 160ms rising 2px | At once | At once |
| Counting values | Digits roll on a spring | At once | At once |
| Spinner and activity shimmer | Turning, shimmering | Still | Still |

The x axis spans the whole run and the y axis is fixed from the first samples, so nothing rescales mid-run. A
simulation keeps running under reduced motion (it is content) but freezes while the card is off screen.

## Related

- `AgentStatus` for the live status of an agent run.
- `ToolCall` for one tool call in an agent transcript.
- `LineChart` for a general multi-series chart.
- `CopyButton` for copying a value with confirmation.
