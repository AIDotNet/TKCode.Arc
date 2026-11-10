using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How sure the owner is that a key result will be met.</summary>
public enum GoalTrackerConfidence
{
    /// <summary>Expected to be met.</summary>
    OnTrack,

    /// <summary>Could be missed without help.</summary>
    AtRisk,

    /// <summary>Not expected to be met.</summary>
    OffTrack,
}

/// <summary>Which view of the goals <see cref="GoalTracker"/> shows.</summary>
public enum GoalTrackerView
{
    /// <summary>Goal cards with their key results and check-ins.</summary>
    List,

    /// <summary>Progress over the period against the straight line to the target.</summary>
    Timeline,
}

/// <summary>A check-in on a key result: the value reached on a day, the owner's confidence and a note.</summary>
public sealed class GoalTrackerCheckIn
{
    /// <summary>Creates an empty check-in.</summary>
    public GoalTrackerCheckIn()
    {
    }

    /// <summary>Creates a check-in from a date, a value and a confidence, with an optional note and author.</summary>
    public GoalTrackerCheckIn(DateTime date, double value, GoalTrackerConfidence confidence, string? note = null, string? author = null)
    {
        Date = date;
        Value = value;
        Confidence = confidence;
        Note = note;
        Author = author;
    }

    /// <summary>The day of the check-in.</summary>
    public DateTime Date { get; set; }

    /// <summary>The key result's value on that day.</summary>
    public double Value { get; set; }

    /// <summary>How sure the owner was.</summary>
    public GoalTrackerConfidence Confidence { get; set; }

    /// <summary>What changed or what is in the way; null leaves it out.</summary>
    public string? Note { get; set; }

    /// <summary>Who checked in; null leaves it out.</summary>
    public string? Author { get; set; }
}

/// <summary>A measurable result that tells whether a goal was met: from a start value to a target, with check-ins.</summary>
public sealed class GoalTrackerKeyResult
{
    /// <summary>Creates an empty key result.</summary>
    public GoalTrackerKeyResult()
    {
    }

    /// <summary>Creates a key result from a title, a start, a target and the current value.</summary>
    public GoalTrackerKeyResult(string title, double start, double target, double current, string? unit = null)
    {
        Title = title;
        Start = start;
        Target = target;
        Current = current;
        Unit = unit;
    }

    /// <summary>What is measured ("Weekly active teams to 600").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Who owns it; null leaves the avatar out.</summary>
    public ProjectPerson? Owner { get; set; }

    /// <summary>The value at the start of the period.</summary>
    public double Start { get; set; }

    /// <summary>The value to reach (may be below <see cref="Start"/> for a result that should fall).</summary>
    public double Target { get; set; }

    /// <summary>The value now; a check-in writes it.</summary>
    public double Current { get; set; }

    /// <summary>The unit after values ("teams", "%"); null shows plain numbers.</summary>
    public string? Unit { get; set; }

    /// <summary>How sure the owner is now; a check-in writes it.</summary>
    public GoalTrackerConfidence Confidence { get; set; }

    /// <summary>The check-ins, oldest first; the block adds one for each check-in made in it.</summary>
    public List<GoalTrackerCheckIn> CheckIns { get; set; } = new();

    /// <summary>How far from the start to the target the current value is, from 0 to 1.</summary>
    public double Progress => ProgressOf(Current);

    /// <summary>How far from the start to the target a value is, from 0 to 1.</summary>
    public double ProgressOf(double value) => Target == Start ? (value >= Target ? 1 : 0) : Math.Clamp((value - Start) / (Target - Start), 0, 1);

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>A goal: what a team wants to achieve in the period, measured by its key results.</summary>
public sealed class GoalTrackerGoal
{
    /// <summary>Creates an empty goal.</summary>
    public GoalTrackerGoal()
    {
    }

    /// <summary>Creates a goal from a title, a team and an owner.</summary>
    public GoalTrackerGoal(string title, string? team = null, ProjectPerson? owner = null)
    {
        Title = title;
        Team = team;
        Owner = owner;
    }

    /// <summary>The goal in one line.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The team it belongs to (the team filter lists these); null leaves it out.</summary>
    public string? Team { get; set; }

