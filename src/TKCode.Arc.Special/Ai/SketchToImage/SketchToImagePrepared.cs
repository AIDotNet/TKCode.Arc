using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A flattened open or closed polyline with its cumulative lengths: a pencil stroke or a brush sweep. It draws any leading
/// part of itself segment by segment with a cached pen (progressive trimming without building a geometry per frame) and
/// finds the point at a distance without allocating.
/// </summary>
internal sealed class SketchToImagePath
{
    private readonly Point[] _points;
    private readonly double[] _cumulative;

    public SketchToImagePath(IReadOnlyList<Point> points)
    {
        _points = points.Count == 0 ? [default] : [.. points];
        _cumulative = new double[_points.Length];
        for (var i = 1; i < _points.Length; i++)
        {
            var dx = _points[i].X - _points[i - 1].X;
            var dy = _points[i].Y - _points[i - 1].Y;
            _cumulative[i] = _cumulative[i - 1] + Math.Sqrt(dx * dx + dy * dy);
        }
        Length = _cumulative[^1];
        Full = new PolylineGeometry(_points, false);
    }

    /// <summary>The whole polyline as one cached geometry (a finished stroke).</summary>
    public Geometry Full { get; }

    public double Length { get; }

    public Point Start => _points[0];

    public Point End => _points[^1];

    public int Count => _points.Length;

    public Point PointAt(double distance)
    {
        if (distance <= 0 || _points.Length == 1)
        {
            return _points[0];
        }
        if (distance >= Length)
        {
            return _points[^1];
        }
        var index = Array.BinarySearch(_cumulative, distance);
        if (index >= 0)
        {
            return _points[index];
        }
        index = ~index;
        var a = _points[index - 1];
        var b = _points[index];
        var span = _cumulative[index] - _cumulative[index - 1];
        var f = span <= 0 ? 0 : (distance - _cumulative[index - 1]) / span;
        return new Point(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
    }

    /// <summary>Draws the first <paramref name="distance"/> units of the polyline. Round caps make the joints round.</summary>
    public void Draw(DrawingContext context, IPen pen, double distance)
    {
        if (distance <= 0)
        {
            return;
        }
        if (distance >= Length)
        {
            context.DrawGeometry(null, pen, Full);
            return;
        }
        for (var i = 1; i < _points.Length; i++)
        {
            if (_cumulative[i] <= distance)
            {
                context.DrawLine(pen, _points[i - 1], _points[i]);
                continue;
            }
            context.DrawLine(pen, _points[i - 1], PointAt(distance));
            break;
        }
    }
}

/// <summary>One pencil stroke and its slot in the sketching stage (fractions of the stage).</summary>
internal sealed class SketchToImageStroke(SketchToImagePath path)
{
    public SketchToImagePath Path { get; } = path;

    /// <summary>Where the pen starts travelling here from the previous stroke's end.</summary>
    public double TravelStart { get; set; }

    public double Start { get; set; }

    public double End { get; set; }

    /// <summary>Where the pen comes from (the previous stroke's end; the stroke's own start for the first).</summary>
    public Point From { get; set; }
}

/// <summary>One shape of a region, ready to draw: its geometry, the flat color's brush and pen, and its finished brush.</summary>
internal sealed class SketchToImagePreparedShape
{
    public required Geometry Geometry { get; init; }

    public IBrush? Flat { get; init; }

    /// <summary>The round brush in the flat color, as wide as the region's sweep.</summary>
    public IPen? Brush { get; init; }

    public IBrush? Finish { get; init; }
}

/// <summary>One region and its slot in the blocking stage (fractions of the stage).</summary>
internal sealed class SketchToImagePreparedRegion
{
    public required IReadOnlyList<SketchToImagePreparedShape> Shapes { get; init; }

    public required SketchToImagePath Sweep { get; init; }

    public double BrushWidth { get; init; }

    /// <summary>Everything the sweep covers: a finished region is drawn clipped to it, so finishing never fills more than the brush did.</summary>
    public Rect Swept { get; init; }

    public required IBrush Tip { get; init; }

    public double TravelStart { get; set; }

    public double Start { get; set; }

    public double End { get; set; }

    public Point From { get; set; }
}

/// <summary>
/// A <see cref="SketchToImageScene"/> made ready to draw once: strokes flattened with their time slots (by length, plus the
/// pen's travel between them), each region's geometry, pens and zigzag sweep with its slot, and the light. Built when the
/// scene changes, never per frame.
/// </summary>
internal sealed class SketchToImagePrepared
{
    /// <summary>The share of the sketching stage the strokes take; the pen lifts away in the rest.</summary>
    public const double StrokesShare = 0.96;

    /// <summary>The share of the blocking stage the sweeps take; the brush lifts away in the rest.</summary>
    public const double SweepsShare = 0.97;

    /// <summary>A pen travel costs this much stroke length per unit of distance (and at least <see cref="MinTravel"/>).</summary>
    public const double TravelCost = 0.35;

    public const double MinTravel = 14;

