namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ReplyDraft"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; the words that carry a name or a count are format functions.
/// </summary>
public sealed class ReplyDraftLabels
{
    /// <summary>The English defaults.</summary>
    public static ReplyDraftLabels Default { get; } = new();

    /// <summary>The card's accessible name, from the sender ("Email from" and the name, or "Email" without one).</summary>
    public Func<string, string> Article { get; init; } = sender => string.IsNullOrWhiteSpace(sender) ? "Email" : "Email from " + sender;

    /// <summary>The recipient line when the message has no <see cref="ReplyDraftMessage.To"/>.</summary>
    public string ToMe { get; init; } = "to me";

    /// <summary>The time the email arrived, as shown in its header.</summary>
    public Func<DateTimeOffset, string> ReceivedAt { get; init; } =
        time => time.ToLocalTime().ToString("MMM d, h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The reply area's accessible name.</summary>
    public string ReplySection { get; init; } = "AI reply draft";

    /// <summary>The reply area's heading, from the model's name ("Reply with" and the name, or "Reply" without one).</summary>
    public Func<string, string> ReplyWith { get; init; } = model => string.IsNullOrWhiteSpace(model) ? "Reply" : "Reply with " + model;

    /// <summary>The quick replies row's accessible name.</summary>
    public string QuickReplies { get; init; } = "Quick replies";

    /// <summary>The draft text's accessible name.</summary>
    public string DraftText { get; init; } = "Draft reply";

    /// <summary>The tone group's label.</summary>
    public string Tone { get; init; } = "Tone";

    /// <summary>The length group's label.</summary>
    public string Length { get; init; } = "Length";

    /// <summary>The tone option names.</summary>
    public string Friendly { get; init; } = "Friendly";

    /// <inheritdoc cref="Friendly"/>
    public string Concise { get; init; } = "Concise";

    /// <inheritdoc cref="Friendly"/>
    public string Formal { get; init; } = "Formal";

    /// <summary>The length option names.</summary>
    public string Short { get; init; } = "Short";

    /// <inheritdoc cref="Short"/>
    public string Medium { get; init; } = "Medium";

    /// <inheritdoc cref="Short"/>
    public string Long { get; init; } = "Long";

    /// <summary>
    /// The empty state's line, from the model's name, when the card does not start by itself or has nothing to draft
    /// from; without a model it says no reply was drafted yet.
    /// </summary>
    public Func<string, string> EmptyPrompt { get; init; } = model =>
        string.IsNullOrWhiteSpace(model) ? "No reply drafted yet." : model + " can draft a reply to this email.";

    /// <summary>The empty state's button.</summary>
    public string DraftReply { get; init; } = "Draft reply";

    /// <summary>The line in the draft box while the model reads, from the sender's first name ("Reading Maya's email").</summary>
    public Func<string, string> Reading { get; init; } = sender => "Reading " + sender + "'s email";

    /// <summary>The status while a fresh draft is written.</summary>
    public string Drafting { get; init; } = "Drafting";

    /// <summary>The status while a tone or length rewrite runs.</summary>
    public string Rewriting { get; init; } = "Rewriting";

    /// <summary>The status once the draft is finished and editable.</summary>
    public string Ready { get; init; } = "Ready to edit";

    /// <summary>The status before the first draft.</summary>
    public string NoDraft { get; init; } = "No draft yet";

    /// <summary>The status while the insert handler runs.</summary>
    public string Inserting { get; init; } = "Inserting";

    /// <summary>The status once the draft is inserted.</summary>
    public string Inserted { get; init; } = "Inserted";

    /// <summary>The status after a failed draft or insert.</summary>
    public string Failed { get; init; } = "Couldn't draft";

    /// <summary>The status after a failed insert.</summary>
    public string InsertFailed { get; init; } = "Couldn't insert";

    /// <summary>The message of a failure that brought none of its own.</summary>
    public string FailedMessage { get; init; } = "The model could not write a reply.";

    /// <summary>The retry button of a failed draft.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The Regenerate button.</summary>
    public string Regenerate { get; init; } = "Regenerate";

    /// <summary>The Insert button.</summary>
    public string Insert { get; init; } = "Insert";

    /// <summary>The keyboard hint beside the buttons; null works it out from the platform (⌘ Enter or Ctrl Enter).</summary>
    public string? InsertShortcut { get; init; }

    /// <summary>The words added when the card runs its built-in simulation.</summary>
    public string Simulated { get; init; } = "Simulated, nothing was sent";

    /// <summary>Said when a fresh draft starts, from the model's name.</summary>
    public Func<string, string> DraftingAnnouncement { get; init; } = model =>
        string.IsNullOrWhiteSpace(model) ? "Drafting a reply" : model + " is drafting a reply";

    /// <summary>Said when a rewrite starts, from the tone and length names ("Rewriting: Formal, Short").</summary>
    public Func<string, string, string> RewritingAnnouncement { get; init; } = (tone, length) => "Rewriting: " + tone + ", " + length;

    /// <summary>Said when a draft or rewrite is finished.</summary>
    public string ReadyAnnouncement { get; init; } = "Draft ready. You can edit it before inserting";

    /// <summary>Said when the draft is inserted.</summary>
    public string InsertedAnnouncement { get; init; } = "Draft inserted";

    /// <summary>The keyboard hint the card shows.</summary>
    internal string EffectiveInsertShortcut => InsertShortcut ?? (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ? "⌘ Enter" : "Ctrl Enter");

    /// <summary>The name of a tone.</summary>
    public string ToneName(ReplyDraftTone tone) => tone switch
    {
        ReplyDraftTone.Concise => Concise,
        ReplyDraftTone.Formal => Formal,
        _ => Friendly,
    };

    /// <summary>The name of a length.</summary>
    public string LengthName(ReplyDraftLength length) => length switch
    {
        ReplyDraftLength.Short => Short,
        ReplyDraftLength.Long => Long,
        _ => Medium,
    };
}
