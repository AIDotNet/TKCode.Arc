using TKCode.Arc.Generated.TKCodeArcA2UI;

namespace TKCode.Arc;

public static class ArcA2UIBuilderExtensions
{
    /// <summary>
    /// Enables the A2UI renderer view (<see cref="A2UISurfaceView"/>) and its tokens. Depends on
    /// <see cref="ArcBuilderExtensions.UseArcControls"/>.
    /// </summary>
    public static ArcBuilder UseArcA2UI(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
