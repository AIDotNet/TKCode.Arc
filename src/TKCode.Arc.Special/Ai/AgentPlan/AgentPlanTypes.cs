using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where an <see cref="AgentPlan"/> stands.</summary>
public enum AgentPlanStatus
{
    /// <summary>The plan is a draft: steps can be renamed, moved, removed and added, then approved.</summary>
    Draft,

    /// <summary>The plan was approved and the agent is working through it.</summary>
    Running,

    /// <summary>The run is paused; Resume continues it.</summary>
    Paused,

    /// <summary>Every step is finished; the footer shows the total time and the summary.</summary>
    Done
}

/// <summary>Where one step of an <see cref="AgentPlan"/> stands while the plan runs.</summary>
public enum AgentPlanStepState
{
    /// <summary>Not started yet: an empty ring.</summary>
    Pending,

    /// <summary>The step the agent is working on: a spinning arc, a shimmering title and its activity lines.</summary>
    Active,

    /// <summary>Finished: a disc with a check, the title in secondary text and the time on the right.</summary>
    Done,

    /// <summary>Failed: a disc with an exclamation mark, the reason, and Retry and Skip.</summary>
    Failed,

    /// <summary>Skipped after a failure: a dashed ring, muted title and the word Skipped.</summary>
    Skipped
}

/// <summary>What a step's note says about where the step came from; it decides whether the model's mark leads the note.</summary>
public enum AgentPlanNoteKind
{
    /// <summary>A plain note (<see cref="AgentPlanStep.Note"/>) with no mark, or no note at all.</summary>
    None,

    /// <summary>A step the agent suggested while planning (Keep planning): the mark and "Suggested".</summary>
    Suggested,

    /// <summary>A step the agent inserted while running: the mark and "Added by agent".</summary>
    AddedByAgent
}

/// <summary>The kind of draft edit an <see cref="AgentPlan.Changed"/> event reports.</summary>
public enum AgentPlanChangeKind
{
    /// <summary>A step title was saved (Enter or leaving the field).</summary>
    Rename,

    /// <summary>A step was moved (dropped after a drag, or moved with the keyboard).</summary>
    Move,

    /// <summary>A step was removed (its remove button, Backspace in an empty title, or an emptied title losing focus).</summary>
    Remove,

    /// <summary>An empty step was added at the end (Add step).</summary>
    Add,

    /// <summary>A suggested step finished streaming in and became editable.</summary>
    SuggestionStreamed
}

/// <summary>What someone asked for on a failed step.</summary>
public enum AgentPlanStepActionKind
{
    /// <summary>Run the failed step again.</summary>
    Retry,

    /// <summary>Leave the failed step and continue with the next one.</summary>
    Skip
}

/// <summary>Arguments of <see cref="AgentPlan.Approved"/>: the steps in their final order, titles trimmed, empty steps removed.</summary>
public sealed class AgentPlanApprovedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentPlanApprovedEventArgs(RoutedEvent routedEvent, IReadOnlyList<AgentPlanStep> steps)
        : base(routedEvent)
    {
        Steps = steps;
    }

    /// <summary>The approved steps, in order.</summary>
    public IReadOnlyList<AgentPlanStep> Steps { get; }
}

/// <summary>Arguments of <see cref="AgentPlan.Changed"/>: the draft after an edit and what the edit was.</summary>
public sealed class AgentPlanChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentPlanChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<AgentPlanStep> steps, AgentPlanChangeKind kind, AgentPlanStep? step)
        : base(routedEvent)
    {
        Steps = steps;
        Kind = kind;
        Step = step;
    }

    /// <summary>Every step of the draft after the edit, in order.</summary>
    public IReadOnlyList<AgentPlanStep> Steps { get; }

    /// <summary>What changed.</summary>
    public AgentPlanChangeKind Kind { get; }

    /// <summary>The step that was renamed, moved, removed, added or streamed in.</summary>
    public AgentPlanStep? Step { get; }
}

/// <summary>Arguments of <see cref="AgentPlan.StepAction"/>: Retry or Skip on a failed step.</summary>
public sealed class AgentPlanStepActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentPlanStepActionEventArgs(RoutedEvent routedEvent, AgentPlanStepActionKind kind, AgentPlanStep step)
        : base(routedEvent)
    {
        Kind = kind;
        Step = step;
    }

    /// <summary>Retry or Skip.</summary>
    public AgentPlanStepActionKind Kind { get; }

    /// <summary>The failed step.</summary>
    public AgentPlanStep Step { get; }
}

/// <summary>Arguments of <see cref="AgentPlan.PauseChanged"/>: Pause (true) or Resume (false).</summary>
public sealed class AgentPlanPauseChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentPlanPauseChangedEventArgs(RoutedEvent routedEvent, bool isPaused)
        : base(routedEvent)
    {
        IsPaused = isPaused;
    }

    /// <summary>True when Pause was pressed, false for Resume.</summary>
    public bool IsPaused { get; }
}
