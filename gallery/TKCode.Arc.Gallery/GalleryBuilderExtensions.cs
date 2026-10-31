using TKCode.Arc.Generated.TKCodeArcGallery;
using TKCode.Arc.Mobile;
using TKCode.Arc.Themes.Corners;
using TKCode.Arc.Themes.Pixel;

namespace TKCode.Arc.Gallery;

public static class GalleryBuilderExtensions
{
    /// <summary>
    /// Registers the gallery's shell text (<c>Localization/*.xlf</c>) and enables everything the gallery's pages show: the
    /// blocks (<c>UseArcBlocks()</c>), the mobile components (<c>UseArcMobile()</c>), the Pixel and corner styles the
    /// Appearance panel offers (<c>UseArcPixelTheme()</c>, <c>UseArcCorners()</c>), <c>UseArcAdvancedCharts()</c>,
    /// <c>UseArcWorkflow()</c> and <c>UseArcSpecial()</c>. Call it inside <c>UseArc(...)</c> in every host that shows a
    /// <see cref="Shell.GalleryWindow"/>; the host's <c>UseLanguages</c> decides which of the translations are used.
    /// </summary>
    /// <param name="builder">The Arc builder.</param>
    /// <param name="onDemand">
    /// Loads the Special components when a page first needs them (<see cref="GalleryModules"/>); a host that leaves
    /// TKCode.Arc.Special out of its startup download passes one, and <c>UseArcSpecial()</c> then runs at that point
    /// instead of at startup. Null (every desktop host) enables everything now.
    /// </param>
    public static ArcBuilder UseArcGallery(this ArcBuilder builder, IGalleryAssemblyLoader? onDemand = null)
    {
        GalleryModules.LoadOnDemand(onDemand);
        if (onDemand is not null)
        {
            return builder.UsePackage(ArcGeneratedAssets.PackageId, static package => DependsOnAllButSpecial(package));
        }
        return builder.UsePackage(ArcGeneratedAssets.PackageId, static package => DependsOnAllButSpecial(package)
            .DependsOn(static b => b.UseArcSpecial()));
    }

    private static ArcPackageBuilder DependsOnAllButSpecial(ArcPackageBuilder package)
        => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())
            .DependsOn(static b => b.UseArcBlocks())
            .DependsOn(static b => b.UseArcAdvancedCharts())
            .DependsOn(static b => b.UseArcWorkflow())
            .DependsOn(static b => b.UseArcMobile())
            .DependsOn(static b => b.UseArcPixelTheme())
            .DependsOn(static b => b.UseArcCorners()));
}
