using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc.Tests.Special.Photos;

/// <summary>Small in-memory pictures for the Photos tests: decoded at once, so images report loaded without any I/O.</summary>
internal static class PhotosTestPictures
{
    public static ImageSource Picture(int width = 30, int height = 20) =>
        ImageSource.FromBitmap(new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul));
}
