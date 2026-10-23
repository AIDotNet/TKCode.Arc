using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TKCode.Arc;

/// <summary>Answers a question about a page, and may propose a change to one block of it.</summary>
public interface IAiPanelResponder
{
    /// <summary>
    /// Answers <paramref name="request"/>. While this runs the panel shows "Reading the page"; the first text it receives starts the answer.
    /// Throw (other than <see cref="OperationCanceledException"/>) to fail the turn; the token is cancelled when the person stops it.
    /// </summary>
    /// <param name="request">The question, the page and the selection.</param>
    /// <param name="cancellationToken">Cancelled when the turn is stopped.</param>
    ValueTask<AiPanelReply> RespondAsync(AiPanelRequest request, CancellationToken cancellationToken);
}

/// <summary>What a panel responder is asked.</summary>
/// <param name="Prompt">The question.</param>
/// <param name="Page">The page as it is now, with the changes the person applied.</param>
/// <param name="Selection">The selection the question is about, if any.</param>
public sealed record AiPanelRequest(string Prompt, AiPanelPage Page, AiPanelSelection? Selection);

/// <summary>The block a reply proposes to replace.</summary>
/// <param name="BlockId">The id of the block.</param>
/// <param name="After">The whole new block (same kind and id).</param>
public sealed record AiPanelChangeProposal(string BlockId, AiPanelBlock After);

/// <summary>An answer: its text, which arrives as a stream or whole, and an optional change.</summary>
public sealed class AiPanelReply
{
    /// <summary>Creates a reply whose text arrives as a stream.</summary>
    /// <param name="text">The text increments, of any size; the panel releases them in whole words on a steady clock.</param>
    /// <param name="change">The proposed change; read after the text ends.</param>
    public AiPanelReply(IAsyncEnumerable<string> text, AiPanelChangeProposal? change = null)
    {
        Text = text;
        Change = change;
    }

    /// <summary>The text increments.</summary>
    public IAsyncEnumerable<string> Text { get; }

    /// <summary>The proposed change.</summary>
    public AiPanelChangeProposal? Change { get; }

    /// <summary>Creates a reply from a whole text; the panel cuts it into words and releases them.</summary>
    /// <param name="text">The text.</param>
    /// <param name="change">The proposed change.</param>
    public static AiPanelReply FromText(string text, AiPanelChangeProposal? change = null) => new(Single(text), change);

    private static async IAsyncEnumerable<string> Single(string text)
    {
        yield return text;
        await Task.CompletedTask;
    }
}
