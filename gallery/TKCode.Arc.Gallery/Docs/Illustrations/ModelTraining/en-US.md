---
title: Model training
summary: A training loop step by step; a batch flows forward to a prediction, the loss is measured and flows back, the weights move and the loss curve falls.
---

<Demo name="TrainingLoop" />

## When to use

`ModelTraining` explains how a model learns, one step of the training loop at a time. A batch of examples is drawn
from the data and flows forward through a small network to a prediction. The prediction is compared with the right
answer: bars show the probability the model gave each class, and the loss is the negative log of the right one, so a
confident right answer costs little and a confident wrong one costs a lot. The loss then flows backward through the
layers, giving each weight its gradient, and every weight takes a small step against it, scaled by the learning rate.
The connections change thickness and color as their weights move, and the loss lands on a curve that falls step by
step. The first step stops at each phase with a caption; later steps repeat the loop faster. The network and the numbers
are illustrative.

- **Courses and onboarding**: a lesson on gradient descent, before the math.
- **Product pages for training platforms**: a visual next to text about fine-tuning or training runs.
- **Talks**: the whole loop told in about twenty seconds with no narration.

## When not to use

- **A real training run**: the card plays its own small story. For a live job use `TrainingRun`, or plot measured
  losses with a `LineChart`.
- **Explaining one architecture**: the network is a generic three-layer sketch, not a transformer or a convolution.
- **Many cards on a page**: each animates while it plays. Keep one per page, or pause the others with `IsPlaying`.

## Usage

The card needs no data: it plays six steps with a sample loss curve that falls from 2.30 and levels off near 0.4.

```xml
<arc:ModelTraining />
```

`ModelTraining` derives from `Explainer`, which supplies the story clock (`IsPlaying`, `Speed`, `IsLooping`,
`Play()`, `Pause()`, `StepForward()`, `Restart()`), start on view, the playback buttons and the polite live region;
this control adds the speed control and the stage.

### One step

| Phase | What happens |
|---|---|
| `Batch` | A batch card slides from the data into the input layer |
| `Forward` | A wave of activations runs from the inputs to the outputs |
| `Loss` | The prediction bars fill; the right class is lit; the loss is −ln of its probability |
| `Backward` | A wave of gradients runs from the outputs back to the inputs |
| `Update` | Every connection moves toward its new weight and glows once; the loss is plotted |

`Phase`, `Iteration` and `CurrentLoss` report where the story is; `IterationCompleted` is raised at the end of each
step with the step and its loss.

### Your own run

`StepCount` (2 to 20) sets the number of steps, `BatchSize` and `LearningRate` are named in the captions and the
readout, and `LossValues` replaces the sample curve. The example below shows a learning rate that is too large: the loss
bounces instead of settling.

<Demo name="OwnLosses" />

```csharp
card.StepCount = 8;
card.LossValues = [2.31, 1.12, 1.64, 0.98, 1.41, 0.92, 1.27, 0.95];
card.IterationCompleted += (_, e) => log.Add($"{e.Iteration}: {e.Loss:0.00}");
```

`Labels` holds every word the card shows or speaks, including the phase names; replace it to translate or reword.

### Reduced motion

Under reduced motion the card opens on its end with the whole curve. Play and Next step still work, and each phase
applies in place without waves.

<Demo name="ReducedMotion" />

## API reference

<!-- api: ModelTraining -->

<!-- api: ModelTrainingLabels -->

<!-- api: ModelTrainingIterationEventArgs -->

## Accessibility

- The card is a group named by `Title` ("Model training"). The status line says Ready, Training, Paused or Trained.
- The stage's accessible name follows the story ("Step 2 / 6, Backward, loss 1.48").
- The live region speaks only when the reader drives the story: the phase captions during the first step, then
  "Step 3 / 6: loss 1.05" for each later step. It stays quiet while the story plays on its own.
- Play, Next step and Restart are buttons with names and tooltips; the speed control is named "Speed".
- The phase rail names every phase in words, and the right class is marked "target" as well as colored.

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Batch | The card slides into the input layer | Appears | Appears |
| Forward and backward passes | Dots run along the connections, nodes light as the wave passes | Not shown | Not shown |
| Prediction bars | Fill over 0.5 s | Fill at once | Fill at once |
| Weight update | Connections ease to their new thickness and glow once | Change at once | Change at once |
| Loss point | The new point grows in | Appears | Appears |
| Caption | Crossfade | Changes in place | Changes in place |

The first step takes about six seconds at speed 1; later steps run at less than half that. Everything runs on the story
clock, so it pauses, steps and scales with `Speed`.

## Responsive behavior

- From 600 px the prediction bars and the loss curve stand in a column on the right; narrower, they sit side by side
  under the network.
- The card is at most 780 px wide; under 560 px its padding tightens and under 420 px the "Speed" words hide.
- Every control is a button, so touch works the same as a pointer.

## Related

| Component | Choose it when |
|---|---|
| `TrainingRun` | You show a real fine-tuning job with its live loss |
| `MixtureOfExperts` | The story is how a model routes tokens to experts |
| `LineChart` | You plot measured losses |
