using TKCode.Arc.Generated.TKCodeArc;

namespace TKCode.Arc;

public static class ArcBuilderExtensions
{
    /// <summary>
    /// Enables the Arc controls. Depends on <see cref="ArcCoreBuilderExtensions.UseArcCore"/>.
    /// </summary>
    public static ArcBuilder UseArcControls(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcCore())));
}
