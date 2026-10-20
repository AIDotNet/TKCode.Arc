using AtomUI;
using AtomUI.Generated.TKCodeArcGallery;

namespace TKCode.Arc.Gallery;

public static class GalleryBuilderExtensions
{
    /// <summary>
    /// Registers the gallery's shell text (<c>Localization/*.xlf</c>) with AtomUI. Call it next to <c>UseArc()</c> in every
    /// host that shows a <see cref="Shell.GalleryWindow"/>; the host's <c>UseLanguages</c> decides which of the translations are used.
    /// </summary>
    public static IAtomUIBuilder UseArcGallery(this IAtomUIBuilder builder)
        => GeneratedControlPackageRegistration.Definition.Register(builder, static package =>
        {
            package.DependsOn(static dependency => dependency.UseArc());
        });
}
