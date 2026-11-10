using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>The objects drawn by <see cref="ProductListingArtwork"/>.</summary>
public enum ProductListingArtworkKind
{
    /// <summary>A canvas tote bag.</summary>
    Tote,

    /// <summary>An insulated water bottle.</summary>
    Bottle,

    /// <summary>A stoneware mug.</summary>
    Mug,

    /// <summary>A six-panel cap.</summary>
    Cap,

    /// <summary>A candle in a glass.</summary>
    Candle,

    /// <summary>A cloth-bound notebook.</summary>
    Notebook
}

/// <summary>
/// Product pictures drawn in code for the Commerce samples (no photographs): one object in flat shapes on a soft backdrop,
/// in any color, as a 400 × 500 <see cref="DrawingImage"/>. Four views per object: front, turned, a close-up and a styled
/// shot on a deeper backdrop. Images are cached by object, color and view.
/// </summary>
public static class ProductListingArtwork
{
    private const double W = 400;
    private const double H = 500;

    private static readonly Dictionary<(ProductListingArtworkKind, Color, int), DrawingImage> s_cache = new();

    /// <summary>The picture of <paramref name="kind"/> in <paramref name="color"/>; <paramref name="view"/> 0–3 picks the shot.</summary>
    public static IImage Create(ProductListingArtworkKind kind, Color color, int view = 0)
    {
        view = Math.Clamp(view, 0, 3);
        var key = (kind, color, view);
        if (!s_cache.TryGetValue(key, out var image))
        {
            image = new DrawingImage(Build(kind, color, view));
            s_cache[key] = image;
        }
        return image;
    }

    private static DrawingGroup Build(ProductListingArtworkKind kind, Color color, int view)
    {
        var root = new DrawingGroup();
        // Backdrop: a pale wash of the product color (deeper for the styled shot) and a floor.
        var wall = Mix(color, Colors.White, view == 3 ? 0.62 : 0.86);
        var floor = Mix(color, Colors.White, view == 3 ? 0.5 : 0.78);
        Fill(root, new RectangleGeometry(new Rect(0, 0, W, H)), Vertical(wall, Mix(wall, Colors.Black, 0.04)));
        Fill(root, new RectangleGeometry(new Rect(0, 370, W, H - 370)), Vertical(floor, Mix(floor, Colors.Black, 0.06)));

        var subject = new DrawingGroup();
        Fill(subject, new EllipseGeometry(new Rect(110, 392, 180, 22)), Solid(Color.FromArgb(40, 0, 0, 0)));
        switch (kind)
        {
            case ProductListingArtworkKind.Tote:
                Tote(subject, color);
                break;
            case ProductListingArtworkKind.Bottle:
                Bottle(subject, color);
                break;
            case ProductListingArtworkKind.Mug:
                Mug(subject, color);
                break;
            case ProductListingArtworkKind.Cap:
                Cap(subject, color);
                break;
            case ProductListingArtworkKind.Candle:
                Candle(subject, color);
                break;
            default:
                Notebook(subject, color);
                break;
        }
        subject.Transform = view switch
        {
            1 => new MatrixTransform(Matrix.CreateTranslation(-200, -300) * Matrix.CreateSkew(-0.12, 0) * Matrix.CreateScale(0.86, 0.92)
                                     * Matrix.CreateTranslation(214, 312)),
            2 => new MatrixTransform(Matrix.CreateTranslation(-200, -250) * Matrix.CreateScale(1.9, 1.9) * Matrix.CreateTranslation(200, 300)),
            3 => new MatrixTransform(Matrix.CreateScale(0.8, 0.8) * Matrix.CreateTranslation(110, 70)),
            _ => null,
        };
        root.Children.Add(subject);
        root.ClipGeometry = new RectangleGeometry(new Rect(0, 0, W, H));
        return root;
    }

    private static void Tote(DrawingGroup g, Color c)
    {
        var dark = Mix(c, Colors.Black, 0.25);
        Stroke(g, "M150 200 C150 110 250 110 250 200", Solid(dark), 9);
        Fill(g, Geometry.Parse("M110 200 L290 200 L304 400 L96 400 Z"), Vertical(c, Mix(c, Colors.Black, 0.12)));
        Fill(g, Geometry.Parse("M150 290 L250 290 L252 352 L148 352 Z"), Solid(Mix(c, Colors.Black, 0.1)));
        Stroke(g, "M112 214 L288 214", Solid(Mix(c, Colors.White, 0.35)), 2);
        Fill(g, new RectangleGeometry(new Rect(186, 310, 28, 16), 3, 3), Solid(Mix(c, Colors.White, 0.7)));
    }

    private static void Bottle(DrawingGroup g, Color c)
    {
        Fill(g, new RectangleGeometry(new Rect(172, 112, 56, 38), 8, 8), Solid(Color.Parse("#2B2B2E")));
        Fill(g, Geometry.Parse("M180 150 L220 150 C222 170 252 180 252 210 L252 384 C252 396 244 402 232 402 L168 402 C156 402 148 396 148 384 L148 210 C148 180 178 170 180 150 Z"),
            Horizontal(Mix(c, Colors.White, 0.2), Mix(c, Colors.Black, 0.18)));
        Fill(g, new RectangleGeometry(new Rect(160, 214, 10, 160), 5, 5), Solid(Color.FromArgb(70, 255, 255, 255)));
        Fill(g, new RectangleGeometry(new Rect(148, 300, 104, 26)), Solid(Mix(c, Colors.Black, 0.22)));
    }

