namespace TKCode.Arc.Gallery.Shell;

/// <summary>Where the Friends page and the home page get the logos of the sites (<see cref="FriendMark"/>).</summary>
public interface IGalleryLogoSource
{
    /// <summary>Opens the PNG of the logo named <paramref name="name"/>, or returns null when there is none.</summary>
    ValueTask<Stream?> OpenAsync(string name);
}

/// <summary>
/// The logo source of the gallery. A desktop host has no site to fetch from, so the default has none and a mark shows the
/// site's letters; the WebAssembly host replaces it with one that fetches <c>friends/&lt;name&gt;.png</c> from the server
/// (<c>wwwroot/friends</c>).
/// </summary>
public static class GalleryLogos
{
    public static IGalleryLogoSource Source { get; set; } = new NoLogoSource();

    private sealed class NoLogoSource : IGalleryLogoSource
    {
        public ValueTask<Stream?> OpenAsync(string name) => new((Stream?)null);
    }
}
