using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The pictures the ImageReveal demos' simulations reveal: three invented painted scenes (an alpine lake at golden hour,
/// desert dunes at dusk, a lighthouse on sea cliffs at dawn) made of gradients and simple shapes, rendered once into a
/// 1024 × 1024 bitmap each the first time a demo asks for them. Nothing is downloaded. A real app reveals the images its
/// model returns.
/// </summary>
internal static class ImageRevealDemoData
{
    /// <summary>The bitmaps' side in pixels.</summary>
    private const int Side = 1024;

    private static IReadOnlyList<ImageRevealSimulationPicture>? s_pictures;

    /// <summary>The three scenes with their alt text, painted on first use (a scene stays null where the platform cannot render offscreen).</summary>
    public static IReadOnlyList<ImageRevealSimulationPicture> Pictures => s_pictures ??=
    [
        new ImageRevealSimulationPicture { Image = Render(0), AltText = "A calm alpine lake at golden hour" },
        new ImageRevealSimulationPicture { Image = Render(1), AltText = "Desert dunes under a rising moon at dusk" },
        new ImageRevealSimulationPicture { Image = Render(2), AltText = "A lighthouse on sea cliffs at dawn" },
    ];

    private static IImage? Render(int index)
    {
        try
        {
            var target = new RenderTargetBitmap(new PixelSize(Side, Side), new Vector(96, 96));
            using (var context = target.CreateDrawingContext())
            {
                Draw(context, index, new Rect(0, 0, Side, Side));
            }
            return target;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Paints scene <paramref name="index"/> into <paramref name="bounds"/> (shapes are authored on a 100 × 100 grid).</summary>
    private static void Draw(DrawingContext context, int index, Rect bounds)
    {
        using (context.PushClip(bounds))
        using (context.PushTransform(Matrix.CreateScale(bounds.Width / 100, bounds.Height / 100) * Matrix.CreateTranslation(bounds.X, bounds.Y)))
        {
            switch (index)
            {
                case 0:
                    DrawLake(context);
                    break;
                case 1:
                    DrawDunes(context);
                    break;
                default:
                    DrawLighthouse(context);
                    break;
            }
        }
    }

    private static readonly Rect s_full = new(0, 0, 100, 100);

    private static IBrush Vertical(params (double Offset, string Color)[] stops) => Linear(0.5, 0, 0.5, 1, stops);

    private static IBrush Linear(double x1, double y1, double x2, double y2, params (double Offset, string Color)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(x1, y1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(x2, y2, RelativeUnit.Relative),
        };
        foreach (var (offset, color) in stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.Parse(color), offset));
        }
        return brush;
    }

