using TKCode.Arc.Generated.TKCodeArcSpecial;

namespace TKCode.Arc;

public static class ArcSpecialBuilderExtensions
{
    /// <summary>
    /// Enables the Arc special components (the animated AI explainers such as <c>AgentLoop</c>). Depends on
    /// <see cref="ArcBuilderExtensions.UseArcControls"/>.
    /// </summary>
    public static ArcBuilder UseArcSpecial(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
