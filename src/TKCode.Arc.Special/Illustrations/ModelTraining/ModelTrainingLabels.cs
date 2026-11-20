using System.Globalization;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="ModelTraining"/> run is (<see cref="ModelTraining.Phase"/>).</summary>
public enum ModelTrainingPhase
{
    /// <summary>Nothing has happened yet.</summary>
    Ready,

    /// <summary>A batch leaves the training data for the input layer.</summary>
    Batch,

    /// <summary>The batch flows forward through the layers to a prediction.</summary>
    Forward,

    /// <summary>The prediction is compared with the right answer, giving the loss.</summary>
    Loss,

    /// <summary>The error flows backward, strengthening the connections it passes.</summary>
    Backward,

    /// <summary>The weights take their step and the loss moves along the curve.</summary>
    Update,

    /// <summary>The run has ended (shown as the finished, evaluated model).</summary>
    Done,

    /// <summary>Batch after batch over the epochs; the loss falls along the curve.</summary>
    Epochs,

    /// <summary>The held-out data runs through the trained model once and is scored against the target.</summary>
    Evaluate
}

/// <summary>Arguments of <see cref="ModelTraining.IterationCompleted"/>.</summary>
public sealed class ModelTrainingIterationEventArgs : RoutedEventArgs
{
    internal ModelTrainingIterationEventArgs(RoutedEvent routedEvent, object source, int iteration, double loss)
        : base(routedEvent, source)
    {
        Iteration = iteration;
        Loss = loss;
    }

    /// <summary>The training step whose weight update finished, from 1.</summary>
    public int Iteration { get; }

    /// <summary>The loss after that update.</summary>
    public double Loss { get; }
}

/// <summary>
/// Every word a <see cref="ModelTraining"/> shows or announces, in English by default (the play button is named by
/// <see cref="Explainer.PlaybackLabels"/>). Replace the instance to translate or reword; texts with numbers are
/// functions so the word order can change.
/// </summary>
public sealed class ModelTrainingLabels
{
    /// <summary>The names of the five steps in the step bar, in order: batch, forward pass, backward pass, epochs, evaluate.</summary>
    public string[] Steps { get; set; } = ["Batch", "Forward pass", "Backward pass", "Epochs", "Evaluate"];

    /// <summary>The step bar's accessible name.</summary>
    public string StepsName { get; set; } = "Training steps";

    /// <summary>A step button's accessible name; the arguments are the step (from 1), the number of steps and its name.</summary>
    public Func<int, int, string, string> StepName { get; set; } = (step, count, name) => $"Step {step} of {count}: {name}";

    /// <summary>The training data stack's name.</summary>
    public string TrainingSet { get; set; } = "Training data";

    /// <summary>The held-out data stack's name.</summary>
    public string HeldOutSet { get; set; } = "Holdout data";

    /// <summary>What one sample is; it follows every sample count ("12,800 photos").</summary>
    public string Unit { get; set; } = "photos";

    /// <summary>A sample count under a stack; the arguments are the count and <see cref="Unit"/>.</summary>
    public Func<int, string, string> SampleCount { get; set; } =
        (count, unit) => string.Create(CultureInfo.InvariantCulture, $"{count:N0} {unit}");

    /// <summary>The input layer's name.</summary>
    public string Input { get; set; } = "Input";

    /// <summary>A hidden layer's name.</summary>
    public string Hidden { get; set; } = "Hidden";

    /// <summary>The output layer's name.</summary>
    public string Output { get; set; } = "Output";

    /// <summary>The loss readout's label.</summary>
    public string LossCurve { get; set; } = "Loss";

    /// <summary>The epoch readout's label.</summary>
    public string Epoch { get; set; } = "Epoch";

    /// <summary>The line under the loss before the first batch has been measured.</summary>
    public string NoBatch { get; set; } = "No batch yet";

    /// <summary>The line under the loss once training runs; the argument is the first loss.</summary>
    public Func<double, string> StartedAt { get; set; } = loss => string.Create(CultureInfo.InvariantCulture, $"From {loss:0.00}");

    /// <summary>The line under the epoch; the argument is the number of epochs.</summary>
    public Func<int, string> EpochOf { get; set; } = epochs => string.Create(CultureInfo.InvariantCulture, $"of {epochs}");

    /// <summary>The validation point's label on the loss chart; the argument is its loss.</summary>
    public Func<double, string> Validation { get; set; } = loss => string.Create(CultureInfo.InvariantCulture, $"Validation {loss:0.00}");

    /// <summary>The evaluation row's name.</summary>
    public string Evaluation { get; set; } = "Evaluation";

