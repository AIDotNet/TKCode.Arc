using Avalonia;

namespace TKCode.Arc.Gallery.Content;

public static class GalleryContentAppBuilderExtensions
{
    /// <summary>
    /// Marks a desktop host that reads the text of the docs pages and guides from this assembly. The assembly holds only
    /// resources, which Avalonia finds by name, so nothing keeps it in a published app: the host also lists it as a
    /// <c>TrimmerRootAssembly</c> (a NativeAOT publish keeps no metadata for an assembly whose types nothing uses, and
    /// loading it by name then fails). The WebAssembly host does not call this; it fetches the same files over HTTP.
    /// </summary>
    public static AppBuilder WithGalleryContent(this AppBuilder builder) => builder;
}
