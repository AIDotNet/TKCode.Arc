using TKCode.Arc.Generated.TKCodeArcSpecial;

namespace TKCode.Arc;

public static class ArcSpecialBuilderExtensions
{
    /// <summary>
    /// Enables the Arc special components (the animated AI explainers such as <c>AgentLoop</c>). Depends on
    /// <see cref="ArcBuilderExtensions.UseArcControls"/>. The AI components that show code (AskYourData, CodeSuggestion,
    /// DesignToCode, InstallSnippet) color it when the app also calls <c>UseArcHighlighting()</c> (TKCode.Arc.Highlighting);
    /// without it their code is plain text.
    /// </summary>
    public static ArcBuilder UseArcSpecial(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
