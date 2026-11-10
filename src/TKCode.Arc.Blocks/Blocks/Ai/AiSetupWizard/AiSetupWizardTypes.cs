using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The steps of an <see cref="AiSetupWizard"/>, in order.</summary>
public enum AiSetupStep
{
    /// <summary>Choose the provider, the model and enter a key.</summary>
    Provider,

    /// <summary>Choose which workspace data the assistant may read.</summary>
    DataAccess,

    /// <summary>Training, masking and history settings.</summary>
    Privacy,

    /// <summary>Run a test against the chosen setup.</summary>
    Test,

    /// <summary>A summary and the finish button.</summary>
    Done
}

/// <summary>An AI provider offered in the first step of an <see cref="AiSetupWizard"/>.</summary>
public sealed record AiSetupProvider
{
    /// <summary>Your identifier of the provider; reported in <see cref="AiSetupConfiguration.ProviderId"/>.</summary>
    public required string Id { get; init; }

    /// <summary>The provider's name.</summary>
    public required string Name { get; init; }

    /// <summary>One line on what sets it apart; null leaves it out.</summary>
    public string? Description { get; init; }

    /// <summary>The models it offers, best default first.</summary>
    public IReadOnlyList<string> Models { get; init; } = [];

    /// <summary>Whether the workspace must enter an API key for it.</summary>
    public bool RequiresKey { get; init; } = true;

    /// <summary>Where to find the key, shown under the key field; null leaves it out.</summary>
    public string? KeyHint { get; init; }
}

/// <summary>A kind of workspace data the assistant may read, offered in the second step.</summary>
public sealed record AiSetupDataSource
{
    /// <summary>Your identifier of the source; reported in <see cref="AiSetupConfiguration.DataSourceIds"/>.</summary>
    public required string Id { get; init; }

    /// <summary>The source's name ("Documents").</summary>
    public required string Name { get; init; }

    /// <summary>What the assistant would read; null leaves it out.</summary>
    public string? Description { get; init; }

    /// <summary>Whether it starts checked.</summary>
    public bool IsEnabledByDefault { get; init; }
}

/// <summary>Everything chosen in an <see cref="AiSetupWizard"/>.</summary>
public sealed record AiSetupConfiguration
{
    /// <summary>The chosen provider's <see cref="AiSetupProvider.Id"/>; null before one is chosen.</summary>
    public string? ProviderId { get; init; }

    /// <summary>The chosen model; null before one is chosen.</summary>
    public string? Model { get; init; }

    /// <summary>The key entered; empty when the provider needs none. Store it as a secret.</summary>
    public string ApiKey { get; init; } = "";

    /// <summary>The ids of the data sources the assistant may read.</summary>
    public IReadOnlyList<string> DataSourceIds { get; init; } = [];

    /// <summary>Whether the workspace's data must stay out of model training.</summary>
    public bool ExcludeFromTraining { get; init; } = true;

    /// <summary>Whether personal information is masked before anything is sent.</summary>
    public bool MaskPersonalInfo { get; init; } = true;

    /// <summary>How many days chat history is kept; 0 keeps none.</summary>
    public int HistoryDays { get; init; } = 30;
}

/// <summary>The result of the connection test.</summary>
/// <param name="IsSuccess">Whether every check passed.</param>
/// <param name="Message">What to tell the person (the reply time on success, what failed otherwise).</param>
public sealed record AiSetupTestResult(bool IsSuccess, string Message);

/// <summary>Arguments of <see cref="AiSetupWizard.StepChanged"/>.</summary>
public sealed class AiSetupStepChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiSetupStepChangedEventArgs(RoutedEvent routedEvent, AiSetupStep oldStep, AiSetupStep newStep) : base(routedEvent)
    {
        OldStep = oldStep;
        NewStep = newStep;
    }

    /// <summary>The step before.</summary>
    public AiSetupStep OldStep { get; }

    /// <summary>The step now shown.</summary>
    public AiSetupStep NewStep { get; }
}

/// <summary>Arguments of <see cref="AiSetupWizard.Completed"/>.</summary>
public sealed class AiSetupCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiSetupCompletedEventArgs(RoutedEvent routedEvent, AiSetupConfiguration configuration) : base(routedEvent)
        => Configuration = configuration;

    /// <summary>What was set up.</summary>
    public AiSetupConfiguration Configuration { get; }
}

/// <summary>
/// Every word an <see cref="AiSetupWizard"/> shows or reads out, with English defaults. Assign a new instance to
/// <see cref="AiSetupWizard.Labels"/>.
/// </summary>
public sealed record AiSetupWizardLabels
{
    /// <summary>The wizard's heading ("Turn on AI for your workspace").</summary>
    public string Title { get; init; } = "Turn on AI for your workspace";

    /// <summary>The names of the five steps, in order.</summary>
    public IReadOnlyList<string> Steps { get; init; } = ["Provider and model", "Data access", "Privacy", "Test", "Done"];

    /// <summary>The short descriptions of the five steps, in order.</summary>
    public IReadOnlyList<string> StepDescriptions { get; init; } =
    [
        "Pick who runs the model",
        "Choose what it can read",
        "Decide what is kept",
        "Make sure it answers",
        "Review and finish",
    ];

    /// <summary>The Back button.</summary>
    public string Back { get; init; } = "Back";

    /// <summary>The Continue button.</summary>
    public string Continue { get; init; } = "Continue";

    /// <summary>The last step's button.</summary>
    public string Finish { get; init; } = "Turn on AI";

    /// <summary>The label of the model menu.</summary>
    public string Model { get; init; } = "Model";

    /// <summary>The label of the key field.</summary>
    public string ApiKey { get; init; } = "API key";

