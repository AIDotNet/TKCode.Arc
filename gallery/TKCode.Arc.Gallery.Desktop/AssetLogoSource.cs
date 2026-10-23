using TKCode.Arc.Gallery.Shell;
using Avalonia.Platform;

namespace TKCode.Arc.Gallery.Desktop;

/// <summary>Reads the logos of the Friends page from this assembly's resources (the files of <c>wwwroot/friends</c>).</summary>
internal sealed class AssetLogoSource : IGalleryLogoSource
{
    public ValueTask<Stream?> OpenAsync(string name)
    {
        var uri = new Uri($"avares://TKCode.Arc.Gallery.Desktop/Friends/{name}.png");
        return new(AssetLoader.Exists(uri) ? AssetLoader.Open(uri) : null);
    }
}
