using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a <see cref="TrainingRun"/> job is.</summary>
public enum TrainingRunStatus
{
    /// <summary>Waiting for hardware: the chart is empty and the ETA reads "About 18m 40s".</summary>
    Queued,

    /// <summary>Training. With no loss samples yet it reads as starting up.</summary>
    Running,

    /// <summary>Held by the person; Resume carries on from the same step.</summary>
    Paused,

    /// <summary>Finished: the results replace the live stats and Deploy is offered.</summary>
    Completed,

    /// <summary>Stopped by an error: the reason shows in the footer and Retry from checkpoint is offered.</summary>
    Failed,

    /// <summary>Cancelled by the person.</summary>
    Cancelled
}

/// <summary>What a <see cref="TrainingRunEvent"/> reports; it picks the row's icon and color.</summary>
public enum TrainingRunEventKind
{
    /// <summary>The run started or resumed.</summary>
    Start,

    /// <summary>A checkpoint was saved.</summary>
    Checkpoint,

    /// <summary>An evaluation finished.</summary>
    Eval,

    /// <summary>An epoch finished.</summary>
    Epoch,

    /// <summary>Neutral information.</summary>
    Info,

    /// <summary>Something worth a look that did not stop the run.</summary>
    Warning,

    /// <summary>An error that stopped the run.</summary>
    Error,

    /// <summary>The run finished.</summary>
    Done
}

/// <summary>How a <see cref="TrainingRun"/> plays its <see cref="TrainingRun.Simulation"/>.</summary>
public enum TrainingRunScenario
{
    /// <summary>A run that trains through its three epochs and completes.</summary>
    Healthy,

    /// <summary>A run that runs out of GPU memory once at step 940, so Retry from checkpoint can be tried.</summary>
    Oom
}

/// <summary>The action buttons a <see cref="TrainingRun"/> may offer. A button also needs the right status.</summary>
[Flags]
public enum TrainingRunActions
{
    /// <summary>No buttons: a read-only monitor.</summary>
    None = 0,

    /// <summary>Pause while running.</summary>
    Pause = 1,

    /// <summary>Resume while paused.</summary>
    Resume = 2,

    /// <summary>Cancel while queued, running or paused (a second press confirms).</summary>
    Cancel = 4,

    /// <summary>Retry from checkpoint after a failure.</summary>
    Retry = 8,

    /// <summary>Run again after the run completed, failed or was cancelled.</summary>
    Restart = 16,

    /// <summary>Deploy after completion (it also needs <see cref="TrainingRun.DeployHandler"/>, or the simulation).</summary>
    Deploy = 32,

    /// <summary>Every button.</summary>
    All = Pause | Resume | Cancel | Retry | Restart | Deploy
}

/// <summary>The training data of a <see cref="TrainingRun"/>, shown in the footer.</summary>
public sealed class TrainingRunDataset
{
    /// <summary>Creates an empty dataset; set <see cref="Name"/> before showing it.</summary>
    public TrainingRunDataset()
    {
    }

    /// <summary>Creates a dataset with a name and, optionally, its size and mark.</summary>
    /// <param name="name">The dataset's name, such as "your-org/support-tickets".</param>
    /// <param name="examples">How many examples it holds, or null to leave the count out.</param>
    /// <param name="mark">A 24 × 24 stroked glyph drawn at 14px, or null for the database glyph.</param>
    public TrainingRunDataset(string name, int? examples = null, Geometry? mark = null)
    {
        Name = name;
        Examples = examples;
        Mark = mark;
    }

    /// <summary>The dataset's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How many examples it holds; null leaves the count out.</summary>
    public int? Examples { get; set; }

