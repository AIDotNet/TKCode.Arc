using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The voice of a <see cref="ReplyDraft"/> reply.</summary>
public enum ReplyDraftTone
{
    /// <summary>Warm and personal (the default).</summary>
    Friendly,

    /// <summary>Straight to the point, no pleasantries.</summary>
    Concise,

    /// <summary>Polite and businesslike.</summary>
    Formal
}

/// <summary>How long a <see cref="ReplyDraft"/> reply is.</summary>
public enum ReplyDraftLength
{
    /// <summary>One or two sentences.</summary>
    Short,

    /// <summary>A short paragraph (the default).</summary>
    Medium,

    /// <summary>A fuller reply with the details spelled out.</summary>
    Long
}

/// <summary>What a <see cref="ReplyDraftRequest"/> asks the generator for.</summary>
public enum ReplyDraftMode
{
    /// <summary>A fresh draft, streamed into the draft box (the first draft, a quick reply, Regenerate).</summary>
    Draft,

    /// <summary>
    /// Restyle <see cref="ReplyDraftRequest.Previous"/> (the current text, with the reader's edits) for a new tone or length.
    /// The card collects the whole text and swaps it in with one crossfade.
    /// </summary>
    Rewrite
}

/// <summary>
/// The received email a <see cref="ReplyDraft"/> answers: who sent it, the subject, the body (blank lines split paragraphs)
/// and when it arrived. Data only.
/// </summary>
public sealed class ReplyDraftMessage
{
    /// <summary>The sender's display name ("Maya Chen"); it also names the card.</summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>The sender's address, shown after the name.</summary>
    public string? FromEmail { get; set; }

    /// <summary>The sender's picture; null shows their initials.</summary>
    public ImageSource? FromAvatar { get; set; }

    /// <summary>Who it was sent to ("me", "Design team"); null shows <see cref="ReplyDraftLabels.ToMe"/>.</summary>
    public string? To { get; set; }

    /// <summary>The subject line.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The body as plain text; blank lines split paragraphs.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>When it arrived; null hides the time.</summary>
    public DateTimeOffset? ReceivedAt { get; set; }

    /// <summary>The body's paragraphs: split on blank lines, trimmed, empty ones dropped.</summary>
    public IReadOnlyList<string> Paragraphs =>
        Body.Replace("\r\n", "\n").Split("\n\n").Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
}

/// <summary>A quick reply chip of a <see cref="ReplyDraft"/>: a stable id the prompt understands and the label readers see.</summary>
public sealed class ReplyDraftSuggestion
{
    /// <summary>Creates an empty suggestion; set <see cref="Id"/> and <see cref="Label"/>.</summary>
    public ReplyDraftSuggestion()
    {
    }

    /// <summary>Creates a suggestion.</summary>
    /// <param name="id">A stable id your prompt maps to an intent.</param>
    /// <param name="label">What readers see: two or three words.</param>
    public ReplyDraftSuggestion(string id, string label)
    {
        Id = id;
        Label = label;
    }

