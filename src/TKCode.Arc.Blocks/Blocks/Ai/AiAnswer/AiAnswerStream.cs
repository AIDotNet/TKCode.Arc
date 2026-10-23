using System.Collections.Generic;
using System.Threading;

namespace TKCode.Arc;

/// <summary>Writes an answer as a stream of events. <c>AiAnswer</c> shows them as they arrive.</summary>
public interface IAiAnswerResponder
{
    /// <summary>
    /// Answers <paramref name="request"/>. Throw (other than <see cref="OperationCanceledException"/>) to fail the answer;
    /// the token is cancelled when the person stops it, asks something else or leaves.
    /// </summary>
    /// <param name="request">What to answer.</param>
    /// <param name="cancellationToken">Cancelled when the answer is stopped or replaced.</param>
    IAsyncEnumerable<AiAnswerEvent> RespondAsync(AiAnswerRequest request, CancellationToken cancellationToken);
}

/// <summary>What a responder is asked.</summary>
/// <param name="Question">The question.</param>
/// <param name="Mode">How the answer is asked for.</param>
/// <param name="Model">The model to use.</param>
/// <param name="PreviousAnswerMarkdown">The answer being replaced (for try again, concise, detailed and switch model), if any.</param>
public sealed record AiAnswerRequest(string Question, AiAnswerMode Mode, AiModel? Model, string? PreviousAnswerMarkdown);

/// <summary>Something a responder reports while it answers.</summary>
public abstract record AiAnswerEvent;

/// <summary>More reasoning text.</summary>
/// <param name="TextDelta">The text, of any size.</param>
public sealed record AiAnswerReasoningEvent(string TextDelta) : AiAnswerEvent;

/// <summary>The sources are known; send them early, while the model still thinks, so the row fills at once.</summary>
/// <param name="Sources">The sources, numbered from 1 in order unless their <see cref="AiSource.Index"/> is set.</param>
public sealed record AiAnswerSourcesEvent(IReadOnlyList<AiSource> Sources) : AiAnswerEvent;

/// <summary>The model is done thinking and the text starts (the label turns to "Thought for 3s").</summary>
/// <param name="Duration">How long it thought; omit it to use the measured time to the first text.</param>
public sealed record AiAnswerThoughtEvent(TimeSpan? Duration = null) : AiAnswerEvent;

/// <summary>More of the answer text, of any size.</summary>
/// <param name="MarkdownDelta">The Markdown.</param>
public sealed record AiAnswerTextEvent(string MarkdownDelta) : AiAnswerEvent;

/// <summary>The answer is complete.</summary>
/// <param name="Tokens">How many tokens it used.</param>
/// <param name="Duration">The whole time.</param>
/// <param name="FollowUps">Questions to ask next; they replace those of the version.</param>
public sealed record AiAnswerDoneEvent(int? Tokens = null, TimeSpan? Duration = null, IReadOnlyList<string>? FollowUps = null) : AiAnswerEvent;
