using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc;

/// <summary>Ink colors of <see cref="SignaturePad"/>.</summary>
public enum SignatureInkColor
{
    /// <summary>Black ink (the theme's foreground on screen, #17171a when exported).</summary>
    Black,

    /// <summary>Blue ink (#1d3fae when exported).</summary>
    Blue,

    /// <summary>Violet ink (#5b35c8 when exported).</summary>
    Violet
}

/// <summary>Nib sizes of <see cref="SignaturePad"/>: 3.4, 5.6 and 8.4 pad units.</summary>
public enum SignatureInkWidth
{
    /// <summary>A fine nib (3.4 units).</summary>
    Fine,

    /// <summary>A medium nib (5.6 units), the default.</summary>
    Medium,

    /// <summary>A bold nib (8.4 units).</summary>
    Bold
}

/// <summary>A sampled point: position in pad units (600 × 260), pen pressure from 0 to 1 (or -1 when the device has none), and milliseconds since the stroke began.</summary>
/// <param name="X">Horizontal position in pad units.</param>
/// <param name="Y">Vertical position in pad units.</param>
/// <param name="Pressure">Pen pressure from 0 to 1, or -1 for mouse and touch input.</param>
/// <param name="Time">Milliseconds since the stroke began.</param>
public readonly record struct InkPoint(double X, double Y, double Pressure, double Time);

/// <summary>One stroke of a signature, stored in pad units so it scales with the pad and can be replayed.</summary>
/// <param name="Id">A stable identifier.</param>
/// <param name="Points">The sampled points.</param>
/// <param name="Color">The ink color.</param>
/// <param name="Width">The nib size.</param>
/// <param name="At">When the stroke began, in milliseconds; replay keeps the rhythm between strokes.</param>
public sealed record InkStroke(string Id, IReadOnlyList<InkPoint> Points, SignatureInkColor Color, SignatureInkWidth Width, double At);

/// <summary>Options of <see cref="SignatureExport.GetStrokeOutline"/>.</summary>
/// <param name="Size">Diameter of the stroke at half pressure, in pad units.</param>
/// <param name="Thinning">How much pressure changes the width, from 0 to 1.</param>
/// <param name="Streamline">How far each point trails the pointer, from 0 to 1: higher is smoother but lags more.</param>
/// <param name="SimulatePressure">Derive pressure from speed when the device reports none: fast is thin, slow is full.</param>
/// <param name="TaperStart">Length over which the start tapers, in pad units.</param>
/// <param name="TaperEnd">Length over which the end tapers, in pad units.</param>
/// <param name="Last">False while the stroke is still being drawn, so the end does not taper yet.</param>
public readonly record struct InkOutlineOptions(double Size, double Thinning, double Streamline, bool SimulatePressure, double TaperStart, double TaperEnd, bool Last)
{
    /// <summary>Arc's defaults for a nib of <paramref name="size"/>: thinning 0.62, streamline 0.42, simulated pressure.</summary>
    public static InkOutlineOptions For(double size) => new(size, 0.62, 0.42, true, 0, 0, true);
}

/// <summary>
/// Signature ink geometry and export, ported from Arc: variable width outlines in the spirit of perfect-freehand
/// (streamlined points, pressure or speed driven width, tapered ends, round joins and caps), drawn as one smooth
/// closed path of quadratic curves; SVG path data, standalone SVG, PNG bytes and bitmaps cropped to the ink.
/// </summary>
public static class SignatureExport
{
    /// <summary>Pad coordinate width; strokes are stored in a 600 × 260 space.</summary>
    public const double PadWidth = 600;

    /// <summary>Pad coordinate height.</summary>
    public const double PadHeight = 260;

    /// <summary>The nib diameter of a width, in pad units.</summary>
    public static double NibSize(SignatureInkWidth width) => width switch
    {
        SignatureInkWidth.Fine => 3.4,
        SignatureInkWidth.Bold => 8.4,
        _ => 5.6
    };

