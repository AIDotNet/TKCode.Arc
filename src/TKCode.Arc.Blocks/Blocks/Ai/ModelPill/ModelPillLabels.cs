namespace TKCode.Arc;

/// <summary>
/// The words a <see cref="ModelPill"/> shows or announces, with English defaults. Replace the whole object to translate
/// them (<c>new ModelPillLabels { EffortName = ... }</c>).
/// </summary>
public sealed class ModelPillLabels
{
    /// <summary>The visible name of an effort level after the model name ("Medium"); defaults to <see cref="ModelEffortNames.Name"/>.</summary>
    public Func<ModelEffort, string> EffortName { get; init; } = ModelEffortNames.Name;

    /// <summary>
    /// Accessible name of a pill that shows a model and its effort ("Model: Aurora Prime 3, medium effort. Change model");
    /// gets the model and the effort.
    /// </summary>
    public Func<AiModel, ModelEffort, string> AutomationName { get; init; } =
        static (model, effort) => $"Model: {model.Name}, {ModelEffortNames.Name(effort).ToLowerInvariant()} effort. Change model";

    /// <summary>Accessible name of a pill that shows a model without an effort ("Model: Granite 70B. Change model").</summary>
    public Func<AiModel, string> AutomationNameWithoutEffort { get; init; } =
        static model => $"Model: {model.Name}. Change model";

    /// <summary>Accessible name of a pill without a model; gets the pill's <see cref="ModelPill.EmptyText"/>.</summary>
    public Func<string, string> AutomationNameEmpty { get; init; } = static emptyText => emptyText;
}