    /// <summary>The line under the evaluation; the argument is the target accuracy in percent.</summary>
    public Func<double, string> TargetReadout { get; set; } = target => string.Create(CultureInfo.InvariantCulture, $"Target {target:0.#}%");

    /// <summary>The evaluation tag before the held-out data runs.</summary>
    public string Pending { get; set; } = "Pending";

    /// <summary>The evaluation tag while the held-out data runs.</summary>
    public string Scoring { get; set; } = "Scoring";

    /// <summary>The evaluation tag when the score reaches the target.</summary>
    public string Passed { get; set; } = "Passed";

    /// <summary>The evaluation tag when the score stays under the target.</summary>
    public string BelowTarget { get; set; } = "Below target";

    /// <summary>The hover detail of the training data; the arguments are the samples, <see cref="Unit"/> and the batch size.</summary>
    public Func<int, string, int, string> TrainingDetail { get; set; } =
        (samples, unit, batch) => string.Create(CultureInfo.InvariantCulture, $"{samples:N0} {unit}, {batch} per batch");

    /// <summary>The hover detail of the held-out data; the arguments are its samples and <see cref="Unit"/>.</summary>
    public Func<int, string, string> HeldOutDetail { get; set; } =
        (samples, unit) => string.Create(CultureInfo.InvariantCulture, $"{samples:N0} {unit} kept out of training");

    /// <summary>The hover detail of the input layer; the argument is its units.</summary>
    public Func<int, string> InputDetail { get; set; } = units => $"{units} features in each sample";

    /// <summary>The hover detail of a hidden layer; the arguments are its units and the weights coming into it.</summary>
    public Func<int, int, string> HiddenDetail { get; set; } = (units, weights) => $"{units} units, {weights} weights in, ReLU";

    /// <summary>The hover detail of the output layer; the arguments are the two classes.</summary>
    public Func<string, string, string> OutputDetail { get; set; } = (first, second) => $"Softmax: {first} or {second}";

    /// <summary>The batch step's caption; the argument is the batch size.</summary>
    public Func<int, string> BatchCaption { get; set; } =
        size => $"A batch of {size} examples is drawn from the training data. Each step learns from one batch.";

    /// <summary>The forward pass caption.</summary>
    public string ForwardCaption { get; set; } =
        "Forward pass: the examples flow through the layers. Every connection multiplies by its weight, giving a prediction.";

    /// <summary>The backward pass caption.</summary>
    public string BackwardCaption { get; set; } =
        "Backward pass: the loss flows back through the layers, and each weight learns which way would have lowered it.";

    /// <summary>The epochs step's caption; the arguments are the number of epochs, the first and the last loss.</summary>
    public Func<int, double, double, string> EpochsCaption { get; set; } =
        (epochs, first, last) => string.Create(CultureInfo.InvariantCulture,
            $"The same loop repeats over {epochs} epochs, and the loss drops from {first:0.00} to {last:0.00}.");

    /// <summary>
    /// The evaluate step's caption; the arguments are the accuracy, the target (both in percent) and whether the score
    /// reaches the target.
    /// </summary>
    public Func<double, double, bool, string> EvaluateCaption { get; set; } =
        (accuracy, target, passed) => passed
            ? string.Create(CultureInfo.InvariantCulture, $"Data the model never saw scores {accuracy:0.0}%, clearing the {target:0.#}% bar: the model ships.")
            : string.Create(CultureInfo.InvariantCulture, $"Data the model never saw scores {accuracy:0.0}%, short of the {target:0.#}% bar: back to training.");

    /// <summary>No longer used: use <see cref="Steps"/> (this forwards to it).</summary>
    [Obsolete("Use Steps instead (deprecated in 1.3.0).")]
    public string[] PhaseNames { get => Steps; set => Steps = value; }

    /// <summary>No longer shown: the illustration has no status line.</summary>
    [Obsolete("No longer shown: the illustration has no status line (deprecated in 1.3.0).")]
    public string Ready { get; set; } = "Ready";

    /// <summary>No longer shown: the illustration has no status line.</summary>
    [Obsolete("No longer shown: the illustration has no status line (deprecated in 1.3.0).")]
    public string Training { get; set; } = "Training";

    /// <summary>No longer shown: the illustration has no status line.</summary>
    [Obsolete("No longer shown: the illustration has no status line (deprecated in 1.3.0).")]
    public string Paused { get; set; } = "Paused";

    /// <summary>No longer shown: the illustration has no status line.</summary>
    [Obsolete("No longer shown: the illustration has no status line (deprecated in 1.3.0).")]
    public string Trained { get; set; } = "Trained";

