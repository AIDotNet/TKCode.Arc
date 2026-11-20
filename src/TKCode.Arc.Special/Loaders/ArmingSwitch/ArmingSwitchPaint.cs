using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Brush helpers for the arming switch drawing: CSS-style linear gradients at an angle over a box of a given aspect,
/// radial ellipses and conic sweeps, all immutable so the drawing allocates nothing per frame.
/// </summary>
internal static class ArmingSwitchPaint
{
    /// <summary>A CSS <c>linear-gradient(angle, ...)</c> over a box <paramref name="width"/> × <paramref name="height"/> (0° up, 90° right).</summary>
    public static IBrush Linear(double angle, double width, double height, params (double Offset, Color Color)[] stops)
    {
        var a = angle * Math.PI / 180;
        var dx = Math.Sin(a);
        var dy = -Math.Cos(a);
        var length = (Math.Abs(width * dx) + Math.Abs(height * dy)) / 2;
        var cx = width / 2;
        var cy = height / 2;
        var start = new RelativePoint((cx - (dx * length)) / width, (cy - (dy * length)) / height, RelativeUnit.Relative);
        var end = new RelativePoint((cx + (dx * length)) / width, (cy + (dy * length)) / height, RelativeUnit.Relative);
        return new ImmutableLinearGradientBrush(Stops(stops), 1, null, null, GradientSpreadMethod.Pad, start, end);
    }

    /// <summary>A radial ellipse centred at (<paramref name="cx"/>, <paramref name="cy"/>) with radii as fractions of the box.</summary>
    public static IBrush Radial(double cx, double cy, double rx, double ry, params (double Offset, Color Color)[] stops)
    {
        var centre = new RelativePoint(cx, cy, RelativeUnit.Relative);
        return new ImmutableRadialGradientBrush(Stops(stops), 1, null, null, GradientSpreadMethod.Pad, centre, centre,
            new RelativeScalar(rx, RelativeUnit.Relative), new RelativeScalar(ry, RelativeUnit.Relative));
    }

    /// <summary>A conic sweep from <paramref name="angle"/> degrees (0 up, clockwise) around the box's centre.</summary>
    public static IBrush Conic(double angle, params (double Offset, Color Color)[] stops) =>
        new ConicGradientBrush
        {
            Angle = angle,
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientStops = Mutable(stops)
        }.ToImmutable();

    public static IBrush Solid(Color color) => new ImmutableSolidColorBrush(color);

    public static IPen Pen(Color color, double thickness, PenLineCap cap = PenLineCap.Flat) =>
        new ImmutablePen(new ImmutableSolidColorBrush(color), thickness, null, cap);

    private static IReadOnlyList<ImmutableGradientStop> Stops((double Offset, Color Color)[] stops)
    {
        var list = new ImmutableGradientStop[stops.Length];
        for (var i = 0; i < stops.Length; i++)
        {
            list[i] = new ImmutableGradientStop(stops[i].Offset, stops[i].Color);
        }
        return list;
    }

    private static GradientStops Mutable((double Offset, Color Color)[] stops)
    {
        var list = new GradientStops();
        foreach (var (offset, color) in stops)
        {
            list.Add(new GradientStop(color, offset));
        }
        return list;
    }

    /// <summary>The colour with its alpha scaled by <paramref name="alpha"/>.</summary>
    public static Color A(Color color, double alpha) => Color.FromArgb((byte)Math.Clamp(Math.Round(color.A * alpha), 0, 255), color.R, color.G, color.B);

    /// <summary>The colour with its OKLCH lightness multiplied by <paramref name="factor"/> (Arc's color-mix lighten / darken).</summary>
    public static Color Shade(Color color, double factor)
    {
        var (l, c, h) = Oklch.FromColor(color);
        return Oklch.ToColor(Math.Clamp(l * factor, 0, 1), c, h, color.A / 255.0);
    }

    /// <summary>The colour mixed towards white by <paramref name="t"/> (in OKLab).</summary>
    public static Color Lighten(Color color, double t) => Oklch.MixOklab(color, Colors.White, t);

    /// <summary>The colour mixed towards black by <paramref name="t"/> (in OKLab).</summary>
    public static Color Darken(Color color, double t) => Oklch.MixOklab(color, Colors.Black, t);
}
