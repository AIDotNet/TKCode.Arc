using TKCode.Arc.Generated.TKCodeArcRibbon;

namespace TKCode.Arc;

public static class ArcRibbonBuilderExtensions
{
    /// <summary>
    /// Enables the Arc ribbon (<see cref="Ribbon"/>, <see cref="RibbonTab"/>, <see cref="RibbonGroup"/>, the ribbon buttons and
    /// <see cref="QuickAccessToolBar"/>) and its built-in text in en-US, zh-CN and zh-TW. Depends on
    /// <see cref="ArcBuilderExtensions.UseArcControls"/>.
    /// </summary>
    public static ArcBuilder UseArcRibbon(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
