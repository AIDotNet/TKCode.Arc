using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// The copies an <see cref="ImageReveal"/> draws its soft preview and its band from, built once per image and canvas shape:
/// the image cover-cropped to the canvas and shrunk to 48, 24, 12 and 6 pixels wide (each from the one before, so every
/// copy is a true average of the image's own colors; drawn smoothly upscaled they make the blur chain) and a near-sharp
/// copy at a third of the canvas's device width for the band.
/// </summary>
internal sealed class ImageRevealChain : IDisposable
{
    /// <summary>The soft copies' widths, softest first (the spec's 6 to 48px chain).</summary>
    public static readonly int[] SoftWidths = [6, 12, 24, 48];

    private ImageRevealChain(Bitmap[] soft, Bitmap near, IImage source, Rect crop, double aspect, int nearWidth)
    {
        Soft = soft;
        Near = near;
        Source = source;
        Crop = crop;
        Aspect = aspect;
        NearWidth = nearWidth;
    }

    /// <summary>The soft copies, softest first.</summary>
    public Bitmap[] Soft { get; }

    /// <summary>The near-sharp copy the band blends in.</summary>
    public Bitmap Near { get; }

    /// <summary>The image the copies were made from.</summary>
    public IImage Source { get; }

    /// <summary>The cover-crop of <see cref="Source"/> in its own source units (pixels for bitmaps).</summary>
    public Rect Crop { get; }

    /// <summary>The canvas's width over height the copies were made for.</summary>
    public double Aspect { get; }

    /// <summary>The near copy's width in pixels.</summary>
    public int NearWidth { get; }

    /// <summary>The size <c>DrawImage</c> reads source rectangles in: pixels for bitmaps, otherwise the image's size.</summary>
    public static Size SourceSize(IImage image) => image is Bitmap bitmap
        ? new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height)
        : image.Size;

    /// <summary>The centered part of an image of <paramref name="size"/> that covers a canvas of <paramref name="aspect"/> (CSS object-fit: cover).</summary>
    public static Rect CoverCrop(Size size, double aspect)
    {
        if (size.Width <= 0 || size.Height <= 0 || !(aspect > 0))
        {
            return new Rect(size);
        }
        if (size.Width / size.Height > aspect)
        {
            var width = size.Height * aspect;
            return new Rect((size.Width - width) / 2, 0, width, size.Height);
        }
        var height = size.Width / aspect;
        return new Rect(0, (size.Height - height) / 2, size.Width, height);
    }

    /// <summary>The near copy's width for a canvas <paramref name="deviceWidth"/> device pixels wide.</summary>
    public static int NearWidthFor(double deviceWidth) => (int)Math.Clamp(Math.Round(deviceWidth / 3 / 16) * 16, 64, 704);

    /// <summary>Builds the copies, or returns null when the platform cannot render offscreen.</summary>
    public static ImageRevealChain? Build(IImage source, double aspect, int nearWidth)
    {
        var size = SourceSize(source);
        if (size.Width <= 0 || size.Height <= 0 || !(aspect > 0))
        {
            return null;
        }
        var crop = CoverCrop(size, aspect);
        var soft = new Bitmap[SoftWidths.Length];
        Bitmap? near = null;
        try
        {
            near = Render(source, crop, nearWidth, aspect);
            // Each copy is shrunk from the next larger one: a box-like average, so the softest keeps the image's colors.
            IImage from = source;
            var fromRect = crop;
            for (var i = SoftWidths.Length - 1; i >= 0; i--)
            {
                var copy = Render(from, fromRect, SoftWidths[i], aspect);
                soft[i] = copy;
                from = copy;
                fromRect = new Rect(0, 0, copy.PixelSize.Width, copy.PixelSize.Height);
            }
            return new ImageRevealChain(soft, near, source, crop, aspect, nearWidth);
        }
        catch (Exception)
        {
            near?.Dispose();
            foreach (var copy in soft)
            {
                copy?.Dispose();
            }
            return null;
        }
    }

    private static RenderTargetBitmap Render(IImage source, Rect sourceRect, int width, double aspect)
    {
        var pixels = new PixelSize(Math.Max(1, width), Math.Max(1, (int)Math.Round(width / aspect)));
        var target = new RenderTargetBitmap(pixels, new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
        {
            context.DrawImage(source, sourceRect, new Rect(0, 0, pixels.Width, pixels.Height));
        }
        return target;
    }

    public void Dispose()
    {
        Near.Dispose();
        foreach (var copy in Soft)
        {
            copy.Dispose();
        }
    }
}

/// <summary>
/// The film grain: one 256 × 256 tile of fine monochrome noise (each pixel a little lighter or darker, premultiplied),
/// made once for the whole app. The canvas draws it one tile pixel per device pixel at a frame-dependent offset, which
/// refreshes the grain 24 times a second without new textures.
/// </summary>
internal static class ImageRevealGrain
{
    /// <summary>The tile's side in pixels.</summary>
    public const int Side = 256;

    /// <summary>The strongest grain alpha (of 255).</summary>
    public const int MaxAlpha = 30;

    private static Bitmap? s_tile;
    private static bool s_failed;

    /// <summary>The tile, or null where the platform cannot make bitmaps.</summary>
    public static Bitmap? Tile
    {
        get
        {
            if (s_tile is not null || s_failed)
            {
                return s_tile;
            }
            try
            {
                var bitmap = new WriteableBitmap(new PixelSize(Side, Side), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                using (var locked = bitmap.Lock())
                {
                    var row = new byte[Side * 4];
                    var state = 0x9E3779B9u;
                    for (var y = 0; y < Side; y++)
                    {
                        for (var x = 0; x < Side; x++)
                        {
                            state ^= state << 13;
                            state ^= state >> 17;
                            state ^= state << 5;
                            var alpha = (byte)(state % (MaxAlpha + 1));
                            var light = (state & 0x100) != 0;
                            var value = light ? alpha : (byte)0;
                            row[x * 4] = value;
                            row[x * 4 + 1] = value;
                            row[x * 4 + 2] = value;
                            row[x * 4 + 3] = alpha;
                        }
                        System.Runtime.InteropServices.Marshal.Copy(row, 0, locked.Address + y * locked.RowBytes, row.Length);
                    }
                }
                s_tile = bitmap;
            }
            catch (Exception)
            {
                s_failed = true;
            }
            return s_tile;
        }
    }
}
