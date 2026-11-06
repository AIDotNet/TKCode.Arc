using TKCode.Arc.Generated.TKCodeArcDock;

namespace TKCode.Arc;

public static class ArcDockBuilderExtensions
{
    /// <summary>
    /// Enables the Arc dock layout (<see cref="DockLayout"/>, <see cref="DockPane"/> and the model types) and its built-in
    /// text in en-US, zh-CN and zh-TW. Depends on <see cref="ArcBuilderExtensions.UseArcControls"/>.
    /// </summary>
    public static ArcBuilder UseArcDock(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
