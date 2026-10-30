namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="GhostMascot"/> speaks, in English by default: the status words of its accessible label and
/// the pattern that joins the assistant's name to them ("Assistant, thinking"). Replace the instance (or set its
/// properties) to translate or reword.
/// </summary>
public sealed class GhostMascotLabels
{
    /// <summary>The status word while idle.</summary>
    public string Idle { get; set; } = "idle";

    /// <summary>The status word while listening.</summary>
    public string Listening { get; set; } = "listening";

    /// <summary>The status word while thinking.</summary>
    public string Thinking { get; set; } = "thinking";

    /// <summary>The status word while writing.</summary>
    public string Writing { get; set; } = "writing";

    /// <summary>The status word once done.</summary>
    public string Done { get; set; } = "done";

    /// <summary>The status word after an error.</summary>
    public string Error { get; set; } = "error";

    /// <summary>The accessible label pattern: <c>{0}</c> is the assistant's name, <c>{1}</c> the status word.</summary>
    public string LabelFormat { get; set; } = "{0}, {1}";

    /// <summary>The status word for <paramref name="status"/>.</summary>
    public string WordFor(GhostMascotStatus status) => status switch
    {
        GhostMascotStatus.Listening => Listening,
        GhostMascotStatus.Thinking => Thinking,
        GhostMascotStatus.Writing => Writing,
        GhostMascotStatus.Done => Done,
        GhostMascotStatus.Error => Error,
        _ => Idle
    };

    /// <summary>The accessible label for <paramref name="name"/> in <paramref name="status"/> ("Assistant, thinking").</summary>
    public string LabelFor(string name, GhostMascotStatus status)
    {
        var word = WordFor(status);
        try
        {
            return string.Format(LabelFormat ?? "{0}, {1}", name, word);
        }
        catch (FormatException)
        {
            return name + ", " + word;
        }
    }
}
