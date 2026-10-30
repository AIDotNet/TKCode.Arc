using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The scene the SketchToImage demos paint: layered alpine ridges at golden hour, a low sun, warm haze, pines on a dark
/// foreground hill, two wisps of cloud and two birds, with its prompt. Everything is drawn in code: the pencil lines trace
/// the same outlines the brush fills, and the finished picture is the same shapes with gradients, haze bands, snow
/// highlights and a sun glow. Invented demo content; no image is downloaded.
/// </summary>
internal static class SketchToImageDemoData
{
    /// <summary>The prompt shown under the stage label.</summary>
    public const string Prompt = "Layered alpine ridges at golden hour, warm haze";

    /// <summary>The prompt of the neutral-tone demo.</summary>
    public const string GraphitePrompt = "A quiet ridge line at dusk, graphite study";

    private static SketchToImageScene? s_alpine;

    /// <summary>The alpine scene (immutable, shared by every demo), built on first use.</summary>
    public static SketchToImageScene Alpine => s_alpine ??= BuildAlpine();

    private static readonly Point s_sun = new(420, 178);

    private static readonly Point[] s_far =
    [
        new(0, 232), new(40, 214), new(78, 222), new(118, 196), new(150, 206), new(190, 180), new(226, 198), new(262, 188),
        new(300, 206), new(336, 192), new(372, 214), new(410, 200), new(452, 216), new(490, 196), new(530, 210),
        new(566, 198), new(600, 212),
    ];

    private static readonly Point[] s_mid =
    [
        new(0, 268), new(52, 240), new(96, 252), new(140, 222), new(176, 238), new(214, 214), new(250, 234), new(292, 246),
        new(330, 226), new(372, 250), new(412, 236), new(456, 258), new(500, 232), new(548, 252), new(600, 238),
    ];

    private static readonly Point[] s_near =
    [
        new(0, 312), new(60, 286), new(104, 298), new(156, 270), new(196, 290), new(244, 278), new(290, 300), new(338, 280),
        new(384, 296), new(430, 274), new(478, 292), new(528, 280), new(600, 300),
    ];

    private const string ForegroundLine = "M0 352 C80 334 150 344 220 340 C300 336 360 326 440 334 C510 341 560 336 600 330";

    private static readonly (double X, double Base, double Height)[] s_pines =
    [
        (64, 350, 64), (98, 346, 44), (476, 340, 34), (520, 338, 58), (548, 336, 40),
    ];

    private static SketchToImageScene BuildAlpine()
    {
        var sun = Circle(s_sun, 30);
        var lines = new List<string>
        {
            Polyline(s_far, close: false),
            Polyline(s_mid, close: false),
            Polyline(s_near, close: false),
            ForegroundLine,
            sun,
            "M190 180 L204 206",
            "M214 214 L232 246 L226 268",
            "M330 226 L346 256",
            "M156 270 L170 300 L164 322",
            "M430 274 L446 304",
        };
        foreach (var pine in s_pines)
        {
            lines.Add(Pine(pine.X, pine.Base, pine.Height));
        }
        lines.Add("M86 96 C116 86 146 92 172 88 C196 84 220 90 238 88");
        lines.Add("M356 70 C380 62 406 66 430 62");
        lines.Add("M250 120 Q256 114 262 120 Q268 114 274 120");
        lines.Add("M284 104 Q288 100 292 104 Q296 100 300 104");

        var regions = new List<SketchToImageRegion>
        {
            new()
            {
                Brush = new Rect(0, 0, 600, 250),
                Shapes =
                [
                    new("M0 0H600V400H0Z", Rgb(0xEDCBAE), Vertical((0, 0xFFA7B6D3), (0.5, 0xFFE9C3AA), (0.8, 0xFFF7D9B2), (1, 0xFFFBE3BC))),
                    new(Circle(s_sun, 120), null, Radial(0xC8FFEBC0, 0x00FFE6B0)),
                ],
            },
            new()
            {
                Brush = new Rect(384, 142, 72, 72),
                Shapes = [new(sun, Rgb(0xFBE3B0), Radial(0xFFFFF8E4, 0xFFFCD596))],
            },
            new()
            {
                Brush = new Rect(0, 172, 600, 100),
                Shapes = [new(Polyline(s_far, close: true), Rgb(0xD3B1B6), Vertical((0, 0xFFE2BFBF), (1, 0xFFC7A3B1)))],
            },
            new()
            {
                Brush = new Rect(0, 208, 600, 108),
                Shapes =
                [
                    new("M0 200H600V300H0Z", null, Vertical((0, 0x00FFF1E0), (0.6, 0x8CFFF1E0), (1, 0x00FFF1E0))),
                    new(Polyline(s_mid, close: true), Rgb(0xA08AA6), Vertical((0, 0xFFB69AB2), (1, 0xFF85718F))),
                    new(Snow(140, 222, 30.0 / 44, 16.0 / 36), null, Solid(0xD9E6D6E0)),
                    new(Snow(214, 214, 24.0 / 38, 20.0 / 36), null, Solid(0xD9E6D6E0)),
                    new(Snow(330, 226, 20.0 / 38, 24.0 / 42), null, Solid(0xD9E6D6E0)),
                    new(Snow(500, 232, 26.0 / 44, 20.0 / 48), null, Solid(0xD9E6D6E0)),
                ],
            },
            new()
            {
                Brush = new Rect(0, 264, 600, 96),
                Shapes =
                [
                    new("M0 250H600V330H0Z", null, Vertical((0, 0x00FFF1E0), (0.55, 0x60FFF1E0), (1, 0x00FFF1E0))),
                    new(Polyline(s_near, close: true), Rgb(0x675C7A), Vertical((0, 0xFF776A8C), (1, 0xFF4F4562))),
                ],
            },
            new()
            {
                Brush = new Rect(0, 322, 600, 78),
                Shapes = [new(ForegroundLine + " V400 H0 Z", Rgb(0x33464A), Vertical((0, 0xFF3E5557), (1, 0xFF22302F)))],
            },
            new()
            {
                Brush = new Rect(36, 276, 532, 76),
                Shapes = s_pines
                    .Select(p => new SketchToImageShape(Pine(p.X, p.Base, p.Height), Rgb(0x1D2B2A), Vertical((0, 0xFF263836), (1, 0xFF172221))))
                    .ToArray(),
            },
        };

        return new SketchToImageScene
        {
            Alt = "Layered mountain ridges under a warm evening sky",
            Lines = lines,
            Regions = regions,
            Light = s_sun,
        };
    }

