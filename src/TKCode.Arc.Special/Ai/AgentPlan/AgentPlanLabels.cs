using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AgentPlan"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; texts with numbers or names are functions so that word order and plurals can change. The numbers in
/// <see cref="StepCount"/> and <see cref="Position"/> roll on screen while the words around them stay.
/// </summary>
public sealed class AgentPlanLabels
{
    /// <summary>The bold word that starts the header ("Plan").</summary>
    public string Heading { get; init; } = "Plan";

    /// <summary>What separates <see cref="Heading"/> from the step count.</summary>
    public string HeadingSeparator { get; init; } = " · ";

    /// <summary>The step count after the heading; the argument is the number of steps ("6 steps", "1 step").</summary>
    public Func<int, string> StepCount { get; init; } =
        count => count == 1 ? string.Create(CultureInfo.CurrentCulture, $"{count} step") : string.Create(CultureInfo.CurrentCulture, $"{count} steps");

    /// <summary>The run position on the right of the header; the arguments are the current step (1-based) and the count.</summary>
    public Func<int, int, string> Position { get; init; } =
        (current, count) => string.Create(CultureInfo.CurrentCulture, $"{current} of {count}");

    /// <summary>The plan's accessible name; the arguments are the model's name (null without a model) and the goal.</summary>
    public Func<string?, string?, string> PlanName { get; init; } = (model, goal) =>
    {
        var who = string.IsNullOrWhiteSpace(model) ? "Plan" : model + " plan";
        return string.IsNullOrWhiteSpace(goal) ? who : who + ": " + goal;
    };

    /// <summary>The accessible name of the list of steps.</summary>
    public string StepsName { get; init; } = "Plan steps";

    /// <summary>The accessible name of a step's activity lines.</summary>
    public string ActivityName { get; init; } = "Activity";

    /// <summary>The placeholder of an empty step title.</summary>
    public string TitlePlaceholder { get; init; } = "Describe the step";

    /// <summary>The accessible name of a step's title field; the argument is the step's 1-based position.</summary>
    public Func<int, string> TitleName { get; init; } = position => string.Create(CultureInfo.CurrentCulture, $"Step {position}");

    /// <summary>The accessible name of a step's drag handle; the arguments are its position and title.</summary>
    public Func<int, string, string> MoveName { get; init; } =
        (position, title) => string.Create(CultureInfo.CurrentCulture, $"Move step {position}: {title}");

    /// <summary>The accessible name of a step's remove button; the arguments are its position and title.</summary>
    public Func<int, string, string> RemoveName { get; init; } =
        (position, title) => string.Create(CultureInfo.CurrentCulture, $"Remove step {position}: {title}");

    /// <summary>The hidden hint that describes every title field, drag handle and remove button.</summary>
    public string EditHint { get; init; } =
        "Hold Alt and press the up or down arrow to move a step, or use the arrows on its handle. Escape undoes a rename.";

    /// <summary>The sentence shown while the draft has no steps.</summary>
    public string EmptyPlan { get; init; } = "Nothing is planned yet. Add a step, or let the agent keep planning.";

    /// <summary>The button under the steps that adds an empty one.</summary>
    public string AddStep { get; init; } = "Add step";

    /// <summary>The button that asks the agent for one more step.</summary>
    public string KeepPlanning { get; init; } = "Keep planning";

    /// <summary>The button that approves the draft and starts the run.</summary>
    public string Approve { get; init; } = "Approve plan";

    /// <summary>The button that pauses the run.</summary>
    public string Pause { get; init; } = "Pause";

    /// <summary>The button that resumes a paused run.</summary>
    public string Resume { get; init; } = "Resume";

    /// <summary>The button that runs a failed step again.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The button that leaves a failed step.</summary>
    public string Skip { get; init; } = "Skip";

    /// <summary>The button that goes back to the draft once the plan is done.</summary>
    public string StartOver { get; init; } = "Start over";

    /// <summary>The note of a suggested step (<see cref="AgentPlanNoteKind.Suggested"/>).</summary>
    public string Suggested { get; init; } = "Suggested";

    /// <summary>The note of a step the agent inserted while running (<see cref="AgentPlanNoteKind.AddedByAgent"/>).</summary>
    public string AddedByAgent { get; init; } = "Added by the agent";

    /// <summary>The word before a failed step's reason.</summary>
    public string FailedPrefix { get; init; } = "Failed";

    /// <summary>The word on the right of a skipped step.</summary>
    public string SkippedTag { get; init; } = "Skipped";

    /// <summary>The state words read before each title while the plan runs.</summary>
    public string StatePending { get; init; } = "Pending";

    /// <inheritdoc cref="StatePending"/>
    public string StateActive { get; init; } = "In progress";

    /// <inheritdoc cref="StatePending"/>
    public string StateDone { get; init; } = "Done";

    /// <inheritdoc cref="StatePending"/>
    public string StateFailed { get; init; } = "Failed";

    /// <inheritdoc cref="StatePending"/>
    public string StateSkipped { get; init; } = "Skipped";

    /// <summary>The footer while a step runs; the argument is the current step's position.</summary>
    public Func<int, string> Working { get; init; } = position => string.Create(CultureInfo.CurrentCulture, $"Working on step {position}");

