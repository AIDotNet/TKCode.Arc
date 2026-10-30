namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AiMascot"/> says, in English by default. Replace the instance (or set its properties) to
/// translate or reword; the accessible label is a function so that the word order can change.
/// </summary>
public sealed class AiMascotLabels
{
    /// <summary>The shared English labels.</summary>
    public static AiMascotLabels Default { get; } = new();

    /// <summary>The assistant's name when <see cref="AiMascot.AssistantName"/> is not set.</summary>
    public string DefaultName { get; set; } = "Assistant";

    /// <summary>The idle state's word.</summary>
    public string Idle { get; set; } = "idle";

    /// <summary>The listening state's word.</summary>
    public string Listening { get; set; } = "listening";

    /// <summary>The thinking state's word.</summary>
    public string Thinking { get; set; } = "thinking";

    /// <summary>The writing state's word.</summary>
    public string Writing { get; set; } = "writing";

    /// <summary>The done state's word.</summary>
    public string Done { get; set; } = "done";

    /// <summary>The error state's word.</summary>
    public string Error { get; set; } = "error";

    /// <summary>The accessible label and the announcement: the name and the state's word, "Assistant, thinking".</summary>
    public Func<string, string, string> Label { get; set; } = (name, status) => $"{name}, {status}";

    /// <summary>The word for <paramref name="status"/>.</summary>
    public string StatusWord(AiMascotStatus status) => status switch
    {
        AiMascotStatus.Listening => Listening,
        AiMascotStatus.Thinking => Thinking,
        AiMascotStatus.Writing => Writing,
        AiMascotStatus.Done => Done,
        AiMascotStatus.Error => Error,
        _ => Idle
    };
}