    private SketchToImagePrepared(SketchToImageScene scene, List<SketchToImageStroke> strokes, List<SketchToImagePreparedRegion> regions)
    {
        Scene = scene;
        Strokes = strokes;
        Regions = regions;
        Light = scene.Light ?? new Point(SketchToImageScene.CanvasWidth * 0.72, SketchToImageScene.CanvasHeight * 0.38);
    }

    public SketchToImageScene Scene { get; }

    public IReadOnlyList<SketchToImageStroke> Strokes { get; }

    public IReadOnlyList<SketchToImagePreparedRegion> Regions { get; }

    public Point Light { get; }

    public static SketchToImagePrepared From(SketchToImageScene scene)
    {
        var strokes = new List<SketchToImageStroke>();
        foreach (var line in scene.Lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            foreach (var contour in PathMorphShape.Parse(line).Contours)
            {
                if (contour.Points.Count > 1 && contour.Length > 0.01)
                {
                    strokes.Add(new SketchToImageStroke(new SketchToImagePath(contour.Points)));
                }
            }
        }
        ScheduleStrokes(strokes);

        var regions = new List<SketchToImagePreparedRegion>();
        foreach (var region in scene.Regions)
        {
            var sweep = Sweep(region.Brush, out var width);
            var shapes = new List<SketchToImagePreparedShape>();
            Color? firstFill = null;
            foreach (var shape in region.Shapes)
            {
                if (string.IsNullOrWhiteSpace(shape.Data))
                {
                    continue;
                }
                ImmutableSolidColorBrush? flat = shape.Fill is { } fill ? new ImmutableSolidColorBrush(fill) : null;
                firstFill ??= shape.Fill;
                shapes.Add(new SketchToImagePreparedShape
                {
                    Geometry = StreamGeometry.Parse(shape.Data),
                    Flat = flat,
                    Brush = flat is null ? null : new ImmutablePen(flat, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round),
                    Finish = shape.Finish ?? flat,
                });
            }
            var tip = region.Tip ?? firstFill ?? Colors.Gray;
            regions.Add(new SketchToImagePreparedRegion
            {
                Shapes = shapes,
                Sweep = sweep,
                BrushWidth = width,
                Swept = region.Brush.Inflate(width / 2),
                Tip = new ImmutableSolidColorBrush(tip),
            });
        }
        ScheduleRegions(regions);
        return new SketchToImagePrepared(scene, strokes, regions);
    }

    /// <summary>Gives each stroke a slot by its length and each gap a slot by the distance the pen travels.</summary>
    private static void ScheduleStrokes(List<SketchToImageStroke> strokes)
    {
        double cursor = 0;
        var previous = strokes.Count > 0 ? strokes[0].Path.Start : default;
        foreach (var stroke in strokes)
        {
            var travel = cursor == 0 ? 0 : Math.Max(MinTravel, TravelCost * Distance(previous, stroke.Path.Start));
            stroke.From = previous;
            stroke.TravelStart = cursor;
            cursor += travel;
            stroke.Start = cursor;
            cursor += stroke.Path.Length;
            stroke.End = cursor;
            previous = stroke.Path.End;
        }
        var k = cursor > 0 ? StrokesShare / cursor : 0;
        foreach (var stroke in strokes)
        {
            stroke.TravelStart *= k;
            stroke.Start *= k;
            stroke.End *= k;
        }
    }

    private static void ScheduleRegions(List<SketchToImagePreparedRegion> regions)
    {
        double cursor = 0;
        var previous = regions.Count > 0 ? regions[0].Sweep.Start : default;
        foreach (var region in regions)
        {
            var travel = cursor == 0 ? 0 : Math.Max(MinTravel, TravelCost * Distance(previous, region.Sweep.Start));
            region.From = previous;
            region.TravelStart = cursor;
            cursor += travel;
            region.Start = cursor;
            cursor += region.Sweep.Length;
            region.End = cursor;
            previous = region.Sweep.End;
        }
        var k = cursor > 0 ? SweepsShare / cursor : 0;
        foreach (var region in regions)
        {
            region.TravelStart *= k;
            region.Start *= k;
            region.End *= k;
        }
    }

    /// <summary>
    /// The path the round brush sweeps through a rectangle: horizontal passes back and forth, one per band, the brush a
    /// little wider than a band so the passes overlap. The passes run a few units past the sides so the caps cover the corners.
    /// </summary>
    internal static SketchToImagePath Sweep(Rect rect, out double width)
    {
        var rows = Math.Max(2, (int)Math.Ceiling(rect.Height / 44));
        var band = rect.Height / rows;
        width = band * 1.4;
        var points = new List<Point>(rows * 2);
        for (var row = 0; row < rows; row++)
        {
            var y = rect.Top + band * (row + 0.5);
            var left = new Point(rect.Left + 2, y);
            var right = new Point(rect.Right - 2, y);
            points.Add(row % 2 == 0 ? left : right);
            points.Add(row % 2 == 0 ? right : left);
        }
        return new SketchToImagePath(points);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