    /// <summary>The print ink color used by exports.</summary>
    public static Color PrintColor(SignatureInkColor color) => color switch
    {
        SignatureInkColor.Blue => Color.FromRgb(0x1d, 0x3f, 0xae),
        SignatureInkColor.Violet => Color.FromRgb(0x5b, 0x35, 0xc8),
        _ => Color.FromRgb(0x17, 0x17, 0x1a)
    };

    private static string PrintHex(SignatureInkColor color) => color switch
    {
        SignatureInkColor.Blue => "#1d3fae",
        SignatureInkColor.Violet => "#5b35c8",
        _ => "#17171a"
    };

    private readonly record struct Sample(Point At, double Pressure, double Length, Vector Direction);

    private static Vector Normalize(Vector v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        return length > 0 ? new Vector(v.X / length, v.Y / length) : default;
    }

    private static Vector Perp(Vector v) => new(v.Y, -v.X);

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static Vector Lerp(Vector a, Vector b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static double Dot(Vector a, Vector b) => a.X * b.X + a.Y * b.Y;

    private static Point RotateAround(Point p, Point c, double r)
    {
        var s = Math.Sin(r);
        var co = Math.Cos(r);
        var px = p.X - c.X;
        var py = p.Y - c.Y;
        return new Point(px * co - py * s + c.X, px * s + py * co + c.Y);
    }

    private static double EaseOut(double t) => t * (2 - t);

    /// <summary>The outline polygon of a variable width stroke through <paramref name="input"/>.</summary>
    public static IReadOnlyList<Point> GetStrokeOutline(IReadOnlyList<InkPoint> input, InkOutlineOptions options)
    {
        if (input.Count == 0)
        {
            return Array.Empty<Point>();
        }
        var size = options.Size;
        var thinning = options.Thinning;
        var streamline = options.Streamline;
        var usePen = !options.SimulatePressure && input.Any(point => point.Pressure > 0);
        var head = input[0];

        // Streamline: each point eases toward the raw input, which removes jitter without a smoothing pass.
        var points = new List<Sample>(input.Count);
        var previous = new Point(head.X, head.Y);
        double length = 0;
        points.Add(new Sample(previous, usePen ? head.Pressure : 0.5, 0, new Vector(1, 0)));
        for (var i = 1; i < input.Count; i++)
        {
            var sample = input[i];
            var raw = new Point(sample.X, sample.Y);
            var at = i == input.Count - 1 && options.Last ? raw : Lerp(previous, raw, 1 - streamline);
            var d = Distance(at, previous);
            if (d < 0.4)
            {
                continue;
            }
            length += d;
            points.Add(new Sample(at, usePen ? sample.Pressure : 0.5, length, Normalize(previous - at)));
            previous = at;
        }
        if (points.Count > 1)
        {
            points[0] = points[0] with { Direction = points[1].Direction };
        }
        var start = points[0];
        var total = length;

        // A dot: a round mark sized by the first pressure.
        if (points.Count == 1)
        {
            var r = Math.Max(0.6, size * (0.5 - thinning * (0.5 - start.Pressure)) * 0.9);
            var dot = new Point[18];
            for (var i = 0; i < 18; i++)
            {
                var a = i / 18.0 * Math.PI * 2;
                dot[i] = new Point(start.At.X + Math.Cos(a) * r, start.At.Y + Math.Sin(a) * r);
            }
            return dot;
        }

        var left = new List<Point>(points.Count + 16);
        var right = new List<Point>(points.Count + 16);
        var pressure = start.Pressure;
        var previousDirection = start.Direction;
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            if (!usePen)
            {
                // Speed thins the line: a long step between samples reads as a fast stroke.
                var step = i == 0 ? 0 : Distance(point.At, points[i - 1].At);
                var speed = Math.Min(1, step / size);
                var target = Math.Min(1, 1 - speed);
                pressure = Math.Min(1, pressure + (target - pressure) * speed * 0.3);
            }
            else
            {
                pressure = point.Pressure;
            }
            var radius = Math.Max(0.25, size * (0.5 - thinning * (0.5 - pressure)));
            var ts = options.TaperStart > 0 ? EaseOut(Math.Min(1, point.Length / options.TaperStart)) : 1;
            var te = options.TaperEnd > 0 && options.Last ? EaseOut(Math.Min(1, (total - point.Length) / options.TaperEnd)) : 1;
            radius = Math.Max(0.12 * size, radius * Math.Min(ts, te));

            var next = i + 1 < points.Count ? points[i + 1].Direction : point.Direction;
            var offset = Perp(Lerp(next, point.Direction, 0.5)) * radius;
            // A sharp turn: sweep round the corner so the outline does not pinch.
            if (i > 0 && i < points.Count - 1 && Dot(point.Direction, next) < -0.2)
            {
                var corner = Perp(previousDirection) * radius;
                for (var t = 0.0; t <= 1; t += 1.0 / 12)
                {
                    left.Add(RotateAround(point.At - corner, point.At, Math.PI * t));
                    right.Add(RotateAround(point.At + corner, point.At, -Math.PI * t));
                }
            }
            else
            {
                left.Add(point.At - offset);
                right.Add(point.At + offset);
            }
            previousDirection = point.Direction;
        }

        // Round caps: half circles swept around the first and last points, on the side facing away from the stroke.
        static List<Point> Cap(Point center, Point from, Point to, Vector away)
        {
            var result = new List<Point>(12);
            var r = Distance(center, from);
            var begin = Math.Atan2(from.Y - center.Y, from.X - center.X);
            var mid = begin + Math.PI / 2;
            var sign = Dot(new Vector(Math.Cos(mid), Math.Sin(mid)), away) >= 0 ? 1 : -1;
            for (var k = 1; k < 12; k++)
            {
                var a = begin + sign * Math.PI * k / 12;
                result.Add(new Point(center.X + Math.Cos(a) * r, center.Y + Math.Sin(a) * r));
            }
            result.Add(to);
            return result;
        }
        var end = points[^1];
        var endCap = Cap(end.At, left[^1], right[^1], end.Direction * -1);
        var startCap = Cap(start.At, right[0], left[0], start.Direction);
        var outline = new List<Point>(left.Count + right.Count + 24);
        outline.AddRange(left);
        outline.AddRange(endCap);
        for (var i = right.Count - 1; i >= 0; i--)
        {
            outline.Add(right[i]);
        }
        outline.AddRange(startCap);
        return outline;
    }

