namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="CubeMascot"/> speaks, in English by default: the status words and the pattern that joins
/// them with the assistant's name into the accessible label ("Assistant, thinking"). Replace the instance (or set its
/// properties) to translate or reword.
/// </summary>
public sealed class CubeMascotLabels
{
    /// <summary>The status word while idle.</summary>
    public string Idle { get; set; } = "idle";

    /// <summary>The status word while thinking.</summary>
    public string Thinking { get; set; } = "thinking";

    /// <summary>The status word while writing.</summary>
    public string Writing { get; set; } = "writing";

    /// <summary>The status word once done.</summary>
    public string Done { get; set; } = "done";

    /// <summary>The status word after an error.</summary>
    public string Error { get; set; } = "error";

    /// <summary>The accessible label pattern: {0} is the assistant's name, {1} the status word.</summary>
    public string LabelFormat { get; set; } = "{0}, {1}";

    /// <summary>The status word for <paramref name="status"/>.</summary>
    public string WordFor(CubeMascotStatus status) => status switch
    {
        CubeMascotStatus.Thinking => Thinking,
        CubeMascotStatus.Writing => Writing,
        CubeMascotStatus.Done => Done,
        CubeMascotStatus.Error => Error,
        _ => Idle
    };

    /// <summary>The accessible label for <paramref name="name"/> in <paramref name="status"/> ("Assistant, thinking").</summary>
    public string LabelFor(string name, CubeMascotStatus status)
    {
        var word = WordFor(status);
        try
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, LabelFormat ?? "{0}, {1}", name, word);
        }
        catch (FormatException)
        {
            return name + ", " + word;
        }
    }
}