    /// <summary>The error under the providers when none is chosen.</summary>
    public string ProviderRequired { get; init; } = "Choose a provider to continue.";

    /// <summary>The error under the key field when it is empty.</summary>
    public string KeyRequired { get; init; } = "Enter the key to continue.";

    /// <summary>The intro of the data step.</summary>
    public string DataIntro { get; init; } = "The assistant only reads what you allow, and only what each person can already see.";

    /// <summary>The training switch.</summary>
    public string ExcludeFromTraining { get; init; } = "Keep our data out of model training";

    /// <summary>Under the training switch.</summary>
    public string ExcludeFromTrainingDetail { get; init; } = "The provider may not use prompts or answers to improve its models.";

    /// <summary>The masking switch.</summary>
    public string MaskPersonalInfo { get; init; } = "Mask personal information";

    /// <summary>Under the masking switch.</summary>
    public string MaskPersonalInfoDetail { get; init; } = "Names, emails and phone numbers are replaced before anything leaves the workspace.";

    /// <summary>The history setting's label.</summary>
    public string History { get; init; } = "Keep chat history";

    /// <summary>The history choices; the value 0 keeps none.</summary>
    public Func<int, string> HistoryOption { get; init; } = static days => days == 0 ? "Off" : $"{days} days";

    /// <summary>The intro of the test step.</summary>
    public string TestIntro { get; init; } = "Send one short request with these settings before anyone else uses them.";

    /// <summary>The test button.</summary>
    public string RunTest { get; init; } = "Run test";

    /// <summary>The test button after a test.</summary>
    public string RunAgain { get; init; } = "Run again";

    /// <summary>The four checks of the test, in order.</summary>
    public IReadOnlyList<string> TestChecks { get; init; } =
        ["Reach the provider", "Check the key", "Get an answer from the model", "Read one allowed document"];

    /// <summary>The heading of the last step.</summary>
    public string DoneTitle { get; init; } = "Everything is ready";

    /// <summary>The text of the last step.</summary>
    public string DoneText { get; init; } = "Review the setup below. People in the workspace see AI features as soon as you turn it on.";

    /// <summary>The heading shown once finished.</summary>
    public string CompletedTitle { get; init; } = "AI is on";

    /// <summary>The summary's row names: provider, model, data, training, masking, history.</summary>
    public IReadOnlyList<string> SummaryRows { get; init; } = ["Provider", "Model", "Data", "Training", "Masking", "History"];

    /// <summary>Yes and no in the summary.</summary>
    public string On { get; init; } = "On";

    /// <summary>Yes and no in the summary.</summary>
    public string Off { get; init; } = "Off";

    /// <summary>The data row when nothing is allowed.</summary>
    public string NoData { get; init; } = "None";

    /// <summary>The step announcement ("Step 2 of 5: Data access").</summary>
    public Func<int, int, string, string> StepAnnouncement { get; init; } = static (step, count, name) => $"Step {step} of {count}: {name}";
}

/// <summary>Invented sample data for <see cref="AiSetupWizard"/>: its defaults and the simulated test.</summary>
public static class AiSetupWizardSamples
{
    /// <summary>How long each simulated check takes (0.45 s).</summary>
    public static readonly TimeSpan SimulatedCheckTime = TimeSpan.FromMilliseconds(450);

    /// <summary>Three invented providers.</summary>
    public static IReadOnlyList<AiSetupProvider> Providers() =>
    [
        new()
        {
            Id = "northwind", Name = "Northwind AI", Description = "Hosted in the EU, strong at long documents",
            Models = ["Northwind Large 3", "Northwind Swift 3"], KeyHint = "Find it under Settings › Keys in your Northwind console.",
        },
        new()
        {
            Id = "cobalt", Name = "Cobalt Labs", Description = "Fast replies, good value for chat",
            Models = ["Cobalt Atlas 2", "Cobalt Mini 2"], KeyHint = "Keys start with cb_ and are shown once when created.",
        },
        new()
        {
            Id = "inhouse", Name = "Your own server", Description = "Runs on your hardware, no key needed",
            Models = ["Meadow 8B", "Meadow 70B"], RequiresKey = false,
        },
    ];

    /// <summary>Four kinds of workspace data.</summary>
    public static IReadOnlyList<AiSetupDataSource> DataSources() =>
    [
        new() { Id = "docs", Name = "Documents and wikis", Description = "Pages, files and their comments", IsEnabledByDefault = true },
        new() { Id = "tickets", Name = "Support tickets", Description = "Ticket threads and internal notes", IsEnabledByDefault = true },
        new() { Id = "chat", Name = "Team chat", Description = "Public channels only, never direct messages" },
        new() { Id = "crm", Name = "Customer records", Description = "Accounts, contacts and deal notes" },
    ];

    /// <summary>The history choices in days (0 is off).</summary>
    public static IReadOnlyList<int> HistoryChoices() => [0, 30, 90];

    /// <summary>
    /// The simulated test: four checks, one every <see cref="SimulatedCheckTime"/>. It fails at the key check when the
    /// key is "fail" (so the error path can be tried), and otherwise reports a made-up reply time.
    /// </summary>
    public static async Task<AiSetupTestResult> TestAsync(AiSetupConfiguration configuration, IProgress<int> progress,
        Func<TimeSpan, CancellationToken, Task> delay, CancellationToken cancellation)
    {
        for (var i = 0; i < 4; i++)
        {
            await delay(SimulatedCheckTime, cancellation);
            if (i == 1 && string.Equals(configuration.ApiKey.Trim(), "fail", StringComparison.OrdinalIgnoreCase))
            {
                return new AiSetupTestResult(false, "The provider did not accept the key. Check it and try again.");
            }
            progress.Report(i + 1);
        }
        return new AiSetupTestResult(true, "The model answered in 640 ms.");
    }
}