    /// <summary>A stable id your prompt maps to an intent ("yes", "later").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>What readers see on the chip: two or three words ("Sounds good").</summary>
    public string Label { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>What a <see cref="ReplyDraftGenerator"/> is asked to write.</summary>
public sealed class ReplyDraftRequest
{
    /// <summary>Creates a request.</summary>
    public ReplyDraftRequest(ReplyDraftMessage message, ReplyDraftTone tone, ReplyDraftLength length, ReplyDraftSuggestion? suggestion,
        ReplyDraftMode mode, string? previous, CancellationToken cancellationToken)
    {
        Message = message;
        Tone = tone;
        Length = length;
        Suggestion = suggestion;
        Mode = mode;
        Previous = previous;
        CancellationToken = cancellationToken;
    }

    /// <summary>The email being answered.</summary>
    public ReplyDraftMessage Message { get; }

    /// <summary>The tone to write in.</summary>
    public ReplyDraftTone Tone { get; }

    /// <summary>The length to aim for.</summary>
    public ReplyDraftLength Length { get; }

    /// <summary>The quick reply steering the draft, or null when there are none.</summary>
    public ReplyDraftSuggestion? Suggestion { get; }

    /// <summary>A fresh draft, or a rewrite of <see cref="Previous"/>.</summary>
    public ReplyDraftMode Mode { get; }

    /// <summary>The text on screen with the reader's edits (a rewrite restyles it); null when there is none.</summary>
    public string? Previous { get; }

    /// <summary>Cancelled when a newer action replaces this request or the card leaves the window: pass it to your client.</summary>
    public CancellationToken CancellationToken { get; }
}

/// <summary>
/// Writes a <see cref="ReplyDraft"/> reply as a stream of text deltas. Yield chunks from your model's stream, or wrap a
/// whole text with <see cref="ReplyDraftStreams.FromText"/> to have it streamed locally in chunks. Throw to show the error
/// state; honour <see cref="ReplyDraftRequest.CancellationToken"/>.
/// </summary>
/// <param name="request">What to write.</param>
public delegate IAsyncEnumerable<string> ReplyDraftGenerator(ReplyDraftRequest request);

/// <summary>
/// Puts a finished <see cref="ReplyDraft"/> reply into your composer. While the returned task runs, Insert shows a
/// spinner; a fault shows the error in the status line and keeps the draft.
/// </summary>
/// <param name="draft">The draft, with the reader's edits, and how it was written.</param>
public delegate Task ReplyDraftInsertHandler(ReplyDraftEventArgs draft);

/// <summary>
/// Arguments of <see cref="ReplyDraft.DraftCompleted"/> and <see cref="ReplyDraft.InsertRequested"/>: the text and how it was
/// written.
/// </summary>
public sealed class ReplyDraftEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ReplyDraftEventArgs(RoutedEvent? routedEvent, string draft, ReplyDraftTone tone, ReplyDraftLength length,
        ReplyDraftSuggestion? suggestion, ReplyDraftMode mode)
        : base(routedEvent)
    {
        Draft = draft;
        Tone = tone;
        Length = length;
        Suggestion = suggestion;
        Mode = mode;
    }

    /// <summary>The reply text, including the reader's edits.</summary>
    public string Draft { get; }

    /// <summary>The tone it was written in.</summary>
    public ReplyDraftTone Tone { get; }

    /// <summary>The length it was written for.</summary>
    public ReplyDraftLength Length { get; }

    /// <summary>The quick reply that steered it, or null.</summary>
    public ReplyDraftSuggestion? Suggestion { get; }

    /// <summary>Whether the text came from a fresh draft or a rewrite.</summary>
    public ReplyDraftMode Mode { get; }
}

/// <summary>Arguments of <see cref="ReplyDraft.SuggestionSelected"/>.</summary>
public sealed class ReplyDraftSuggestionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ReplyDraftSuggestionEventArgs(RoutedEvent routedEvent, ReplyDraftSuggestion suggestion) : base(routedEvent)
    {
        Suggestion = suggestion;
    }

    /// <summary>The quick reply that was chosen.</summary>
    public ReplyDraftSuggestion Suggestion { get; }
}

/// <summary>Arguments of <see cref="ReplyDraft.ToneChanged"/>.</summary>
public sealed class ReplyDraftToneChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ReplyDraftToneChangedEventArgs(RoutedEvent routedEvent, ReplyDraftTone tone) : base(routedEvent)
    {
        Tone = tone;
    }

    /// <summary>The tone picked.</summary>
    public ReplyDraftTone Tone { get; }
}

/// <summary>Arguments of <see cref="ReplyDraft.LengthChanged"/>.</summary>
public sealed class ReplyDraftLengthChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ReplyDraftLengthChangedEventArgs(RoutedEvent routedEvent, ReplyDraftLength length) : base(routedEvent)
    {
        Length = length;
    }

    /// <summary>The length picked.</summary>
    public ReplyDraftLength Length { get; }
}
