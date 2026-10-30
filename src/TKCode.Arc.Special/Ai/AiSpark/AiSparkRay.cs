using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One tapered ray of <see cref="AiSpark"/>, pointing up from the origin: a sharp tip at <c>-length</c>, convex sides
/// that meet a round base of radius <c>halfWidth</c> centered at <c>-start</c>. The path is built once and its points are
/// moved in place each frame, so drawing a ray allocates nothing.
/// </summary>
internal sealed class AiSparkRay
{
    private readonly PathFigure _figure = new() { IsClosed = true, IsFilled = true };
    private readonly QuadraticBezierSegment _right = new();
    private readonly ArcSegment _base = new() { SweepDirection = SweepDirection.Clockwise };
    private readonly QuadraticBezierSegment _left = new();
    private double _start = double.NaN;
    private double _length = double.NaN;
    private double _halfWidth = double.NaN;

    public AiSparkRay()
    {
        _figure.Segments!.Add(_right);
        _figure.Segments.Add(_base);
        _figure.Segments.Add(_left);
        Geometry.Figures!.Add(_figure);
    }

    /// <summary>The ray's path in its own coordinates (pointing up from the origin).</summary>
    public PathGeometry Geometry { get; } = new();

    /// <summary>Moves the path's points to a ray whose base is centered <paramref name="start"/> from the origin.</summary>
    public void Update(double start, double length, double halfWidth)
    {
        // A change below a hundredth of a unit is invisible at any size: skip it so a resting mark does no work.
        if (Math.Abs(start - _start) < 0.01 && Math.Abs(length - _length) < 0.01 && Math.Abs(halfWidth - _halfWidth) < 0.01)
        {
            return;
        }
        _start = start;
        _length = length = Math.Max(length, start + halfWidth * 0.5);
        _halfWidth = halfWidth;
        var shoulder = -(start + (length - start) * 0.5);
        _figure.StartPoint = new Point(0, -length);
        _right.Point1 = new Point(halfWidth, shoulder);
        _right.Point2 = new Point(halfWidth, -start);
        _base.Point = new Point(-halfWidth, -start);
        _base.Size = new Size(halfWidth, halfWidth);
        _left.Point1 = new Point(-halfWidth, shoulder);
        _left.Point2 = new Point(0, -length);
    }
}
