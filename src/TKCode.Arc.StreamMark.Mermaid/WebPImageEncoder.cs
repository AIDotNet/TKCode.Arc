using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using StreamMark.Avalonia.Images;

namespace StreamMark.Mermaid;

/// <summary>
/// 用 Skia 把位图编码为 WebP（有损、保留透明度），供 <see cref="ImagePipeline"/> 的磁盘缩略图使用
/// （<c>new ImagePipelineOptions { ThumbnailEncoder = new WebPImageEncoder() }</c>）。体积约为 PNG 的 1/5。
/// </summary>
public sealed class WebPImageEncoder : IImageEncoder
{
    /// <summary>有损压缩质量（0–100），默认 80。</summary>
    public int Quality { get; init; } = 80;

    public byte[]? Encode(Bitmap bitmap)
    {
        var colorType = bitmap.Format switch
        {
            { } format when format == PixelFormat.Bgra8888 => SKColorType.Bgra8888,
            { } format when format == PixelFormat.Rgba8888 => SKColorType.Rgba8888,
            _ => SKColorType.Unknown,
        };
        var alphaType = bitmap.AlphaFormat switch
        {
            AlphaFormat.Premul => SKAlphaType.Premul,
            AlphaFormat.Unpremul => SKAlphaType.Unpremul,
            AlphaFormat.Opaque => SKAlphaType.Opaque,
            _ => SKAlphaType.Unknown,
        };
        if (colorType == SKColorType.Unknown || alphaType == SKAlphaType.Unknown)
        {
            return null;
        }
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var length = stride * size.Height;
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), buffer, length, stride);
            using var pixmap = new SKPixmap(new SKImageInfo(size.Width, size.Height, colorType, alphaType), buffer, stride);
            using var data = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, Quality));
            return data?.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
