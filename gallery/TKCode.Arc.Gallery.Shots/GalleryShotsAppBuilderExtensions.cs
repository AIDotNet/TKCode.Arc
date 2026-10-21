using Avalonia;

namespace TKCode.Arc.Gallery.Shots;

public static class GalleryShotsAppBuilderExtensions
{
    /// <summary>
    /// Keeps the screenshots of the docs pages in a desktop host. The assembly holds only resources, so nothing in the
    /// gallery refers to it by name: a trimmed or NativeAOT publish drops it unless the host calls this next to
    /// <c>WithArcFonts()</c>. The WebAssembly host does not call it; it fetches the same files over HTTP.
    /// </summary>
    public static AppBuilder WithGalleryShots(this AppBuilder builder) => builder;
}
