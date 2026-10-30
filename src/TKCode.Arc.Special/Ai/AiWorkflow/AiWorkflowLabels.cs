namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AiWorkflow"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; the sentences that carry a value are format functions.
/// </summary>
public sealed class AiWorkflowLabels
{
    /// <summary>The English defaults.</summary>
    public static AiWorkflowLabels Default { get; } = new();

    /// <summary>The button that starts a test run.</summary>
    public string TestRun { get; init; } = "Test run";

    /// <summary>The same button while a run is in progress.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The button on a failed step.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The button that opens the add step menu.</summary>
    public string AddStep { get; init; } = "Add step";

    /// <summary>The add step menu's search field.</summary>
    public string SearchPlaceholder { get; init; } = "Search steps";

    /// <summary>The add step menu when nothing matches the search.</summary>
    public string NoMatches { get; init; } = "No steps match";

    /// <summary>The name of the add step menu's list.</summary>
    public string MenuName { get; init; } = "Steps you can add";

    /// <summary>The line over the empty state's triggers.</summary>
    public string ChooseTrigger { get; init; } = "Choose a trigger to start the flow";

    /// <summary>The empty state when the catalog has no triggers.</summary>
    public string NoSteps { get; init; } = "No steps yet";

    /// <summary>The name of a step's remove button ("Remove Create issue").</summary>
    public Func<string, string> Remove { get; init; } = title => "Remove " + title;

    /// <summary>The status of a running step.</summary>
    public string Running { get; init; } = "Running";

    /// <summary>The status of a failed step.</summary>
    public string Failed { get; init; } = "Failed";

    /// <summary>The status of a step on a path the run did not take.</summary>
    public string Skipped { get; init; } = "Skipped";

    /// <summary>A duration: "700ms", "1.2s", "1m 05s".</summary>
    public Func<TimeSpan, string> Duration { get; init; } = AiWorkflowFormat.Duration;

    /// <summary>The status of a finished step read to screen readers ("Done in 700ms").</summary>
    public Func<TimeSpan?, string> DoneSpoken { get; init; } = d => d is { } t ? "Done in " + AiWorkflowFormat.Duration(t) : "Done";

    /// <summary>The status of a step that has not run, read to screen readers.</summary>
    public string NotRun { get; init; } = "Not run";

    /// <summary>A step's accessible name: title, app and status ("Create issue, Tracker, Done in 700ms").</summary>
    public Func<string, string?, string, string> StepName { get; init; } =
        (title, app, status) => string.IsNullOrEmpty(app) ? title + ", " + status : title + ", " + app + ", " + status;

    /// <summary>The small note in the header when no <see cref="AiWorkflow.RunStep"/> is wired.</summary>
    public string Simulated { get; init; } = "Simulated";

    /// <summary>The footer while nothing has run.</summary>
    public string Ready { get; init; } = "Ready to test";

    /// <summary>The footer after a passed run ("Passed in 3.4s").</summary>
    public Func<TimeSpan, string> Passed { get; init; } = d => "Passed in " + AiWorkflowFormat.Duration(d);

    /// <summary>The footer after a failed run ("Failed at Create issue").</summary>
    public Func<string, string> FailedAt { get; init; } = title => "Failed at " + title;

    /// <summary>The footer while a run is in progress.</summary>
    public string Testing { get; init; } = "Testing";

    /// <summary>The footer after Stop.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The route output's line ("Took the Urgent path").</summary>
    public Func<string, string> Route { get; init; } = path => "Took the " + path + " path";

    /// <summary>The name of the branch's path tabs.</summary>
    public string Paths { get; init; } = "Paths";

    /// <summary>The name of a path's list of steps ("Urgent path").</summary>
    public Func<string, string> PathName { get; init; } = path => path + " path";

    /// <summary>The name of the list of steps.</summary>
    public string StepsName { get; init; } = "Steps";

    /// <summary>The name of the card while it loads.</summary>
    public string Loading { get; init; } = "Loading workflow";

    /// <summary>Said when a run starts.</summary>
    public string AnnounceStarted { get; init; } = "Test run started";

    /// <summary>Said when a step finishes ("Classify done in 1.2s. Urgent").</summary>
    public Func<string, TimeSpan, string?, string> AnnounceStepDone { get; init; } = (title, d, output) =>
        title + " done in " + AiWorkflowFormat.Duration(d) + (string.IsNullOrEmpty(output) ? "." : ". " + output);

    /// <summary>Said when a step fails ("Create issue failed: Token expired").</summary>
    public Func<string, string, string> AnnounceStepFailed { get; init; } = (title, error) => title + " failed: " + error;

    /// <summary>Said when a run passes ("Test run passed in 3.4s").</summary>
    public Func<TimeSpan, string> AnnouncePassed { get; init; } = d => "Test run passed in " + AiWorkflowFormat.Duration(d) + ".";

    /// <summary>Said when a run is stopped.</summary>
    public string AnnounceStopped { get; init; } = "Test run stopped.";

    /// <summary>Added to the outcome when no <see cref="AiWorkflow.RunStep"/> is wired.</summary>
    public string AnnounceSimulated { get; init; } = "Simulated, nothing was sent.";

    /// <summary>Said when a step is added ("Added Create issue").</summary>
    public Func<string, string> AnnounceAdded { get; init; } = title => "Added " + title;

    /// <summary>Said when a step is removed ("Removed Create issue").</summary>
    public Func<string, string> AnnounceRemoved { get; init; } = title => "Removed " + title;
}
