using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a question came from.</summary>
public enum AiAnswerQuestionOrigin
{
    /// <summary>The follow-up box.</summary>
    Composer,

    /// <summary>A related question under an answer.</summary>
    Related,

    /// <summary>A suggestion of the empty page.</summary>
    Suggestion,

    /// <summary>Code called <c>AskAsync</c> or set <c>Question</c>.</summary>
    Programmatic
}

/// <summary>Arguments of <c>AiAnswer.QuestionAsked</c>: a question is about to be answered; set <see cref="Cancel"/> to stop it.</summary>
public class AiAnswerQuestionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiAnswerQuestionEventArgs(RoutedEvent routedEvent, object source, string question, AiAnswerQuestionOrigin origin)
        : base(routedEvent, source)
    {
        Question = question;
        Origin = origin;
    }

    /// <summary>The question.</summary>
    public string Question { get; }

    /// <summary>Where it came from.</summary>
    public AiAnswerQuestionOrigin Origin { get; }

    /// <summary>Set to true to ask nothing.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Arguments of <c>AiAnswer.FeedbackChanged</c>.</summary>
public class AiAnswerFeedbackEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiAnswerFeedbackEventArgs(RoutedEvent routedEvent, object source, AiAnswerFeedbackValue value, string? reason, string question,
        AiAnswerVersion version, int versionIndex)
        : base(routedEvent, source)
    {
        Value = value;
        Reason = reason;
        Question = question;
        Version = version;
        VersionIndex = versionIndex;
    }

    /// <summary>The rating; <see cref="AiAnswerFeedbackValue.None"/> when it was taken back.</summary>
    public AiAnswerFeedbackValue Value { get; }

    /// <summary>The reason chosen for a down rating.</summary>
    public string? Reason { get; }

    /// <summary>The question.</summary>
    public string Question { get; }

    /// <summary>The rated version (answer, model).</summary>
    public AiAnswerVersion Version { get; }

    /// <summary>Its index in the versions.</summary>
    public int VersionIndex { get; }
}

/// <summary>Arguments of <c>AiAnswer.SourceInvoked</c>: a source card or a citation was pressed; set <c>Handled</c> to open it yourself.</summary>
public class AiAnswerSourceEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiAnswerSourceEventArgs(RoutedEvent routedEvent, object source, AiSource item, bool fromCitation)
        : base(routedEvent, source)
    {
        Item = item;
        FromCitation = fromCitation;
    }

    /// <summary>The source.</summary>
    public AiSource Item { get; }

    /// <summary>True when a citation in the text was pressed, false for a card.</summary>
    public bool FromCitation { get; }
}

/// <summary>What was copied.</summary>
public enum AiAnswerCopyKind
{
    /// <summary>The answer text, with its sources.</summary>
    Answer,

    /// <summary>The link to the answer.</summary>
    ShareLink,

    /// <summary>The link to the thread.</summary>
    ThreadLink,

    /// <summary>A code block.</summary>
    Code
}

/// <summary>Arguments of <c>AiAnswer.Copied</c>.</summary>
public class AiAnswerCopiedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiAnswerCopiedEventArgs(RoutedEvent routedEvent, object source, AiAnswerCopyKind kind, string text)
        : base(routedEvent, source)
    {
        Kind = kind;
        Text = text;
    }

    /// <summary>What was copied.</summary>
    public AiAnswerCopyKind Kind { get; }

    /// <summary>The text that went to the clipboard.</summary>
    public string Text { get; }
}

/// <summary>Arguments of <c>AiAnswer.StatusChanged</c>.</summary>
public class AiAnswerStatusEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiAnswerStatusEventArgs(RoutedEvent routedEvent, object source, AiAnswerStatus oldStatus, AiAnswerStatus newStatus)
        : base(routedEvent, source)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    /// <summary>The status before.</summary>
    public AiAnswerStatus OldStatus { get; }

    /// <summary>The status now.</summary>
    public AiAnswerStatus NewStatus { get; }
}

/// <summary>What <c>AiAnswer.ShareLinkProvider</c> is asked for.</summary>
/// <param name="Question">The question.</param>
/// <param name="AnswerMarkdown">The shown answer.</param>
/// <param name="VersionIndex">The shown version for the link of one answer; null for the link of the whole thread.</param>
public sealed record AiAnswerShareInfo(string Question, string AnswerMarkdown, int? VersionIndex);
