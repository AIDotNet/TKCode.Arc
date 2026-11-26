using TKCode.Arc;
using TKCode.Arc.Generated.TKCodeArcMobile;

namespace TKCode.Arc.Mobile;

public static class ArcMobileBuilderExtensions
{
    /// <summary>
    /// Enables the Arc mobile controls (shell, overlays, lists, actions, selection and text fields). Depends on
    /// <see cref="ArcCoreBuilderExtensions.UseArcCore"/> and does not enable the desktop Arc controls.
    /// </summary>
    public static ArcBuilder UseArcMobile(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcCore())));
}
