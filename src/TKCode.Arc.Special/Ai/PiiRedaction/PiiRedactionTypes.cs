namespace TKCode.Arc;

/// <summary>The kinds of personal data a <see cref="PiiRedaction"/> marks and swaps for placeholders.</summary>
public enum PiiType
{
    /// <summary>A person's name, swapped for <c>[NAME_1]</c>.</summary>
    Name,

    /// <summary>An email address, swapped for <c>[EMAIL_1]</c>.</summary>
    Email,

    /// <summary>A phone number (7 to 15 digits), swapped for <c>[PHONE_1]</c>.</summary>
    Phone,

    /// <summary>A payment card number (13 to 19 digits that pass the Luhn check), swapped for <c>[CARD_1]</c>.</summary>
    Card
}

/// <summary>Where a <see cref="PiiRedaction"/> is in its flow.</summary>
public enum PiiRedactionPhase
{
    /// <summary>Nothing has run: the message can be edited and Scan starts.</summary>
    Idle,

    /// <summary>The detector runs and each entity is marked in turn.</summary>
    Scanning,

    /// <summary>The message is redacted: the safe prompt shows and Send starts.</summary>
    Redacted,

    /// <summary>The safe prompt travels from the person through the guard to the model.</summary>
    Sending,

    /// <summary>The model is working on the reply.</summary>
    Thinking,

    /// <summary>The reply streams in with its placeholders.</summary>
    Streaming,

    /// <summary>The guard puts the original values back into the reply.</summary>
    Restoring,

    /// <summary>The reply is restored for the person.</summary>
    Done,

    /// <summary>The detector or the model failed; Retry runs the failed step again.</summary>
    Error
}

/// <summary>
/// One span of personal data found in a text: its type and its characters from <see cref="Start"/> up to, not including,
/// <see cref="End"/>. Detectors return these; <see cref="PiiRedactor.Redact"/> turns them into <see cref="PiiEntity"/>s.
/// </summary>
/// <param name="Type">What the span is.</param>
/// <param name="Start">The index of its first character.</param>
/// <param name="End">The index after its last character (exclusive).</param>
public readonly record struct PiiMatch(PiiType Type, int Start, int End)
{
    /// <summary>The number of characters in the span.</summary>
    public int Length => End - Start;
}

/// <summary>
/// One redacted value: where it was in the message, the value itself and the token that replaced it. The same value
/// appearing twice gets the same token. Log the types server side, never the values next to the prompt.
/// </summary>
public sealed class PiiEntity
{
    /// <summary>Creates an entity.</summary>
    public PiiEntity(PiiType type, int start, int end, string value, string token, int promptStart)
    {
        Type = type;
        Start = start;
        End = end;
        Value = value;
        Token = token;
        PromptStart = promptStart;
    }

    /// <summary>What the value is.</summary>
    public PiiType Type { get; }

    /// <summary>The index of its first character in the original message.</summary>
    public int Start { get; }

    /// <summary>The index after its last character in the original message (exclusive).</summary>
    public int End { get; }

    /// <summary>The original value, exactly as typed.</summary>
    public string Value { get; }

    /// <summary>The token without brackets, such as <c>EMAIL_1</c>.</summary>
    public string Token { get; }

    /// <summary>The placeholder written into the safe prompt, such as <c>[EMAIL_1]</c>.</summary>
    public string Placeholder => "[" + Token + "]";

    /// <summary>The index of the placeholder's first character in the safe prompt.</summary>
    public int PromptStart { get; }

    /// <summary>The index after the placeholder's last character in the safe prompt (exclusive).</summary>
    public int PromptEnd => PromptStart + Placeholder.Length;
}

/// <summary>The outcome of <see cref="PiiRedactor.Redact"/>: the safe prompt and every entity it replaced, in order.</summary>
public sealed class PiiRedactionResult
{
    /// <summary>Creates a result.</summary>
    public PiiRedactionResult(string prompt, IReadOnlyList<PiiEntity> entities)
    {
        Prompt = prompt;
        Entities = entities;
    }

    /// <summary>The message with every entity swapped for its placeholder.</summary>
    public string Prompt { get; }

    /// <summary>Every entity, in the order it appears in the message.</summary>
    public IReadOnlyList<PiiEntity> Entities { get; }
}

/// <summary>A value <see cref="PiiRedactor.RestoreWithRanges"/> put back into a reply, and where it now is.</summary>
internal readonly record struct PiiRestoredRange(PiiType Type, string Token, int Start, int Length);
