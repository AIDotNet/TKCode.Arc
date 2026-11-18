using TKCode.Arc.Generated.TKCodeArcGraphics3DControls;

namespace TKCode.Arc;

/// <summary>Registers the TKCode.Arc.Graphics3D.Controls package.</summary>
public static class ArcGraphics3DBuilderExtensions
{
    /// <summary>
    /// Enables the Arc 3D controls (<see cref="Viewport3D"/>, <see cref="ModelViewer"/>). Depends on <see cref="ArcBuilderExtensions.UseArcControls"/>.
    /// The engine itself (<c>TKCode.Arc.Graphics3D</c>) needs no registration.
    /// </summary>
    public static ArcBuilder UseArcGraphics3D(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
