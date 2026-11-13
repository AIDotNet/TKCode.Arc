using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How a run of a routine ended.</summary>
public enum AgentRoutineOutcome
{
    /// <summary>It did its job.</summary>
    Succeeded,

    /// <summary>It stopped with an error.</summary>
    Failed,

    /// <summary>It had nothing to do (no new input, outside its window).</summary>
    Skipped
}

/// <summary>One past run of a routine.</summary>
/// <param name="Started">When it started.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Duration">How long it took.</param>
/// <param name="Summary">What it did or why it failed, in a line.</param>
public sealed record AgentRoutineRun(DateTimeOffset Started, AgentRoutineOutcome Outcome, TimeSpan Duration, string? Summary = null);

/// <summary>
/// A scheduled agent routine. An <see cref="AvaloniaObject"/>: change it while the list is on screen and its row
/// follows. The list writes <see cref="IsEnabled"/> when the person flips the switch and <see cref="Runs"/> after a run
/// started with Run now.
/// </summary>
public class AgentRoutine : AvaloniaObject
{
    /// <summary>Your id of the routine.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<AgentRoutine, string?>(nameof(Id));

    /// <summary>The routine's name ("Morning inbox digest").</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<AgentRoutine, string>(nameof(Name), string.Empty);

    /// <summary>The agent that runs it.</summary>
    public static readonly StyledProperty<string?> AgentProperty =
        AvaloniaProperty.Register<AgentRoutine, string?>(nameof(Agent));

    /// <summary>When it runs, as a five-field cron expression ("0 9 * * 1-5"); see <see cref="AgentRoutineSchedule"/>.</summary>
    public static readonly StyledProperty<string?> ScheduleProperty =
        AvaloniaProperty.Register<AgentRoutine, string?>(nameof(Schedule));

    /// <summary>What the agent is told to do each time.</summary>
    public static readonly StyledProperty<string?> InstructionsProperty =
        AvaloniaProperty.Register<AgentRoutine, string?>(nameof(Instructions));

    /// <summary>Whether the routine runs on its schedule.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<AgentRoutine, bool>(nameof(IsEnabled), true);

    /// <summary>Past runs, newest first.</summary>
    public static readonly StyledProperty<IReadOnlyList<AgentRoutineRun>?> RunsProperty =
        AvaloniaProperty.Register<AgentRoutine, IReadOnlyList<AgentRoutineRun>?>(nameof(Runs));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="AgentProperty"/>
    public string? Agent { get => GetValue(AgentProperty); set => SetValue(AgentProperty, value); }

    /// <inheritdoc cref="ScheduleProperty"/>
    public string? Schedule { get => GetValue(ScheduleProperty); set => SetValue(ScheduleProperty, value); }

    /// <inheritdoc cref="InstructionsProperty"/>
    public string? Instructions { get => GetValue(InstructionsProperty); set => SetValue(InstructionsProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="RunsProperty"/>
    public IReadOnlyList<AgentRoutineRun>? Runs { get => GetValue(RunsProperty); set => SetValue(RunsProperty, value); }
}

/// <summary>Event data of the <see cref="AgentRoutines"/> events: the routine, and for a run, its result.</summary>
public sealed class AgentRoutineEventArgs(RoutedEvent routedEvent, AgentRoutine routine, AgentRoutineRun? run = null) : RoutedEventArgs(routedEvent)
{
    /// <summary>The routine.</summary>
    public AgentRoutine Routine { get; } = routine;

    /// <summary>The run that finished (<see cref="AgentRoutines.RunCompleted"/>); null for the other events.</summary>
    public AgentRoutineRun? Run { get; } = run;
}
