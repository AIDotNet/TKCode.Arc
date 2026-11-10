using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The pictures of the Photos demos, painted in code so the gallery works offline and ships no photographs: landscapes
/// from a few palettes, a ceramic mug seen from several sides, and album and book covers with invented names. Each is
/// rendered once into a bitmap on first use and shared. A real app passes its own pictures (addresses or bitmaps).
/// </summary>
internal static class PhotosDemoArt
{
    private static readonly Dictionary<string, Bitmap> s_cache = new();

    /// <summary>A landscape palette: sky top and bottom, sun, three ridges from far to near, and the ground or water.</summary>
    private sealed record Palette(string Name, Color SkyTop, Color SkyBottom, Color Sun, Color Far, Color Mid, Color Near, Color Ground,
                                  bool Water = false, bool Stars = false, bool Trees = false, double SunHeight = 0.32);

    private static readonly Palette[] s_palettes =
    [
        new("Dawn ridge", C("#F6C9A8"), C("#FCE8D2"), C("#FFF4E0"), C("#C99A9E"), C("#8F6E86"), C("#4E4560"), C("#2F2B3D")),
        new("Harbour dusk", C("#2E3E6E"), C("#F09A6B"), C("#FFD7A3"), C("#5B5A86"), C("#3F4470"), C("#2A2F52"), C("#1E2746"), Water: true, SunHeight: 0.52),
        new("Pine valley", C("#BFE0E4"), C("#EEF6EC"), C("#FFFFFF"), C("#9CC3B6"), C("#5E9481"), C("#2F6152"), C("#1F4237"), Trees: true),
        new("Dune noon", C("#7DB8E8"), C("#D8ECF7"), C("#FFF8E1"), C("#E9C48E"), C("#D9A066"), C("#B97A43"), C("#9A5F33"), SunHeight: 0.18),
        new("Night pass", C("#0C1430"), C("#26305E"), C("#F3F0D8"), C("#2C3A6B"), C("#1F2A52"), C("#141C3B"), C("#0B1028"), Stars: true, SunHeight: 0.2),
        new("Meadow", C("#9FD3F0"), C("#E6F4F9"), C("#FFFDF2"), C("#A9C98E"), C("#7DB06A"), C("#4F8F48"), C("#3C7A3A"), Trees: true, SunHeight: 0.16),
        new("Snow peaks", C("#B9CCE6"), C("#EEF3FA"), C("#FFFFFF"), C("#DCE5F0"), C("#AFC0D6"), C("#7D91AE"), C("#E9EEF5")),
        new("Still lake", C("#F2B7A6"), C("#F9DCC4"), C("#FFF1DD"), C("#B98C9A"), C("#7F6A86"), C("#4D4A6A"), C("#3A3B5C"), Water: true, SunHeight: 0.46),
        new("Canyon", C("#F4A261"), C("#FBD9A0"), C("#FFF0C9"), C("#D9774B"), C("#B4573A"), C("#7F3B2C"), C("#5C2A22"), SunHeight: 0.22),
        new("Aurora", C("#06182A"), C("#0F3C4C"), C("#9EF0C9"), C("#173B4F"), C("#10293A"), C("#0A1C29"), C("#071420"), Stars: true, SunHeight: 0.12),
        new("Lagoon", C("#5EC6D9"), C("#C9F0EE"), C("#FFFBEA"), C("#7FCBB7"), C("#3FA68F"), C("#1F7F6E"), C("#2AA3B5"), Water: true, SunHeight: 0.14),
        new("Fog hills", C("#C9CDD3"), C("#ECEDEF"), C("#F7F7F5"), C("#B5B9C0"), C("#9298A3"), C("#6B7280"), C("#555B66"), SunHeight: 0.4)
    ];

    /// <summary>The number of landscapes.</summary>
    public static int SceneCount => s_palettes.Length;

    /// <summary>The name of a landscape.</summary>
    public static string SceneName(int scene) => s_palettes[Math.Abs(scene) % s_palettes.Length].Name;

