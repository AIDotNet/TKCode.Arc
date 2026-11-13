using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a task of an <see cref="AgentTasks"/> board stands; each status is a column.</summary>
public enum AgentTaskStatus
{
    /// <summary>Waiting for an agent.</summary>
    Queued,

    /// <summary>An agent is working on it; <see cref="AgentTaskItem.Progress"/> shows how far.</summary>
    Running,

    /// <summary>The agent is waiting for a person (a question, a review).</summary>
    NeedsInput,

    /// <summary>The agent gave up with an error; shown in the same column as <see cref="NeedsInput"/>.</summary>
    Failed,

    /// <summary>Finished.</summary>
    Done
}

/// <summary>
/// A task delegated to an agent. An <see cref="AvaloniaObject"/>: change <see cref="Status"/>, <see cref="Progress"/> or
/// <see cref="Note"/> while the board is on screen and the card follows (and moves column).
/// </summary>
public class AgentTaskItem : AvaloniaObject
{
    /// <summary>Your id of the task, handed back in <see cref="AgentTasks.TaskOpened"/>.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<AgentTaskItem, string?>(nameof(Id));

    /// <summary>What the task is ("Draft the March release notes").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AgentTaskItem, string>(nameof(Title), string.Empty);

    /// <summary>The name of the agent that owns the task.</summary>
    public static readonly StyledProperty<string?> AgentProperty =
        AvaloniaProperty.Register<AgentTaskItem, string?>(nameof(Agent));

    /// <summary>Where the task stands.</summary>
    public static readonly StyledProperty<AgentTaskStatus> StatusProperty =
        AvaloniaProperty.Register<AgentTaskItem, AgentTaskStatus>(nameof(Status));

    /// <summary>How far a running task is, from 0 to 1.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AgentTaskItem, double>(nameof(Progress));

    /// <summary>A short line under the title: the current step, the question for the person, or the error.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<AgentTaskItem, string?>(nameof(Note));

    /// <summary>When the task last changed; shown as a relative time.</summary>
    public static readonly StyledProperty<DateTimeOffset?> UpdatedProperty =
        AvaloniaProperty.Register<AgentTaskItem, DateTimeOffset?>(nameof(Updated));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="AgentProperty"/>
    public string? Agent { get => GetValue(AgentProperty); set => SetValue(AgentProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public AgentTaskStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="UpdatedProperty"/>
    public DateTimeOffset? Updated { get => GetValue(UpdatedProperty); set => SetValue(UpdatedProperty, value); }
}

/// <summary>Event data of <see cref="AgentTasks.TaskOpened"/>: the task whose run the person opened.</summary>
public sealed class AgentTaskEventArgs(RoutedEvent routedEvent, AgentTaskItem task) : RoutedEventArgs(routedEvent)
{
    /// <summary>The task.</summary>
    public AgentTaskItem Task { get; } = task;
}
