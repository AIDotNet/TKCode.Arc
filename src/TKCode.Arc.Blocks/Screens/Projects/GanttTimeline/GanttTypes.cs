using Avalonia;

namespace TKCode.Arc;

/// <summary>The scale of a <see cref="GanttTimeline"/>: how wide one day is.</summary>
public enum GanttZoom
{
    /// <summary>36 pixels a day.</summary>
    Day,

    /// <summary>14 pixels a day (the default).</summary>
    Week,

    /// <summary>5 pixels a day.</summary>
    Month,
}

/// <summary>What changed a task's dates (<see cref="GanttChange.Kind"/>).</summary>
public enum GanttChangeKind
{
    /// <summary>The bar was moved (drag or Alt + arrow).</summary>
    Move,

    /// <summary>The start or the end was changed (edge drag or Alt + Shift + arrow).</summary>
    Resize,

    /// <summary>The last change was undone.</summary>
    Undo,

    /// <summary>Every task went back to its starting dates.</summary>
    Reset,
}

/// <summary>A phase: a row that folds the tasks of its group into one summary bar.</summary>
public sealed class GanttGroup
{
    public GanttGroup()
    {
    }

    public GanttGroup(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>The id that <see cref="GanttTask.GroupId"/> refers to.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The phase name shown in the task column.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>A task bar (or a milestone) on the timeline.</summary>
public sealed class GanttTask
{
    public GanttTask()
    {
    }

    public GanttTask(string id, string name, string groupId, DateOnly start, DateOnly end, double progress = 0)
    {
        Id = id;
        Name = name;
        GroupId = groupId;
        Start = start;
        End = end;
        Progress = progress;
    }

    /// <summary>A stable id (dependencies, focus and changes refer to it).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name shown on the bar and in the task column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The id of the <see cref="GanttGroup"/> the task belongs to.</summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>The first day of the task.</summary>
    public DateOnly Start { get; set; }

    /// <summary>The last day of the task, included. A milestone uses <see cref="Start"/> only.</summary>
    public DateOnly End { get; set; }

    /// <summary>The part done, 0 to 1.</summary>
    public double Progress { get; set; }

    /// <summary>The person responsible; shown as an avatar before the name.</summary>
    public ProjectPerson? Assignee { get; set; }

    /// <summary>The ids of the tasks that must end before this one starts.</summary>
    public IList<string> DependsOn { get; set; } = [];

    /// <summary>True draws a single-day marker instead of a bar.</summary>
    public bool IsMilestone { get; set; }

    internal GanttTask Clone() => new()
    {
        Id = Id, Name = Name, GroupId = GroupId, Start = Start, End = IsMilestone ? Start : End, Progress = Progress,
        Assignee = Assignee, DependsOn = [.. DependsOn], IsMilestone = IsMilestone,
    };
}

/// <summary>What changed in <see cref="GanttTimeline.TasksChanged"/>.</summary>
public sealed class GanttChange
{
    public GanttChange(string id, DateOnly start, DateOnly end, GanttChangeKind kind)
    {
        Id = id;
        Start = start;
        End = end;
        Kind = kind;
    }

    /// <summary>The task that changed (empty after a reset).</summary>
    public string Id { get; }

    /// <summary>The new first day.</summary>
    public DateOnly Start { get; }

    /// <summary>The new last day.</summary>
    public DateOnly End { get; }

    /// <summary>What caused the change.</summary>
    public GanttChangeKind Kind { get; }
}

/// <summary>Data of <see cref="GanttTimeline.TasksChanged"/>.</summary>
public sealed class GanttTasksChangedEventArgs(IReadOnlyList<GanttTask> tasks, GanttChange change) : EventArgs
{
    /// <summary>All tasks after the change.</summary>
    public IReadOnlyList<GanttTask> Tasks { get; } = tasks;

    /// <summary>What changed.</summary>
    public GanttChange Change { get; } = change;
}
