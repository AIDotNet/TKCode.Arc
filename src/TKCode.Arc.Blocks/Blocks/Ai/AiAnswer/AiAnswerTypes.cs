using Avalonia;
using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>Where an answer is in its life.</summary>
public enum AiAnswerStatus
{
    /// <summary>No question yet: the empty page with its suggestions.</summary>
    Empty,

    /// <summary>The model is thinking; the reasoning streams.</summary>
    Thinking,

    /// <summary>The answer text is being written.</summary>
    Writing,

    /// <summary>The answer is complete.</summary>
    Ready,

    /// <summary>The person stopped it; what was written stays, marked as possibly incomplete.</summary>
    Stopped,

    /// <summary>It failed; <c>ErrorMessage</c> says why.</summary>
    Failed
}

/// <summary>How an answer was asked for; carried in <see cref="AiAnswerRequest"/> and kept on every version.</summary>
public enum AiAnswerMode
{
    /// <summary>The first answer to a question.</summary>
    Answer,

    /// <summary>"Try again": the same question once more.</summary>
    Retry,

    /// <summary>"More concise".</summary>
    Concise,

    /// <summary>"More detailed".</summary>
    Detailed,

    /// <summary>"Switch model": the same question with another model.</summary>
    SwitchModel
}

/// <summary>How a version ended.</summary>
public enum AiAnswerOutcome
{
    /// <summary>It ran to the end.</summary>
    Complete,

    /// <summary>The person stopped it.</summary>
    Stopped,

    /// <summary>It failed.</summary>
    Failed
}

/// <summary>The person's rating of a version.</summary>
public enum AiAnswerFeedbackValue
{
    /// <summary>No rating.</summary>
    None,

    /// <summary>A good answer.</summary>
    Up,

    /// <summary>A bad answer; the reason is asked after.</summary>
    Down
}

/// <summary>What a finished answer cost.</summary>
/// <param name="Tokens">The number of tokens the answer used.</param>
/// <param name="ThoughtTime">How long the model thought before the first text.</param>
/// <param name="Duration">The whole time to the end.</param>
public sealed record AiAnswerUsage(int? Tokens = null, TimeSpan? ThoughtTime = null, TimeSpan? Duration = null);

/// <summary>
/// One answer to a question: the model, the text, the reasoning, the sources, what it cost, how it ended and the rating.
/// Asking again (try again, more concise, more detailed, another model) adds a version; the person pages between them.
/// </summary>
public class AiAnswerVersion : AvaloniaObject
{
    /// <summary>How the answer was asked for.</summary>
    public static readonly StyledProperty<AiAnswerMode> ModeProperty =
        AvaloniaProperty.Register<AiAnswerVersion, AiAnswerMode>(nameof(Mode), AiAnswerMode.Answer);

    /// <summary>The model that wrote it.</summary>
    public static readonly StyledProperty<AiModel?> ModelProperty =
        AvaloniaProperty.Register<AiAnswerVersion, AiModel?>(nameof(Model));

    /// <summary>The answer in the Markdown subset: paragraphs, short headings, ordered and bullet lists, a table, code blocks, bold, inline code and "[n]" citations.</summary>
    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<AiAnswerVersion, string>(nameof(Markdown), string.Empty);

    /// <summary>The reasoning summary, in paragraphs separated by blank lines.</summary>
    public static readonly StyledProperty<string> ReasoningProperty =
        AvaloniaProperty.Register<AiAnswerVersion, string>(nameof(Reasoning), string.Empty);

    /// <summary>What it cost.</summary>
    public static readonly StyledProperty<AiAnswerUsage?> UsageProperty =
        AvaloniaProperty.Register<AiAnswerVersion, AiAnswerUsage?>(nameof(Usage));

    /// <summary>How it ended.</summary>
    public static readonly StyledProperty<AiAnswerOutcome> OutcomeProperty =
        AvaloniaProperty.Register<AiAnswerVersion, AiAnswerOutcome>(nameof(Outcome), AiAnswerOutcome.Complete);

    /// <summary>Why it failed.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<AiAnswerVersion, string?>(nameof(ErrorMessage));

    /// <summary>The rating.</summary>
    public static readonly StyledProperty<AiAnswerFeedbackValue> FeedbackProperty =
        AvaloniaProperty.Register<AiAnswerVersion, AiAnswerFeedbackValue>(nameof(Feedback));

    /// <summary>Why the person rated it down.</summary>
    public static readonly StyledProperty<string?> FeedbackReasonProperty =
        AvaloniaProperty.Register<AiAnswerVersion, string?>(nameof(FeedbackReason));

    /// <summary>How the answer was asked for.</summary>
    public AiAnswerMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>The model that wrote it.</summary>
    public AiModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <summary>The answer text.</summary>
    public string Markdown { get => GetValue(MarkdownProperty); set => SetValue(MarkdownProperty, value); }

    /// <summary>The reasoning summary.</summary>
    public string Reasoning { get => GetValue(ReasoningProperty); set => SetValue(ReasoningProperty, value); }

    /// <summary>What it cost.</summary>
    public AiAnswerUsage? Usage { get => GetValue(UsageProperty); set => SetValue(UsageProperty, value); }

    /// <summary>How it ended.</summary>
    public AiAnswerOutcome Outcome { get => GetValue(OutcomeProperty); set => SetValue(OutcomeProperty, value); }

    /// <summary>Why it failed.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <summary>The rating.</summary>
    public AiAnswerFeedbackValue Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

    /// <summary>Why the person rated it down.</summary>
    public string? FeedbackReason { get => GetValue(FeedbackReasonProperty); set => SetValue(FeedbackReasonProperty, value); }

    /// <summary>The sources the answer cites, numbered from 1.</summary>
    public AvaloniaList<AiSource> Sources { get; } = new();

    /// <summary>Questions to ask next.</summary>
    public AvaloniaList<string> FollowUps { get; } = new();
}
