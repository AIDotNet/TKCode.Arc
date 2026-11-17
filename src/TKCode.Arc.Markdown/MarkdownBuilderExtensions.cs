using TKCode.Arc.Generated.TKCodeArcMarkdown;

namespace TKCode.Arc;

public static class ArcMarkdownBuilderExtensions
{
    /// <summary>
    /// Enables the Markdown controls (<see cref="Markdown"/>, <see cref="ChatThread"/> and <see cref="ChatComposer"/>).
    /// Depends on <see cref="ArcBuilderExtensions.UseArcControls"/>. Fenced code is highlighted only when
    /// <c>UseArcHighlighting()</c> (TKCode.Arc.Highlighting) is enabled as well.
    /// </summary>
    public static ArcBuilder UseArcMarkdown(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
