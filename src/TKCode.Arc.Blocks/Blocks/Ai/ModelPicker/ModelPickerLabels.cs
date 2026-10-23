namespace TKCode.Arc;

/// <summary>
/// The words of a <see cref="ModelPicker"/> that belong to the picker itself: the confirmation under the summary and the
/// sentences read out when the model or the effort changes. The words of the pill, the menu and the summary are in
/// <see cref="Pill"/>, <see cref="Menu"/> and <see cref="Summary"/>. Replace the whole object (or any of its parts) to
/// translate; defaults are English.
/// </summary>
public sealed class ModelPickerLabels
{
    /// <summary>The words of the pill (its accessible name).</summary>
    public ModelPillLabels Pill { get; init; } = new();

    /// <summary>The words of the menu (rows, pages, accessible names).</summary>
    public ModelMenuLabels Menu { get; init; } = new();

    /// <summary>The words of the summary (fact labels and values).</summary>
    public ModelSummaryLabels Summary { get; init; } = new();

    /// <summary>The effort level name used in the confirmation and the announcements.</summary>
    public Func<ModelEffort, string> EffortName { get; init; } = ModelEffortNames.Name;

    /// <summary>The confirmation after a model change; the arguments are the old and the new model names ("A → B").</summary>
    public Func<string, string, string> ModelNotice { get; init; } = static (previous, next) => $"{previous} → {next}";

    /// <summary>The confirmation after an effort change; the arguments are the old and the new level names ("Effort: Medium → High").</summary>
    public Func<string, string, string> EffortNotice { get; init; } = static (previous, next) => $"Effort: {previous} → {next}";

    /// <summary>
    /// The confirmation after a model change that also moved the effort to a level the new model offers; the arguments are
    /// the old model name, the new model name and the new level name ("A → B · Effort adjusted to High").
    /// </summary>
    public Func<string, string, string, string> ModelAdjustedNotice { get; init; } =
        static (previous, next, level) => $"{previous} → {next} · Effort adjusted to {level}";

    /// <summary>The announcement after a model change; the arguments are the model name and the effort level name (null when the model has none).</summary>
    public Func<string, string?, string> ModelAnnouncement { get; init; } =
        static (model, effort) => effort is null ? $"Now using {model}" : $"Now using {model}, {effort.ToLowerInvariant()} effort";

    /// <summary>The announcement after an effort change; the arguments are the model name and the effort level name.</summary>
    public Func<string, string, string> EffortAnnouncement { get; init; } =
        static (model, effort) => $"{model}, {effort.ToLowerInvariant()} effort";
}
