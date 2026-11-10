using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The phases of one training step in a <see cref="ModelTraining"/> story.</summary>
public enum ModelTrainingPhase
{
    /// <summary>Nothing has happened yet.</summary>
    Ready,

    /// <summary>A batch of examples is drawn from the data.</summary>
    Batch,

    /// <summary>The batch flows forward through the layers to a prediction.</summary>
    Forward,

    /// <summary>The prediction is compared with the right answer, giving the loss.</summary>
    Loss,

    /// <summary>The loss flows backward, giving every weight its gradient.</summary>
    Backward,

    /// <summary>Each weight moves a small step against its gradient.</summary>
    Update,

    /// <summary>The run has ended.</summary>
    Done
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

    /// <summary>The step that finished, from 1.</summary>
    public int Iteration { get; }

    /// <summary>That step's loss.</summary>
    public double Loss { get; }
}

/// <summary>
/// Every word a <see cref="ModelTraining"/> shows or announces, in English by default (the playback buttons are named by
/// <see cref="Explainer.PlaybackLabels"/>). Replace the instance to translate or reword; texts with numbers are
/// functions so the word order can change.
/// </summary>
public sealed class ModelTrainingLabels
{
    /// <summary>The status before the run starts.</summary>
    public string Ready { get; set; } = "Ready";

    /// <summary>The status while the run plays or steps.</summary>
    public string Training { get; set; } = "Training";

    /// <summary>The status while paused.</summary>
    public string Paused { get; set; } = "Paused";

    /// <summary>The status once the run has ended.</summary>
    public string Trained { get; set; } = "Trained";

    /// <summary>The words before the speed control, also its accessible name.</summary>
    public string Speed { get; set; } = "Speed";

    /// <summary>The phase names on the phase rail, in order: batch, forward, loss, backward, update.</summary>
    public string[] PhaseNames { get; set; } = ["Batch", "Forward", "Loss", "Backward", "Update"];

    /// <summary>The data column's heading.</summary>
    public string Data { get; set; } = "Data";

    /// <summary>The network's heading.</summary>
    public string Model { get; set; } = "Model";

    /// <summary>The loss chart's heading.</summary>
    public string LossCurve { get; set; } = "Loss";

    /// <summary>The prediction bars' heading.</summary>
    public string Prediction { get; set; } = "Prediction";

    /// <summary>The mark on the bar of the right answer.</summary>
    public string Target { get; set; } = "target";

    /// <summary>The step readout; the arguments are the step and the number of steps.</summary>
    public Func<int, int, string> StepReadout { get; set; } = (step, count) => $"Step {step} / {count}";

    /// <summary>The learning rate readout; the argument is the rate.</summary>
    public Func<double, string> RateReadout { get; set; } = rate => $"Learning rate {rate:0.###}";

    /// <summary>A batch card's label; the argument is the batch number.</summary>
    public Func<int, string> BatchName { get; set; } = batch => $"Batch {batch}";

    /// <summary>The caption before the run starts; the argument is the number of steps.</summary>
    public Func<int, string> ReadyCaption { get; set; } =
        steps => $"How a model learns: {steps} steps of predict, measure the error, and nudge every weight.";

    /// <summary>The batch caption; the argument is the batch size.</summary>
    public Func<int, string> BatchCaption { get; set; } =
        size => $"A batch of {size} examples is drawn from the training data. Each step learns from one batch.";

    /// <summary>The forward pass caption.</summary>
    public string ForwardCaption { get; set; } =
        "Forward pass: the examples flow through the layers. Every connection multiplies by its weight, giving a prediction.";

    /// <summary>The loss caption; the arguments are the probability given to the right answer and the loss.</summary>
    public Func<double, double, string> LossCaption { get; set; } =
        (p, loss) => $"The model gave the right answer {p:P0}. The loss, −ln of that, is {loss:0.00}: the lower, the better.";

    /// <summary>The backward pass caption.</summary>
    public string BackwardCaption { get; set; } =
        "Backward pass: the loss flows back through the layers, and each weight learns which way would have lowered it.";

    /// <summary>The update caption; the argument is the learning rate.</summary>
    public Func<double, string> UpdateCaption { get; set; } =
        rate => $"Update: every weight takes a small step against its gradient, scaled by the learning rate of {rate:0.###}.";

    /// <summary>The caption while later steps repeat the loop; the arguments are the step and the number of steps.</summary>
    public Func<int, int, string> RepeatCaption { get; set; } =
        (step, count) => $"Step {step} of {count}: the same loop again with a new batch. Watch the loss curve fall.";

    /// <summary>The caption at the end; the arguments are the first and the last loss.</summary>
    public Func<double, double, string> DoneCaption { get; set; } =
        (first, last) => $"Training brought the loss from {first:0.00} down to {last:0.00}. Real runs repeat this loop many thousands of times.";
}
