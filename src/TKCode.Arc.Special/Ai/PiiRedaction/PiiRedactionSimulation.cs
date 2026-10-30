namespace TKCode.Arc;

/// <summary>
/// A conversation a <see cref="PiiRedaction"/> plays without a model, from data you give it: the message, the names the
/// built-in detector should always flag, the model the safe prompt goes to and the model's reply with its placeholders.
/// Set it as <see cref="PiiRedaction.Simulation"/>: while the card has no <see cref="PiiRedaction.Message"/> it shows
/// this message and, unless <see cref="PiiRedaction.AutoPlay"/> is false, plays scan, send and restore on a loop. Without
/// a <see cref="PiiRedaction.Sender"/> the reply is simulated and the card says so; nothing is sent.
/// </summary>
public sealed class PiiRedactionSimulation
{
    /// <summary>The message to check.</summary>
    public string? Message { get; set; }

    /// <summary>Names the built-in detector always flags, when the card has no <see cref="PiiRedaction.Names"/>.</summary>
    public IReadOnlyList<string>? Names { get; set; }

    /// <summary>The model the safe prompt goes to, when the card has no <see cref="PiiRedaction.ModelName"/>.</summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// The model's reply to <see cref="Message"/>, with placeholders such as <c>[NAME_1]</c> that the guard restores. Null,
    /// or a message that was edited, gets a short stand-in reply that greets the first name and confirms the first contact.
    /// </summary>
    public string? Reply { get; set; }

    /// <summary>
    /// The stand-in reply when there is no written one: it greets the first name and confirms the first email or phone, by
    /// placeholder, so the restore step has something to put back. Not a model.
    /// </summary>
    internal static string StandInReply(IReadOnlyList<PiiEntity> entities)
    {
        string? First(PiiType type) => entities.FirstOrDefault(e => e.Type == type)?.Placeholder;
        var name = First(PiiType.Name);
        var contact = First(PiiType.Email) ?? First(PiiType.Phone);
        var card = First(PiiType.Card);
        var reply = name is null ? "Thanks for reaching out." : "Thanks for reaching out, " + name + ".";
        reply += " I have looked into your request and can help with it.";
        if (card is not null)
        {
            reply += " I will only refer to the card as " + card + ".";
        }
        reply += contact is null
            ? " I will follow up here as soon as I have an update."
            : " I will send an update to " + contact + " as soon as I have one.";
        return reply;
    }

    /// <summary>The written reply restored for the person, the height the reply area reserves; empty without one.</summary>
    internal string RestoredReply()
    {
        if (Reply is not { Length: > 0 } reply || string.IsNullOrEmpty(Message))
        {
            return string.Empty;
        }
        var redaction = PiiRedactor.Redact(Message, PiiRedactor.Detect(Message, Names ?? []));
        return PiiRedactor.Restore(reply, redaction.Entities);
    }
}