    private static Color Rgb(uint rgb) => Color.FromUInt32(0xFF000000 | rgb);

    private static IBrush Solid(uint argb) => new ImmutableSolidColorBrush(Color.FromUInt32(argb));

    private static IBrush Vertical(params (double Offset, uint Argb)[] stops) =>
        new ImmutableLinearGradientBrush(
            stops.Select(s => new ImmutableGradientStop(s.Offset, Color.FromUInt32(s.Argb))).ToArray(),
            startPoint: new RelativePoint(0.5, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(0.5, 1, RelativeUnit.Relative));

    private static IBrush Radial(uint inner, uint outer) =>
        new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromUInt32(inner)), new ImmutableGradientStop(1, Color.FromUInt32(outer))],
            radius: 0.5);

    private static string Polyline(Point[] points, bool close)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < points.Length; i++)
        {
            builder.Append(i == 0 ? "M" : " L").Append(Num(points[i].X)).Append(' ').Append(Num(points[i].Y));
        }
        if (close)
        {
            builder.Append(" V400 H0 Z");
        }
        return builder.ToString();
    }

    private static string Circle(Point center, double radius) =>
        $"M{Num(center.X - radius)} {Num(center.Y)} A{Num(radius)} {Num(radius)} 0 1 0 {Num(center.X + radius)} {Num(center.Y)} " +
        $"A{Num(radius)} {Num(radius)} 0 1 0 {Num(center.X - radius)} {Num(center.Y)} Z";

    /// <summary>A three-tier pine on a short trunk, symmetric about <paramref name="x"/>.</summary>
    private static string Pine(double x, double baseY, double height)
    {
        var top = baseY - height;
        var w = height * 0.38;
        (double Dx, double Y)[] right =
        [
            (w * 0.45, top + height * 0.33), (w * 0.22, top + height * 0.33), (w * 0.72, top + height * 0.64),
            (w * 0.45, top + height * 0.64), (w, baseY - height * 0.1), (w * 0.12, baseY - height * 0.1), (w * 0.12, baseY),
        ];
        var builder = new StringBuilder();
        builder.Append('M').Append(Num(x)).Append(' ').Append(Num(top));
        foreach (var (dx, y) in right)
        {
            builder.Append(" L").Append(Num(x + dx)).Append(' ').Append(Num(y));
        }
        for (var i = right.Length - 1; i >= 0; i--)
        {
            builder.Append(" L").Append(Num(x - right[i].Dx)).Append(' ').Append(Num(right[i].Y));
        }
        return builder.Append(" Z").ToString();
    }

    /// <summary>A snow cap hugging a peak whose slopes fall by <paramref name="left"/> and <paramref name="right"/> per unit.</summary>
    private static string Snow(double x, double y, double left, double right) =>
        $"M{Num(x)} {Num(y)} L{Num(x + 13)} {Num(y + 13 * right)} L{Num(x + 7)} {Num(y + 13 * right + 4)} " +
        $"L{Num(x + 1)} {Num(y + 6)} L{Num(x - 6)} {Num(y + 12 * left + 3)} L{Num(x - 12)} {Num(y + 12 * left)} Z";

    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
