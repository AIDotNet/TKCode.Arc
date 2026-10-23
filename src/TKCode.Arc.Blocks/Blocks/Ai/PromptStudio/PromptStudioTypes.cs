using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The part of a prompt template.</summary>
public enum PromptPart
{
    /// <summary>The system message.</summary>
    System,

    /// <summary>The user message.</summary>
    User
}

/// <summary>Where a test run is.</summary>
public enum PromptRunState
{
    /// <summary>Nothing running; the panel says what Run does.</summary>
    Idle,

    /// <summary>Waiting for the first text ("Thinking").</summary>
    Thinking,

    /// <summary>The reply is being written ("Writing").</summary>
    Streaming,

    /// <summary>The reply is complete.</summary>
    Done,

    /// <summary>The person stopped the run.</summary>
    Stopped,

    /// <summary>The run failed; Retry runs it again.</summary>
    Failed
}

/// <summary>A prompt template: the system message and the user message, with <c>{{name}}</c> variables in them.</summary>
/// <param name="System">The system message.</param>
/// <param name="User">The user message.</param>
public readonly record struct PromptTemplate(string System, string User);

/// <summary>Who wrote a version.</summary>
public class PromptAuthor : AvaloniaObject
{
    /// <summary>The name.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<PromptAuthor, string>(nameof(Name), string.Empty);

    /// <summary>The picture; without one the avatar shows the initials.</summary>
    public static readonly StyledProperty<IImage?> AvatarProperty =
        AvaloniaProperty.Register<PromptAuthor, IImage?>(nameof(Avatar));

    /// <summary>The name.</summary>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <summary>The picture.</summary>
    public IImage? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
}

/// <summary>One saved version of the prompt.</summary>
public class PromptVersion : AvaloniaObject
{
    /// <summary>Stable id.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<PromptVersion, string>(nameof(Id), string.Empty);

    /// <summary>The short name ("v4").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PromptVersion, string>(nameof(Label), string.Empty);

    /// <summary>The commit message.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<PromptVersion, string?>(nameof(Message));

    /// <summary>Who wrote it.</summary>
    public static readonly StyledProperty<PromptAuthor?> AuthorProperty =
        AvaloniaProperty.Register<PromptVersion, PromptAuthor?>(nameof(Author));

    /// <summary>When it was written.</summary>
    public static readonly StyledProperty<DateTimeOffset> CreatedAtProperty =
        AvaloniaProperty.Register<PromptVersion, DateTimeOffset>(nameof(CreatedAt));

    /// <summary>The system message of this version.</summary>
    public static readonly StyledProperty<string> SystemProperty =
        AvaloniaProperty.Register<PromptVersion, string>(nameof(System), string.Empty);

    /// <summary>The user message of this version.</summary>
    public static readonly StyledProperty<string> UserProperty =
        AvaloniaProperty.Register<PromptVersion, string>(nameof(User), string.Empty);

    /// <summary>The id of the version.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The short name of the version.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The commit message.</summary>
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <summary>Who wrote it.</summary>
    public PromptAuthor? Author { get => GetValue(AuthorProperty); set => SetValue(AuthorProperty, value); }

    /// <summary>When it was written.</summary>
    public DateTimeOffset CreatedAt { get => GetValue(CreatedAtProperty); set => SetValue(CreatedAtProperty, value); }

    /// <summary>The system message.</summary>
    public string System { get => GetValue(SystemProperty); set => SetValue(SystemProperty, value); }

    /// <summary>The user message.</summary>
    public string User { get => GetValue(UserProperty); set => SetValue(UserProperty, value); }

    /// <summary>The two messages as a template.</summary>
    public PromptTemplate Template => new(System, User);
}

/// <summary>A saved test case: a label and a value for each variable.</summary>
public class PromptTestCase : AvaloniaObject
{
    /// <summary>Stable id.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<PromptTestCase, string>(nameof(Id), string.Empty);

    /// <summary>A short label; all cases share one segmented control.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PromptTestCase, string>(nameof(Label), string.Empty);

    /// <summary>The id of the test case.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The label of the test case.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The value of each variable, by name.</summary>
    public AvaloniaDictionary<string, string> Values { get; } = new();
}

/// <summary>What a run handler is asked.</summary>
/// <param name="Raw">The template as written, with its <c>{{name}}</c> markers.</param>
/// <param name="Rendered">The template with the values filled in.</param>
/// <param name="Values">The value of each variable.</param>
/// <param name="Model">The selected model.</param>
/// <param name="Effort">The selected effort.</param>
/// <param name="TestCaseId">The id of the selected test case, if any.</param>
/// <param name="VersionId">The id of the open version, if any.</param>
public sealed record PromptRunRequest(PromptTemplate Raw, PromptTemplate Rendered, IReadOnlyDictionary<string, string> Values,
    AiModel? Model, ModelEffort Effort, string? TestCaseId, string? VersionId);

/// <summary>What a finished run produced and cost.</summary>
/// <param name="Text">The reply.</param>
/// <param name="Elapsed">How long it took.</param>
/// <param name="InputTokens">The estimated tokens of the rendered prompt.</param>
/// <param name="OutputTokens">The estimated tokens of the reply.</param>
/// <param name="Cost">The cost at the model's prices; null when it has none.</param>
/// <param name="IsSimulated">Whether the built-in simulator wrote the reply.</param>
public sealed record PromptRunResult(string Text, TimeSpan Elapsed, int InputTokens, int OutputTokens, decimal? Cost, bool IsSimulated);

/// <summary>Runs a prompt and returns the reply as a stream of text increments.</summary>
/// <param name="request">What to run.</param>
/// <param name="cancellationToken">Cancelled when the run is stopped or replaced.</param>
public delegate IAsyncEnumerable<string> PromptRunHandler(PromptRunRequest request, CancellationToken cancellationToken);
