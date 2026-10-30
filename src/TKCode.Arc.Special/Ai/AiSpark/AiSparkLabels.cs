namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AiSpark"/> shows or announces, in English by default: the pill's status labels and the
/// spoken phrases the mark uses as its accessible name. Replace the instance (or set its properties) to translate or reword.
/// </summary>
public sealed class AiSparkLabels
{
    /// <summary>The pill label while idle.</summary>
    public string Ready { get; set; } = "Ready";

    /// <summary>The pill label while thinking.</summary>
    public string Thinking { get; set; } = "Thinking";

    /// <summary>The pill label while generating.</summary>
    public string Writing { get; set; } = "Writing";

    /// <summary>The pill label once done.</summary>
    public string Done { get; set; } = "Done";

    /// <summary>The pill label after an error.</summary>
    public string Error { get; set; } = "Couldn't finish";

    /// <summary>The accessible name while idle.</summary>
    public string ReadyPhrase { get; set; } = "AI is ready";

    /// <summary>The accessible name while thinking.</summary>
    public string ThinkingPhrase { get; set; } = "AI is thinking";

    /// <summary>The accessible name while generating.</summary>
    public string WritingPhrase { get; set; } = "AI is writing";

    /// <summary>The accessible name once done.</summary>
    public string DonePhrase { get; set; } = "AI is done";

    /// <summary>The accessible name after an error.</summary>
    public string ErrorPhrase { get; set; } = "AI couldn't finish";

    /// <summary>The pill label for <paramref name="status"/>.</summary>
    public string LabelFor(AiSparkStatus status) => status switch
    {
        AiSparkStatus.Thinking => Thinking,
        AiSparkStatus.Generating => Writing,
        AiSparkStatus.Done => Done,
        AiSparkStatus.Error => Error,
        _ => Ready
    };

    /// <summary>The spoken phrase for <paramref name="status"/>.</summary>
    public string PhraseFor(AiSparkStatus status) => status switch
    {
        AiSparkStatus.Thinking => ThinkingPhrase,
        AiSparkStatus.Generating => WritingPhrase,
        AiSparkStatus.Done => DonePhrase,
        AiSparkStatus.Error => ErrorPhrase,
        _ => ReadyPhrase
    };
}