    /// <summary>Arc's outline options for a stroke: tapered start, a long tapered end for simulated pressure, no end taper for pens.</summary>
    public static InkOutlineOptions OptionsFor(InkStroke stroke, bool last) => OptionsFor(stroke.Width, stroke.Points, last);

    internal static InkOutlineOptions OptionsFor(SignatureInkWidth width, IReadOnlyList<InkPoint> points, bool last)
    {
        var size = NibSize(width);
        var pen = points.Any(point => point.Pressure > 0);
        return new InkOutlineOptions(size, 0.62, 0.42, !pen, size * 1.5, pen ? 0 : size * 5, last);
    }

    private static string Round(double value) => (Math.Round(value * 100) / 100).ToString(CultureInfo.InvariantCulture);

    /// <summary>An outline as one smooth closed SVG path, with quadratic curves through the midpoints of its edges.</summary>
    public static string OutlineToPathData(IReadOnlyList<Point> points)
    {
        if (points.Count < 3)
        {
            return string.Empty;
        }
        var sb = new StringBuilder(points.Count * 24);
        sb.Append('M').Append(Round(points[0].X)).Append(' ').Append(Round(points[0].Y)).Append(" Q");
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            sb.Append(Round(a.X)).Append(' ').Append(Round(a.Y)).Append(' ')
              .Append(Round((a.X + b.X) / 2)).Append(' ').Append(Round((a.Y + b.Y) / 2)).Append(' ');
        }
        sb.Append('Z');
        return sb.ToString();
    }

    /// <summary>The visible points of a stroke replayed up to <paramref name="upTo"/> ms, and whether it is complete.</summary>
    internal static (IReadOnlyList<InkPoint> Points, bool Done) PointsUpTo(InkStroke stroke, double upTo)
    {
        var all = stroke.Points;
        var done = all.Count == 0 || upTo >= all[^1].Time;
        if (done)
        {
            return (all, true);
        }
        var visible = new List<InkPoint>();
        foreach (var point in all)
        {
            if (point.Time <= upTo)
            {
                visible.Add(point);
            }
        }
        return (visible, false);
    }

    /// <summary>The SVG path data of a stroke (in pad units), or of its first <paramref name="upTo"/> ms while it is replayed.</summary>
    public static string ToSvgPathData(InkStroke stroke, double upTo = double.PositiveInfinity)
    {
        var (points, done) = PointsUpTo(stroke, upTo);
        return OutlineToPathData(GetStrokeOutline(points, OptionsFor(stroke.Width, points, done)));
    }

    /// <summary>A filled geometry of the outline (in pad units), the same curve as the SVG path.</summary>
    public static Geometry ToGeometry(IReadOnlyList<Point> outline)
    {
        var geometry = new StreamGeometry();
        if (outline.Count < 3)
        {
            return geometry;
        }
        using var context = geometry.Open();
        context.SetFillRule(FillRule.NonZero);
        context.BeginFigure(outline[0], true);
        for (var i = 0; i < outline.Count; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Count];
            context.QuadraticBezierTo(a, new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2));
        }
        context.EndFigure(true);
        return geometry;
    }

    /// <summary>The filled geometry of a whole stroke, in pad units.</summary>
    public static Geometry ToGeometry(InkStroke stroke) => ToGeometry(GetStrokeOutline(stroke.Points, OptionsFor(stroke, true)));

    /// <summary>Tight bounds of the ink in pad units, padded by 16.</summary>
    public static Rect InkBounds(IReadOnlyList<InkStroke> strokes)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var stroke in strokes)
        {
            foreach (var point in stroke.Points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }
        if (!double.IsFinite(minX))
        {
            return default;
        }
        const double pad = 16;
        return new Rect(Math.Floor(minX - pad), Math.Floor(minY - pad), Math.Ceiling(maxX - minX + pad * 2), Math.Ceiling(maxY - minY + pad * 2));
    }

    /// <summary>A standalone SVG of the signature, cropped to the ink, in print ink colors (empty without strokes).</summary>
    public static string ToSvg(IReadOnlyList<InkStroke> strokes)
    {
        if (strokes.Count == 0)
        {
            return string.Empty;
        }
        var box = InkBounds(strokes);
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"")
          .Append(Round(box.X)).Append(' ').Append(Round(box.Y)).Append(' ').Append(Round(box.Width)).Append(' ').Append(Round(box.Height))
          .Append("\" width=\"").Append(Round(box.Width)).Append("\" height=\"").Append(Round(box.Height)).Append("\">");
        foreach (var stroke in strokes)
        {
            sb.Append("<path d=\"").Append(ToSvgPathData(stroke)).Append("\" fill=\"").Append(PrintHex(stroke.Color)).Append("\"/>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>A transparent bitmap of the signature, cropped to the ink, at <paramref name="scale"/> times pad resolution.</summary>
    /// <exception cref="InvalidOperationException">There are no strokes.</exception>
    public static RenderTargetBitmap ToBitmap(IReadOnlyList<InkStroke> strokes, double scale = 3)
    {
        if (strokes.Count == 0)
        {
            throw new InvalidOperationException("Nothing to export");
        }
        var box = InkBounds(strokes);
        var size = new PixelSize(Math.Max(1, (int)Math.Ceiling(box.Width * scale)), Math.Max(1, (int)Math.Ceiling(box.Height * scale)));
        var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            using (context.PushTransform(Matrix.CreateTranslation(-box.X, -box.Y) * Matrix.CreateScale(scale, scale)))
            {
                foreach (var stroke in strokes)
                {
                    context.DrawGeometry(new SolidColorBrush(PrintColor(stroke.Color)), null, ToGeometry(stroke));
                }
            }
        }
        return bitmap;
    }

    /// <summary>PNG bytes of <see cref="ToBitmap"/>.</summary>
    /// <exception cref="InvalidOperationException">There are no strokes.</exception>
    public static byte[] ToPng(IReadOnlyList<InkStroke> strokes, double scale = 3)
    {
        using var bitmap = ToBitmap(strokes, scale);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }
}
