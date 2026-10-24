using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The Gallery's brand mark: the logo in the top bar and the icon of the desktop window. The artwork is
/// <c>Assets/icon.png</c>.
/// </summary>
internal static class GalleryBrand
{
    private static readonly Uri s_logo = new("avares://TKCode.Arc.Gallery/Assets/icon.png");

    /// <summary>The logo as an image for the top bar.</summary>
    public static Bitmap CreateLogo()
    {
        using var stream = AssetLoader.Open(s_logo);
        return new Bitmap(stream);
    }

    /// <summary>The logo as the icon of a desktop window.</summary>
    public static WindowIcon CreateWindowIcon()
    {
        using var stream = AssetLoader.Open(s_logo);
        return new WindowIcon(stream);
    }
}
