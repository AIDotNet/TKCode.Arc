using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The sample pictures of the Image trail demos, painted in code (gradient skies, layered hills, a sun or moon, and simple
/// shapes) and rendered once into bitmaps, so the gallery needs no photo files or outside service.
/// </summary>
public static class ImageTrailDemoData
{
    // Sky top, sky bottom, far hills, near hills, sun: twelve invented times of day and seasons.
    private static readonly string[][] Palettes =
    [
        ["#F6C28B", "#FBE7C6", "#C98A6B", "#7A4E45", "#FFF3D6"],
        ["#9CC9E8", "#E4F1F7", "#7FA88C", "#3F6B55", "#FFFFFF"],
        ["#2B3A67", "#7E6B9A", "#4A4E7A", "#23264A", "#F4E3B5"],
        ["#F2A07B", "#F7D4B4", "#B86B5E", "#5E3A3E", "#FFE7C2"],
        ["#BFE3D6", "#F2F7EF", "#8CBF9F", "#4C8064", "#FFFBEA"],
        ["#1D2B3A", "#3E5C76", "#2A4458", "#152230", "#E8EEF5"],
        ["#E8B4C8", "#F7E3EA", "#B5879E", "#6E4A63", "#FFF0F5"],
        ["#A7C4E5", "#EEF3F9", "#C8D6E5", "#8FA5BD", "#FFFFFF"],
        ["#F5D06F", "#FBEFC4", "#C9A24E", "#7D6230", "#FFFBE6"],
        ["#6FA8A0", "#D5ECE6", "#4F8478", "#2C5149", "#F7FFF9"],
        ["#D98E5F", "#F3CBA5", "#9E5C3E", "#4E2E22", "#FFE1BF"],
        ["#8E9BC7", "#DCE1F2", "#6A759E", "#3B4266", "#FDF8E8"],
    ];

    private static IReadOnlyList<ImageSource>? s_photos;
    private static IReadOnlyList<ImageSource>? s_squares;

    /// <summary>Twelve 4:3 landscapes, painted on first use.</summary>
    public static IReadOnlyList<ImageSource> Photos =>
        s_photos ??= Enumerable.Range(0, 12).Select(i => ImageSource.FromBitmap(Paint(i, 320, 240))).ToArray();

    /// <summary>Eight square landscapes for the small trail, painted on first use.</summary>
    public static IReadOnlyList<ImageSource> Squares =>
        s_squares ??= Enumerable.Range(0, 8).Select(i => ImageSource.FromBitmap(Paint(i * 3 % 12, 200, 200))).ToArray();

    private static Bitmap Paint(int index, int width, int height)
    {
        var target = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        {
            Paint(context, Palettes[index % Palettes.Length], index, width, height);
        }
        return target;
    }

    private static void Paint(DrawingContext context, string[] palette, int index, double w, double h)
    {
        var sky = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse(palette[0]), 0), new GradientStop(Color.Parse(palette[1]), 0.75) },
        };
        context.FillRectangle(sky, new Rect(0, 0, w, h));
        // The sun or moon sits somewhere different in every picture.
        var sunX = w * (0.2 + 0.6 * (index * 37 % 11) / 10.0);
        context.DrawEllipse(new SolidColorBrush(Color.Parse(palette[4]), 0.9), null, new Point(sunX, h * 0.3), w * 0.08, w * 0.08);
        context.DrawGeometry(new SolidColorBrush(Color.Parse(palette[2])), null, Hills(w, h, 0.58, 0.12, index));
        context.DrawGeometry(new SolidColorBrush(Color.Parse(palette[3])), null, Hills(w, h, 0.74, 0.08, index + 5));
        // A few trees along the near ridge on every other picture.
        if (index % 2 == 0)
        {
            var tree = new SolidColorBrush(Color.Parse(palette[3]));
            for (var t = 0; t < 4; t++)
            {
                var x = w * (0.15 + 0.2 * t + 0.04 * (index % 3));
                var baseY = h * 0.76;
                var crown = Geometry.Parse(FormattableString.Invariant(
                    $"M{x - w * 0.03},{baseY} L{x},{baseY - h * 0.16} L{x + w * 0.03},{baseY} Z"));
                context.DrawGeometry(tree, null, crown);
            }
        }
    }

    /// <summary>A ridge line across the picture at <paramref name="level"/> of its height, filled down to the bottom.</summary>
    private static Geometry Hills(double w, double h, double level, double swing, int seed)
    {
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(0, h), true);
            const int steps = 24;
            for (var s = 0; s <= steps; s++)
            {
                var x = w * s / steps;
                var y = h * (level - swing * (0.6 * Math.Sin(s * 0.45 + seed) + 0.4 * Math.Sin(s * 1.1 + seed * 2)));
                path.LineTo(new Point(x, y));
            }
            path.LineTo(new Point(w, h));
            path.EndFigure(true);
        }
        return geometry;
    }
}
