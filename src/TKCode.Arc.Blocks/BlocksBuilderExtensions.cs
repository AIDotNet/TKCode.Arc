using TKCode.Arc.Generated.TKCodeArcBlocks;

namespace TKCode.Arc;

public static class ArcBlocksBuilderExtensions
{
    /// <summary>
    /// Enables the Arc blocks (page-level compositions such as the command palette). Depends on
    /// <see cref="ArcBuilderExtensions.UseArcControls"/>, <see cref="ArcMarkdownBuilderExtensions.UseArcMarkdown"/> and
    /// <see cref="ArcHighlightingBuilderExtensions.UseArcHighlighting"/> (the AI blocks render Markdown and code).
    /// </summary>
    public static ArcBuilder UseArcBlocks(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls().UseArcMarkdown().UseArcHighlighting())));
}
