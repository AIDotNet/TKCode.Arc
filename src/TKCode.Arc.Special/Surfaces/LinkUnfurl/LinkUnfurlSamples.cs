using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Invented sample links for <see cref="LinkUnfurl"/> demos: three fictional sites (a trail guide, a recipe box, a
/// history podcast) with code-drawn favicons and share images, plus the starting message. None of them is a real site.
/// </summary>
public static class LinkUnfurlSamples
{
    private static IReadOnlyList<LinkUnfurlPreview>? s_previews;

    /// <summary>The composer's name and placeholder in the demos.</summary>
    public const string Channel = "Message #trail-club";

    /// <summary>The demos' starting message (a trailing space so a pasted link reads after it).</summary>
    public const string Message = "Saturday's route is settled, details below ";

    /// <summary>A plain address with no sample behind it: it unfurls into a text-only card built from the address.</summary>
    public const string PlainAddress = "https://example.org/packing-list";

    /// <summary>A trail guide: a two-line description and a mountain lakes image.</summary>
    public static LinkUnfurlPreview Trail => Previews[0];

    /// <summary>A recipe: a one-line description and a noodle bowl image.</summary>
    public static LinkUnfurlPreview Recipe => Previews[1];

    /// <summary>A podcast episode: a short description and a waveform image.</summary>
    public static LinkUnfurlPreview Episode => Previews[2];

    /// <summary>The three samples, in the order of the demo's buttons.</summary>
    public static IReadOnlyList<LinkUnfurlPreview> Previews => s_previews ??= Create();

    private static IReadOnlyList<LinkUnfurlPreview> Create() =>
    [
        new LinkUnfurlPreview("https://www.ridgeway.trails/routes/48213907", "Ridgeway", "Seven lakes loop above the timber line")
        {
            Description = "Day hike of 14 km with 900 m of climbing. Best from July, once the upper passes lose their snow.",
            Image = LinkUnfurlArt.Lakes(),
            ImageAlt = "Two blue lakes under jagged peaks at first light",
            Favicon = LinkUnfurlArt.PeakIcon()
        },
        new LinkUnfurlPreview("https://panfold.kitchen/r/m82q/miso-butter-noodles", "Panfold", "Miso butter noodles, one pan")
        {
            Description = "Serves four · vegetarian · ready in twenty minutes",
            Image = LinkUnfurlArt.Bowl(),
            ImageAlt = "A bowl of noodles with greens seen from above",
            Favicon = LinkUnfurlArt.PanIcon()
        },
        new LinkUnfurlPreview("https://play.longwave.fm/ep/Zr4Kq8LmT2wVa9NcB1yXpE", "Longwave", "Maps that lied")
        {
            Description = "Episode 41 · 48 min · the history of phantom islands",
            Image = LinkUnfurlArt.Waveform(),
            ImageAlt = "A violet sound wave over a faded chart grid",
            Favicon = LinkUnfurlArt.RadioIcon()
        }
    ];
}

/// <summary>The samples' favicons and share images, drawn from shapes and gradients (no image files).</summary>
internal static class LinkUnfurlArt
{
    private const double W = 382;
    private const double H = 200;

    private static IBrush Solid(string color) => new SolidColorBrush(Color.Parse(color));

    private static IBrush Vertical(params (double Offset, string Color)[] stops) => Gradient(new RelativePoint(0.5, 0, RelativeUnit.Relative),
        new RelativePoint(0.5, 1, RelativeUnit.Relative), stops);

    private static IBrush Gradient(RelativePoint start, RelativePoint end, params (double Offset, string Color)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        foreach (var (offset, color) in stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.Parse(color), offset));
        }
        return brush;
    }

    private static void Rect(DrawingGroup group, IBrush brush, double x, double y, double w, double h, double r = 0) =>
        group.Children.Add(new GeometryDrawing { Brush = brush, Geometry = new RectangleGeometry(new Rect(x, y, w, h), r, r) });

    private static void Ellipse(DrawingGroup group, IBrush? brush, IPen? pen, double cx, double cy, double rx, double ry) =>
        group.Children.Add(new GeometryDrawing { Brush = brush, Pen = pen, Geometry = new EllipseGeometry(new Rect(cx - rx, cy - ry, 2 * rx, 2 * ry)) });

    private static void Path(DrawingGroup group, IBrush? brush, IPen? pen, string data) =>
        group.Children.Add(new GeometryDrawing { Brush = brush, Pen = pen, Geometry = StreamGeometry.Parse(data) });

    /// <summary>Mountain lakes at first light: a pale sky, layered peaks with snow caps and two lakes.</summary>
    public static IImage Lakes()
    {
        var g = new DrawingGroup();
        Rect(g, Vertical((0, "#9EC3E6"), (0.6, "#F3D9C4"), (1, "#F7E9DA")), 0, 0, W, 120);
        Path(g, Solid("#7C8DA6"), null, "M0,120 L0,70 L46,34 L80,62 L128,18 L170,58 L214,30 L262,66 L308,24 L354,56 L382,44 L382,120 Z");
        Path(g, Solid("#FFFFFF"), null, "M128,18 L112,34 L122,32 L130,38 L140,30 Z M308,24 L292,40 L304,38 L312,44 L322,36 Z M46,34 L36,42 L48,42 Z");
        Path(g, Solid("#4E6079"), null, "M0,132 L0,96 L60,70 L110,92 L170,64 L236,96 L300,72 L382,98 L382,132 Z");
        Rect(g, Vertical((0, "#2E6FA8"), (1, "#163C63")), 0, 120, W, H - 120);
        Ellipse(g, Solid("#3E86C4"), null, 120, 150, 80, 14);
        Ellipse(g, Solid("#3E86C4"), null, 290, 168, 64, 10);
        Path(g, Solid("#2F4A35"), null, "M0,200 L0,176 L40,160 L90,184 L150,170 L210,190 L280,176 L340,192 L382,180 L382,200 Z");
        var pine = Solid("#22382A");
        foreach (var x in new[] { 18.0, 34, 60, 324, 348, 366 })
        {
            Path(g, pine, null, $"M{x},{150} L{x - 7},{176} L{x + 7},{176} Z");
        }
        return new DrawingImage(g);
    }

    /// <summary>A noodle bowl from above on a warm table, with greens, an egg half and chopsticks.</summary>
    public static IImage Bowl()
    {
        var g = new DrawingGroup();
        Rect(g, Vertical((0, "#E9C9A0"), (1, "#D9AE7E")), 0, 0, W, H);
        Ellipse(g, Solid("#F7F1E8"), null, 191, 100, 84, 84);
        Ellipse(g, Solid("#C9853E"), null, 191, 100, 70, 70);
        var noodle = new Pen(Solid("#F2D38C"), 3, lineCap: PenLineCap.Round);
        for (var i = 0; i < 7; i++)
        {
            Path(g, null, noodle, $"M{140 + i * 6},{70 + i * 9} Q{190},{50 + i * 12} {244 - i * 4},{78 + i * 8}");
        }
        Ellipse(g, Solid("#FFFFFF"), null, 222, 82, 18, 14);
        Ellipse(g, Solid("#F5B335"), null, 224, 82, 9, 8);
        foreach (var (x, y) in new[] { (160.0, 120.0), (176.0, 132.0), (200.0, 128.0), (150.0, 98.0) })
        {
            Ellipse(g, Solid("#4E9A47"), null, x, y, 9, 5);
        }
        var stick = new Pen(Solid("#5B3A22"), 4, lineCap: PenLineCap.Round);
        Path(g, null, stick, "M300,20 L352,186 M316,16 L366,180");
        return new DrawingImage(g);
    }

    /// <summary>A sound wave over a faded chart grid, on deep violet.</summary>
    public static IImage Waveform()
    {
        var g = new DrawingGroup();
        Rect(g, Gradient(new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(1, 1, RelativeUnit.Relative),
            (0, "#1E1442"), (0.6, "#3A2479"), (1, "#6A44B8")), 0, 0, W, H);
        var grid = new Pen(new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)), 1);
        for (var x = 0.0; x <= W; x += 32)
        {
            Path(g, null, grid, $"M{x},0 L{x},{H}");
        }
        for (var y = 0.0; y <= H; y += 32)
        {
            Path(g, null, grid, $"M0,{y} L{W},{y}");
        }
        var bar = Solid("#E6D8FF");
        for (var i = 0; i < 34; i++)
        {
            var h = 10 + 70 * Math.Abs(Math.Sin(i * 0.55) * Math.Cos(i * 0.21));
            Rect(g, bar, 24 + i * 10, 100 - h / 2, 5, h, 2.5);
        }
        Ellipse(g, null, new Pen(Solid("#FFC861"), 2), 300, 52, 16, 16);
        return new DrawingImage(g);
    }

    private static DrawingGroup Icon()
    {
        var g = new DrawingGroup();
        Rect(g, Brushes.Transparent, 0, 0, 16, 16);
        return g;
    }

    /// <summary>A green mountain peak with a snow cap.</summary>
    public static IImage PeakIcon()
    {
        var g = Icon();
        Path(g, Solid("#2F8F5B"), null, "M1,14 L6.2,4.5 L9,9 L11,6.5 L15,14 Z");
        Path(g, Solid("#FFFFFF"), null, "M6.2,4.5 L4.9,6.9 L6.4,6.3 L7.4,6.8 Z");
        return new DrawingImage(g);
    }

    /// <summary>An orange pan seen from the side.</summary>
    public static IImage PanIcon()
    {
        var g = Icon();
        Rect(g, Solid("#F07A2E"), 1.5, 7, 9.5, 5, 2.4);
        Rect(g, Solid("#7A4A2A"), 10.5, 8.2, 4.5, 1.8, 0.9);
        Rect(g, Solid("#FFD3A8"), 3, 5, 6.5, 1.6, 0.8);
        return new DrawingImage(g);
    }

    /// <summary>A violet square with three wave bars.</summary>
    public static IImage RadioIcon()
    {
        var g = Icon();
        Rect(g, Solid("#5B3CC4"), 0.5, 0.5, 15, 15, 4);
        Rect(g, Solid("#FFFFFF"), 4, 6, 1.8, 4, 0.9);
        Rect(g, Solid("#FFFFFF"), 7.1, 3.5, 1.8, 9, 0.9);
        Rect(g, Solid("#FFFFFF"), 10.2, 5, 1.8, 6, 0.9);
        return new DrawingImage(g);
    }
}