    /// <summary>Who owns it; null leaves the avatar out.</summary>
    public ProjectPerson? Owner { get; set; }

    /// <summary>The key results that measure it.</summary>
    public List<GoalTrackerKeyResult> KeyResults { get; set; } = new();

    /// <summary>The average progress of the key results, from 0 to 1.</summary>
    public double Progress => KeyResults.Count == 0 ? 0 : KeyResults.Average(k => k.Progress);

    /// <summary>The least confident of the key results' confidences.</summary>
    public GoalTrackerConfidence Confidence => KeyResults.Count == 0 ? GoalTrackerConfidence.OnTrack : KeyResults.Max(k => k.Confidence);

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>Arguments of <see cref="GoalTracker.CheckedIn"/>.</summary>
public sealed class GoalTrackerCheckedInEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public GoalTrackerCheckedInEventArgs(RoutedEvent routedEvent, GoalTrackerGoal goal, GoalTrackerKeyResult keyResult, GoalTrackerCheckIn checkIn)
        : base(routedEvent)
    {
        Goal = goal;
        KeyResult = keyResult;
        CheckIn = checkIn;
    }

    /// <summary>The goal of the key result.</summary>
    public GoalTrackerGoal Goal { get; }

    /// <summary>The key result checked in on (its current value and confidence are already updated).</summary>
    public GoalTrackerKeyResult KeyResult { get; }

    /// <summary>The new check-in (already added to <see cref="GoalTrackerKeyResult.CheckIns"/>).</summary>
    public GoalTrackerCheckIn CheckIn { get; }
}

/// <summary>The words <see cref="GoalTracker"/> shows and reads out; every one has an English default.</summary>
public sealed class GoalTrackerLabels
{
    /// <summary>The confidences' names, in the order of <see cref="GoalTrackerConfidence"/>.</summary>
    public IReadOnlyList<string> Confidences { get; init; } = ["On track", "At risk", "Off track"];

    /// <summary>The views' names, in the order of <see cref="GoalTrackerView"/>.</summary>
    public IReadOnlyList<string> Views { get; init; } = ["List", "Timeline"];

    /// <summary>The screen reader name of the view switch.</summary>
    public string ViewName { get; init; } = "View";

    /// <summary>The team filter's first option.</summary>
    public string AllTeams { get; init; } = "All teams";

    /// <summary>The screen reader name of the team filter.</summary>
    public string TeamName { get; init; } = "Team";

    /// <summary>The period line; {0} is the start, {1} the end, {2} the share of the period gone.</summary>
    public string PeriodFormat { get; init; } = "{0} – {1} · {2} of the period gone";

    /// <summary>The overall progress line; {0} is the share.</summary>
    public string OverallFormat { get; init; } = "{0} overall";

    /// <summary>A key result's value line; {0} is the current value, {1} the target.</summary>
    public string ValueFormat { get; init; } = "{0} of {1}";

    /// <summary>The check-in button.</summary>
    public string CheckIn { get; init; } = "Check in";

    /// <summary>The check-in editor's value field.</summary>
    public string NewValue { get; init; } = "New value";

    /// <summary>The check-in editor's confidence switch.</summary>
    public string Confidence { get; init; } = "Confidence";

    /// <summary>The check-in editor's note field.</summary>
    public string Note { get; init; } = "What changed?";

    /// <summary>The check-in editor's save button.</summary>
    public string Save { get; init; } = "Save check-in";

    /// <summary>The check-in editor's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The last check-in line; {0} is the date, {1} the note.</summary>
    public string LastCheckInFormat { get; init; } = "{0}: {1}";

    /// <summary>The line of a key result with no check-in yet.</summary>
    public string NoCheckIn { get; init; } = "No check-in yet";

    /// <summary>The marker's name on the progress bars: where the result should be by today.</summary>
    public string Expected { get; init; } = "Expected by today";

    /// <summary>The announcement after a check-in; {0} is the key result, {1} its progress, {2} the confidence.</summary>
    public string CheckedInFormat { get; init; } = "Checked in on {0}: {1}, {2}";

    /// <summary>The timeline's today line.</summary>
    public string Today { get; init; } = "Today";

    /// <summary>The line when no goal matches the team filter.</summary>
    public string Empty { get; init; } = "No goals for this team.";
}
