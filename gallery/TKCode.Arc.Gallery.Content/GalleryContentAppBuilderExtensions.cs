using Avalonia;

namespace TKCode.Arc.Gallery.Content;

public static class GalleryContentAppBuilderExtensions
{
    /// <summary>
    /// Keeps the text of the docs pages and guides in a desktop host. The assembly holds only resources, so nothing in the
    /// gallery refers to it by name: a trimmed or NativeAOT publish drops it unless the host calls this next to
    /// <c>WithGalleryShots()</c>. The WebAssembly host does not call it; it fetches the same files over HTTP.
    /// </summary>
    public static AppBuilder WithGalleryContent(this AppBuilder builder) => builder;
}
