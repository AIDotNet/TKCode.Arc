namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The fine-tuning job the training run demos play: a LoRA run on 8× H200 against an invented support dataset. The base
/// model keeps a neutral label (which models a vendor lets you fine-tune is not something these demos can state); the job,
/// the dataset and the endpoint are invented. Demo content only.
/// </summary>
internal static class TrainingRunDemoData
{
    /// <summary>The job's name.</summary>
    public const string JobName = "support-v4";

    /// <summary>The base model, by a neutral label.</summary>
    public static AgentModel BaseModel { get; } = new("Base model");

    /// <summary>The method and the hardware after the model.</summary>
    public static IReadOnlyList<string> Details { get; } = ["LoRA r16", "8× H200"];

    /// <summary>The training data: an invented dataset of support tickets.</summary>
    public static TrainingRunDataset Dataset { get; } = new("contoso/support-tickets-oct", 48210, null);

    /// <summary>The hardware the job queues for and trains on.</summary>
    public const string Hardware = "8× H200";

    /// <summary>The address the simulated deploy returns.</summary>
    public const string Endpoint = "https://inference.example.com/v1/support-v4";

    /// <summary>A new simulation of the job (code uses it; the XAML demos spell it out).</summary>
    public static TrainingRunSimulation Simulation() => new()
    {
        JobName = JobName,
        BaseModel = BaseModel,
        Details = Details,
        Dataset = Dataset,
        Hardware = Hardware,
        Endpoint = Endpoint,
    };
}