    /// <summary>No longer shown: the illustration has no speed control (set <see cref="Explainer.Speed"/>).</summary>
    [Obsolete("No longer shown: the illustration has no speed control (deprecated in 1.3.0).")]
    public string Speed { get; set; } = "Speed";

    /// <summary>No longer shown: use <see cref="TrainingSet"/> for the data's name.</summary>
    [Obsolete("No longer shown: use TrainingSet for the data's name (deprecated in 1.3.0).")]
    public string Data { get; set; } = "Data";

    /// <summary>No longer shown: the layers are named by <see cref="Input"/>, <see cref="Hidden"/> and <see cref="Output"/>.</summary>
    [Obsolete("No longer shown: the layers are named by Input, Hidden and Output (deprecated in 1.3.0).")]
    public string Model { get; set; } = "Model";

    /// <summary>No longer shown: the class names label the output bars.</summary>
    [Obsolete("No longer shown: the class names label the output bars (deprecated in 1.3.0).")]
    public string Prediction { get; set; } = "Prediction";

    /// <summary>No longer shown: the winning class is marked by its brighter name and bar.</summary>
    [Obsolete("No longer shown: the winning class is marked by its brighter name and bar (deprecated in 1.3.0).")]
    public string Target { get; set; } = "target";

    /// <summary>No longer shown: the epoch readout counts the run (see <see cref="EpochOf"/>).</summary>
    [Obsolete("No longer shown: the epoch readout counts the run (deprecated in 1.3.0).")]
    public Func<int, int, string> StepReadout { get; set; } = (step, count) => $"Step {step} / {count}";

    /// <summary>No longer shown: the illustration has no learning rate readout.</summary>
    [Obsolete("No longer shown: the illustration has no learning rate readout (deprecated in 1.3.0).")]
    public Func<double, string> RateReadout { get; set; } = rate => $"Learning rate {rate:0.###}";

    /// <summary>No longer shown: the batch is drawn as a sheet without a name.</summary>
    [Obsolete("No longer shown: the batch is drawn as a sheet without a name (deprecated in 1.3.0).")]
    public Func<int, string> BatchName { get; set; } = batch => $"Batch {batch}";

    /// <summary>No longer shown: before it plays the illustration shows the finished model and its caption.</summary>
    [Obsolete("No longer shown: before it plays the illustration shows the finished model (deprecated in 1.3.0).")]
    public Func<int, string> ReadyCaption { get; set; } =
        steps => $"How a model learns: {steps} steps of predict, measure the error, and nudge every weight.";

    /// <summary>No longer shown: the loss readout and chart show the loss.</summary>
    [Obsolete("No longer shown: the loss readout and chart show the loss (deprecated in 1.3.0).")]
    public Func<double, double, string> LossCaption { get; set; } =
        (p, loss) => $"The model gave the right answer {p:P0}. The loss, −ln of that, is {loss:0.00}: the lower, the better.";

    /// <summary>No longer shown: the backward pass caption (<see cref="BackwardCaption"/>) covers the update.</summary>
    [Obsolete("No longer shown: BackwardCaption covers the update (deprecated in 1.3.0).")]
    public Func<double, string> UpdateCaption { get; set; } =
        rate => $"Update: every weight takes a small step against its gradient, scaled by the learning rate of {rate:0.###}.";

    /// <summary>No longer shown: use <see cref="EpochsCaption"/>.</summary>
    [Obsolete("No longer shown: use EpochsCaption (deprecated in 1.3.0).")]
    public Func<int, int, string> RepeatCaption { get; set; } =
        (step, count) => $"Step {step} of {count}: the same loop again with a new batch. Watch the loss curve fall.";

    /// <summary>No longer shown: use <see cref="EvaluateCaption"/> (the run ends with the evaluation).</summary>
    [Obsolete("No longer shown: use EvaluateCaption (deprecated in 1.3.0).")]
    public Func<double, double, string> DoneCaption { get; set; } =
        (first, last) => $"Training brought the loss from {first:0.00} down to {last:0.00}. Real runs repeat this loop many thousands of times.";

    /// <summary>The name of step <paramref name="index"/> (0 to 4), falling back to the English name.</summary>
    internal string StepAt(int index)
    {
        var steps = Steps;
        if (steps is not null && index >= 0 && index < steps.Length && !string.IsNullOrEmpty(steps[index]))
        {
            return steps[index];
        }
        return index switch
        {
            0 => "Batch",
            1 => "Forward pass",
            2 => "Backward pass",
            3 => "Epochs",
            _ => "Evaluate"
        };
    }
}
