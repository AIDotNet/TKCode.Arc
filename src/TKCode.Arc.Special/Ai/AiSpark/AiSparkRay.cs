using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One tapered ray of <see cref="AiSpark"/>, pointing up from the origin: a sharp tip at <c>-length</c>, convex sides
/// that meet a round base of radius <c>halfWidth</c> centered at <c>-start</c>. A frame only moves the ray's three
/// numbers; the path is built from them when the ray is next drawn (once, and only if one moved), so a resting ray
/// builds nothing and a moving one raises no change notifications.
/// </summary>
internal sealed class AiSparkRay
{
    private double _start = double.NaN;
    private double _length = double.NaN;
    private double _halfWidth = double.NaN;
    private StreamGeometry? _geometry;

    /// <summary>The ray's path in its own coordinates (pointing up from the origin).</summary>
    public Geometry Geometry => _geometry ??= Build();

    /// <summary>Moves the path's points to a ray whose base is centered <paramref name="start"/> from the origin.</summary>
    public void Update(double start, double length, double halfWidth)
    {
        // A change below a hundredth of a unit is invisible at any size: skip it so a resting mark does no work.
        if (Math.Abs(start - _start) < 0.01 && Math.Abs(length - _length) < 0.01 && Math.Abs(halfWidth - _halfWidth) < 0.01)
        {
            return;
        }
        _start = start;
        _length = Math.Max(length, start + halfWidth * 0.5);
        _halfWidth = halfWidth;
        _geometry = null;
    }

    private StreamGeometry Build()
    {
        // Never updated (not drawn by the mark): an empty ray rather than one made of NaNs.
        var start = double.IsNaN(_start) ? 0 : _start;
        var length = double.IsNaN(_length) ? 0 : _length;
        var halfWidth = double.IsNaN(_halfWidth) ? 0 : _halfWidth;
        var shoulder = -(start + (length - start) * 0.5);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.SetFillRule(FillRule.EvenOdd);
            path.BeginFigure(new Point(0, -length), true);
            path.QuadraticBezierTo(new Point(halfWidth, shoulder), new Point(halfWidth, -start));
            path.ArcTo(new Point(-halfWidth, -start), new Size(halfWidth, halfWidth), 0, false, SweepDirection.Clockwise);
            path.QuadraticBezierTo(new Point(-halfWidth, shoulder), new Point(0, -length));
            path.EndFigure(true);
        }
        return geometry;
    }
}
