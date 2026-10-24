using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The picture a <see cref="LoginImmersive"/> page shows when it has no photo: a dusk over layered ridges, drawn from
/// plain shapes (a sky gradient with a low warm glow and a few thin clouds, five ridge silhouettes from smooth noise
/// that cool and pale with distance, haze between them, a sunlit rim on the near hills and stands of conifers). It is
/// built once in a 1600 × 1000 design space and drawn like a photo with <c>object-fit: cover</c>: scaled to fill the
/// block and placed by an alignment point, so it crops instead of stretching.
/// </summary>
internal static class LoginImmersiveScene
{
    /// <summary>The design space the scene is built in.</summary>
    public static readonly Size DesignSize = new(1600, 1000);

    private static IReadOnlyList<Op>? s_ops;

    /// <summary>One shape of the scene: its geometry, fill, outline and an optional transform (design units).</summary>
    internal readonly record struct Op(Geometry Geometry, IBrush? Fill, IPen? Pen, Matrix? Transform = null);

    /// <summary>The scene's shapes in paint order (tests read their count and bounds).</summary>
    internal static IReadOnlyList<Op> Ops => s_ops ??= Build();

    /// <summary>
    /// The transform that places a <paramref name="content"/>-sized picture over <paramref name="bounds"/> like CSS
    /// <c>object-fit: cover</c> with <c>object-position</c> <paramref name="alignment"/> (fractions, 0.5 = centred).
    /// </summary>
    public static Matrix CoverTransform(Size content, Rect bounds, Point alignment)
    {
        if (content.Width <= 0 || content.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return Matrix.Identity;
        }
        var scale = Math.Max(bounds.Width / content.Width, bounds.Height / content.Height);
        var x = bounds.X + (bounds.Width - content.Width * scale) * alignment.X;
        var y = bounds.Y + (bounds.Height - content.Height * scale) * alignment.Y;
        return Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y);
    }

    /// <summary>Draws the scene over <paramref name="bounds"/> (cover, placed by <paramref name="alignment"/>).</summary>
    public static void Draw(DrawingContext context, Rect bounds, Point alignment)
    {
        using (context.PushClip(bounds))
        using (context.PushTransform(CoverTransform(DesignSize, bounds, alignment)))
        {
            foreach (var op in Ops)
            {
                if (op.Transform is { } transform)
                {
                    using (context.PushTransform(transform))
                    {
                        context.DrawGeometry(op.Fill, op.Pen, op.Geometry);
                    }
                }
                else
                {
                    context.DrawGeometry(op.Fill, op.Pen, op.Geometry);
                }
            }
        }
    }

    private static IBrush Vertical(params (double Offset, Color Color)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
        };
        foreach (var (offset, color) in stops)
        {
            brush.GradientStops.Add(new GradientStop(color, offset));
        }
        return brush.ToImmutable();
    }

    private static IBrush Radial(Color inner, Color outer)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(inner, 0));
        brush.GradientStops.Add(new GradientStop(outer, 1));
        return brush.ToImmutable();
    }

    private static Color C(double l, double c, double h, double a = 1) => Oklch.ToColor(l, c, h, a);

    /// <summary>One ridge: where its line sits, how it rises, its colours from crest to foot, and its seed.</summary>
    private readonly record struct Ridge(double Base, double Amplitude, double Wavelength, double Sharpness, int Seed,
        Color Crest, Color Foot, int Trees);

    private static IReadOnlyList<Op> Build()
    {
        var w = DesignSize.Width;
        var h = DesignSize.Height;
        var ops = new List<Op>();
        var all = new RectangleGeometry(new Rect(DesignSize));

        // Sky: cool grey-blue overhead, warm peach towards the horizon.
        ops.Add(new Op(all, Vertical(
            (0.00, C(0.60, 0.030, 240)),
            (0.22, C(0.66, 0.030, 230)),
            (0.42, C(0.76, 0.050, 65)),
            (0.56, C(0.82, 0.075, 52)),
            (0.70, C(0.80, 0.070, 50))), null));
        // A low sun just off the left edge warms that side of the sky.
        ops.Add(new Op(new EllipseGeometry(new Rect(-520, 160, 1100, 620)), Radial(C(0.88, 0.090, 55, 0.70), C(0.88, 0.090, 55, 0)), null));
        // Wisps of cloud catch the last light, mostly high on the right: each is a few soft, overlapping ellipses.
        var cloud = Radial(C(0.93, 0.015, 70, 0.30), C(0.93, 0.015, 70, 0));
        foreach (var (x, y, cw, ch, angle) in new[]
                 {
                     (1060.0, 70.0, 760.0, 70.0, -9.0), (1250.0, 150.0, 640.0, 54.0, -11.0), (960.0, 215.0, 520.0, 40.0, -8.0),
                     (1420.0, 250.0, 380.0, 34.0, -13.0), (760.0, 120.0, 420.0, 30.0, -7.0), (260.0, 40.0, 420.0, 46.0, -4.0),
                     (520.0, 300.0, 300.0, 22.0, -15.0)
                 })
        {
            for (var k = 0; k < 3; k++)
            {
                // Rotated about its centre by the drawing transform, so the soft fill follows the wisp.
                var cx = x + (k - 1) * cw * 0.22;
                var cy = y + (k - 1) * ch * 0.18 - Math.Abs(k - 1) * ch * 0.2;
                var size = 1 - Math.Abs(k - 1) * 0.35;
                var ellipse = new EllipseGeometry(new Rect(cx - cw * size / 2, cy - ch * size / 2, cw * size, ch * size));
                var turn = Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateRotation(angle * Math.PI / 180) * Matrix.CreateTranslation(cx, cy);
                ops.Add(new Op(ellipse, cloud, null, turn));
            }
        }

        var ridges = new[]
        {
            new Ridge(0.545, 0.115, 190, 0.90, 11, C(0.73, 0.022, 258), C(0.69, 0.024, 252), 0),
            new Ridge(0.595, 0.095, 240, 0.70, 23, C(0.63, 0.025, 250), C(0.56, 0.026, 245), 0),
            new Ridge(0.660, 0.090, 300, 0.45, 37, C(0.52, 0.030, 70), C(0.42, 0.030, 62), 0),
            new Ridge(0.735, 0.095, 380, 0.25, 41, C(0.55, 0.075, 72), C(0.34, 0.050, 62), 9),
            new Ridge(0.845, 0.085, 470, 0.15, 59, C(0.60, 0.100, 75), C(0.27, 0.045, 58), 12)
        };
        for (var i = 0; i < ridges.Length; i++)
        {
            var ridge = ridges[i];
            var line = RidgeLine(ridge, w, h);
            ops.Add(new Op(Silhouette(line, w, h),
                Vertical((0, ridge.Crest), (0.18, Mix(ridge.Crest, ridge.Foot, 0.45)), (0.55, Mix(ridge.Crest, ridge.Foot, 0.85)), (1, ridge.Foot)), null));
            if (ridge.Trees > 0)
            {
                // Nearer stands are darker; the middle ones keep a little of the haze.
                var tree = new ImmutableSolidColorBrush(i == ridges.Length - 1 ? C(0.20, 0.025, 60) : C(0.27, 0.025, 62));
                ops.Add(new Op(Trees(line, ridge), tree, null));
            }
            if (i == ridges.Length - 1)
            {
                // The low sun still reaches the near hills on the left: a warm wash over their slopes.
                ops.Add(new Op(new EllipseGeometry(new Rect(-500, 620, 1500, 520)), Radial(C(0.80, 0.110, 70, 0.30), C(0.80, 0.110, 70, 0)), null));
            }
            if (i < 2)
            {
                // Haze settles in the valley in front of the far ranges.
                var top = h * (ridge.Base + 0.01);
                ops.Add(new Op(new RectangleGeometry(new Rect(0, top, w, h * 0.12)),
                    Vertical((0, C(0.86, 0.040, 60, 0)), (0.55, C(0.86, 0.040, 60, 0.30)), (1, C(0.86, 0.040, 60, 0))), null));
            }
        }
        return ops;
    }

    private static Color Mix(Color a, Color b, double t) => Oklch.MixOklab(a, b, t);

    /// <summary>The ridge's crest as points every 8 design units, left to right.</summary>
    private static Point[] RidgeLine(in Ridge ridge, double w, double h)
    {
        const double Step = 8;
        var count = (int)Math.Ceiling(w / Step) + 1;
        var points = new Point[count];
        for (var i = 0; i < count; i++)
        {
            var x = Math.Min(w, i * Step);
            var u = x / ridge.Wavelength;
            // Rolling hills blend into sharp peaks as the sharpness rises (1 − |2n − 1| makes crests).
            var smooth = Fbm(u, ridge.Seed, 4);
            var peaks = 1 - Math.Abs(2 * Fbm(u * 1.3, ridge.Seed + 101, 4) - 1);
            var height = smooth * (1 - ridge.Sharpness) + peaks * ridge.Sharpness;
            points[i] = new Point(x, h * (ridge.Base - ridge.Amplitude * (height - 0.35)));
        }
        return points;
    }

    private static Geometry Silhouette(Point[] line, double w, double h)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(0, h + 2), true);
            foreach (var point in line)
            {
                g.LineTo(point);
            }
            g.LineTo(new Point(w, h + 2));
            g.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>
    /// Stands of conifers along the crest: each tree a narrow spire with three tiers of boughs, rooted a third of its
    /// height into the slope so the stands read as forest on the hill, not shapes on its edge.
    /// </summary>
    private static Geometry Trees(Point[] line, Ridge ridge)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.SetFillRule(FillRule.NonZero);
            var scale = ridge.Base > 0.8 ? 1.25 : 0.7;
            for (var stand = 0; stand < ridge.Trees; stand++)
            {
                var center = (0.03 + 0.94 * Hash(stand * 7 + 3, ridge.Seed)) * DesignSize.Width;
                var spread = (60 + Hash(stand * 5 + 1, ridge.Seed) * 160) * scale;
                var count = 5 + (int)(Hash(stand * 13 + 5, ridge.Seed) * 14);
                for (var t = 0; t < count; t++)
                {
                    var x = center + (Hash(stand * 31 + t * 7, ridge.Seed) - 0.5) * spread;
                    var height = (30 + Hash(stand * 17 + t * 3, ridge.Seed + 5) * 50) * scale;
                    var foot = CrestAt(line, x) + height * (0.25 + Hash(t + stand * 5, ridge.Seed + 2) * 0.2);
                    Spire(g, x, foot, height, height * (0.15 + Hash(t * 11 + stand, ridge.Seed + 9) * 0.05));
                }
            }
        }
        return geometry;
    }

    private static void Spire(StreamGeometryContext g, double x, double foot, double height, double half)
    {
        g.BeginFigure(new Point(x - half, foot), true);
        // Up the left side: three tiers, each stepping in.
        for (var tier = 0; tier < 3; tier++)
        {
            var top = foot - height * (tier + 1) / 3.6;
            var width = half * (1 - tier * 0.28);
            g.LineTo(new Point(x - width * 0.45, top));
            g.LineTo(new Point(x - width * 0.78, top + height * 0.02));
        }
        g.LineTo(new Point(x, foot - height));
        for (var tier = 2; tier >= 0; tier--)
        {
            var top = foot - height * (tier + 1) / 3.6;
            var width = half * (1 - tier * 0.28);
            g.LineTo(new Point(x + width * 0.78, top + height * 0.02));
            g.LineTo(new Point(x + width * 0.45, top));
        }
        g.LineTo(new Point(x + half, foot));
        g.EndFigure(true);
    }

    private static double CrestAt(Point[] line, double x)
    {
        var step = line.Length > 1 ? line[1].X - line[0].X : 1;
        var i = Math.Clamp((int)(x / step), 0, line.Length - 2);
        var t = Math.Clamp((x - line[i].X) / step, 0, 1);
        return line[i].Y + (line[i + 1].Y - line[i].Y) * t;
    }

    /// <summary>A repeatable pseudo-random value in [0, 1) for an integer and a seed.</summary>
    internal static double Hash(int i, int seed)
    {
        unchecked
        {
            var v = (uint)(i * 374761393 + seed * 668265263);
            v = (v ^ (v >> 13)) * 1274126177u;
            v ^= v >> 16;
            return (v & 0xFFFFFF) / (double)0x1000000;
        }
    }

    private static double ValueNoise(double x, int seed)
    {
        var i = (int)Math.Floor(x);
        var f = x - i;
        var t = f * f * (3 - 2 * f);
        var a = Hash(i, seed);
        var b = Hash(i + 1, seed);
        return a + (b - a) * t;
    }

    /// <summary>Fractal value noise in [0, 1]: <paramref name="octaves"/> layers, each twice as fine and half as strong.</summary>
    internal static double Fbm(double x, int seed, int octaves)
    {
        double sum = 0;
        double amplitude = 1;
        double norm = 0;
        var frequency = 1.0;
        for (var o = 0; o < octaves; o++)
        {
            sum += ValueNoise(x * frequency, seed + o * 17) * amplitude;
            norm += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }
        return sum / norm;
    }
}
