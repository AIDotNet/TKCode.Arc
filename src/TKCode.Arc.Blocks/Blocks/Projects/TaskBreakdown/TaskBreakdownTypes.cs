using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>One subtask of a breakdown: a title, an estimate in hours and who does it.</summary>
public sealed class TaskBreakdownSubtask
{
    /// <summary>Creates an empty subtask.</summary>
    public TaskBreakdownSubtask()
    {
    }

    /// <summary>Creates a subtask from a title, an estimate in hours and an optional assignee.</summary>
    public TaskBreakdownSubtask(string title, double estimateHours, ProjectPerson? assignee = null)
    {
        Title = title;
        EstimateHours = estimateHours;
        Assignee = assignee;
    }

    /// <summary>What to do; the row's field writes it.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The estimate in hours; the row's field writes it.</summary>
    public double EstimateHours { get; set; }

    /// <summary>Who does it; null is unassigned. The row's picker writes it.</summary>
    public ProjectPerson? Assignee { get; set; }

    /// <summary>True for a subtask the suggester proposed (until the person edits its title).</summary>
    public bool IsSuggested { get; set; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>What the suggester is asked to break down.</summary>
/// <param name="Title">The task's title.</param>
/// <param name="Description">The task's description.</param>
/// <param name="People">The people subtasks can be assigned to.</param>
public sealed record TaskBreakdownRequest(string Title, string Description, IReadOnlyList<ProjectPerson> People);

/// <summary>Arguments of <see cref="TaskBreakdown.Created"/>.</summary>
public sealed class TaskBreakdownCreatedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TaskBreakdownCreatedEventArgs(RoutedEvent routedEvent, string title, IReadOnlyList<TaskBreakdownSubtask> subtasks)
        : base(routedEvent)
    {
        Title = title;
        Subtasks = subtasks;
    }

    /// <summary>The task's title.</summary>
    public string Title { get; }

    /// <summary>The subtasks in their order, empty titles left out.</summary>
    public IReadOnlyList<TaskBreakdownSubtask> Subtasks { get; }
}

/// <summary>The words <see cref="TaskBreakdown"/> shows and reads out; every one has an English default.</summary>
public sealed class TaskBreakdownLabels
{
    /// <summary>The title field's label.</summary>
    public string TitleField { get; init; } = "Task";

    /// <summary>The description field's label.</summary>
    public string DescriptionField { get; init; } = "Description";

    /// <summary>The description field's placeholder.</summary>
    public string DescriptionPlaceholder { get; init; } = "What needs to happen? One step per line or sentence works best.";

    /// <summary>The suggest button.</summary>
    public string Suggest { get; init; } = "Suggest subtasks";

    /// <summary>The suggest button once there are subtasks (it replaces them).</summary>
    public string SuggestAgain { get; init; } = "Suggest again";

    /// <summary>The subtasks heading.</summary>
    public string Subtasks { get; init; } = "Subtasks";

    /// <summary>The line shown while there are no subtasks.</summary>
    public string Empty { get; init; } = "Describe the task and press Suggest, or add subtasks yourself.";

    /// <summary>A row's title field name; {0} is the row's number.</summary>
    public string SubtaskFormat { get; init; } = "Subtask {0}";

    /// <summary>The estimate field's name.</summary>
    public string Estimate { get; init; } = "Estimate in hours";

    /// <summary>The unit after estimates.</summary>
    public string HoursUnit { get; init; } = "h";

    /// <summary>The assignee picker's placeholder and name.</summary>
    public string Assignee { get; init; } = "Assignee";

    /// <summary>The first option of the assignee picker.</summary>
    public string Unassigned { get; init; } = "Unassigned";

    /// <summary>The remove button's name; {0} is the subtask.</summary>
    public string RemoveFormat { get; init; } = "Remove {0}";

    /// <summary>The add button.</summary>
    public string Add { get; init; } = "Add subtask";

    /// <summary>The total line; {0} is the hours, {1} the working days.</summary>
    public string TotalFormat { get; init; } = "{0} h · about {1} days";

    /// <summary>The create button; {0} is the count.</summary>
    public string CreateFormat { get; init; } = "Create {0} subtasks";

    /// <summary>The announcement after suggestions arrived; {0} is the count.</summary>
    public string SuggestedFormat { get; init; } = "{0} subtasks suggested";

    /// <summary>The announcement after the subtasks were created; {0} is the count.</summary>
    public string CreatedFormat { get; init; } = "{0} subtasks created";

    /// <summary>The line when the suggester failed.</summary>
    public string SuggestFailed { get; init; } = "Suggestions are not available right now. Add subtasks yourself.";

    /// <summary>The name of the reorderable list.</summary>
    public string ListName { get; init; } = "Subtasks, in order";
}
