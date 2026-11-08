using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>The still lifes drawn for <see cref="CartDrawerSamples"/>: simple vector scenes, 4:5, in warm greys.</summary>
internal enum CartDrawerArtworkKind
{
    Chair,
    Lamp,
    Jug,
    Mugs,
    Dishes,
    Blanket
}

/// <summary>
/// Code-generated product pictures (no photographs): a wall gradient, a surface and one object in flat shapes, drawn as
/// a 400 × 500 <see cref="DrawingImage"/> so it stays sharp at any size, light or dark theme.
/// </summary>
internal static class CartDrawerArtwork
{
    private const double W = 400;
    private const double H = 500;

    private static readonly Dictionary<CartDrawerArtworkKind, DrawingImage> s_cache = new();

    /// <summary>The picture of <paramref name="kind"/> (built once).</summary>
    public static DrawingImage Get(CartDrawerArtworkKind kind)
    {
        if (!s_cache.TryGetValue(kind, out var image))
        {
            image = new DrawingImage(Build(kind));
            s_cache[kind] = image;
        }
        return image;
    }

    private static DrawingGroup Build(CartDrawerArtworkKind kind)
    {
        var g = new DrawingGroup();
        switch (kind)
        {
            case CartDrawerArtworkKind.Chair:
                Chair(g);
                break;
            case CartDrawerArtworkKind.Lamp:
                Lamp(g);
                break;
            case CartDrawerArtworkKind.Jug:
                Jug(g);
                break;
            case CartDrawerArtworkKind.Mugs:
                Mugs(g);
                break;
            case CartDrawerArtworkKind.Dishes:
                Dishes(g);
                break;
            default:
                Blanket(g);
                break;
        }
        return g;
    }

    private static void Chair(DrawingGroup g)
    {
        Scene(g, "#DAD5CD", "#CBC5BB", 372, "#B5AEA3", "#A39B90");
        // A window strip on the right lets daylight in.
        Fill(g, new RectangleGeometry(new Rect(352, 0, 48, 372)), Vertical("#EEF0EE", "#DADDD9"));
        Fill(g, new RectangleGeometry(new Rect(346, 0, 6, 372)), Solid("#3C3A37"));
        Fill(g, new EllipseGeometry(new Rect(110, 420, 250, 26)), Solid("#000000", 0.14));
        var wood = Solid("#A26F3F");
        var woodDark = Solid("#7E522B");
        Stroke(g, "M150 352 L128 432 M318 330 L338 424 M182 346 L200 428 M292 334 L276 418", woodDark, 9);
        Fill(g, Geometry.Parse("M118 168 L156 154 L238 326 L198 342 Z"), wood);
        Fill(g, Geometry.Parse("M150 330 L300 302 L330 330 L178 360 Z"), wood);
        Stroke(g, "M136 196 L172 184 M146 224 L184 211 M157 252 L196 238 M168 280 L208 265", Solid("#C99A66"), 3);
        // Wool throw over the back.
        Fill(g, Geometry.Parse("M130 160 C150 140 190 150 196 176 C206 210 230 250 236 300 C214 310 196 300 186 280 C170 240 140 200 130 160 Z"),
            Solid("#F2EDE4"));
        Fill(g, Geometry.Parse("M206 300 C230 290 262 292 276 306 C262 318 232 322 206 316 Z"), Solid("#E7E0D3"));
    }

    private static void Lamp(DrawingGroup g)
    {
        Scene(g, "#E7DECF", "#D6C9B7", 366, "#4C3C30", "#3A2E25");
        Fill(g, new EllipseGeometry(new Rect(40, 40, 340, 300)), Radial("#FFF3D6", 0.75, "#E7DECF", 0));
        Fill(g, new EllipseGeometry(new Rect(150, 352, 130, 16)), Solid("#000000", 0.25));
        Fill(g, Geometry.Parse("M196 210 L226 210 L250 358 L172 358 Z"), Vertical("#FBF7F0", "#E3DCD0"));
        Fill(g, Geometry.Parse("M112 214 A100 92 0 0 1 312 214 Z"), Vertical("#FFF8E6", "#F0DFBA"));
        Fill(g, new RectangleGeometry(new Rect(112, 210, 200, 6)), Solid("#E6D3AC"));
        // Books on the left.
        Fill(g, new RectangleGeometry(new Rect(40, 230, 26, 136)), Solid("#CDBFAE"));
        Fill(g, new RectangleGeometry(new Rect(68, 222, 22, 144)), Solid("#EDE6DA"));
        Fill(g, new RectangleGeometry(new Rect(92, 240, 24, 126)), Solid("#B49A86"));
    }