    /// <summary>Landscape <paramref name="scene"/> at the given size (shared per scene and size).</summary>
    public static Bitmap Scene(int scene, int width, int height)
    {
        var palette = s_palettes[Math.Abs(scene) % s_palettes.Length];
        return Get($"scene:{scene}:{width}x{height}", width, height, (context, bounds) => PaintScene(context, bounds, palette, scene));
    }

    /// <summary>The demo mug from one of five views (front, handle side, top, glaze detail, on the table) in a glaze.</summary>
    public static Bitmap Mug(int view, int glaze = 0) =>
        Get($"mug:{view}:{glaze}", 900, 900, (context, bounds) => PaintMug(context, bounds, view, glaze));

    /// <summary>The number of mug views.</summary>
    public const int MugViews = 5;

    /// <summary>Square album cover <paramref name="index"/> with its invented title.</summary>
    public static Bitmap Album(int index, string title) =>
        Get($"album:{index}", 600, 600, (context, bounds) => PaintCover(context, bounds, index, title, book: false));

    /// <summary>A 2:3 book cover with its invented title.</summary>
    public static Bitmap Book(int index, string title) =>
        Get($"book:{index}", 400, 600, (context, bounds) => PaintCover(context, bounds, index, title, book: true));

    private static Bitmap Get(string key, int width, int height, Action<DrawingContext, Rect> paint)
    {
        if (s_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var target = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        {
            paint(context, new Rect(0, 0, width, height));
        }
        s_cache[key] = target;
        return target;
    }

    private static Color C(string hex) => Color.Parse(hex);

    private static IBrush Vertical(Color top, Color bottom, double split = 1) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(top, 0), new GradientStop(bottom, split) }
    };

    /// <summary>A small deterministic noise, so every picture is the same on every run.</summary>
    private static double Noise(int seed, int i)
    {
        var x = Math.Sin(seed * 12.9898 + i * 78.233) * 43758.5453;
        return x - Math.Floor(x);
    }

    private static void PaintScene(DrawingContext context, Rect bounds, Palette p, int seed)
    {
        var w = bounds.Width;
        var h = bounds.Height;
        var horizon = h * 0.62;
        context.FillRectangle(Vertical(p.SkyTop, p.SkyBottom), new Rect(0, 0, w, horizon + 2));
        if (p.Stars)
        {
            var star = new SolidColorBrush(Colors.White, 0.8);
            for (var i = 0; i < 90; i++)
            {
                var r = 0.6 + Noise(seed, i * 3) * 1.6;
                context.DrawEllipse(star, null, new Point(Noise(seed, i) * w, Noise(seed, i + 500) * horizon * 0.8), r, r);
            }
        }
        if (p.Name == "Aurora")
        {
            for (var band = 0; band < 3; band++)
            {
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(new Point(0, h * (0.22 + band * 0.06)), true);
                    for (var x = 0.0; x <= w; x += w / 24)
                    {
                        g.LineTo(new Point(x, h * (0.18 + band * 0.07) + Math.Sin(x / w * 7 + band) * h * 0.05));
                    }
                    g.LineTo(new Point(w, h * (0.42 + band * 0.04)));
                    g.LineTo(new Point(0, h * (0.42 + band * 0.04)));
                    g.EndFigure(true);
                }
                context.DrawGeometry(new SolidColorBrush(p.Sun, 0.16 - band * 0.03), null, geometry);
            }
        }
        var sunRadius = Math.Min(w, h) * 0.075;
        var sunCenter = new Point(w * (0.28 + Noise(seed, 7) * 0.44), h * p.SunHeight);
        context.DrawEllipse(new SolidColorBrush(p.Sun, 0.28), null, sunCenter, sunRadius * 1.9, sunRadius * 1.9);
        context.DrawEllipse(new SolidColorBrush(p.Sun), null, sunCenter, sunRadius, sunRadius);
        Ridge(context, w, horizon, h * 0.26, p.Far, seed, 0, 9);
        Ridge(context, w, horizon + h * 0.04, h * 0.2, p.Mid, seed, 1, 7);
        if (p.Water)
        {
            context.FillRectangle(Vertical(p.Ground, Color.FromArgb(255, (byte)(p.Ground.R / 2), (byte)(p.Ground.G / 2), (byte)(p.Ground.B / 2))),
                new Rect(0, horizon + h * 0.06, w, h));
            // The sun's path on the water.
            var glint = new SolidColorBrush(p.Sun, 0.5);
            for (var i = 0; i < 9; i++)
            {
                var y = horizon + h * (0.09 + i * 0.03);
                var half = sunRadius * (0.9 - i * 0.07);
                context.FillRectangle(glint, new Rect(sunCenter.X - half, y, half * 2, h * 0.008));
            }
            Ridge(context, w * 0.45, horizon + h * 0.08, h * 0.1, p.Near, seed, 2, 4);
            return;
        }
        Ridge(context, w, horizon + h * 0.12, h * 0.16, p.Near, seed, 2, 5);
        context.FillRectangle(new SolidColorBrush(p.Ground), new Rect(0, horizon + h * 0.24, w, h));
        if (p.Trees)
        {
            var tree = new SolidColorBrush(p.Ground);
            for (var i = 0; i < 16; i++)
            {
                var x = Noise(seed, i + 40) * w;
                var baseY = horizon + h * (0.2 + Noise(seed, i + 80) * 0.18);
                var tall = h * (0.08 + Noise(seed, i + 120) * 0.1);
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(new Point(x, baseY - tall), true);
                    g.LineTo(new Point(x + tall * 0.32, baseY));
                    g.LineTo(new Point(x - tall * 0.32, baseY));
                    g.EndFigure(true);
                }
                context.DrawGeometry(tree, null, geometry);
            }
        }
    }

    private static void Ridge(DrawingContext context, double w, double baseY, double amplitude, Color color, int seed, int layer, int peaks)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(0, baseY + amplitude), true);
            for (var i = 0; i <= peaks; i++)
            {
                var x = w * i / peaks;
                var y = baseY - amplitude * (0.25 + Noise(seed * 7 + layer, i) * 0.75) * (i % 2 == 0 ? 1 : 0.55);
                g.LineTo(new Point(x, y));
            }
            g.LineTo(new Point(w, baseY + amplitude * 3));
            g.LineTo(new Point(0, baseY + amplitude * 3));
            g.EndFigure(true);
        }
        context.DrawGeometry(new SolidColorBrush(color), null, geometry);
    }

    private static readonly (Color Light, Color Dark, string Name)[] s_glazes =
    [
        (C("#E8DCC8"), C("#B8A486"), "Oat"),
        (C("#7FA3B8"), C("#4B6E85"), "Tide"),
        (C("#C7735A"), C("#8E4634"), "Terracotta")
    ];

    /// <summary>The name of a glaze.</summary>
    public static string GlazeName(int glaze) => s_glazes[Math.Abs(glaze) % s_glazes.Length].Name;

    private static void PaintMug(DrawingContext context, Rect bounds, int view, int glazeIndex)
    {
        var (light, dark, _) = s_glazes[Math.Abs(glazeIndex) % s_glazes.Length];
        var w = bounds.Width;
        var h = bounds.Height;
        context.FillRectangle(Vertical(C("#F4F1EC"), C("#E4DED5")), bounds);
        var glaze = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = { new GradientStop(dark, 0), new GradientStop(light, 0.38), new GradientStop(light, 0.55), new GradientStop(dark, 1) }
        };
        var shadow = new SolidColorBrush(Color.FromArgb(40, 60, 40, 20));
        var rim = new Pen(new SolidColorBrush(dark), 6);
        switch (view)
        {
            case 0:
            case 1:
            {
                var body = new Rect(w * 0.3, h * 0.28, w * 0.4, h * 0.46);
                context.DrawEllipse(shadow, null, new Point(w * 0.5, body.Bottom + 6), w * 0.26, h * 0.035);
                var handleX = view == 0 ? body.Right : body.Left;
                var handle = new Pen(new SolidColorBrush(dark), w * 0.035);
                context.DrawEllipse(null, handle, new Point(handleX, body.Center.Y), w * 0.08, h * 0.11);
                context.DrawRectangle(glaze, null, body, w * 0.03, w * 0.03);
                context.DrawEllipse(new SolidColorBrush(light), rim, new Point(body.Center.X, body.Top), body.Width / 2, h * 0.035);
                context.DrawEllipse(new SolidColorBrush(C("#5A3B24")), null, new Point(body.Center.X, body.Top + 4), body.Width / 2 - 12, h * 0.022);
                if (view == 1)
                {
                    // A stamped maker's mark near the foot.
                    context.DrawEllipse(null, new Pen(new SolidColorBrush(dark), 3), new Point(body.Center.X, body.Bottom - h * 0.07), w * 0.03, w * 0.03);
                }
                break;
            }
            case 2:
            {
                var center = new Point(w * 0.5, h * 0.5);
                context.DrawEllipse(shadow, null, new Point(center.X + 10, center.Y + 14), w * 0.27, w * 0.27);
                context.DrawEllipse(null, new Pen(new SolidColorBrush(dark), w * 0.035), new Point(center.X + w * 0.28, center.Y), w * 0.07, w * 0.04);
                context.DrawEllipse(glaze, rim, center, w * 0.26, w * 0.26);
                context.DrawEllipse(new SolidColorBrush(C("#5A3B24")), null, center, w * 0.22, w * 0.22);
                context.DrawEllipse(new SolidColorBrush(C("#C89B6C"), 0.7), null, new Point(center.X - w * 0.04, center.Y - w * 0.03), w * 0.08, w * 0.05);
                break;
            }
            case 3:
            {
                // A close look at the glaze: speckles and a drip line.
                context.FillRectangle(glaze, bounds);
                var speck = new SolidColorBrush(dark, 0.7);
                for (var i = 0; i < 260; i++)
                {
                    var r = 1.5 + Noise(glazeIndex + 3, i) * 4;
                    context.DrawEllipse(speck, null, new Point(Noise(glazeIndex, i) * w, Noise(glazeIndex, i + 999) * h), r, r);
                }
                var drip = new StreamGeometry();
                using (var g = drip.Open())
                {
                    g.BeginFigure(new Point(0, h * 0.42), true);
                    for (var x = 0.0; x <= w; x += w / 18)
                    {
                        g.LineTo(new Point(x, h * 0.42 + Math.Abs(Math.Sin(x / w * 11)) * h * 0.1));
                    }
                    g.LineTo(new Point(w, 0));
                    g.LineTo(new Point(0, 0));
                    g.EndFigure(true);
                }
                context.DrawGeometry(new SolidColorBrush(light, 0.85), null, drip);
                break;
            }
            default:
            {
                // On the table, with steam.
                context.FillRectangle(Vertical(C("#C9A27C"), C("#9C7552")), new Rect(0, h * 0.62, w, h * 0.38));
                var body = new Rect(w * 0.36, h * 0.36, w * 0.3, h * 0.34);
                context.DrawEllipse(shadow, null, new Point(body.Center.X + 20, body.Bottom + 4), w * 0.22, h * 0.03);
                context.DrawEllipse(null, new Pen(new SolidColorBrush(dark), w * 0.03), new Point(body.Right, body.Center.Y), w * 0.06, h * 0.08);
                context.DrawRectangle(glaze, null, body, w * 0.025, w * 0.025);
                context.DrawEllipse(new SolidColorBrush(light), rim, new Point(body.Center.X, body.Top), body.Width / 2, h * 0.028);
                var steam = new Pen(new SolidColorBrush(Colors.White, 0.7), 8, lineCap: PenLineCap.Round);
                for (var s = 0; s < 3; s++)
                {
                    var x0 = body.Center.X + (s - 1) * w * 0.06;
                    var geometry = new StreamGeometry();
                    using (var g = geometry.Open())
                    {
                        g.BeginFigure(new Point(x0, body.Top - h * 0.03), false);
                        g.CubicBezierTo(new Point(x0 - 30, body.Top - h * 0.09), new Point(x0 + 30, body.Top - h * 0.14), new Point(x0, body.Top - h * 0.2));
                        g.EndFigure(false);
                    }
                    context.DrawGeometry(null, steam, geometry);
                }
                break;
            }
        }
    }

    private static readonly Color[][] s_coverPalettes =
    [
        [C("#1D2B53"), C("#FF77A8"), C("#FFCCAA")],
        [C("#0F4C5C"), C("#E36414"), C("#FB8B24")],
        [C("#F2E8CF"), C("#386641"), C("#BC4749")],
        [C("#22223B"), C("#C9ADA7"), C("#F2E9E4")],
        [C("#003049"), C("#FCBF49"), C("#EAE2B7")],
        [C("#2B2D42"), C("#EF233C"), C("#EDF2F4")],
        [C("#FFE8D6"), C("#6B705C"), C("#CB997E")],
        [C("#0B090A"), C("#E5383B"), C("#F5F3F4")],
        [C("#3D405B"), C("#81B29A"), C("#F4F1DE")],
        [C("#14213D"), C("#FCA311"), C("#E5E5E5")],
        [C("#5F0F40"), C("#FB8B24"), C("#E9C46A")],
        [C("#E9F5DB"), C("#718355"), C("#263C41")]
    ];

    private static void PaintCover(DrawingContext context, Rect bounds, int index, string title, bool book)
    {
        var colors = s_coverPalettes[Math.Abs(index) % s_coverPalettes.Length];
        var (ground, accent, ink) = (colors[0], colors[1], colors[2]);
        var w = bounds.Width;
        var h = bounds.Height;
        context.FillRectangle(new SolidColorBrush(ground), bounds);
        var accentBrush = new SolidColorBrush(accent);
        var inkBrush = new SolidColorBrush(ink);
        switch (index % 6)
        {
            case 0:
                for (var r = 6; r >= 1; r--)
                {
                    context.DrawEllipse(r % 2 == 0 ? accentBrush : inkBrush, null, new Point(w * 0.5, h * 0.42), w * 0.06 * r, w * 0.06 * r);
                }
                break;
            case 1:
                for (var i = 0; i < 9; i++)
                {
                    context.FillRectangle(i % 2 == 0 ? accentBrush : inkBrush, new Rect(0, h * (0.1 + i * 0.07), w, h * 0.035));
                }
                break;
            case 2:
                for (var row = 0; row < 7; row++)
                {
                    for (var column = 0; column < 7; column++)
                    {
                        var r = w * 0.012 * (1 + (row + column) % 4);
                        context.DrawEllipse((row * column) % 3 == 0 ? accentBrush : inkBrush, null,
                            new Point(w * (0.14 + column * 0.12), h * (0.1 + row * 0.085)), r, r);
                    }
                }
                break;
            case 3:
            {
                var pen = new Pen(accentBrush, w * 0.02);
                for (var i = 0; i < 6; i++)
                {
                    var geometry = new StreamGeometry();
                    using (var g = geometry.Open())
                    {
                        g.BeginFigure(new Point(0, h * (0.15 + i * 0.08)), false);
                        for (var x = 0.0; x <= w; x += w / 20)
                        {
                            g.LineTo(new Point(x, h * (0.15 + i * 0.08) + Math.Sin(x / w * Math.PI * 4 + i) * h * 0.025));
                        }
                        g.EndFigure(false);
                    }
                    context.DrawGeometry(null, pen, geometry);
                }
                break;
            }
            case 4:
            {
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(new Point(w * 0.5, h * 0.08), true);
                    g.LineTo(new Point(w * 0.88, h * 0.66));
                    g.LineTo(new Point(w * 0.12, h * 0.66));
                    g.EndFigure(true);
                }
                context.DrawGeometry(accentBrush, null, geometry);
                context.DrawEllipse(inkBrush, null, new Point(w * 0.5, h * 0.48), w * 0.12, w * 0.12);
                break;
            }
            default:
                context.DrawRectangle(accentBrush, null, new Rect(w * 0.12, h * 0.1, w * 0.5, h * 0.4));
                context.DrawRectangle(inkBrush, null, new Rect(w * 0.38, h * 0.26, w * 0.5, h * 0.4));
                break;
        }
        var text = new FormattedText(title, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default,
            book ? w * 0.1 : w * 0.075, inkBrush)
        {
            MaxTextWidth = w * 0.8,
            TextAlignment = TextAlignment.Left
        };
        context.FillRectangle(new SolidColorBrush(ground, 0.85), new Rect(0, h * 0.74, w, h * 0.26));
        context.DrawText(text, new Point(w * 0.1, h * (book ? 0.77 : 0.8)));
    }
}
