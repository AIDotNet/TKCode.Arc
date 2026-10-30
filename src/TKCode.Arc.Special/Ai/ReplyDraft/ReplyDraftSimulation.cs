namespace TKCode.Arc;

/// <summary>
/// An email a <see cref="ReplyDraft"/> plays without a generator, from data you give it: the received message, the quick
/// replies, the model credited and the replies you wrote for each quick reply, tone and length. Set it as
/// <see cref="ReplyDraft.Simulation"/> while the card has no <see cref="ReplyDraft.Message"/>: the card shows the email,
/// streams the matching reply after a short read (3 to 8 words every 34ms), says it is simulated (nothing is sent) and,
/// unless <see cref="ReplyDraft.AutoPlay"/> is false, tours tones, length, a second quick reply and Insert on a loop.
/// </summary>
public sealed class ReplyDraftSimulation
{
    /// <summary>The received email.</summary>
    public ReplyDraftMessage? Message { get; set; }

    /// <summary>The quick replies shown as chips; null or empty hides the row.</summary>
    public IReadOnlyList<ReplyDraftSuggestion>? Suggestions { get; set; }

    /// <summary>The model credited on the reply when the card has no <see cref="ReplyDraft.Model"/>.</summary>
    public AgentModel? Model { get; set; }

    /// <summary>
    /// The replies, one per quick reply, tone and length. A request takes the reply for its quick reply, tone and length;
    /// else one for its tone and length with no quick reply; else the first for its tone and length.
    /// </summary>
    public IReadOnlyList<ReplyDraftVersion>? Replies { get; set; }

    /// <summary>
    /// Streams the reply for <paramref name="request"/> the way the card plays it: after a short read (longer for a fresh
    /// draft than for a rewrite), in chunks of 3 to 8 words every 34ms. It sends nothing and honours the request's token.
    /// Use it as a <see cref="ReplyDraft.Generator"/> to wrap the simulation in code of your own.
    /// </summary>
    /// <param name="request">What to write.</param>
    public async IAsyncEnumerable<string> Generate(ReplyDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = request.CancellationToken;
        await Task.Delay(request.Mode == ReplyDraftMode.Rewrite ? ReplyDraftStreams.RewriteDelay : ReplyDraftStreams.ReadDelay, token)
            .ConfigureAwait(true);
        await foreach (var chunk in ReplyDraftStreams.Chunk(Reply(request), token).ConfigureAwait(true))
        {
            yield return chunk;
        }
    }

    /// <summary>The reply for the request's quick reply, tone and length (empty when there is none).</summary>
    internal string Reply(ReplyDraftRequest request)
    {
        if (Replies is not { Count: > 0 } replies)
        {
            return string.Empty;
        }
        var id = request.Suggestion?.Id;
        ReplyDraftVersion? unsteered = null;
        ReplyDraftVersion? any = null;
        foreach (var reply in replies)
        {
            if (reply is null || reply.Tone != request.Tone || reply.Length != request.Length)
            {
                continue;
            }
            if (reply.SuggestionId == id)
            {
                return reply.Text ?? string.Empty;
            }
            unsteered ??= reply.SuggestionId is null ? reply : null;
            any ??= reply;
        }
        return (unsteered ?? any)?.Text ?? string.Empty;
    }
}

/// <summary>One reply of a <see cref="ReplyDraftSimulation"/>: the quick reply, tone and length it answers, and its text.</summary>
/// <param name="SuggestionId">The <see cref="ReplyDraftSuggestion.Id"/> it answers, or null for any quick reply.</param>
/// <param name="Tone">The tone it is written in.</param>
/// <param name="Length">Its length.</param>
/// <param name="Text">The whole reply, greeting and sign off included; blank lines split paragraphs.</param>
public sealed record ReplyDraftVersion(string? SuggestionId, ReplyDraftTone Tone, ReplyDraftLength Length, string Text);