    private static void Jug(DrawingGroup g)
    {
        Scene(g, "#E6E8EA", "#D3D7DA", 380, "#CFCAC2", "#BDB7AE");
        Fill(g, new EllipseGeometry(new Rect(130, 392, 150, 18)), Solid("#000000", 0.12));
        var body = Geometry.Parse("M150 170 L250 170 C256 170 260 176 258 186 L250 380 C250 392 244 398 232 398 L168 398 C156 398 150 392 150 380 L142 186 C140 176 144 170 150 170 Z");
        Fill(g, body, Vertical("#C7D3DA", "#9FB2BD", 0.8));
        Fill(g, Geometry.Parse("M151 280 L249 280 L250 380 C250 392 244 398 232 398 L168 398 C156 398 150 392 150 380 Z"), Solid("#8FA7B5", 0.55));
        Stroke(g, "M164 186 L168 390 M182 184 L184 392 M200 184 L200 392 M218 184 L216 392 M236 186 L232 390", Solid("#FFFFFF", 0.55), 3);
        Stroke(g, "M256 200 C300 206 300 300 254 312", Solid("#AFC0CA"), 10);
        Stroke(g, "M146 168 L254 168", Solid("#E8EEF1"), 6);
        // A small glass beside it.
        Fill(g, Geometry.Parse("M282 318 L330 318 L326 398 L286 398 Z"), Vertical("#DCE4E8", "#B8C7CF", 0.75));
    }

    private static void Mugs(DrawingGroup g)
    {
        Scene(g, "#CFCBC2", "#BEB9AF", 360, "#E9E5DD", "#DCD7CE");
        Fill(g, new EllipseGeometry(new Rect(70, 394, 290, 22)), Solid("#000000", 0.12));
        Mug(g, 84, 250, 130, 150, "#ECE5D9", "#D8CFC1");
        Mug(g, 210, 280, 120, 124, "#8E7C69", "#766554");
        var speck = Solid("#6E6255");
        foreach (var (x, y) in new[] { (104, 300), (130, 336), (170, 290), (190, 360), (118, 372), (152, 318), (184, 330), (98, 344) })
        {
            Fill(g, new EllipseGeometry(new Rect(x, y, 3.5, 3.5)), speck);
        }
    }

    private static void Mug(DrawingGroup g, double x, double y, double w, double h, string light, string dark)
    {
        Fill(g, new RectangleGeometry(new Rect(x, y, w, h), 10, 10), Horizontal(light, dark));
        Fill(g, new EllipseGeometry(new Rect(x, y - 12, w, 24)), Solid(dark));
        Fill(g, new EllipseGeometry(new Rect(x + 6, y - 8, w - 12, 16)), Solid("#5E5247", 0.55));
    }

    private static void Dishes(DrawingGroup g)
    {
        Scene(g, "#403C39", "#2F2D2B", 352, "#5A544E", "#4A4540");
        Fill(g, new EllipseGeometry(new Rect(70, 392, 270, 26)), Solid("#000000", 0.3));
        var plate = Horizontal("#E1DACF", "#BDB4A7");
        for (var i = 0; i < 3; i++)
        {
            var top = 380 - i * 26;
            Fill(g, new EllipseGeometry(new Rect(80, top - 6, 250, 40)), Solid("#9E9488"));
            Fill(g, new EllipseGeometry(new Rect(80, top - 14, 250, 40)), plate);
        }
        Fill(g, Geometry.Parse("M130 300 C130 250 280 250 280 300 C280 336 130 336 130 300 Z"), Horizontal("#EAE4DA", "#C8BFB2"));
        Fill(g, new EllipseGeometry(new Rect(130, 282, 150, 36)), Solid("#D9D1C5"));
        Fill(g, new EllipseGeometry(new Rect(146, 288, 118, 24)), Solid("#B9AFA2"));
    }