    private static IBrush Glow(string color, double alpha)
    {
        var c = Color.Parse(color);
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alpha * 255), c.R, c.G, c.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
        return brush;
    }

    private static void Fill(DrawingContext context, IBrush brush, string path) =>
        context.DrawGeometry(brush, null, Geometry.Parse(path));

    private static void Disc(DrawingContext context, IBrush brush, double x, double y, double rx, double ry) =>
        context.DrawEllipse(brush, null, new Point(x, y), rx, ry);

    /// <summary>An alpine lake at golden hour: a warm sky, a low sun, two ranges of peaks with snow, pines and the reflection.</summary>
    private static void DrawLake(DrawingContext c)
    {
        c.FillRectangle(Vertical((0, "#27345F"), (0.38, "#B86A73"), (0.56, "#F3A35C"), (0.64, "#F8D48E")), s_full);
        Disc(c, Glow("#FFE2A6", 0.85), 63, 52, 34, 26);
        Disc(c, new SolidColorBrush(Color.Parse("#FFF1CF")), 63, 54, 4.2, 4.2);
        Disc(c, Glow("#FFFFFF", 0.25), 22, 20, 26, 6);
        Disc(c, Glow("#FFE7C4", 0.22), 80, 28, 22, 4);
        Fill(c, Vertical((0, "#6B5E8E"), (1, "#8A6F92")), "M0,52 L9,41 L16,46 L27,31 L36,42 L44,36 L55,48 L66,38 L75,45 L86,33 L100,45 L100,64 L0,64 Z");
        Fill(c, new SolidColorBrush(Color.Parse("#E8DDE6"), 0.75), "M27,31 L31,35.5 L29,35 L27.5,37 L25,34.5 L23.5,35 Z");
        Fill(c, new SolidColorBrush(Color.Parse("#EBDCE2"), 0.7), "M86,33 L90,37.8 L88,37.2 L86.3,39 L84,36.6 L82.6,37.4 Z");
        Fill(c, Vertical((0, "#3B3A62"), (1, "#2A2C4C")), "M0,58 L12,47 L21,53 L33,43 L45,55 L58,46 L70,56 L83,49 L100,57 L100,65 L0,65 Z");
        Fill(c, Vertical((0, "#F2B06E"), (0.25, "#B87079"), (0.7, "#3E4675"), (1, "#1E2645")), "M0,64 L100,64 L100,100 L0,100 Z");
        Disc(c, Glow("#FFE0A0", 0.8), 63, 72, 3.2, 16);
        Disc(c, Glow("#FFD18A", 0.35), 63, 70, 12, 3);
        var ripple = new SolidColorBrush(Color.Parse("#FFE6BD"), 0.35);
        for (var i = 0; i < 9; i++)
        {
            var y = 67 + i * 3.4;
            var w = 6 + i * 2.2;
            c.FillRectangle(ripple, new Rect(63 - w / 2 + (i % 3 - 1) * 2, y, w, 0.35));
        }
        var pines = new SolidColorBrush(Color.Parse("#141A30"));
        for (var i = 0; i < 7; i++)
        {
            var x = i * 4.2 - 1;
            var h = 10 + (i * 37 % 7);
            Fill(c, pines, FormattableString.Invariant($"M{x},{66} L{x + 2.4},{66 - h} L{x + 4.8},{66} Z"));
        }
        for (var i = 0; i < 5; i++)
        {
            var x = 82 + i * 4;
            var h = 8 + (i * 53 % 6);
            Fill(c, pines, FormattableString.Invariant($"M{x},{66} L{x + 2.2},{66 - h} L{x + 4.4},{66} Z"));
        }
        c.FillRectangle(new SolidColorBrush(Color.Parse("#141A30")), new Rect(0, 65.5, 100, 1.2));
        Fill(c, Vertical((0, "#1A2038"), (1, "#0E1222")), "M0,86 C18,82 34,92 52,90 C70,88 84,84 100,88 L100,100 L0,100 Z");
    }

    /// <summary>Desert dunes at dusk: a dusky sky, a pale moon, layered dunes with lit ridges.</summary>
    private static void DrawDunes(DrawingContext c)
    {
        c.FillRectangle(Vertical((0, "#1C2448"), (0.35, "#5B3F6E"), (0.55, "#C46A6A"), (0.7, "#F4B07C")), s_full);
        Disc(c, Glow("#FFE9D2", 0.55), 30, 30, 16, 16);
        Disc(c, new SolidColorBrush(Color.Parse("#FFF4E6")), 30, 30, 5.5, 5.5);
        var star = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.75);
        for (var i = 0; i < 18; i++)
        {
            var x = (i * 61 % 100) + 0.5;
            var y = (i * 37 % 34) + 2;
            Disc(c, star, x, y, 0.22, 0.22);
        }
        Fill(c, Vertical((0, "#E08E62"), (1, "#B9604C")), "M0,62 C16,54 30,56 46,60 C60,63 74,53 100,57 L100,100 L0,100 Z");
        Fill(c, Linear(0, 0.2, 1, 0.8, (0, "#F2A774"), (0.55, "#C2674C"), (1, "#8C4440")), "M0,72 C14,64 28,63 40,68 C54,74 70,62 100,66 L100,100 L0,100 Z");
        Fill(c, new SolidColorBrush(Color.Parse("#FFD0A0"), 0.55), "M0,72 C14,64 28,63 40,68 C30,65.8 16,66.6 0,73.4 Z");
        Fill(c, Linear(0, 0.1, 1, 0.9, (0, "#A4523F"), (0.6, "#6E3236"), (1, "#3E1F2C")), "M0,84 C20,74 38,76 54,82 C70,88 84,78 100,79 L100,100 L0,100 Z");
        Fill(c, new SolidColorBrush(Color.Parse("#E88A60"), 0.6), "M54,82 C70,88 84,78 100,79 L100,80.2 C86,79.6 72,89.6 54,82 Z");
        Fill(c, Vertical((0, "#3A1C2A"), (1, "#24121D")), "M0,94 C24,88 52,96 76,92 C88,90 96,91 100,92 L100,100 L0,100 Z");
    }

    /// <summary>A lighthouse on sea cliffs at dawn: a pale sky with soft clouds, the sea, green cliffs and a striped tower.</summary>
    private static void DrawLighthouse(DrawingContext c)
    {
        c.FillRectangle(Vertical((0, "#7FA8D4"), (0.42, "#C7D6EA"), (0.6, "#F6D8C6")), s_full);
        Disc(c, Glow("#FFF3E2", 0.8), 78, 58, 30, 14);
        var cloud = Glow("#FFFFFF", 0.7);
        Disc(c, cloud, 22, 22, 18, 4);
        Disc(c, cloud, 34, 19, 12, 3.4);
        Disc(c, cloud, 70, 30, 20, 3.6);
        Disc(c, Glow("#FFE4D2", 0.6), 58, 40, 16, 2.6);
        Fill(c, Vertical((0, "#5C8FB2"), (0.4, "#2F6687"), (1, "#173F5A")), "M0,60 L100,60 L100,100 L0,100 Z");
        Disc(c, Glow("#FFEBD6", 0.7), 78, 62, 20, 2.2);
        var glint = new SolidColorBrush(Color.Parse("#E9F2FA"), 0.4);
        for (var i = 0; i < 12; i++)
        {
            var y = 63 + i * 3;
            var x = (i * 29 % 70) + 30;
            c.FillRectangle(glint, new Rect(x, y, 4 + i % 4, 0.3));
        }
        Fill(c, Linear(0, 0, 1, 1, (0, "#5E7B54"), (0.5, "#3E5A3E"), (1, "#24382A")), "M0,46 L14,44 L30,47 L40,52 L44,62 L40,74 L46,100 L0,100 Z");
        Fill(c, Linear(0, 0, 1, 1, (0, "#8A7A66"), (1, "#4A4038")), "M40,52 L44,62 L40,74 L46,100 L34,100 L31,80 L35,62 Z");
        Fill(c, new SolidColorBrush(Color.Parse("#A7C08A"), 0.6), "M0,46 L14,44 L30,47 L40,52 L28,49.5 L14,46.6 L0,48.4 Z");
        Fill(c, new SolidColorBrush(Color.Parse("#FFF6D8"), 0.28), "M21.5,25.5 L58,18 L58,30 Z");
        c.FillRectangle(new SolidColorBrush(Color.Parse("#F4F1EA")), new Rect(18.6, 27, 5.8, 19));
        var band = new SolidColorBrush(Color.Parse("#C2453D"));
        c.FillRectangle(band, new Rect(18.6, 31, 5.8, 3));
        c.FillRectangle(band, new Rect(18.6, 38, 5.8, 3));
        c.FillRectangle(new SolidColorBrush(Color.Parse("#2B2F36")), new Rect(18, 24.6, 7, 2.6));
        Disc(c, new SolidColorBrush(Color.Parse("#FFE9A8")), 21.5, 25.8, 1.6, 1.1);
        Fill(c, new SolidColorBrush(Color.Parse("#2B2F36")), "M18,24.6 L21.5,21.6 L25,24.6 Z");
        Fill(c, new SolidColorBrush(Color.Parse("#E8EEF2"), 0.55), "M40,74 C44,75 47,73 52,74.5 C48,76 44,76.5 40.6,76 Z");
    }
}