    /// <summary>A 24 × 24 stroked glyph drawn at 14px before the name; null draws the database glyph.</summary>
    public Geometry? Mark { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>One loss sample of a <see cref="TrainingRun"/>: the optimizer step and the train and validation loss at it.</summary>
public sealed class TrainingRunLossPoint
{
    /// <summary>Creates an empty sample.</summary>
    public TrainingRunLossPoint()
    {
    }

    /// <summary>Creates a sample.</summary>
    /// <param name="step">The optimizer step.</param>
    /// <param name="train">The train loss at that step, or null.</param>
    /// <param name="validation">The validation loss at that step, or null.</param>
    public TrainingRunLossPoint(int step, double? train, double? validation = null)
    {
        Step = step;
        Train = train;
        Validation = validation;
    }

    /// <summary>The optimizer step.</summary>
    public int Step { get; set; }

    /// <summary>The train loss at the step, or null when this sample only carries validation.</summary>
    public double? Train { get; set; }

    /// <summary>The validation loss at the step, or null when no evaluation ran there.</summary>
    public double? Validation { get; set; }
}

/// <summary>One entry of a <see cref="TrainingRun"/>'s event log.</summary>
public sealed class TrainingRunEvent
{
    /// <summary>Creates an empty event.</summary>
    public TrainingRunEvent()
    {
    }

    /// <summary>Creates an event.</summary>
    /// <param name="id">A stable id, unique within the run.</param>
    /// <param name="kind">What it reports.</param>
    /// <param name="label">The text, such as "Checkpoint saved".</param>
    /// <param name="step">The step it happened at, or null.</param>
    public TrainingRunEvent(string id, TrainingRunEventKind kind, string label, int? step = null)
    {
        Id = id;
        Kind = kind;
        Label = label;
        Step = step;
    }

    /// <summary>A stable id, unique within the run; a new id at the top of the log slides the list down.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>What the event reports.</summary>
    public TrainingRunEventKind Kind { get; set; }

    /// <summary>The text of the row.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The step it happened at, shown at the end of the row; null shows none.</summary>
    public int? Step { get; set; }
}

/// <summary>One result a completed <see cref="TrainingRun"/> shows in place of the live stats.</summary>
public sealed class TrainingRunMetric
{
    /// <summary>Creates an empty result.</summary>
    public TrainingRunMetric()
    {
    }

    /// <summary>Creates a result.</summary>
    /// <param name="label">What it measures, such as "Validation loss".</param>
    /// <param name="value">The value as shown, such as "0.468".</param>
    /// <param name="hint">A short note under the value, or null.</param>
    public TrainingRunMetric(string label, string value, string? hint = null)
    {
        Label = label;
        Value = value;
        Hint = hint;
    }

    /// <summary>What the result measures.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The value as shown.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>A short note under the value, or null.</summary>
    public string? Hint { get; set; }
}

/// <summary>
/// One update of a running job for <see cref="TrainingRun.Apply"/> and <see cref="TrainingRun.FollowAsync"/>: every value
/// that is set replaces the card's, a loss sample and an event are appended, and nulls leave the card as it is.
/// </summary>
public sealed class TrainingRunProgress
{
    /// <summary>The new status, or null to keep it.</summary>
    public TrainingRunStatus? Status { get; init; }

    /// <summary>Optimizer steps done, or null to keep them.</summary>
    public int? Step { get; init; }

    /// <summary>A loss sample to append, or null.</summary>
    public TrainingRunLossPoint? Loss { get; init; }

    /// <summary>The current learning rate, or null to keep it.</summary>
    public double? LearningRate { get; init; }

    /// <summary>Seconds of training so far, or null to keep them.</summary>
    public TimeSpan? Elapsed { get; init; }

    /// <summary>Estimated time left, or null to keep it.</summary>
    public TimeSpan? Eta { get; init; }

    /// <summary>An event to append to the log, or null.</summary>
    public TrainingRunEvent? Event { get; init; }

    /// <summary>The line over the empty chart while queued or starting, or null to keep it.</summary>
    public string? Activity { get; init; }

    /// <summary>Why the run failed, or null to keep it.</summary>
    public string? ErrorText { get; init; }
}
