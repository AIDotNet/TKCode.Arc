using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Invented sample queue for the gallery and tests: five fictional tracks of three to four minutes (the first and last by
/// the same artist) with code-drawn abstract covers — gradients and simple shapes, no photos — and a description of each.
/// </summary>
public static class NowPlayingSamples
{
    private const double Side = 900;

    /// <summary>The five sample tracks (new objects each call; the covers are shared).</summary>
    public static IList<NowPlayingTrack> CreateTracks() =>
    [
        new NowPlayingTrack("low-orbit", "Low Orbit", "Mara Quell", TimeSpan.FromSeconds(206))
        {
            Artwork = Covers.LowOrbit, ArtworkAlt = "Amber light curving over a dark horizon"
        },
        new NowPlayingTrack("saltwind-road", "Saltwind Road", "The Copper Fields", TimeSpan.FromSeconds(242))
        {
            Artwork = Covers.Saltwind, ArtworkAlt = "Layered dunes under a pale sky"
        },
        new NowPlayingTrack("paper-lanterns", "Paper, lanterns", "Iris Navarro", TimeSpan.FromSeconds(192))
        {
            Artwork = Covers.Lanterns, ArtworkAlt = "A ring of warm light in the dark"
        },
        new NowPlayingTrack("steel-and-fern", "Steel and fern", "Okafor & Lund", TimeSpan.FromSeconds(227))
        {
            Artwork = Covers.Facets, ArtworkAlt = "Sharp grey facets in hard light"
        },
        new NowPlayingTrack("riverglass", "Riverglass", "Mara Quell", TimeSpan.FromSeconds(184))
        {
            Artwork = Covers.Riverglass, ArtworkAlt = "Teal currents swirling in deep water"
        }
    ];

    /// <summary>Fills <paramref name="player"/>'s queue with <see cref="CreateTracks"/>.</summary>
    public static void Apply(NowPlaying player)
    {
        player.Tracks = new Avalonia.Collections.AvaloniaList<NowPlayingTrack>(CreateTracks());
    }

    /// <summary>The cover of sample track <paramref name="index"/> (0 to 4), for demos that build their own tracks.</summary>
    public static IImage Cover(int index) => index switch
    {
        0 => Covers.LowOrbit,
        1 => Covers.Saltwind,
        2 => Covers.Lanterns,
        3 => Covers.Facets,
        _ => Covers.Riverglass
    };

    private static class Covers
    {
        public static readonly IImage LowOrbit = Build(LowOrbitScene);
        public static readonly IImage Saltwind = Build(SaltwindScene);
        public static readonly IImage Lanterns = Build(LanternsScene);
        public static readonly IImage Facets = Build(FacetsScene);
        public static readonly IImage Riverglass = Build(RiverglassScene);
    }

    private static IImage Build(Action<DrawingGroup> scene)
    {
        var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(new Rect(0, 0, Side, Side)) };
        scene(group);
        return new CoverImage(group);
    }

    /// <summary>
    /// A drawn cover with a fixed 900 × 900 frame. (A <c>DrawingImage</c> sizes itself to its drawing's bounds, which strokes
    /// running off the edge would enlarge.)
    /// </summary>
    private sealed class CoverImage(Drawing drawing) : IImage
    {
        public Size Size => new(Side, Side);

        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
        {
            if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            {
                return;
            }
            var transform = Matrix.CreateTranslation(-sourceRect.X, -sourceRect.Y)
                            * Matrix.CreateScale(destRect.Width / sourceRect.Width, destRect.Height / sourceRect.Height)
                            * Matrix.CreateTranslation(destRect.X, destRect.Y);
            using (context.PushClip(destRect))
            using (context.PushTransform(transform))
            {
                drawing.Draw(context);
            }
        }
    }

    private static readonly Geometry Square = new RectangleGeometry(new Rect(0, 0, Side, Side));

    private static LinearGradientBrush Gradient(Point start, Point end, params (double Offset, uint Color)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(start, RelativeUnit.Relative),
            EndPoint = new RelativePoint(end, RelativeUnit.Relative)
        };
        foreach (var (offset, color) in stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.FromUInt32(color), offset));
        }
        return brush;
    }

    private static void Fill(DrawingGroup group, IBrush brush, Geometry geometry) =>
        group.Children.Add(new GeometryDrawing { Brush = brush, Geometry = geometry });

    private static void Stroke(DrawingGroup group, uint color, double thickness, Geometry geometry) =>
        group.Children.Add(new GeometryDrawing
        {
            Pen = new Pen(new SolidColorBrush(Color.FromUInt32(color)), thickness, lineCap: PenLineCap.Round),
            Geometry = geometry
        });

    // Deep indigo into amber on the diagonal, three thin rings sweeping in from the lower right.
    private static void LowOrbitScene(DrawingGroup g)
    {
        Fill(g, Gradient(new Point(0, 0), new Point(1, 1), (0, 0xFF1B1C4Bu), (0.55, 0xFF6A3A5Cu), (1, 0xFFF0A24Au)), Square);
        Fill(g, Gradient(new Point(0.5, 0), new Point(0.5, 1), (0, 0x00000000u), (0.7, 0x00000000u), (1, 0x55120C2Au)), Square);
        for (var i = 0; i < 3; i++)
        {
            var r = 380 + i * 110;
            Stroke(g, 0x2EFFFFFFu, 4, new EllipseGeometry(new Rect(Side - 120 - r, Side - 80 - r, 2 * r, 2 * r)));
        }
    }

    // Sand and gold falling to a pale grey-blue sky, three rolling bands of dune.
    private static void SaltwindScene(DrawingGroup g)
    {
        Fill(g, Gradient(new Point(0.5, 0), new Point(0.5, 1), (0, 0xFFB9C7D6u), (0.42, 0xFFE4DCCDu), (1, 0xFFD9A867u)), Square);
        uint[] bands = [0xFFE8C48Bu, 0xFFD39A5Bu, 0xFFB57841u];
        for (var b = 0; b < 3; b++)
        {
            var baseline = 430 + b * 150;
            var amplitude = 46 - b * 8;
            var phase = b * 1.7;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(0, Side), true);
                for (var x = 0; x <= Side; x += 30)
                {
                    var y = baseline + amplitude * Math.Sin(x / Side * Math.PI * 2.2 + phase);
                    ctx.LineTo(new Point(x, y));
                }
                ctx.LineTo(new Point(Side, Side));
                ctx.EndFigure(true);
            }
            Fill(g, new SolidColorBrush(Color.FromUInt32(bands[b])), geometry);
        }
    }

    // A warm ring of light on near black, softened by wider faint strokes.
    private static void LanternsScene(DrawingGroup g)
    {
        Fill(g, new SolidColorBrush(Color.FromUInt32(0xFF0E0D0Cu)), Square);
        var ring = new EllipseGeometry(new Rect(210, 210, 480, 480));
        Stroke(g, 0x14FFD9A0u, 150, ring);
        Stroke(g, 0x26FFDDA8u, 96, ring);
        Stroke(g, 0x55FFE3B6u, 60, ring);
        Stroke(g, 0xE6FFF1D8u, 26, ring);
    }

    // Grey facets in hard light: triangles alternating light and shadow.
    private static void FacetsScene(DrawingGroup g)
    {
        Fill(g, new SolidColorBrush(Color.FromUInt32(0xFF2A2A2Bu)), Square);
        (Point A, Point B, Point C, uint Color)[] facets =
        [
            (new Point(0, 0), new Point(520, 0), new Point(0, 610), 0xFF8D8D8Fu),
            (new Point(520, 0), new Point(900, 0), new Point(260, 900), 0xFFD7D7D5u),
            (new Point(0, 610), new Point(520, 0), new Point(260, 900), 0xFF5A5A5Cu),
            (new Point(900, 0), new Point(900, 520), new Point(260, 900), 0xFF3B3B3Du),
            (new Point(900, 520), new Point(900, 900), new Point(260, 900), 0xFFB4B4B2u),
            (new Point(0, 610), new Point(260, 900), new Point(0, 900), 0xFF1D1D1Eu)
        ];
        foreach (var (a, b, c, color) in facets)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(a, true);
                ctx.LineTo(b);
                ctx.LineTo(c);
                ctx.EndFigure(true);
            }
            Fill(g, new SolidColorBrush(Color.FromUInt32(color)), geometry);
        }
    }

    // Teal sinking into deep sea blue, two translucent currents curling through it.
    private static void RiverglassScene(DrawingGroup g)
    {
        Fill(g, Gradient(new Point(0, 0), new Point(0.8, 1), (0, 0xFF2BB3A6u), (0.5, 0xFF137582u), (1, 0xFF0B2A4Au)), Square);
        Stroke(g, 0x40C9FFF4u, 70, Curve(new Point(-60, 640), new Point(250, 120), new Point(700, 980), new Point(980, 300)));
        Stroke(g, 0x2EE6FFFBu, 38, Curve(new Point(80, -40), new Point(-20, 520), new Point(820, 420), new Point(560, 960)));
    }

    private static Geometry Curve(Point start, Point c1, Point c2, Point end)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false);
            ctx.CubicBezierTo(c1, c2, end);
            ctx.EndFigure(false);
        }
        return geometry;
    }
}
