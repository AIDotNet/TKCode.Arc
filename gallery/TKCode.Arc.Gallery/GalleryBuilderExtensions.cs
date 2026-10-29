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
    public static ArcBuilder UseArcGallery(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())
            .DependsOn(static b => b.UseArcBlocks())
            .DependsOn(static b => b.UseArcAdvancedCharts())
            .DependsOn(static b => b.UseArcWorkflow())
            .DependsOn(static b => b.UseArcSpecial())
            .DependsOn(static b => b.UseArcMobile())
            .DependsOn(static b => b.UseArcPixelTheme())
            .DependsOn(static b => b.UseArcCorners())));
}