    private static void Blanket(DrawingGroup g)
    {
        Scene(g, "#6F6C68", "#595652", 330, "#B8A890", "#A6967E");
        Fill(g, new EllipseGeometry(new Rect(60, 390, 300, 24)), Solid("#000000", 0.22));
        Fill(g, new RectangleGeometry(new Rect(64, 300, 290, 96), 18, 18), Vertical("#C4B49C", "#A99880"));
        Fill(g, new RectangleGeometry(new Rect(80, 236, 262, 78), 20, 20), Vertical("#F3EEE6", "#DDD5C8"));
        Fill(g, new RectangleGeometry(new Rect(96, 186, 236, 64), 20, 20), Vertical("#F7F3EC", "#E4DDD1"));
        var waffle = Solid("#BDB2A2", 0.55);
        Stroke(g, "M110 258 L330 258 M110 280 L330 280 M120 200 L320 200 M120 222 L320 222", waffle, 1.5);
        Stroke(g, "M130 240 L130 312 M170 240 L170 312 M210 240 L210 312 M250 240 L250 312 M290 240 L290 312", waffle, 1.5);
        Stroke(g, "M90 396 L86 420 M110 396 L108 422 M130 396 L130 420 M150 396 L152 422 M170 396 L172 420 M190 396 L194 422 M210 396 L214 420",
            Solid("#D8CDBB"), 2);
    }

    // ---- Building blocks ----

    private static void Scene(DrawingGroup g, string wallTop, string wallBottom, double horizon, string floorTop, string floorBottom)
    {
        Fill(g, new RectangleGeometry(new Rect(0, 0, W, H)), Vertical(wallTop, wallBottom));
        Fill(g, new RectangleGeometry(new Rect(0, horizon, W, H - horizon)), Vertical(floorTop, floorBottom));
    }

    private static void Fill(DrawingGroup g, Geometry geometry, IBrush brush)
        => g.Children.Add(new GeometryDrawing { Geometry = geometry, Brush = brush });

    private static void Stroke(DrawingGroup g, string data, IBrush brush, double thickness)
        => g.Children.Add(new GeometryDrawing
        {
            Geometry = Geometry.Parse(data),
            Pen = new ImmutablePen(brush.ToImmutable(), thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round)
        });

    private static IBrush Solid(string hex, double alpha = 1)
    {
        var c = Color.Parse(hex);
        return new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(alpha * 255), c.R, c.G, c.B));
    }

    private static IBrush Vertical(string top, string bottom, double opacity = 1)
        => Gradient(top, bottom, new RelativePoint(0.5, 0, RelativeUnit.Relative), new RelativePoint(0.5, 1, RelativeUnit.Relative), opacity);

    private static IBrush Horizontal(string left, string right)
        => Gradient(left, right, new RelativePoint(0, 0.5, RelativeUnit.Relative), new RelativePoint(1, 0.5, RelativeUnit.Relative), 1);

    private static IBrush Gradient(string from, string to, RelativePoint start, RelativePoint end, double opacity)
        => new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, Color.Parse(from)), new ImmutableGradientStop(1, Color.Parse(to))],
            opacity, startPoint: start, endPoint: end);

    private static IBrush Radial(string center, double centerAlpha, string edge, double edgeAlpha)
    {
        var c = Color.Parse(center);
        var e = Color.Parse(edge);
        return new ImmutableRadialGradientBrush(
            [
                new ImmutableGradientStop(0, Color.FromArgb((byte)Math.Round(centerAlpha * 255), c.R, c.G, c.B)),
                new ImmutableGradientStop(1, Color.FromArgb((byte)Math.Round(edgeAlpha * 255), e.R, e.G, e.B))
            ], 1, null, null, GradientSpreadMethod.Pad, null, null, 0.5);
    }
}
