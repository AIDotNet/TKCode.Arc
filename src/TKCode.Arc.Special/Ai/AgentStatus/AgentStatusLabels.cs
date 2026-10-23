namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AgentStatus"/> shows or announces, in English by default. Replace the instance (or set its
/// properties) to translate or reword; texts with numbers or names are functions so that the word order can change.
/// The durations they receive are already formatted by <see cref="FormatTimer"/>, <see cref="FormatTotal"/> and the
/// other format functions.
/// </summary>
public sealed class AgentStatusLabels
{
    /// <summary>The agent's name when <see cref="AgentStatus.Agent"/> is not set.</summary>
    public string DefaultAgentName { get; set; } = "Agent";

    /// <summary>The idle label, and the label once a finished run has settled.</summary>
    public string Ready { get; set; } = "Ready";

    /// <summary>The label while paused.</summary>
    public string Paused { get; set; } = "Paused";

    /// <summary>The label while working when no activity is given.</summary>
    public string Working { get; set; } = "Working";

    /// <summary>The label while input is needed when no request is given.</summary>
    public string NeedsInput { get; set; } = "Approval needed";

    /// <summary>The label after a failure when no failure is given.</summary>
    public string Failed { get; set; } = "Failed";

    /// <summary>The done label: the formatted total and the outcome (null when none), "Done in 2m 23s · 5 files touched".</summary>
    public Func<string, string?, string> DoneIn { get; set; } =
        (total, outcome) => string.IsNullOrEmpty(outcome) ? $"Done in {total}" : $"Done in {total} · {outcome}";

    /// <summary>The done announcement after the agent's name: the total in words and the outcome, "done in 14 seconds, 5 files touched".</summary>
    public Func<string, string?, string> DoneInSpoken { get; set; } =
        (total, outcome) => string.IsNullOrEmpty(outcome) ? $"done in {total}" : $"done in {total}, {outcome}";

    /// <summary>The stopped label: the formatted total, "Stopped after 7s".</summary>
    public Func<string, string> StoppedAfter { get; set; } = total => $"Stopped after {total}";

    /// <summary>The stopped announcement after the agent's name: the total in words, "stopped after 7 seconds".</summary>
    public Func<string, string> StoppedAfterSpoken { get; set; } = total => $"stopped after {total}";

    /// <summary>An activity's count: value and total, "8 of 12".</summary>
    public Func<int, int, string> Count { get; set; } = (value, total) => $"{value} of {total}";

    /// <summary>The panel's step counter while the run is live: the step's number and the total, "Step 3 of 6".</summary>
    public Func<int, int, string> StepOf { get; set; } = (index, total) => $"Step {index} of {total}";

    /// <summary>The panel's step counter once the run ended: finished (done or skipped) and total, "6 of 6 steps".</summary>
    public Func<int, int, string> StepsFinished { get; set; } = (finished, total) => $"{finished} of {total} steps";

    /// <summary>The progress bar's value text: finished and total, "4 of 6 steps finished".</summary>
    public Func<int, int, string> ProgressValueText { get; set; } = (finished, total) => $"{finished} of {total} steps finished";

    /// <summary>The progress bar's accessible name.</summary>
    public string RunProgress { get; set; } = "Run progress";

    /// <summary>The panel's accessible name: the agent's name, "Atlas Scout 2 run details".</summary>
    public Func<string, string> RunDetails { get; set; } = agent => $"{agent} run details";

    /// <summary>The panel's text before a run has any step: the agent's name.</summary>
    public Func<string, string> EmptyPanel { get; set; } = agent => $"Nothing has run yet. Steps appear here once {agent} starts.";

    /// <summary>
    /// The pill's accessible name: the agent, the activity with its count, and the timer when one shows (null otherwise),
    /// "Juniper Code 3: Checking specs 8 of 12, 0:09 elapsed".
    /// </summary>
    public Func<string, string, string?, string> PillName { get; set; } =
        (agent, activity, elapsed) => elapsed is null ? $"{agent}: {activity}" : $"{agent}: {activity}, {elapsed} elapsed";

    /// <summary>
    /// A polite announcement: the agent, the activity (without its count) or outcome, and an action's notice (null when
    /// none), "Juniper Code 3: Paused. The run holds where it is.".
    /// </summary>
    public Func<string, string, string?, string> Announcement { get; set; } =
        (agent, text, notice) => string.IsNullOrEmpty(notice) ? $"{agent}: {text}" : $"{agent}: {text}. {notice}";

    /// <summary>The Pause button.</summary>
    public string Pause { get; set; } = "Pause";

    /// <summary>The Resume button.</summary>
    public string Resume { get; set; } = "Resume";

    /// <summary>The Stop button.</summary>
    public string Stop { get; set; } = "Stop";

    /// <summary>The Approve button.</summary>
    public string Approve { get; set; } = "Approve";

    /// <summary>The Deny button.</summary>
    public string Deny { get; set; } = "Deny";

    /// <summary>The Retry button.</summary>
    public string Retry { get; set; } = "Retry";

    /// <summary>The Open run button.</summary>
    public string OpenRun { get; set; } = "Open run";

    /// <summary>The Open run button's confirmation for a moment after it was pressed.</summary>
    public string Opened { get; set; } = "Opened";

    /// <summary>The timer: active time (paused and failed time left out) as shown, "2:20".</summary>
    public Func<TimeSpan, string> FormatTimer { get; set; } = AgentStatusFormat.Timer;

    /// <summary>A run's total as shown in "Done in …" and "Stopped after …", "2m 23s".</summary>
    public Func<TimeSpan, string> FormatTotal { get; set; } = AgentStatusFormat.Total;

    /// <summary>A run's total in words for announcements, "2 minutes 23 seconds".</summary>
    public Func<TimeSpan, string> FormatTotalSpoken { get; set; } = AgentStatusFormat.TotalSpoken;

    /// <summary>A finished step's duration, "1.9s".</summary>
    public Func<TimeSpan, string> FormatStepDuration { get; set; } = AgentStatusFormat.StepDuration;

    /// <summary>A running or waiting step's time so far, "12s".</summary>
    public Func<TimeSpan, string> FormatLiveStepDuration { get; set; } = AgentStatusFormat.LiveStepDuration;
}
