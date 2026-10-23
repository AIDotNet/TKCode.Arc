namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="ModelMenu"/> shows or announces, with English defaults. Replace the whole object (it is
/// immutable) to translate the menu: <c>menu.Labels = new ModelMenuLabels { MoreModels = "Weitere Modelle" }</c>.
/// </summary>
public sealed class ModelMenuLabels
{
    /// <summary>Accessible name of the menu ("Models").</summary>
    public string Menu { get; init; } = "Models";

    /// <summary>Accessible name of the group of featured models at the top ("Featured models").</summary>
    public string Featured { get; init; } = "Featured models";

    /// <summary>The row that opens the effort levels, and the header above the segmented row of the inline layout ("Effort").</summary>
    public string Effort { get; init; } = "Effort";

    /// <summary>The row that opens the other models ("More models").</summary>
    public string MoreModels { get; init; } = "More models";

    /// <summary>Accessible name of the effort submenu, and the title of its page in a narrow window ("Effort").</summary>
    public string EffortMenu { get; init; } = "Effort";

    /// <summary>Accessible name of the other-models submenu, and the title of its page in a narrow window ("More models").</summary>
    public string MoreModelsMenu { get; init; } = "More models";

    /// <summary>Small text after the name of the model's default effort level ("Default").</summary>
    public string Default { get; init; } = "Default";

    /// <summary>Accessible name of the back row on a page in a narrow window; gets the page title ("Back to featured models, Effort").</summary>
    public Func<string, string> BackTo { get; init; } = static page => $"Back to featured models, {page}";

    /// <summary>The name of an effort level; defaults to <see cref="ModelEffortNames.Name"/>.</summary>
    public Func<ModelEffort, string> EffortName { get; init; } = ModelEffortNames.Name;

    /// <summary>
    /// The line under an effort level's name, and the description at the right of the inline layout's "Effort" header;
    /// defaults to <see cref="ModelEffortNames.Description"/>.
    /// </summary>
    public Func<ModelEffort, string> EffortDescription { get; init; } = ModelEffortNames.Description;

    /// <summary>
    /// The word in a cell of the inline layout's segmented row: a cell is about 53px wide at 12px text, too narrow for
    /// "Extra high", so that level reads "X-high"; the others use <see cref="ModelEffortNames.Name"/>. The accessible names
    /// keep the full <see cref="EffortName"/>.
    /// </summary>
    public Func<ModelEffort, string> EffortSegmentName { get; init; } =
        static effort => effort == ModelEffort.ExtraHigh ? "X-high" : ModelEffortNames.Name(effort);

    /// <summary>
    /// Accessible name of the inline layout's segmented row; gets the header (<see cref="Effort"/>) and the current
    /// level's <see cref="EffortName"/> ("Effort: Medium").
    /// </summary>
    public Func<string, string, string> EffortStrip { get; init; } = static (title, level) => $"{title}: {level}";

    /// <summary>The only line of a menu without models ("No models available").</summary>
    public string Empty { get; init; } = "No models available";
}
