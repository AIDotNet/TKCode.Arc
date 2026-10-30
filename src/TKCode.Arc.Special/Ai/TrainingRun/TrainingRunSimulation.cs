namespace TKCode.Arc;

/// <summary>
/// The fine-tuning job a <see cref="TrainingRun"/> plays on its own while the app sets no <see cref="TrainingRun.Status"/>:
/// its name, base model, details, dataset, the hardware it queues for and trains on, and the endpoint a simulated deploy
/// returns. The card plays it as <see cref="TrainingRun.Scenario"/> says: it queues, starts up and trains over 2,400
/// steps in three epochs with a procedural loss curve, checkpoints, evals and a done event (or an out of memory failure
/// to retry from the last checkpoint), freezing while the card is off screen. Nothing is sent. Set the data before handing
/// the simulation to the card; to play another job, give the card a new simulation.
/// </summary>
public sealed class TrainingRunSimulation
{
    /// <summary>The job or output model name (the card's own <see cref="TrainingRun.JobName"/> wins).</summary>
    public string? JobName { get; set; }

    /// <summary>The base model and its mark (the card's own <see cref="TrainingRun.BaseModel"/> wins).</summary>
    public AgentModel? BaseModel { get; set; }

    /// <summary>Short facts after the model, such as the method and the hardware (the card's own <see cref="TrainingRun.Details"/> win).</summary>
    public IReadOnlyList<string>? Details { get; set; }

    /// <summary>The training data (the card's own <see cref="TrainingRun.Dataset"/> wins).</summary>
    public TrainingRunDataset? Dataset { get; set; }

    /// <summary>The hardware the job waits for and trains on ("8× H200"), named in the queue and start texts; "GPUs" when null.</summary>
    public string? Hardware { get; set; }

    /// <summary>The address a simulated deploy returns; null deploys without one.</summary>
    public string? Endpoint { get; set; }
}