    /// <summary>The footer while paused; the argument is the position of the step the run will continue with.</summary>
    public Func<int, string> Paused { get; init; } = position => string.Create(CultureInfo.CurrentCulture, $"Paused at step {position}");

    /// <summary>The footer while a failed step waits for Retry or Skip; the argument is its position.</summary>
    public Func<int, string> Failed { get; init; } = position => string.Create(CultureInfo.CurrentCulture, $"Step {position} failed");

    /// <summary>The bold start of the done line; the argument is the formatted total time.</summary>
    public Func<string, string> DoneIn { get; init; } = total => $"Done in {total}.";

    /// <summary>The accessible name of the progress line.</summary>
    public string ProgressName { get; init; } = "Plan progress";

    /// <summary>The progress line's value text; the arguments are the finished steps (done or skipped) and the count.</summary>
    public Func<int, int, string> ProgressValue { get; init; } =
        (finished, count) => string.Create(CultureInfo.CurrentCulture, $"{finished} of {count} steps finished");

    /// <summary>A step's time: seconds under a minute ("23s"), then minutes and two-digit seconds ("1m 07s").</summary>
    public Func<TimeSpan, string> Duration { get; init; } = AgentPlanFormat.Duration;

    // Announcements (one polite live region) ------------------------------------------------------------------------

    /// <summary>Announced after a rename; the argument is the new title.</summary>
    public Func<string, string> AnnounceRenamed { get; init; } = title => $"Step renamed to {title}";

    /// <summary>Announced after a keyboard move; the arguments are the new position and the count.</summary>
    public Func<int, int, string> AnnounceMoved { get; init; } =
        (position, count) => string.Create(CultureInfo.CurrentCulture, $"Step moved to position {position} of {count}");

    /// <summary>Announced after a step is removed; the argument is its title.</summary>
    public Func<string, string> AnnounceRemoved { get; init; } = title => $"Removed: {title}";

    /// <summary>Announced after an empty step is removed.</summary>
    public string AnnounceRemovedEmpty { get; init; } = "Removed an empty step";

    /// <summary>Announced after Add step.</summary>
    public string AnnounceAdded { get; init; } = "Added a new step";

    /// <summary>Announced when Keep planning starts.</summary>
    public string AnnouncePlanning { get; init; } = "Asking the agent for one more step";

    /// <summary>Announced when a suggested step has streamed in; the argument is its title.</summary>
    public Func<string, string> AnnounceSuggested { get; init; } =
        title => $"The agent suggested a step: {title}. You can edit, move or remove it before you approve.";

    /// <summary>Announced on approval; the argument is the number of steps.</summary>
    public Func<int, string> AnnounceApproved { get; init; } = count => count == 1
        ? "Plan approved. Running 1 step."
        : string.Create(CultureInfo.CurrentCulture, $"Plan approved. Running {count} steps.");

    /// <summary>Added to the approval announcement while the built-in simulation runs the plan.</summary>
    public string AnnounceSimulated { get; init; } = "This run is simulated.";

    /// <summary>Announced when a step is done; the argument is its title.</summary>
    public Func<string, string> AnnounceDone { get; init; } = title => $"Step done: {title}";

    /// <summary>Announced when the agent inserts a step while running; the argument is its title.</summary>
    public Func<string, string> AnnounceInserted { get; init; } = title => $"New step from the agent: {title}";

    /// <summary>Announced when a step fails; the arguments are its position and the reason (may be empty).</summary>
    public Func<int, string, string> AnnounceFailed { get; init; } = (position, error) => string.IsNullOrWhiteSpace(error)
        ? string.Create(CultureInfo.CurrentCulture, $"Step {position} failed. Retry it or skip it.")
        : string.Create(CultureInfo.CurrentCulture, $"Step {position} failed: {error}. Retry it or skip it.");

    /// <summary>Announced when a failed step runs again; the argument is its title.</summary>
    public Func<string, string> AnnounceRetrying { get; init; } = title => $"Retrying: {title}";

    /// <summary>Announced when a step is skipped; the argument is its title.</summary>
    public Func<string, string> AnnounceSkipped { get; init; } = title => $"Skipped: {title}";

    /// <summary>Announced when the run pauses.</summary>
    public string AnnouncePaused { get; init; } = "Plan paused";

    /// <summary>Announced when the run resumes.</summary>
    public string AnnounceResumed { get; init; } = "Plan resumed";

    /// <summary>Announced when the plan is done; the arguments are the formatted total time and the summary (may be empty).</summary>
    public Func<string, string?, string> AnnounceFinished { get; init; } = (total, summary) => string.IsNullOrWhiteSpace(summary)
        ? $"Plan finished in {total}."
        : $"Plan finished in {total}. {summary}";

    /// <summary>Announced when the plan goes back to the draft.</summary>
    public string AnnounceBackToDraft { get; init; } = "Plan reset to the draft";

    /// <summary>The state word of <paramref name="state"/>.</summary>
    public string StateWord(AgentPlanStepState state) => state switch
    {
        AgentPlanStepState.Active => StateActive,
        AgentPlanStepState.Done => StateDone,
        AgentPlanStepState.Failed => StateFailed,
        AgentPlanStepState.Skipped => StateSkipped,
        _ => StatePending
    };
}
