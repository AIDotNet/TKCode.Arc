using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="TrainingRun"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; counts, steps and times are format functions.
/// </summary>
public sealed class TrainingRunLabels
{
    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    /// <summary>The English defaults.</summary>
    public static TrainingRunLabels Default { get; } = new();

    /// <summary>The title when no job name is given.</summary>
    public string DefaultName { get; init; } = "Fine-tuning job";

    /// <summary>The status while queued.</summary>
    public string Queued { get; init; } = "Queued";

    /// <summary>The status while running with no loss samples yet.</summary>
    public string Starting { get; init; } = "Starting";

    /// <summary>The status while training.</summary>
    public string Training { get; init; } = "Training";

    /// <summary>The status while paused.</summary>
    public string Paused { get; init; } = "Paused";

    /// <summary>The status once the run completed.</summary>
    public string Completed { get; init; } = "Completed";

    /// <summary>The status after a failure.</summary>
    public string Failed { get; init; } = "Failed";

    /// <summary>The status after a cancel.</summary>
    public string Cancelled { get; init; } = "Cancelled";

    /// <summary>What screen readers hear once a deploy has finished.</summary>
    public string Deployed { get; init; } = "Deployed";

    /// <summary>The line over the empty chart while queued, when no activity is given.</summary>
    public string WaitingInQueue { get; init; } = "Waiting in queue";

    /// <summary>The line over the empty chart while starting, when no activity is given.</summary>
    public string StartingUp { get; init; } = "Starting up";

    /// <summary>The legend entry of the train loss.</summary>
    public string Train { get; init; } = "Train";

    /// <summary>The legend entry of the validation loss.</summary>
    public string Validation { get; init; } = "Validation";

    /// <summary>The validation legend entry on a narrow card (under 440px).</summary>
    public string ValidationShort { get; init; } = "Val";

    /// <summary>The step stat.</summary>
    public string Step { get; init; } = "Step";

    /// <summary>The learning rate stat.</summary>
    public string LearningRate { get; init; } = "Learning rate";

    /// <summary>The elapsed time stat.</summary>
    public string Elapsed { get; init; } = "Elapsed";

    /// <summary>The time left stat.</summary>
    public string Eta { get; init; } = "ETA";

    /// <summary>The word before the ETA while queued ("About 18m 40s").</summary>
    public string About { get; init; } = "About";

    /// <summary>A value that is not known yet.</summary>
    public string Missing { get; init; } = "–";

    /// <summary>The units after the hours, minutes and seconds of a time ("12m 4s").</summary>
    public string HoursUnit { get; init; } = "h";

    /// <summary>The minutes unit.</summary>
    public string MinutesUnit { get; init; } = "m";

    /// <summary>The seconds unit.</summary>
    public string SecondsUnit { get; init; } = "s";

    /// <summary>The label of an epoch under the axis, from its number: "Epoch 2".</summary>
    public Func<int, string> Epoch { get; init; } = n => string.Create(s_invariant, $"Epoch {n}");

    /// <summary>The epoch readout beside the legend, from the current epoch and the total: "Epoch 2 of 3".</summary>
    public Func<int, int, string> EpochOf { get; init; } = (n, total) => string.Create(s_invariant, $"Epoch {n} of {total}");

    /// <summary>A step in the event log and the inspected step: "step 800".</summary>
    public Func<int, string> StepAt { get; init; } = step => "step " + step.ToString("N0", s_invariant);

    /// <summary>The total after the step counter: " / 2,400".</summary>
    public Func<int, string> StepTotal { get; init; } = total => " / " + total.ToString("N0", s_invariant);

    /// <summary>The dataset size in the footer: "48,210 examples".</summary>
    public Func<int, string> Examples { get; init; } = n => n.ToString("N0", s_invariant) + (n == 1 ? " example" : " examples");

    /// <summary>The Pause button.</summary>
    public string Pause { get; init; } = "Pause";

    /// <summary>The Resume button.</summary>
    public string Resume { get; init; } = "Resume";

    /// <summary>The Cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The Cancel button after one press; a second press within 3 seconds cancels.</summary>
    public string ConfirmCancel { get; init; } = "Confirm cancel";

    /// <summary>What screen readers hear after the first press on Cancel.</summary>
    public string ConfirmCancelNotice { get; init; } = "Press Confirm cancel to stop the run";

    /// <summary>The Retry button after a failure.</summary>
    public string RetryFromCheckpoint { get; init; } = "Retry from checkpoint";

    /// <summary>The Run again button after the run completed, failed or was cancelled.</summary>
    public string RunAgain { get; init; } = "Run again";

    /// <summary>The Deploy button.</summary>
    public string Deploy { get; init; } = "Deploy";

    /// <summary>The Deploy button's name while the deploy runs.</summary>
    public string Deploying { get; init; } = "Deploying";

    /// <summary>The button after a deploy failed.</summary>
    public string RetryDeploy { get; init; } = "Retry deploy";

    /// <summary>The footer after a deploy failed without a message.</summary>
    public string DeployFailed { get; init; } = "Deploy failed";

    /// <summary>The footer once deployed, before the endpoint (or alone when there is none).</summary>
    public string DeployedTo { get; init; } = "Deployed";

    /// <summary>The name of the button that copies the endpoint.</summary>
    public string CopyEndpoint { get; init; } = "Copy endpoint";

    /// <summary>The name of the chart.</summary>
    public string Chart { get; init; } = "Loss chart";

    /// <summary>The chart's name while it has no samples.</summary>
    public string NoSamples { get; init; } = "no loss samples yet";

    /// <summary>The train loss in the chart's name: "train loss 0.812".</summary>
    public Func<string, string> TrainLoss { get; init; } = value => "train loss " + value;

    /// <summary>The validation loss in the chart's name: "validation loss 0.874".</summary>
    public Func<string, string> ValidationLoss { get; init; } = value => "validation loss " + value;

    /// <summary>The name of the stats list.</summary>
    public string Stats { get; init; } = "Run stats";

    /// <summary>The name of the event log.</summary>
    public string EventLog { get; init; } = "Recent events";

    /// <summary>The default results: the final train loss.</summary>
    public string FinalTrainLoss { get; init; } = "Train loss";

    /// <summary>The default results: the final validation loss.</summary>
    public string FinalValidationLoss { get; init; } = "Validation loss";

    /// <summary>The default results: the steps.</summary>
    public string Steps { get; init; } = "Steps";

    /// <summary>The default results: the duration.</summary>
    public string Duration { get; init; } = "Duration";

    /// <summary>The hint under the final train loss, from the first one: "from 2.31".</summary>
    public Func<string, string> From { get; init; } = value => "from " + value;

    /// <summary>The hint under the final validation loss, from the step of the best one: "best at step 2,200".</summary>
    public Func<int, string> BestAt { get; init; } = step => "best at step " + step.ToString("N0", s_invariant);

    /// <summary>The hint under the steps: "3 epochs".</summary>
    public Func<int, string> EpochCount { get; init; } = n => n == 1 ? "1 epoch" : string.Create(s_invariant, $"{n} epochs");
}