    private static void Mug(DrawingGroup g, Color c)
    {
        Stroke(g, "M262 262 C318 262 318 352 262 352", Solid(Mix(c, Colors.Black, 0.15)), 14);
        Fill(g, new RectangleGeometry(new Rect(126, 236, 150, 168), 16, 16), Horizontal(Mix(c, Colors.White, 0.15), Mix(c, Colors.Black, 0.15)));
        Fill(g, new EllipseGeometry(new Rect(126, 224, 150, 26)), Solid(Mix(c, Colors.Black, 0.08)));
        Fill(g, new EllipseGeometry(new Rect(134, 229, 134, 18)), Solid(Color.Parse("#4A3626")));
        var speck = Solid(Mix(c, Colors.Black, 0.35));
        foreach (var (x, y) in new[] { (150, 280), (190, 320), (230, 296), (170, 360), (246, 370), (210, 260) })
        {
            Fill(g, new EllipseGeometry(new Rect(x, y, 3.5, 3.5)), speck);
        }
    }

    private static void Cap(DrawingGroup g, Color c)
    {
        Fill(g, Geometry.Parse("M90 352 C120 330 200 326 300 340 C320 344 320 362 300 364 C220 366 140 370 96 372 C80 372 78 360 90 352 Z"),
            Solid(Mix(c, Colors.Black, 0.2)));
        Fill(g, Geometry.Parse("M120 344 C116 250 170 206 220 206 C276 206 318 252 312 340 C260 330 170 330 120 344 Z"),
            Vertical(Mix(c, Colors.White, 0.12), c));
        Stroke(g, "M216 210 C210 250 210 300 214 334 M168 226 C178 262 186 300 190 334 M266 228 C258 262 252 300 248 334", Solid(Mix(c, Colors.Black, 0.18)), 2);
        Fill(g, new EllipseGeometry(new Rect(208, 198, 16, 12)), Solid(Mix(c, Colors.Black, 0.25)));
    }

    private static void Candle(DrawingGroup g, Color c)
    {
        Fill(g, new RectangleGeometry(new Rect(136, 228, 128, 176), 18, 18), Vertical(Color.FromArgb(150, 255, 255, 255), Color.FromArgb(200, 230, 230, 230)));
        Fill(g, new RectangleGeometry(new Rect(146, 260, 108, 136), 12, 12), Vertical(Mix(c, Colors.White, 0.3), c));
        Fill(g, new RectangleGeometry(new Rect(160, 300, 80, 46), 4, 4), Solid(Color.Parse("#F7F3EA")));
        Stroke(g, "M200 260 L200 238", Solid(Color.Parse("#3A2E25")), 3);
        Fill(g, Geometry.Parse("M200 196 C212 214 212 232 200 238 C188 232 188 214 200 196 Z"), Vertical(Color.Parse("#FFE08A"), Color.Parse("#F59E3B")));
    }

    private static void Notebook(DrawingGroup g, Color c)
    {
        Fill(g, new RectangleGeometry(new Rect(128, 168, 158, 230), 6, 6), Solid(Color.Parse("#F3EFE6")));
        Fill(g, new RectangleGeometry(new Rect(118, 160, 158, 232), 8, 8), Vertical(Mix(c, Colors.White, 0.1), Mix(c, Colors.Black, 0.12)));
        Fill(g, new RectangleGeometry(new Rect(118, 160, 22, 232)), Solid(Mix(c, Colors.Black, 0.22)));
        Fill(g, new RectangleGeometry(new Rect(166, 210, 84, 36), 3, 3), Solid(Mix(c, Colors.White, 0.75)));
        Stroke(g, "M250 160 L250 424", Solid(Color.Parse("#C2453D")), 4);
    }

    // ---- Building blocks ----

    private static Color Mix(Color a, Color b, double t)
        => Color.FromArgb((byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private static void Fill(DrawingGroup g, Geometry geometry, IBrush brush)
        => g.Children.Add(new GeometryDrawing { Geometry = geometry, Brush = brush });

    private static void Stroke(DrawingGroup g, string data, IBrush brush, double thickness)
        => g.Children.Add(new GeometryDrawing
        {
            Geometry = Geometry.Parse(data),
            Pen = new ImmutablePen(brush.ToImmutable(), thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round)
        });

    private static IBrush Solid(Color color) => new ImmutableSolidColorBrush(color);

    private static IBrush Vertical(Color top, Color bottom)
        => new ImmutableLinearGradientBrush([new ImmutableGradientStop(0, top), new ImmutableGradientStop(1, bottom)],
            startPoint: new RelativePoint(0.5, 0, RelativeUnit.Relative), endPoint: new RelativePoint(0.5, 1, RelativeUnit.Relative));

    private static IBrush Horizontal(Color left, Color right)
        => new ImmutableLinearGradientBrush([new ImmutableGradientStop(0, left), new ImmutableGradientStop(1, right)],
            startPoint: new RelativePoint(0, 0.5, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0.5, RelativeUnit.Relative));
}
