using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="ProjectBoard.TaskMoving"/>: a task is about to change stage; set <see cref="Cancel"/> to keep it.</summary>
public class ProjectBoardTaskMovingEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The board.</param>
    /// <param name="task">The task about to move.</param>
    /// <param name="fromStage">The stage it is in.</param>
    /// <param name="toStage">The stage it is going to.</param>
    public ProjectBoardTaskMovingEventArgs(RoutedEvent routedEvent, object source, ProjectBoardTask task, ProjectBoardStage fromStage,
        ProjectBoardStage toStage)
        : base(routedEvent, source)
    {
        Task = task;
        FromStage = fromStage;
        ToStage = toStage;
    }

    /// <summary>The task about to move.</summary>
    public ProjectBoardTask Task { get; }

    /// <summary>The stage it is in.</summary>
    public ProjectBoardStage FromStage { get; }

    /// <summary>The stage it is going to.</summary>
    public ProjectBoardStage ToStage { get; }

    /// <summary>True keeps the task where it is.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Arguments of <see cref="ProjectBoard.TaskMoved"/>: a task changed stage. Raised as the card starts its flight, with the
/// task's <see cref="ProjectBoardTask.StageKey"/> and its place in the collection already updated.
/// </summary>
public class ProjectBoardTaskMovedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The board.</param>
    /// <param name="task">The task that moved.</param>
    /// <param name="fromStage">The stage it left.</param>
    /// <param name="toStage">The stage it is in now.</param>
    /// <param name="fromIndex">Its position in the old column (counting every task, filtered or not).</param>
    /// <param name="toIndex">Its position in the new column.</param>
    public ProjectBoardTaskMovedEventArgs(RoutedEvent routedEvent, object source, ProjectBoardTask task, ProjectBoardStage fromStage,
        ProjectBoardStage toStage, int fromIndex, int toIndex)
        : base(routedEvent, source)
    {
        Task = task;
        FromStage = fromStage;
        ToStage = toStage;
        FromIndex = fromIndex;
        ToIndex = toIndex;
    }

    /// <summary>The task that moved.</summary>
    public ProjectBoardTask Task { get; }

    /// <summary>The stage it left.</summary>
    public ProjectBoardStage FromStage { get; }

    /// <summary>The stage it is in now.</summary>
    public ProjectBoardStage ToStage { get; }

    /// <summary>Its position in the old column, counting every task of that stage (filtered or not).</summary>
    public int FromIndex { get; }

    /// <summary>Its position in the new column, counting every task of that stage.</summary>
    public int ToIndex { get; }
}

/// <summary>
/// Arguments of <see cref="ProjectBoard.AddRequested"/>: the Add button was pressed. Put a task in <see cref="Task"/> to
/// add it, or set <c>Handled</c> without a task to add nothing (for example to open your own form first); otherwise the
/// board adds a draft from <see cref="ProjectBoard.DraftTitles"/>.
/// </summary>
public class ProjectBoardAddRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The board.</param>
    /// <param name="stage">The stage a new task goes to.</param>
    public ProjectBoardAddRequestedEventArgs(RoutedEvent routedEvent, object source, ProjectBoardStage stage)
        : base(routedEvent, source)
    {
        Stage = stage;
    }

    /// <summary>The stage a new task goes to (<see cref="ProjectBoard.AddStageKey"/>, or the first stage).</summary>
    public ProjectBoardStage Stage { get; }

    /// <summary>The task to add; null lets the board add a draft unless the event is handled.</summary>
    public ProjectBoardTask? Task { get; set; }
}

/// <summary>Arguments of <see cref="ProjectBoard.TaskAdded"/>: a task was added through the board.</summary>
public class ProjectBoardTaskEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The board.</param>
    /// <param name="task">The task.</param>
    /// <param name="stage">Its stage.</param>
    public ProjectBoardTaskEventArgs(RoutedEvent routedEvent, object source, ProjectBoardTask task, ProjectBoardStage stage)
        : base(routedEvent, source)
    {
        Task = task;
        Stage = stage;
    }

    /// <summary>The task.</summary>
    public ProjectBoardTask Task { get; }

    /// <summary>Its stage.</summary>
    public ProjectBoardStage Stage { get; }
}

/// <summary>Arguments of <see cref="ProjectBoard.FilterChanged"/>: the board switched between all work and my work.</summary>
public class ProjectBoardFilterChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The board.</param>
    /// <param name="oldFilter">The filter before.</param>
    /// <param name="newFilter">The filter now.</param>
    public ProjectBoardFilterChangedEventArgs(RoutedEvent routedEvent, object source, ProjectBoardFilter oldFilter, ProjectBoardFilter newFilter)
        : base(routedEvent, source)
    {
        OldFilter = oldFilter;
        NewFilter = newFilter;
    }

    /// <summary>The filter before.</summary>
    public ProjectBoardFilter OldFilter { get; }

    /// <summary>The filter now.</summary>
    public ProjectBoardFilter NewFilter { get; }
}
