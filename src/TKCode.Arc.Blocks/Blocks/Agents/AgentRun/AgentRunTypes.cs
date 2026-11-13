using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What a step of an <see cref="AgentRun"/> is.</summary>
public enum AgentRunStepKind
{
    /// <summary>The agent reasons before acting; the detail is its reasoning.</summary>
    Thinking,

    /// <summary>The agent calls a tool; the detail shows the input and the result.</summary>
    ToolCall,

    /// <summary>The agent creates, changes or deletes files; the detail lists them.</summary>
    FileEdit,

    /// <summary>The agent writes to the person; the text shows open.</summary>
    Message
}

/// <summary>Where a step of an <see cref="AgentRun"/> stands.</summary>
public enum AgentRunStepStatus
{
    /// <summary>Not started yet.</summary>
    Pending,

    /// <summary>Running now.</summary>
    Running,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>Finished with an error; <see cref="AgentRunStep.Output"/> says why.</summary>
    Failed,

    /// <summary>Cut short because the run was stopped.</summary>
    Stopped
}

/// <summary>Where a whole <see cref="AgentRun"/> stands.</summary>
public enum AgentRunState
{
    /// <summary>Not started.</summary>
    Idle,

    /// <summary>Working: the clock runs and Stop is offered.</summary>
    Running,

    /// <summary>Finished: Run again is offered.</summary>
    Completed,

    /// <summary>Stopped by the person: Retry is offered.</summary>
    Stopped,

    /// <summary>Ended with an error: the reason shows and Retry is offered.</summary>
    Failed
}

/// <summary>How a file of an <see cref="AgentRunStepKind.FileEdit"/> step changed.</summary>
public enum AgentRunFileChange
{
    /// <summary>A new file.</summary>
    Created,

    /// <summary>An existing file was changed.</summary>
    Modified,

    /// <summary>The file was removed.</summary>
    Deleted
}

/// <summary>A file touched by an <see cref="AgentRunStepKind.FileEdit"/> step.</summary>
/// <param name="Path">The file's path as the person knows it ("src/routes/upload.ts").</param>
/// <param name="Change">Created, modified or deleted.</param>
/// <param name="Added">Lines added.</param>
/// <param name="Removed">Lines removed.</param>
public sealed record AgentRunFile(string Path, AgentRunFileChange Change, int Added = 0, int Removed = 0);

/// <summary>
/// One step of an <see cref="AgentRun"/>. An <see cref="AvaloniaObject"/>: change <see cref="Status"/>,
/// <see cref="Output"/> or <see cref="Duration"/> while the run is on screen and the timeline follows.
/// </summary>
public class AgentRunStep : AvaloniaObject
{
    /// <summary>What the step is.</summary>
    public static readonly StyledProperty<AgentRunStepKind> KindProperty =
        AvaloniaProperty.Register<AgentRunStep, AgentRunStepKind>(nameof(Kind));

    /// <summary>The one-line title of the step ("Search the code for the upload route").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AgentRunStep, string>(nameof(Title), string.Empty);

    /// <summary>The reasoning of a thinking step or the text of a message; null shows none.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentRunStep, string?>(nameof(Text));

    /// <summary>The tool a tool call runs ("search_code"), shown in mono type next to the title.</summary>
    public static readonly StyledProperty<string?> ToolNameProperty =
        AvaloniaProperty.Register<AgentRunStep, string?>(nameof(ToolName));

    /// <summary>What the tool was given, shown as written (often JSON or a command line).</summary>
    public static readonly StyledProperty<string?> InputProperty =
        AvaloniaProperty.Register<AgentRunStep, string?>(nameof(Input));

    /// <summary>What the tool returned, or why the step failed.</summary>
    public static readonly StyledProperty<string?> OutputProperty =
        AvaloniaProperty.Register<AgentRunStep, string?>(nameof(Output));

    /// <summary>The files a file edit step touched.</summary>
    public static readonly StyledProperty<IReadOnlyList<AgentRunFile>?> FilesProperty =
        AvaloniaProperty.Register<AgentRunStep, IReadOnlyList<AgentRunFile>?>(nameof(Files));

    /// <summary>Where the step stands.</summary>
    public static readonly StyledProperty<AgentRunStepStatus> StatusProperty =
        AvaloniaProperty.Register<AgentRunStep, AgentRunStepStatus>(nameof(Status));

    /// <summary>How long the step took; null while it runs or when unknown.</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<AgentRunStep, TimeSpan?>(nameof(Duration));

    /// <inheritdoc cref="KindProperty"/>
    public AgentRunStepKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="ToolNameProperty"/>
    public string? ToolName { get => GetValue(ToolNameProperty); set => SetValue(ToolNameProperty, value); }

    /// <inheritdoc cref="InputProperty"/>
    public string? Input { get => GetValue(InputProperty); set => SetValue(InputProperty, value); }

    /// <inheritdoc cref="OutputProperty"/>
    public string? Output { get => GetValue(OutputProperty); set => SetValue(OutputProperty, value); }

    /// <inheritdoc cref="FilesProperty"/>
    public IReadOnlyList<AgentRunFile>? Files { get => GetValue(FilesProperty); set => SetValue(FilesProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public AgentRunStepStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan? Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <summary>Whether the step has anything to show when opened.</summary>
    internal bool HasDetail => Kind switch
    {
        AgentRunStepKind.Thinking or AgentRunStepKind.Message => !string.IsNullOrWhiteSpace(Text),
        AgentRunStepKind.ToolCall => !string.IsNullOrWhiteSpace(Input) || !string.IsNullOrWhiteSpace(Output),
        _ => Files is { Count: > 0 }
    };
}

/// <summary>Event data of <see cref="AgentRun.StepToggled"/>: the step whose detail opened or closed.</summary>
public sealed class AgentRunStepEventArgs(RoutedEvent routedEvent, AgentRunStep step, bool isExpanded) : RoutedEventArgs(routedEvent)
{
    /// <summary>The step.</summary>
    public AgentRunStep Step { get; } = step;

    /// <summary>Whether its detail is now open.</summary>
    public bool IsExpanded { get; } = isExpanded;
}
