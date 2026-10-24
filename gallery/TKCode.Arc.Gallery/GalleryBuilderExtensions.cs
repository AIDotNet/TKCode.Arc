using AtomUI;
using AtomUI.Generated.TKCodeArcGallery;

namespace TKCode.Arc.Gallery;

public static class GalleryBuilderExtensions
{
    /// <summary>
    /// Registers the gallery's shell text (<c>Localization/*.xlf</c>) with AtomUI, and the blocks, advanced charts and special
    /// components its pages show (<c>UseArcBlocks()</c>, <c>UseArcAdvancedCharts()</c>, <c>UseArcWorkflow()</c>, <c>UseArcSpecial()</c>). Call it next to <c>UseArc()</c> in every host that shows a
    /// <see cref="Shell.GalleryWindow"/>;
    /// the host's <c>UseLanguages</c> decides which of the translations are used.
    /// </summary>
    public static IAtomUIBuilder UseArcGallery(this IAtomUIBuilder builder)
        => GeneratedControlPackageRegistration.Definition.Register(builder, static package =>
        {
            package.DependsOn(static dependency => dependency.UseArc());
            package.DependsOn(static dependency => dependency.UseArcBlocks());
            package.DependsOn(static dependency => dependency.UseArcAdvancedCharts());
            package.DependsOn(static dependency => dependency.UseArcWorkflow());
            package.DependsOn(static dependency => dependency.UseArcSpecial());
        });
}
