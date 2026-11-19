using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;
using StreamMark.Avalonia.Images;
using Svg.Skia;

namespace StreamMark.Mermaid;

/// <summary>
/// 用 Svg.Skia 把 SVG 图片栅格化为位图，供 <see cref="ImagePipeline"/> 使用
/// （<c>new ImagePipelineOptions { SvgRasterizer = new SvgImageRasterizer() }</c>）。
/// </summary>
public sealed class SvgImageRasterizer : ISvgRasterizer
{
    /// <summary>栅格化的最大边长（像素），防止恶意 SVG 声明超大尺寸。</summary>
    public int MaxDimension { get; init; } = 4096;

    public Bitmap? Rasterize(byte[] svg, int targetWidth, out PixelSize naturalSize)
    {
        naturalSize = default;
        try
        {
            using var stream = new MemoryStream(svg);
            using var document = new SKSvg();
            var picture = document.Load(stream);
            if (picture is null)
            {
                return null;
            }
            var bounds = picture.CullRect;
            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
            naturalSize = new PixelSize(width, height);
            var scale = targetWidth > 0 ? (float)targetWidth / width : 1f;
            // 小图标放大到目标宽度会模糊，矢量图按目标宽度渲染；同时限制最大尺寸
            scale = Math.Min(scale, (float)MaxDimension / Math.Max(width, height));
            var pixelWidth = Math.Max(1, (int)Math.Round(width * scale));
            var pixelHeight = Math.Max(1, (int)Math.Round(height * scale));
            using var bitmap = new SKBitmap(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(scale);
                canvas.Translate(-bounds.Left, -bounds.Top);
                canvas.DrawPicture(picture);
            }
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            using var output = new MemoryStream(png.ToArray());
            return new Bitmap(output);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
