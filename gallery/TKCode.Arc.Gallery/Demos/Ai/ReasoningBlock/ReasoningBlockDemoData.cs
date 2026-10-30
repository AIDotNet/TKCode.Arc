namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented reasoning the reasoning block demos share: a model tracing a double charge to webhook redeliveries.
/// Demo content only; a real app passes what its model has sent.
/// </summary>
internal static class ReasoningBlockDemoData
{
    /// <summary>The whole reasoning: bold headings, paragraphs under them.</summary>
    public const string Reasoning =
        "**Reading the webhook handler**\n\nThe handler in api/billing/webhook.ts checks the signature and then calls fulfillOrder for every invoice.paid event. Nothing records which events it already handled, so a redelivered event runs the whole path again.\n\n" +
        "**Checking when Stripe retries**\n\nStripe sends an event again when the endpoint times out or answers with an error. The logs show fulfillOrder taking 11 seconds, because the receipt PDF renders before the handler answers.\n\n" +
        "**Tracing the second charge**\n\nEvery double charge lines up with a redelivery. Each retry calls createUsageCharge again with a key built from Date.now(), so Stripe sees a new request instead of a repeat.\n\n" +
        "**Choosing a fix**\n\nRecord event.id in a processed_events table inside the same transaction, derive the idempotency key from the invoice id, and move the receipt render to a queue so the handler answers in well under a second.";

    /// <summary>The first two steps, where the interrupted block stopped.</summary>
    public static readonly string Interrupted = Reasoning[..Reasoning.IndexOf("**Tracing", StringComparison.Ordinal)].TrimEnd();

    /// <summary>Why the interrupted block stopped.</summary>
    public const string InterruptedReason = "The connection dropped before the model finished.";

    /// <summary>How long the finished block thought.</summary>
    public static readonly TimeSpan FinishedElapsed = TimeSpan.FromSeconds(7);

    /// <summary>How long the interrupted block thought before it failed.</summary>
    public static readonly TimeSpan InterruptedElapsed = TimeSpan.FromSeconds(3);
}
