using System.Collections.Generic;
using System.Threading;

namespace TKCode.Arc;

/// <summary>Writes the reply to a prompt as a stream of events. <c>AiChat</c> shows them as they arrive.</summary>
public interface IAiChatResponder
{
    /// <summary>
    /// Answers the last message of <paramref name="request"/>. Throw (other than <see cref="OperationCanceledException"/>) or
    /// send an <see cref="AiChatErrorEvent"/> to fail the reply; the token is cancelled when the person stops it.
    /// </summary>
    /// <param name="request">The messages of the shown branch and the selected model.</param>
    /// <param name="cancellationToken">Cancelled when the reply is stopped, replaced or the chat is unloaded.</param>
    IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AiChatRequest request, CancellationToken cancellationToken);
}

/// <summary>What a responder is asked: the conversation as it is shown (the selected version of every turn).</summary>
/// <param name="Conversation">The conversation.</param>
/// <param name="Model">The selected model, if any.</param>
/// <param name="Messages">The messages in order, user and assistant alternating, ending with the prompt to answer.</param>
public sealed record AiChatRequest(AiChatConversation Conversation, AiModel? Model, IReadOnlyList<AiChatRequestMessage> Messages);

/// <summary>One message of a <see cref="AiChatRequest"/>.</summary>
/// <param name="Role">Who wrote it.</param>
/// <param name="Content">Its text.</param>
/// <param name="Attachments">The files of a user message.</param>
public sealed record AiChatRequestMessage(AiChatRole Role, string Content, IReadOnlyList<AiAttachment> Attachments);

/// <summary>Something a responder reports while it answers.</summary>
public abstract record AiChatStreamEvent;

/// <summary>More reasoning text.</summary>
/// <param name="Text">The text, of any size.</param>
public sealed record AiChatReasoningDelta(string Text) : AiChatStreamEvent;

/// <summary>The reasoning is over (the label turns to "Thought for 3s").</summary>
/// <param name="Duration">How long the model thought; null uses the measured time.</param>
public sealed record AiChatReasoningEnd(TimeSpan? Duration = null) : AiChatStreamEvent;

/// <summary>A tool call started or changed; events with the same id change the same tool.</summary>
public sealed record AiChatToolUpdate(string Id, AiChatToolKind Kind, AiChatToolStatus Status, string Label, string? Summary = null,
    IReadOnlyList<string>? Queries = null, IReadOnlyList<AiSource>? Sources = null, string? Input = null, string? Output = null) : AiChatStreamEvent;

/// <summary>Sources the text will cite. Send them before the text that cites them; they are numbered in order from 1.</summary>
/// <param name="Sources">The sources.</param>
public sealed record AiChatSourcesEvent(IReadOnlyList<AiSource> Sources) : AiChatStreamEvent;

/// <summary>More of the reply text, of any size; the chat paces it into whole words.</summary>
/// <param name="Text">The text.</param>
public sealed record AiChatTextDelta(string Text) : AiChatStreamEvent;

/// <summary>The reply failed.</summary>
/// <param name="Message">What to tell the person.</param>
public sealed record AiChatErrorEvent(string Message) : AiChatStreamEvent;
