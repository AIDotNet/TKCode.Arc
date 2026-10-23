namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="ModelSummary"/> shows or reads out, with English defaults. Words built from a value are
/// functions, so an app can apply its own grammar. Assign a new instance to <see cref="ModelSummary.Labels"/> to change them.
/// </summary>
public sealed class ModelSummaryLabels
{
    /// <summary>Accessible name of the summary region ("Selected model").</summary>
    public string Region { get; init; } = "Selected model";

    /// <summary>Label of the context window fact ("Context").</summary>
    public string Context { get; init; } = "Context";

    /// <summary>Label of the input price fact ("Input").</summary>
    public string Input { get; init; } = "Input";

    /// <summary>Label of the output price fact ("Output").</summary>
    public string Output { get; init; } = "Output";

    /// <summary>Label of the effort fact ("Effort").</summary>
    public string Effort { get; init; } = "Effort";

    /// <summary>
    /// The effort value of a model without effort levels ("Fixed"). Keep it short: a fact cell of the four-column
    /// table has about 92px for its value.
    /// </summary>
    public string NotAdjustable { get; init; } = "Fixed";

    /// <summary>The value shown when the model or the number is missing (an en dash).</summary>
    public string Missing { get; init; } = "–";

    /// <summary>What a screen reader hears instead of <see cref="Missing"/> ("not available").</summary>
    public string MissingSpoken { get; init; } = "not available";

    /// <summary>The context value from the formatted token count ("800K" → "800K tokens").</summary>
    public Func<string, string> ContextValue { get; init; } = static tokens => $"{tokens} tokens";

    /// <summary>The name of an effort level (defaults to <see cref="ModelEffortNames.Name"/>: Low, Medium, High, Extra high, Max).</summary>
    public Func<ModelEffort, string> EffortName { get; init; } = ModelEffortNames.Name;

    /// <summary>Accessible name of a fact, reading its label and value together ("Context: 800K tokens").</summary>
    public Func<string, string, string> Fact { get; init; } = static (label, value) => $"{label}: {value}";
}
