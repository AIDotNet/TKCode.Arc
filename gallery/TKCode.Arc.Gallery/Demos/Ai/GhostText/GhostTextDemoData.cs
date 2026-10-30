namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented pull request description the ghost text demos write: the lines the simulation types with the
/// suggestions that continue them, the field's label, placeholder and footer, and the model credited in the chip (a real
/// one from <see cref="AiModelCatalog"/>, with a neutral mark).
/// </summary>
internal static class GhostTextDemoData
{
    public const string LabelText = "Description";

    public const string Placeholder = "Describe what changed and why";

    public const string FooterText = "Markdown is supported";

    /// <summary>The model in the chip: Mistral AI's Mistral Small 4 (checked on <see cref="AiModelCatalog.CheckedOn"/>).</summary>
    public static AgentModel Model { get; } = new(AiModelCatalog.MistralSmall4.Name, ArcIcons.Sparkles);

    /// <summary>The texts the simulation types and the suggestions streamed for them.</summary>
    public static readonly IReadOnlyList<GhostTextSimulationLine> Script =
    [
        new("Fixes the flaky checkout test", " by waiting for the payment webhook before it asserts."),
        new("The retry helper now", " backs off for up to two seconds instead of sleeping."),
    ];

    /// <summary>The rule based stand-in for a model that answers whatever the person types (see <see cref="GhostTextDemoModel"/>).</summary>
    public static readonly GhostTextSuggest Suggest = GhostTextDemoModel.Create();
}
