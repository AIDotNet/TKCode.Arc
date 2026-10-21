using Avalonia.Platform;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>Where the docs pages get their screenshots (<see cref="GalleryShotView"/>).</summary>
public interface IGalleryShotSource
{
    /// <summary>
    /// Opens the capture at <paramref name="uri"/> (as <see cref="Docs.GalleryDocs.ScreenshotUri"/> builds it), or returns
    /// null when there is none. A source that has the bytes at hand completes at once and the view shows the picture
    /// before it returns; one that has to fetch them completes later.
    /// </summary>
    ValueTask<Stream?> OpenAsync(Uri uri);
}

/// <summary>
/// The screenshot source of the gallery. The default reads the captures from the <c>TKCode.Arc.Gallery.Shots</c> assembly;
/// the WebAssembly host replaces it with one that fetches them from the server when a page shows them.
/// </summary>
public static class GalleryShots
{
    public static IGalleryShotSource Source { get; set; } = new AssetShotSource();

    private sealed class AssetShotSource : IGalleryShotSource
    {
        public ValueTask<Stream?> OpenAsync(Uri uri)
            => new(AssetLoader.Exists(uri) ? AssetLoader.Open(uri) : null);
    }
}
