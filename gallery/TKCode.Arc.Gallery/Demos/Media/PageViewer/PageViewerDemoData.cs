using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The pictures and the on-demand source of the PageViewer demos. Every page is drawn here from rectangles, so the demos
/// work offline and there is nothing third-party in them: a mock magazine page with a colored plate and lines of grey text.
/// </summary>
public static class PageViewerDemoData
{
    private const int PictureWidth = 800;
    private const int PictureHeight = 1100;
    private static readonly Vector s_retina = new(192, 192);
    private static IReadOnlyList<Bitmap>? s_pictures;

    /// <summary>Six pictures of 400 × 550 DIPs, made the first time a demo shows them.</summary>
    public static IReadOnlyList<Bitmap> Pictures
    {
        get
        {
            if (s_pictures is null)
            {
                var pictures = new Bitmap[6];
                for (var i = 0; i < pictures.Length; i++)
                {
                    pictures[i] = Paint(i, new PixelSize(PictureWidth, PictureHeight), null, s_retina);
                }
                s_pictures = pictures;
            }
            return s_pictures;
        }
    }

    /// <summary>A long document that renders each page when the viewer asks for it.</summary>
    public static IPageSource Generated { get; } = new GeneratedPageSource(240);

    /// <summary>
    /// Draws a page, or the <paramref name="region"/> of it, at <paramref name="target"/> pixels for the whole page. The
    /// shapes are in fractions of the page, so any size is sharp.
    /// </summary>
    public static Bitmap Paint(int page, PixelSize target, PixelRect? region, Vector dpi)
    {
        var area = region ?? new PixelRect(target);
        var pixels = new int[area.Width * area.Height];
        Array.Fill(pixels, unchecked((int)0xFFFFFFFF));
        void Fill(double x0, double y0, double x1, double y1, uint argb)
        {
            var left = Math.Max(area.X, (int)(x0 * target.Width));
            var right = Math.Min(area.Right, (int)(x1 * target.Width));
            var top = Math.Max(area.Y, (int)(y0 * target.Height));
            var bottom = Math.Min(area.Bottom, (int)(y1 * target.Height));
            for (var y = top; y < bottom; y++)
            {
                pixels.AsSpan((y - area.Y) * area.Width + left - area.X, Math.Max(0, right - left)).Fill(unchecked((int)argb));
            }
        }

        var hue = s_hues[page % s_hues.Length];
        Fill(0.08, 0.06, 0.92, 0.075, 0xFF1E2B33);
        Fill(0.08, 0.09, 0.55, 0.098, 0xFF8A949C);
        var plateBottom = page % 2 == 0 ? 0.42 : 0.3;
        Fill(0.08, 0.13, 0.92, plateBottom, hue);
        Fill(0.08, plateBottom - 0.06, 0.92, plateBottom, Darken(hue));
        DrawNumber(page + 1, 0.12, 0.16, Fill);
        var random = new Random(page * 17 + 3);
        for (var line = plateBottom + 0.04; line < 0.9; line += 0.028)
        {
            var end = line + 0.028 >= 0.9 || random.Next(6) == 0 ? 0.3 + random.NextDouble() * 0.4 : 0.92;
            Fill(0.08, line, end, line + 0.012, 0xFFC4CAD0);
        }
        Fill(0.46, 0.94, 0.54, 0.945, 0xFF8A949C);

        var bitmap = new WriteableBitmap(area.Size, dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var buffer = bitmap.Lock();
        for (var y = 0; y < area.Height; y++)
        {
            Marshal.Copy(pixels, y * area.Width, buffer.Address + y * buffer.RowBytes, area.Width);
        }
        return bitmap;
    }

    private static readonly uint[] s_hues = [0xFF2F7F9B, 0xFFC96F4A, 0xFF5B8A5F, 0xFF7A63B5, 0xFFD2A53A, 0xFF3D6FB5];

    private static uint Darken(uint argb)
    {
        var r = (argb >> 16 & 0xFF) * 3 / 4;
        var g = (argb >> 8 & 0xFF) * 3 / 4;
        var b = (argb & 0xFF) * 3 / 4;
        return 0xFF000000 | r << 16 | g << 8 | b;
    }

    /// <summary>The page number as seven-segment digits, so that pages tell apart without text rendering.</summary>
    private static void DrawNumber(int number, double x, double y, Action<double, double, double, double, uint> fill)
    {
        const double W = 0.06;
        const double H = 0.05;
        const double T = 0.012;
        var text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var digit in text)
        {
            var mask = s_segments[digit - '0'];
            void Segment(int bit, double x0, double y0, double x1, double y1)
            {
                if ((mask >> bit & 1) != 0)
                {
                    fill(x + x0, y + y0, x + x1, y + y1, 0xFFFFFFFF);
                }
            }

            Segment(0, 0, 0, W, T * 0.8);
            Segment(1, W - T, 0, W, H);
            Segment(2, W - T, H, W, 2 * H);
            Segment(3, 0, 2 * H - T * 0.8, W, 2 * H);
            Segment(4, 0, H, T, 2 * H);
            Segment(5, 0, 0, T, H);
            Segment(6, 0, H - T * 0.4, W, H + T * 0.4);
            x += W + 0.02;
        }
    }

    private static readonly byte[] s_segments = [0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F];

    /// <summary>Renders off the UI thread and hands the bitmap to the viewer, which disposes it after evicting it.</summary>
    private sealed class GeneratedPageSource : IPageSource
    {
        private static readonly Size s_pageSize = new(612, 792);

        public GeneratedPageSource(int pageCount)
        {
            PageCount = pageCount;
        }

        public int PageCount { get; }

        public event EventHandler? PagesChanged
        {
            add { }
            remove { }
        }

        public Size GetPageSize(int pageIndex) => s_pageSize;

        public async ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new PageImage(Paint(request.PageIndex, request.TargetSize, request.Region, new Vector(96, 96)));
            }, cancellationToken);
        }
    }
}
